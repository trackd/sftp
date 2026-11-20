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

### UserName (Default)

```
Connect-SftpServer [-HostName] <string> [-Port <int>] [-UserName <string>]
 [-HostKeyFingerprint <string[]>] [-KnownHostsFile <string>] [-SkipHostKeyCheck]
 [-KeyFile <string[]>] [-PrivateKey <securestring[]>] [-KeyPassphrase <securestring>]
 [-ConfigFile <string>] [-IgnoreSshConfig] [-Name <string>] [-ConnectTimeout <int>]
 [-OperationTimeout <int>] [-KeepAliveInterval <int>] [<CommonParameters>]
```

### Credential

```
Connect-SftpServer [-HostName] <string> -Credential <pscredential> [-Port <int>]
 [-HostKeyFingerprint <string[]>] [-KnownHostsFile <string>] [-SkipHostKeyCheck]
 [-KeyFile <string[]>] [-PrivateKey <securestring[]>] [-KeyPassphrase <securestring>]
 [-ConfigFile <string>] [-IgnoreSshConfig] [-Name <string>] [-ConnectTimeout <int>]
 [-OperationTimeout <int>] [-KeepAliveInterval <int>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Connects to an SFTP server and returns a session to use with the other commands. Log in with a
password (-Credential), a private key (-KeyFile), or both.

It works like the OpenSSH sftp command: -HostName can be user@host or a Host alias from
~/.ssh/config, whose HostName, User, Port, IdentityFile, UserKnownHostsFile and
StrictHostKeyChecking settings are used. Without -KeyFile, the keys from the config and the default
keys in ~/.ssh (id_ed25519, id_ecdsa, id_rsa) are tried. The user name defaults to your own.
Parameters you type always win over the config.

The new session becomes the current one: the other commands use it when you do not pass -Session.
Give it a -Name to refer to it later, for example when you work with several servers.

Choose how to check that you reached the right server:

- ~/.ssh/known_hosts, the default, as used by ssh. The first connection to a server adds it to the
  file; after that, the connection is refused if the server's key changes. -KnownHostsFile picks
  another file.
- -HostKeyFingerprint: the server's fingerprint, from Get-SshHostKey. Best for scheduled scripts.
- -SkipHostKeyCheck: no check at all. Only use this for testing.

## EXAMPLES

### Log in with your default key from ~/.ssh

The server is remembered in ~/.ssh/known_hosts the first time.

```powershell
Connect-SftpServer deploy@sftp.example.com
```

### Use a Host alias from ~/.ssh/config

The alias's HostName, User, Port and IdentityFile are used.

```powershell
Connect-SftpServer backups
```

### Log in with a user name and password

```powershell
$session = Connect-SftpServer -HostName sftp.example.com -Credential (Get-Credential) -HostKeyFingerprint 'ssh-ed25519 255 X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc'
```

### Log in with a specific private key and a pinned fingerprint

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

### -ConfigFile

The OpenSSH client config file to read. The default is ~/.ssh/config.

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

The server: a name, an IP address, user@host, or a Host alias from ~/.ssh/config. Press Tab to pick from ~/.ssh/config and ~/.ssh/known_hosts.

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

### -IgnoreSshConfig

Do not read ~/.ssh/config.

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

The private key file to log in with. OpenSSH and PuTTY (.ppk) key files are supported. Press Tab to pick one of the keys in ~/.ssh. Without -KeyFile, the default keys in ~/.ssh are tried.

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

The known_hosts file to check the server against. The default is ~/.ssh/known_hosts. Servers not yet in the file are added; a changed key is refused.

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

Your user name, when you log in with a private key only. The default is your own user name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: UserName
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

A host name.

### System.Int32

A port number.

## OUTPUTS

### sftp.SftpSession

The connection. Close it with Disconnect-SftpServer when you are done. HostKeyVerification shows how the host key was checked.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

