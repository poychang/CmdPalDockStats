[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & ./packaging/New-Assets.ps1
    dotnet publish src/CmdPalDockStats.SensorHost -c Release -r win-x64 --self-contained true -o artifacts/SensorHost
    if ($LASTEXITCODE -ne 0) { throw 'SensorHost publish failed.' }
    dotnet build src/CmdPalDockStats.Extension -c Release -p:Platform=x64 -r win-x64 --self-contained true `
        -p:GenerateAppxPackageOnBuild=true -p:AppxPackageSigningEnabled=false "-p:AppxPackageDir=$PSScriptRoot/artifacts/MSIX/"
    if ($LASTEXITCODE -ne 0) { throw 'Extension packaging failed.' }
}
finally {
    Pop-Location
}