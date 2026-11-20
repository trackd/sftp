---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Set-SftpContent
---

# Set-SftpContent

## SYNOPSIS

Writes text to a file on the server.

## SYNTAX

### __AllParameterSets

```
Set-SftpContent [-Path] <string> [-Value] <Object[]> [-Append] [-NoNewline] [-Encoding <string>]
 [-PassThru] [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Writes text to a file on the server, replacing what was there, or adding to the end with -Append. Each value becomes one line; lines end with a Unix line break.

## EXAMPLES

### Write a small file

```powershell
Set-SftpContent -Path /upload/ready.txt -Value 'done'
```

### Write the output of a command

```powershell
Get-Date -Format o | Set-SftpContent -Path /upload/timestamp.txt
```

### Add a line to a log

```powershell
Set-SftpContent -Path /logs/transfers.log -Value 'sales.csv uploaded' -Append
```

## PARAMETERS

### -Append

Add the text to the end of the file instead of replacing it.

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

### -Encoding

The text encoding to write. The default is UTF-8 without a byte order mark.

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

### -NoNewline

Do not end the text with a line break.

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

### -PassThru

Return the written file.

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

The file on the server to write. It is created if it does not exist.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
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

### -Value

The text to write. Each value becomes one line. Accepts pipeline input.

```yaml
Type: System.Object[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

## INPUTS

### System.Object

Text to write.

### System.Object[]

Text to write.

## OUTPUTS

### sftp.SftpItem

The written file, with -PassThru.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

