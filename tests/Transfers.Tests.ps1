# One-shot transfers and folder synchronization.
# Runs against the bundled test server unless SFTP_TEST_HOST and friends are set (see TestContext.ps1).
BeforeDiscovery {
    $external = [bool]$env:SFTP_TEST_HOST
}

BeforeAll {
    . (Join-Path $PSScriptRoot 'TestContext.ps1')
}

AfterAll {
    . (Join-Path $PSScriptRoot 'TestContext.ps1') -Cleanup
}
Describe 'One-shot transfers' -Tag Integration {
    BeforeAll {
        $file = Join-Path $TestDrive 'oneshot.txt'
        'once' | Set-Content -LiteralPath $file
    }

    It 'Uploads and downloads with connection parameters instead of a session' {
        $before = @(Get-SftpSession).Count
        $up = Send-SftpFile @connect -Path $file -Destination "$remote/"
        $up.Success | Should -BeTrue
        $up.SessionName | Should -Not -Be 'main'
        $down = Receive-SftpFile @connect -Path "$remote/oneshot.txt" -Destination (Join-Path $TestDrive 'oneshot-back.txt')
        $down.Success | Should -BeTrue
        Get-Content (Join-Path $TestDrive 'oneshot-back.txt') | Should -Be 'once'
        @(Get-SftpSession).Count | Should -Be $before
        (Get-SftpSession | Where-Object IsCurrent).Name | Should -Be 'main'
    }

    It 'Closes its session when the transfer fails' {
        $before = @(Get-SftpSession).Count
        $result = Receive-SftpFile @connect -Path "$remote/missing.txt" -Destination $TestDrive -ErrorAction SilentlyContinue
        $result.Success | Should -BeFalse
        @(Get-SftpSession).Count | Should -Be $before
    }

    It 'Reports connection failures as terminating errors' {
        { Send-SftpFile -HostName $testServer.HostName -Port $testServer.Port -Credential $credential -HostKeyFingerprint ('SHA256:' + ('A' * 43)) -Path $file -ErrorAction Stop } |
            Should -Throw -ErrorId 'HostKeyMismatch,sftp.Commands.SendSftpFileCommand'
    }

    It 'Refuses -Session together with -HostName, and connection options without -HostName' {
        { Send-SftpFile @connect -Session main -Path $file -ErrorAction Stop } | Should -Throw -ErrorId 'SessionAndHostName,sftp.Commands.SendSftpFileCommand'
        { Send-SftpFile -Port 2222 -Path $file -ErrorAction Stop } | Should -Throw -ErrorId 'HostNameRequired,sftp.Commands.SendSftpFileCommand'
    }
}

Describe 'Synchronizing folders' -Tag Integration {
    BeforeAll {
        $source = Join-Path $TestDrive 'sync-source'
        $null = New-Item -ItemType Directory -Path (Join-Path $source 'sub') -Force
        'one' | Set-Content -LiteralPath (Join-Path $source 'one.txt')
        'two' | Set-Content -LiteralPath (Join-Path $source 'sub/two.txt')
        $target = "$remote/sync"
    }

    It 'Uploads everything the first time and nothing the second time' {
        $first = Sync-SftpFolder -Session $session -LocalPath $source -RemotePath $target -Direction Upload
        @($first | Where-Object Operation -EQ 'Upload').Count | Should -Be 2
        @($first | Where-Object { -not $_.Success }).Count | Should -Be 0
        Get-SftpContent -Session $session -Path "$target/sub/two.txt" | Should -Be 'two'
        @(Sync-SftpFolder -Session $session -LocalPath $source -RemotePath $target -Direction Upload).Count | Should -Be 0
    }

    It 'Uploads only changed files' {
        'changed!' | Set-Content -LiteralPath (Join-Path $source 'one.txt')
        $results = Sync-SftpFolder -Session $session -LocalPath $source -RemotePath $target -Direction Upload
        $results.Destination | Should -Be "$target/one.txt"
    }

    It 'Removes extra remote files only with -Remove, and not with -WhatIf' {
        Set-SftpContent -Session $session -Path "$target/extra.txt" -Value 'extra'
        @(Sync-SftpFolder -Session $session -LocalPath $source -RemotePath $target -Direction Upload).Count | Should -Be 0
        Sync-SftpFolder -Session $session -LocalPath $source -RemotePath $target -Direction Upload -Remove -WhatIf
        Test-SftpPath -Session $session -Path "$target/extra.txt" | Should -BeTrue
        $removed = Sync-SftpFolder -Session $session -LocalPath $source -RemotePath $target -Direction Upload -Remove
        $removed.Operation | Should -Be 'Remove'
        $removed.Source | Should -Be "$target/extra.txt"
        Test-SftpPath -Session $session -Path "$target/extra.txt" | Should -BeFalse
    }

    It 'Downloads into an empty folder and removes local extras with -Remove' {
        $copy = Join-Path $TestDrive 'sync-copy'
        $down = Sync-SftpFolder -Session $session -LocalPath $copy -RemotePath $target -Direction Download
        @($down | Where-Object Operation -EQ 'Download').Count | Should -Be 2
        Get-Content -LiteralPath (Join-Path $copy 'sub/two.txt') | Should -Be 'two'
        'local only' | Set-Content -LiteralPath (Join-Path $copy 'local.txt')
        @(Sync-SftpFolder -Session $session -LocalPath $copy -RemotePath $target -Direction Download).Count | Should -Be 0
        (Sync-SftpFolder -Session $session -LocalPath $copy -RemotePath $target -Direction Download -Remove).Operation | Should -Be 'Remove'
        Join-Path $copy 'local.txt' | Should -Not -Exist
    }
}
