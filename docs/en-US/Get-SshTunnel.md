---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-20-2026
PlatyPS schema version: 2024-05-01
title: Get-SshTunnel
---

# Get-SshTunnel

## SYNOPSIS

Lists the open SSH tunnels.

## SYNTAX

### __AllParameterSets

```
Get-SshTunnel [[-Id] <int[]>] [-Session <SftpSession>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Returns the tunnels opened with New-SshTunnel that are still open, on all sessions or on one session.

## EXAMPLES

### List all open tunnels

```powershell
Get-SshTunnel
```

### List the tunnels of one session

```powershell
Get-SshTunnel -Session prod
```

### Close all tunnels

```powershell
Get-SshTunnel | Remove-SshTunnel
```

## PARAMETERS

### -Id

Only return the tunnels with these numbers.

```yaml
Type: System.Int32[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Session

Only return the tunnels of this session: the object from Connect-SftpServer, or its name.

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

### sftp.SshTunnel

The open tunnels.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)
