---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Receive-SftpFile
---

# Receive-SftpFile

## SYNOPSIS

Downloads files from the server.

## SYNTAX

### Path (Default)

```
Receive-SftpFile [-Path] <string[]> [-Destination] <string> [-Force] [-PreserveTimestamp]
 [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### LiteralPath

```
Receive-SftpFile [-Destination] <string> -LiteralPath <string[]> [-SessionName <string>] [-Force]
 [-PreserveTimestamp] [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Downloads files from the server to a folder on your computer. Files that already exist on your
computer are not replaced unless you add -Force.

Each file returns a result; check its Success property to see whether it was downloaded.

## EXAMPLES

### Download one file

```powershell
Receive-SftpFile -Path /reports/sales.csv -Destination ./FromSftp
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

## PARAMETERS

### -Destination

The folder on your computer to download to. It must already exist.

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

The files on the server to download. Wildcards are allowed, for example /reports/*.csv.

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

### sftp.SftpTransferResult

One result per file, showing whether it was downloaded.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

