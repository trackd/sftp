---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-20-2026
PlatyPS schema version: 2024-05-01
title: Set-SftpPermission
---

# Set-SftpPermission

## SYNOPSIS

Changes the Unix permissions of files and folders on the server, like chmod.

## SYNTAX

### Path (Default)

```
Set-SftpPermission [-Path] <string[]> [-Mode] <string> [-Recurse] [-PassThru]
 [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### LiteralPath

```
Set-SftpPermission [-Mode] <string> -LiteralPath <string[]> [-SessionName <string>] [-Recurse]
 [-PassThru] [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Sets permissions from an octal mode (0644, 755) or a symbolic mode like chmod uses (u+x, go-w, u=rw,go=r). In symbolic modes, u is the owner, g the group, o everyone else and a all three; leaving out who means all three. Use -Recurse to change folders and everything in them, and -WhatIf to see what would change.

## EXAMPLES

### Make a script executable for its owner

```powershell
Set-SftpPermission -Path /upload/run.sh -Mode u+x
```

### Give a folder and its contents fixed permissions

```powershell
Set-SftpPermission -Path /data/shared -Mode 0750 -Recurse
```

### See which files would lose write access for others

```powershell
Set-SftpPermission -Path /upload/* -Mode o-w -WhatIf
```

## PARAMETERS

### -LiteralPath

The same as -Path, but used exactly as written. Accepts files piped from Get-SftpFile.

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

### -Mode

The new permissions: octal like 0644 or 755, or symbolic like u+x, go-w or u=rw,go=r. Symbolic modes can also use X (execute only for folders and files that are already executable), s (setuid or setgid) and t (sticky).

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PassThru

Return the permissions of each item after the change.

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

### -Path

The paths on the server. Wildcards are allowed.

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

### -Recurse

Also change everything inside the folders. Symbolic links are not followed.

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

## INPUTS

### System.String

Paths on the server.

## OUTPUTS

### sftp.SftpPermissionInfo

With -PassThru, the new permissions of each item.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)
