# ShadowUser

SAM (Security Account Manager) is a tool that demonstrates advanced user account manipulation techniques through registry operations. This tool creates hidden administrator accounts by cloning existing user credentials and manipulating Windows registry entries.

## Overview

ShadowUser exploits Windows user account management by:
- Creating temporary hidden administrator accounts
- Manipulating SAM registry entries to clone user credentials
- Bypassing standard user detection mechanisms
- Automatically enabling RDP access for persistence

This technique demonstrates that attackers can gain persistent access to Windows systems through registry manipulation.

## Features

- **Hidden User Creation**: Creates administrator accounts with names ending in `$` to reduce visibility
- **Credential Cloning**: Copies authentication data between user accounts via registry manipulation
- **SAM Registry Access**: Temporarily grants SYSTEM-level access to Security Account Manager
- **RDP Enablement**: Automatically configures Remote Desktop Protocol for remote access
- **Error Handling**: Robust exception handling with automatic cleanup on failure
- **Random Password Generation**: Creates secure random passwords for new accounts

### Registry Manipulation Process

```
1. Create temporary hidden user account (username$)
2. Grant SYSTEM full access to SAM registry hive
3. Export registry entries for target and clone users
4. Extract and clone F-value (credential data) between users
5. Remove temporary user account
6. Import modified registry entries
7. Restore SAM access permissions
8. Clean up temporary files
```

## Compilation

## Installation
- Download the project to your computer.

- Open in Visual Studio:
   ```bash
   start ShadowUser.sln
   ```

- Build the solution:
   - Set configuration to `Release`
   - Build → Build Solution (Ctrl+Shift+B)

- Locate the executable:
   ```
   .\bin\Release\ShadowUser.exe
   ```
## Usage

### Basic Syntax
```bash
ShadowUser.exe TargetUser CloneUser
```

### Parameters
- `<TargetUser>`: The existing user account to clone credentials from
- `<CloneUser>`: The existing user account to receive cloned credentials

### Examples

```bash
ShadowUser.exe victim_user attacker_account
```

### Output
The tool provides detailed progress information:
```
[*] Creating temporary hidden user...
[*] Granting SYSTEM full access to SAM hive...
[*] Exporting registry entries for target and clone users...
[*] Cloning F-value from target to clone user...
[*] Removing temporary hidden user...
[*] Restoring registry entries...
[*] Denying Administrators access to SAM hive...
[*] Cleaning up temporary files...
[*] Checking/enabling RDP...
[*] Shadow user created successfully.
[+] Clone user: standarduser
[+] Hidden user: administrator$
[+] Password: aB3$mK9@pL
```

## Configuration

### Registry Paths
The tool operates on these critical registry locations:
- `HKLM\SAM\SAM\Domains\Account\Users\Names\`
- `HKLM\SAM\SAM\Domains\Account\Users\{RID}\`
- `HKLM\SYSTEM\CurrentControlSet\Control\Terminal Server`

### Temporary Files
Temporary files are created in `C:\Windows\Temp\`:
- `{username}.reg` - Exported user registry entries
- `UsersF.reg` - F-value registry data
- `{tempid}.txt` - RID mapping information

### RDP Configuration
Automatically configures:
- Terminal Services startup type
- Windows Firewall Remote Desktop rules
- RDP port settings (default 3389)


### Attack Vector
This tool demonstrates several attack techniques:
- **Privilege Escalation**: Grants admin rights to standard users
- **Persistence**: Creates hidden accounts for long-term access
- **Evasion**: Uses registry manipulation to bypass user management tools
- **Lateral Movement**: Enables RDP for remote system access

## Code Analysis

### Key Functions

**User Management:**
```csharp
static void CreateUser(string username, string password)
static void DeleteUser(string username)
```

**Registry Operations:**
```csharp
static void ExportUserNameKey(string username, string tempId)
static void CloneUserFValue(string tempId)
static void ImportRegistryFiles(string hiddenUser)
```

**Permission Management:**
```csharp
static void GrantSystemFullAccessToSam()
static void DenyAdministratorsAccessToSam()
```

## Compatibility

| Windows Version | Compatibility | Notes |
|----------------|---------------|-------|
| Windows 7      | ✅ Full       | Tested and working |
| Windows 8/8.1  | ✅ Full       | Tested and working |
| Windows 10     | ✅ Full       | Tested and working |
| Windows 11     | ⚠️ Limited    | May require UAC bypass |
| Windows Server | ✅ Full       | 2008 R2 and newer |

## Troubleshooting

### Common Issues

**"Access Denied" Errors:**
- Ensure running as Administrator
- Check UAC settings
- Verify user account permissions

**Registry Access Failures:**
- Confirm SAM database is accessible
- Check for antivirus interference
- Verify .NET Framework installation

**RDP Configuration Issues:**
- Check Windows Firewall settings
- Verify Terminal Services status
- Confirm network connectivity

### Debug Mode
Enable verbose logging by modifying the source code to include additional Console.WriteLine statements in exception handlers.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Discalimer

**This tool is designed solely for educational purposes, security research, and authorized penetration testing.** The authors are not responsible for any misuse of this tool or any damage it may cause.