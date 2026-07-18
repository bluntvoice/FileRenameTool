$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = New-Object System.Collections.Generic.List[byte[]]

function New-BrushBitmap([int]$size) {
    $scale = 4
    $canvasSize = $size * $scale
    $bitmap = New-Object System.Drawing.Bitmap($canvasSize, $canvasSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.ScaleTransform($canvasSize / 64.0, $canvasSize / 64.0)

    $deepBlue = [System.Drawing.Color]::FromArgb(255, 11, 58, 130)
    $paleBlue = [System.Drawing.Color]::FromArgb(255, 147, 197, 253)

    $handlePen = New-Object System.Drawing.Pen($deepBlue, 10)
    $handlePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $handlePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.DrawLine($handlePen, 31, 32, 56, 32)

    $ferrule = New-Object System.Drawing.Drawing2D.GraphicsPath
    $ferrule.AddPolygon([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(16.0, 20.0),
        [System.Drawing.PointF]::new(34.0, 25.0),
        [System.Drawing.PointF]::new(34.0, 39.0),
        [System.Drawing.PointF]::new(16.0, 44.0)
    ))
    $paleBrush = New-Object System.Drawing.SolidBrush($paleBlue)
    $outlinePen = New-Object System.Drawing.Pen($deepBlue, 2.2)
    $outlinePen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $graphics.FillPath($paleBrush, $ferrule)
    $graphics.DrawPath($outlinePen, $ferrule)

    $graphics.Dispose()
    $handlePen.Dispose()
    $outlinePen.Dispose()
    $paleBrush.Dispose()
    $ferrule.Dispose()

    $final = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $finalGraphics = [System.Drawing.Graphics]::FromImage($final)
    $finalGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $finalGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $finalGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $finalGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $finalGraphics.DrawImage($bitmap, 0, 0, $size, $size)
    $finalGraphics.Dispose()
    $bitmap.Dispose()
    return $final
}

foreach ($size in $sizes) {
    $bitmap = New-BrushBitmap $size
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames.Add($stream.ToArray())
    if ($size -eq 256) {
        $bitmap.Save((Join-Path $PSScriptRoot 'brush.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $stream.Dispose()
    $bitmap.Dispose()
}

$iconPath = Join-Path $PSScriptRoot 'brush.ico'
$file = [System.IO.File]::Open($iconPath, [System.IO.FileMode]::Create)
$writer = New-Object System.IO.BinaryWriter($file)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$frames.Count)
$offset = 6 + (16 * $frames.Count)

for ($index = 0; $index -lt $frames.Count; $index++) {
    $size = $sizes[$index]
    $dimension = if ($size -eq 256) { [byte]0 } else { [byte]$size }
    $writer.Write($dimension)
    $writer.Write($dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$frames[$index].Length)
    $writer.Write([uint32]$offset)
    $offset += $frames[$index].Length
}

foreach ($frame in $frames) {
    $writer.Write($frame)
}

$writer.Dispose()
$file.Dispose()
Write-Host "Generated brush.ico and brush.png"
