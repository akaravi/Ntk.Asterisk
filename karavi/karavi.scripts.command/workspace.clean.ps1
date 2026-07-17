#Requires -Version 5.1
<#
.SYNOPSIS
  Remove build/publish artifacts per clean-manifest.json.
#>
[CmdletBinding()]
param(
    [switch]$WhatIf,
    [switch]$Deep
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\karavi.scripts.tools\workspace.paths.ps1')

$paths = Get-KaraviPaths
$manifestPath = $paths.CleanManifest
if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw "clean-manifest not found: $manifestPath"
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$repo = $paths.RepoRoot
$toRemove = @()

foreach ($rel in $manifest.pathsRelativeToRepoRoot) {
    $toRemove += Join-Path $repo $rel
}

if ($Deep -and $manifest.deepExtra) {
    foreach ($pattern in $manifest.deepExtra) {
        $toRemove += Get-ChildItem -Path $repo -Recurse -Directory -Filter ($pattern -replace '^\*\*/', '') -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty FullName
    }
}

foreach ($target in $toRemove | Select-Object -Unique) {
    if (-not (Test-Path -LiteralPath $target)) { continue }
    $preserve = $false
    foreach ($keep in $manifest.preserve) {
        if ($target -like (Join-Path $repo ($keep -replace '/', '\*'))) { $preserve = $true; break }
    }
    if ($preserve) { continue }

    if ($WhatIf) {
        Write-Host "[WhatIf] Remove: $target"
    }
    else {
        Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "Removed: $target"
    }
}

Write-Host 'clean complete.'
