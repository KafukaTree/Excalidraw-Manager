param(
    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\ExcalidrawManager'),
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if ($SkipBuild) {
    & (Join-Path $root 'check-environment.ps1') -RuntimeOnly
    if ($LASTEXITCODE -ne 0) { throw 'Environment check failed. See the messages above.' }
} else {
    & (Join-Path $root 'build.ps1')
}

$payload = @(
    (Join-Path $root 'dist\ExcalidrawManager.exe'),
    (Join-Path $root 'dist\ExcalidrawManager.Cli.exe'),
    (Join-Path $root 'dist\runtime\main.js'),
    (Join-Path $root 'dist\runtime\server.mjs')
)
foreach ($file in $payload) {
    if (-not (Test-Path -LiteralPath $file)) { throw "Installation payload is missing: $file" }
}

$installDir = [IO.Path]::GetFullPath($InstallDir).TrimEnd('\')
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
Copy-Item -Force (Join-Path $root 'dist\ExcalidrawManager.exe') (Join-Path $installDir 'ExcalidrawManager.exe')
Copy-Item -Force (Join-Path $root 'dist\ExcalidrawManager.Cli.exe') (Join-Path $installDir 'ExcalidrawManager.Cli.exe')
$runtimeInstall = Join-Path $installDir 'runtime'
New-Item -ItemType Directory -Force -Path $runtimeInstall | Out-Null
Copy-Item -Recurse -Force (Join-Path $root 'dist\runtime\*') $runtimeInstall

$oldDefaultDir = (Join-Path $env:LOCALAPPDATA 'Programs\ExcalidrawManager').TrimEnd('\')
$userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
$pathEntries = @($userPath -split ';' | Where-Object {
    $_ -and
    -not [string]::Equals($_.TrimEnd('\'), $oldDefaultDir, [StringComparison]::OrdinalIgnoreCase) -and
    -not [string]::Equals($_.TrimEnd('\'), $installDir, [StringComparison]::OrdinalIgnoreCase)
})
$pathEntries += $installDir
[Environment]::SetEnvironmentVariable('Path', ($pathEntries -join ';'), 'User')

$cmd = Join-Path $installDir 'excalidraw-manager.cmd'
$cmdLines = @(
    '@echo off',
    'if /I "%~1"=="list" goto cli',
    'if /I "%~1"=="stop-all" goto cli',
    'if /I "%~1"=="help" goto cli',
    'if /I "%~1"=="--help" goto cli',
    'if /I "%~1"=="-h" goto cli',
    'if /I "%~1"=="--version" goto cli',
    'if /I "%~1"=="-V" goto cli',
    'start "" "%~dp0ExcalidrawManager.exe" %*',
    'exit /b 0',
    ':cli',
    '"%~dp0ExcalidrawManager.Cli.exe" %*'
)
Set-Content -LiteralPath $cmd -Encoding ASCII -Value $cmdLines
Write-Host "Installed to $installDir"
Write-Host 'Open a new terminal, then run: excalidraw-manager'
