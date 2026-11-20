@{
    RootModule           = 'sftp.psm1'
    ModuleVersion        = '0.3.0'
    GUID                 = 'a20b4f35-de47-4ca8-a229-928f6ec73c12'
    Author               = 'trackd'
    Copyright            = 'Copyright (c) 2026 trackd. Licensed under MIT.'
    Description          = 'Upload, download and manage files on SFTP servers. Built on SSH.NET, with SCP support.'
    PowerShellVersion    = '7.6'
    CompatiblePSEditions = @('Core')
    FunctionsToExport    = @()
    FormatsToProcess     = @('sftp.format.ps1xml')
    TypesToProcess       = @('sftp.types.ps1xml')
    CmdletsToExport      = @(
        'Connect-SftpServer'
        'Disconnect-SftpServer'
        'Get-SftpSession'
        'Get-SshHostKey'
        'Get-SftpFile'
        'Send-SftpFile'
        'Receive-SftpFile'
        'Move-SftpFile'
        'Remove-SftpFile'
        'New-SftpDirectory'
        'Send-ScpFile'
        'Receive-ScpFile'
        'Test-SftpPath'
        'Get-SftpContent'
        'Set-SftpContent'
        'Invoke-SshCommand'
        'New-SshTunnel'
        'Get-SshTunnel'
        'Remove-SshTunnel'
        'Get-SftpPermission'
        'Set-SftpPermission'
        'Get-SftpDiskSpace'
        'Sync-SftpFolder'
    )
    AliasesToExport      = @()
    VariablesToExport    = @()
    PrivateData          = @{
        PSData = @{
            Tags       = @(
                'SFTP'
                'SCP'
                'SSH'
                'FileTransfer'
                'CrossPlatform'
            )
            LicenseUri = 'https://github.com/trackd/sftp/blob/main/LICENSE'
            ProjectUri = 'https://github.com/trackd/sftp'
        }
    }
}
