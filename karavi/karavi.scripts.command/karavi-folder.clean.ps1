#Requires -Version 5.1
<#
.SYNOPSIS
  Section 2 of the karavi-folder skill: remove temporary/interim data.
.DESCRIPTION
  Logs/status-level (default) clears temp junk in karavi.temp.logs\ and
  karavi.temp.status\.
  Deep (-Deep) also clears karavi.temp.deploy\, karavi.temp.build\, repo-level
  output, and per-stack caches. Never touches source, config, history, or
  READMEs. Only the karavi.temp.* folders are deletable.
.PARAMETER Deep
  Also clear karavi.temp.deploy, karavi.temp.build, repo-level output, and caches.
.PARAMETER WhatIf
  Preview only; delete nothing.
.PARAMETER RepoRoot
  Target repo root. Default: walk up from this script until a folder containing
  'karavi' is found.
.EXAMPLE
  .\karavi-folder.clean.ps1                 # logs-level
  .\karavi-folder.clean.ps1 -Deep           # deep cleanup
  .\karavi-folder.clean.ps1 -WhatIf         # dry run
  .\karavi-folder.clean.ps1 -RepoRoot D:\X\Y
#>
[CmdletBinding()]
param(
    [switch]$Deep,
    [switch]$WhatIf,
    [string]$RepoRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Resolve-RepoRoot {
    param([string]$Start = $PSScriptRoot)
    $dir = $Start
    while ($dir) {
        if (Test-Path -LiteralPath (Join-Path $dir 'karavi')) {
            return $dir
        }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    throw "Repo root (folder containing karavi/) not found from: $Start"
}

if (-not $RepoRoot) {
    $RepoRoot = Resolve-RepoRoot
}
if (-not (Test-Path -LiteralPath $RepoRoot)) {
    throw "RepoRoot not found: $RepoRoot"
}

$repo = $RepoRoot
$k = Join-Path $repo 'karavi'

# --- Preserve whitelist: these are NEVER removed ------------------------------
$preservePatterns = @(
    '*karavi.history*', '*karavi.deploy.config*', '*karavi.plans.prompt*',
    '*karavi.scripts.command*', '*karavi.scripts.tools*', '*karavi.assets*',
    '*karavi.mockup*', '*karavi.doc*', '*karavi.BusinessModel.Doc*',
    '*karavi.Customer.doc*', '*karavi.OnlineContent*', '*karavi.SociaMediaContent*',
    '*.gitkeep', '*README.md'
)

function Test-Preserved {
    param([string]$Path)
    foreach ($p in $preservePatterns) {
        if ($Path -like $p -or $Path -like "*\$p") { return $true }
    }
    return $false
}

# --- Default: temp junk in karavi.temp.logs\ and karavi.temp.status\ ----------
$logsDir = Join-Path $k 'karavi.temp.logs'
$statusDir = Join-Path $k 'karavi.temp.status'
foreach ($dir in @($logsDir, $statusDir)) {
    if (Test-Path -LiteralPath $dir) {
        $logPatterns = @(
            '*.out.txt', '*.err.txt', '_tmp-*.ps1', '_tmp-*.py', '_fix-*.py',
            '*launch.ps1', '*loop.ps1', '*run.ps1', '*.pid', '*.token.txt',
            '.browser-check-state.json', '*-state.json', '*.http.json', '*.json',
            '*.html'
        )
        foreach ($pat in $logPatterns) {
            if ($WhatIf -or $Deep) {
                Get-ChildItem -LiteralPath $dir -Filter $pat -File -ErrorAction SilentlyContinue |
                    ForEach-Object { if (-not (Test-Preserved $_.FullName)) {
                        if ($WhatIf) { Write-Host "[WhatIf] Remove: $($_.FullName)" }
                        else { Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue; Write-Host "Removed: $($_.FullName)" } } }
            }
            else {
                Get-ChildItem -LiteralPath $dir -Filter $pat -File -ErrorAction SilentlyContinue |
                    ForEach-Object { if (-not (Test-Preserved $_.FullName)) {
                        Remove-Item -LiteralPath $_.FullName -Force -ErrorAction SilentlyContinue } }
            }
        }
    }
}

# --- Deep: build/deploy temp output, repo-level output, and caches ------------
if ($Deep) {
    $deepDirs = @(
        (Join-Path $k 'karavi.temp.build'),
        (Join-Path $k 'karavi.temp.deploy')
    )
    foreach ($target in $deepDirs) {
        if (-not (Test-Path -LiteralPath $target)) { continue }
        if (-not $target.StartsWith((Join-Path $repo ''), [StringComparison]::OrdinalIgnoreCase)) { continue }
        if (Test-Preserved $target) { Write-Host "Preserved: $target"; continue }
        # Clear build/publish output but keep the folder skeleton + .gitkeep
        if ($WhatIf) { Write-Host "[WhatIf] Clear dir: $target" }
        else {
            Get-ChildItem -LiteralPath $target -Force -ErrorAction SilentlyContinue |
                Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
            if (-not (Test-Path -LiteralPath (Join-Path $target '.gitkeep'))) {
                New-Item -ItemType File -Path (Join-Path $target '.gitkeep') -Force | Out-Null
            }
            Write-Host "Cleared: $target"
        }
    }

    $repoRoots = @(
        (Join-Path $repo 'publish'),
        (Join-Path $repo 'artifacts'),
        (Join-Path $repo '.run-logs'),
        (Join-Path $repo 'LastRunInfo.html'),
        (Join-Path $repo 'Deploy_Summary.html')
    )
    foreach ($target in $repoRoots) {
        if (-not (Test-Path -LiteralPath $target)) { continue }
        if (-not $target.StartsWith((Join-Path $repo ''), [StringComparison]::OrdinalIgnoreCase)) { continue }
        if (Test-Preserved $target) { Write-Host "Preserved: $target"; continue }
        if ($WhatIf) { Write-Host "[WhatIf] Remove: $target" }
        else { Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "Removed: $target" }
    }

    # Per-stack caches
    $cachePatterns = @('bin', 'obj', '.dart_tool', '.next', 'dist', 'out', '.cache')
    foreach ($name in $cachePatterns) {
        Get-ChildItem -Path $repo -Recurse -Directory -Filter $name -ErrorAction SilentlyContinue |
            ForEach-Object {
                if (-not (Test-Preserved $_.FullName)) {
                    if ($WhatIf) { Write-Host "[WhatIf] Remove dir: $($_.FullName)" }
                    else { Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue; Write-Host "Removed dir: $($_.FullName)" }
                }
            }
    }
}

Write-Host 'karavi-folder clean completed.'