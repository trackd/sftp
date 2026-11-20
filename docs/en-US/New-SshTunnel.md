---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-20-2026
PlatyPS schema version: 2024-05-01
title: New-SshTunnel
---

# New-SshTunnel

## SYNOPSIS

Forwards a port on this computer to a host and port reachable from the server.

## SYNTAX

### __AllParameterSets

```
New-SshTunnel [-RemoteHost] <string> [-RemotePort] <int> [-LocalPort <int>] [-LocalAddress <string>]
 [-Session <SftpSession>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Opens a tunnel (like ssh -L): programs on this computer connect to LocalAddress:LocalPort, and the connection comes out at RemoteHost:RemotePort as seen from the server. Use it to reach a database or web service that only the server can see. The tunnel stays open until you close it with Remove-SshTunnel or disconnect the session.

## EXAMPLES

### Reach a database that only the server can see

Connections to the returned local port reach port 5432 on db.internal.

```powershell
$tunnel = New-SshTunnel -RemoteHost db.internal -RemotePort 5432
```

### Reach a web page served on the server itself

```powershell
$tunnel = New-SshTunnel -RemoteHost localhost -RemotePort 8080 -LocalPort 18080
Invoke-RestMethod http://localhost:18080/status
```

### Open a tunnel on a named session and close it when done

```powershell
$tunnel = New-SshTunnel -Session prod -RemoteHost localhost -RemotePort 3306
try { mysql --port $tunnel.LocalPort } finally { $tunnel | Remove-SshTunnel }
```

## PARAMETERS

### -LocalAddress

The address on this computer to listen on. The default, 127.0.0.1, only accepts connections from this computer.

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

### -LocalPort

The port on this computer to listen on. The default, 0, picks a free port; see the LocalPort of the returned tunnel.

```yaml
Type: System.Int32
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

### -RemoteHost

The host to connect to, as seen from the server. Use localhost for a service running on the server itself.

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

### -RemotePort

The port to connect to on the remote host.

```yaml
Type: System.Int32
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

### sftp.SshTunnel

The open tunnel, with its local port.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)
