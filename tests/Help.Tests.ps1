BeforeDiscovery {
    $commands = (Import-PowerShellDataFile (Join-Path $PSScriptRoot '../module/sftp.psd1')).CmdletsToExport |
        ForEach-Object { @{ CommandName = $_ } }
}

BeforeAll {
    Import-Module (Join-Path $PSScriptRoot '../output/sftp.psd1') -Force
    Import-Module Microsoft.PowerShell.PlatyPS -ErrorAction Stop
    $commonParameters = @([System.Management.Automation.PSCmdlet]::CommonParameters) +
        @([System.Management.Automation.PSCmdlet]::OptionalCommonParameters)
}

Describe 'Cmdlet help' {
    It 'Packages a single help file at the module root' {
        $output = Join-Path $PSScriptRoot '../output'
        Join-Path $output 'en-US/sftp.dll-Help.xml' | Should -Exist
        Join-Path $output 'lib/en-US' | Should -Not -Exist
        @(Get-ChildItem $output -Filter '*.xml' -File -Recurse).Count | Should -Be 1
        @(Get-ChildItem (Join-Path $PSScriptRoot '../docs/en-US') -Filter '*.md').Count | Should -Be 23
        $xml = [xml](Get-Content (Join-Path $output 'en-US/sftp.dll-Help.xml') -Raw)
        $documentedNames = @($xml.SelectNodes("//*[local-name()='parameters']/*[local-name()='parameter']/*[local-name()='name']") |
            ForEach-Object InnerText)
        @($documentedNames | Where-Object { $_ -in $commonParameters }).Count | Should -Be 0
    }

    It 'Documents every parameter and three parseable examples for <CommandName>' -ForEach $commands {
        $command = Get-Command "sftp\$CommandName"
        $help = Get-Help "sftp\$CommandName" -Full
        $help.Description.Text | Should -Not -BeNullOrEmpty
        $help.Synopsis | Should -Not -Match ('^\s*' + [regex]::Escape($CommandName) + '\s*\[')
        @($help.Examples.Example).Count | Should -BeGreaterOrEqual 3
        foreach ($example in $help.Examples.Example) {
            $text = @($example.Code; $example.Introduction.Text; $example.Remarks.Text) -join "`n"
            $blocks = @([regex]::Matches($text, '(?ms)^```powershell\s*\r?\n(.*?)^```') | ForEach-Object { $_.Groups[1].Value })
            if ($blocks.Count -eq 0 -and -not [string]::IsNullOrWhiteSpace($example.Code)) { $blocks = @($example.Code) }
            $blocks.Count | Should -BeGreaterThan 0 -Because "$CommandName $($example.Title) needs runnable code"
            foreach ($code in $blocks) {
                $tokens = $null; $parseErrors = $null
                $null = [Management.Automation.Language.Parser]::ParseInput($code, [ref]$tokens, [ref]$parseErrors)
                $parseErrors | Should -BeNullOrEmpty -Because "$CommandName $($example.Title) must parse"
            }
        }
        foreach ($parameter in $command.Parameters.Keys | Where-Object { $_ -notin $commonParameters }) {
            $description = (Get-Help "sftp\$CommandName" -Parameter $parameter).Description.Text -join ' '
            $description | Should -Not -BeNullOrEmpty -Because "$CommandName -$parameter needs help"
            $description | Should -Not -Match '\{\{'
        }
    }

    It 'Uses current sftp command parameters in every documented PowerShell example' {
        $paths = @((Join-Path $PSScriptRoot '../README.md')) + @(Get-ChildItem (Join-Path $PSScriptRoot '../docs/en-US') -Filter '*.md' -File | ForEach-Object FullName)
        foreach ($path in $paths) {
            $text = Get-Content -LiteralPath $path -Raw
            foreach ($match in [regex]::Matches($text, '(?ms)^```powershell\s*\r?\n(.*?)^```')) {
                $tokens = $null; $parseErrors = $null
                $ast = [Management.Automation.Language.Parser]::ParseInput($match.Groups[1].Value, [ref]$tokens, [ref]$parseErrors)
                $parseErrors | Should -BeNullOrEmpty -Because "$path examples must parse"
                foreach ($call in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] }, $true)) {
                    $name = $call.GetCommandName()
                    if (-not $name) { continue }
                    $command = Get-Command -Name $name -Module sftp -ErrorAction SilentlyContinue
                    if (-not $command) { continue }
                    foreach ($parameter in $call.CommandElements | Where-Object { $_ -is [Management.Automation.Language.CommandParameterAst] }) {
                        $candidates = @($command.Parameters.Values | Where-Object {
                            $_.Name -like ($parameter.ParameterName + '*') -or $_.Aliases -contains $parameter.ParameterName
                        })
                        $candidates.Count | Should -BeGreaterThan 0 -Because "$path uses $name -$($parameter.ParameterName), which must exist"
                    }
                }
            }
        }
    }

    It 'Keeps source help aligned with <CommandName>' -ForEach $commands {
        $path = Join-Path $PSScriptRoot "../docs/en-US/$CommandName.md"
        $null = Test-MarkdownCommandHelp -LiteralPath $path -ErrorAction Stop
        Get-Content -LiteralPath $path -Raw | Should -Not -Match '\{\{'
        $model = Import-MarkdownCommandHelp -LiteralPath $path -ErrorAction Stop
        $model.Diagnostics.HadErrors | Should -BeFalse
        $model.Title | Should -Be $CommandName
        $model.Locale.Name | Should -Be 'en-US'
        $model.ExternalHelpFile | Should -Be 'sftp.dll-Help.xml'
        @($model.Parameters.Name | Where-Object { $_ -in $commonParameters }).Count |
            Should -Be 0 -Because 'PowerShell built-ins must not have individually authored parameter sections'
        $command = Get-Command "sftp\$CommandName"
        $actual = @($command.Parameters.Keys | Where-Object { $_ -notin $commonParameters })
        $documented = @($model.Parameters.Name | Where-Object { $_ -notin $commonParameters })
        Compare-Object -ReferenceObject $actual -DifferenceObject $documented | Should -BeNullOrEmpty
    }
}
