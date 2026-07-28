param(
    [Parameter(Mandatory = $true)]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [string]$DestinationPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $SourcePath)) {
    throw "Image d'icone introuvable : $SourcePath"
}

Add-Type -AssemblyName System.Drawing

if (-not ('CodexLimits.NativeIconMethods' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;

namespace CodexLimits
{
    public static class NativeIconMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr handle);
    }
}
'@
}

$destinationDirectory = Split-Path -Parent $DestinationPath
New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null

$source = $null
$sourceBitmap = $null
$bitmap = $null
$graphics = $null
$temporaryIcon = $null
$icon = $null
$stream = $null
$handle = [IntPtr]::Zero

try {
    $source = [System.Drawing.Image]::FromFile($SourcePath)
    $sourceBitmap = New-Object System.Drawing.Bitmap $source

    $minX = $sourceBitmap.Width
    $minY = $sourceBitmap.Height
    $maxX = -1
    $maxY = -1

    for ($y = 0; $y -lt $sourceBitmap.Height; $y++) {
        for ($x = 0; $x -lt $sourceBitmap.Width; $x++) {
            if ($sourceBitmap.GetPixel($x, $y).A -le 8) {
                continue
            }

            if ($x -lt $minX) { $minX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }

    if ($maxX -ge $minX -and $maxY -ge $minY) {
        $sourceRect = New-Object System.Drawing.Rectangle `
            $minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1)
    }
    else {
        $sourceRect = New-Object System.Drawing.Rectangle `
            0, 0, $sourceBitmap.Width, $sourceBitmap.Height
    }

    $canvasSize = 256
    $padding = 6
    $available = $canvasSize - ($padding * 2)
    $scale = [Math]::Min(
        $available / [double][Math]::Max($sourceRect.Width, 1),
        $available / [double][Math]::Max($sourceRect.Height, 1))

    $targetWidth = [Math]::Max([int][Math]::Round($sourceRect.Width * $scale), 1)
    $targetHeight = [Math]::Max([int][Math]::Round($sourceRect.Height * $scale), 1)
    $targetX = [int](($canvasSize - $targetWidth) / 2)
    $targetY = [int](($canvasSize - $targetHeight) / 2)
    $targetRect = New-Object System.Drawing.Rectangle `
        $targetX, $targetY, $targetWidth, $targetHeight

    $bitmap = New-Object System.Drawing.Bitmap `
        $canvasSize, $canvasSize, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.DrawImage(
        $sourceBitmap,
        $targetRect,
        $sourceRect,
        [System.Drawing.GraphicsUnit]::Pixel)

    $handle = $bitmap.GetHicon()
    $temporaryIcon = [System.Drawing.Icon]::FromHandle($handle)
    $icon = $temporaryIcon.Clone()
    $stream = [System.IO.File]::Create($DestinationPath)
    $icon.Save($stream)
}
finally {
    if ($stream) { $stream.Dispose() }
    if ($icon) { $icon.Dispose() }
    if ($temporaryIcon) { $temporaryIcon.Dispose() }
    if ($handle -ne [IntPtr]::Zero) { [CodexLimits.NativeIconMethods]::DestroyIcon($handle) | Out-Null }
    if ($graphics) { $graphics.Dispose() }
    if ($bitmap) { $bitmap.Dispose() }
    if ($sourceBitmap) { $sourceBitmap.Dispose() }
    if ($source) { $source.Dispose() }
}
