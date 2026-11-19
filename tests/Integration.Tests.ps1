# Runs against the bundled test server (tests/sftp.TestServer), started by tests/Start-TestServer.ps1.
# To test against another server instead, set SFTP_TEST_HOST, SFTP_TEST_PORT, SFTP_TEST_USER,
# SFTP_TEST_PASSWORD and SFTP_TEST_KEYFILE (an unencrypted key authorized on that server).
BeforeDiscovery {
    $external = [bool]$env:SFTP_TEST_HOST
}

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../output/sftp.psd1') -Force
    if ($env:SFTP_TEST_HOST) {
        $testServer = [pscustomobject]@{
            HostName = $env:SFTP_TEST_HOST
            Port     = [int]$env:SFTP_TEST_PORT
            UserName = $env:SFTP_TEST_USER
            Password = $env:SFTP_TEST_PASSWORD
            KeyFile  = $env:SFTP_TEST_KEYFILE
            Process  = $null
        }
    }
    else {
        $testServer = & (Join-Path $PSScriptRoot 'Start-TestServer.ps1')
    }

    $server = @{ HostName = $testServer.HostName; Port = $testServer.Port }
    $hostKey = Get-SshHostKey @server
    $password = ConvertTo-SecureString $testServer.Password -AsPlainText -Force
    $credential = [pscredential]::new($testServer.UserName, $password)
    $session = Connect-SftpServer @server -Credential $credential -HostKeyFingerprint $hostKey.Fingerprint
    $remote = '/upload/test-' + [guid]::NewGuid().ToString('n')
    $null = New-SftpDirectory -Session $session -Path $remote
}

AfterAll {
    if ($session) {
        Remove-SftpFile -Session $session -Path $remote -Recurse -Confirm:$false -ErrorAction SilentlyContinue
        Disconnect-SftpServer -Session $session
    }
    if ($testServer.Process) {
        & (Join-Path $PSScriptRoot 'Start-TestServer.ps1') -Stop -Server $testServer
    }
}

Describe 'Host keys and login' -Tag Integration {
    It 'Reads the host key without logging in' {
        $hostKey.Sha256 | Should -Match '^[A-Za-z0-9+/]{43}$'
        $hostKey.Fingerprint | Should -BeLike "$($hostKey.Algorithm) $($hostKey.KeyLength) *"
    }

    It 'Accepts every supported fingerprint form' {
        foreach ($form in $hostKey.Fingerprint, $hostKey.OpenSshFingerprint, $hostKey.Sha256, "MD5:$($hostKey.Md5)") {
            $other = Connect-SftpServer @server -Credential $credential -HostKeyFingerprint $form
            $other.IsConnected | Should -BeTrue
            $other.HostKey.Sha256 | Should -Be $hostKey.Sha256
            Disconnect-SftpServer -Session $other
            $other.IsConnected | Should -BeFalse
        }
    }

    It 'Refuses a server whose host key does not match' {
        $wrong = 'SHA256:' + ('A' * 43)
        { Connect-SftpServer @server -Credential $credential -HostKeyFingerprint $wrong -ErrorAction Stop } |
            Should -Throw -ErrorId 'HostKeyMismatch,sftp.Commands.ConnectSftpServerCommand'
    }

    It 'Accepts the key when any of several fingerprints matches' {
        $other = Connect-SftpServer @server -Credential $credential -HostKeyFingerprint ('SHA256:' + ('A' * 43)), $hostKey.Fingerprint
        $other.IsConnected | Should -BeTrue
        Disconnect-SftpServer -Session $other
    }

    It 'Logs in with a private key file' {
        $other = Connect-SftpServer @server -UserName $testServer.UserName -KeyFile $testServer.KeyFile -HostKeyFingerprint $hostKey.Fingerprint
        $other.IsConnected | Should -BeTrue
        Disconnect-SftpServer -Session $other
    }

    It 'Logs in with a private key passed as a secure string' {
        $key = ConvertTo-SecureString (Get-Content -LiteralPath $testServer.KeyFile -Raw) -AsPlainText -Force
        $other = Connect-SftpServer @server -UserName $testServer.UserName -PrivateKey $key -HostKeyFingerprint $hostKey.Fingerprint
        $other.IsConnected | Should -BeTrue
        Disconnect-SftpServer -Session $other
    }

    It 'Reports a wrong password as an authentication failure' {
        $bad = [pscredential]::new($testServer.UserName, (ConvertTo-SecureString 'wrong' -AsPlainText -Force))
        { Connect-SftpServer @server -Credential $bad -HostKeyFingerprint $hostKey.Fingerprint -ErrorAction Stop } |
            Should -Throw -ErrorId 'AuthenticationFailed,sftp.Commands.ConnectSftpServerCommand'
    }
}

