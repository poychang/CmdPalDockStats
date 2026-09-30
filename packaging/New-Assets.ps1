[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$directory = Join-Path $root 'src/CmdPalDockStats.Extension/Assets'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$sizes = @{ StoreLogo = 50; Square44x44Logo = 44; Square150x150Logo = 150 }
foreach ($name in $sizes.Keys) {
    $size = $sizes[$name]
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(18, 139, 108), 7)
    $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(18, 139, 108))
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.ScaleTransform($size / 100.0, $size / 100.0)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $graphics.DrawLine($pen, 30, 20, 30, 60)
        $graphics.DrawEllipse($pen, 17, 54, 26, 26)
        $graphics.FillEllipse($brush, 24, 61, 12, 12)
        $graphics.DrawLine($pen, 54, 65, 64, 65)
        $graphics.DrawLine($pen, 64, 65, 72, 35)
        $graphics.DrawLine($pen, 72, 35, 81, 55)
        $graphics.DrawLine($pen, 81, 55, 88, 55)
        $bitmap.Save((Join-Path $directory "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $brush.Dispose()
        $pen.Dispose()
        $graphics.Dispose()
        $bitmap.Dispose()
    }
}