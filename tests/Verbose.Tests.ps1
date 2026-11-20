# Verbose output, including SSH.NET log messages.
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
Describe 'Verbose output' -Tag Integration {
    It 'Shows connection steps and SSH.NET messages with -Verbose' {
        $messages = Connect-SftpServer @connect -Name verbose-test -Verbose 4>&1 |
            Where-Object { $_ -is [System.Management.Automation.VerboseRecord] } | ForEach-Object Message
        $messages | Should -Contain "Connecting to $($testServer.HostName):$($testServer.Port) as $($testServer.UserName)"
        @($messages | Where-Object { $_ -like 'SSH.NET *' }).Count | Should -BeGreaterThan 0
        Disconnect-SftpServer -Session verbose-test
    }

    It 'Shows transfer steps with -Verbose and nothing without it' {
        $file = Join-Path $TestDrive 'verbose.txt'
        'v' | Set-Content -LiteralPath $file
        $verbose = Send-SftpFile -Session $session -Path $file -Destination "$remote/" -Force -Verbose 4>&1 |
            Where-Object { $_ -is [System.Management.Automation.VerboseRecord] } | ForEach-Object Message
        @($verbose | Where-Object { $_ -like "Uploaded $remote/verbose.txt in *" }).Count | Should -Be 1
        @($verbose | Where-Object { $_ -like 'Uploading *' }).Count | Should -Be 1
        $quiet = Send-SftpFile -Session $session -Path $file -Destination "$remote/" -Force 4>&1 |
            Where-Object { $_ -is [System.Management.Automation.VerboseRecord] }
        $quiet | Should -BeNullOrEmpty
    }
}
