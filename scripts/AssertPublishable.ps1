[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
Push-Location $root
try {
    $trackedFiles = git ls-files
    $blockedPatterns = @(
        '^libs/.*\.dll$',
        '^Decompiled/',
        '^TinyCombatArena\[Host\]/',
        '^TinyCombatArena\[Client\]/',
        '\.log$',
        '\.tmp$',
        '^bin/',
        '^obj/',
        '/bin/',
        '/obj/'
    )

    $blocked = @()
    foreach ($file in $trackedFiles) {
        $normalized = $file -replace '\\', '/'
        foreach ($pattern in $blockedPatterns) {
            if ($normalized -match $pattern) {
                $blocked += $file
                break
            }
        }
    }

    if ($blocked.Count -gt 0) {
        Write-Host "The repository contains files that should not be published:" -ForegroundColor Red
        foreach ($file in $blocked) {
            Write-Host "  $file"
        }
        exit 1
    }

    Write-Host "Publishability check passed."
}
finally {
    Pop-Location
}
