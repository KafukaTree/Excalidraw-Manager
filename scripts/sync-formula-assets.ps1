param(
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path))).TrimEnd('\')
$nodeModules = Join-Path $root 'node_modules'
$runtimeRoot = [IO.Path]::GetFullPath((Join-Path $root 'runtime'))
$destination = [IO.Path]::GetFullPath((Join-Path $runtimeRoot 'formula-editor\vendor'))

if (-not $destination.StartsWith($runtimeRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to replace a vendor directory outside runtime: $destination"
}

function Read-PackageVersion([string]$packageDirectory) {
    $manifest = Join-Path $packageDirectory 'package.json'
    if (-not (Test-Path -LiteralPath $manifest)) {
        throw "Required package is not installed: $packageDirectory"
    }
    return (Get-Content -Raw -LiteralPath $manifest | ConvertFrom-Json).version
}

$mathLiveSource = Join-Path $nodeModules 'mathlive'
$mathJaxSource = Join-Path $nodeModules 'mathjax'
$mathJaxFontSource = Join-Path $nodeModules '@mathjax\mathjax-newcm-font'

$mathLiveVersion = Read-PackageVersion $mathLiveSource
$mathJaxVersion = Read-PackageVersion $mathJaxSource
$mathJaxFontVersion = Read-PackageVersion $mathJaxFontSource

if ($mathLiveVersion -ne '0.110.0') { throw "Expected MathLive 0.110.0, found $mathLiveVersion" }
if ($mathJaxVersion -ne '4.1.3') { throw "Expected MathJax 4.1.3, found $mathJaxVersion" }
if ($mathJaxFontVersion -ne '4.1.3') { throw "Expected MathJax New Computer Modern font 4.1.3, found $mathJaxFontVersion" }

if (Test-Path -LiteralPath $destination) {
    if (-not $Force) {
        Write-Host "Refreshing formula assets in $destination"
    }
    Remove-Item -Recurse -Force -LiteralPath $destination
}

$mathLiveDestination = Join-Path $destination 'mathlive'
$mathJaxDestination = Join-Path $destination 'mathjax'
New-Item -ItemType Directory -Force -Path $mathLiveDestination, $mathJaxDestination | Out-Null

Copy-Item -Force (Join-Path $mathLiveSource 'mathlive.min.mjs') $mathLiveDestination
Copy-Item -Force (Join-Path $mathLiveSource 'mathlive-fonts.css') $mathLiveDestination
Copy-Item -Force (Join-Path $mathLiveSource 'LICENSE.txt') $mathLiveDestination
Copy-Item -Force (Join-Path $mathLiveSource 'package.json') $mathLiveDestination
Copy-Item -Recurse -Force (Join-Path $mathLiveSource 'fonts') $mathLiveDestination

# This pre-built MathJax component already contains the TeX, MathML and SVG
# processors. The accompanying svg directory contains every lazily-loaded
# New Computer Modern glyph range, so formula export remains fully offline.
Copy-Item -Force (Join-Path $mathJaxFontSource 'tex-mml-svg-mathjax-newcm.js') (Join-Path $mathJaxDestination 'tex-mml-svg.js')
Copy-Item -Recurse -Force (Join-Path $mathJaxFontSource 'svg') $mathJaxDestination
# The combined browser component initializes MathJax's accessibility worker.
# Keep its speech engine and language maps local so startup never waits on a
# CDN, even when assistive output is not actively requested.
Copy-Item -Recurse -Force (Join-Path $mathJaxSource 'sre') $mathJaxDestination
Copy-Item -Force (Join-Path $mathJaxSource 'LICENSE') (Join-Path $mathJaxDestination 'LICENSE.txt')
Copy-Item -Force (Join-Path $mathJaxFontSource 'package.json') (Join-Path $mathJaxDestination 'font-package.json')

$metadata = [ordered]@{
    generatedBy = 'scripts/sync-formula-assets.ps1'
    mathlive = $mathLiveVersion
    mathjax = $mathJaxVersion
    mathjaxFont = $mathJaxFontVersion
}
$metadata | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $destination 'versions.json')

$files = Get-ChildItem -Recurse -File -LiteralPath $destination
$size = ($files | Measure-Object -Property Length -Sum).Sum
Write-Host ("Formula assets ready: {0} files, {1:N1} MiB" -f $files.Count, ($size / 1MB))
