# Windows icon format conversion only. Preserve the approved transparent logo and alpha.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$sourcePath = Join-Path $root 'assets\Naqla-logo-transparent.png'
$assetDirectory = Join-Path $root 'assets'
New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
$source = [System.Drawing.Image]::FromFile($sourcePath)
$frames = [System.Collections.Generic.List[byte[]]]::new()
$sizes = @(16,20,24,32,40,48,64,128,256)
try {
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $memory = [System.IO.MemoryStream]::new()
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $ratio = [math]::Min($size / $source.Width, $size / $source.Height)
            $width = [int][math]::Round($source.Width * $ratio)
            $height = [int][math]::Round($source.Height * $ratio)
            $bounds = [System.Drawing.Rectangle]::new([int](($size-$width)/2),[int](($size-$height)/2),$width,$height)
            $graphics.DrawImage($source,$bounds)
            if ($size -eq 256) {
                $bitmap.Save($memory,[System.Drawing.Imaging.ImageFormat]::Png)
            } else {
                # DIB frames are compatible with classic GDI/NotifyIcon as well as
                # Explorer. PNG-compressed small frames can decode incorrectly in GDI+.
                $dib = [System.IO.BinaryWriter]::new($memory,[System.Text.Encoding]::UTF8,$true)
                $maskStride = [int]([math]::Ceiling($size / 32.0) * 4)
                try {
                    $dib.Write([int]40); $dib.Write([int]$size); $dib.Write([int]($size*2))
                    $dib.Write([uint16]1); $dib.Write([uint16]32); $dib.Write([uint32]0)
                    $dib.Write([uint32]($size*$size*4 + $maskStride*$size))
                    $dib.Write([int]0); $dib.Write([int]0); $dib.Write([uint32]0); $dib.Write([uint32]0)
                    $data = $bitmap.LockBits([System.Drawing.Rectangle]::new(0,0,$size,$size),[System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                    $masks = [System.Collections.Generic.List[byte[]]]::new()
                    try {
                        for ($row=$size-1; $row -ge 0; $row--) {
                            $pixels = [byte[]]::new($size*4)
                            [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]::Add($data.Scan0,$row*$data.Stride),$pixels,0,$pixels.Length)
                            $dib.Write($pixels)
                            $mask = [byte[]]::new($maskStride)
                            for ($column=0; $column -lt $size; $column++) {
                                if ($pixels[$column*4+3] -lt 128) {
                                    $byteIndex = [int][math]::Floor($column/8)
                                    $mask[$byteIndex] = $mask[$byteIndex] -bor (1 -shl (7-($column%8)))
                                }
                            }
                            $masks.Add($mask)
                        }
                        foreach ($mask in $masks) { $dib.Write([byte[]]$mask) }
                    } finally { $bitmap.UnlockBits($data) }
                } finally { $dib.Dispose() }
            }
            $frames.Add($memory.ToArray())
        } finally { $memory.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $source.Dispose() }
$output = [System.IO.File]::Create((Join-Path $assetDirectory 'Naqla.ico'))
$writer = [System.IO.BinaryWriter]::new($output)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($index=0; $index -lt $sizes.Count; $index++) {
        $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$index].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$index].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
} finally { $writer.Dispose() }
# Decode Windows' special zero-byte 256px PNG directory entry.
$memory = [System.IO.MemoryStream]::new($frames[$frames.Count-1],$false)
$decoded = [System.Drawing.Image]::FromStream($memory)
try { if ($decoded.Width -ne 256 -or $decoded.Height -ne 256) { throw 'Invalid large icon frame.' } }
finally { $decoded.Dispose(); $memory.Dispose() }
# .NET Framework's Icon selector can choose 128px for a 256px request; validate the
# notification-area sizes with it, while Windows uses the 256px PNG for large icons.
foreach ($size in ($sizes | Where-Object { $_ -lt 256 })) {
    $icon = [System.Drawing.Icon]::new((Join-Path $assetDirectory 'Naqla.ico'),$size,$size)
    try { if ($icon.Width -ne $size -or $icon.Height -ne $size) { throw "Icon size mismatch: $size" } }
    finally { $icon.Dispose() }
}
Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $root 'preview\LOGO.png') -Force
Write-Host ('Converted transparent logo to assets\Naqla.ico: ' + ($sizes -join ', ') + ' px; preview uses the same transparent PNG.')
