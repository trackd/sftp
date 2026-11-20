---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-20-2026
PlatyPS schema version: 2024-05-01
title: Receive-SftpFile
---

# Receive-SftpFile

## SYNOPSIS

Downloads files from the server.

## SYNTAX

### Path (Default)

```
Receive-SftpFile [-Path] <string[]> [[-Destination] <string>] [-Force] [-PreserveTimestamp]
 [-Recurse] [-HostName <string>] [-Port <int>] [-Credential <pscredential>] [-UserName <string>]
 [-KeyFile <string[]>] [-KeyPassphrase <securestring>] [-HostKeyFingerprint <string[]>]
 [-KnownHostsFile <string>] [-SkipHostKeyCheck] [-Session <SftpSession>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### LiteralPath

```
Receive-SftpFile [[-Destination] <string>] -LiteralPath <string[]> [-SessionName <string>] [-Force]
 [-PreserveTimestamp] [-Recurse] [-HostName <string>] [-Port <int>] [-Credential <pscredential>]
 [-UserName <string>] [-KeyFile <string[]>] [-KeyPassphrase <securestring>]
 [-HostKeyFingerprint <string[]>] [-KnownHostsFile <string>] [-SkipHostKeyCheck]
 [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Downloads files from the server to a folder on your computer. Files that already exist on your
computer are not replaced unless you add -Force.

Each file returns a result; check its Success property to see whether it was downloaded.

To connect just for this command, give the server with -HostName (and -Credential or -KeyFile if needed) instead of -Session.

## EXAMPLES

### Download one file to the current folder

```powershell
Receive-SftpFile -Path /reports/sales.csv
```

### Download a whole folder

```powershell
Receive-SftpFile -Path /reports -Destination ./FromSftp -Recurse
```

### Download all CSV files in a folder

```powershell
Receive-SftpFile -Path /reports/*.csv -Destination ./FromSftp
```

### Download files, then delete them from the server

Only files that downloaded successfully are deleted.

```powershell
Get-SftpFile -Path /reports -File |
    Receive-SftpFile -Destination ./FromSftp |
    Where-Object Success |
    Remove-SftpFile
```

### Download without connecting first

The connection is opened for this command and closed when it finishes, even if the download fails.

```powershell
Receive-SftpFile -HostName backup@nas -Path /backup/latest.tar.gz -Destination ./Backups
```

### Download from an Sftp drive

```powershell
New-PSDrive -Name logs -PSProvider Sftp -Root /var/log -Session web01
Receive-SftpFile -Path logs:\nginx\*.log -Destination ./Logs
```
## PARAMETERS

### -Credential

User name and password, with -HostName.

```yaml
Type: System.Management.Automation.PSCredential
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Destination

Where to download: an existing folder on your computer, or a new file name when you download one file. The default is the current folder.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Force

Replace files that already exist on your computer.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -HostKeyFingerprint

Accepted host key fingerprints, with -HostName. Without a host key option, ~/.ssh/known_hosts is used, as with Connect-SftpServer.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Fingerprint
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -HostName

Connect to this server for this command only, instead of using an open session. Accepts a name, an IP address, user@host or a Host alias from ~/.ssh/config, the same as Connect-SftpServer. The connection is closed when the command finishes, fails or is stopped, and does not become the current session.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ComputerName
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -KeyFile

Private key files, with -HostName. Without it, the keys from ~/.ssh/config and the default keys in ~/.ssh are tried.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -KeyPassphrase

The passphrase for encrypted private keys, with -HostName.

```yaml
Type: System.Security.SecureString
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -KnownHostsFile

An OpenSSH known_hosts file to check the host key against, with -HostName.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -LiteralPath

The same as -Path, but used exactly as written, so characters like [ and ] are not treated as wildcards. Accepts files piped from Get-SftpFile.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- FullName
- RemotePath
ParameterSets:
- Name: LiteralPath
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Path

The files on the server to download. Wildcards are allowed, for example /reports/*.csv. A path on an Sftp drive, such as prod:\reports\*.csv, uses that drive's session.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: true
Aliases: []
ParameterSets:
- Name: Path
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Port

The server port, with -HostName. Defaults to the port in ~/.ssh/config, or 22.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PreserveTimestamp

Keep each file's modification time from the server.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Recurse

Download folders with everything in them, keeping the folder structure.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Session

The session to use: the object from Connect-SftpServer, or its name. By default the session the piped files came from is used, then the session of the Sftp drive in the path or of the current location, otherwise the most recent connection.

```yaml
Type: sftp.SftpSession
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SessionName

The name of the session to use. You rarely type this: it is filled in from the files and results you pipe in, so they go back to the server they came from.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: LiteralPath
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SkipHostKeyCheck

Accept any host key, with -HostName. Only for testing: you cannot tell whether you reached the real server.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -UserName

The user name for key login, with -HostName. Defaults to the user in -HostName or ~/.ssh/config, or your local user name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

## INPUTS

### System.String

Files on the server, for example from Get-SftpFile.

## OUTPUTS

### sftp.SftpTransferResult

One result per file, showing whether it was downloaded.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)
