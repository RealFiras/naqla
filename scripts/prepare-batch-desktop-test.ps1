param([string]$Destination = (Join-Path (Split-Path -Parent $PSScriptRoot) ('artifacts\desktop-batch-test-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))))
$ErrorActionPreference = 'Stop'
$destinationPath = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $destinationPath) { throw 'Choose a new, empty fixture directory; existing files will not be replaced.' }
New-Item -ItemType Directory -Path $destinationPath | Out-Null
Add-Type -AssemblyName System.Drawing
$entries = @()
foreach ($group in @('10-images','partial-failure')) {
    $directory = Join-Path $destinationPath $group
    New-Item -ItemType Directory -Path $directory | Out-Null
    $count = if ($group -eq '10-images') { 10 } else { 3 }
    for ($index = 1; $index -le $count; $index++) {
        $name = ('صورة-{0:D2}.png' -f $index); $path = Join-Path $directory $name
        $bitmap = New-Object Drawing.Bitmap(256,192)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $brush = New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(255, (30 + 17 * $index), 85, (210 - 11 * $index)))
        $font = New-Object Drawing.Font('Segoe UI', 18)
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.FillRectangle($brush, 24, 24, 208, 144)
            $graphics.DrawString(('NAQLA {0:D2}' -f $index), $font, [Drawing.Brushes]::White, 45, 72)
            $bitmap.Save($path, [Drawing.Imaging.ImageFormat]::Png)
        } finally { $graphics.Dispose(); $brush.Dispose(); $font.Dispose(); $bitmap.Dispose() }
        $entries += [ordered]@{ RelativePath = ($group + '/' + $name); SHA256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; ShouldConvert = $true }
    }
    if ($group -eq 'partial-failure') {
        $broken = Join-Path $directory 'ملف-تالف.png'
        [IO.File]::WriteAllText($broken, 'Deliberately corrupt local PNG fixture for partial-failure acceptance.')
        $entries += [ordered]@{ RelativePath = ($group + '/ملف-تالف.png'); SHA256 = (Get-FileHash -LiteralPath $broken -Algorithm SHA256).Hash; ShouldConvert = $false }
    }
}
[ordered]@{ CreatedUtc = [DateTime]::UtcNow.ToString('o'); ExpectedWidth = 256; ExpectedHeight = 192; Files = $entries; ActualExplorerDragVerified = $false } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destinationPath 'sources.json') -Encoding UTF8
Write-Output $destinationPath
Write-Output 'Fixtures created only. No Explorer drag or application conversion has been performed.'
