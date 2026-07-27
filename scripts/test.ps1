$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

dotnet build .\CodexLimits.Windows.sln -c Release
dotnet run --project .\tests\CodexLimits.SmokeTests\CodexLimits.SmokeTests.csproj -c Release --no-build
