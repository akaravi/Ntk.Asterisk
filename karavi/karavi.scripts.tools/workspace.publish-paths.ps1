#Requires -Version 5.1
. (Join-Path $PSScriptRoot 'workspace.paths.ps1')

function Get-PublishPaths {
    $p = Get-KaraviPaths
    [ordered]@{
        StageRoot     = $p.PublishFiles
        FtpDeploy     = Join-Path $p.PublishFiles 'ftp-deploy'
        DeployFiles   = $p.DeployFiles
        PublishConfig = $p.PublishConfig
        DeployConfig  = $p.DeployConfig
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    Export-ModuleMember -Function Get-PublishPaths -ErrorAction SilentlyContinue
}
