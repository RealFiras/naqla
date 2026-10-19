param([Parameter(Mandatory=$true)][string]$Directory, [ValidateSet('10-images','partial-failure')][string]$Group = '10-images')
$ErrorActionPreference = 'Stop'
$fixtureRoot = [IO.Path]::GetFullPath($Directory)
$manifest = Get-Content -LiteralPath (Join-Path $fixtureRoot 'sources.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Add-Type -AssemblyName System.Drawing
$rows = foreach ($source in $manifest.Files | Where-Object { $_.RelativePath.StartsWith($Group + '/') }) {
    $path = Join-Path $fixtureRoot $source.RelativePath
    $output = [IO.Path]::ChangeExtension($path, '.jpg')
    $sameHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $source.SHA256
    $decoded = $false
    if (Test-Path -LiteralPath $output) {
        $bitmap = New-Object Drawing.Bitmap($output)
        try {
            $corner = $bitmap.GetPixel(0,0)
            $decoded = $bitmap.Width -eq $manifest.ExpectedWidth -and $bitmap.Height -eq $manifest.ExpectedHeight -and $corner.R -gt 240 -and $corner.G -gt 240 -and $corner.B -gt 240
        } finally { $bitmap.Dispose() }
    }
    [pscustomobject]@{ Source = [IO.Path]::GetFileName($path); OriginalUnchanged = $sameHash; ExpectedOutput = $source.ShouldConvert; OutputExists = (Test-Path -LiteralPath $output); DecodeAndMattePassed = $decoded }
}
$passed = @($rows | Where-Object { !$_.OriginalUnchanged -or ($_.ExpectedOutput -and !$_.DecodeAndMattePassed) -or (!$_.ExpectedOutput -and $_.OutputExists) }).Count -eq 0
$report = [ordered]@{ Group = $Group; DiskEvidencePassed = $passed; SourceCount = @($rows).Count; ActualExplorerDragVerified = $false; Note = 'File evidence only. Confirm the actual Explorer drag with the tester and its diagnostic session.'; Files = @($rows) }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $fixtureRoot ($Group + '-verification.json')) -Encoding UTF8
$report | ConvertTo-Json -Depth 5
if (!$passed) { throw 'Expected outputs/original hashes did not pass. See the per-file report.' }
