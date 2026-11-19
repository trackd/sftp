---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Send-ScpFile
---

# Send-ScpFile

## SYNOPSIS

Uploads files with SCP.

## SYNTAX

### Path (Default)

```
Send-ScpFile [-Path] <string[]> [-Destination] <string> [-Session <SftpSession>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

### LiteralPath

```
Send-ScpFile [-Destination] <string> -LiteralPath <string[]> [-SessionName <string>]
 [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uploads files with SCP instead of SFTP, for servers that only support SCP. Use Send-SftpFile when
the server supports SFTP.

SCP always replaces files that already exist on the server.

## EXAMPLES

### Upload a file

```powershell
Send-ScpFile -Path ./ToSftp/report.csv -Destination /upload
```

### Upload the files Get-ChildItem finds

```powershell
Get-ChildItem ./ToSftp -Filter *.csv | Send-ScpFile -Destination /upload
```

### Show files that failed to upload

```powershell
Send-ScpFile -Path ./ToSftp/a.csv, ./ToSftp/b.csv -Destination /upload -ErrorAction SilentlyContinue | Where-Object { -not $_.Success }
```

## PARAMETERS

### -Destination

The folder on the server to upload to.

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

### -LiteralPath

The files on your computer to upload, used exactly as written. Accepts files piped from Get-ChildItem.

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

The files on your computer to upload. Wildcards are allowed.

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

Files on your computer, for example from Get-ChildItem.

### System.String[]

Files on your computer, for example from Get-ChildItem.

## OUTPUTS

### sftp.SftpTransferResult

One result per file, showing whether it was uploaded.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

