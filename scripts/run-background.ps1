$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "..\src\CodexLimits.App\CodexLimits.App.csproj"

dotnet run --project $project --configuration Debug -- --background
