$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Le SDK .NET 8 est requis : https://dotnet.microsoft.com/download/dotnet/8.0'
}

dotnet run --project .\src\CodexLimits.App\CodexLimits.App.csproj --configuration Debug -- --background
