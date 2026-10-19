param([string]$Destination = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\media-engine'))
$ErrorActionPreference = 'Stop'
$version = '9.0.2'
$expectedHash = '60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba'
$url = 'https://www.gyan.dev/ffmpeg/builds/packages/ffmpeg-9.0.2-essentials_build.zip'
$root = Split-Path -Parent $PSScriptRoot
$cache = Join-Path $root 'artifacts\downloads'
New-Item -ItemType Directory -Path $cache -Force | Out-Null
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$archive = Join-Path $cache ('ffmpeg-' + $version + '-essentials.zip')
if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
    $ProgressPreference = 'SilentlyContinue'
    $partial = $archive + '.partial'
    curl.exe --fail --location --silent --show-error --max-time 240 --output $partial $url
    if ($LASTEXITCODE -ne 0) { throw 'Media engine download failed.' }
    if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $expectedHash) { throw 'Downloaded media engine checksum mismatch.' }
    Move-Item -LiteralPath $partial -Destination $archive -Force
}
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'FFmpeg archive checksum mismatch. Delete only the cached archive and retry.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($name in @('ffmpeg.exe','ffprobe.exe','LICENSE')) {
        $entry = @($zip.Entries | Where-Object { $_.Name -eq $name })[0]
        if (!$entry) { throw "Missing media engine file: $name" }
        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry, (Join-Path $Destination $name), $true)
    }
} finally { $zip.Dispose() }
@"
FFmpeg $version, essentials build by Gyan Doshi (unmodified separate executables).
Binary archive: $url
SHA256: $expectedHash
Build information: https://www.gyan.dev/ffmpeg/builds/
Corresponding source: https://github.com/FFmpeg/FFmpeg/tree/n$version
Build recipe and external-library source links: https://github.com/GyanD/codexffmpeg
License: GNU GPL v3; see LICENSE. Naqla invokes the separate command-line programs.
Runtime is entirely local; no network download occurs in the installed app.
"@ | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $Destination 'NOTICE.txt')
Write-Host "Verified FFmpeg $version in $Destination"
