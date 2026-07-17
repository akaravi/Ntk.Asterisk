#Requires -Version 5.1
Set-StrictMode -Version Latest

function Get-RepoRootFromKaraviScript {
    param([string]$ScriptDirectory = $PSScriptRoot)
    $dir = $ScriptDirectory
    while ($dir) {
        $karavi = Join-Path $dir 'karavi'
        if (Test-Path -LiteralPath $karavi) {
            return $dir
        }
        $parent = Split-Path -Parent $dir
        if ($parent -eq $dir) { break }
        $dir = $parent
    }
    throw "Repository root (folder containing karavi/) not found from: $ScriptDirectory"
}

function Get-KaraviPaths {
    param([string]$RepoRoot = $null)

    if (-not $RepoRoot) {
        $RepoRoot = Get-RepoRootFromKaraviScript
    }

    $k = Join-Path $RepoRoot 'karavi'
    if (-not (Test-Path -LiteralPath $k)) {
        throw "karavi folder not found: $k"
    }

    [ordered]@{
        RepoRoot           = $RepoRoot
        KaraviRoot         = $k
        PlansPrompt        = Join-Path $k 'karavi.plans.prompt'
        PlansCursor        = Join-Path $k 'karavi.plans.prompt/cursor'
        CustomerDoc        = Join-Path $k 'karavi.Customer.doc'
        Doc                = Join-Path $k 'karavi.doc'
        BusinessModelDoc   = Join-Path $k 'karavi.BusinessModel.Doc'
        History            = Join-Path $k 'karavi.history'
        Logs               = Join-Path $k 'karavi.logs'
        Status             = Join-Path $k 'karavi.status'
        DeployConfig       = Join-Path $k 'karavi.deploy.config'
        DeployFiles        = Join-Path $k 'karavi.deploy.files'
        BuildConfig        = Join-Path $k 'karavi.build.config'
        BuildFiles         = Join-Path $k 'karavi.build.files'
        PublishConfig      = Join-Path $k 'karavi.publish.config'
        PublishFiles       = Join-Path $k 'karavi.publish.files'
        Assets             = Join-Path $k 'karavi.assets'
        Scripts            = Join-Path $k 'karavi.scripts'
        ScriptsCommand     = Join-Path $k 'karavi.scripts.command'
        ScriptsTools       = Join-Path $k 'karavi.scripts.tools'
        SociaMediaContent  = Join-Path $k 'karavi.SociaMediaContent'
        CleanManifest      = Join-Path $k 'karavi.build.config/clean-manifest.json'
        VersionManifest    = Join-Path $k 'karavi.build.config/version-manifest.json'
        LocalDevPorts      = Join-Path $k 'karavi.build.config/local-dev-ports.json'
        ProductionHosts    = Join-Path $k 'karavi.deploy.config/production-hosts.json'
        LastRunInfo        = Join-Path $k 'karavi.status/LastRunInfo.html'
        DeploySummary      = Join-Path $k 'karavi.status/Deploy_Summary.html'
    }
}

function Resolve-KaraviScriptPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )
    $paths = Get-KaraviPaths
    $full = Join-Path $paths.KaraviRoot ($RelativePath -replace '^karavi/', '')
    if (-not (Test-Path -LiteralPath $full)) {
        throw "Script not found: $full"
    }
    return $full
}

if ($MyInvocation.InvocationName -ne '.') {
    Export-ModuleMember -Function Get-RepoRootFromKaraviScript, Get-KaraviPaths, Resolve-KaraviScriptPath -ErrorAction SilentlyContinue
}