Describe 'SFTP file operations' -Tag Integration {
    BeforeAll {
        $local = Join-Path $TestDrive 'local'
        $download = Join-Path $TestDrive 'download'
        $null = New-Item -ItemType Directory -Path $local, $download
        'alpha' | Set-Content -LiteralPath (Join-Path $local 'a.txt')
        'bravo' | Set-Content -LiteralPath (Join-Path $local 'b.csv')
        $big = Join-Path $local 'big.bin'
        [IO.File]::WriteAllBytes($big, [byte[]]::new(3MB))
        (Get-Item $big).LastWriteTimeUtc = [datetime]::new(2020, 1, 2, 3, 4, 5, [DateTimeKind]::Utc)
    }

    It 'Uploads files matched by a wildcard and reports each one' {
        $results = Send-SftpFile -Session $session -Path (Join-Path $local '*') -Destination $remote
        $results.Count | Should -Be 3
        $results | Should -BeOfType ([sftp.SftpTransferResult])
        @($results | Where-Object { -not $_.Success }).Count | Should -Be 0
        ($results | Where-Object { $_.Source -like '*a.txt' }).Destination | Should -Be "$remote/a.txt"
    }

    It 'Refuses to overwrite without -Force, and overwrites with it' {
        $failed = Send-SftpFile -Session $session -Path (Join-Path $local 'a.txt') -Destination $remote -ErrorAction SilentlyContinue -ErrorVariable uploadError
        $failed.Success | Should -BeFalse
        $failed.Error | Should -BeLike '*already exists*'
        $uploadError[0].FullyQualifiedErrorId | Should -Be 'UploadFailed,sftp.Commands.SendSftpFileCommand'
        (Send-SftpFile -Session $session -Path (Join-Path $local 'a.txt') -Destination $remote -Force).Success | Should -BeTrue
    }

    It 'Uploads through a temporary name and leaves no partial file behind' {
        $result = Send-SftpFile -Session $session -LiteralPath $big -Destination $remote -Force -UseTemporaryName -PreserveTimestamp
        $result.Success | Should -BeTrue
        $files = Get-SftpFile -Session $session -Path $remote -File
        $files.Name | Should -Not -Contain 'big.bin.filepart'
        ($files | Where-Object Name -EQ 'big.bin').Length | Should -Be 3MB
        ($files | Where-Object Name -EQ 'big.bin').LastWriteTimeUtc | Should -Be ([datetime]::new(2020, 1, 2, 3, 4, 5, [DateTimeKind]::Utc))
    }

    It 'Accepts files from the pipeline' {
        $results = Get-ChildItem -LiteralPath $local -Filter '*.csv' | Send-SftpFile -Session $session -Destination $remote -Force
        $results.Success | Should -BeTrue
    }

    It 'Changes nothing with -WhatIf' {
        Send-SftpFile -Session $session -Path (Join-Path $local 'a.txt') -Destination "$remote/missing-dir" -WhatIf | Should -BeNullOrEmpty
        Remove-SftpFile -Session $session -Path "$remote/a.txt" -WhatIf
        (Get-SftpFile -Session $session -Path "$remote/a.txt").Name | Should -Be 'a.txt'
    }

    It 'Lists with a filter, files only and recursively' {
        $null = New-SftpDirectory -Session $session -Path "$remote/sub/deeper" -Force
        $null = Send-SftpFile -Session $session -Path (Join-Path $local 'a.txt') -Destination "$remote/sub/deeper"
        @(Get-SftpFile -Session $session -Path $remote -Filter '*.TXT').Name | Should -Be @('a.txt')
        @(Get-SftpFile -Session $session -Path $remote -Filter '*.TXT' -CaseSensitive).Count | Should -Be 0
        @(Get-SftpFile -Session $session -Path $remote -Directory).Name | Should -Be @('sub')
        @(Get-SftpFile -Session $session -Path $remote -Recurse -File -Filter 'a.txt').FullName | Sort-Object |
            Should -Be @("$remote/a.txt", "$remote/sub/deeper/a.txt")
    }

    It 'Downloads what Get-SftpFile lists, with timestamps' {
        $results = Get-SftpFile -Session $session -Path $remote -File | Receive-SftpFile -Session $session -Destination $download -PreserveTimestamp
        @($results | Where-Object Success).Count | Should -Be 3
        Get-Content -LiteralPath (Join-Path $download 'a.txt') | Should -Be 'alpha'
        (Get-Item (Join-Path $download 'big.bin')).Length | Should -Be 3MB
        (Get-Item (Join-Path $download 'big.bin')).LastWriteTimeUtc | Should -Be ([datetime]::new(2020, 1, 2, 3, 4, 5, [DateTimeKind]::Utc))
        @(Get-ChildItem $download -Filter '*.filepart').Count | Should -Be 0
    }

    It 'Refuses to overwrite a local file without -Force' {
        $result = Receive-SftpFile -Session $session -Path "$remote/a.txt" -Destination $download -ErrorAction SilentlyContinue -ErrorVariable downloadError
        $result.Success | Should -BeFalse
        $downloadError[0].FullyQualifiedErrorId | Should -Be 'DownloadFailed,sftp.Commands.ReceiveSftpFileCommand'
        (Receive-SftpFile -Session $session -Path "$remote/a.txt" -Destination $download -Force).Success | Should -BeTrue
    }

    It 'Reports a missing remote file as not found' {
        $result = Receive-SftpFile -Session $session -Path "$remote/nope.txt" -Destination $download -ErrorAction SilentlyContinue -ErrorVariable downloadError
        $result.Success | Should -BeFalse
        $downloadError[0].CategoryInfo.Category | Should -Be 'ObjectNotFound'
    }

    It 'Moves into a directory, renames, and replaces only with -Force' {
        $null = New-SftpDirectory -Session $session -Path "$remote/done"
        (Move-SftpFile -Session $session -Path "$remote/b.csv" -Destination "$remote/done" -PassThru).FullName | Should -Be "$remote/done/b.csv"
        $null = Send-SftpFile -Session $session -Path (Join-Path $local 'b.csv') -Destination $remote
        Move-SftpFile -Session $session -Path "$remote/b.csv" -Destination "$remote/done/b.csv" -ErrorAction SilentlyContinue -ErrorVariable moveError
        $moveError[0].FullyQualifiedErrorId | Should -Be 'MoveFailed,sftp.Commands.MoveSftpFileCommand'
        Move-SftpFile -Session $session -Path "$remote/b.csv" -Destination "$remote/done/b.csv" -Force
        @(Get-SftpFile -Session $session -Path "$remote/done").Name | Should -Be @('b.csv')
        @(Get-SftpFile -Session $session -Path $remote -Filter 'b.csv').Count | Should -Be 0
    }

    It 'Removes files, and directories only with -Recurse when not empty' {
        Remove-SftpFile -Session $session -Path "$remote/a.txt" -Confirm:$false
        Remove-SftpFile -Session $session -Path "$remote/sub" -Confirm:$false -ErrorAction SilentlyContinue -ErrorVariable removeError
        $removeError | Should -Not -BeNullOrEmpty
        Remove-SftpFile -Session $session -Path "$remote/sub" -Recurse -Confirm:$false
        (Get-SftpFile -Session $session -Path $remote).Name | Should -Not -Contain 'sub'
        (Get-SftpFile -Session $session -Path $remote).Name | Should -Not -Contain 'a.txt'
    }

    It 'Creates parent directories with -Force and tolerates existing ones' {
        (New-SftpDirectory -Session $session -Path "$remote/x/y/z" -Force).IsDirectory | Should -BeTrue
        (New-SftpDirectory -Session $session -Path "$remote/x/y/z" -Force).IsDirectory | Should -BeTrue
        New-SftpDirectory -Session $session -Path "$remote/x" -ErrorAction SilentlyContinue -ErrorVariable mkdirError
        $mkdirError | Should -Not -BeNullOrEmpty
    }

    It 'Rejects a closed session' {
        $closed = Connect-SftpServer @server -Credential $credential -HostKeyFingerprint $hostKey.Fingerprint
        Disconnect-SftpServer -Session $closed
        { Get-SftpFile -Session $closed -ErrorAction Stop } | Should -Throw -ErrorId 'SessionClosed,sftp.Commands.GetSftpFileCommand'
    }
}

