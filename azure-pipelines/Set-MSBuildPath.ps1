$MSBuildPath = & "$PSScriptRoot\Get-MSBuildPath.ps1"
Write-Verbose "Using MSBuild from $MSBuildPath"
Write-Host "##vso[task.prependpath]$MSBuildPath"
