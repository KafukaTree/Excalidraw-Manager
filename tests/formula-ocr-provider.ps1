[CmdletBinding()]
param(
    [string]$PythonPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$serverPath = Join-Path $repoRoot 'runtime\formula-ocr-provider\server.py'
$driveRoot = [System.IO.Path]::GetPathRoot($repoRoot)
$tempParent = Join-Path $driveRoot 'tmp'
$tempRoot = Join-Path $tempParent ("ExcalidrawManager-formula-ocr-test-{0}" -f [guid]::NewGuid().ToString('N'))
$modelRoot = Join-Path $tempRoot 'models'
$token = 'offline-contract-test-token-0123456789abcdef'
$tokenVariable = 'EXCALIDRAW_MANAGER_FORMULA_OCR_TOKEN'
$client = $null
$processes = New-Object System.Collections.ArrayList
$passed = 0
$testError = $null
$cleanupError = $null
$pythonPrefixArguments = @()

if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    $pythonCommand = Get-Command python.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $pythonCommand) {
        $PythonPath = $pythonCommand.Source
    }
    else {
        $pythonLauncher = Get-Command py.exe -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -ne $pythonLauncher) {
            $PythonPath = $pythonLauncher.Source
            $pythonPrefixArguments = @('-3')
        }
    }
}

