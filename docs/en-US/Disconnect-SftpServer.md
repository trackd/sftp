---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Disconnect-SftpServer
---

# Disconnect-SftpServer

## SYNOPSIS

Closes a connection to an SFTP server.

## SYNTAX

### __AllParameterSets

```
Disconnect-SftpServer [[-Session] <SftpSession[]>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Closes a session opened with Connect-SftpServer. Without -Session it closes the current session, and the previously opened session becomes current again.

## EXAMPLES

### Close the current session

```powershell
Disconnect-SftpServer
```

### Always close the connection, even if something fails

```powershell
Connect-SftpServer -HostName sftp.example.com -Credential (Get-Credential) -HostKeyFingerprint 'ssh-ed25519 255 X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc'
try {
    Send-SftpFile -Path ./ToSftp/report.csv -Destination /upload
}
finally {
    Disconnect-SftpServer
}
```

### Close every open session

```powershell
Get-SftpSession | Disconnect-SftpServer
```

## PARAMETERS

### -Session

The sessions to close, as session objects or names. The default is the current session.

```yaml
Type: sftp.SftpSession[]
DefaultValue: ''
SupportsWildcards: false
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

### sftp.SftpSession

Sessions to close, for example from Get-SftpSession.

### sftp.SftpSession[]

Sessions to close, for example from Get-SftpSession.

## OUTPUTS

### System.Void

None.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

