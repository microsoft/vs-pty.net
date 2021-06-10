Function Get-SymbolFiles {
    [CmdletBinding()]
    param (
        [parameter(Mandatory=$true)]
        [string]$Path
    )

    $WindowsPdbSubDirName = "symstore"

    $ActivityName = "Collecting symbols from $Path"
    Write-Progress -Activity $ActivityName -CurrentOperation "Discovery PDB files"
    $PDBs = Get-ChildItem -rec "$Path\*.pdb" |? { $_.FullName -notmatch "unittest|tests|\W$WindowsPdbSubDirName\W" }
    Write-Progress -Activity $ActivityName -CurrentOperation "De-duplicating symbols"
    $PDBsByHash = @{}
    $i = 0
    $PDBs |% {
        Write-Progress -Activity $ActivityName -CurrentOperation "De-duplicating symbols" -PercentComplete (100 * $i / $PDBs.Length)
        $hash = Get-FileHash $_
        $i++
        Add-Member -InputObject $_ -MemberType NoteProperty -Name Hash -Value $hash.Hash
        Write-Output $_
    } | Sort-Object CreationTime |% {
        # De-dupe based on hash. Prefer the first match so we take the first built copy.
        if (-not $PDBsByHash.ContainsKey($_.Hash)) {
            $PDBsByHash.Add($_.Hash, $_.FullName)
            Write-Output $_
        }
    } |% {
        # Collect the DLLs/EXEs as well.
        $dllPath = "$($_.Directory)\$($_.BaseName).dll"
        $exePath = "$($_.Directory)\$($_.BaseName).exe"
        if (Test-Path $dllPath) {
            $BinaryImagePath = $dllPath
        } elseif (Test-Path $exePath) {
            $BinaryImagePath = $exePath
        }

        Write-Output $BinaryImagePath

        # Move PDB files to PDB output folder for symbol archival
        Write-Host "Preparing $_ for symbol archival" -ForegroundColor DarkGray

        $WindowsPdbDir = "$($_.Directory.FullName)\$WindowsPdbSubDirName"
        if (!(Test-Path $WindowsPdbDir)) { mkdir $WindowsPdbDir | Out-Null }

        Write-Host "Copying $_ to $WindowsPdbDir" -ForegroundColor DarkGray
        Copy-Item $_ -Destination $WindowsPdbDir

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Copying of `"$_`" to `"$WindowsPdbDir`" failed with exit code $LASTEXITCODE"
        }
    }
}

# This doesn't work off Windows, nor do we need to convert symbols on multiple OS agents
if ($IsMacOS -or $IsLinux) {
    return;
}

$BinPath = [System.IO.Path]::GetFullPath("$PSScriptRoot\..\..\bin")
if (!(Test-Path $BinPath)) { return }
$symbolfiles = Get-SymbolFiles -Path $BinPath | Get-Unique

@{
    "$BinPath" = $SymbolFiles;
}