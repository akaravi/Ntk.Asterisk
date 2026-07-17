#Requires -Version 5.1
<#
.SYNOPSIS
  Merge karavi.plans.cursor and karavi.plans into karavi.plans.prompt (no duplicates); update references; remove legacy folders.
.EXAMPLE
  .\workspace.migrate-plans-consolidate.ps1 -RepoRoot D:\SourceKaravi\GitHub\NTK.ApiLayer
.EXAMPLE
  .\workspace.migrate-plans-consolidate.ps1 -GitHubRoot D:\SourceKaravi\GitHub
#>
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$RepoRoot,
    [string]$GitHubRoot = 'D:\SourceKaravi\GitHub',
    [switch]$AllRepos
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-FileSha256 {
    param([string]$Path)
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Merge-TreeInto {
    param(
        [string]$SourceDir,
        [string]$DestDir,
        [ref]$Stats
    )

    if (-not (Test-Path -LiteralPath $SourceDir)) { return }

    $null = New-Item -ItemType Directory -Force -Path $DestDir

    Get-ChildItem -LiteralPath $SourceDir -Force | ForEach-Object {
        if ($_.Name -eq '__pycache__') { return }

        $target = Join-Path $DestDir $_.Name
        if ($_.PSIsContainer) {
            Merge-TreeInto -SourceDir $_.FullName -DestDir $target -Stats $Stats
            return
        }

        if (Test-Path -LiteralPath $target) {
            $same = (Get-FileSha256 $_.FullName) -eq (Get-FileSha256 $target)
            if ($same) {
                $Stats.Value.SkippedDuplicate++
                return
            }
            if ($_.LastWriteTimeUtc -gt (Get-Item -LiteralPath $target).LastWriteTimeUtc) {
                Copy-Item -LiteralPath $_.FullName -Destination $target -Force
                $Stats.Value.Overwritten++
                return
            }
            $Stats.Value.KeptExisting++
            return
        }

        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
        $Stats.Value.Copied++
    }
}

function Remove-TreeIfExists {
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Update-TextFilePaths {
    param([string]$FilePath)

    $raw = [System.IO.File]::ReadAllText($FilePath)
    if ($raw -notmatch 'karavi\.plans(\.cursor|/)') { return $false }

    $updated = $raw
    $pairs = @(
        @('karavi/karavi.plans.cursor/cursor/', 'karavi/karavi.plans.prompt/cursor/'),
        @('karavi\karavi.plans.cursor\cursor\', 'karavi\karavi.plans.prompt\cursor\'),
        @('karavi\karavi.plans.cursor\cursor', 'karavi\karavi.plans.prompt\cursor'),
        @('karavi/karavi.plans/cursor/', 'karavi/karavi.plans.prompt/cursor/'),
        @('karavi\karavi.plans\cursor\', 'karavi\karavi.plans.prompt\cursor\'),
        @('karavi\karavi.plans\cursor', 'karavi\karavi.plans.prompt\cursor'),
        @('karavi.plans.cursor/cursor/', 'karavi.plans.prompt/cursor/'),
        @('karavi.plans.cursor\cursor\', 'karavi.plans.prompt\cursor\'),
        @('karavi.plans.cursor\cursor', 'karavi.plans.prompt\cursor'),
        @('karavi.plans/cursor/', 'karavi.plans.prompt/cursor/'),
        @('karavi.plans\cursor\', 'karavi.plans.prompt\cursor\'),
        @('karavi.plans\cursor', 'karavi.plans.prompt\cursor'),
        @('karavi/karavi.plans.cursor/', 'karavi/karavi.plans.prompt/cursor/'),
        @('karavi\karavi.plans.cursor\', 'karavi\karavi.plans.prompt\cursor\'),
        @('karavi\karavi.plans.cursor', 'karavi\karavi.plans.prompt\cursor'),
        @('karavi.plans.cursor', 'karavi.plans.prompt/cursor')
    )

    foreach ($pair in $pairs) {
        $updated = $updated.Replace($pair[0], $pair[1])
    }

    # Fix over-replacement: karavi.plans.prompt/cursor.prompt -> karavi.plans.prompt
    $updated = $updated.Replace('karavi.plans.prompt/cursor.prompt', 'karavi.plans.prompt')
    $updated = $updated.Replace('karavi/karavi.plans.prompt/cursor.prompt', 'karavi/karavi.plans.prompt')
    $updated = $updated.Replace('karavi\karavi.plans.prompt\cursor.prompt', 'karavi\karavi.plans.prompt')

    if ($updated -eq $raw) { return $false }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($FilePath, $updated, $utf8)
    return $true
}

function Update-WorkspacePathsScript {
    param([string]$KaraviRoot)

    $pathsFile = Join-Path $KaraviRoot 'karavi.scripts.tools\workspace.paths.ps1'
    if (-not (Test-Path -LiteralPath $pathsFile)) { return $false }

    $raw = [System.IO.File]::ReadAllText($pathsFile)
    $updated = $raw

    $updated = $updated -replace "Join-Path\s+\`$k\s+'karavi\.plans\.cursor'", "Join-Path `$k 'karavi.plans.prompt/cursor'"
    $updated = $updated -replace "Join-Path\s+\`$karavi\s+'karavi\.plans\.cursor\\cursor'", "Join-Path `$karavi 'karavi.plans.prompt/cursor'"
    $updated = $updated -replace "Join-Path\s+\`$karavi\s+'karavi\.plans\.cursor'", "Join-Path `$karavi 'karavi.plans.prompt/cursor'"
    $updated = $updated -replace "Join-Path\s+\`$karaviRoot\s+'karavi\.plans\.cursor\\cursor'", "Join-Path `$karaviRoot 'karavi.plans.prompt/cursor'"
    $updated = $updated -replace "Join-Path\s+\`$karaviRoot\s+'karavi\.plans\.cursor'", "Join-Path `$karaviRoot 'karavi.plans.prompt/cursor'"

    if ($updated -match "PlansMain\s*=") {
        $updated = $updated -replace "(?m)^\s*PlansMain\s*=.*\r?\n", ''
    }

    if ($updated -notmatch 'PlansPrompt\s*=') {
        $updated = $updated -replace "(?m)(^\s*PlansCursor\s*=)", "`$1`r`n        PlansPrompt        = Join-Path `$k 'karavi.plans.prompt'"
    }

    if ($updated -eq $raw) {
        return (Update-TextFilePaths -FilePath $pathsFile)
    }

    $utf8 = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($pathsFile, $updated, $utf8)
    return $true
}

function Invoke-PlansConsolidate {
    param([string]$Root)

    $karavi = Join-Path $Root 'karavi'
    if (-not (Test-Path -LiteralPath $karavi)) {
        Write-Warning "Skip (no karavi/): $Root"
        return
    }

    $legacyCursor = Join-Path $karavi 'karavi.plans.cursor'
    $legacyPlans = Join-Path $karavi 'karavi.plans'
    $promptRoot = Join-Path $karavi 'karavi.plans.prompt'
    $destCursor = Join-Path $promptRoot 'cursor'

    if (-not (Test-Path -LiteralPath $legacyCursor) -and -not (Test-Path -LiteralPath $legacyPlans)) {
        Write-Host "Skip (already consolidated): $Root"
        return
    }

    if ($PSCmdlet.ShouldProcess($Root, 'consolidate karavi plans folders')) {
        $null = New-Item -ItemType Directory -Force -Path $destCursor
        $null = New-Item -ItemType Directory -Force -Path (Join-Path $promptRoot 'claude') -ErrorAction SilentlyContinue
        $null = New-Item -ItemType Directory -Force -Path (Join-Path $promptRoot 'other') -ErrorAction SilentlyContinue

        $stats = @{ Copied = 0; SkippedDuplicate = 0; Overwritten = 0; KeptExisting = 0 }
        $statsRef = [ref]$stats

        $sourceDirs = @(
            (Join-Path $legacyPlans 'cursor'),
            (Join-Path $legacyCursor 'cursor')
        )
        foreach ($src in $sourceDirs) {
            if (-not (Test-Path -LiteralPath $src)) { continue }
            Merge-TreeInto -SourceDir $src -DestDir $destCursor -Stats $statsRef
        }

        foreach ($legacyRoot in @($legacyPlans, $legacyCursor)) {
            if (-not (Test-Path -LiteralPath $legacyRoot)) { continue }
            Get-ChildItem -LiteralPath $legacyRoot -Force | ForEach-Object {
                if ($_.Name -eq 'cursor' -or $_.Name -eq '__pycache__') { return }
                if ($_.PSIsContainer) {
                    Merge-TreeInto -SourceDir $_.FullName -DestDir (Join-Path $destCursor $_.Name) -Stats $statsRef
                }
                else {
                    $target = Join-Path $destCursor $_.Name
                    if (Test-Path -LiteralPath $target) {
                        $same = (Get-FileSha256 $_.FullName) -eq (Get-FileSha256 $target)
                        if ($same) { $stats.SkippedDuplicate++; return }
                        if ($_.LastWriteTimeUtc -gt (Get-Item -LiteralPath $target).LastWriteTimeUtc) {
                            Copy-Item -LiteralPath $_.FullName -Destination $target -Force
                            $stats.Overwritten++
                        }
                        else { $stats.KeptExisting++ }
                    }
                    else {
                        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
                        $stats.Copied++
                    }
                }
            }
        }

        Remove-TreeIfExists -Path $legacyCursor
        Remove-TreeIfExists -Path $legacyPlans

        $exts = @('*.ps1', '*.md', '*.mdc', '*.json', '*.txt', '*.py')
        $skipDirs = @('node_modules', 'bin', 'obj', '.git', '__pycache__', 'dist', 'build')
        $textUpdated = 0

        foreach ($ext in $exts) {
            Get-ChildItem -LiteralPath $Root -Filter $ext -Recurse -File -ErrorAction SilentlyContinue |
                Where-Object {
                    $p = $_.FullName
                    -not ($skipDirs | Where-Object { $p -match [regex]::Escape([IO.Path]::DirectorySeparatorChar + $_ + [IO.Path]::DirectorySeparatorChar) })
                } |
                ForEach-Object {
                    if (Update-TextFilePaths -FilePath $_.FullName) { $textUpdated++ }
                }
        }

        $pathsUpdated = Update-WorkspacePathsScript -KaraviRoot $karavi

        Write-Host "OK: $Root | copied=$($stats.Copied) dup=$($stats.SkippedDuplicate) overwrite=$($stats.Overwritten) kept=$($stats.KeptExisting) textFiles=$textUpdated paths=$pathsUpdated"
    }
}

if ($AllRepos -or (-not $RepoRoot)) {
    if (-not (Test-Path -LiteralPath $GitHubRoot)) {
        throw "GitHub root not found: $GitHubRoot"
    }
    Get-ChildItem -LiteralPath $GitHubRoot -Directory | ForEach-Object {
        Invoke-PlansConsolidate -Root $_.FullName
    }
}
else {
    Invoke-PlansConsolidate -Root $RepoRoot
}