function Assert-Equal {
    param(
        [Parameter(Mandatory = $true)]$Expected,
        [Parameter(Mandatory = $true)]$Actual,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($Expected -ne $Actual) {
        throw "$Name`nExpected: $Expected`nActual:   $Actual"
    }
    $script:passed++
    Write-Host "PASS $Name"
}

function Assert-True {
    param(
        [Parameter(Mandatory = $true)][bool]$Condition,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if (-not $Condition) {
        throw $Name
    }
    $script:passed++
    Write-Host "PASS $Name"
}

function Convert-ResponseJson {
    param(
        [Parameter(Mandatory = $true)]$Response,
        [Parameter(Mandatory = $true)][string]$Name
    )

    try {
        return $Response.Body | ConvertFrom-Json
    }
    catch {
        throw "$Name did not return valid JSON. Body: $($Response.Body)"
    }
}

function Get-FreeLoopbackPort {
    $listener = New-Object System.Net.Sockets.TcpListener([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Start-Provider {
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [Parameter(Mandatory = $true)][int]$ParentPid,
        [Parameter(Mandatory = $true)][string]$Label
    )

    $stdoutPath = Join-Path $tempRoot "$Label.stdout.log"
    $stderrPath = Join-Path $tempRoot "$Label.stderr.log"
    $previousToken = [Environment]::GetEnvironmentVariable($tokenVariable, 'Process')
    try {
        [Environment]::SetEnvironmentVariable($tokenVariable, $token, 'Process')
        $providerArguments = @($pythonPrefixArguments) + @(
            $serverPath,
            '--host', '127.0.0.1',
            '--port', $Port,
            '--model-root', $modelRoot,
            '--parent-pid', $ParentPid,
            '--token-env', $tokenVariable
        )
        $process = Start-Process -FilePath $PythonPath -ArgumentList $providerArguments -WorkingDirectory $repoRoot -WindowStyle Hidden -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
    }
    finally {
        [Environment]::SetEnvironmentVariable($tokenVariable, $previousToken, 'Process')
    }

    [void]$script:processes.Add($process)
    return [pscustomobject]@{
        Process = $process
        Stdout = $stdoutPath
        Stderr = $stderrPath
        Port = $Port
    }
}

function Invoke-ProviderRequest {
    param(
        [Parameter(Mandatory = $true)][int]$Port,
        [ValidateSet('GET', 'POST')][string]$Method = 'GET',
        [string]$Path = '/v1/info',
        [AllowNull()][string]$BearerToken = $token,
        [AllowNull()][string]$Body = $null,
        [string]$ContentType = 'application/json',
        [AllowNull()][string]$HostHeader = $null,
        [AllowNull()][string]$Origin = $null
    )

    $uri = New-Object System.Uri("http://127.0.0.1:$Port$Path")
    $request = New-Object System.Net.Http.HttpRequestMessage(
        [System.Net.Http.HttpMethod]::new($Method),
        $uri
    )
    try {
        if ($null -eq $HostHeader) {
            $request.Headers.Host = "127.0.0.1:$Port"
        }
        else {
            $request.Headers.Host = $HostHeader
        }
        if ($null -ne $BearerToken) {
            [void]$request.Headers.TryAddWithoutValidation('Authorization', "Bearer $BearerToken")
        }
        if ($null -ne $Origin) {
            [void]$request.Headers.TryAddWithoutValidation('Origin', $Origin)
        }
        if ($Method -eq 'POST') {
            $request.Content = New-Object System.Net.Http.StringContent(
                $(if ($null -eq $Body) { '' } else { $Body }),
                [System.Text.Encoding]::UTF8,
                $ContentType
            )
        }

        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            return [pscustomobject]@{
                Status = [int]$response.StatusCode
                Body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            }
        }
        finally {
            $response.Dispose()
        }
    }
    finally {
        $request.Dispose()
    }
}

function Wait-ProviderReady {
    param(
        [Parameter(Mandatory = $true)]$Provider,
        [int]$TimeoutSeconds = 10
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $Provider.Process.Refresh()
        if ($Provider.Process.HasExited) {
            $stderr = if (Test-Path -LiteralPath $Provider.Stderr) {
                Get-Content -Raw -LiteralPath $Provider.Stderr
            }
            else { '' }
            throw "Provider exited before becoming ready (exit $($Provider.Process.ExitCode)). $stderr"
        }
        try {
            $response = Invoke-ProviderRequest -Port $Provider.Port
            if ($response.Status -eq 200) {
                return
            }
        }
        catch {
            # Connection refusal is expected until the child binds its port.
        }
        Start-Sleep -Milliseconds 100
    }
    throw "Provider did not become ready within $TimeoutSeconds seconds."
}

function Wait-ProcessExit {
    param(
        [Parameter(Mandatory = $true)][System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 12
    )

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        $Process.Refresh()
        if ($Process.HasExited) {
            return $true
        }
        Start-Sleep -Milliseconds 200
    }
    return $false
}

try {
    if ([string]::IsNullOrWhiteSpace($PythonPath) -or -not (Test-Path -LiteralPath $PythonPath -PathType Leaf)) {
        throw "Python 3 was not found. Add python.exe or py.exe to PATH, or pass -PythonPath explicitly."
    }
    if (-not (Test-Path -LiteralPath $serverPath -PathType Leaf)) {
        throw "Provider entry point was not found at '$serverPath'."
    }
    if ([System.IO.Path]::GetPathRoot($tempRoot) -ne $driveRoot) {
        throw "Refusing to use a temporary directory outside the repository drive: '$tempRoot'."
    }

    New-Item -ItemType Directory -Path $modelRoot -Force | Out-Null
    foreach ($fileName in @('image_resizer.onnx', 'encoder.onnx', 'decoder.onnx', 'tokenizer.json')) {
        New-Item -ItemType File -Path (Join-Path $modelRoot $fileName) -Force | Out-Null
    }
    $badManifest = [ordered]@{
        revision = 'sha256:' + ('0' * 64)
        files = @('decoder.onnx', 'encoder.onnx', 'image_resizer.onnx', 'tokenizer.json') | ForEach-Object {
            [ordered]@{ name = $_; size = 0; sha256 = '0' * 64 }
        }
    }
    [IO.File]::WriteAllText((Join-Path $modelRoot 'manifest.json'), ($badManifest | ConvertTo-Json -Depth 4), (New-Object Text.UTF8Encoding($false)))

    Add-Type -AssemblyName System.Net.Http
    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.UseProxy = $false
    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(5)

    $port = Get-FreeLoopbackPort
    $provider = Start-Provider -Port $port -ParentPid 0 -Label 'contract'
    Wait-ProviderReady -Provider $provider

    $response = Invoke-ProviderRequest -Port $port -BearerToken $null
    Assert-Equal 401 $response.Status 'missing token is rejected'
    Assert-Equal 'UNAUTHORIZED' (Convert-ResponseJson $response 'missing token').error.code 'missing token error code'

    $response = Invoke-ProviderRequest -Port $port -BearerToken 'wrong-token'
    Assert-Equal 401 $response.Status 'wrong token is rejected'
    Assert-Equal 'UNAUTHORIZED' (Convert-ResponseJson $response 'wrong token').error.code 'wrong token error code'

    $response = Invoke-ProviderRequest -Port $port
    Assert-Equal 200 $response.Status 'info succeeds with a valid token'
    $info = Convert-ResponseJson $response 'info'
    Assert-Equal '1.0' $info.apiVersion 'info exposes the provider API version'
    Assert-Equal 'rapid-latex-ocr-onnx' $info.models[0].id 'info exposes the configured model'
    Assert-Equal 12582912 ([int]$info.limits.maxInputBytes) 'info exposes the decoded image size limit'

    $response = Invoke-ProviderRequest -Port $port -Path '/v1/health'
    Assert-Equal 200 $response.Status 'health endpoint responds'
    $health = Convert-ResponseJson $response 'health'
    Assert-Equal $false ([bool]$health.ready) 'info did not load the model'
    Assert-Equal 0 @($health.loadedModels).Count 'no model is reported as loaded'
    Assert-Equal 'unavailable' $health.status 'health rejects an invalid model installation'
    Assert-True (@($health.warnings).Count -gt 0) 'health explains a missing runtime or model prerequisite'
    Assert-True (@($health.integrityErrors).Count -gt 0) 'health exposes model-manifest integrity failure'

    $response = Invoke-ProviderRequest -Port $port -Method POST -Path '/v1/warmup' -Body '{}'
    Assert-Equal 503 $response.Status 'empty model files cannot warm up'
    $warmupError = Convert-ResponseJson $response 'warmup'
    Assert-Equal 'MODEL_UNAVAILABLE' $warmupError.error.code 'warmup reports model unavailable'
    Assert-True (@($warmupError.error.details.integrityErrors).Count -gt 0) 'warmup reports model-manifest integrity failure'
    Assert-True (@($warmupError.error.details.integrityErrors.reason) -contains 'SHA-256 mismatch') 'warmup detects a model SHA-256 mismatch'

    $response = Invoke-ProviderRequest -Port $port -HostHeader 'formula.example.invalid'
    Assert-Equal 421 $response.Status 'non-loopback Host is rejected'
    Assert-Equal 'HOST_NOT_ALLOWED' (Convert-ResponseJson $response 'bad Host').error.code 'bad Host error code'

    $response = Invoke-ProviderRequest -Port $port -Origin 'https://example.invalid'
    Assert-Equal 403 $response.Status 'browser Origin is rejected'
    Assert-Equal 'ORIGIN_NOT_ALLOWED' (Convert-ResponseJson $response 'bad Origin').error.code 'bad Origin error code'

    $response = Invoke-ProviderRequest -Port $port -Method POST -Path '/v1/recognize' -ContentType 'text/plain' -Body '{}'
    Assert-Equal 415 $response.Status 'non-JSON content type is rejected'
    Assert-Equal 'UNSUPPORTED_MEDIA_TYPE' (Convert-ResponseJson $response 'bad content type').error.code 'bad content type error code'

    $response = Invoke-ProviderRequest -Port $port -Method POST -Path '/v1/recognize' -Body '{'
    Assert-Equal 400 $response.Status 'malformed JSON is rejected'
    Assert-Equal 'INVALID_REQUEST' (Convert-ResponseJson $response 'bad JSON').error.code 'bad JSON error code'

    $badBase64 = '{"mode":"formula","input":{"kind":"image","mediaType":"image/png","dataBase64":"%%%"}}'
    $response = Invoke-ProviderRequest -Port $port -Method POST -Path '/v1/recognize' -Body $badBase64
    Assert-Equal 400 $response.Status 'invalid base64 is rejected'
    Assert-Equal 'INVALID_REQUEST' (Convert-ResponseJson $response 'bad base64').error.code 'bad base64 error code'

    $badMime = '{"mode":"formula","input":{"kind":"image","mediaType":"image/gif","dataBase64":"AA=="}}'
    $response = Invoke-ProviderRequest -Port $port -Method POST -Path '/v1/recognize' -Body $badMime
    Assert-Equal 415 $response.Status 'unsupported image MIME type is rejected'
    Assert-Equal 'UNSUPPORTED_MEDIA_TYPE' (Convert-ResponseJson $response 'bad MIME').error.code 'bad MIME error code'

    $mismatchedMime = '{"mode":"formula","input":{"kind":"image","mediaType":"image/png","dataBase64":"bm90LWEtcG5n"}}'
    $response = Invoke-ProviderRequest -Port $port -Method POST -Path '/v1/recognize' -Body $mismatchedMime
    Assert-Equal 415 $response.Status 'image bytes must match the declared MIME type'
    Assert-Equal 'UNSUPPORTED_MEDIA_TYPE' (Convert-ResponseJson $response 'MIME mismatch').error.code 'MIME mismatch error code'

    $badMode = '{"mode":"document","input":{"kind":"image","mediaType":"image/png","dataBase64":"AA=="}}'
    $response = Invoke-ProviderRequest -Port $port -Method POST -Path '/v1/recognize' -Body $badMode
    Assert-Equal 422 $response.Status 'unsupported recognition mode is rejected'
    Assert-Equal 'MODE_UNSUPPORTED' (Convert-ResponseJson $response 'bad mode').error.code 'bad mode error code'

    $parent = Start-Process -FilePath 'powershell.exe' -ArgumentList @(
        '-NoLogo', '-NoProfile', '-NonInteractive', '-Command', 'Start-Sleep -Seconds 60'
    ) -WindowStyle Hidden -PassThru
    [void]$processes.Add($parent)
    $parentPort = Get-FreeLoopbackPort
    $parentedProvider = Start-Provider -Port $parentPort -ParentPid $parent.Id -Label 'parent-watch'
    Wait-ProviderReady -Provider $parentedProvider
    Start-Sleep -Seconds 4
    $parent.Refresh()
    $parentedProvider.Process.Refresh()
    Assert-True (-not $parent.HasExited) 'provider watcher never terminates a live parent process'
    Assert-True (-not $parentedProvider.Process.HasExited) 'provider remains alive while its parent process is alive'
    Stop-Process -Id $parent.Id -Force
    [void]$parent.WaitForExit(5000)
    Assert-True (Wait-ProcessExit -Process $parentedProvider.Process -TimeoutSeconds 12) 'provider exits after its parent process ends'
}
catch {
    $testError = $_
}
finally {
    if ($null -ne $client) {
        $client.Dispose()
    }

    foreach ($process in @($processes)) {
        try {
            $process.Refresh()
            if (-not $process.HasExited) {
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
                [void]$process.WaitForExit(5000)
            }
            $process.Dispose()
        }
        catch {
            if ($null -eq $cleanupError) {
                $cleanupError = $_
            }
        }
    }

    try {
        if (Test-Path -LiteralPath $tempRoot) {
            Remove-Item -LiteralPath $tempRoot -Recurse -Force
        }
        if (Test-Path -LiteralPath $tempRoot) {
            throw "The unique test directory was not removed: '$tempRoot'."
        }
    }
    catch {
        if ($null -eq $cleanupError) {
            $cleanupError = $_
        }
    }
}

if ($null -ne $testError) {
    throw $testError
}
if ($null -ne $cleanupError) {
    throw $cleanupError
}

Write-Host "Formula OCR provider contract tests passed ($passed assertions)."
