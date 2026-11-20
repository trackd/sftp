# Shared setup for the integration test files. Dot-source it in BeforeAll, and with -Cleanup in AfterAll:
#   BeforeAll { . (Join-Path $PSScriptRoot 'TestContext.ps1') }
#   AfterAll { . (Join-Path $PSScriptRoot 'TestContext.ps1') -Cleanup }
# It starts the bundled test server (tests/sftp.TestServer), or uses another server when SFTP_TEST_HOST,
# SFTP_TEST_PORT, SFTP_TEST_USER, SFTP_TEST_PASSWORD and SFTP_TEST_KEYFILE are set, and defines:
#   $testServer  connection details (and the server process)
#   $server      @{ HostName; Port } for splatting
#   $hostKey     the server's host key
#   $credential  the test user's credential
#   $connect     every Connect-SftpServer parameter needed, for splatting
#   $session     an open session named 'main'
#   $remote      an empty folder on the server for this test file, removed again by -Cleanup
param([switch]$Cleanup)

if ($Cleanup) {
    Get-PSDrive -PSProvider Sftp -ErrorAction SilentlyContinue | Remove-PSDrive -ErrorAction SilentlyContinue
    if ($session -and $session.IsConnected) {
        Remove-SftpFile -Session $session -Path $remote -Recurse -Confirm:$false -ErrorAction SilentlyContinue
    }
    Get-SftpSession | Disconnect-SftpServer -ErrorAction SilentlyContinue
    if ($testServer.Process) {
        & (Join-Path $PSScriptRoot 'Start-TestServer.ps1') -Stop -Server $testServer
    }
    return
}

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
$credential = [pscredential]::new($testServer.UserName, (ConvertTo-SecureString $testServer.Password -AsPlainText -Force))
$connect = @{ HostName = $testServer.HostName; Port = $testServer.Port; Credential = $credential; HostKeyFingerprint = $hostKey.Fingerprint }
$session = Connect-SftpServer @connect -Name main
$remote = '/upload/test-' + [guid]::NewGuid().ToString('n')
$null = New-SftpDirectory -Session $session -Path $remote
