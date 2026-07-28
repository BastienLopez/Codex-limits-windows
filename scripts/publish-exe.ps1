param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Le SDK .NET 8 est requis : https://dotnet.microsoft.com/download/dotnet/8.0'
}

& .\scripts\test.ps1

[xml]$project = Get-Content -LiteralPath .\src\CodexLimits.App\CodexLimits.App.csproj
$version = $project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) {
    $version = '0.0.0'
}

$releaseRoot = Join-Path (Get-Location) 'artifacts\release'
$publishDirectory = Join-Path $releaseRoot "Codex-Limits-Windows-$version-$Runtime"
$zipPath = Join-Path $releaseRoot "Codex-Limits-Windows-$version-$Runtime.zip"
$hashPath = "$zipPath.sha256"

Remove-Item -LiteralPath $publishDirectory -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $hashPath -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $publishDirectory -Force | Out-Null

dotnet publish .\src\CodexLimits.App\CodexLimits.App.csproj `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $publishDirectory

Get-ChildItem -LiteralPath $publishDirectory -Filter '*.pdb' -File -ErrorAction SilentlyContinue |
    Remove-Item -Force

$docsDirectory = Join-Path $publishDirectory 'docs'
New-Item -ItemType Directory -Path $docsDirectory -Force | Out-Null
Copy-Item -LiteralPath .\docs\codex-limits.png -Destination $docsDirectory -Force

Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $hashPath -Value "$hash  $(Split-Path $zipPath -Leaf)" -Encoding ascii

Write-Host "Publication creee : $zipPath"
Write-Host "SHA-256 : $hashPath"
Write-Warning 'La distribution est non signee. Une signature Authenticode est recommandee avant publication publique.'
