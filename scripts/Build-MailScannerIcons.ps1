Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = 'Stop'

function New-IconBitmap {
    param(
        [string]$Name,
        [scriptblock]$Painter
    )

    $assetsRoot = Join-Path $PSScriptRoot '..\src\MailScanner.App\Assets'
    $variantRoot = Join-Path $assetsRoot 'IconVariants'
    New-Item -ItemType Directory -Force -Path $variantRoot | Out-Null

    $size = 128
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    & $Painter $graphics $size

    $icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
    $iconPath = Join-Path $variantRoot "$Name.ico"
    $stream = [System.IO.File]::Create($iconPath)
    $icon.Save($stream)
    $stream.Dispose()

    $icon.Dispose()
    $graphics.Dispose()
    $bitmap.Dispose()
}

New-IconBitmap -Name 'MailScanner-Mail' -Painter {
    param($g, $size)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 8, 8, 112, 112), ([System.Drawing.Color]::FromArgb(21,55,92)), ([System.Drawing.Color]::FromArgb(79,130,191)), 45
    $g.FillEllipse($bg, 8, 8, 112, 112)
    $g.DrawEllipse((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(170,223,238,255)), 3), 8, 8, 112, 112)
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(246,250,255))), [System.Drawing.Point[]]@((New-Object System.Drawing.Point 28,44),(New-Object System.Drawing.Point 100,44),(New-Object System.Drawing.Point 100,88),(New-Object System.Drawing.Point 28,88)))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(224,237,251))), [System.Drawing.Point[]]@((New-Object System.Drawing.Point 28,44),(New-Object System.Drawing.Point 64,70),(New-Object System.Drawing.Point 100,44)))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(48,92,141)), 4
    $g.DrawRectangle($pen, 28, 44, 72, 44)
    $g.DrawLine($pen, 28, 44, 64, 70)
    $g.DrawLine($pen, 100, 44, 64, 70)
    $g.DrawLine((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(120,220,243,255)), 5), 26, 98, 102, 98)
}

New-IconBitmap -Name 'MailScanner-Orbit' -Painter {
    param($g, $size)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 10, 10, 108, 108), ([System.Drawing.Color]::FromArgb(18,49,86)), ([System.Drawing.Color]::FromArgb(86,143,209)), 45
    $g.FillRectangle($bg, 16, 16, 96, 96)
    $g.DrawRectangle((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(180,230,242,255)), 3), 16, 16, 96, 96)
    $g.DrawEllipse((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(145,216,240,255)), 5), 24, 34, 80, 56)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(246,250,255))), 38, 32, 52, 52)
    $g.DrawString('M', (New-Object System.Drawing.Font 'Bahnschrift', 34, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)), (New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(31,74,118))), (New-Object System.Drawing.RectangleF 0, 0, 128, 116), (New-Object System.Drawing.StringFormat -Property @{ Alignment = [System.Drawing.StringAlignment]::Center; LineAlignment = [System.Drawing.StringAlignment]::Center }))
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,84,163,204))), 92, 42, 14, 14)
}

New-IconBitmap -Name 'MailScanner-Seal' -Painter {
    param($g, $size)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 8, 8, 112, 112), ([System.Drawing.Color]::FromArgb(22,58,93)), ([System.Drawing.Color]::FromArgb(57,100,155)), 90
    $g.FillEllipse($bg, 8, 8, 112, 112)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(28,255,255,255))), 24, 18, 60, 34)
    $shield = [System.Drawing.Point[]]@((New-Object System.Drawing.Point 64,28),(New-Object System.Drawing.Point 94,40),(New-Object System.Drawing.Point 88,84),(New-Object System.Drawing.Point 64,100),(New-Object System.Drawing.Point 40,84),(New-Object System.Drawing.Point 34,40))
    $g.FillPolygon((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(243,248,255))), $shield)
    $g.DrawPolygon((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(47,88,138)), 4), $shield)
    $g.DrawLine((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(86,132,186)), 5), 48, 56, 60, 70)
    $g.DrawLine((New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(86,132,186)), 5), 60, 70, 82, 46)
}

Copy-Item -Path (Join-Path $PSScriptRoot '..\src\MailScanner.App\Assets\IconVariants\MailScanner-Mail.ico') -Destination (Join-Path $PSScriptRoot '..\src\MailScanner.App\Assets\MailScanner.ico') -Force
