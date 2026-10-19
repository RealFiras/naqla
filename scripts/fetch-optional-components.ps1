param([ValidateSet('speech','vision','archives','licenses')][string]$Group = 'licenses')
# Explicit development fetch only. Never called by the app or installed runtime.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $root 'artifacts\optional-components'
$assets = Get-Content -LiteralPath (Join-Path $root 'artifacts\optional-fetch-list.json') -Raw | ConvertFrom-Json
$records = @()
foreach ($asset in $assets) {
    $name = [string]$asset[0]
    $matchesGroup = switch ($Group) {
        'speech' { $name -in @('whisper-bin-x64.zip','ggml-tiny.bin','fixtures/jfk.wav') }
        'vision' { $name -eq 'u2netp.onnx' -or $name.StartsWith('tessdata/') }
        'archives' { $name -eq '7z2604-extra.7z' }
        'licenses' { $name.StartsWith('licenses/') }
    }
    if (!$matchesGroup) { continue }
    $path = Join-Path $dest $name
    New-Item -ItemType Directory -Path (Split-Path -Parent $path) -Force | Out-Null
    if (!(Test-Path -LiteralPath $path)) {
        $temp = $path + '.psdownload'
        try { Invoke-WebRequest -UseBasicParsing -Uri ([string]$asset[1]) -TimeoutSec 120 -OutFile $temp; Move-Item -LiteralPath $temp -Destination $path }
        finally { if (Test-Path -LiteralPath $temp) { Remove-Item -LiteralPath $temp } }
    }
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($asset[2] -and $hash -ne $asset[2]) { throw "Pinned checksum mismatch: $name" }
    $records += [ordered]@{ File=$name; Url=$asset[1]; Sha256=$hash; Bytes=(Get-Item -LiteralPath $path).Length; UpstreamPinnedDigest=[bool]$asset[2] }
    Write-Output "$name verified"
}
$records | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $dest ('sources-' + $Group + '.json')) -Encoding UTF8
