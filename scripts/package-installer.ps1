$ErrorActionPreference = 'Stop'

$isccPath = $null
$isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if ($isccCommand) {
  $isccPath = $isccCommand.Source
}

if (-not $isccPath) {
  $candidates = @(
    "${env:ProgramFiles(x86)}\\Inno Setup 6\\ISCC.exe",
    "$env:ProgramFiles\\Inno Setup 6\\ISCC.exe",
    "$env:LocalAppData\\Programs\\Inno Setup 6\\ISCC.exe"
  )

  foreach ($candidate in $candidates) {
    if (Test-Path $candidate) {
      $isccPath = $candidate
      break
    }
  }
}

if (-not $isccPath) {
  throw 'Inno Setup compiler (ISCC.exe) not found. Install Inno Setup 6 or add ISCC.exe to PATH.'
}

if (Test-Path out/publish) {
  Remove-Item out/publish -Recurse -Force
}

dotnet publish HTWind/HTWind.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o out/publish

New-Item -ItemType Directory -Path dist -Force | Out-Null

Get-ChildItem -Path dist -Filter "HTWind-setup-*.exe" -ErrorAction SilentlyContinue |
  Remove-Item -Force -ErrorAction SilentlyContinue

& $isccPath /DSourceDir="$PWD\out\publish" /DMyAppExeName="HTWind.exe" installer/HTWind.iss
