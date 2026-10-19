param([string]$Runtime = 'win-x64', [switch]$RunAcceptance)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
Push-Location -LiteralPath $projectRoot
try {
    if ($Runtime -ne 'win-x64') { throw 'This reviewed development package supports win-x64 only.' }
    if ($RunAcceptance) {
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $runner = Join-Path $projectRoot ('artifacts\qa-release-' + $stamp)
        $env:NAQLA_FULL_ACCEPTANCE = 'release-acceptance-' + $stamp
        $acceptancePublish = Join-Path $projectRoot 'artifacts\app-0.8-win-x64'
        if (Get-Process -Name 'Naqla.App' -ErrorAction SilentlyContinue) { throw 'Exit Naqla before release acceptance.' }
        dotnet publish src/Naqla.App -c Release -r $Runtime --self-contained true -o $acceptancePublish -p:BaseOutputPath=bin/Publish08/ -p:PublishSingleFile=false -p:PublishTrimmed=false
        if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
        py -3 scripts/audit-native-closure.py
        if ($LASTEXITCODE -ne 0) { throw 'Native import audit failed.' }
        py -3 scripts/verify-portable-lifecycle.py
        if ($LASTEXITCODE -ne 0) { throw 'Portable lifecycle failed.' }
        $osInfo = Get-CimInstance Win32_OperatingSystem; $cpuInfo = Get-CimInstance Win32_Processor | Select-Object -First 1
        [ordered]@{ Windows=$osInfo.Caption; Version=$osInfo.Version; Build=$osInfo.BuildNumber; Cpu=$cpuInfo.Name; LogicalProcessors=[Environment]::ProcessorCount; PhysicalMemoryBytes=(Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory } | ConvertTo-Json | Set-Content -LiteralPath artifacts/environment.json -Encoding UTF8
        dotnet publish tests/Naqla.Tests -c Release -r $Runtime --self-contained true -o $runner -p:BaseOutputPath=bin/RuntimeAcceptance/ -p:PublishSingleFile=false
        if ($LASTEXITCODE -ne 0) { throw 'Acceptance runner publish failed.' }
        $env:NAQLA_PERF_RESULTS = Join-Path $projectRoot ('artifacts\performance-' + $stamp)
        py -3 scripts/profile-workloads.py (Join-Path $runner 'Naqla.Tests.exe') $env:NAQLA_PERF_RESULTS
        if ($LASTEXITCODE -ne 0) { throw 'Measured workloads failed.' }
        & (Join-Path $runner 'Naqla.Tests.exe') (Join-Path $projectRoot ('artifacts\' + $env:NAQLA_FULL_ACCEPTANCE)) --release-acceptance
        if ($LASTEXITCODE -ne 0) { throw 'Acceptance failed; installer was not built.' }
        & (Join-Path $PSScriptRoot 'update-pdf-previews.ps1') -TestResults (Join-Path $projectRoot ('artifacts\' + $env:NAQLA_FULL_ACCEPTANCE))
        py -3 scripts/create-editor-gallery.py
        if ($LASTEXITCODE -ne 0) { throw 'Editor gallery failed.' }
        py -3 scripts/update-testing-guide.py
        if ($LASTEXITCODE -ne 0) { throw 'HTML testing guide failed.' }
        py -3 scripts/write-performance-report.py
        if ($LASTEXITCODE -ne 0) { throw 'Performance report failed.' }
        py -3 scripts/finalize-development-report.py
        if ($LASTEXITCODE -ne 0) { throw 'Evidence report failed.' }
    }
    $proof = Get-Content -LiteralPath artifacts/final-verification.json -Raw -Encoding UTF8 | ConvertFrom-Json
    if (!$proof.ReadyForPackaging -or $proof.Build -ne '0.8.0-development.1' -or $proof.BundledRuntime -ne '10.0.13') { throw 'A current passing release acceptance report is required.' }
    foreach ($source in $proof.ProductionSourceSha256) {
        if ((Get-FileHash -LiteralPath (Join-Path $projectRoot $source.path) -Algorithm SHA256).Hash -ne $source.sha256) { throw ('Source changed after acceptance/report: ' + $source.path) }
    }
    $publish = Join-Path $projectRoot 'artifacts\app-0.8-win-x64'
    $active = @(Get-Process -Name 'Naqla.App' -ErrorAction SilentlyContinue)
    if ($active.Count) { throw 'Exit Naqla before rebuilding its development payload.' }
    dotnet publish src/Naqla.App -c Release -r $Runtime --self-contained true -o $publish -p:BaseOutputPath=bin/Publish08/ -p:PublishSingleFile=false -p:PublishTrimmed=false
    if ($LASTEXITCODE -ne 0) { throw 'Application publish failed.' }
    # Remove only unused build assets from this exact owned x64 publication directory.
    $x86 = [IO.Path]::GetFullPath((Join-Path $publish 'x86'))
    if (!$x86.StartsWith($publish + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Invalid x86 cleanup target.' }
    if (Test-Path -LiteralPath $x86) {
        if ((Get-Item -LiteralPath $x86).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'The x86 output directory must not be a link.' }
        foreach ($name in @('leptonica-1.82.0.dll','tesseract50.dll')) { $unused = Join-Path $x86 $name; if (Test-Path -LiteralPath $unused) { Remove-Item -LiteralPath $unused } }
        if ([IO.Directory]::GetFileSystemEntries($x86).Length -eq 0) { Remove-Item -LiteralPath $x86 }
    }
    foreach ($file in Get-ChildItem -LiteralPath $publish -File -Recurse | Where-Object { $_.Extension -in @('.pdb','.lib') }) { Remove-Item -LiteralPath $file.FullName }
    foreach ($guide in @('README.md','README.ar.md','PROGRESS.md','FEATURE-AUDIT.md','THIRD_PARTY_NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot $guide) -Destination $publish -Force }
    $publishedDocs = Join-Path $publish 'docs'; New-Item -ItemType Directory -Path $publishedDocs -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'docs') -File | Where-Object { $_.Extension -eq '.md' -and $_.Name -notlike '*.initial.*' } | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $publishedDocs -Force }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\TESTING.html') -Destination $publish -Force
    $rootGuidePath = Join-Path $publish 'TESTING.html'
    $rootGuideContent = [IO.File]::ReadAllText($rootGuidePath).Replace('href="../preview/', 'href="preview/').Replace('href="DIAGNOSTICS.md"', 'href="docs/DIAGNOSTICS.md"').Replace('href="WINDOWS-INTEGRATION.md"', 'href="docs/WINDOWS-INTEGRATION.md"').Replace('href="END-TO-END-TEST-REPORT.md"', 'href="docs/END-TO-END-TEST-REPORT.md"')
    [IO.File]::WriteAllText($rootGuidePath, $rootGuideContent, (New-Object Text.UTF8Encoding($false)))
    foreach ($rootGuide in @('BATCH-TESTING.md','ADVANCED-PLAN.md','SMART-RENAME.md','TARGET-SIZE.md','PDF-TOOLKIT.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot ('docs\' + $rootGuide)) -Destination $publish -Force }
    $rootPdf = Join-Path $publish 'PDF-TOOLKIT.md'
    [IO.File]::WriteAllText($rootPdf, ([IO.File]::ReadAllText($rootPdf)).Replace('../preview/', 'preview/'), (New-Object Text.UTF8Encoding($false)))
    Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\DESKTOP-TESTS.md') -Destination $publish -Force
    $publishedPreview = Join-Path $publish 'preview'; New-Item -ItemType Directory -Path $publishedPreview -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $projectRoot 'preview') | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $publishedPreview -Recurse -Force }
    $publishedTesting = Join-Path $publish 'testing'; New-Item -ItemType Directory -Path $publishedTesting -Force | Out-Null
    foreach ($helper in @('prepare-target-size-desktop-test.ps1','verify-target-size-desktop-test.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $helper) -Destination $publishedTesting -Force }
    $payload = Join-Path $projectRoot 'artifacts\payload.zip'
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $payload) { Remove-Item -LiteralPath $payload }
    [IO.Compression.ZipFile]::CreateFromDirectory($publish, $payload)
    $setupOutput = Join-Path $projectRoot 'artifacts\setup-0.8'
    dotnet publish src/Naqla.Setup -c Release -r $Runtime --self-contained true -o $setupOutput -p:BaseOutputPath=bin/Publish08/ -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:PublishTrimmed=false
    if ($LASTEXITCODE -ne 0) { throw 'Installer publish failed.' }
    $installer = Join-Path $projectRoot 'artifacts\Naqla-Dev-Setup-win-x64.exe'
    Copy-Item -LiteralPath (Join-Path $setupOutput 'Naqla.Setup.exe') -Destination $installer -Force
    Get-FileHash -LiteralPath $installer -Algorithm SHA256
    Write-Output 'Development installer created. Run verify-diagnostic-package.ps1 for extraction/process smoke; clean-PC and physical drag checks remain manual.'
} finally { Pop-Location }
