$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source = [Drawing.Image]::FromFile((Join-Path $PSScriptRoot 'PlexMonitor-icon.png'))
$sizes = @(16,20,24,32,40,48,64,128,256)
$frames = @()
try {
    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source,[Drawing.Rectangle]::new(0,0,$size,$size))
            if ($size -eq 256) {
                $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
            } else {
                # DIB frames keep small Win32/GDI icons compatible with Icon.ToBitmap.
                $dib = [IO.BinaryWriter]::new($stream,[Text.Encoding]::UTF8,$true)
                try {
                    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
                    $dib.Write([uint32]40); $dib.Write([int]$size); $dib.Write([int]($size * 2))
                    $dib.Write([uint16]1); $dib.Write([uint16]32); $dib.Write([uint32]0)
                    $dib.Write([uint32]($size * $size * 4)); $dib.Write([int]0); $dib.Write([int]0)
                    $dib.Write([uint32]0); $dib.Write([uint32]0)
                    for ($row=$size-1; $row -ge 0; $row--) {
                        for ($column=0; $column -lt $size; $column++) {
                            $pixel=$bitmap.GetPixel($column,$row)
                            $dib.Write([byte]$pixel.B); $dib.Write([byte]$pixel.G)
                            $dib.Write([byte]$pixel.R); $dib.Write([byte]$pixel.A)
                        }
                    }
                    for ($row=$size-1; $row -ge 0; $row--) {
                        $mask = [byte[]]::new($maskStride)
                        for ($column=0; $column -lt $size; $column++) {
                            if ($bitmap.GetPixel($column,$row).A -eq 0) {
                                $index=[int][Math]::Floor($column/8)
                                $mask[$index]=$mask[$index] -bor (128 -shr ($column % 8))
                            }
                        }
                        $dib.Write($mask)
                    }
                } finally { $dib.Dispose() }
            }
            $frames += ,$stream.ToArray()
        } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $source.Dispose() }
$writer = [IO.BinaryWriter]::new([IO.File]::Create((Join-Path $PSScriptRoot 'PlexMonitor.ico')))
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i=0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose() }
Write-Output 'Created PlexMonitor.ico with 9 sizes (16–256px).'
