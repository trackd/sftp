# SSH tunnels (local port forwarding).
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
Describe 'SSH tunnels' -Tag Integration -Skip:$external {
    BeforeAll {
        $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
        $listener.Start()
        $targetPort = $listener.LocalEndpoint.Port
    }

    AfterAll {
        $listener.Stop()
    }

    It 'Forwards a local port to a host reachable from the server' {
        $tunnel = New-SshTunnel -Session $session -RemoteHost 127.0.0.1 -RemotePort $targetPort
        try {
            $tunnel | Should -BeOfType ([sftp.SshTunnel])
            $tunnel.LocalPort | Should -BeGreaterThan 0
            $tunnel.RemotePort | Should -Be $targetPort
            $tunnel.SessionName | Should -Be 'main'
            $tunnel.IsOpen | Should -BeTrue
            $client = [System.Net.Sockets.TcpClient]::new('127.0.0.1', $tunnel.LocalPort)
            $accept = $listener.AcceptTcpClientAsync()
            $accept.Wait(10000) | Should -BeTrue
            $far = $accept.Result
            $far.GetStream().Write([byte[]](1, 2, 3), 0, 3)
            $buffer = [byte[]]::new(3)
            $read = 0
            while ($read -lt 3) { $read += $client.GetStream().Read($buffer, $read, 3 - $read) }
            $buffer | Should -Be ([byte[]](1, 2, 3))
            $client.GetStream().Write([byte[]](9), 0, 1)
            $back = [byte[]]::new(1)
            $far.GetStream().Read($back, 0, 1) | Should -Be 1
            $back[0] | Should -Be 9
            $client.Dispose()
            $far.Dispose()
        }
        finally {
            $tunnel | Remove-SshTunnel
        }
    }

    It 'Lists and closes tunnels' {
        $one = New-SshTunnel -Session $session -RemoteHost 127.0.0.1 -RemotePort $targetPort
        $two = New-SshTunnel -Session $session -RemoteHost 127.0.0.1 -RemotePort $targetPort
        @(Get-SshTunnel).Id | Should -Contain $one.Id
        (Get-SshTunnel -Id $two.Id).LocalPort | Should -Be $two.LocalPort
        Remove-SshTunnel -Id $one.Id
        $one.IsOpen | Should -BeFalse
        @(Get-SshTunnel).Id | Should -Not -Contain $one.Id
        Get-SshTunnel -Session main | Remove-SshTunnel
        @(Get-SshTunnel -Session main).Count | Should -Be 0
        { Remove-SshTunnel -Id 999999 -ErrorAction Stop } | Should -Throw -ErrorId 'TunnelNotFound,sftp.Commands.RemoveSshTunnelCommand'
    }

    It 'Closes tunnels when their session disconnects' {
        $other = Connect-SftpServer @connect -Name tunnel-owner
        $tunnel = New-SshTunnel -Session $other -RemoteHost 127.0.0.1 -RemotePort $targetPort
        $tunnel.IsOpen | Should -BeTrue
        Disconnect-SftpServer -Session $other
        $tunnel.IsOpen | Should -BeFalse
        @(Get-SshTunnel).Id | Should -Not -Contain $tunnel.Id
        { [System.Net.Sockets.TcpClient]::new('127.0.0.1', $tunnel.LocalPort).Dispose() } | Should -Throw
    }
}
