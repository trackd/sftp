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

### 1. Get the server's fingerprint

The first time you use a server, read its host key fingerprint and confirm it with whoever runs
the server:

```powershell
Get-SshHostKey -HostName sftp.example.com
```

You only need to do this once; keep the fingerprint with your script.

### 2. Connect

```powershell
$fingerprint = 'ssh-ed25519 255 X5cCJufoqU3+Or4iPYZA7O2Ztw7v49wVFfGh9+ViYOc'
Connect-SftpServer -HostName sftp.example.com -Credential (Get-Credential) -HostKeyFingerprint $fingerprint
```

Or with a private key:

```powershell
Connect-SftpServer -HostName sftp.example.com -UserName deploy -KeyFile ./deploy.ppk -HostKeyFingerprint $fingerprint
```

Instead of a fingerprint you can use a `known_hosts` file, like `ssh` does: the first connection
adds the server, and later connections are refused if its key changes.

```powershell
Connect-SftpServer -HostName sftp.example.com -Credential (Get-Credential) -KnownHostsFile ~/.ssh/known_hosts
```

For testing only, `-SkipHostKeyCheck` connects without checking the server at all.

The other commands use this connection automatically; there is no need to pass it around.

### 3. Transfer files

Local paths are on your computer; remote paths (starting with `/`) are on the server.

```powershell
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
| `Send-ScpFile` / `Receive-ScpFile` | Uploads and downloads with SCP |

The commands work together in the pipeline: pipe `Get-ChildItem` into `Send-SftpFile`, or `Get-SftpFile`
into `Receive-SftpFile`, `Move-SftpFile` or `Remove-SftpFile`, and pipe transfer results on to the next
step. Paths accept wildcards; use `-LiteralPath` for names that contain `[` or `]`. Commands that change
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
