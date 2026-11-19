---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Get-SshHostKey
---

# Get-SshHostKey

## SYNOPSIS

Shows a server's host key fingerprint.

## SYNTAX

### __AllParameterSets

```
Get-SshHostKey [-HostName] <string[]> [-Port <int>] [-ConnectTimeout <int>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Shows the host key fingerprint of an SSH server, without logging in. Connect-SftpServer needs this
fingerprint.

Check the fingerprint with whoever runs the server before you use it, for example by phone or
email. That is how you know you reached the real server.

## EXAMPLES

### Show a server's fingerprint

```powershell
Get-SshHostKey -HostName sftp.example.com
```

### Copy the fingerprint into a script

```powershell
(Get-SshHostKey -HostName sftp.example.com).Fingerprint | Set-Clipboard
```

### Check several servers

```powershell
'sftp1.example.com', 'sftp2.example.com' | Get-SshHostKey | Select-Object HostName, Fingerprint
```

## PARAMETERS

### -ConnectTimeout

How many seconds to wait for the server. The default is 30.

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

### -HostName

The name or IP address of the server.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ComputerName
- Host
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

### -Port

The server port. The default is 22.

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
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

## INPUTS

### System.String

A host name.

### System.Int32

A port number.

### System.String[]

Host names.

## OUTPUTS

### sftp.SshHostKey

The server's host key. Use the Fingerprint property with Connect-SftpServer.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

