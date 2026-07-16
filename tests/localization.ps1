$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe = Join-Path $root 'dist\ExcalidrawManager.exe'
$cli = Join-Path $root 'dist\ExcalidrawManager.Cli.exe'

if (-not (Test-Path -LiteralPath $exe) -or -not (Test-Path -LiteralPath $cli)) {
    & (Join-Path $root 'build.ps1')
}

[void][Reflection.Assembly]::LoadFile($exe)

function Assert-Equal([string]$Expected, [string]$Actual, [string]$Label) {
    if ($Expected -cne $Actual) {
        throw "$Label expected '$Expected' but got '$Actual'"
    }
}

function ConvertFrom-CodePoints([int[]]$CodePoints) {
    return -join ($CodePoints | ForEach-Object { [char]$_ })
}

[ExcalidrawManager.Localization]::Configure('zh-CN')
Assert-Equal 'zh-CN' ([ExcalidrawManager.Localization]::Language) 'Chinese language code'
Assert-Equal (ConvertFrom-CodePoints @(0x8bbe, 0x7f6e)) ([ExcalidrawManager.Localization]::T('Settings')) 'Settings translation'
Assert-Equal (ConvertFrom-CodePoints @(0x6dfb, 0x52a0, 0x5de5, 0x4f5c, 0x533a)) ([ExcalidrawManager.Localization]::T('Add root')) 'Toolbar translation'
Assert-Equal (ConvertFrom-CodePoints @(0x7d20, 0x6750, 0x5e93)) ([ExcalidrawManager.Localization]::T('Libraries')) 'Library translation'
$startingChinese = (ConvertFrom-CodePoints @(0x6b63, 0x5728, 0x542f, 0x52a8)) + ' sample.excalidraw...'
Assert-Equal $startingChinese ([ExcalidrawManager.Localization]::F('Starting {0}...', @('sample.excalidraw'))) 'Formatted status translation'
$portChinese = (ConvertFrom-CodePoints @(0x7aef, 0x53e3)) + ' 6417 ' + (ConvertFrom-CodePoints @(0x5df2, 0x88ab, 0x5360, 0x7528, 0x3002))
Assert-Equal $portChinese ([ExcalidrawManager.Localization]::F('Port {0} is already in use.', @(6417))) 'Formatted error translation'
Assert-Equal (ConvertFrom-CodePoints @(0x7b80, 0x4f53, 0x4e2d, 0x6587)) ([ExcalidrawManager.Localization]::T('Simplified Chinese')) 'Language option translation'

[ExcalidrawManager.Localization]::Configure('en')
Assert-Equal 'en' ([ExcalidrawManager.Localization]::Language) 'English language code'
Assert-Equal 'Settings' ([ExcalidrawManager.Localization]::T('Settings')) 'English fallback'
Assert-Equal 'Starting sample.excalidraw...' ([ExcalidrawManager.Localization]::F('Starting {0}...', @('sample.excalidraw'))) 'English formatted status'

Assert-Equal 'zh-CN' ([ExcalidrawManager.Localization]::NormalizePreference('zh')) 'Chinese preference normalization'
Assert-Equal 'en' ([ExcalidrawManager.Localization]::NormalizePreference('en-US')) 'English preference normalization'
Assert-Equal 'system' ([ExcalidrawManager.Localization]::NormalizePreference('unsupported')) 'Unsupported preference fallback'

$settings = New-Object ExcalidrawManager.AppSettings
Assert-Equal 'system' $settings.Language 'Default language preference'

$guiVersion = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
$cliVersion = (& $cli --version).Trim()
Assert-Equal '0.3.0.0' $guiVersion 'GUI file version'
Assert-Equal '0.3.0' $cliVersion 'CLI version'

Write-Host 'PASS Chinese and English localization, language preferences, and version metadata'
