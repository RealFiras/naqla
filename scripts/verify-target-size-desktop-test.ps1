param([Parameter(Mandatory=$true)][string]$Directory, [ValidateSet('images','partial-failure','unattainable')][string]$Group = 'images', [long]$TargetBytes = 102400, [ValidateSet('jpg','png','tiff')][string]$Extension = 'jpg', [switch]$ExpectNoOutput)
$ErrorActionPreference = 'Stop'
if ($TargetBytes -lt 1) { throw 'TargetBytes must be positive.' }
$fixtureRoot = [IO.Path]::GetFullPath($Directory)
$manifest = Get-Content -LiteralPath (Join-Path $fixtureRoot 'sources.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Add-Type -AssemblyName System.Drawing
$rows = @(foreach ($source in $manifest.Files | Where-Object { $_.RelativePath.StartsWith($Group + '/') }) {
    $path = [IO.Path]::GetFullPath((Join-Path $fixtureRoot $source.RelativePath))
    if (!$path.StartsWith($fixtureRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Fixture entry is outside its root.' }
    $output = Join-Path ([IO.Path]::GetDirectoryName($path)) ([IO.Path]::GetFileNameWithoutExtension($path) + ' optimized.' + $Extension)
    $sameHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $source.SHA256
    $exists = Test-Path -LiteralPath $output -PathType Leaf
    $bytes = if ($exists) { (Get-Item -LiteralPath $output).Length } else { 0 }
    $decoded = $false
    if ($exists) {
        $image = New-Object Drawing.Bitmap($output)
        try {
            $pixel = $image.GetPixel(0, 128)
            $matte = if ($Extension -eq 'jpg') { $pixel.R -gt 240 -and $pixel.G -gt 240 -and $pixel.B -gt 240 } else { $pixel.A -lt 5 }
            $decoded = $image.Width -eq $manifest.Width -and $image.Height -eq $manifest.Height -and $matte
        } finally { $image.Dispose() }
    }
    $expected = $source.ValidImage -and !$ExpectNoOutput
    [pscustomobject]@{ Source = [IO.Path]::GetFileName($path); OriginalUnchanged = $sameHash; ExpectedOutput = $expected; OutputExists = $exists; OutputBytes = $bytes; TargetMet = $exists -and $bytes -gt 0 -and $bytes -le $TargetBytes; DecodedDimensionsAndAlpha = $decoded }
})
$passed = $rows.Count -gt 0 -and @($rows | Where-Object { !$_.OriginalUnchanged -or ($_.ExpectedOutput -and (!$_.TargetMet -or !$_.DecodedDimensionsAndAlpha)) -or (!$_.ExpectedOutput -and $_.OutputExists) }).Count -eq 0
$report = [ordered]@{ Group = $Group; TargetBytes = $TargetBytes; DiskEvidencePassed = $passed; SourceCount = $rows.Count; ActualExplorerDragVerified = $false; Note = 'Disk evidence only. Obtain the tester report and marked diagnostic session for real Explorer acceptance.'; Files = $rows }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $fixtureRoot ($Group + '-verification.json')) -Encoding UTF8
$report | ConvertTo-Json -Depth 5
if (!$passed) { throw 'Original hashes, real decode or strict byte limits failed. Use fresh fixtures for the first run.' }
