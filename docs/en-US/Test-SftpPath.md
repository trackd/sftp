---
document type: cmdlet
external help file: sftp.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: sftp
ms.date: 11-19-2026
PlatyPS schema version: 2024-05-01
title: Test-SftpPath
---

# Test-SftpPath

## SYNOPSIS

Tests whether files or folders exist on the server.

## SYNTAX

### Path (Default)

```
Test-SftpPath [-Path] <string[]> [-PathType <SftpPathType>] [-Session <SftpSession>]
 [<CommonParameters>]
```

### LiteralPath

```
Test-SftpPath -LiteralPath <string[]> [-SessionName <string>] [-PathType <SftpPathType>]
 [-Session <SftpSession>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Returns $true when the path exists on the server, otherwise $false. With wildcards it returns $true when anything matches. Use -PathType to only count files or folders.

## EXAMPLES

### Check that a folder exists

```powershell
Test-SftpPath -Path /upload
```

### Check whether any CSV files are waiting

```powershell
if (Test-SftpPath -Path /reports/*.csv -PathType Leaf) { Receive-SftpFile -Path /reports/*.csv -Destination ./FromSftp }
```

### Create a folder only if it is missing

```powershell
if (-not (Test-SftpPath -Path /archive/2026)) { New-SftpDirectory -Path /archive/2026 }
```

## PARAMETERS

### -LiteralPath

The same as -Path, but used exactly as written. Accepts files piped from Get-SftpFile.

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

The paths on the server to test. Wildcards are allowed.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: true
Aliases: []
ParameterSets:
- Name: Path
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PathType

Only count files (Leaf) or folders (Container). The default, Any, counts both.

```yaml
Type: sftp.SftpPathType
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

Paths on the server.

### System.String[]

Paths on the server.

## OUTPUTS

### System.Boolean

True when the path exists.

## NOTES

## RELATED LINKS

- [sftp documentation](https://github.com/trackd/sftp)

