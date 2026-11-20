<#
.SYNOPSIS
Start the bundled SSH/SFTP test server for the integration tests.
.DESCRIPTION
Builds tests/sftp.TestServer (a small, test-only server with no dependencies), generates ECDSA
client keys with ssh-keygen and starts the server on a free loopback port. Returns the connection
details and the server process. Stop the server with -Stop, or by disposing the returned process.
No Docker or system SSH service is needed.
.PARAMETER Root
Directory the server exposes as "/". A new temporary directory by default.
.PARAMETER NoPosixRename
Start the server without the posix-rename@openssh.com extension, to test the rename fallback.
.PARAMETER NoStatVfs
Start the server without the statvfs@openssh.com extension, to test servers that cannot report disk space.
.PARAMETER Stop
Stop a server previously returned by this script.
.PARAMETER Server
The object this script returned, for -Stop.
.EXAMPLE
$server = ./tests/Start-TestServer.ps1
.EXAMPLE
./tests/Start-TestServer.ps1 -Stop -Server $server
.OUTPUTS
PSCustomObject with HostName, Port, Fingerprint, UserName, Password, KeyFile, EncryptedKeyFile,
KeyPassphrase, Root and Process.
#>
[CmdletBinding(DefaultParameterSetName = 'Start')]
param(
    [Parameter(ParameterSetName = 'Start')]
    [string]$Root,
    [Parameter(ParameterSetName = 'Start')]
    [switch]$NoPosixRename,
    [Parameter(ParameterSetName = 'Start')]
    [switch]$NoStatVfs,
    [Parameter(Mandatory, ParameterSetName = 'Stop')]
    [switch]$Stop,
    [Parameter(Mandatory, ParameterSetName = 'Stop')]
    [psobject]$Server
)
$ErrorActionPreference = 'Stop'
if ($Stop) {
    if (-not $Server.Process.HasExited) {
        $Server.Process.StandardInput.Close()
        if (-not $Server.Process.WaitForExit(5000)) { $Server.Process.Kill() }
    }
    $Server.Process.Dispose()
    return
}

$projectRoot = Split-Path -Parent $PSScriptRoot
$serverOutput = Join-Path $projectRoot 'artifacts' 'testserver'
$serverDll = Join-Path $serverOutput 'sftp.TestServer.dll'
$serverSources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'sftp.TestServer') -File -Recurse | Where-Object FullName -NotMatch '[\\/](bin|obj)[\\/]'
if (-not (Test-Path -LiteralPath $serverDll) -or ($serverSources | Where-Object LastWriteTimeUtc -GT (Get-Item -LiteralPath $serverDll).LastWriteTimeUtc)) {
    $build = & dotnet build (Join-Path $PSScriptRoot 'sftp.TestServer' 'sftp.TestServer.csproj') --configuration Release --nologo --verbosity quiet --output $serverOutput 2>&1
    if ($LASTEXITCODE -ne 0) { throw "Test server build failed:`n$($build -join "`n")" }
}

$work = Join-Path ([IO.Path]::GetTempPath()) ('sftp-tests-' + [guid]::NewGuid().ToString('n'))
$null = New-Item -ItemType Directory -Path $work
if (-not $Root) { $Root = Join-Path $work 'root' }
$null = New-Item -ItemType Directory -Path (Join-Path $Root 'upload') -Force

$keyFile = Join-Path $work 'id_ecdsa'
$encryptedKeyFile = Join-Path $work 'id_ecdsa_encrypted'
$keyPassphrase = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(12))
foreach ($key in @(@($keyFile, ''), @($encryptedKeyFile, $keyPassphrase))) {
    & ssh-keygen -q -t ecdsa -b 256 -C sftp-module-tests -f $key[0] -N $key[1]
    if ($LASTEXITCODE -ne 0) { throw 'ssh-keygen failed.' }
}
$authorized = Join-Path $work 'authorized_keys'
Get-Content -LiteralPath "$keyFile.pub", "$encryptedKeyFile.pub" | Set-Content -LiteralPath $authorized
$password = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(18))

$start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
foreach ($argument in @($serverDll, '--root', $Root, '--user', 'tester', '--password', $password, '--authorized-key', $authorized)) {
    $start.ArgumentList.Add($argument)
}
if ($NoPosixRename) { $start.ArgumentList.Add('--no-posix-rename') }
if ($NoStatVfs) { $start.ArgumentList.Add('--no-statvfs') }
$start.RedirectStandardInput = $true
$start.RedirectStandardOutput = $true
$start.UseShellExecute = $false
$process = [Diagnostics.Process]::Start($start)
$readLine = $process.StandardOutput.ReadLineAsync()
if (-not $readLine.Wait(30000) -or -not $readLine.Result) {
    $process.Kill()
    throw 'The test server did not start.'
}
$info = $readLine.Result | ConvertFrom-Json

[pscustomobject]@{
    HostName         = '127.0.0.1'
    Port             = [int]$info.port
    Fingerprint      = $info.fingerprint
    UserName         = 'tester'
    Password         = $password
    KeyFile          = $keyFile
    EncryptedKeyFile = $encryptedKeyFile
    KeyPassphrase    = $keyPassphrase
    Root             = $Root
    Process          = $process
}