Describe 'SCP transfers' -Tag Integration {
    BeforeAll {
        $scpLocal = Join-Path $TestDrive 'scp'
        $scpDownload = Join-Path $TestDrive 'scp-download'
        $null = New-Item -ItemType Directory -Path $scpLocal, $scpDownload
        # A name that would break an unquoted remote shell command.
        $odd = Join-Path $scpLocal 'odd name; touch pwned.txt'
        'charlie' | Set-Content -LiteralPath $odd
    }

    It 'Uploads and downloads with shell-quoted remote paths' {
        $up = Send-ScpFile -Session $session -Path $odd -Destination $remote
        $up.Success | Should -BeTrue
        $up.Protocol | Should -Be 'Scp'
        $names = (Get-SftpFile -Session $session -Path $remote).Name
        $names | Should -Contain 'odd name; touch pwned.txt'
        $names | Should -Not -Contain 'pwned.txt'
        $down = Receive-ScpFile -Session $session -Path "$remote/odd name; touch pwned.txt" -Destination $scpDownload
        $down.Success | Should -BeTrue
        Get-Content -LiteralPath (Join-Path $scpDownload 'odd name; touch pwned.txt') | Should -Be 'charlie'
    }
}

Describe 'Behaviour that needs the bundled test server' -Tag Integration -Skip:$external {
    It 'Logs in with an encrypted key and its passphrase' {
        $passphrase = ConvertTo-SecureString $testServer.KeyPassphrase -AsPlainText -Force
        $other = Connect-SftpServer @server -UserName $testServer.UserName -KeyFile $testServer.EncryptedKeyFile -KeyPassphrase $passphrase -HostKeyFingerprint $hostKey.Fingerprint
        $other.IsConnected | Should -BeTrue
        Disconnect-SftpServer -Session $other
    }

    It 'Asks for the passphrase of an encrypted key' {
        { Connect-SftpServer @server -UserName $testServer.UserName -KeyFile $testServer.EncryptedKeyFile -HostKeyFingerprint $hostKey.Fingerprint -ErrorAction Stop } |
            Should -Throw -ErrorId 'KeyPassphraseRequired,sftp.Commands.ConnectSftpServerCommand'
    }

    It 'Never ran an injected SCP command' {
        Join-Path $testServer.Root 'injection-detected.txt' | Should -Not -Exist
    }

    It 'Replaces files without posix-rename when the server lacks it' {
        $plain = & (Join-Path $PSScriptRoot 'Start-TestServer.ps1') -NoPosixRename
        try {
            $plainKey = Get-SshHostKey -HostName $plain.HostName -Port $plain.Port
            $plainCredential = [pscredential]::new($plain.UserName, (ConvertTo-SecureString $plain.Password -AsPlainText -Force))
            $plainSession = Connect-SftpServer -HostName $plain.HostName -Port $plain.Port -Credential $plainCredential -HostKeyFingerprint $plainKey.Fingerprint
            $file = Join-Path $TestDrive 'replace.txt'
            'one' | Set-Content -LiteralPath $file
            $null = Send-SftpFile -Session $plainSession -LiteralPath $file -Destination /upload
            'two' | Set-Content -LiteralPath $file
            (Send-SftpFile -Session $plainSession -LiteralPath $file -Destination /upload -Force -UseTemporaryName).Success | Should -BeTrue
            Get-Content -LiteralPath (Join-Path $plain.Root 'upload/replace.txt') | Should -Be 'two'
            Join-Path $plain.Root 'upload/replace.txt.filepart' | Should -Not -Exist

            $null = Send-SftpFile -Session $plainSession -LiteralPath $file -Destination / -Force
            Move-SftpFile -Session $plainSession -Path /replace.txt -Destination /upload/replace.txt -Force
            Join-Path $plain.Root 'replace.txt' | Should -Not -Exist
            Disconnect-SftpServer -Session $plainSession
        }
        finally {
            & (Join-Path $PSScriptRoot 'Start-TestServer.ps1') -Stop -Server $plain
        }
    }
}

