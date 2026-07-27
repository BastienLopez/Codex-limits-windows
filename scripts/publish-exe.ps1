$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

$Output = Join-Path (Get-Location) 'artifacts\win-x64'
Remove-Item $Output -Recurse -Force -ErrorAction SilentlyContinue

dotnet publish .\src\CodexLimits.App\CodexLimits.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $Output

Write-Host "Publication terminée : $Output"
