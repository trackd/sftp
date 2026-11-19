---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Connect-SftpServer
---

# Connect-SftpServer

## SYNOPSIS

Connects and logs in to an SFTP server.

## SYNTAX

### Credential (Default)

```
Connect-SftpServer [-HostName] <string> -Credential <pscredential> [-Port <int>]
 [-HostKeyFingerprint <string[]>] [-KnownHostsFile <string>] [-SkipHostKeyCheck]
 [-KeyFile <string[]>] [-PrivateKey <securestring[]>] [-KeyPassphrase <securestring>]
 [-Name <string>] [-ConnectTimeout <int>] [-OperationTimeout <int>] [-KeepAliveInterval <int>]
 [<CommonParameters>]
```

### UserName

```
Connect-SftpServer [-HostName] <string> -UserName <string> [-Port <int>]
 [-HostKeyFingerprint <string[]>] [-KnownHostsFile <string>] [-SkipHostKeyCheck]
 [-KeyFile <string[]>] [-PrivateKey <securestring[]>] [-KeyPassphrase <securestring>]
 [-Name <string>] [-ConnectTimeout <int>] [-OperationTimeout <int>] [-KeepAliveInterval <int>]
 [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Connects to an SFTP server and returns a session to use with the other commands. Log in with a
password (-Credential), a private key (-KeyFile), or both.

The new session becomes the current one: the other commands use it when you do not pass -Session.
Give it a -Name to refer to it later, for example when you work with several servers.

Choose how to check that you reached the right server:

- -HostKeyFingerprint: the server's fingerprint, from Get-SshHostKey. Best for scripts.
- -KnownHostsFile: a known_hosts file, as used by ssh. The first connection to a server adds it to
  the file; after that, the connection is refused if the server's key changes.
- -SkipHostKeyCheck: no check at all. Only use this for testing.

## EXAMPLES

### Log in with a user name and password

```powershell
$session = Connect-SftpServer -HostName sftp.example.com -Credential (Get-Credential) -HostKeyFingerprint 'ssh-ed25519 255 X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc'
```

### Log in with a private key

Add -KeyPassphrase if the key is protected by a passphrase.

```powershell
$session = Connect-SftpServer -HostName sftp.example.com -UserName deploy -KeyFile ./deploy.ppk -HostKeyFingerprint 'ssh-ed25519 255 X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc'
```

### Work with two servers

Files remember which session they came from, so this copies reports from one server to the other.

```powershell
Connect-SftpServer -HostName sftp1.example.com -Credential (Get-Credential) -KnownHostsFile ~/.ssh/known_hosts -Name source
Connect-SftpServer -HostName sftp2.example.com -Credential (Get-Credential) -KnownHostsFile ~/.ssh/known_hosts -Name target
Get-SftpFile -Session source -Path /reports/*.csv |
    Receive-SftpFile -Destination ./FromSftp |
    Send-SftpFile -Session target -Destination /incoming
```

### Remember servers in a known_hosts file

The first connection adds the server to the file. Later connections are refused if its key changes.

```powershell
$session = Connect-SftpServer -HostName sftp.example.com -Port 2222 -Credential (Get-Credential) -KnownHostsFile ~/.ssh/known_hosts
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

### -Credential

Your user name and password.

```yaml
Type: System.Management.Automation.PSCredential
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Credential
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -HostKeyFingerprint

The server's host key fingerprint, from Get-SshHostKey. You can give several, for example while a server changes its key.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Fingerprint
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
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ComputerName
- Host
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -KeepAliveInterval

Sends a keep-alive message every this many seconds, so long idle connections are not dropped. The default, 0, sends none.

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

### -KeyFile

The private key file to log in with. OpenSSH and PuTTY (.ppk) key files are supported.

```yaml
Type: System.String[]
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

### -KeyPassphrase

The passphrase that protects the private key.

```yaml
Type: System.Security.SecureString
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

### -KnownHostsFile

A known_hosts file to check the server against, for example ~/.ssh/known_hosts. Servers not yet in the file are added; a changed key is refused.

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

### -Name

A name for the session, to use with -Session and Get-SftpSession. The default is user@host.

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

### -OperationTimeout

How many seconds to wait for each file operation. The default, 0, waits as long as needed.

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

### -PrivateKey

The private key itself, as a secure string, for example from a secret vault. Use instead of -KeyFile.

```yaml
Type: System.Security.SecureString[]
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

### -SkipHostKeyCheck

Connect without checking the host key. Only use this for testing: you cannot tell whether you reached the real server.

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

### -UserName

Your user name, when you log in with a private key only.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: UserName
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
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

## OUTPUTS

### sftp.SftpSession

The connection. Close it with Disconnect-SftpServer when you are done. HostKeyVerification shows how the host key was checked.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