Describe 'Choosing how to check the host key' -Tag Integration {
    It 'Connects without a check when asked, and warns' {
        $other = Connect-SftpServer @server -Credential $credential -SkipHostKeyCheck -WarningVariable warning -WarningAction SilentlyContinue
        $other.IsConnected | Should -BeTrue
        $other.HostKeyVerification | Should -Be 'Skipped'
        $warning[0] | Should -BeLike "*not checked*$($hostKey.Sha256)*"
        Disconnect-SftpServer -Session $other
    }

    It 'Adds an unknown host to a known_hosts file, then trusts only that key' {
        $knownHosts = Join-Path $TestDrive 'ssh/known_hosts'
        $first = Connect-SftpServer @server -Credential $credential -KnownHostsFile $knownHosts -WarningVariable warning -WarningAction SilentlyContinue
        $first.HostKeyVerification | Should -Be 'KnownHostsAdded'
        $warning[0] | Should -BeLike 'Added *'
        Disconnect-SftpServer -Session $first
        (Get-Content -LiteralPath $knownHosts) | Should -BeLike "``[$($server.HostName)``]:$($server.Port) ecdsa-sha2-nistp256 *"

        $second = Connect-SftpServer @server -Credential $credential -KnownHostsFile $knownHosts -WarningVariable warning
        $second.HostKeyVerification | Should -Be 'KnownHosts'
        $warning | Should -BeNullOrEmpty
        Disconnect-SftpServer -Session $second

        # Same host, different key: refused.
        $fields = (Get-Content -LiteralPath $knownHosts) -split ' '
        $blob = [Convert]::FromBase64String($fields[2])
        $blob[-1] = $blob[-1] -bxor 0xFF
        Set-Content -LiteralPath $knownHosts -Value "$($fields[0]) $($fields[1]) $([Convert]::ToBase64String($blob))"
        { Connect-SftpServer @server -Credential $credential -KnownHostsFile $knownHosts -ErrorAction Stop } |
            Should -Throw -ErrorId 'HostKeyMismatch,sftp.Commands.ConnectSftpServerCommand'
    }

    It 'Requires exactly one way of checking the host key' {
        { Connect-SftpServer @server -Credential $credential -ErrorAction Stop } |
            Should -Throw -ErrorId 'HostKeyCheckRequired,sftp.Commands.ConnectSftpServerCommand'
        { Connect-SftpServer @server -Credential $credential -HostKeyFingerprint $hostKey.Fingerprint -SkipHostKeyCheck -ErrorAction Stop } |
            Should -Throw -ErrorId 'HostKeyCheckRequired,sftp.Commands.ConnectSftpServerCommand'
    }

    It 'Reports how a pinned fingerprint was checked' {
        $session.HostKeyVerification | Should -Be 'Fingerprint'
    }
}

