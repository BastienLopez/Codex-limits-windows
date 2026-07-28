$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

Get-ChildItem -Directory -Recurse -Force |
    Where-Object {
        $_.FullName -notmatch '[\\/]\.git([\\/]|$)' -and
        $_.Name -in @('bin', 'obj', '.vs', 'artifacts', 'TestResults')
    } |
    Sort-Object FullName -Descending |
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Get-ChildItem -File -Recurse -Force |
    Where-Object {
        $_.FullName -notmatch '[\\/]\.git([\\/]|$)' -and
        ($_.Name -like '*.bak*' -or $_.Name -like '*.tmp')
    } |
    Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host 'Dossiers générés et sauvegardes temporaires supprimés.'
