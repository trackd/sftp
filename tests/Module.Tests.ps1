BeforeDiscovery {
    $manifest = Import-PowerShellDataFile (Join-Path $PSScriptRoot '../module/sftp.psd1')
    $commandCases = $manifest.CmdletsToExport | ForEach-Object { @{ CommandName = $_ } }
}

BeforeAll {
    $manifestPath = Join-Path $PSScriptRoot '../output/sftp.psd1'
    Import-Module $manifestPath -Force
    # Every shipped assembly is a maintenance and security commitment: adding one must be deliberate.
    $allowedAssemblies = @(
        'sftp.dll'
        'sftp.ALC.dll'
        'Renci.SshNet.dll'
        'BouncyCastle.Cryptography.dll'
        'Microsoft.Extensions.Logging.Abstractions.dll'
        'Microsoft.Extensions.DependencyInjection.Abstractions.dll'
    )
}

Describe 'Module exports and packaging' {
    It 'Exports exactly the documented cmdlets' {
        (Test-ModuleManifest $manifestPath).ExportedCmdlets.Count | Should -Be 12
        @(Get-Command -Module sftp -CommandType Cmdlet).Count | Should -Be 12
    }

    It 'Exports <CommandName> through the private assembly context' -ForEach $commandCases {
        $command = Get-Command "sftp\$CommandName"
        $context = [Runtime.Loader.AssemblyLoadContext]::GetLoadContext($command.ImplementingType.Assembly)
        $context.Name | Should -BeExactly 'sftp'
    }

    It 'Loads SSH.NET and its dependencies only inside the private context' {
        # SSH.NET loads on first use.
        $null = Get-SshHostKey -HostName 127.0.0.1 -Port 9 -ConnectTimeout 1 -ErrorAction SilentlyContinue
        $assembly = (Get-Command sftp\Connect-SftpServer).ImplementingType.Assembly
        $context = [Runtime.Loader.AssemblyLoadContext]::GetLoadContext($assembly)
        @([Runtime.Loader.AssemblyLoadContext]::Default.Assemblies | Where-Object { $_.GetName().Name -eq 'Renci.SshNet' }).Count | Should -Be 0
        ($context.Assemblies | ForEach-Object { $_.GetName().Name }) | Should -Contain 'Renci.SshNet'
    }

    It 'Ships only the allowed assemblies and no debug symbols' {
        $packaged = @(Get-ChildItem (Join-Path $PSScriptRoot '../output/lib') -Recurse -File)
        @($packaged | Where-Object Extension -EQ '.pdb').Count | Should -Be 0
        $assemblies = @($packaged | Where-Object Extension -EQ '.dll' | ForEach-Object Name | Sort-Object)
        $assemblies | Should -Be ($allowedAssemblies | Sort-Object)
        @($packaged | Where-Object Extension -NotIn '.dll').Name | Should -BeNullOrEmpty
    }

    It 'Requires PowerShell 7.6 or newer on Core' {
        $manifest = Test-ModuleManifest $manifestPath
        $manifest.PowerShellVersion | Should -Be ([version]'7.6')
        $manifest.CompatiblePSEditions | Should -Be @('Core')
    }

    It 'Offers the three ways of checking the host key' {
        $parameters = (Get-Command sftp\Connect-SftpServer).Parameters
        foreach ($name in 'HostKeyFingerprint', 'KnownHostsFile', 'SkipHostKeyCheck') {
            $parameters.Keys | Should -Contain $name
        }
        $parameters['SkipHostKeyCheck'].ParameterType | Should -Be ([switch])
    }

    It 'Gives <Name> a wildcard -Path and a pipeline-bound -LiteralPath' -ForEach @(
        @{ Name = 'Get-SftpFile'; Aliases = @('FullName', 'RemotePath') }
        @{ Name = 'Receive-SftpFile'; Aliases = @('FullName', 'RemotePath') }
        @{ Name = 'Move-SftpFile'; Aliases = @('FullName', 'RemotePath') }
        @{ Name = 'Remove-SftpFile'; Aliases = @('FullName', 'RemotePath') }
        @{ Name = 'Send-SftpFile'; Aliases = @('PSPath', 'FullName', 'LocalPath') }
        @{ Name = 'Send-ScpFile'; Aliases = @('PSPath', 'FullName', 'LocalPath') }
    ) {
        $command = Get-Command "sftp\$Name"
        $path = $command.Parameters['Path']
        $literal = $command.Parameters['LiteralPath']
        $path.Attributes.Where({ $_ -is [System.Management.Automation.SupportsWildcardsAttribute] }).Count | Should -Be 1
        $path.ParameterSets.Values.ValueFromPipeline | Should -Contain $true
        $literal.ParameterSets.Values.ValueFromPipelineByPropertyName | Should -Contain $true
        foreach ($alias in $Aliases) { $literal.Aliases | Should -Contain $alias }
    }

    It 'Makes -Session optional and lets it take a session name' {
        foreach ($name in 'Get-SftpFile', 'Send-SftpFile', 'Receive-SftpFile', 'Move-SftpFile', 'Remove-SftpFile', 'New-SftpDirectory', 'Send-ScpFile', 'Receive-ScpFile') {
            $parameter = (Get-Command "sftp\$name").Parameters['Session']
            $parameter.ParameterSets.Values.IsMandatory | Should -Not -Contain $true -Because "$name should default to the current session"
            $parameter.Attributes.Where({ $_ -is [System.Management.Automation.ArgumentTransformationAttribute] }).Count | Should -Be 1
            if ($name -ne 'New-SftpDirectory') {
                (Get-Command "sftp\$name").Parameters['SessionName'].ParameterSets.Values.ValueFromPipelineByPropertyName | Should -Contain $true
            }
        }
    }

    It 'Supports WhatIf on every command that changes files' {
        foreach ($name in 'Send-SftpFile', 'Receive-SftpFile', 'Move-SftpFile', 'Remove-SftpFile', 'New-SftpDirectory', 'Send-ScpFile', 'Receive-ScpFile') {
            (Get-Command "sftp\$name").Parameters.Keys | Should -Contain 'WhatIf' -Because "$name changes files"
        }
    }

    It 'Remains usable after removing and re-importing the module' {
        Remove-Module sftp
        Import-Module $manifestPath -Force
        Get-Command sftp\Connect-SftpServer | Should -Not -BeNullOrEmpty
        (Get-Help Connect-SftpServer -Full).Synopsis | Should -Match 'SFTP server'
    }
}

