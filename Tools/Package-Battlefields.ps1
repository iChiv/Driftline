param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot '../Builds/BattlefieldsV2'),
    [string]$Destination = (Join-Path $PSScriptRoot '../Builds/Driftline-BattlefieldsV2-Windows.zip')
)
$ErrorActionPreference='Stop'
$BuildDirectory=(Resolve-Path -LiteralPath $BuildDirectory).Path
$Destination=[IO.Path]::GetFullPath($Destination)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot '../Builds/LAN-PLAYTEST.md') -Destination (Join-Path $BuildDirectory 'LAN-PLAYTEST.md') -Force
$archive=[IO.Compression.ZipFile]::Open($Destination,[IO.Compression.ZipArchiveMode]::Create)
try {
    foreach($file in Get-ChildItem -LiteralPath $BuildDirectory -File -Recurse){
        $relative=[IO.Path]::GetRelativePath($BuildDirectory,$file.FullName)
        if($relative -match 'BurstDebugInformation_DoNotShip|BackUpThisFolder_ButDontShipItWithYourGame'){continue}
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative.Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal)|Out-Null
    }
} finally {$archive.Dispose()}
Get-Item -LiteralPath $Destination | Select-Object FullName,Length
Get-FileHash -LiteralPath $Destination -Algorithm SHA256
