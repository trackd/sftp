# File permissions and disk space.
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
Describe 'Permissions' -Tag Integration {
    BeforeAll {
        $null = New-SftpDirectory -Session $session -Path "$remote/perm"
        Set-SftpContent -Session $session -Path "$remote/perm/file.sh" -Value 'echo'
        Set-SftpContent -Session $session -Path "$remote/perm/other.txt" -Value 'text'
    }

    It 'Shows permissions in ls and octal form' {
        $permission = Get-SftpPermission -Session $session -Path "$remote/perm/file.sh"
        $permission | Should -BeOfType ([sftp.SftpPermissionInfo])
        $permission.Octal | Should -Match '^0[0-7]{3}$'
        $permission.Mode | Should -Match '^-[rwxsStT-]{9}$'
        (Get-SftpPermission -Session $session -Path "$remote/perm").Mode | Should -BeLike 'd*'
    }

    It 'Sets octal and symbolic modes' {
        (Set-SftpPermission -Session $session -Path "$remote/perm/file.sh" -Mode 0644 -PassThru).Octal | Should -Be '0644'
        (Set-SftpPermission -Session $session -Path "$remote/perm/file.sh" -Mode 'u+x,g+x' -PassThru).Octal | Should -Be '0754'
        (Set-SftpPermission -Session $session -Path "$remote/perm/file.sh" -Mode 'go-rwx' -PassThru).Mode | Should -Be '-rwx------'
        (Get-SftpPermission -Session $session -Path "$remote/perm/file.sh").Octal | Should -Be '0700'
    }

    It 'Changes folders and their contents with -Recurse' {
        $changed = Set-SftpPermission -Session $session -Path "$remote/perm" -Mode 0750 -Recurse -PassThru
        $changed.Count | Should -Be 3
        $changed.Octal | Sort-Object -Unique | Should -Be '0750'
    }

    It 'Changes nothing with -WhatIf' {
        $before = (Get-SftpPermission -Session $session -Path "$remote/perm/other.txt").Octal
        Set-SftpPermission -Session $session -Path "$remote/perm/other.txt" -Mode 0600 -WhatIf
        (Get-SftpPermission -Session $session -Path "$remote/perm/other.txt").Octal | Should -Be $before
    }

    It 'Rejects a mode that is not valid' {
        { Set-SftpPermission -Session $session -Path "$remote/perm/other.txt" -Mode 'u+q' -ErrorAction Stop } |
            Should -Throw -ErrorId 'InvalidMode,sftp.Commands.SetSftpPermissionCommand'
    }
}

Describe 'Disk space' -Tag Integration {
    It 'Reports total, free and available space' {
        $space = Get-SftpDiskSpace -Session $session -Path $remote
        $space | Should -BeOfType ([sftp.SftpDiskSpace])
        $space.Path | Should -Be $remote
        $space.TotalBytes | Should -BeGreaterThan 0
        $space.AvailableBytes | Should -BeLessOrEqual $space.FreeBytes
        $space.UsedBytes | Should -Be ($space.TotalBytes - $space.FreeBytes)
    }

    It 'Computes the sizes from the block counts' -Skip:$external {
        $space = Get-SftpDiskSpace -Session $session -Path /
        $space.TotalBytes | Should -Be (1000 * 4096)
        $space.FreeBytes | Should -Be (250 * 4096)
        $space.AvailableBytes | Should -Be (200 * 4096)
        $space.PercentUsed | Should -Be 75
    }

    It 'Explains when the server cannot report disk space' -Skip:$external {
        $plain = & (Join-Path $PSScriptRoot 'Start-TestServer.ps1') -NoStatVfs
        try {
            $plainCredential = [pscredential]::new($plain.UserName, (ConvertTo-SecureString $plain.Password -AsPlainText -Force))
            $plainSession = Connect-SftpServer -HostName $plain.HostName -Port $plain.Port -Credential $plainCredential -HostKeyFingerprint $plain.Fingerprint
            $result = Get-SftpDiskSpace -Session $plainSession -Path / -ErrorAction SilentlyContinue -ErrorVariable spaceError
            $result | Should -BeNullOrEmpty
            $spaceError[0].FullyQualifiedErrorId | Should -Be 'DiskSpaceNotSupported,sftp.Commands.GetSftpDiskSpaceCommand'
            $spaceError[0].Exception.Message | Should -BeLike '*statvfs@openssh.com*'
            Disconnect-SftpServer -Session $plainSession
        }
        finally {
            & (Join-Path $PSScriptRoot 'Start-TestServer.ps1') -Stop -Server $plain
        }
    }
}