Describe 'Argument validation without a server' {
    BeforeAll {
        $password = ConvertTo-SecureString 'not-used' -AsPlainText -Force
        $credential = [pscredential]::new('user', $password)
    }

    It 'Requires a host key check before connecting' {
        { Connect-SftpServer -HostName 192.0.2.1 -Credential $credential -ErrorAction Stop } |
            Should -Throw -ErrorId 'HostKeyCheckRequired,sftp.Commands.ConnectSftpServerCommand'
    }

    It 'Rejects a malformed host key fingerprint before connecting' {
        { Connect-SftpServer -HostName 192.0.2.1 -Credential $credential -HostKeyFingerprint 'nope' -ErrorAction Stop } |
            Should -Throw -ErrorId 'InvalidHostKeyFingerprint,sftp.Commands.ConnectSftpServerCommand'
    }

    It 'Requires a password or a key' {
        { Connect-SftpServer -HostName 192.0.2.1 -UserName user -HostKeyFingerprint ('SHA256:' + 'A' * 43) -ErrorAction Stop } |
            Should -Throw -ErrorId 'MissingAuthentication,sftp.Commands.ConnectSftpServerCommand'
    }

    It 'Reports a missing key file' {
        { Connect-SftpServer -HostName 192.0.2.1 -UserName user -KeyFile (Join-Path $TestDrive 'missing') -HostKeyFingerprint ('SHA256:' + 'A' * 43) -ErrorAction Stop } |
            Should -Throw -ErrorId 'MissingAuthentication,sftp.Commands.ConnectSftpServerCommand'
    }

    It 'Reports an unreachable server as a non-terminating error' {
        $result = Get-SshHostKey -HostName 127.0.0.1 -Port 9 -ConnectTimeout 2 -ErrorAction SilentlyContinue -ErrorVariable scanError
        $result | Should -BeNullOrEmpty
        $scanError[0].FullyQualifiedErrorId | Should -Be 'HostKeyScanFailed,sftp.Commands.GetSshHostKeyCommand'
    }
}
