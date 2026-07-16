param(
    [string]$RuntimeDir
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if ([string]::IsNullOrWhiteSpace($RuntimeDir)) {
    $RuntimeDir = Join-Path $root 'runtime'
}
$RuntimeDir = [IO.Path]::GetFullPath($RuntimeDir)
$serverPath = Join-Path $RuntimeDir 'formula-server.mjs'
$providerRegistryPath = Join-Path $RuntimeDir 'formula-provider-registry.mjs'
$editorPath = Join-Path $RuntimeDir 'formula-editor'
$node = (Get-Command node -ErrorAction Stop).Source
$assertions = 0

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw "Assertion failed: $message" }
    $script:assertions++
}

function Assert-Equal($expected, $actual, [string]$message) {
    if ($expected -ne $actual) {
        throw "Assertion failed: $message (expected '$expected', got '$actual')"
    }
    $script:assertions++
}

function Quote-ProcessArgument([string]$value) {
    if ($value -notmatch '[\s"]') { return $value }
    return '"' + ($value -replace '(\\*)"', '$1$1\"' -replace '(\\+)$', '$1$1') + '"'
}

function Invoke-HttpRequest(
    [System.Net.Http.HttpClient]$client,
    [string]$method,
    [string]$url,
    [string]$body = $null,
    [hashtable]$headers = @{}
) {
    $request = New-Object System.Net.Http.HttpRequestMessage(
        [System.Net.Http.HttpMethod]::new($method),
        [Uri]::new($url)
    )
    if (-not [string]::IsNullOrEmpty($body)) {
        $request.Content = New-Object System.Net.Http.StringContent(
            $body,
            [Text.Encoding]::UTF8,
            'application/json'
        )
    }
    foreach ($name in $headers.Keys) {
        $request.Headers.TryAddWithoutValidation([string]$name, [string]$headers[$name]) | Out-Null
    }
    try {
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            $content = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            return [pscustomobject]@{
                Status = [int]$response.StatusCode
                Body = $content
                Headers = $response.Headers
                ContentHeaders = $response.Content.Headers
            }
        } finally {
            $response.Dispose()
        }
    } finally {
        $request.Dispose()
    }
}

$requiredFiles = @(
    $serverPath,
    $providerRegistryPath,
    (Join-Path $editorPath 'index.html'),
    (Join-Path $editorPath 'app.mjs'),
    (Join-Path $editorPath 'styles.css'),
    (Join-Path $editorPath 'templates.mjs'),
    (Join-Path $editorPath 'vendor\versions.json'),
    (Join-Path $editorPath 'vendor\mathlive\mathlive.min.mjs'),
    (Join-Path $editorPath 'vendor\mathlive\mathlive-fonts.css'),
    (Join-Path $editorPath 'vendor\mathlive\fonts\KaTeX_Main-Regular.woff2'),
    (Join-Path $editorPath 'vendor\mathjax\tex-mml-svg.js'),
    (Join-Path $editorPath 'vendor\mathjax\svg\dynamic\math.js')
)
foreach ($file in $requiredFiles) {
    Assert-True (Test-Path -LiteralPath $file -PathType Leaf) "required runtime file exists: $file"
    Assert-True ((Get-Item -LiteralPath $file).Length -gt 0) "required runtime file is not empty: $file"
}

$versions = Get-Content -Raw -LiteralPath (Join-Path $editorPath 'vendor\versions.json') | ConvertFrom-Json
Assert-Equal '0.110.0' $versions.mathlive 'MathLive version is pinned'
Assert-Equal '4.1.3' $versions.mathjax 'MathJax version is pinned'
Assert-Equal '4.1.3' $versions.mathjaxFont 'MathJax font version is pinned'

$javascriptFiles = @(
    Get-ChildItem -Recurse -File -LiteralPath $RuntimeDir |
        Where-Object { $_.Extension -eq '.js' -or $_.Extension -eq '.mjs' } |
        Select-Object -ExpandProperty FullName
)
foreach ($file in $javascriptFiles) {
    & $node --check $file
    if ($LASTEXITCODE -ne 0) { throw "JavaScript syntax check failed: $file" }
}
Write-Host "JavaScript syntax: $($javascriptFiles.Count) files passed"

& $node (Join-Path $root 'tests\provider-registry.mjs') $RuntimeDir
if ($LASTEXITCODE -ne 0) { throw "Provider registry integration test failed with exit code $LASTEXITCODE" }

$listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()

$arguments = @(
    $serverPath,
    '--host', '127.0.0.1',
    '--port', "$port",
    '--assets', $editorPath,
    '--parent-pid', "$PID",
    '--lang', 'zh-CN'
)
$startInfo = New-Object Diagnostics.ProcessStartInfo
$startInfo.FileName = $node
$startInfo.Arguments = (($arguments | ForEach-Object { Quote-ProcessArgument $_ }) -join ' ')
$startInfo.WorkingDirectory = $root
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$process = [Diagnostics.Process]::Start($startInfo)
$client = $null

