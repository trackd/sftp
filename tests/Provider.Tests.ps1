# The Sftp drive provider, and drive paths in the sftp commands.
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
Describe 'Sftp drives' -Tag Integration {
    BeforeAll {
        $drive = New-PSDrive -Name sftptest -PSProvider Sftp -Root $remote -Session main
        Set-SftpContent -Session $session -Path "$remote/hello.txt" -Value 'line1', 'line2'
        # Created here rather than by one test for the next, so each test stands on its own.
        $null = New-SftpDirectory -Session $session -Path "$remote/places"
        Set-SftpContent -Session $session -Path "$remote/places/here.txt" -Value 'relative'
        # PowerShell shows provider paths with the platform's separator: \ on Windows, / elsewhere.
        $separator = [IO.Path]::DirectorySeparatorChar
    }

    AfterAll {
        Remove-PSDrive -Name sftptest -ErrorAction SilentlyContinue
    }

    It 'Creates a drive for an open session' {
        $drive.Session.Name | Should -Be 'main'
        $drive.RemoteRoot | Should -Be $remote
        $drive.OwnsSession | Should -BeFalse
        $drive.DisplayRoot | Should -BeLike "*:$remote"
    }

    It 'Lists, tests and reads items' {
        (Get-ChildItem sftptest:\).Name | Should -Contain 'hello.txt'
        Test-Path sftptest:\hello.txt | Should -BeTrue
        Test-Path sftptest:\missing.txt | Should -BeFalse
        Test-Path sftptest:\ -PathType Container | Should -BeTrue
        $item = Get-Item sftptest:\hello.txt
        $item | Should -BeOfType ([sftp.SftpItem])
        $item.FullName | Should -Be "$remote/hello.txt"
        $item.SessionName | Should -Be 'main'
    }

    It 'Changes location into folders' {
        Push-Location sftptest:\places
        try {
            (Get-Location).Path | Should -Be "sftptest:${separator}places"
            $null = New-Item there.txt -Value 'created here'
            Get-Content there.txt | Should -Be 'created here'
            Get-Content here.txt | Should -Be 'relative'
        }
        finally {
            Pop-Location
        }
    }

    It 'Reads and writes text and bytes' {
        Get-Content sftptest:\hello.txt | Should -Be @('line1', 'line2')
        Get-Content sftptest:\hello.txt -Raw | Should -BeExactly "line1`nline2`n"
        Set-Content sftptest:\new.txt -Value 'a', 'b'
        Add-Content sftptest:\new.txt -Value 'c'
        Get-Content sftptest:\new.txt | Should -Be @('a', 'b', 'c')
        Set-Content sftptest:\bytes.bin -Value ([byte[]](0, 1, 2, 255)) -AsByteStream
        $bytes = Get-Content sftptest:\bytes.bin -AsByteStream -Raw
        $bytes | Should -Be ([byte[]](0, 1, 2, 255))
        Get-SftpContent -Session $session -Path "$remote/new.txt" -Raw | Should -BeExactly "a`nb`nc`n"
    }

    It 'Creates, renames, moves and removes items' {
        $null = New-Item sftptest:\work -ItemType Directory
        $file = New-Item sftptest:\work\one.txt -Value 'first'
        $file.Length | Should -Be 5
        { New-Item sftptest:\work\one.txt -ErrorAction Stop } | Should -Throw '*already exists*'
        $null = New-Item sftptest:\work\one.txt -Value 'again' -Force
        Rename-Item sftptest:\work\one.txt two.txt
        Test-Path sftptest:\work\two.txt | Should -BeTrue
        $null = New-Item sftptest:\work\inner -ItemType Directory
        Move-Item sftptest:\work\two.txt sftptest:\work\inner
        Test-Path sftptest:\work\inner\two.txt | Should -BeTrue
        Move-Item sftptest:\work\inner\two.txt sftptest:\work\three.txt
        Get-Content sftptest:\work\three.txt | Should -Be 'again'
        Remove-Item sftptest:\work\three.txt
        Test-Path sftptest:\work\three.txt | Should -BeFalse
        Remove-Item sftptest:\work -Recurse
        Test-Path sftptest:\work | Should -BeFalse
    }

    It 'Supports -Filter, -Recurse and -Depth' {
        $null = New-Item sftptest:\deep -ItemType Directory -Force
        $null = New-Item sftptest:\deep\a -ItemType Directory -Force
        $null = New-Item sftptest:\deep\a\b -ItemType Directory -Force
        $null = New-Item sftptest:\deep\top.log -Value 'x'
        $null = New-Item sftptest:\deep\a\b\bottom.log -Value 'y'
        $null = New-Item sftptest:\deep\a\note.txt -Value 'z'
        (Get-ChildItem sftptest:\deep -Recurse -Filter *.log).Name | Sort-Object | Should -Be @('bottom.log', 'top.log')
        (Get-ChildItem sftptest:\deep -Recurse -Depth 0).Name | Sort-Object | Should -Be @('a', 'top.log')
        (Get-ChildItem sftptest:\deep -Recurse -Depth 1).Name | Sort-Object | Should -Be @('a', 'b', 'note.txt', 'top.log')
        (Get-ChildItem sftptest:\deep\a -Include *.txt -Recurse).Name | Should -Be 'note.txt'
    }

    It 'Pipes drive items to other commands' {
        (Get-ChildItem sftptest:\hello.txt | Get-Content) | Should -Be @('line1', 'line2')
        $local = Join-Path $TestDrive 'from-drive'
        $null = New-Item -ItemType Directory -Path $local
        $result = Get-ChildItem sftptest:\hello.txt | Receive-SftpFile -Destination $local
        $result.Success | Should -BeTrue
        $result.SessionName | Should -Be 'main'
        Get-Content (Join-Path $local 'hello.txt') | Should -Be @('line1', 'line2')
    }

    It 'Accepts drive paths in Send-SftpFile and Receive-SftpFile' {
        $file = Join-Path $TestDrive 'drive-upload.txt'
        'uploaded' | Set-Content -LiteralPath $file
        $up = Send-SftpFile -Path $file -Destination sftptest:\places\
        $up.Destination | Should -Be "$remote/places/drive-upload.txt"
        $down = Receive-SftpFile -Path sftptest:\places\drive-*.txt -Destination (Join-Path $TestDrive 'drive-download.txt')
        $down.Source | Should -Be "$remote/places/drive-upload.txt"
        Get-Content (Join-Path $TestDrive 'drive-download.txt') | Should -Be 'uploaded'
    }

    It 'Uses the drive''s connection in every sftp command, whatever the current session is' {
        $other = Connect-SftpServer @connect -Name not-the-drive
        try {
            (Get-SftpSession | Where-Object IsCurrent).Name | Should -Be 'not-the-drive'
            $item = Get-SftpFile sftptest:\hello.txt
            $item.FullName | Should -Be "$remote/hello.txt"
            $item.SessionName | Should -Be 'main'
            Test-SftpPath sftptest:\hello.txt | Should -BeTrue
            Get-SftpContent sftptest:\hello.txt | Should -Be @('line1', 'line2')
            (Set-SftpContent sftptest:\via-drive.txt -Value 'x' -PassThru).SessionName | Should -Be 'main'
            (New-SftpDirectory sftptest:\via-dir).FullName | Should -Be "$remote/via-dir"
            Move-SftpFile -Path sftptest:\via-drive.txt -Destination sftptest:\via-dir\
            Test-Path sftptest:\via-dir\via-drive.txt | Should -BeTrue
            (Get-SftpPermission sftptest:\via-dir\via-drive.txt).SessionName | Should -Be 'main'
            (Get-SftpDiskSpace sftptest:\).Path | Should -Be $remote
            Remove-SftpFile sftptest:\via-dir -Recurse -Confirm:$false
            Test-Path sftptest:\via-dir | Should -BeFalse
            (Get-SftpSession).Name | Should -Contain 'not-the-drive'
        }
        finally {
            Disconnect-SftpServer -Session $other
        }
    }

    It 'Resolves relative paths against the current location on a drive' {
        Push-Location sftptest:\places
        try {
            (Get-SftpFile here.txt).FullName | Should -Be "$remote/places/here.txt"
            Get-SftpContent here.txt | Should -Be 'relative'
            (Get-SftpFile ../hello.txt).FullName | Should -Be "$remote/hello.txt"
            (Get-SftpPermission /upload).FullName | Should -Be '/upload'
        }
        finally {
            Pop-Location
        }
    }

    It 'Uploads into the current drive folder when -Destination is left out, only on a drive of the same session' {
        $file = Join-Path $TestDrive 'into-location.txt'
        'here' | Set-Content -LiteralPath $file
        $other = Connect-SftpServer @connect -Name upload-elsewhere
        try {
            Push-Location sftptest:\places
            try {
                (Send-SftpFile -LiteralPath $file -Force).Destination | Should -Be "$remote/places/into-location.txt"
                (Send-SftpFile -Session upload-elsewhere -LiteralPath $file -Force).Destination | Should -Be "$($other.HomeDirectory.TrimEnd('/'))/into-location.txt"
            }
            finally {
                Pop-Location
            }
            (Send-SftpFile -LiteralPath $file -Force).Destination | Should -Be "$($other.HomeDirectory.TrimEnd('/'))/into-location.txt"
        }
        finally {
            Remove-SftpFile -Session main -Path "$($session.HomeDirectory.TrimEnd('/'))/into-location.txt" -Confirm:$false -ErrorAction SilentlyContinue
            Disconnect-SftpServer -Session $other
        }
    }

    It 'Refuses a drive path together with a different -Session' {
        $other = Connect-SftpServer @connect -Name wrong-session
        try {
            $result = Get-SftpFile -Session wrong-session -Path sftptest:\hello.txt -ErrorAction SilentlyContinue -ErrorVariable driveError
            $result | Should -BeNullOrEmpty
            $driveError[0].FullyQualifiedErrorId | Should -Be 'DriveOnOtherSession,sftp.Commands.GetSftpFileCommand'
        }
        finally {
            Disconnect-SftpServer -Session $other
        }
    }

    It 'Gives items the properties of local files' {
        $file = Get-SftpFile sftptest:\hello.txt
        $file | Should -BeOfType ([sftp.SftpFileInfo])
        $file | Should -BeOfType ([sftp.SftpItem])
        $file.Name | Should -Be 'hello.txt'
        $file.BaseName | Should -Be 'hello'
        $file.Extension | Should -Be '.txt'
        $file.DirectoryName | Should -Be $remote
        $file.GetDirectory().FullName | Should -Be $remote
        $file.Exists | Should -BeTrue
        $file.Length | Should -Be 12
        $file.LastWriteTime | Should -BeOfType ([datetime])
        $file.LastWriteTimeUtc.Kind | Should -Be 'Utc'
        $file.LastAccessTime | Should -BeOfType ([datetime])
        $file.CreationTime | Should -Be $file.LastWriteTime
        $file.Attributes | Should -Be ([IO.FileAttributes]::Normal)
        $file.IsReadOnly | Should -BeFalse
        $file.Mode | Should -Be '------'
        $file.UnixMode | Should -Match '^-[rwxsStT-]{9}$'
        $file.UnixFileMode.HasFlag([IO.UnixFileMode]::UserRead) | Should -BeTrue
        "$file" | Should -Be "$remote/hello.txt"

        $folder = Get-Item sftptest:\places
        $folder | Should -BeOfType ([sftp.SftpDirectoryInfo])
        $folder.PSObject.Properties.Name | Should -Not -Contain 'Length'
        $folder.Parent | Should -Be $remote
        $folder.Root | Should -Be '/'
        $folder.BaseName | Should -Be 'places'
        $folder.GetParent().FullName | Should -Be $remote
        $folder.Attributes.HasFlag([IO.FileAttributes]::Directory) | Should -BeTrue
        $folder.Mode | Should -BeLike 'd*'
        $folder.UnixMode | Should -BeLike 'd*'
        $folder.Extension | Should -Be ''

        $top = Get-Item sftptest:\
        $top.GetFiles('*.txt').Name | Should -Contain 'hello.txt'
        $top.GetFiles() | Should -BeOfType ([sftp.SftpFileInfo])
        $top.GetDirectories().Name | Should -Contain 'places'
        $top.GetDirectories() | Should -BeOfType ([sftp.SftpDirectoryInfo])
        @($top.GetFileSystemInfos()).Count | Should -Be (@($top.GetFiles()).Count + @($top.GetDirectories()).Count)

        $null = New-Item sftptest:\.hidden -Value 'h'
        (Get-Item sftptest:\.hidden).Attributes | Should -Be ([IO.FileAttributes]::Hidden)
        Set-SftpPermission -Session $session -Path "$remote/.hidden" -Mode 0444
        (Get-SftpFile -Session $session -Path "$remote/.hidden").IsReadOnly | Should -BeTrue
    }

    It 'Shows used and free space in Get-PSDrive' -Skip:$external {
        $listed = Get-PSDrive -Name sftptest
        $listed.Used | Should -Be (750 * 4096)
        $listed.Free | Should -Be (200 * 4096)
        (Get-PSDrive -PSProvider Sftp | Format-Table | Out-String) | Should -Match 'sftptest'
    }

    It 'Notices changes made outside the drive once the cache expires' {
        Test-Path sftptest:\outside.txt | Should -BeFalse
        Set-SftpContent -Session $session -Path "$remote/outside.txt" -Value 'later'
        Start-Sleep -Milliseconds 2100
        Test-Path sftptest:\outside.txt | Should -BeTrue
    }

    It 'Explains that copying to another provider needs Send-SftpFile or Receive-SftpFile' {
        { Copy-Item sftptest:\hello.txt (Join-Path $TestDrive 'copy.txt') -ErrorAction Stop } | Should -Throw
    }

    It 'Opens its own session from -HostName and closes it with Remove-PSDrive' {
        $own = New-PSDrive -Name sftpown -PSProvider Sftp -Root / -HostName $testServer.HostName -Port $testServer.Port -Credential $credential -HostKeyFingerprint $hostKey.Fingerprint
        try {
            $own.OwnsSession | Should -BeTrue
            $own.Session.IsConnected | Should -BeTrue
            $own.Session.IsCurrent | Should -BeFalse
            (Get-SftpSession | Where-Object IsCurrent).Name | Should -Be 'main'
            Test-Path "sftpown:$remote/hello.txt" | Should -BeTrue
        }
        finally {
            Remove-PSDrive -Name sftpown
        }
        $own.Session.IsConnected | Should -BeFalse
    }

    It 'Requires a session or a host name' {
        { New-PSDrive -Name sftpnone -PSProvider Sftp -Root / -ErrorAction Stop } | Should -Throw '*-Session*-HostName*'
    }
}
