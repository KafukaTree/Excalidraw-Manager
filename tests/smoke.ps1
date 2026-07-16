$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$exe = Join-Path $root 'dist\ExcalidrawManager.exe'
$captureHelper = Join-Path $root 'dist\runtime\FormulaCapture.exe'
$board = Join-Path $env:TEMP ('excalidraw-manager-smoke-' + [guid]::NewGuid().ToString('N') + '.excalidraw')

if (-not (Test-Path -LiteralPath $exe)) {
    & (Join-Path $root 'build.ps1')
}
if (-not (Test-Path -LiteralPath $captureHelper -PathType Leaf)) {
    throw 'In-memory formula capture helper is missing from the runtime payload'
}

[void][Reflection.Assembly]::LoadFile($exe)
$instance = $null
try {
    [ExcalidrawManager.ProcessService]::CreateEmptyScene($board)
    $port = [ExcalidrawManager.ProcessService]::FindFreePort(16417)
    $instance = [ExcalidrawManager.ProcessService]::Start($board, $port, 'system')
    $meta = Invoke-RestMethod -Uri ($instance.Url + '/meta')
    $data = Invoke-RestMethod -Uri ($instance.Url + '/data')
    $library = Invoke-RestMethod -Uri ($instance.Url + '/library')
    $client = (Invoke-WebRequest -UseBasicParsing -Uri ($instance.Url + '/assets/main.js')).Content
    if ($meta.fileName -ne [IO.Path]::GetFileName($board)) { throw 'Unexpected /meta fileName' }
    if ($meta.libraryPersistence -ne $true) { throw 'Managed library persistence is not enabled' }
    if ([string]::IsNullOrWhiteSpace([string]$meta.formulaEditorUrl)) { throw 'Managed formula editor URL is missing' }
    if ($data.type -ne 'excalidraw' -or $data.version -ne 2) { throw 'Unexpected /data scene' }
    if ($library.type -ne 'excalidrawlib') { throw 'Unexpected /library document' }
    if (-not $client.Contains('onLibraryChange:saveManagedLibrary') -or -not $client.Contains('BG().catch(console.error);') -or -not $client.Contains('window.name="ExcalidrawManager"+location.port') -or -not $client.Contains('fetch("/library/import"') -or -not $client.Contains('startFormulaOverlay')) { throw 'Managed client hook/bootstrap/window binding/import/formula overlay missing' }
    $overlay = (Invoke-WebRequest -UseBasicParsing -Uri ($instance.Url + '/formula-overlay.mjs')).Content
    if (-not $overlay.Contains("new File([svg], 'formula.svg'") -or -not $overlay.Contains("new PointerEvent('pointermove'")) { throw 'Managed formula overlay runtime is incomplete' }
    Write-Host "PASS PID=$($instance.Pid) PORT=$($instance.Port)"
}
finally {
    if ($null -ne $instance) { [void][ExcalidrawManager.ProcessService]::Stop($instance.Pid) }
    Remove-Item -LiteralPath $board -Force -ErrorAction SilentlyContinue
}
