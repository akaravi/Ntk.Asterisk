#Requires -Version 5.1
<#
.SYNOPSIS
  Verify karavi folder structure exists (starter-kit contract).
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'workspace.paths.ps1')

$required = @(
    'PlansCursor', 'PlansPrompt', 'Doc', 'History', 'Logs', 'Status',
    'DeployConfig', 'DeployFiles', 'BuildConfig', 'BuildFiles',
    'PublishConfig', 'PublishFiles', 'Scripts', 'ScriptsCommand', 'ScriptsTools'
)

$paths = Get-KaraviPaths
$missing = @()
foreach ($key in $required) {
    $path = $paths[$key]
    if (-not (Test-Path -LiteralPath $path)) {
        $missing += $path
    }
}

$configFiles = @(
    $paths.CleanManifest,
    $paths.VersionManifest,
    $paths.LocalDevPorts,
    $paths.ProductionHosts
)
foreach ($file in $configFiles) {
    if (-not (Test-Path -LiteralPath $file)) {
        $missing += $file
    }
}

if ($missing.Count -gt 0) {
    Write-Error ("karavi structure incomplete:`n" + ($missing -join "`n"))
}

$ports = Get-Content -LiteralPath $paths.LocalDevPorts -Raw -Encoding UTF8 | ConvertFrom-Json
if ($ports.productId -eq 'CHANGE_ME') {
    Write-Warning 'local-dev-ports.json: set productId (still CHANGE_ME).'
}

$legacyFolders = @(
    (Join-Path $paths.KaraviRoot 'karavi.plans.cursor'),
    (Join-Path $paths.KaraviRoot 'karavi.plans')
)
foreach ($legacy in $legacyFolders) {
    if (Test-Path -LiteralPath $legacy) {
        Write-Error "Legacy plans folder must be removed (merged into karavi.plans.prompt/cursor): $legacy"
    }
}

$plansCursor = $paths['PlansCursor']
if ($plansCursor -and ($plansCursor -notmatch 'karavi\.plans\.prompt')) {
    Write-Error "PlansCursor must resolve under karavi.plans.prompt/cursor: $plansCursor"
}

Write-Host 'karavi structure OK.'
exit 0
