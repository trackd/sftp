# sftp

Upload, download and manage files on SFTP servers from PowerShell 7.6+.

- Log in with a password, a private key (including PuTTY `.ppk` files), or both.
- Check you reached the right server with a pinned fingerprint or a `known_hosts` file.
- Every transfer returns a result per file, so you can see exactly what was sent or received.
- SCP is available for servers that do not offer SFTP.

## Install

```powershell
Install-PSResource -Name sftp
```

## Getting started

### 1. Connect

If you already use `ssh`, this works the same way: `user@host` and the `Host` aliases in
`~/.ssh/config` work, your keys in `~/.ssh` are used, and the server is remembered in
`~/.ssh/known_hosts` the first time you connect. Press Tab after `Connect-SftpServer` to pick a host.

```powershell
Connect-SftpServer deploy@sftp.example.com
Connect-SftpServer backups        # a Host alias from ~/.ssh/config
```

With a password, or a specific key:

```powershell
Connect-SftpServer -HostName sftp.example.com -Credential (Get-Credential)
Connect-SftpServer -HostName sftp.example.com -UserName deploy -KeyFile ./deploy.ppk
```

### 2. For scheduled scripts: pin the fingerprint

Read the server's host key fingerprint once, confirm it with whoever runs the server, and keep it
with your script:

```powershell
Get-SshHostKey -HostName sftp.example.com
Connect-SftpServer -HostName sftp.example.com -UserName deploy -HostKeyFingerprint 'ssh-ed25519 255 X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc'
```

For testing only, `-SkipHostKeyCheck` connects without checking the server at all. The other
commands use the connection automatically; there is no need to pass it around.
### 3. Transfer files

Local paths are on your computer; remote paths (starting with `/`) are on the server.

```powershell
# Upload a file to your home folder on the server
Send-SftpFile ./report.csv

# Upload every CSV file in the local ToSftp folder to /upload on the server
Send-SftpFile -Path ./ToSftp/*.csv -Destination /upload

# See what is on the server
Get-SftpFile -Path /reports

# Download all CSV files in /reports to the local FromSftp folder
Receive-SftpFile -Path /reports/*.csv -Destination ./FromSftp
```

### 4. Disconnect

```powershell
Disconnect-SftpServer
```

## A complete script

Download new reports, then delete them from the server once they have arrived safely:

```powershell
$fingerprint = 'ssh-ed25519 255 X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc'
Connect-SftpServer -HostName sftp.example.com -Credential (Get-Credential) -HostKeyFingerprint $fingerprint
try {
    Get-SftpFile -Path /reports -File |
        Receive-SftpFile -Destination ./FromSftp |
        Where-Object Success |
        Remove-SftpFile
}
finally {
    Disconnect-SftpServer
}
```

## Several servers

Give each connection a name. Files and results remember which server they came from, so piped
commands go back to the right one; use `-Session` to choose a server explicitly.

```powershell
Connect-SftpServer -HostName sftp1.example.com -Credential (Get-Credential) -KnownHostsFile ~/.ssh/known_hosts -Name source
Connect-SftpServer -HostName sftp2.example.com -Credential (Get-Credential) -KnownHostsFile ~/.ssh/known_hosts -Name target

# Copy reports from one server to the other
Get-SftpFile -Session source -Path /reports/*.csv |
    Receive-SftpFile -Destination ./FromSftp |
    Send-SftpFile -Session target -Destination /incoming

Get-SftpSession                          # list open sessions
Get-SftpSession | Disconnect-SftpServer  # close them all
```

## One command, no session

`Send-SftpFile` and `Receive-SftpFile` can connect for a single command: give `-HostName` (and
`-Credential` or `-KeyFile` if needed) instead of `-Session`. The connection is closed when the command
finishes, fails or is stopped, and your current session is left alone.

```powershell
Send-SftpFile -HostName deploy@web01 -Path ./site.zip -Destination /var/www/
Receive-SftpFile -HostName backup@nas -Path /backup/latest.tar.gz -Destination ./Backups
```

## Browse the server as a drive

The `Sftp` provider shows a folder on the server as a PowerShell drive, so the usual commands work on it:
`Get-ChildItem` (with `-Filter`, `-Recurse` and `-Depth`), `Set-Location`, `Test-Path`, `Get-Item`,
`New-Item`, `Remove-Item`, `Rename-Item`, `Move-Item`, `Get-Content` and `Set-Content` (text, or bytes
with `-AsByteStream`).

