$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$source = Join-Path $root 'src\Program.cs'
$localizationSource = Join-Path $root 'src\Localization.cs'
$output = Join-Path $root 'dist\ExcalidrawManager.exe'
$cliOutput = Join-Path $root 'dist\ExcalidrawManager.Cli.exe'
$icon = Join-Path $root 'assets\app-icon.ico'
$runtimeOutput = Join-Path $root 'dist\runtime'
$formulaServerSource = Join-Path $root 'runtime\formula-server.mjs'
$formulaProviderSource = Join-Path $root 'runtime\formula-provider-registry.mjs'
$formulaEditorSource = Join-Path $root 'runtime\formula-editor'
$formulaAssetSync = Join-Path $root 'scripts\sync-formula-assets.ps1'
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

& (Join-Path $root 'check-environment.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Environment check failed. See the messages above.' }

# Refresh the checked-in browser assets when the pinned packages are available.
# This script only copies local files; it never installs or downloads packages.
$formulaPackageManifests = @(
    (Join-Path $root 'node_modules\mathlive\package.json'),
    (Join-Path $root 'node_modules\mathjax\package.json'),
    (Join-Path $root 'node_modules\@mathjax\mathjax-newcm-font\package.json')
)
if (($formulaPackageManifests | Where-Object { -not (Test-Path -LiteralPath $_) }).Count -eq 0) {
    & $formulaAssetSync
    if ($LASTEXITCODE -ne 0) { throw "Formula asset synchronization failed with exit code $LASTEXITCODE" }
} else {
    Write-Host 'Pinned formula packages are not installed; validating the bundled offline assets.'
}

$formulaVersionsPath = Join-Path $formulaEditorSource 'vendor\versions.json'
$formulaSources = @(
    $formulaServerSource,
    $formulaProviderSource,
    (Join-Path $formulaEditorSource 'index.html'),
    (Join-Path $formulaEditorSource 'styles.css'),
    (Join-Path $formulaEditorSource 'app.mjs'),
    (Join-Path $formulaEditorSource 'templates.mjs'),
    $formulaVersionsPath,
    (Join-Path $formulaEditorSource 'vendor\mathlive\mathlive.min.mjs'),
    (Join-Path $formulaEditorSource 'vendor\mathlive\mathlive-fonts.css'),
    (Join-Path $formulaEditorSource 'vendor\mathlive\fonts\KaTeX_Main-Regular.woff2'),
    (Join-Path $formulaEditorSource 'vendor\mathjax\tex-mml-svg.js'),
    (Join-Path $formulaEditorSource 'vendor\mathjax\svg\dynamic\math.js')
)
foreach ($file in $formulaSources) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Formula editor source is missing: $file"
    }
}
$formulaVersions = Get-Content -Raw -LiteralPath $formulaVersionsPath | ConvertFrom-Json
if ($formulaVersions.mathlive -ne '0.110.0' -or
    $formulaVersions.mathjax -ne '4.1.3' -or
    $formulaVersions.mathjaxFont -ne '4.1.3') {
    throw 'Bundled formula assets do not match MathLive 0.110.0 and MathJax 4.1.3.'
}
& node --check $formulaServerSource
if ($LASTEXITCODE -ne 0) { throw 'Formula editor server has invalid JavaScript syntax' }
& node --check (Join-Path $formulaEditorSource 'app.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Formula editor application has invalid JavaScript syntax' }
& node --check (Join-Path $formulaEditorSource 'templates.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Formula editor templates have invalid JavaScript syntax' }

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
Copy-Item -Force $formulaServerSource (Join-Path $runtimeOutput 'formula-server.mjs')
Copy-Item -Force $formulaProviderSource (Join-Path $runtimeOutput 'formula-provider-registry.mjs')
$formulaEditorOutput = Join-Path $runtimeOutput 'formula-editor'
if (Test-Path -LiteralPath $formulaEditorOutput) {
    Remove-Item -Recurse -Force -LiteralPath $formulaEditorOutput
}
Copy-Item -Recurse -Force $formulaEditorSource $formulaEditorOutput

& $csc /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:$icon /out:$output `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Management.dll `
    /reference:System.Web.Extensions.dll `
    /reference:System.Windows.Forms.dll `
    $localizationSource `
    $source

if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
& $csc /nologo /target:exe /optimize+ /codepage:65001 /out:$cliOutput `
    /reference:System.dll `
    /reference:System.Management.dll `
    $localizationSource `
    (Join-Path $root 'src\Cli.cs')
if ($LASTEXITCODE -ne 0) { throw "CLI build failed with exit code $LASTEXITCODE" }
Write-Host "Built $output"
Write-Host "Built $cliOutput"
Write-Host "Built managed Excalidraw runtime in $runtimeOutput"
Write-Host "Bundled offline formula editor in $formulaEditorOutput"
