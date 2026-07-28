$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

$required = @(
    '.\README.md',
    '.\LICENSE',
    '.\PRIVACY.md',
    '.\SECURITY.md',
    '.\THIRD_PARTY_NOTICES.md',
    '.\TRADEMARKS.md',
    '.\docs\icon.png',
    '.\docs\icon.ico',
    '.\docs\codex-limits.png'
)

foreach ($path in $required) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Fichier obligatoire manquant : $path"
    }
}

$forbiddenDirectories = Get-ChildItem -Directory -Recurse -Force |
    Where-Object {
        $_.FullName -notmatch '[\\/]\.git([\\/]|$)' -and
        $_.Name -in @('bin', 'obj', '.vs', 'artifacts', 'TestResults')
    }

if ($forbiddenDirectories) {
    $names = $forbiddenDirectories.FullName -join [Environment]::NewLine
    throw "Dossiers générés à nettoyer avant publication :`n$names"
}

$forbiddenFiles = Get-ChildItem -File -Recurse -Force |
    Where-Object {
        $_.FullName -notmatch '[\\/]\.git([\\/]|$)' -and
        ($_.Name -like '*.bak*' -or $_.Name -like '*.tmp' -or $_.Extension -in @('.pfx', '.p12', '.pem', '.key'))
    }

if ($forbiddenFiles) {
    $names = $forbiddenFiles.FullName -join [Environment]::NewLine
    throw "Fichiers temporaires ou sensibles à nettoyer :`n$names"
}

$textExtensions = @('.cs', '.xaml', '.xml', '.csproj', '.props', '.ps1', '.md', '.json', '.yml', '.yaml')
$textFiles = Get-ChildItem -File -Recurse -Force |
    Where-Object {
        $_.FullName -notmatch '[\\/]\.git([\\/]|$)' -and
        $_.FullName -ne $PSCommandPath -and
        $_.Extension -in $textExtensions
    }

$secretPatterns = @(
    'sk-[A-Za-z0-9_-]{20,}',
    'ghp_[A-Za-z0-9]{20,}',
    'github_pat_[A-Za-z0-9_]{20,}',
    '-----BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY-----'
)

foreach ($pattern in $secretPatterns) {
    $match = $textFiles | Select-String -Pattern $pattern -CaseSensitive -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($match) {
        throw "Secret potentiel détecté dans $($match.Path), ligne $($match.LineNumber)."
    }
}

Write-Host 'Audit source OK : structure, documents et recherche ciblée de secrets validés.'
