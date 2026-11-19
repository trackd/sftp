---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Move-SftpFile
---

# Move-SftpFile

## SYNOPSIS

Moves or renames files on the server.

## SYNTAX

### Path (Default)

```
Move-SftpFile [-Path] <string[]> [-Destination] <string> [-Force] [-PassThru]
 [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### LiteralPath

```
Move-SftpFile [-Destination] <string> -LiteralPath <string[]> [-SessionName <string>] [-Force]
 [-PassThru] [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Renames a file or folder on the server, or moves it into another folder. A file that already exists at the destination is not replaced unless you add -Force.

## EXAMPLES

### Rename a file

```powershell
Move-SftpFile -Path /upload/order.tmp -Destination /upload/order.xml
```

### Move processed files into a Done folder

End the destination with / to move files into that folder.

```powershell
Move-SftpFile -Path /reports/*.csv -Destination /reports/done/
```

### Replace an existing file

```powershell
Move-SftpFile -Path /upload/new.csv -Destination /upload/current.csv -Force
```

## PARAMETERS

### -Destination

The new name, or the folder to move into.

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

### -Force

Replace a file that already exists at the destination.

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

### -LiteralPath

The same as -Path, but used exactly as written. Accepts files piped from Get-SftpFile and results from Send-SftpFile.

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

### -PassThru

Return the moved files.

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

The files or folders on the server to move. Wildcards are allowed.

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

The session to use: the object from Connect-SftpServer, or its name. By default the session the piped files came from is used, otherwise the most recent connection.

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

### sftp.SftpItem

The moved file, when you use -PassThru.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

