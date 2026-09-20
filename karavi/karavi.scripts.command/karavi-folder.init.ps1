#Requires -Version 5.1
<#
.SYNOPSIS
  Initialize and migrate the standard karavi/ workspace tree (default: Full structure).
.DESCRIPTION
  Scaffolds the canonical karavi/ workspace tree in a repo root.
  Scans and migrates existing legacy/variant folder structures inside karavi/
  by renaming and relocating them to the new standard paths without data loss.
  Default structure is Full (20 standard folders/subfolders).
.PARAMETER RepoRoot
  Target repo root. Default: walk up from this script until a folder containing
  '.git' or 'karavi' is found.
.PARAMETER Core
  If specified, only scaffolds the 9 core folders instead of the default Full structure.
.PARAMETER NoMigrate
  Skip legacy folder migration/renaming.
.PARAMETER WhatIf
  Preview only; make no changes.
.EXAMPLE
  .\karavi-folder.init.ps1                   # Full structure + migration (default)
  .\karavi-folder.init.ps1 -Core             # Core structure only
  .\karavi-folder.init.ps1 -WhatIf           # Dry run preview
  .\karavi-folder.init.ps1 -RepoRoot D:\X\Y  # Specific repo
#>
[CmdletBinding()]
param(
    [string]$RepoRoot,
    [switch]$Core,
    [switch]$NoMigrate,
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

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
    if ($WhatIf) {
        Write-Host "[WhatIf] Create root karavi dir: $k"
    } else {
        New-Item -ItemType Directory -Force -Path $k | Out-Null
        Write-Host "Created root karavi folder: $k"
    }
}

