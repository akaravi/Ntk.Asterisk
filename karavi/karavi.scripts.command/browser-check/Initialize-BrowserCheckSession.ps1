param(
  [Parameter(Mandatory = $true)]
  [string]$ProjectName
)

$ErrorActionPreference = 'Stop'
# PSScriptRoot = karavi/karavi.scripts/browser-check → repo root is ../../..
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$ts = Get-Date -Format 'yyyyMMdd-HHmmss'
$sessionId = "bc-$ProjectName-$ts"

$logsDir = Join-Path $root 'karavi\karavi.logs'
$statusDir = Join-Path $root 'karavi\karavi.status'
$sessionDir = Join-Path $logsDir "browser-check-$ts"

New-Item -ItemType Directory -Force -Path $logsDir, $statusDir, $sessionDir | Out-Null

$state = [ordered]@{
  sessionId       = $sessionId
  productId       = $ProjectName
  createdAt       = (Get-Date).ToUniversalTime().ToString('o')
  sessionDir      = $sessionDir.Replace('\', '/')
  skillsDiscovered = @()
  skillsInvoked   = @()
  stats           = @{
    pass         = 0
    fail         = 0
    pending      = 0
    skipped      = 0
    fixesApplied = 0
  }
  status          = 'initialized'
}

$statePath = Join-Path $logsDir '.browser-check-state.json'
$state | ConvertTo-Json -Depth 8 | Set-Content -Path $statePath -Encoding utf8

$tree = [ordered]@{
  sessionId     = $sessionId
  productId     = $ProjectName
  createdAt     = $state.createdAt
  skillsInvoked = @()
  hosts         = @()
  stats         = $state.stats
}

$treePath = Join-Path $statusDir 'BrowserCheck_Tree.json'
$tree | ConvertTo-Json -Depth 8 | Set-Content -Path $treePath -Encoding utf8

Write-Host "BrowserCheck session initialized: $sessionId"
Write-Host "State: $statePath"
Write-Host "Tree:  $treePath"
Write-Host "Logs:  $sessionDir"
