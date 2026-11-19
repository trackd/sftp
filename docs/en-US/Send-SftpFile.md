---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Send-SftpFile
---

# Send-SftpFile

## SYNOPSIS

Uploads files to the server.

## SYNTAX

### Path (Default)

```
Send-SftpFile [-Path] <string[]> [-Destination] <string> [-Force] [-UseTemporaryName]
 [-PreserveTimestamp] [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### LiteralPath

```
Send-SftpFile [-Destination] <string> -LiteralPath <string[]> [-SessionName <string>] [-Force]
 [-UseTemporaryName] [-PreserveTimestamp] [-Session <SftpSession>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uploads files from your computer to a folder on the server. Files that already exist on the
server are not replaced unless you add -Force.

Each file returns a result; check its Success property to see whether it was uploaded.

## EXAMPLES

### Upload all CSV files from a local folder

```powershell
Send-SftpFile -Path ./ToSftp/*.csv -Destination /upload
```

### Replace a file that is already on the server

```powershell
Send-SftpFile -Path ./ToSftp/prices.xml -Destination /upload -Force
```

### Upload the files Get-ChildItem finds, then move the local copies to a Sent folder

```powershell
Get-ChildItem ./ToSftp -Filter *.csv |
    Send-SftpFile -Destination /upload |
    Where-Object Success |
    ForEach-Object { Move-Item -LiteralPath $_.Source -Destination ./Sent }
```

## PARAMETERS

### -Destination

The folder on the server to upload to. It must already exist.

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

### System.String[]

Files on your computer, for example from Get-ChildItem.

## OUTPUTS

### sftp.SftpTransferResult

One result per file, showing whether it was uploaded.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

