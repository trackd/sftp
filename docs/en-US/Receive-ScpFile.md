---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Receive-ScpFile
---

# Receive-ScpFile

## SYNOPSIS

Downloads files with SCP.

## SYNTAX

### __AllParameterSets

```
Receive-ScpFile [-Path] <string[]> [[-Destination] <string>] [-SessionName <string>] [-Force]
 [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Downloads files with SCP instead of SFTP, for servers that only support SCP. Use Receive-SftpFile
when the server supports SFTP. Files that already exist on your computer are not replaced unless
you add -Force.

## EXAMPLES

### Download a file

```powershell
Receive-ScpFile -Path /reports/sales.csv -Destination ./FromSftp
```

### Download several files and replace local copies

```powershell
Receive-ScpFile -Path /reports/sales.csv, /reports/stock.csv -Destination ./FromSftp -Force
```

### Check whether a download worked

```powershell
(Receive-ScpFile -Path /reports/sales.csv -Destination ./FromSftp).Success
```

## PARAMETERS

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

### -Path

The files on the server to download, used exactly as written. SCP does not support wildcards. Accepts files piped from Get-SftpFile.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- FullName
- RemotePath
- LiteralPath
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
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
- Name: (All)
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

### sftp.SftpTransferResult

One result per file, showing whether it was downloaded.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

