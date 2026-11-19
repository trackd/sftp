---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Get-SftpSession
---

# Get-SftpSession

## SYNOPSIS

Lists your open SFTP sessions.

## SYNTAX

### __AllParameterSets

```
Get-SftpSession [[-Name] <string[]>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Lists the sessions opened with Connect-SftpServer that are still open. The current session, used when a command is run without -Session, is marked in the Current column.

## EXAMPLES

### List open sessions

```powershell
Get-SftpSession
```

### Find a session by name

```powershell
Get-SftpSession -Name prod*
```

### Close all sessions

```powershell
Get-SftpSession | Disconnect-SftpServer
```

## PARAMETERS

### -Name

Only list sessions with this name. Wildcards are allowed.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: true
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: false
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

## INPUTS

### System.String

Session names.

### System.String[]

Session names.

## OUTPUTS

### sftp.SftpSession

An open session.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

