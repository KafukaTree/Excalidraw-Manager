param(
    [switch]$RuntimeOnly
)

$ErrorActionPreference = 'Stop'
$script:Failures = New-Object System.Collections.Generic.List[string]
$script:Warnings = New-Object System.Collections.Generic.List[string]

function Write-Check([string]$Name, [string]$Value) {
    Write-Host ("[OK]   {0}: {1}" -f $Name, $Value) -ForegroundColor Green
}

function Add-Failure([string]$Message) {
    $script:Failures.Add($Message)
    Write-Host ("[FAIL] {0}" -f $Message) -ForegroundColor Red
}

function Add-Warning([string]$Message) {
    $script:Warnings.Add($Message)
    Write-Host ("[WARN] {0}" -f $Message) -ForegroundColor Yellow
}

function Find-CommandPath([string]$Name) {
    $command = Get-Command $Name -ErrorAction SilentlyContinue
    if ($null -eq $command) { return $null }
    return $command.Source
}

if ($env:OS -ne 'Windows_NT') {
    Add-Failure 'Excalidraw Manager currently supports Windows only.'
} else {
    Write-Check 'Operating system' ([Environment]::OSVersion.VersionString)
}

if ($PSVersionTable.PSVersion -lt [version]'5.1') {
    Add-Failure ("PowerShell 5.1 or newer is required; found {0}." -f $PSVersionTable.PSVersion)
} else {
    Write-Check 'PowerShell' $PSVersionTable.PSVersion.ToString()
}

$nodePath = Find-CommandPath 'node.exe'
if ([string]::IsNullOrWhiteSpace($nodePath)) {
    Add-Failure 'node.exe is not on PATH. Install Node.js, then open a new terminal.'
} else {
    try {
        $nodeText = (& $nodePath --version 2>&1 | Select-Object -First 1).ToString().Trim()
        $nodeVersion = [version](($nodeText.TrimStart('v') -split '-')[0])
        if ($nodeVersion -lt [version]'20.11.0') {
            Add-Failure ("Node.js 20.11 or newer is required; found {0} at {1}." -f $nodeText, $nodePath)
        } else {
            Write-Check 'Node.js' ("{0} ({1})" -f $nodeText, $nodePath)
        }
    } catch {
        Add-Failure ("Unable to run node.exe at {0}: {1}" -f $nodePath, $_.Exception.Message)
    }
}

$npmPath = Find-CommandPath 'npm.cmd'
if ([string]::IsNullOrWhiteSpace($npmPath)) {
    Add-Warning 'npm.cmd is not on PATH. It is needed to install or repair excalidraw-edit.'
} else {
    Write-Check 'npm' $npmPath
}

$editCommand = Find-CommandPath 'excalidraw-edit.cmd'
if ([string]::IsNullOrWhiteSpace($editCommand)) {
    Add-Failure 'excalidraw-edit.cmd is not on PATH. Run: npm install --global excalidraw-edit@0.1.1'
} elseif ([string]::IsNullOrWhiteSpace($nodePath)) {
    Add-Failure 'Cannot inspect excalidraw-edit without node.exe.'
} else {
    $globalPrefix = Split-Path -Parent $editCommand
    $packageRoot = Join-Path $globalPrefix 'node_modules\excalidraw-edit'
    $cliPath = Join-Path $packageRoot 'src\cli.js'
    $publicDir = Join-Path $packageRoot 'src\public'
    try {
        if (-not (Test-Path -LiteralPath $cliPath)) {
            Add-Failure ("excalidraw-edit CLI is missing: {0}" -f $cliPath)
        } else {
            $editVersion = (& $nodePath $cliPath --version 2>&1 | Select-Object -First 1).ToString().Trim()
            if ($editVersion -ne '0.1.1') {
                Add-Failure ("This release requires excalidraw-edit 0.1.1; found {0}. Run: npm install --global excalidraw-edit@0.1.1" -f $editVersion)
            } else {
                Write-Check 'excalidraw-edit' ("{0} ({1})" -f $editVersion, $cliPath)
            }
        }
        foreach ($asset in @('index.html', 'assets\main.js')) {
            $assetPath = Join-Path $publicDir $asset
            if (-not (Test-Path -LiteralPath $assetPath)) {
                Add-Failure ("Required excalidraw-edit asset is missing: {0}" -f $assetPath)
            }
        }
        if (Test-Path -LiteralPath $publicDir) { Write-Check 'Browser assets' $publicDir }
    } catch {
        Add-Failure ("Unable to inspect excalidraw-edit: {0}" -f $_.Exception.Message)
    }
}

if (-not $RuntimeOnly) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) {
        Add-Failure ("The .NET Framework C# compiler is missing: {0}" -f $compiler)
    } else {
        Write-Check '.NET Framework compiler' $compiler
    }
}

Write-Host ''
if ($script:Failures.Count -gt 0) {
    Write-Host ("Environment check failed with {0} problem(s)." -f $script:Failures.Count) -ForegroundColor Red
    exit 1
}

if ($script:Warnings.Count -gt 0) {
    Write-Host ("Environment is usable with {0} warning(s)." -f $script:Warnings.Count) -ForegroundColor Yellow
} else {
    Write-Host 'Environment is ready.' -ForegroundColor Green
}
exit 0
