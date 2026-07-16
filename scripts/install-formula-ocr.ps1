param(
    [Parameter(Mandatory = $true)]
    [string]$InstallRoot,
    [string]$Python = 'D:\DevTools\Python\Master\Py312\python.exe',
    [switch]$AcceptUpstreamModelLicense,
    [switch]$AllowSystemDrive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if (-not $AcceptUpstreamModelLicense) {
    throw 'The RapidLaTeXOCR weights originate from pix2tex and are marked CC BY-NC-SA. Read the upstream terms, then rerun with -AcceptUpstreamModelLicense.'
}

$root = [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($InstallRoot.Trim()))
$systemRoot = [IO.Path]::GetPathRoot([Environment]::GetFolderPath('Windows'))
$targetRoot = [IO.Path]::GetPathRoot($root)
if (-not $AllowSystemDrive -and [string]::Equals($systemRoot, $targetRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to install on the Windows system drive ($systemRoot). Choose a D-drive folder or explicitly pass -AllowSystemDrive."
}
if (-not (Test-Path -LiteralPath $Python -PathType Leaf)) {
    throw "Python was not found: $Python"
}

foreach ($name in @(
    'PIP_TARGET', 'PIP_PREFIX', 'PIP_INDEX_URL', 'PIP_EXTRA_INDEX_URL',
    'PIP_TRUSTED_HOST', 'PIP_NO_INDEX', 'PIP_FIND_LINKS',
    'PYTHONHOME', 'PYTHONPATH', 'PYTHONUSERBASE', 'VIRTUAL_ENV'
)) {
    Remove-Item -LiteralPath "Env:$name" -ErrorAction SilentlyContinue
}
$env:PIP_CONFIG_FILE = 'NUL'
$env:PIP_REQUIRE_VIRTUALENV = '1'
$basePythonFacts = & $Python -c 'import struct,sys; print(str(sys.version_info.major)+chr(46)+str(sys.version_info.minor)+chr(124)+str(struct.calcsize(chr(80))*8))'
if ($LASTEXITCODE -ne 0 -or $basePythonFacts -notmatch '^(3\.10|3\.11|3\.12)\|64$') {
    throw "Formula OCR requires 64-bit Python 3.10-3.12; found $basePythonFacts"
}

$venv = Join-Path $root 'venv'
$venvPython = Join-Path $venv 'Scripts\python.exe'
$modelRoot = Join-Path $root 'models\rapid-latex-ocr'
$cacheRoot = Join-Path $root 'cache'
$tempRoot = Join-Path $root 'temp'
$runRoot = Join-Path $root 'run'
$installMarker = Join-Path $root 'install-complete.json'
$directories = @(
    $root,
    $modelRoot,
    (Join-Path $cacheRoot 'pip'),
    (Join-Path $cacheRoot 'torch'),
    (Join-Path $cacheRoot 'huggingface'),
    $tempRoot,
    $runRoot
)
foreach ($directory in $directories) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}
if (Test-Path -LiteralPath $installMarker -PathType Leaf) {
    Remove-Item -LiteralPath $installMarker -Force
}

$env:PIP_CACHE_DIR = Join-Path $cacheRoot 'pip'
$env:TORCH_HOME = Join-Path $cacheRoot 'torch'
$env:HF_HOME = Join-Path $cacheRoot 'huggingface'
$env:TEMP = $tempRoot
$env:TMP = $tempRoot
$env:PYTHONNOUSERSITE = '1'
$env:PIP_DISABLE_PIP_VERSION_CHECK = '1'
$env:PIP_NO_INPUT = '1'

if (-not (Test-Path -LiteralPath $venvPython -PathType Leaf)) {
    Write-Host "Creating isolated Python environment in $venv"
    & $Python -m venv $venv
    if ($LASTEXITCODE -ne 0) { throw "Python venv creation failed with exit code $LASTEXITCODE" }
}

$venvFacts = & $venvPython -c 'import struct,sys; print(str(sys.version_info.major)+chr(46)+str(sys.version_info.minor)+chr(124)+str(struct.calcsize(chr(80))*8)+chr(124)+sys.prefix)'
if ($LASTEXITCODE -ne 0 -or $venvFacts -notmatch '^(3\.10|3\.11|3\.12)\|64\|(.+)$') {
    throw "Formula OCR requires a 64-bit Python 3.10-3.12 virtual environment; found $venvFacts"
}
$actualPrefix = [IO.Path]::GetFullPath($Matches[2].Trim())
$expectedPrefix = [IO.Path]::GetFullPath($venv)
if (-not [string]::Equals($actualPrefix.TrimEnd('\'), $expectedPrefix.TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
    throw "The selected virtual environment resolves outside the install root: $actualPrefix"
}

$packages = @(
    'numpy==1.26.4',
    'opencv-python-headless==4.11.0.86',
    'onnxruntime==1.24.4',
    'tokenizers==0.22.2',
    'Pillow==12.3.0',
    'PyYAML==6.0.3',
    'chardet==5.2.0',
    'requests==2.32.5',
    'tqdm==4.67.1'
)

Write-Host 'Installing pinned CPU runtime packages. All pip caches remain under the selected install root.'
& $venvPython -m pip install --index-url 'https://pypi.org/simple' --only-binary=:all: @packages
if ($LASTEXITCODE -ne 0) { throw "Formula OCR dependency installation failed with exit code $LASTEXITCODE" }
& $venvPython -m pip install --index-url 'https://pypi.org/simple' --only-binary=:all: --no-deps 'rapid-latex-ocr==0.0.9'
if ($LASTEXITCODE -ne 0) { throw "rapid-latex-ocr installation failed with exit code $LASTEXITCODE" }

$assets = @(
    [pscustomobject]@{ Name = 'decoder.onnx'; Size = 50952726; Sha256 = 'bd695497bf1b22279b7626f5916c79226e1e244c84355f8da7edfd2d921d0072'; Url = 'https://github.com/RapidAI/RapidLaTeXOCR/releases/download/v0.0.0/decoder.onnx' },
    [pscustomobject]@{ Name = 'encoder.onnx'; Size = 89008136; Sha256 = '01bf5dc25539ca0cd5b1bd29296ea495977a6ba5f629dc4178277809d26e5e7d'; Url = 'https://github.com/RapidAI/RapidLaTeXOCR/releases/download/v0.0.0/encoder.onnx' },
    [pscustomobject]@{ Name = 'image_resizer.onnx'; Size = 38967751; Sha256 = 'e0b075c39700f64d50400f39c8fc186bbb3b5d84d31864008313f376603aca9d'; Url = 'https://github.com/RapidAI/RapidLaTeXOCR/releases/download/v0.0.0/image_resizer.onnx' },
    [pscustomobject]@{ Name = 'tokenizer.json'; Size = 24174; Sha256 = '1dc27b18d6a518d0d5ff3f4bb7bd98521fe80ad39e5b2a246d4109f1bb9d5019'; Url = 'https://github.com/RapidAI/RapidLaTeXOCR/releases/download/v0.0.0/tokenizer.json' }
)

foreach ($asset in $assets) {
    $destination = Join-Path $modelRoot $asset.Name
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        $matchesPinnedHash = (Get-Item -LiteralPath $destination).Length -eq $asset.Size -and
            (Get-FileHash -Algorithm SHA256 -LiteralPath $destination).Hash.ToLowerInvariant() -eq $asset.Sha256
        if ($matchesPinnedHash) {
            Write-Host "Model file already present: $($asset.Name)"
            continue
        }
        $backup = "$destination.invalid-$(Get-Date -Format 'yyyyMMddHHmmss')"
        Move-Item -LiteralPath $destination -Destination $backup
    }
    $partial = "$destination.part"
    if (Test-Path -LiteralPath $partial -PathType Leaf) { Remove-Item -LiteralPath $partial -Force }
    Write-Host "Downloading $($asset.Name) ($([Math]::Round($asset.Size / 1MB, 1)) MiB)"
    Invoke-WebRequest -UseBasicParsing -Uri $asset.Url -OutFile $partial
    if ((Get-Item -LiteralPath $partial).Length -ne $asset.Size) {
        throw "Downloaded size mismatch for $($asset.Name)"
    }
    $downloadedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $partial).Hash.ToLowerInvariant()
    if ($downloadedHash -ne $asset.Sha256) {
        Remove-Item -LiteralPath $partial -Force
        throw "Downloaded SHA-256 mismatch for $($asset.Name). Expected $($asset.Sha256), received $downloadedHash"
    }
    Move-Item -LiteralPath $partial -Destination $destination
}

$hashRows = foreach ($asset in ($assets | Sort-Object Name)) {
    $path = Join-Path $modelRoot $asset.Name
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    if ($hash -ne $asset.Sha256) {
        throw "Installed SHA-256 mismatch for $($asset.Name). Expected $($asset.Sha256), received $hash"
    }
    [pscustomobject]@{ name = $asset.Name; size = $asset.Size; sha256 = $hash }
}
$revisionInput = (($hashRows | ForEach-Object { "$($_.name):$($_.sha256)" }) -join "`n")
$sha = [Security.Cryptography.SHA256]::Create()
try {
    $revisionBytes = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($revisionInput))
    $revision = 'sha256:' + (($revisionBytes | ForEach-Object { $_.ToString('x2') }) -join '')
} finally {
    $sha.Dispose()
}
$manifest = [ordered]@{
    provider = 'rapid-latex-ocr'
    packageVersion = '0.0.9'
    modelVersion = 'v0.0.0'
    modelSource = 'https://github.com/RapidAI/RapidLaTeXOCR/releases/tag/v0.0.0'
    modelLicense = 'CC-BY-NC-SA (upstream pix2tex weights; review upstream terms)'
    revision = $revision
    files = @($hashRows)
}
$manifestJson = $manifest | ConvertTo-Json -Depth 5
[IO.File]::WriteAllText((Join-Path $modelRoot 'manifest.json'), $manifestJson, (New-Object Text.UTF8Encoding($false)))

& $venvPython -c 'import pathlib,sys; import onnxruntime, PIL, rapid_latex_ocr, tokenizers; root=pathlib.Path(sys.prefix).resolve(); modules=(onnxruntime,PIL,rapid_latex_ocr,tokenizers); assert all(pathlib.Path(m.__file__).resolve().is_relative_to(root) for m in modules); print(sys.prefix)'
if ($LASTEXITCODE -ne 0) { throw 'Formula OCR import verification failed' }

$completed = [ordered]@{
    schemaVersion = 1
    completedAtUtc = [DateTime]::UtcNow.ToString('o')
    installRoot = $root
    python = $venvPython
    packageVersion = '0.0.9'
    modelVersion = 'v0.0.0'
    modelRevision = $revision
}
[IO.File]::WriteAllText($installMarker, ($completed | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding($false)))

Write-Host ''
Write-Host 'Formula OCR installation completed.'
Write-Host "Root: $root"
Write-Host "Python: $venvPython"
Write-Host "Models: $modelRoot"
Write-Host "Revision: $revision"
Write-Host 'Select this root in Excalidraw Manager Settings > Formula OCR folder.'
