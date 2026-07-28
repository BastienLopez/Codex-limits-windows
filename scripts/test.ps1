$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Le SDK .NET 8 est requis : https://dotnet.microsoft.com/download/dotnet/8.0'
}

dotnet restore .\CodexLimits.Windows.sln
dotnet build .\CodexLimits.Windows.sln -c Release --no-restore
dotnet run --project .\tests\CodexLimits.SmokeTests\CodexLimits.SmokeTests.csproj -c Release --no-build

Write-Host 'Build Release et smoke tests OK.'
