$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$programPath = Join-Path $root 'src\Program.cs'
$localizationPath = Join-Path $root 'src\Localization.cs'
$assertions = 0

Add-Type -AssemblyName System.Web.Extensions

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Assertion failed: $Message" }
    $script:assertions++
}

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) {
        throw "Assertion failed: $Message (expected '$Expected', got '$Actual')"
    }
    $script:assertions++
}

function Compile-ManagerAssembly([string]$ProgramSource, [string]$LocalizationSource) {
    $provider = New-Object Microsoft.CSharp.CSharpCodeProvider
    try {
        $parameters = New-Object System.CodeDom.Compiler.CompilerParameters
        $parameters.GenerateExecutable = $false
        $parameters.GenerateInMemory = $true
        $parameters.IncludeDebugInformation = $false
        $parameters.CompilerOptions = '/optimize+ /codepage:65001'
        foreach ($reference in @(
            'System.dll',
            'System.Core.dll',
            'System.Drawing.dll',
            'System.Management.dll',
            'System.Web.Extensions.dll',
            'System.Windows.Forms.dll'
        )) {
            [void]$parameters.ReferencedAssemblies.Add($reference)
        }

        [string[]]$sources = @($LocalizationSource, $ProgramSource)
        $result = $provider.CompileAssemblyFromSource($parameters, [string[]]$sources)
        if ($result.Errors.HasErrors) {
            $details = ($result.Errors | Where-Object { -not $_.IsWarning } | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
            throw "Current manager source does not compile in memory:$([Environment]::NewLine)$details"
        }
        return $result.CompiledAssembly
    }
    finally {
        $provider.Dispose()
    }
}

$programSource = Get-Content -Raw -Encoding UTF8 -LiteralPath $programPath
$localizationSource = Get-Content -Raw -Encoding UTF8 -LiteralPath $localizationPath
$assembly = Compile-ManagerAssembly $programSource $localizationSource

$settingsType = $assembly.GetType('ExcalidrawManager.AppSettings', $true)
$groupType = $assembly.GetType('ExcalidrawManager.WorkspaceGroup', $true)
$formulaServiceType = $assembly.GetType('ExcalidrawManager.FormulaEditorService', $true)
$localizationType = $assembly.GetType('ExcalidrawManager.Localization', $true)
$settings = [Activator]::CreateInstance($settingsType)

# Workspace paths are Windows case-insensitive. Aliases and virtual-group
# assignments must therefore use the same comparer before and after a load.
$workspacePath = 'C:\Research\Excalidraw'
$workspacePathVariant = 'c:\research\EXCALIDRAW'
$settings.Aliases.Add($workspacePath, 'Research boards')
Assert-True ($settings.Aliases.ContainsKey($workspacePathVariant)) 'workspace aliases are case-insensitive in a new settings model'
Assert-Equal 'Research boards' $settings.Aliases[$workspacePathVariant] 'workspace alias lookup accepts a differently-cased path'
Assert-True ($settings.Aliases.Comparer.Equals($workspacePath, $workspacePathVariant)) 'workspace alias dictionary exposes an ordinal-ignore-case comparer'
Assert-True ($programSource.Contains('result.Aliases = CopyCaseInsensitive(result.Aliases)')) 'loaded alias dictionaries are rebuilt with an ordinal-ignore-case comparer'
Assert-True ($programSource.Contains('result.WorkspaceRootGroups = CopyCaseInsensitive(result.WorkspaceRootGroups)')) 'loaded workspace-group maps are rebuilt with an ordinal-ignore-case comparer'
Assert-True ($programSource.Contains('new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)')) 'loaded path maps use an ordinal-ignore-case destination dictionary'

$parent = [Activator]::CreateInstance($groupType)
$parent.Id = 'group-parent'
$parent.Name = 'Research'
$parent.ParentId = $null
$child = [Activator]::CreateInstance($groupType)
$child.Id = 'group-child'
$child.Name = 'Papers'
$child.ParentId = $parent.Id
[void]$settings.WorkspaceGroups.Add($parent)
[void]$settings.WorkspaceGroups.Add($child)
$settings.WorkspaceRootGroups.Add($workspacePath, $child.Id)
Assert-True ($settings.WorkspaceRootGroups.ContainsKey($workspacePathVariant)) 'workspace-root group assignments are case-insensitive'

$serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$json = $serializer.Serialize($settings)
Assert-True ($json.Contains('"WorkspaceGroups"')) 'settings JSON includes virtual workspace groups'
Assert-True ($json.Contains('"WorkspaceRootGroups"')) 'settings JSON includes root-to-group assignments'
$roundTrip = $serializer.Deserialize($json, $settingsType)
Assert-Equal 2 $roundTrip.WorkspaceGroups.Count 'virtual workspace groups survive JSON round-trip'
$roundTripChild = @($roundTrip.WorkspaceGroups | Where-Object { $_.Id -eq 'group-child' } | Select-Object -First 1)
Assert-Equal 1 $roundTripChild.Count 'nested virtual group survives JSON round-trip'
Assert-Equal 'group-parent' $roundTripChild[0].ParentId 'virtual group parent relationship survives JSON round-trip'
Assert-Equal 'group-child' $roundTrip.WorkspaceRootGroups[$workspacePath] 'workspace root assignment survives JSON round-trip'
Assert-Equal 'Research boards' $roundTrip.Aliases[$workspacePath] 'workspace alias survives JSON round-trip'

$settingsStoreType = $assembly.GetType('ExcalidrawManager.SettingsStore', $true)
$normalizeMethod = $settingsStoreType.GetMethod('NormalizeWorkspaceGroups', [Reflection.BindingFlags]'NonPublic, Static')
$corruptSettings = [Activator]::CreateInstance($settingsType)
$cycleA = [Activator]::CreateInstance($groupType)
$cycleA.Id = 'cycle-a'; $cycleA.Name = 'A'; $cycleA.ParentId = 'cycle-b'
$cycleB = [Activator]::CreateInstance($groupType)
$cycleB.Id = 'cycle-b'; $cycleB.Name = 'B'; $cycleB.ParentId = 'cycle-a'
$cycleChild = [Activator]::CreateInstance($groupType)
$cycleChild.Id = 'cycle-child'; $cycleChild.Name = 'Cycle child'; $cycleChild.ParentId = 'cycle-a'
$orphan = [Activator]::CreateInstance($groupType)
$orphan.Id = 'orphan'; $orphan.Name = 'Orphan'; $orphan.ParentId = 'missing-parent'
$orphanChainChild = [Activator]::CreateInstance($groupType)
$orphanChainChild.Id = 'orphan-chain-child'; $orphanChainChild.Name = 'Child'; $orphanChainChild.ParentId = 'orphan-chain-parent'
$orphanChainParent = [Activator]::CreateInstance($groupType)
$orphanChainParent.Id = 'orphan-chain-parent'; $orphanChainParent.Name = 'Parent'; $orphanChainParent.ParentId = 'missing-grandparent'
# Put a legitimate descendant first so cycle repair cannot simply detach the
# traversal's starting node.
[void]$corruptSettings.WorkspaceGroups.Add($cycleChild)
[void]$corruptSettings.WorkspaceGroups.Add($cycleA)
[void]$corruptSettings.WorkspaceGroups.Add($cycleB)
[void]$corruptSettings.WorkspaceGroups.Add($orphan)
# Deliberately put the child before its damaged parent to catch ordering bugs.
[void]$corruptSettings.WorkspaceGroups.Add($orphanChainChild)
[void]$corruptSettings.WorkspaceGroups.Add($orphanChainParent)
$corruptSettings.WorkspaceRootGroups['C:\Valid'] = 'cycle-a'
$corruptSettings.WorkspaceRootGroups['C:\Invalid'] = 'missing-parent'
[void]$normalizeMethod.Invoke($null, [object[]]@($corruptSettings))
Assert-True (-not ($cycleA.ParentId -eq 'cycle-b' -and $cycleB.ParentId -eq 'cycle-a')) 'settings normalization breaks virtual-group cycles'
Assert-Equal 'cycle-a' $cycleChild.ParentId 'settings normalization preserves a child that points into a repaired cycle'
Assert-True ([string]::IsNullOrEmpty($orphan.ParentId)) 'settings normalization promotes orphan groups to the top level'
Assert-True ([string]::IsNullOrEmpty($orphanChainParent.ParentId)) 'settings normalization repairs a missing grandparent at the damaged parent'
Assert-Equal 'orphan-chain-parent' $orphanChainChild.ParentId 'settings normalization preserves a valid child-to-parent relationship'
Assert-True (-not $corruptSettings.WorkspaceRootGroups.ContainsKey('C:\Invalid')) 'settings normalization removes assignments to missing groups'
Assert-Equal 'cycle-a' $corruptSettings.WorkspaceRootGroups['c:\valid'] 'valid assignments survive normalization with case-insensitive lookup'

# The application header may keep the BrandMark type for compatibility, but it
# must not instantiate the removed purple in-content logo.
Assert-True (-not [regex]::IsMatch($programSource, '\bnew\s+BrandMark\b')) 'main-window source no longer instantiates the purple BrandMark'

# The manager exposes a native palette entry. It launches Chromium app mode and
# promotes the resulting native HWND to the Windows topmost band.
Assert-True ($null -ne $formulaServiceType.GetMethod('OpenNativePalette')) 'formula service exposes the native palette entry point'
Assert-True ($programSource.Contains('NativeFormulaWindow.Open(')) 'formula service delegates palette opening to the native window layer'
Assert-True ($programSource.Contains('compact=1&native=1')) 'native palette URL uses the compact standalone interface'
Assert-True ($programSource.Contains('"--app="')) 'native palette launches the browser in app mode without an address bar'
Assert-True ($programSource.Contains('HwndTopmost = new IntPtr(-1)')) 'native palette defines the Windows topmost insertion target'
Assert-True ($programSource.Contains('SetWindowPos(handle, HwndTopmost')) 'native palette promotes the browser app window to topmost'
Assert-True ($programSource.Contains('WindowMarkerName = "ExcalidrawManager.FormulaPalette"')) 'native palette marks the exact HWND it owns'
Assert-True ($programSource.Contains('GetProp(_activeWindow, WindowMarkerName) == _activeMarker')) 'native palette validates its HWND marker before reuse or close'
Assert-True (-not $programSource.Contains('fallbackHandle')) 'native palette never falls back to an arbitrary new Chromium window'
Assert-True ($programSource.Contains('MakeUiButton(T("Formula Palette")')) 'main action bar exposes a formula palette button'
Assert-True ($programSource.Contains('FormulaEditorService.OpenNativePalette(editorUrl)')) 'main-window palette action uses the native entry point'

$rejectedRemoteUrl = $false
try {
    [void]$formulaServiceType.GetMethod('OpenNativePalette').Invoke($null, [object[]]@('https://example.com/formula'))
}
catch {
    $rejectedRemoteUrl = $true
}
Assert-True $rejectedRemoteUrl 'native palette rejects a non-loopback editor URL before launching a browser'

# Deleting a virtual group changes settings topology only. The confirmation copy
# promises this explicitly, and the method must not contain filesystem mutation.
$deleteMessage = "Delete virtual group '{0}'? Its workspaces and subgroups will move up one level. No local files will be changed."
$deleteStart = $programSource.IndexOf('private void DeleteWorkspaceGroup(string groupId)', [StringComparison]::Ordinal)
$deleteEnd = $programSource.IndexOf('private void MoveRootToGroup(string root, string groupId)', $deleteStart, [StringComparison]::Ordinal)
Assert-True ($deleteStart -ge 0 -and $deleteEnd -gt $deleteStart) 'virtual-group delete method can be isolated for contract checks'
$deleteBody = $programSource.Substring($deleteStart, $deleteEnd - $deleteStart)
Assert-True ($deleteBody.Contains($deleteMessage)) 'virtual-group confirmation says that no local files are changed'
Assert-True ($deleteBody.Contains('_settings.WorkspaceGroups.Remove(group)')) 'virtual-group deletion removes only the settings group record'
Assert-True ($deleteBody.Contains('_settings.WorkspaceRootGroups')) 'virtual-group deletion reparents workspace mappings in settings'
Assert-True (-not [regex]::IsMatch($deleteBody, '\b(?:File|Directory)\s*\.\s*(?:Delete|Move|CreateDirectory)\s*\(')) 'virtual-group deletion contains no local filesystem mutation call'
Assert-True ($localizationSource.Contains($deleteMessage)) 'virtual-group no-files-changed confirmation is localized'

[void]$localizationType.GetMethod('Configure').Invoke($null, [object[]]@('zh-CN'))
$localizedDelete = [string]$localizationType.GetMethod('F').Invoke($null, [object[]]@($deleteMessage, [object[]]@('Research')))
$noLocalFilesPhrase = -join (@(0x4e0d, 0x4f1a, 0x66f4, 0x6539, 0x4efb, 0x4f55, 0x672c, 0x5730, 0x6587, 0x4ef6) | ForEach-Object { [char]$_ })
Assert-True ($localizedDelete.Contains($noLocalFilesPhrase)) 'Chinese delete confirmation also promises not to change local files'

Write-Host "PASS workspace groups, aliases, native palette, and main-window contracts ($assertions assertions)."
