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
$overlayPath = Join-Path $RuntimeDir 'formula-overlay.mjs'
$providerServerPath = Join-Path $RuntimeDir 'formula-ocr-provider\server.py'
$captureHelperPath = Join-Path $RuntimeDir 'FormulaCapture.exe'
$captureSourcePath = Join-Path $root 'src\FormulaCapture.cs'
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
    $overlayPath,
    $providerServerPath,
    (Join-Path $editorPath 'index.html'),
    (Join-Path $editorPath 'app.mjs'),
    (Join-Path $editorPath 'completion.mjs'),
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
Assert-True (Test-Path -LiteralPath $captureSourcePath -PathType Leaf) 'in-memory screen capture source exists'
$captureSource = Get-Content -Raw -LiteralPath $captureSourcePath -Encoding utf8
Assert-True ($captureSource.Contains('SystemInformation.VirtualScreen')) 'screen capture spans the Windows virtual screen'
Assert-True ($captureSource.Contains('CopyFromScreen')) 'screen pixels are captured before selection'
Assert-True ($captureSource.Contains('new MemoryStream()')) 'selected PNG is encoded in memory'
Assert-True ($captureSource.Contains('Keys.Escape')) 'screen capture supports Escape cancellation'
Assert-True ($captureSource.Contains('SetProcessDpiAwarenessContext')) 'screen capture supports mixed-DPI monitor coordinates'
Assert-True (-not $captureSource.Contains('SaveDialog')) 'screen capture does not open a file save dialog'
$desktopLimitCheck = $captureSource.IndexOf('ValidateDesktopSize(virtualScreen)', [StringComparison]::Ordinal)
$desktopAllocation = $captureSource.IndexOf('new Bitmap(virtualScreen.Width', [StringComparison]::Ordinal)
$selectionLimitCheck = $captureSource.IndexOf('ValidateSelectionSize(selection, desktop.Size)', [StringComparison]::Ordinal)
$selectionAllocation = $captureSource.IndexOf('desktop.Clone(selection', [StringComparison]::Ordinal)
Assert-True ($desktopLimitCheck -ge 0 -and $desktopLimitCheck -lt $desktopAllocation) 'virtual-screen pixel limit is checked before full bitmap allocation'
Assert-True ($selectionLimitCheck -ge 0 -and $selectionLimitCheck -lt $selectionAllocation) 'selection pixel limit is checked before crop allocation and PNG encoding'
Assert-True ($captureSource.Contains('output.Length > MaximumPngBytes')) 'encoded PNG bytes are limited before base64 conversion'

if (Test-Path -LiteralPath $captureHelperPath -PathType Leaf) {
    $captureStartInfo = New-Object Diagnostics.ProcessStartInfo
    $captureStartInfo.FileName = $captureHelperPath
    $captureStartInfo.Arguments = '--describe'
    $captureStartInfo.UseShellExecute = $false
    $captureStartInfo.CreateNoWindow = $true
    $captureStartInfo.RedirectStandardOutput = $true
    $captureStartInfo.RedirectStandardError = $true
    $captureProcess = [Diagnostics.Process]::Start($captureStartInfo)
    try {
        $captureDescriptionText = $captureProcess.StandardOutput.ReadToEnd()
        $captureErrorText = $captureProcess.StandardError.ReadToEnd()
        Assert-True ($captureProcess.WaitForExit(5000)) 'screen capture helper describe command exits promptly'
        Assert-Equal 0 $captureProcess.ExitCode "screen capture helper describe command succeeds: $captureErrorText"
        $captureDescription = $captureDescriptionText | ConvertFrom-Json
        Assert-Equal 'image/png' $captureDescription.mimeType 'screen capture helper reports PNG output'
        Assert-Equal $true $captureDescription.inMemory 'screen capture helper reports in-memory operation'
        Assert-Equal 64000000 $captureDescription.maximumDesktopPixels 'screen capture helper reports its full-desktop pixel limit'
        Assert-Equal 40000000 $captureDescription.maximumSelectionPixels 'screen capture helper reports its OCR selection pixel limit'
        Assert-Equal 12582912 $captureDescription.maximumPngBytes 'screen capture helper reports its PNG byte limit'
    } finally {
        if (-not $captureProcess.HasExited) { $captureProcess.Kill() }
        $captureProcess.Dispose()
    }
}

