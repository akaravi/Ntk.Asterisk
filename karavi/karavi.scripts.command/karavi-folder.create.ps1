#Requires -Version 5.1
<#
.SYNOPSIS
  Section 1 of the karavi-folder skill: create the standard karavi/ skeleton (default: Full structure).
.DESCRIPTION
  Creates the canonical karavi/ workspace folders in a repo root.
  Scans and migrates legacy/variant folder structures inside karavi/ by renaming
  and relocating them to standard names without data loss.
  Default structure is Full (18 standard folders).
.PARAMETER RepoRoot
  Target repo root. Default: walk up from this script until a repo root
  (folder containing '.git' or 'karavi') is found.
.PARAMETER Core
  If specified, only scaffolds the 9 core folders instead of the default Full structure.
.PARAMETER Full
  Explicitly enable Full structure (enabled by default).
.PARAMETER NoMigrate
  Skip legacy folder migration/renaming.
.PARAMETER WhatIf
  Preview only; create nothing.
.EXAMPLE
  .\karavi-folder.create.ps1                     # Full structure (default) + migration
  .\karavi-folder.create.ps1 -Core               # Core structure only
  .\karavi-folder.create.ps1 -WhatIf             # Preview
  .\karavi-folder.create.ps1 -RepoRoot D:\X\Y
#>
[CmdletBinding()]
param(
    [string]$RepoRoot,
    [switch]$Core,
    [switch]$Full,
    [switch]$NoMigrate,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# If init script exists in same folder, delegate to init.ps1 for consistent behavior
$initScript = Join-Path $PSScriptRoot 'karavi-folder.init.ps1'
if (Test-Path -LiteralPath $initScript) {
    $params = @{}
    if ($RepoRoot) { $params['RepoRoot'] = $RepoRoot }
    if ($Core) { $params['Core'] = $true }
    if ($NoMigrate) { $params['NoMigrate'] = $true }
    if ($WhatIf) { $params['WhatIf'] = $true }
    & $initScript @params
    return
}

function Resolve-RepoRoot {
    param([string]$Start = $PSScriptRoot)
    $cur = (Resolve-Path $Start).Path
    while ($cur -and (Test-Path -LiteralPath $cur)) {
        if ((Test-Path -LiteralPath (Join-Path $cur '.git')) -or (Test-Path -LiteralPath (Join-Path $cur 'karavi'))) {
            return $cur
        }
        $parent = Split-Path -Parent $cur
        if ($parent -eq $cur -or [string]::IsNullOrEmpty($parent)) { break }
        $cur = $parent
    }
    return (Get-Location).Path
}

if (-not $RepoRoot) {
    $RepoRoot = Resolve-RepoRoot
}
if (-not (Test-Path -LiteralPath $RepoRoot)) {
    throw "RepoRoot not found: $RepoRoot"
}

$repo = (Resolve-Path $RepoRoot).Path
$k = Join-Path $repo 'karavi'

if (-not (Test-Path -LiteralPath $k)) {
    if ($WhatIf) { Write-Host "[WhatIf] Create dir: $k" }
    else { New-Item -ItemType Directory -Force -Path $k | Out-Null }
}

$core = @(
    'karavi.plans.prompt',
    'karavi.history',
    'karavi.deploy.config',
    'karavi.scripts.command',
    'karavi.scripts.tools',
    'karavi.temp.logs',
    'karavi.temp.status',
    'karavi.temp.deploy',
    'karavi.temp.build'
)

$optional = @(
    'karavi.assets/brand',
    'karavi.assets/icons',
    'karavi.assets/screenshots',
    'karavi.assets/templates',
    'karavi.mockup',
    'karavi.doc',
    'karavi.BusinessModel.Doc',
    'karavi.Customer.doc',
    'karavi.OnlineContent/SociaMediaContent',
    'karavi.OnlineContent/WordPressContent',
    'karavi.OnlineContent/LinkedinConetnt'
)

$isFull = (-not $Core)

$foldersToCreate = @($core)
if ($isFull) {
    $foldersToCreate += $optional
}

$created = 0
foreach ($rel in $foldersToCreate) {
    $target = Join-Path $k ($rel -replace '/', [IO.Path]::DirectorySeparatorChar)
    $rootCheck = (Join-Path $repo '') -replace '\\$', ''
    if (-not $target.StartsWith($rootCheck, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refused: $target resolves outside repo root."
    }
    if (-not (Test-Path -LiteralPath $target)) {
        if ($WhatIf) { Write-Host "[WhatIf] Create dir: $target" }
        else {
            New-Item -ItemType Directory -Force -Path $target | Out-Null
            $gitkeepPath = Join-Path $target '.gitkeep'
            if (-not (Test-Path -LiteralPath $gitkeepPath)) {
                New-Item -ItemType File -Path $gitkeepPath -Force | Out-Null
            }
        }
        $created++
    }
}

Write-Host "karavi-folder create: $created folder(s) processed under $k (Scope: $(if ($isFull) { 'Full (Default)' } else { 'Core' }))"
if (-not $WhatIf -and -not (Test-Path -LiteralPath (Join-Path $k '.gitkeep'))) {
    Write-Host 'Next: add the "# --- karavi ---" block to .gitignore (see references/folders.md).'
}
