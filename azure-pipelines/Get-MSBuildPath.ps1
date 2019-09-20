$vswherePath = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswherePath)) {
    Write-Error "Unable to find vswhere.exe. Is VS 2017 15.3 or later installed? Use $PSScriptRoot\Install-VS.ps1 to acquire VS with the required components."
    exit 1
}

$vswhereArgs = & "$PSScriptRoot\Get-VSWhereArgs.ps1"

Write-Verbose "`"$vswherePath`" $vswhereArgs"
$output = & $vswherePath $vswhereArgs
if ($lastexitcode -eq 87) {
    $vswhere = [xml]'<instances />'
} else {
    $vswhere = [xml]$output
}
if ($vswhere.files.length -eq 0) {
    Write-Error "No VS installation detected that has all the required workloads installed. Use $PSScriptRoot\Install-VS.ps1 to acquire VS with the required components."
    exit 2
}

$MSBuildPath = Split-Path -Path $vswhere.files.file
Write-Output $MSBuildPath