$versions = Get-Content -Raw -LiteralPath (Join-Path $editorPath 'vendor\versions.json') | ConvertFrom-Json
Assert-Equal '0.110.0' $versions.mathlive 'MathLive version is pinned'
Assert-Equal '4.1.3' $versions.mathjax 'MathJax version is pinned'
Assert-Equal '4.1.3' $versions.mathjaxFont 'MathJax font version is pinned'
$package = Get-Content -Raw -LiteralPath (Join-Path $root 'package.json') | ConvertFrom-Json
$packageLockText = Get-Content -Raw -LiteralPath (Join-Path $root 'package-lock.json')
$packageLockVersion = [regex]::Match($packageLockText, '"version"\s*:\s*"([^"]+)"').Groups[1].Value
Assert-Equal '0.4.0' $package.version 'package version matches the formula editor release'
Assert-Equal '0.4.0' $packageLockVersion 'lockfile version matches the formula editor release'

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

& $node (Join-Path $root 'tests\formula-template-insertion.mjs') $editorPath
if ($LASTEXITCODE -ne 0) { throw "Formula template insertion test failed with exit code $LASTEXITCODE" }

& $node (Join-Path $root 'tests\formula-completion-trigger.mjs') $editorPath
if ($LASTEXITCODE -ne 0) { throw "Formula completion trigger test failed with exit code $LASTEXITCODE" }

& $node (Join-Path $root 'tests\provider-registry.mjs') $RuntimeDir
if ($LASTEXITCODE -ne 0) { throw "Provider registry integration test failed with exit code $LASTEXITCODE" }