try {
    Add-Type -AssemblyName System.Net.Http
    $client = New-Object System.Net.Http.HttpClient
    $client.Timeout = [TimeSpan]::FromSeconds(5)
    $baseUrl = "http://127.0.0.1:$port"
    $statusResponse = $null
    $deadline = [DateTime]::UtcNow.AddSeconds(15)
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.HasExited) {
            throw "Formula server exited during startup with code $($process.ExitCode)"
        }
        try {
            $statusResponse = Invoke-HttpRequest $client 'GET' "$baseUrl/api/status"
            if ($statusResponse.Status -eq 200) { break }
        } catch {
            Start-Sleep -Milliseconds 100
        }
    }
    Assert-True ($null -ne $statusResponse) 'formula server answered before the startup deadline'
    Assert-Equal 200 $statusResponse.Status 'GET /api/status succeeds'
    $status = $statusResponse.Body | ConvertFrom-Json
    Assert-Equal 'Excalidraw Manager Formula Editor' $status.app 'status identifies the formula editor'
    Assert-Equal '0.3.0' $status.version 'status exposes the application version'
    Assert-Equal $true $status.offline 'status reports offline operation'
    Assert-Equal $false $status.ocrAvailable 'status reports that OCR is optional and unavailable'
    Assert-Equal 0 @($status.providers).Count 'status has no installed model providers'

    $modelsResponse = Invoke-HttpRequest $client 'GET' "$baseUrl/api/models"
    Assert-Equal 200 $modelsResponse.Status 'GET /api/models succeeds'
    $models = $modelsResponse.Body | ConvertFrom-Json
    Assert-Equal 'formula-model-api-v1' $models.contract 'model endpoint exposes the replaceable provider contract'
    Assert-Equal 0 @($models.providers).Count 'model endpoint has no provider before optional installation'

    $homeResponse = Invoke-HttpRequest $client 'GET' "$baseUrl/"
    Assert-Equal 200 $homeResponse.Status 'formula editor home page is served'
    Assert-True ($homeResponse.Body.Contains('id="latex-source"')) 'home page includes the LaTeX source editor'
    Assert-True ($homeResponse.Body.Contains('id="math-field"')) 'home page includes the MathLive visual editor'
    Assert-True (($homeResponse.Headers.GetValues('X-Formula-Editor-Version') | Select-Object -First 1) -eq '0.3.0') 'security/version response header is present'

    $mathLive = Invoke-HttpRequest $client 'GET' "$baseUrl/vendor/mathlive/mathlive.min.mjs"
    Assert-Equal 200 $mathLive.Status 'MathLive browser module is served locally'
    Assert-True ($mathLive.Body.Length -gt 100000) 'MathLive browser module is complete'

    $mathJax = Invoke-HttpRequest $client 'GET' "$baseUrl/vendor/mathjax/tex-mml-svg.js"
    Assert-Equal 200 $mathJax.Status 'MathJax SVG browser bundle is served locally'
    Assert-True ($mathJax.Body.Length -gt 1000000) 'MathJax SVG browser bundle is complete'

    $missing = Invoke-HttpRequest $client 'GET' "$baseUrl/definitely-not-present.txt"
    Assert-Equal 404 $missing.Status 'missing static files return 404'

    $traversal = Invoke-HttpRequest $client 'GET' "$baseUrl/%2e%2e%2fpackage.json"
    Assert-Equal 400 $traversal.Status 'encoded path traversal is rejected'
    Assert-True (-not $traversal.Body.Contains('excalidraw-manager-formula-editor-assets')) 'path traversal cannot expose repository files'

    $badHost = Invoke-HttpRequest $client 'GET' "$baseUrl/api/status" $null @{ Host = 'attacker.example' }
    Assert-Equal 421 $badHost.Status 'non-loopback Host headers are rejected'

    $badOrigin = Invoke-HttpRequest $client 'POST' "$baseUrl/api/recognize" '{"mode":"formula","image":"AA=="}' @{ Origin = 'https://attacker.example' }
    Assert-Equal 403 $badOrigin.Status 'cross-origin recognition requests are rejected'

    $recognize = Invoke-HttpRequest $client 'POST' "$baseUrl/api/recognize" '{"mode":"formula","image":"AA=="}'
    Assert-Equal 503 $recognize.Status 'POST /api/recognize reports unavailable optional model'
    $recognizeError = $recognize.Body | ConvertFrom-Json
    Assert-Equal 'MODEL_UNAVAILABLE' $recognizeError.error 'recognition endpoint returns a stable machine-readable error'

    Write-Host "Formula editor HTTP/API tests passed ($assertions assertions)."
} finally {
    if ($null -ne $client) { $client.Dispose() }
    if ($null -ne $process) {
        if (-not $process.HasExited) {
            $process.Kill()
            $process.WaitForExit(5000) | Out-Null
        }
        $process.Dispose()
    }
}
