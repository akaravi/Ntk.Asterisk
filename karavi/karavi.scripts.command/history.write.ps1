#Requires -Version 5.1
<#
.SYNOPSIS
  Append entry to karavi.history/history.YYYY-MM-DD.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Title,

    [string]$Request = '',
    [string]$Changes = '',
    [string]$Verification = '',
    [string]$Date = (Get-Date -Format 'yyyy-MM-dd')
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '..\karavi.scripts.tools\workspace.paths.ps1')

$paths = Get-KaraviPaths
$historyFile = Join-Path $paths.History "history.$Date.md"
$timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm'

$entry = @"

## $Date — $Title
- **زمان:** $timestamp
- **درخواست:** $Request
- **تغییرات:** $Changes
- **تأیید:** $Verification

"@

if (-not (Test-Path -LiteralPath $historyFile)) {
    $header = "# history.$Date`n`n"
    Set-Content -LiteralPath $historyFile -Value ($header + $entry.TrimStart()) -Encoding UTF8
}
else {
    Add-Content -LiteralPath $historyFile -Value $entry -Encoding UTF8
}

Write-Host "History updated: $historyFile"
