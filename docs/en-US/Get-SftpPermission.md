---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-20-2026
PlatyPS schema version: 2024-05-01
title: Get-SftpPermission
---

# Get-SftpPermission

## SYNOPSIS

Shows the Unix permissions of files and folders on the server.

## SYNTAX

### Path (Default)

```
Get-SftpPermission [-Path] <string[]> [-Session <SftpSession>] [<CommonParameters>]
```

### LiteralPath

```
Get-SftpPermission -LiteralPath <string[]> [-SessionName <string>] [-Session <SftpSession>]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Returns the permissions of each item in ls -l form (-rw-r--r--) and in octal (0644), with the numeric owner and group.

## EXAMPLES

### Show the permissions of a file

```powershell
Get-SftpPermission -Path /upload/run.sh
```

### Show the permissions of everything in a folder

```powershell
Get-SftpPermission -Path /upload/*
```

### Find files that everyone can write to

```powershell
Get-SftpFile -Path /upload -Recurse | Get-SftpPermission | Where-Object { $_.Mode[8] -eq 'w' }
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

The permissions of each item.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)
