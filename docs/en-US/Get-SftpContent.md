---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Get-SftpContent
---

# Get-SftpContent

## SYNOPSIS

Reads text files on the server.

## SYNTAX

### Path (Default)

```
Get-SftpContent [-Path] <string[]> [-Raw] [-Encoding <string>] [-Session <SftpSession>]
 [<CommonParameters>]
```

### LiteralPath

```
Get-SftpContent -LiteralPath <string[]> [-SessionName <string>] [-Raw] [-Encoding <string>]
 [-Session <SftpSession>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Reads a text file on the server and returns it one line at a time, or as one string with -Raw. Useful for checking log or status files without downloading them.

## EXAMPLES

### Show a file

```powershell
Get-SftpContent -Path /reports/status.txt
```

### Show the last 20 lines of a log

```powershell
Get-SftpContent -Path /logs/import.log | Select-Object -Last 20
```

### Read a JSON file

```powershell
Get-SftpContent -Path /config/settings.json -Raw | ConvertFrom-Json
```

## PARAMETERS

### -Encoding

The text encoding of the file. The default is UTF-8.

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

The files on the server to read. Wildcards are allowed.

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

### -Raw

Return the whole file as one string instead of one string per line.

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

Files on the server.

### System.String[]

Files on the server.

## OUTPUTS

### System.String

The lines of the file, or the whole file with -Raw.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

