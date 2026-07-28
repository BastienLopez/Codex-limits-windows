param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET 8 SDK is required to create a release build.'
}

& .\scripts\test.ps1

[xml]$project = Get-Content -LiteralPath .\src\CodexLimits.App\CodexLimits.App.csproj
$version = $project.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) {
    $version = '0.0.0'
}

$artifactsRoot = Join-Path (Get-Location) 'artifacts'
$stagingDirectory = Join-Path $artifactsRoot "publish\$Runtime"
$releaseRoot = Join-Path $artifactsRoot 'release'
$releaseBaseName = "CodexLimits.Windows-$version-$Runtime"
$releaseExe = Join-Path $releaseRoot "$releaseBaseName.exe"
$releaseExeHash = "$releaseExe.sha256"
$releaseZip = Join-Path $releaseRoot "$releaseBaseName.zip"
$releaseZipHash = "$releaseZip.sha256"
$bundleDirectory = Join-Path $artifactsRoot "bundle\$releaseBaseName"

Remove-Item -LiteralPath $stagingDirectory -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $bundleDirectory -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $releaseExe -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $releaseExeHash -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $releaseZip -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $releaseZipHash -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $stagingDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
New-Item -ItemType Directory -Path $bundleDirectory -Force | Out-Null

dotnet publish .\src\CodexLimits.App\CodexLimits.App.csproj `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -p:PublishReadyToRun=false `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $stagingDirectory

$publishedExe = Join-Path $stagingDirectory 'CodexLimits.Windows.exe'
if (-not (Test-Path -LiteralPath $publishedExe -PathType Leaf)) {
    throw "Published executable not found: $publishedExe"
}

Copy-Item -LiteralPath $publishedExe -Destination $releaseExe -Force
$exeHash = (Get-FileHash -LiteralPath $releaseExe -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $releaseExeHash -Value "$exeHash  $(Split-Path $releaseExe -Leaf)" -Encoding ascii

Copy-Item -LiteralPath $publishedExe -Destination (Join-Path $bundleDirectory 'CodexLimits.Windows.exe') -Force
foreach ($file in @('README.md', 'LICENSE', 'PRIVACY.md', 'SECURITY.md', 'THIRD_PARTY_NOTICES.md', 'TRADEMARKS.md')) {
    Copy-Item -LiteralPath (Join-Path (Get-Location) $file) -Destination $bundleDirectory -Force
}

Compress-Archive -Path (Join-Path $bundleDirectory '*') -DestinationPath $releaseZip -CompressionLevel Optimal
$zipHash = (Get-FileHash -LiteralPath $releaseZip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $releaseZipHash -Value "$zipHash  $(Split-Path $releaseZip -Leaf)" -Encoding ascii

Write-Host "Standalone EXE created: $releaseExe"
Write-Host "EXE SHA-256: $releaseExeHash"
Write-Host "Release ZIP created: $releaseZip"
Write-Host "ZIP SHA-256: $releaseZipHash"
Write-Warning 'The executable is self-contained but unsigned. Sign it with Authenticode before public distribution.'
