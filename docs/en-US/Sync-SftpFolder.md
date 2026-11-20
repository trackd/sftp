---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-20-2026
PlatyPS schema version: 2024-05-01
title: Sync-SftpFolder
---

# Sync-SftpFolder

## SYNOPSIS

Makes a folder on the server match a local folder, or the other way round.

## SYNTAX

### __AllParameterSets

```
Sync-SftpFolder [-LocalPath] <string> [-RemotePath] <string> -Direction <SyncDirection> [-Remove]
 [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Compares two folder trees and copies only the files that are missing or different: a file counts as unchanged when its size and modification time match. With -Direction Upload the server folder is updated from the local folder; with Download the local folder is updated from the server. Copied files get the source's modification time, so the next run skips them. Files that are only in the destination are kept unless you add -Remove. Use -WhatIf to see what would happen first.

## EXAMPLES

### Upload new and changed files

```powershell
Sync-SftpFolder -LocalPath ./site -RemotePath /var/www/site -Direction Upload
```

### Make a local copy of a server folder, deleting files that are gone

```powershell
Sync-SftpFolder -LocalPath ./mirror -RemotePath /reports -Direction Download -Remove
```

### Preview a mirror before running it

```powershell
Sync-SftpFolder -LocalPath ./site -RemotePath /var/www/site -Direction Upload -Remove -WhatIf
```

## PARAMETERS

### -Direction

Upload makes the server folder match the local folder; Download makes the local folder match the server folder.

```yaml
Type: sftp.SyncDirection
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -LocalPath

The folder on this computer. For a download it is created if it does not exist.

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

### -RemotePath

The folder on the server. For an upload it is created if it does not exist.

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

### -Remove

Also delete files and folders that are only in the destination. Use -WhatIf first to see what would be deleted.

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

## INPUTS

### None

This command does not take pipeline input.

## OUTPUTS

### sftp.SftpTransferResult

One result per file copied or deleted. Deletions have the Operation Remove.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)
