---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: New-SftpDirectory
---

# New-SftpDirectory

## SYNOPSIS

Creates folders on the server.

## SYNTAX

### __AllParameterSets

```
New-SftpDirectory [-Path] <string[]> [-Force] [-Session <SftpSession>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Creates a folder on the server. Add -Force to also create any missing parent folders, and to not fail if the folder already exists.

## EXAMPLES

### Create a folder

```powershell
New-SftpDirectory -Path /upload/2026
```

### Create a folder and its parents if they are missing

```powershell
New-SftpDirectory -Path /archive/2026/11 -Force
```

### Create a folder for each local folder

```powershell
Get-ChildItem ./ToSftp -Directory | ForEach-Object { New-SftpDirectory -Path "/upload/$($_.Name)" -Force }
```

## PARAMETERS

### -Force

Also create missing parent folders, and do not fail if the folder already exists.

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

The folders to create on the server.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
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

## INPUTS

### System.String

Folders to create.

### System.String[]

Folders to create.

## OUTPUTS

### sftp.SftpItem

The created folder.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

