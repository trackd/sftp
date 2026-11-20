---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-20-2026
PlatyPS schema version: 2024-05-01
title: Remove-SshTunnel
---

# Remove-SshTunnel

## SYNOPSIS

Closes SSH tunnels.

## SYNTAX

### Tunnel (Default)

```
Remove-SshTunnel [-Tunnel] <SshTunnel[]> [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Id

```
Remove-SshTunnel -Id <int[]> [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Stops tunnels opened with New-SshTunnel. Programs can no longer connect to their local ports. Tunnels also close when their session is disconnected.

## EXAMPLES

### Close a tunnel

```powershell
$tunnel = New-SshTunnel -RemoteHost localhost -RemotePort 5432
Remove-SshTunnel -Tunnel $tunnel
```

### Close a tunnel by its number

```powershell
Remove-SshTunnel -Id 3
```

### Close all tunnels of a session

```powershell
Get-SshTunnel -Session prod | Remove-SshTunnel
```

## PARAMETERS

### -Id

The numbers of the tunnels to close, as shown by Get-SshTunnel.

```yaml
Type: System.Int32[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Id
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Tunnel

The tunnels to close, from New-SshTunnel or Get-SshTunnel.

```yaml
Type: sftp.SshTunnel[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Tunnel
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

## INPUTS

### sftp.SshTunnel

Tunnels to close.

## OUTPUTS

### System.Void

None.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)
