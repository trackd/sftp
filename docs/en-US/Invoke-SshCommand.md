---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Invoke-SshCommand
---

# Invoke-SshCommand

## SYNOPSIS

Runs a command on the server over SSH.

## SYNTAX

### __AllParameterSets

```
Invoke-SshCommand [-Command] <string[]> [-Timeout <int>] [-Session <SftpSession>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Runs a command on the server, using the same login and host key check as the session, and returns what it printed, any error text and its exit code. A command that fails (exit code other than 0) also writes an error, so -ErrorAction Stop works as usual. The command runs in the user's login shell on the server, as if typed after ssh user@host.

## EXAMPLES

### Run a command

```powershell
Invoke-SshCommand -Command 'df -h /data'
```

### Show only the output

```powershell
(Invoke-SshCommand -Command 'uptime').Output
```

### Run several commands and stop at the first failure

```powershell
'systemctl restart importer', 'systemctl is-active importer' | Invoke-SshCommand -ErrorAction Stop
```

## PARAMETERS

### -Command

The command to run, as you would type it in the server's shell. Accepts pipeline input.

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

### -Timeout

Seconds to wait for each command before stopping it. The default, 0, waits as long as needed.

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

## INPUTS

### System.String

Commands to run.

### System.String[]

Commands to run.

## OUTPUTS

### sftp.SshCommandResult

The command, its output, error text, exit code and duration.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