```powershell
New-PSDrive -Name prod -PSProvider Sftp -Root / -Session prod
Get-ChildItem prod:\var\log -Filter *.log
Set-Location prod:\upload
Get-Content prod:\etc\hostname

# A drive can also open its own connection; Remove-PSDrive closes it.
New-PSDrive -Name nas -PSProvider Sftp -Root /backup -HostName backup@nas
Remove-PSDrive nas
```

Every sftp command accepts drive paths and uses the drive's connection, whichever session is current. While
the current location is on an Sftp drive, relative paths, the default session and the default upload folder follow it.

```powershell
Get-SftpPermission -Path prod:\upload\run.sh
Set-Location prod:\var\log
Receive-SftpFile -Path app\*.log -Destination C:\Logs   # uses prod's connection
```

`Copy-Item` cannot copy between a drive on the server and a drive on your computer. Use
`Send-SftpFile` and `Receive-SftpFile`:

```powershell
Send-SftpFile -Path ./report.csv -Destination prod:\upload\
Receive-SftpFile -Path prod:\var\log\app\*.log -Destination ./Logs
```

Files come back as `SftpFileInfo` and folders as `SftpDirectoryInfo` (both are `SftpItem`), with the same
properties as local files and folders where SFTP has the information: `Name`, `FullName`, `Extension`,
`LastWriteTime`, `LastAccessTime`, `CreationTime` (SFTP does not report creation times, so this is the
modification time), `Attributes`, `Mode` and `UnixMode`; files add `Length`, `BaseName`, `DirectoryName` and
`IsReadOnly`, folders add `Parent` and `Root`. Folders also have `GetFiles()`, `GetDirectories()`,
`GetFileSystemInfos()` and `GetParent()`, which read from the server. `Get-PSDrive` shows used and free
space for Sftp drives when the server can report it.

## More tools

```powershell
# Keep a server folder in step with a local folder; only new and changed files are copied
Sync-SftpFolder -LocalPath ./site -RemotePath /var/www/site -Direction Upload -Remove -WhatIf

# Permissions, like chmod
Get-SftpPermission -Path /upload/run.sh
Set-SftpPermission -Path /upload/run.sh -Mode u+x

# Free space on the server
Get-SftpDiskSpace -Path /backup

# Reach a database that only the server can see
$tunnel = New-SshTunnel -RemoteHost db.internal -RemotePort 5432
Get-SshTunnel
$tunnel | Remove-SshTunnel
```

Add `-Verbose` to any command to see what it does, including the SSH library's own messages.

## Commands

| Command | What it does |
|---|---|
| `Get-SshHostKey` | Shows a server's host key fingerprint |
| `Connect-SftpServer` | Connects and logs in |
| `Disconnect-SftpServer` | Closes the connection |
| `Get-SftpSession` | Lists open connections |
| `Get-SftpFile` | Lists files and folders on the server |
| `Send-SftpFile` | Uploads files |
| `Receive-SftpFile` | Downloads files |
| `Move-SftpFile` | Moves or renames files on the server |
| `Remove-SftpFile` | Deletes files on the server |
| `New-SftpDirectory` | Creates folders on the server |
| `Test-SftpPath` | Checks whether a file or folder exists |
| `Get-SftpContent` / `Set-SftpContent` | Reads and writes text files on the server |
| `Invoke-SshCommand` | Runs a command on the server |
| `Send-ScpFile` / `Receive-ScpFile` | Uploads and downloads with SCP |
| `Sync-SftpFolder` | Mirrors a folder up to the server or down from it |
| `Get-SftpPermission` / `Set-SftpPermission` | Shows and changes file permissions |
| `Get-SftpDiskSpace` | Shows free space on the server |
| `New-SshTunnel` / `Get-SshTunnel` / `Remove-SshTunnel` | Forwards local ports through the server |
| `New-PSDrive -PSProvider Sftp` | Shows a server folder as a drive |

The commands work together in the pipeline: pipe `Get-ChildItem` into `Send-SftpFile`, or `Get-SftpFile`
into `Receive-SftpFile`, `Move-SftpFile` or `Remove-SftpFile`, and pipe transfer results on to the next
step. Remote paths complete with Tab, straight from the server. Paths accept wildcards; use `-LiteralPath` for names that contain `[` or `]`. Commands that change
files support `-WhatIf` to preview what they would do. Run `Get-Help <command> -Examples` for more examples.

## Dependencies

The module ships SSH.NET and the libraries it requires
`Renci.SshNet`, `BouncyCastle.Cryptography`, `Microsoft.Extensions.Logging.Abstractions` and
`Microsoft.Extensions.DependencyInjection.Abstractions`.

## Development

```powershell
./build.ps1
```

Builds the module, generates help and runs all tests. The integration tests start a small test
server included in the repository; it needs `ssh-keygen` on the PATH.

## License

MIT
