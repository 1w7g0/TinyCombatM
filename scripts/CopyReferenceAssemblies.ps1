[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GamePath,

    [string]$BepInExPath,

    [string]$Destination = (Join-Path $PSScriptRoot "..\libs")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$gameRoot = [System.IO.Path]::GetFullPath($GamePath)
$bepInExRoot = if ($BepInExPath) {
    [System.IO.Path]::GetFullPath($BepInExPath)
}
else {
    $gameRoot
}
$destinationRoot = [System.IO.Path]::GetFullPath($Destination)

$managedDir = Join-Path $gameRoot "Arena_Data\Managed"
$bepInExCoreDir = Join-Path $bepInExRoot "BepInEx\core"

if (!(Test-Path -LiteralPath $managedDir)) {
    throw "Tiny Combat Arena managed DLL folder not found: $managedDir"
}

if (!(Test-Path -LiteralPath $bepInExCoreDir)) {
    throw "BepInEx core folder not found: $bepInExCoreDir"
}

New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null

$references = @(
    @{ Name = "Assembly-CSharp.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.CoreModule.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.PhysicsModule.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.IMGUIModule.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.InputLegacyModule.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.AudioModule.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.ParticleSystemModule.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.TextRenderingModule.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.AnimationModule.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.AssetBundleModule.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.UnityWebRequestModule.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.UI.dll"; SourceDir = $managedDir },
    @{ Name = "UnityEngine.UIModule.dll"; SourceDir = $managedDir },
    @{ Name = "Unity.TextMeshPro.dll"; SourceDir = $managedDir },
    @{ Name = "UniTask.dll"; SourceDir = $managedDir },
    @{ Name = "ShapesRuntime.dll"; SourceDir = $managedDir },
    @{ Name = "Rewired_Core.dll"; SourceDir = $managedDir },
    @{ Name = "Facepunch.Steamworks.Win64.dll"; SourceDir = $managedDir },
    @{ Name = "BepInEx.dll"; SourceDir = $bepInExCoreDir },
    @{ Name = "0Harmony.dll"; SourceDir = $bepInExCoreDir }
)

$missing = @()

foreach ($reference in $references) {
    $source = Join-Path $reference.SourceDir $reference.Name
    $target = Join-Path $destinationRoot $reference.Name

    if (!(Test-Path -LiteralPath $source)) {
        $missing += $source
        continue
    }

    Copy-Item -LiteralPath $source -Destination $target -Force
    Write-Host "Copied $($reference.Name)"
}

if ($missing.Count -gt 0) {
    Write-Host ""
    Write-Host "Missing reference assemblies:" -ForegroundColor Yellow
    foreach ($path in $missing) {
        Write-Host "  $path"
    }
    exit 1
}

Write-Host ""
Write-Host "Reference assemblies copied to $destinationRoot"
