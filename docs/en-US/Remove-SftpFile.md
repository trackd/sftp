---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Remove-SftpFile
---

# Remove-SftpFile

## SYNOPSIS

Deletes files and folders on the server.

## SYNTAX

### Path (Default)

```
Remove-SftpFile [-Path] <string[]> [-Recurse] [-Session <SftpSession>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### LiteralPath

```
Remove-SftpFile -LiteralPath <string[]> [-SessionName <string>] [-Recurse] [-Session <SftpSession>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Deletes files on the server. Folders are only deleted if they are empty, unless you add -Recurse to delete everything in them. Use -WhatIf to see what would be deleted first.

## EXAMPLES

### Delete all temporary files in a folder

```powershell
Remove-SftpFile -Path /upload/*.tmp
```

### Delete files older than 30 days

```powershell
Get-SftpFile -Path /reports -File |
    Where-Object LastWriteTime -LT (Get-Date).AddDays(-30) |
    Remove-SftpFile
```

### See what would be deleted

```powershell
Remove-SftpFile -Path /reports/2025 -Recurse -WhatIf
```

## PARAMETERS

### -LiteralPath

The same as -Path, but used exactly as written. Accepts files piped from Get-SftpFile and results from Receive-SftpFile.

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

The files or folders on the server to delete. Wildcards are allowed.

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

Delete folders and everything in them.

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

Files on the server, for example from Get-SftpFile.

### System.String[]

Files on the server, for example from Get-SftpFile.

## OUTPUTS

### System.Void

None.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

