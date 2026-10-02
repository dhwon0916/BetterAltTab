# Rebuild the multi-resolution Windows icon using only System.Drawing.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetPath = Join-Path $PSScriptRoot '../BetterAltTab/Assets'
$frames = @()
foreach ($size in @(16, 20, 24, 32, 48, 64, 128, 256)) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = 'AntiAlias'
    $graphics.ScaleTransform($size / 64.0, $size / 64.0)
    $back = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(32, 65, 113))
    $front = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(0, 120, 215))
    $line = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(153, 215, 255), 3)
    $arrow = [System.Drawing.Pen]::new([System.Drawing.Color]::White, 4)
    $arrow.StartCap = 'Round'; $arrow.EndCap = 'Round'; $arrow.LineJoin = 'Round'
    try {
        $graphics.FillRectangle($back, 5, 7, 43, 36)
        $graphics.DrawRectangle($line, 5, 7, 43, 36)
        $graphics.FillRectangle($front, 15, 21, 44, 36)
        $graphics.DrawRectangle($line, 15, 21, 44, 36)
        $graphics.DrawLine($arrow, 25, 39, 49, 39)
        $graphics.DrawLine($arrow, 42, 32, 49, 39)
        $graphics.DrawLine($arrow, 49, 39, 42, 46)
        $stream = [IO.MemoryStream]::new()
        try {
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames += ,@{ Size = $size; Bytes = $stream.ToArray() }
            if ($size -eq 256) { [IO.File]::WriteAllBytes((Join-Path $assetPath 'BetterAltTab.png'), $stream.ToArray()) }
        } finally { $stream.Dispose() }
    } finally { $arrow.Dispose(); $line.Dispose(); $front.Dispose(); $back.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
}
$file = [IO.File]::Create((Join-Path $assetPath 'BetterAltTab.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose() }
