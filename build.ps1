$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'src\Program.cs'
$output = Join-Path $root 'dist\ExcalidrawManager.exe'
$cliOutput = Join-Path $root 'dist\ExcalidrawManager.Cli.exe'
$icon = Join-Path $root 'assets\app-icon.ico'
$runtimeOutput = Join-Path $root 'dist\runtime'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

& (Join-Path $root 'check-environment.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Environment check failed. See the messages above.' }

$excalidrawCommand = (Get-Command excalidraw-edit.cmd -ErrorAction Stop).Source
$globalRoot = Split-Path -Parent $excalidrawCommand
$sourceMain = Join-Path $globalRoot 'node_modules\excalidraw-edit\src\public\assets\main.js'
New-Item -ItemType Directory -Force -Path $runtimeOutput | Out-Null
& node (Join-Path $root 'runtime\patch-client.mjs') $sourceMain (Join-Path $runtimeOutput 'main.js')
if ($LASTEXITCODE -ne 0) { throw "Excalidraw client patch failed with exit code $LASTEXITCODE" }
$patchedMain = Join-Path $runtimeOutput 'main.js'
& node --check $patchedMain
if ($LASTEXITCODE -ne 0) { throw 'Patched Excalidraw client has invalid JavaScript syntax' }
$patchedText = Get-Content -Raw -LiteralPath $patchedMain
if (-not $patchedText.Contains('onLibraryChange:saveManagedLibrary') -or -not $patchedText.Contains('BG().catch(console.error);') -or -not $patchedText.Contains('window.name="ExcalidrawManager"+location.port') -or -not $patchedText.Contains('fetch("/library/import"')) {
    throw 'Patched Excalidraw client is missing the library hook or bootstrap call'
}
Copy-Item -Force (Join-Path $root 'runtime\server.mjs') (Join-Path $runtimeOutput 'server.mjs')

& $csc /nologo /target:winexe /optimize+ /win32icon:$icon /out:$output `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Management.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Windows.Forms.dll `
    $source

if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
& $csc /nologo /target:exe /optimize+ /out:$cliOutput `
    /reference:System.dll `
    /reference:System.Management.dll `
    (Join-Path $root 'src\Cli.cs')
if ($LASTEXITCODE -ne 0) { throw "CLI build failed with exit code $LASTEXITCODE" }
Write-Host "Built $output"
Write-Host "Built $cliOutput"
Write-Host "Built managed Excalidraw runtime in $runtimeOutput"
