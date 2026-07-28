param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'Le SDK .NET 8 est requis : https://dotnet.microsoft.com/download/dotnet/8.0'
}

& .\scripts\clean.ps1
& .\scripts\audit-release.ps1
& .\scripts\test.ps1

[xml]$project = Get-Content -LiteralPath .\src\CodexLimits.App\CodexLimits.App.csproj
$version = $project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) {
    [xml]$props = Get-Content -LiteralPath .\Directory.Build.props
    $version = $props.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
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
Copy-Item -LiteralPath .\docs\icon.png -Destination $docsDirectory -Force
Copy-Item -LiteralPath .\docs\RELEASE_CHECKLIST.md -Destination $docsDirectory -Force
Copy-Item -LiteralPath .\docs\AUDIT_RELEASE_READY.md -Destination $docsDirectory -Force

Compress-Archive -Path (Join-Path $publishDirectory '*') -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $hashPath -Value "$hash  $(Split-Path $zipPath -Leaf)" -Encoding ascii

Write-Host "Publication créée : $zipPath"
Write-Host "SHA-256 : $hashPath"
Write-Warning 'La distribution est non signée. Consulte docs\RELEASE_CHECKLIST.md avant une publication publique.'