# --- Canonical Structure (Full is Default) ------------------------------------
$coreFolders = @(
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

$optionalFolders = @(
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

# --- Legacy Folder Migration Mapping -------------------------------------------
# Maps legacy folder names inside karavi/ to their new canonical relative path
$migrationMap = [ordered]@{
    'plans'                      = 'karavi.plans.prompt'
    'prompt'                     = 'karavi.plans.prompt'
    'prompts'                    = 'karavi.plans.prompt'
    'karavi.plans'               = 'karavi.plans.prompt'
    'karavi.prompt'              = 'karavi.plans.prompt'
    'karavi.prompts'             = 'karavi.plans.prompt'
    'plans.prompt'               = 'karavi.plans.prompt'
    'prompts.plan'               = 'karavi.plans.prompt'
    'history'                    = 'karavi.history'
    'histories'                  = 'karavi.history'
    'karavi.histories'           = 'karavi.history'
    'change-history'             = 'karavi.history'
    'log-history'                = 'karavi.history'
    'deploy'                     = 'karavi.deploy.config'
    'config'                     = 'karavi.deploy.config'
    'deploy.config'              = 'karavi.deploy.config'
    'karavi.deploy'              = 'karavi.deploy.config'
    'karavi.config'              = 'karavi.deploy.config'
    'deploy-config'              = 'karavi.deploy.config'
    'karavi.build.config'        = 'karavi.deploy.config'
    'build.config'               = 'karavi.deploy.config'
    'build-config'               = 'karavi.deploy.config'
    'commands'                   = 'karavi.scripts.command'
    'scripts.command'            = 'karavi.scripts.command'
    'karavi.commands'            = 'karavi.scripts.command'
    'karavi.scripts'             = 'karavi.scripts.command'
    'tools'                      = 'karavi.scripts.tools'
    'scripts.tools'              = 'karavi.scripts.tools'
    'karavi.tools'               = 'karavi.scripts.tools'
    'karavi.tmp'                 = 'karavi.temp.logs'
    'tmp'                        = 'karavi.temp.logs'
    'karavi.temp'                = 'karavi.temp.logs'
    'temp'                       = 'karavi.temp.logs'
    'logs'                       = 'karavi.temp.logs'
    'temp.logs'                  = 'karavi.temp.logs'
    'karavi.logs'                = 'karavi.temp.logs'
    'status'                     = 'karavi.temp.status'
    'temp.status'                = 'karavi.temp.status'
    'karavi.status'              = 'karavi.temp.status'
    'temp.deploy'                = 'karavi.temp.deploy'
    'karavi.deploy.temp'         = 'karavi.temp.deploy'
    'build'                      = 'karavi.temp.build'
    'temp.build'                 = 'karavi.temp.build'
    'karavi.build'               = 'karavi.temp.build'
    'mockup'                     = 'karavi.mockup'
    'mockups'                    = 'karavi.mockup'
    'karavi.mockups'             = 'karavi.mockup'
    'ui-mockups'                 = 'karavi.mockup'
    'doc'                        = 'karavi.doc'
    'docs'                       = 'karavi.doc'
    'karavi.docs'                = 'karavi.doc'
    'documentation'              = 'karavi.doc'
    'karavi.documentation'       = 'karavi.doc'
    'business'                   = 'karavi.BusinessModel.Doc'
    'businessmodel'              = 'karavi.BusinessModel.Doc'
    'karavi.business'            = 'karavi.BusinessModel.Doc'
    'karavi.businessmodel'       = 'karavi.BusinessModel.Doc'
    'BusinessModel.Doc'          = 'karavi.BusinessModel.Doc'
    'customer'                   = 'karavi.Customer.doc'
    'customers'                  = 'karavi.Customer.doc'
    'karavi.customer'            = 'karavi.Customer.doc'
    'Customer.doc'               = 'karavi.Customer.doc'
    'karavi.SociaMediaContent'   = 'karavi.OnlineContent/SociaMediaContent'
    'SociaMediaContent'          = 'karavi.OnlineContent/SociaMediaContent'
    'karavi.SocialMediaContent'  = 'karavi.OnlineContent/SociaMediaContent'
    'SocialMediaContent'         = 'karavi.OnlineContent/SociaMediaContent'
    'social'                     = 'karavi.OnlineContent/SociaMediaContent'
    'socialmedia'                = 'karavi.OnlineContent/SociaMediaContent'
    'karavi.social'              = 'karavi.OnlineContent/SociaMediaContent'
    'karavi.socialmedia'         = 'karavi.OnlineContent/SociaMediaContent'
    'OnlineContent'              = 'karavi.OnlineContent'
    'karavi.OnlineContent'       = 'karavi.OnlineContent'
    'wordpress'                  = 'karavi.OnlineContent/WordPressContent'
    'WordPressContent'           = 'karavi.OnlineContent/WordPressContent'
    'linkedin'                   = 'karavi.OnlineContent/LinkedinConetnt'
    'LinkedinContent'            = 'karavi.OnlineContent/LinkedinConetnt'
    'LinkedinConetnt'            = 'karavi.OnlineContent/LinkedinConetnt'
}

function Invoke-Migration {
    if (-not (Test-Path -LiteralPath $k)) { return 0 }
    $migrated = 0
    
    # 1. Handle scripts folder with subfolders (scripts/command, scripts/tools)
    $legacyScripts = Join-Path $k 'scripts'
    if (Test-Path -LiteralPath $legacyScripts) {
        $cmdSrc = Join-Path $legacyScripts 'command'
        if (Test-Path -LiteralPath $cmdSrc) {
            $cmdDst = Join-Path $k 'karavi.scripts.command'
            $migrated += Move-DirectoryContent -Source $cmdSrc -Destination $cmdDst
        }
        $toolSrc = Join-Path $legacyScripts 'tools'
        if (Test-Path -LiteralPath $toolSrc) {
            $toolDst = Join-Path $k 'karavi.scripts.tools'
            $migrated += Move-DirectoryContent -Source $toolSrc -Destination $toolDst
        }
        # If anything remains in scripts/ move to karavi.scripts.command
        $remaining = Get-ChildItem -LiteralPath $legacyScripts -Force -ErrorAction SilentlyContinue
        if ($remaining) {
            $cmdDst = Join-Path $k 'karavi.scripts.command'
            $migrated += Move-DirectoryContent -Source $legacyScripts -Destination $cmdDst
        } else {
            if (-not $WhatIf) { Remove-Item -LiteralPath $legacyScripts -Force -Recurse -ErrorAction SilentlyContinue }
        }
    }

    # 2. Handle assets folder with subfolders (assets/{brand,icons,screenshots,templates})
    $legacyAssets = Join-Path $k 'assets'
    if (Test-Path -LiteralPath $legacyAssets) {
        $assetSub = @('brand', 'icons', 'screenshots', 'templates')
        foreach ($sub in $assetSub) {
            $subSrc = Join-Path $legacyAssets $sub
            if (Test-Path -LiteralPath $subSrc) {
                $subDst = Join-Path (Join-Path $k 'karavi.assets') $sub
                $migrated += Move-DirectoryContent -Source $subSrc -Destination $subDst
            }
        }
        $remAssets = Get-ChildItem -LiteralPath $legacyAssets -Force -ErrorAction SilentlyContinue
        if ($remAssets) {
            $dstAssets = Join-Path $k 'karavi.assets'
            $migrated += Move-DirectoryContent -Source $legacyAssets -Destination $dstAssets
        } else {
            if (-not $WhatIf) { Remove-Item -LiteralPath $legacyAssets -Force -Recurse -ErrorAction SilentlyContinue }
        }
    }

    # 3. Handle legacy karavi.SociaMediaContent -> karavi.OnlineContent/SociaMediaContent
    $legacySocialPaths = @(
        (Join-Path $k 'karavi.SociaMediaContent'),
        (Join-Path $k 'karavi.SocialMediaContent'),
        (Join-Path $k 'SociaMediaContent'),
        (Join-Path $k 'SocialMediaContent')
    )
    foreach ($lsp in $legacySocialPaths) {
        if (Test-Path -LiteralPath $lsp) {
            $targetSocialDst = Join-Path (Join-Path $k 'karavi.OnlineContent') 'SociaMediaContent'
            $migrated += Move-DirectoryContent -Source $lsp -Destination $targetSocialDst
        }
    }

    # 4. Check all direct children of karavi/ against migration map
    $children = Get-ChildItem -LiteralPath $k -Directory -ErrorAction SilentlyContinue
    foreach ($child in $children) {
        $name = $child.Name
        if ($migrationMap.Contains($name)) {
            $targetRel = $migrationMap[$name]
            $targetPath = Join-Path $k ($targetRel -replace '/', [IO.Path]::DirectorySeparatorChar)
            
            # If the legacy folder name is not already identical to the target folder
            if ($child.FullName -ne $targetPath -and (Test-Path -LiteralPath $child.FullName)) {
                $migrated += Move-DirectoryContent -Source $child.FullName -Destination $targetPath
            }
        }
    }

    return $migrated
}

function Move-DirectoryContent {
    param(
        [string]$Source,
        [string]$Destination
    )
    if (-not (Test-Path -LiteralPath $Source)) { return 0 }
    $count = 0

    if (-not (Test-Path -LiteralPath $Destination)) {
        if ($WhatIf) {
            Write-Host "[WhatIf] Rename/Move folder: $Source -> $Destination"
        } else {
            # If destination parent exists or create it
            $destParent = Split-Path -Parent $Destination
            if (-not (Test-Path -LiteralPath $destParent)) {
                New-Item -ItemType Directory -Force -Path $destParent | Out-Null
            }
            Move-Item -LiteralPath $Source -Destination $Destination -Force
            Write-Host "[Migrated] Renamed/Moved folder: $Source -> $Destination"
        }
        return 1
    }

    # Destination already exists: move contents safely
    $items = Get-ChildItem -LiteralPath $Source -Force -ErrorAction SilentlyContinue
    foreach ($item in $items) {
        $destItemPath = Join-Path $Destination $item.Name
        if ($WhatIf) {
            Write-Host "[WhatIf] Move content: $($item.FullName) -> $destItemPath"
            $count++
        } else {
            if (-not (Test-Path -LiteralPath $destItemPath)) {
                Move-Item -LiteralPath $item.FullName -Destination $destItemPath -Force
                Write-Host "[Migrated] Moved: $($item.FullName) -> $destItemPath"
                $count++
            } else {
                # Collision: merge if directory, rename if file
                if ($item.PSIsContainer) {
                    $count += Move-DirectoryContent -Source $item.FullName -Destination $destItemPath
                } else {
                    $backupName = "$($item.BaseName).legacy-$([Guid]::NewGuid().ToString('N').Substring(0,6))$($item.Extension)"
                    $altDest = Join-Path $Destination $backupName
                    Move-Item -LiteralPath $item.FullName -Destination $altDest -Force
                    Write-Host "[Migrated] Collision resolved (renamed): $($item.FullName) -> $altDest"
                    $count++
                }
            }
        }
    }

    # Remove now-empty source folder
    if (-not $WhatIf) {
        $rem = Get-ChildItem -LiteralPath $Source -Force -ErrorAction SilentlyContinue
        if (-not $rem) {
            Remove-Item -LiteralPath $Source -Force -Recurse -ErrorAction SilentlyContinue
        }
    }

    return $count
}

# --- Step 1: Run Migration ---------------------------------------------------
$migratedCount = 0
if (-not $NoMigrate) {
    $migratedCount = Invoke-Migration
}

# --- Step 2: Scaffold Canonical Folders (Full by default) ---------------------
$foldersToCreate = @($coreFolders)
if (-not $Core) {
    # Full is default: include optional folders
    $foldersToCreate += $optionalFolders
}

$createdCount = 0
foreach ($rel in $foldersToCreate) {
    $target = Join-Path $k ($rel -replace '/', [IO.Path]::DirectorySeparatorChar)
    $rootCheck = (Join-Path $repo '') -replace '\\$', ''
    if (-not $target.StartsWith($rootCheck, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refused: $target resolves outside repo root."
    }
    if (-not (Test-Path -LiteralPath $target)) {
        if ($WhatIf) {
            Write-Host "[WhatIf] Create dir: $target"
        } else {
            New-Item -ItemType Directory -Force -Path $target | Out-Null
            # Add .gitkeep to sentinel directories
            $gitkeepPath = Join-Path $target '.gitkeep'
            if (-not (Test-Path -LiteralPath $gitkeepPath)) {
                New-Item -ItemType File -Path $gitkeepPath -Force | Out-Null
            }
        }
        $createdCount++
    }
}

# --- Step 3: Wire .gitignore if needed ---------------------------------------
$gitignorePath = Join-Path $repo '.gitignore'
$gitignoreBlock = @"

# --- karavi ---
karavi/karavi.temp.logs/
karavi/karavi.temp.status/
karavi/karavi.temp.deploy/
karavi/karavi.temp.build/
karavi/karavi.deploy.config/Deploy_FTP.info
karavi/karavi.deploy.config/Deploy_TestUsers.info
karavi/karavi.deploy.config/deploy.secrets.json
"@

if (Test-Path -LiteralPath $gitignorePath) {
    $giContent = Get-Content -LiteralPath $gitignorePath -Raw -ErrorAction SilentlyContinue
    if ($giContent -notmatch 'karavi/karavi\.temp\.logs') {
        if ($WhatIf) {
            Write-Host "[WhatIf] Append '# --- karavi ---' block to .gitignore"
        } else {
            Add-Content -LiteralPath $gitignorePath -Value $gitignoreBlock -Encoding UTF8
            Write-Host "Updated .gitignore with karavi rules."
        }
    }
}

Write-Host "karavi-folder init completed successfully."
Write-Host "Mode: $(if ($Core) { 'Core (9 folders)' } else { 'Full (20 folders/subfolders - Default)' })"
Write-Host "Migrated/Relocated items: $migratedCount"
Write-Host "Created folders: $createdCount"
Write-Host "Root: $k"