Describe 'Pipelines, wildcards and literal paths' -Tag Integration {
    BeforeEach {
        $dir = "$remote/pipe-" + [guid]::NewGuid().ToString('n').Substring(0, 8)
        $null = New-SftpDirectory -Session $session -Path "$dir/ready" -Force
        $local = Join-Path $TestDrive ([guid]::NewGuid().ToString('n'))
        $download = Join-Path $local 'FromSftp'
        $upload = Join-Path $local 'ToSftp'
        $null = New-Item -ItemType Directory -Path $download, $upload
        foreach ($name in 'one.csv', 'two.csv', 'skip.tmp', 'a1.txt') {
            $name | Set-Content -LiteralPath (Join-Path $upload $name)
        }
        'bracket' | Set-Content -LiteralPath (Join-Path $upload 'a[1].txt')
    }

    It 'Downloads what Get-SftpFile lists, then deletes only what arrived' {
        $null = Get-ChildItem -LiteralPath $upload -Filter *.csv | Send-SftpFile -Session $session -Destination $dir
        Get-SftpFile -Session $session -Path $dir -File |
            Receive-SftpFile -Session $session -Destination $download |
            Where-Object Success |
            Remove-SftpFile -Session $session
        @(Get-SftpFile -Session $session -Path $dir -File).Count | Should -Be 0
        (Get-ChildItem -LiteralPath $download).Name | Sort-Object | Should -Be @('one.csv', 'two.csv')
    }

    It 'Uploads Get-ChildItem output and moves what was sent' {
        Get-ChildItem -LiteralPath $upload -Filter *.csv |
            Send-SftpFile -Session $session -Destination $dir |
            Where-Object Success |
            Move-SftpFile -Session $session -Destination "$dir/ready/"
        (Get-SftpFile -Session $session -Path "$dir/ready").Name | Sort-Object | Should -Be @('one.csv', 'two.csv')
    }

    It 'Expands wildcards in remote paths' {
        $null = Send-SftpFile -Session $session -Path (Join-Path $upload '*') -Destination $dir
        (Receive-SftpFile -Session $session -Path "$dir/*.csv" -Destination $download).Count | Should -Be 2
        Remove-SftpFile -Session $session -Path "$dir/*.tmp"
        (Get-SftpFile -Session $session -Path "$dir/*").Name | Should -Not -Contain 'skip.tmp'
        (Get-SftpFile -Session $session -Path "$dir/*.csv").Name | Sort-Object | Should -Be @('one.csv', 'two.csv')
    }

    It 'Reports a wildcard that matches nothing' {
        Remove-SftpFile -Session $session -Path "$dir/*.nothing" -ErrorVariable removeError -ErrorAction SilentlyContinue
        $removeError[0].FullyQualifiedErrorId | Should -Be 'RemoveFailed,sftp.Commands.RemoveSftpFileCommand'
        $removeError[0].CategoryInfo.Category | Should -Be 'ObjectNotFound'
    }

    It 'Treats brackets literally with -LiteralPath and as wildcards with -Path' {
        $null = Send-SftpFile -Session $session -LiteralPath (Join-Path $upload 'a[1].txt'), (Join-Path $upload 'a1.txt') -Destination $dir
        (Get-SftpFile -Session $session -LiteralPath "$dir/a[1].txt").Name | Should -Be 'a[1].txt'
        (Get-SftpFile -Session $session -Path "$dir/a[1].txt").Name | Should -Be 'a1.txt'
        (Receive-SftpFile -Session $session -LiteralPath "$dir/a[1].txt" -Destination $download).Success | Should -BeTrue
        Get-Content -LiteralPath (Join-Path $download 'a[1].txt') | Should -Be 'bracket'
    }

    It 'Accepts remote paths as strings from the pipeline' {
        $null = Send-SftpFile -Session $session -Path (Join-Path $upload '*.csv') -Destination $dir
        "$dir/one.csv", "$dir/two.csv" | Remove-SftpFile -Session $session
        @(Get-SftpFile -Session $session -Path $dir -File).Count | Should -Be 0
    }

    It 'Pipes download results back into an upload' {
        $null = Send-SftpFile -Session $session -Path (Join-Path $upload 'one.csv') -Destination $dir
        Receive-SftpFile -Session $session -Path "$dir/one.csv" -Destination $download |
            Send-SftpFile -Session $session -Destination "$dir/ready" |
            ForEach-Object { $_.RemotePath | Should -Be "$dir/ready/one.csv" }
    }

    It 'Exposes the remote and local path of every transfer' {
        $up = Send-SftpFile -Session $session -Path (Join-Path $upload 'one.csv') -Destination $dir
        $up.RemotePath | Should -Be "$dir/one.csv"
        $up.LocalPath | Should -Be (Join-Path $upload 'one.csv')
        $down = Receive-SftpFile -Session $session -Path "$dir/one.csv" -Destination $download
        $down.RemotePath | Should -Be "$dir/one.csv"
        $down.LocalPath | Should -Be (Join-Path $download 'one.csv')
    }

    It 'Uploads with SCP using wildcards and literal paths' {
        (Send-ScpFile -Session $session -Path (Join-Path $upload '*.csv') -Destination $dir).Count | Should -Be 2
        (Get-ChildItem -LiteralPath $upload | Where-Object Name -EQ 'a[1].txt' | Send-ScpFile -Session $session -Destination $dir).Success | Should -BeTrue
        (Get-SftpFile -Session $session -LiteralPath "$dir/a[1].txt" | Receive-ScpFile -Session $session -Destination $download).Success | Should -BeTrue
    }
}

