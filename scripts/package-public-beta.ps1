param([ValidatePattern('^v\d+\.\d+\.\d+-beta\.\d+$')][string]$Tag = 'v0.8.0-beta.1')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location -LiteralPath $projectRoot
try {
    $output = Join-Path $projectRoot ('artifacts\public-release\' + $Tag + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    if (Test-Path -LiteralPath $output) { throw 'Use a new owned publication directory.' }
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $publish = Join-Path $output 'app'
    $catalog = Join-Path $projectRoot 'artifacts\public-components\component-catalog.json'
    if(!(Test-Path -LiteralPath $catalog)) { throw 'Run scripts/prepare-public-components.py first.' }
    dotnet publish src/Naqla.App -c Release -r win-x64 --self-contained true -o $publish -p:NaqlaPublicRelease=true ("-p:NaqlaComponentCatalogPath=" + $catalog) -p:BaseOutputPath=bin/PublicBeta/ -p:PublishSingleFile=false -p:PublishTrimmed=false
    if ($LASTEXITCODE -ne 0) { throw 'Public application publish failed.' }
    # This payload excludes developer-provided Microsoft CRT files and GPL CLI engines.
    # Explicit setup downloads obtain the latter directly from upstream, with pinned hashes.
    $crt = Get-Content -Raw -Encoding UTF8 assets/native-crt/sources.json | ConvertFrom-Json
    foreach ($item in $crt.Files) { if (Test-Path -LiteralPath (Join-Path $publish $item.File)) { throw ('Unexpected app-local proprietary runtime: ' + $item.File) } }
    foreach ($engine in @('media-engine','heif-engine')) { if(Test-Path -LiteralPath (Join-Path $publish $engine)) { throw 'The public payload must not contain upstream CLI engines.' } }
    foreach ($file in Get-ChildItem -LiteralPath $publish -Recurse -File | Where-Object { $_.Extension -in @('.pdb','.lib') }) { Remove-Item -LiteralPath $file.FullName }
    foreach ($guide in @('README.md','README.ar.md','LICENSE','THIRD_PARTY_NOTICES.md','FEATURE-AUDIT.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot $guide) -Destination $publish }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs') -Destination $publish -Recurse
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $projectRoot 'preview')) { Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $publish 'preview') -Recurse -Force }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\TESTING.html') -Destination $publish -Force
    # The tray opens this guide beside the executable; retain working relative links.
    $guidePath = Join-Path $publish 'TESTING.html'
    $html = [IO.File]::ReadAllText($guidePath).Replace('href="../preview/', 'href="preview/')
    foreach ($name in @('END-TO-END-TEST-REPORT.md','DIAGNOSTICS.md','WINDOWS-INTEGRATION.md')) { $html = $html.Replace(('href="' + $name + '"'), ('href="docs/' + $name + '"')) }
    [IO.File]::WriteAllText($guidePath,$html,(New-Object Text.UTF8Encoding($false)))
    $payload = Join-Path $output 'payload.zip'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($publish,$payload)
    $setupOutput = Join-Path $output 'setup'
    dotnet publish src/Naqla.Setup -c Release -r win-x64 --self-contained true -o $setupOutput -p:NaqlaPublicRelease=true ("-p:NaqlaPayloadPath=" + $payload) -p:BaseOutputPath=bin/PublicBeta/ -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false
    if ($LASTEXITCODE -ne 0) { throw 'Public installer publish failed.' }
    $assetName = 'Naqla-Setup-' + $Tag.Substring(1) + '-win-x64.exe'
    $asset = Join-Path $output $assetName
    Copy-Item -LiteralPath (Join-Path $setupOutput 'Naqla.Setup.exe') -Destination $asset
    $hash = (Get-FileHash -LiteralPath $asset -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::WriteAllText((Join-Path $output 'SHA256SUMS.txt'), ($hash + '  ' + $assetName + "`n"), (New-Object Text.UTF8Encoding($false)))
    [ordered]@{ Tag=$Tag; Installer=$assetName; SHA256=$hash; Bytes=(Get-Item -LiteralPath $asset).Length; BundledRuntime='10.0.13'; OneTimeEngineDownload=$true; RequiredMicrosoftRuntime='Visual C++ x64 (official Microsoft prerequisite)'; Stable=$false } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'build.json') -Encoding UTF8
    $output | Set-Content -LiteralPath artifacts/public-release-path.txt -Encoding UTF8
    Write-Output ('Built public beta installer: ' + $assetName)
    Write-Output ('SHA256: ' + $hash)
} finally { Pop-Location }
