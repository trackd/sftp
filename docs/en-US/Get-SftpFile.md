---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Get-SftpFile
---

# Get-SftpFile

## SYNOPSIS

Lists files and folders on the server.

## SYNTAX

### Path (Default)

```
Get-SftpFile [[-Path] <string[]>] [-Filter <string>] [-CaseSensitive] [-Recurse] [-File]
 [-Directory] [-Session <SftpSession>] [<CommonParameters>]
```

### LiteralPath

```
Get-SftpFile -LiteralPath <string[]> [-SessionName <string>] [-Filter <string>] [-CaseSensitive]
 [-Recurse] [-File] [-Directory] [-Session <SftpSession>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Lists the files and folders in a folder on the server. Pipe the results to Receive-SftpFile, Move-SftpFile or Remove-SftpFile to work with them.

## EXAMPLES

### List a folder

```powershell
Get-SftpFile -Path /reports
```

### List the CSV files in a folder

```powershell
Get-SftpFile -Path /reports/*.csv
```

### Find all CSV files, including in subfolders

```powershell
Get-SftpFile -Path /reports -Filter *.csv -Recurse -File
```

## PARAMETERS

### -CaseSensitive

Match -Filter with exact upper and lower case.

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

### -Directory

List folders only.

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

### -File

List files only.

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

### -Filter

Only list items whose name matches this pattern, for example *.csv.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: true
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

### -LiteralPath

The same as -Path, but used exactly as written, so characters like [ and ] are not treated as wildcards. Accepts folders piped from Get-SftpFile.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- FullName
- RemotePath
ParameterSets:
- Name: LiteralPath
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Path

The folder on the server to list, or files to show. Wildcards are allowed, for example /reports/*.csv. The default is your home folder.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: true
Aliases: []
ParameterSets:
- Name: Path
  Position: 0
  IsRequired: false
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Recurse

Also list the contents of subfolders.

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

### -SessionName

The name of the session to use. You rarely type this: it is filled in from the files and results you pipe in, so they go back to the server they came from.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: LiteralPath
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

Folders on the server.

### System.String[]

Folders on the server.

## OUTPUTS

### sftp.SftpItem

Files are sftp.SftpFileInfo and folders sftp.SftpDirectoryInfo, with the same properties as local files and folders where SFTP has the information: Name, FullName, Extension, LastWriteTime, LastAccessTime, CreationTime (the modification time, as SFTP does not report creation), Attributes, Mode and UnixMode. Files add Length, BaseName, DirectoryName and IsReadOnly; folders add Parent and Root.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