$captureStubSource = Join-Path $root 'tests\fixtures\FormulaCaptureStub.cs'
$captureStubDirectory = Join-Path $root '.tmp'
$captureStubOutput = Join-Path $captureStubDirectory ("FormulaCaptureStub-$PID-" + [guid]::NewGuid().ToString('N') + '.exe')
$csc = "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
New-Item -ItemType Directory -Force -Path $captureStubDirectory | Out-Null
try {
    & $csc /nologo /target:winexe /optimize+ /codepage:65001 /out:$captureStubOutput `
        /reference:System.dll `
        /reference:System.Drawing.dll `
        $captureStubSource
    if ($LASTEXITCODE -ne 0) { throw "Capture endpoint stub build failed with exit code $LASTEXITCODE" }
    & $node (Join-Path $root 'tests\capture-server.mjs') $RuntimeDir $captureStubOutput
    if ($LASTEXITCODE -ne 0) { throw "Capture endpoint integration test failed with exit code $LASTEXITCODE" }
} finally {
    Remove-Item -LiteralPath $captureStubOutput -Force -ErrorAction SilentlyContinue
}

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
    Assert-Equal '0.4.0' $status.version 'status exposes the application version'
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
    Assert-True ($homeResponse.Body.Contains('id="source-highlight"')) 'source editor includes the syntax-highlight mirror'
    Assert-True ($homeResponse.Body.Contains('id="command-suggestions"')) 'source editor includes keyboard command suggestions'
    Assert-True ($homeResponse.Body.Contains('id="quick-popover"')) 'home page includes the compact formula palette popover'
    Assert-True ($homeResponse.Body.Contains('id="quick-symbol-grid"')) 'compact formula palette has a shared symbol grid'
    Assert-True ($homeResponse.Body.Contains('id="capture-ocr"')) 'formula editor exposes in-memory screen capture OCR'
    Assert-True ($homeResponse.Body.Contains('id="copy-main"') -and $homeResponse.Body.Contains('id="copy-menu"')) 'formula editor exposes a split copy action'
    Assert-True (([regex]::Matches($homeResponse.Body, 'data-copy-format=')).Count -eq 6) 'copy menu exposes the six supported formats'
    Assert-True (-not $homeResponse.Body.Contains('id="templates-panel"')) 'formula template panel has been removed'
    Assert-True ($homeResponse.Body.Contains('id="math-field"')) 'home page includes the MathLive visual editor'
    Assert-True (($homeResponse.Headers.GetValues('X-Formula-Editor-Version') | Select-Object -First 1) -eq '0.4.0') 'security/version response header is present'

    $mathLive = Invoke-HttpRequest $client 'GET' "$baseUrl/vendor/mathlive/mathlive.min.mjs"
    Assert-Equal 200 $mathLive.Status 'MathLive browser module is served locally'
    Assert-True ($mathLive.Body.Length -gt 100000) 'MathLive browser module is complete'

    $mathJax = Invoke-HttpRequest $client 'GET' "$baseUrl/vendor/mathjax/tex-mml-svg.js"
    Assert-Equal 200 $mathJax.Status 'MathJax SVG browser bundle is served locally'
    Assert-True ($mathJax.Body.Length -gt 1000000) 'MathJax SVG browser bundle is complete'

    $editorScript = Invoke-HttpRequest $client 'GET' "$baseUrl/app.mjs"
    Assert-Equal 200 $editorScript.Status 'formula editor script is served'
    Assert-True ($editorScript.Body.Contains('excalidraw-manager:insert-svg')) 'compact editor exposes the SVG insertion message contract'
    Assert-True ($editorScript.Body.Contains("params.get('compact') === '1'")) 'compact URL mode is implemented'
    Assert-True ($editorScript.Body.Contains('new URL(state.boardUrl).origin')) 'embedded SVG messages use the exact board origin'
    Assert-True ($editorScript.Body.Contains('url.username || url.password')) 'board links reject embedded URL credentials'
    Assert-True (-not $editorScript.Body.Contains('<?xml version=')) 'clipboard SVG starts directly with the svg element for Excalidraw paste'
    Assert-True ($editorScript.Body.Contains('ocrGeneration')) 'stale OCR requests are guarded by a generation token'
    Assert-True ($editorScript.Body.Contains("new Set(['image/png', 'image/jpeg', 'image/webp'])")) 'OCR input MIME types are explicitly allowlisted'
    Assert-True ($editorScript.Body.Contains("new Set(['svg', 'png', 'latex', 'markdown-inline', 'markdown-block', 'mathml'])")) 'copy formats are explicitly allowlisted'
    Assert-True ($editorScript.Body.Contains('function copySelectedFormat()')) 'button and shortcut share one selected-format copy action'
    Assert-True ($editorScript.Body.Contains("new ClipboardItem({ 'image/png': blob })")) 'PNG copy writes an actual image clipboard item'
    Assert-True ($editorScript.Body.Contains('if (await copySvg())')) 'copy-and-open-board remains fixed to SVG'
    Assert-True ($editorScript.Body.Contains("params.get('native') === '1' ? t('paletteTitle')")) 'native compact windows expose a stable localized title'
    Assert-True ($editorScript.Body.Contains("item[0].toLowerCase().startsWith(prefix)")) 'command matching is case-insensitive for mixed-case LaTeX commands'
    Assert-True ($editorScript.Body.Contains('.slice(0, 12)')) 'command popup exposes an expanded but bounded result set'
    Assert-True ($editorScript.Body.Contains('if (isCommandTypingInput(event)) updateCommandSuggestions()')) 'command suggestions are armed only by direct command typing'
    Assert-True ($editorScript.Body.Contains("source.addEventListener('pointerdown', hideCommandSuggestions)")) 'mouse selection immediately closes command suggestions'
    Assert-True ($editorScript.Body.Contains("document.addEventListener('selectionchange'")) 'range selection keeps command suggestions closed'
    Assert-True (-not $editorScript.Body.Contains("source.addEventListener('click', updateCommandSuggestions)")) 'mouse caret placement cannot reopen command suggestions'
    Assert-True ($editorScript.Body.Contains("button.addEventListener('pointerenter'")) 'formula categories open on pointer hover'
    Assert-True ($editorScript.Body.Contains("openQuickGroup(group, button, { pinned: true, focusFirst: true })")) 'formula categories support keyboard opening and focus transfer'
    Assert-True ($editorScript.Body.Contains("fetch('/api/capture'")) 'screen capture uses the same-origin local capture endpoint'
    Assert-True ($editorScript.Body.Contains('signal: controller.signal')) 'screen capture request can be cancelled when the palette closes'
    Assert-True ($editorScript.Body.Contains('state.captureController?.abort()')) 'closing the palette aborts an active screen capture request'
    Assert-True ($editorScript.Body.Contains('state.compact && state.imageFile === file')) 'compact OCR releases its inaccessible in-memory screenshot after recognition'
    Assert-True ($editorScript.Body.Contains('excalidraw-manager:capture-formula')) 'embedded palettes accept the validated capture shortcut message'
    Assert-True ($editorScript.Body.Contains('event.source !== window.parent')) 'capture shortcut messages validate the exact parent window'
    Assert-True ($editorScript.Body.Contains("event.altKey && event.key.toLowerCase() === 'o'")) 'Ctrl+Alt+O starts in-memory capture from the formula editor'
    Assert-True ($editorScript.Body.Contains("mathfield.insert(insertion.latex")) 'visual palette insertion uses the MathLive caret and selection'
    Assert-True ($editorScript.Body.Contains("insertionMode: 'replaceSelection'")) 'visual palette insertion replaces the active MathLive selection'
    Assert-True ($editorScript.Body.Contains('groupsRect.bottom - panelRect.top')) 'quick category popovers open below the single category strip'
    Assert-True ($editorScript.Body.Contains('groupsRect.bottom - panelRect.top + 2')) 'quick category popovers leave only a minimal pointer-travel gap'
    Assert-True ($editorScript.Body.Contains('setTimeout(() => closeQuickGroup(), 280)')) 'hover popovers allow enough time to cross into the symbol grid'
    Assert-True ($editorScript.Body.Contains('function updatePreviewScale()')) 'formula preview has a responsive width fitter'
    Assert-True ($editorScript.Body.Contains('Math.min(naturalWidth * (state.zoom / 100), availableWidth)')) 'wide previews scale down to the available panel width'

    $catalog = Invoke-HttpRequest $client 'GET' "$baseUrl/templates.mjs"
    Assert-Equal 200 $catalog.Status 'formula command catalog is served'
    Assert-True ($catalog.Body.Contains('export const latexCommands')) 'command completion catalog is exported separately from quick symbols'
    Assert-True ($catalog.Body.Contains("['leftarrow'")) 'typing a left-arrow prefix can offer leftarrow'
    Assert-True ($catalog.Body.Contains("['leftrightarrow'")) 'typing a left-arrow prefix can offer leftrightarrow'
    Assert-True ($catalog.Body.Contains("['leftharpoonup'")) 'left-arrow completion family includes harpoons'
    Assert-True ($catalog.Body.Contains("id: 'matrices'")) 'quick palette includes compact matrix snippets'
    Assert-True ($catalog.Body.Contains("id: 'style'")) 'quick palette includes font and accent snippets'
    Assert-True (([regex]::Matches($catalog.Body, "id: '[a-z]+'")).Count -ge 10) 'quick palette exposes at least ten compact categories'
    Assert-True ($catalog.Body.Contains('\\\\\n&')) 'environment completions include a real newline after a LaTeX row break'
    Assert-True ($catalog.Body.Contains("['sqrt', '\\sqrt[") -and $catalog.Body.Contains("]{}', '\\sqrt[n]{x}'")) 'n-th root command keeps its editable slot in the root index'

    $editorStyles = Invoke-HttpRequest $client 'GET' "$baseUrl/styles.css"
    Assert-Equal 200 $editorStyles.Status 'formula editor styles are served'
    Assert-True ($editorStyles.Body.Contains('.quick-popover')) 'formula palette popover has dedicated styling'
    Assert-True ($editorStyles.Body.Contains('position: absolute')) 'formula palette closes without reserving popover height'
    Assert-True ($editorStyles.Body.Contains('.compact-mode #quick-panel')) 'floating compact mode keeps the formula category strip available'
    Assert-True ($editorStyles.Body.Contains('grid-template-columns: repeat(10, minmax(0, 1fr))')) 'all ten compact formula categories stay on one row'
    Assert-True (-not $editorStyles.Body.Contains('grid-template-columns: repeat(auto-fit, minmax(38px, 1fr))')) 'compact formula categories no longer wrap into a second row'
    Assert-True ($editorStyles.Body.Contains('grid-template-columns: max-content minmax(0, 1fr)')) 'compact OCR stays smaller while copy receives the remaining width'
    Assert-True ($editorStyles.Body.Contains('.compact-mode .output-card') -and $editorStyles.Body.Contains('overflow: visible')) 'compact output allows the copy menu to extend above the preview card without clipping'
    Assert-True ($editorStyles.Body.Contains('.compact-mode .copy-menu:not(.hidden)') -and $editorStyles.Body.Contains('grid-template-columns: repeat(2, minmax(0, 1fr))')) 'compact copy formats use a narrow two-column menu so every option remains visible'
    Assert-True ($editorStyles.Body.Contains('--primary: #343a46')) 'formula controls use a neutral primary accent'
    Assert-True (-not $editorStyles.Body.Contains('#6a51d6') -and -not $editorStyles.Body.Contains('#c4a7ff') -and -not $editorStyles.Body.Contains('#ff79c6')) 'formula syntax highlighting contains no legacy purple accent'
    Assert-True ($editorStyles.Body.Contains('white-space: pre-wrap')) 'LaTeX source wraps in narrow windows'
    Assert-True ($editorStyles.Body.Contains('overflow-x: hidden')) 'formula editor suppresses horizontal scrollbars'
    Assert-True (-not $editorStyles.Body.Contains('overflow-x: auto')) 'formula editor no longer requires sideways scrolling'

    $overlayScript = Get-Content -LiteralPath $overlayPath -Encoding utf8 -Raw
    Assert-True ($overlayScript.Contains("new File([svg], 'formula.svg'")) 'board overlay pastes a real SVG file'
    Assert-True ($overlayScript.Contains("new PointerEvent('pointermove'")) 'board overlay moves the Excalidraw insertion point onto a visible canvas'
    Assert-True ($overlayScript.Contains('event.origin !== formulaUrl.origin')) 'board overlay validates the formula frame origin'
    Assert-True ($overlayScript.Contains('event.source !== frame.contentWindow')) 'board overlay validates the exact formula iframe window'
    Assert-True ($overlayScript.Contains('documentValue.doctype')) 'board overlay rejects SVG documents with a doctype'
    Assert-True ($overlayScript.Contains('parsererror, script, style, foreignObject')) 'board overlay rejects active SVG nodes'
    Assert-True ($overlayScript.Contains("name.startsWith('on')")) 'board overlay rejects SVG event-handler attributes'
    Assert-True ($overlayScript.Contains("!attributeValue.startsWith('#')")) 'board overlay rejects external SVG references'
    Assert-True ($overlayScript.Contains('MAX_SVG_BYTES = 4 * 1024 * 1024')) 'board overlay retains its SVG size limit'
    Assert-True ($overlayScript.Contains('savePlacement(panel, false)')) 'board overlay saves its geometry before it is hidden'
    Assert-True ($overlayScript.Contains("['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw']")) 'board overlay exposes resize handles on every edge and corner'
    Assert-True (-not $overlayScript.Contains('id="popout"')) 'board overlay does not offer a redundant second floating-window button'
    Assert-True (-not $overlayScript.Contains('documentPictureInPicture') -and -not $overlayScript.Contains("window.open('', '_blank'")) 'board overlay contains no unused popup-window implementation'
    Assert-True ($overlayScript.Contains('id="full"') -and $overlayScript.Contains('id="close"')) 'board overlay keeps only its useful full-editor and close header actions'
    Assert-True ($overlayScript.Contains("panel.style.pointerEvents = 'none'")) 'visible palette does not block Excalidraw canvas hit testing during insertion'
    Assert-True ($overlayScript.Contains('excalidraw-manager:capture-formula')) 'board shortcut can request in-memory screenshot OCR'
    Assert-True ($overlayScript.Contains('const MIN_PANEL_WIDTH = 260')) 'board overlay can be resized to a narrow responsive width'
    Assert-True ($overlayScript.Contains('container-type: inline-size')) 'board overlay header responds to panel width instead of board width'

    $serverScript = Get-Content -LiteralPath $serverPath -Encoding utf8 -Raw
    Assert-True ($serverScript.Contains("url.pathname === '/api/capture'")) 'formula server exposes the local screen capture endpoint'
    Assert-True ($serverScript.Contains("spawn(captureHelperPath, ['--parent-pid', String(process.pid), '--lang', defaultLanguage]")) 'formula server starts only its configured capture helper with a parent watcher and UI language'
    Assert-True ($serverScript.Contains("mediaType: 'image/png'")) 'capture API returns the image contract expected by the editor'
    Assert-True ($serverScript.Contains("kind: 'image'")) 'capture API labels returned content as an image'
    Assert-True ($serverScript.Contains('dataBase64: payload.image')) 'capture API returns base64 image data without a temporary file'
    Assert-True ($serverScript.Contains("'CAPTURE_BUSY'")) 'capture API rejects concurrent selection sessions'
    Assert-True ($serverScript.Contains('captureTimeoutMilliseconds = 120_000')) 'capture helper execution has a fixed timeout'
    Assert-True ($serverScript.Contains('createClientLifetime(req, res)')) 'capture and OCR routes monitor the client connection lifetime'
    Assert-True ($serverScript.Contains('requestStop(clientAbortError())')) 'client disconnect terminates the active capture helper'
    Assert-True ($serverScript.Contains('providers.recognize(request, lifetime.signal)')) 'client disconnect signal is forwarded to downstream OCR'

    $missing = Invoke-HttpRequest $client 'GET' "$baseUrl/definitely-not-present.txt"
    Assert-Equal 404 $missing.Status 'missing static files return 404'

    $traversal = Invoke-HttpRequest $client 'GET' "$baseUrl/%2e%2e%2fpackage.json"
    Assert-Equal 400 $traversal.Status 'encoded path traversal is rejected'
    Assert-True (-not $traversal.Body.Contains('excalidraw-manager-formula-editor-assets')) 'path traversal cannot expose repository files'

    $badHost = Invoke-HttpRequest $client 'GET' "$baseUrl/api/status" $null @{ Host = 'attacker.example' }
    Assert-Equal 421 $badHost.Status 'non-loopback Host headers are rejected'

    $badOrigin = Invoke-HttpRequest $client 'POST' "$baseUrl/api/recognize" '{"mode":"formula","image":"AA=="}' @{ Origin = 'https://attacker.example' }
    Assert-Equal 403 $badOrigin.Status 'cross-origin recognition requests are rejected'

    $badCaptureOrigin = Invoke-HttpRequest $client 'POST' "$baseUrl/api/capture" '{}' @{ Origin = 'https://attacker.example' }
    Assert-Equal 403 $badCaptureOrigin.Status 'cross-origin screen capture requests are rejected before the helper starts'

    if (-not (Test-Path -LiteralPath $captureHelperPath -PathType Leaf)) {
        $captureUnavailable = Invoke-HttpRequest $client 'POST' "$baseUrl/api/capture" '{}'
        Assert-Equal 503 $captureUnavailable.Status 'capture endpoint reports a missing helper without blocking'
        $captureUnavailableError = $captureUnavailable.Body | ConvertFrom-Json
        Assert-Equal 'CAPTURE_UNAVAILABLE' $captureUnavailableError.error 'missing capture helper has a stable error code'
    }

    $recognize = Invoke-HttpRequest $client 'POST' "$baseUrl/api/recognize" '{"mode":"formula","image":"AA=="}'
    Assert-Equal 503 $recognize.Status 'POST /api/recognize reports unavailable optional model'
    $recognizeError = $recognize.Body | ConvertFrom-Json
    Assert-Equal 'MODEL_UNAVAILABLE' $recognizeError.error 'recognition endpoint returns a stable machine-readable error'

    $largeWarmupBody = '{"padding":"' + ('x' * (1024 * 1024)) + '"}'
    $largeWarmup = Invoke-HttpRequest $client 'POST' "$baseUrl/api/warmup" $largeWarmupBody
    Assert-Equal 413 $largeWarmup.Status 'warmup request bodies above 1 MiB are rejected'

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