Describe 'Sessions' -Tag Integration -Skip:$external {
    BeforeAll {
        $second = & (Join-Path $PSScriptRoot 'Start-TestServer.ps1')
        $secondKey = Get-SshHostKey -HostName $second.HostName -Port $second.Port
        $secondCredential = [pscredential]::new($second.UserName, (ConvertTo-SecureString $second.Password -AsPlainText -Force))
        $connectFirst = @{ HostName = $testServer.HostName; Port = $testServer.Port; Credential = $credential; HostKeyFingerprint = $hostKey.Fingerprint }
        $connectSecond = @{ HostName = $second.HostName; Port = $second.Port; Credential = $secondCredential; HostKeyFingerprint = $secondKey.Fingerprint }
    }

    AfterAll {
        Get-SftpSession -Name 'first', 'other' -ErrorAction SilentlyContinue | Disconnect-SftpServer
        & (Join-Path $PSScriptRoot 'Start-TestServer.ps1') -Stop -Server $second
    }

    It 'Uses the most recent session when -Session is omitted' {
        $first = Connect-SftpServer @connectFirst -Name first
        $first.IsCurrent | Should -BeTrue
        $items = Get-SftpFile -Path /upload
        $items | Should -Not -BeNullOrEmpty
        $items[0].SessionName | Should -Be 'first'
    }

    It 'Sends piped items back to their own session, even when another session is current' {
        $other = Connect-SftpServer @connectSecond -Name other
        $other.IsCurrent | Should -BeTrue
        'only on the first server' | Set-Content -LiteralPath (Join-Path $testServer.Root 'upload/routing.txt')
        $download = Join-Path $TestDrive 'routing'
        $null = New-Item -ItemType Directory -Path $download
        $result = Get-SftpFile -Session first -Path /upload/routing.txt | Receive-SftpFile -Destination $download
        $result.Success | Should -BeTrue
        $result.SessionName | Should -Be 'first'
        $result | Remove-SftpFile
        Join-Path $testServer.Root 'upload/routing.txt' | Should -Not -Exist
    }

    It 'Accepts a session name for -Session' {
        (Get-SftpFile -Session other -Path /).SessionName | Select-Object -Unique | Should -Be 'other'
    }

    It 'Lists open sessions and marks the current one' {
        $sessions = Get-SftpSession
        $sessions.Name | Should -Contain 'first'
        $sessions.Name | Should -Contain 'other'
        ($sessions | Where-Object IsCurrent).Name | Should -Be 'other'
        (Get-SftpSession -Name 'fir*').Name | Should -Be 'first'
        Get-SftpSession -Name nope -ErrorVariable missing -ErrorAction SilentlyContinue
        $missing[0].FullyQualifiedErrorId | Should -Be 'SessionNotFound,sftp.Commands.GetSftpSessionCommand'
    }

    It 'Refuses a duplicate name and makes default names unique' {
        { Connect-SftpServer @connectSecond -Name other -ErrorAction Stop } |
            Should -Throw -ErrorId 'SessionNameInUse,sftp.Commands.ConnectSftpServerCommand'
        $a = Connect-SftpServer @connectSecond
        $b = Connect-SftpServer @connectSecond
        $a.Name | Should -Not -Be $b.Name
        $b.Name | Should -BeLike "$($a.Name) (*)"
        $a, $b | Disconnect-SftpServer
    }

    It 'Disconnects the current session by default and falls back to the previous one' {
        $items = Get-SftpFile -Session other -Path /
        Disconnect-SftpServer
        (Get-SftpSession).Name | Should -Not -Contain 'other'
        (Get-SftpSession | Where-Object IsCurrent).Name | Should -Not -Be 'other'
        { $items | Get-SftpFile -ErrorAction Stop } | Should -Throw '*No open session named*other*'
    }

    It 'Keeps sessions separate per runspace' {
        $shell = [powershell]::Create()
        try {
            $null = $shell.AddScript({
                    param($module)
                    Import-Module $module
                    @{ Sessions = @(Get-SftpSession).Count; Error = try { Get-SftpFile -ErrorAction Stop; '' } catch { $_.FullyQualifiedErrorId } }
                }).AddArgument((Join-Path $PSScriptRoot '../output/sftp.psd1'))
            $outcome = $shell.Invoke()[0]
            $outcome.Sessions | Should -Be 0
            $outcome.Error | Should -Be 'NotConnected,sftp.Commands.GetSftpFileCommand'
        }
        finally {
            $shell.Dispose()
        }
    }
}
