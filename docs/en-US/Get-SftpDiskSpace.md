---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-20-2026
PlatyPS schema version: 2024-05-01
title: Get-SftpDiskSpace
---

# Get-SftpDiskSpace

## SYNOPSIS

Shows the size and free space of the disk that holds a folder on the server.

## SYNTAX

### __AllParameterSets

```
Get-SftpDiskSpace [[-Path] <string[]>] [-Session <SftpSession>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Returns the total, free and available space of the file system that holds each path. Available space is what you can use; free space can include space reserved for the administrator. The server must support the statvfs@openssh.com extension, as OpenSSH does; other servers report an error.

## EXAMPLES

### Show the space in your home folder

```powershell
Get-SftpDiskSpace
```

### Check that an upload will fit

```powershell
$size = (Get-Item ./backup.zip).Length
if ((Get-SftpDiskSpace -Path /backup).AvailableBytes -gt $size) { Send-SftpFile -Path ./backup.zip -Destination /backup }
```

### Show the space of several folders

```powershell
Get-SftpDiskSpace -Path /data, /var/tmp | Format-Table Path, PercentUsed, AvailableBytes
```

## PARAMETERS

### -Path

The folders on the server to check. The default is your home folder.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- FullName
- RemotePath
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: false
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
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

### System.String

Paths on the server.

## OUTPUTS

### sftp.SftpDiskSpace

The space of the file system that holds each path.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)
