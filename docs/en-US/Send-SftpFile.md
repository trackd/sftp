---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-20-2026
PlatyPS schema version: 2024-05-01
title: Send-SftpFile
---

# Send-SftpFile

## SYNOPSIS

Uploads files to the server.

## SYNTAX

### Path (Default)

```
Send-SftpFile [-Path] <string[]> [[-Destination] <string>] [-Force] [-UseTemporaryName]
 [-PreserveTimestamp] [-Recurse] [-HostName <string>] [-Port <int>] [-Credential <pscredential>]
 [-UserName <string>] [-KeyFile <string[]>] [-KeyPassphrase <securestring>]
 [-HostKeyFingerprint <string[]>] [-KnownHostsFile <string>] [-SkipHostKeyCheck]
 [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### LiteralPath

```
Send-SftpFile [[-Destination] <string>] -LiteralPath <string[]> [-SessionName <string>] [-Force]
 [-UseTemporaryName] [-PreserveTimestamp] [-Recurse] [-HostName <string>] [-Port <int>]
 [-Credential <pscredential>] [-UserName <string>] [-KeyFile <string[]>]
 [-KeyPassphrase <securestring>] [-HostKeyFingerprint <string[]>] [-KnownHostsFile <string>]
 [-SkipHostKeyCheck] [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uploads files from your computer to a folder on the server. Files that already exist on the
server are not replaced unless you add -Force.

Each file returns a result; check its Success property to see whether it was uploaded.

To connect just for this command, give the server with -HostName (and -Credential or -KeyFile if needed) instead of -Session.

## EXAMPLES

### Upload all CSV files from a local folder

```powershell
Send-SftpFile -Path ./ToSftp/*.csv -Destination /upload
```

### Upload a whole folder

The folder is created on the server, with its subfolders.

```powershell
Send-SftpFile -Path ./Reports -Destination /upload -Recurse
```

### Upload a file under a new name, replacing it if it exists

```powershell
Send-SftpFile -Path ./ToSftp/prices.xml -Destination /upload/prices-today.xml -Force
```

### Upload the files Get-ChildItem finds, then move the local copies to a Sent folder

```powershell
Get-ChildItem ./ToSftp -Filter *.csv |
    Send-SftpFile -Destination /upload |
    Where-Object Success |
    ForEach-Object { Move-Item -LiteralPath $_.Source -Destination ./Sent }
```

### Upload without connecting first

The connection is opened for this command and closed when it finishes. Host aliases and keys from ~/.ssh/config work as with Connect-SftpServer.

```powershell
Send-SftpFile -HostName deploy@web01 -Path ./site.zip -Destination /var/www/
```

### Upload into a folder on an Sftp drive

```powershell
New-PSDrive -Name web -PSProvider Sftp -Root /var/www -Session web01
Send-SftpFile -Path ./index.html -Destination web:\site\
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

Where to upload on the server: an existing folder, or a new file name when you upload one file. A path ending with / is always a folder, and ~ is your home folder. The default is your home folder, or the current folder when your location is on an Sftp drive of the same session (after Set-Location prod:\upload). A path on an Sftp drive, such as prod:\upload\, uses that drive's session.

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

Replace files that already exist on the server.

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

The files on your computer to upload, used exactly as written (no wildcards). Accepts files piped from Get-ChildItem and results from Receive-SftpFile.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- PSPath
- FullName
- LocalPath
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

The files on your computer to upload. Wildcards such as *.csv are allowed.

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

Keep each file's original modification time on the server.

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

Upload folders with everything in them, keeping the folder structure.

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

### -UseTemporaryName

Upload each file under a temporary name and rename it when complete, so nobody on the server can pick up a half-uploaded file.

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

## INPUTS

### System.String

Files on your computer, for example from Get-ChildItem.

## OUTPUTS

### sftp.SftpTransferResult

One result per file, showing whether it was uploaded.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)
