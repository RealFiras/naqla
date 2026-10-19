param([string]$Destination = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\heif-engine'))
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $PSScriptRoot 'heif-engine.lock.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$cache = Join-Path $root 'artifacts\downloads\heif'
$staging = Join-Path $root 'artifacts\heif-staging'
New-Item -ItemType Directory -Path $cache,$staging,$Destination -Force | Out-Null
Push-Location -LiteralPath $root
try {
    foreach ($package in $manifest.packages) {
        $name = [IO.Path]::GetFileName(([Uri]$package.url).AbsolutePath)
        $archive = Join-Path $cache $name
        if (!(Test-Path -LiteralPath $archive) -or (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.sha256) {
            $partial = $archive + '.partial'
            curl.exe --fail --silent --show-error --location --max-time 120 --output $partial $package.url
            if ($LASTEXITCODE -ne 0) { throw ('HEIF package download failed: ' + $package.name) }
            if ((Get-FileHash -LiteralPath $partial -Algorithm SHA256).Hash -ne $package.sha256) { throw 'HEIF package checksum mismatch.' }
            Move-Item -LiteralPath $partial -Destination $archive -Force
        }
        # ASCII relative argv supports Windows tar builds which narrow non-ASCII absolute paths.
        tar.exe -xf ('artifacts/downloads/heif/' + $name) -C 'artifacts/heif-staging' '--exclude=ucrt64/include' '--exclude=ucrt64/lib' '--exclude=ucrt64/share/man' '--exclude=ucrt64/share/doc'
        if ($LASTEXITCODE -ne 0) { throw 'HEIF extraction failed. Developer tar must support Zstandard.' }
    }
    foreach ($file in $manifest.files) {
        $source = Join-Path $staging ('ucrt64\bin\' + $file.name)
        if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $file.sha256) { throw ('HEIF native file checksum mismatch: ' + $file.name) }
        Copy-Item -LiteralPath $source -Destination (Join-Path $Destination $file.name) -Force
    }
    Copy-Item -LiteralPath (Join-Path $staging 'ucrt64\share\licenses') -Destination $Destination -Recurse -Force
    # The MSYS2 x265 binary archive omits COPYING; retain the upstream 4.3 notice locally.
    $x265Notice = Join-Path $root 'assets\licenses\heif\x265\COPYING'
    if ((Get-FileHash -LiteralPath $x265Notice -Algorithm SHA256).Hash -ne 'D8AFB1BCC7A2CFC603683B168D6987EF0A48E59E0DA3693BF55C5D33B67E2B49') { throw 'x265 license notice checksum mismatch.' }
    $x265Directory = Join-Path $Destination 'licenses\x265'
    New-Item -ItemType Directory -Path $x265Directory -Force | Out-Null
    Copy-Item -LiteralPath $x265Notice -Destination $x265Directory -Force
    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $Destination 'packages.lock.json') -Force
    @"
Naqla HEIC engine: unmodified libheif CLI tools and native dependencies from MSYS2 UCRT64.
No MSYS2 installation, development tools, administrator rights or internet are required at runtime.
Packages, SHA256 values and corresponding source archive URLs: packages.lock.json.
Package build recipes: https://github.com/msys2/MINGW-packages
Upstream: https://github.com/strukturag/libheif
Includes libheif LGPL-3.0 and x265 GPL-2.0-or-later; see licenses for bundled component notices.
x265 COPYING: https://raw.githubusercontent.com/Multicorewareinc/x265/4.3/COPYING
The separate heif-enc program is invoked only for an explicitly selected local image operation.
"@ | Set-Content -LiteralPath (Join-Path $Destination 'NOTICE.txt') -Encoding UTF8
    Write-Host ('Verified HEIF engine: ' + $manifest.files.Count + ' native files.')
} finally { Pop-Location }
