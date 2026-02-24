$ErrorActionPreference = 'Stop'

if (Test-Path out/publish) {
  Remove-Item out/publish -Recurse -Force
}

dotnet publish HTWind/HTWind.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o out/publish

New-Item -ItemType Directory -Path dist -Force | Out-Null

$localVersion = "local-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
$zipPath = "dist/HTWind-portable-$localVersion.zip"

if (Test-Path $zipPath) {
  Remove-Item $zipPath -Force
}

Compress-Archive -Path "out/publish/*" -DestinationPath $zipPath
Write-Host "Portable package created: $zipPath"
