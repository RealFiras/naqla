param([string]$Destination = (Join-Path (Split-Path -Parent $PSScriptRoot) ('artifacts\desktop-target-size-test-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))))
$ErrorActionPreference = 'Stop'
$fixtureRoot = [IO.Path]::GetFullPath($Destination)
if (Test-Path -LiteralPath $fixtureRoot) { throw 'Choose a fresh fixture folder. Existing files are never replaced.' }
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class NaqlaTargetSizeFixture {
    public static void Write(string path, int seed) {
        using (var image = new Bitmap(384, 256, PixelFormat.Format32bppArgb)) {
            var random = new Random(seed);
            for (int y = 0; y < 256; y++) for (int x = 0; x < 384; x++)
                image.SetPixel(x, y, Color.FromArgb(x < 16 ? 0 : 255, random.Next(256), random.Next(256), random.Next(256)));
            image.Save(path, ImageFormat.Png);
        }
    }
}
'@
$entries = @()
foreach ($group in @('images','partial-failure','unattainable')) {
    $directory = Join-Path $fixtureRoot $group
    New-Item -ItemType Directory -Path $directory | Out-Null
    $count = if ($group -eq 'images') { 3 } else { 1 }
    for ($index = 1; $index -le $count; $index++) {
        $name = ('صورة-{0:D2}.png' -f $index)
        $path = Join-Path $directory $name
        [NaqlaTargetSizeFixture]::Write($path, (44551 + $index))
        $entries += [ordered]@{ RelativePath = ($group + '/' + $name); SHA256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash; ValidImage = $true }
    }
    if ($group -eq 'partial-failure') {
        $broken = Join-Path $directory '00-corrupt.png'
        [IO.File]::WriteAllText($broken, 'Deliberately corrupt PNG for actual partial-failure acceptance.')
        $entries += [ordered]@{ RelativePath = ($group + '/00-corrupt.png'); SHA256 = (Get-FileHash -LiteralPath $broken -Algorithm SHA256).Hash; ValidImage = $false }
    }
}
[ordered]@{ CreatedUtc = [DateTime]::UtcNow.ToString('o'); Width = 384; Height = 256; Files = $entries; ActualExplorerDragVerified = $false } |
    ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $fixtureRoot 'sources.json') -Encoding UTF8
Write-Output $fixtureRoot
Write-Output 'Real PNG fixtures only. No Explorer drag, optimization or desktop acceptance has occurred.'
