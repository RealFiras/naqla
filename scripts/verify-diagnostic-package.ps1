param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifactDirectory = Join-Path $root ('artifacts\diagnostic-package-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$installer = Join-Path $root ('artifacts\Naqla-Dev-Setup-' + $Runtime + '.exe')
if (Get-Process -Name 'Naqla.App' -ErrorAction SilentlyContinue) { throw 'Exit the running Naqla app before this isolated process smoke test.' }
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$extractedDirectory = Join-Path $artifactDirectory 'payload'
$setup = Start-Process -FilePath $installer -ArgumentList ('--extract-only "{0}"' -f $extractedDirectory) -WindowStyle Hidden -PassThru
if (!$setup.WaitForExit(60000) -or $setup.ExitCode -ne 0) { throw 'Installer extraction failed or timed out.' }
$publishDirectory = Join-Path $root 'artifacts\app-0.8-win-x64'
$files = @(Get-ChildItem -LiteralPath $publishDirectory -File -Recurse)
$mismatches = @()
foreach ($file in $files) {
    $relative = $file.FullName.Substring($publishDirectory.Length + 1)
    $target = Join-Path $extractedDirectory $relative
    if (!(Test-Path -LiteralPath $target) -or (Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) { $mismatches += $relative }
}
if ($mismatches.Count -gt 0) { throw 'Extracted payload differs from the published app.' }
$heifLock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'heif-engine.lock.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($native in $heifLock.files) {
    $nativePath = Join-Path $extractedDirectory ('heif-engine\' + $native.name)
    if (!(Test-Path -LiteralPath $nativePath) -or (Get-FileHash -LiteralPath $nativePath -Algorithm SHA256).Hash -ne $native.sha256) { throw ('Packaged HEIF native hash mismatch: ' + $native.name) }
}
# Exercise the extracted native dependency closure without adding anything to PATH.
# Only ASCII relative image names go through native argv; .NET sets the Unicode cwd.
function Invoke-BundledNative([string]$FilePath, [string]$Arguments, [string]$WorkingDirectory) {
    $info = New-Object System.Diagnostics.ProcessStartInfo
    $info.FileName = $FilePath; $info.Arguments = $Arguments; $info.WorkingDirectory = $WorkingDirectory
    $info.UseShellExecute = $false; $info.CreateNoWindow = $true; $info.WindowStyle = 'Hidden'
    $info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
    $nativeProcess = [Diagnostics.Process]::Start($info)
    try {
        $stdout = $nativeProcess.StandardOutput.ReadToEndAsync(); $stderr = $nativeProcess.StandardError.ReadToEndAsync()
        if (!$nativeProcess.WaitForExit(30000)) { $nativeProcess.Kill(); throw 'Packaged HEIF smoke timed out.' }
        $stdout.GetAwaiter().GetResult() | Out-Null; $stderr.GetAwaiter().GetResult() | Out-Null
        if ($nativeProcess.ExitCode -ne 0) { throw 'Packaged HEIF smoke failed.' }
    } finally { $nativeProcess.Dispose() }
}
$heicWork = Join-Path $artifactDirectory 'heic-smoke'
New-Item -ItemType Directory -Path $heicWork -Force | Out-Null
Add-Type -AssemblyName System.Drawing
$fixture = New-Object System.Drawing.Bitmap(32, 24)
try {
    for ($y = 0; $y -lt 24; $y++) { for ($x = 0; $x -lt 32; $x++) {
        $fixture.SetPixel($x, $y, [Drawing.Color]::FromArgb($(if ($x -lt 8) { 0 } else { 255 }), 220, 40, 30))
    } }
    $fixture.Save((Join-Path $heicWork 'input.png'), [Drawing.Imaging.ImageFormat]::Png)
} finally { $fixture.Dispose() }
Invoke-BundledNative (Join-Path $extractedDirectory 'heif-engine\heif-enc.exe') '--hevc -e x265 -q 92 -p x265:pools=2 -p x265:frame-threads=2 -p x265:ctu=16 -p x265:log-level=error -o output.heic input.png' $heicWork
Invoke-BundledNative (Join-Path $extractedDirectory 'heif-engine\heif-dec.exe') 'output.heic decoded.png' $heicWork
$decoded = New-Object System.Drawing.Bitmap((Join-Path $heicWork 'decoded.png'))
try {
    if ($decoded.Width -ne 32 -or $decoded.Height -ne 24 -or $decoded.GetPixel(0, 12).A -gt 5 -or $decoded.GetPixel(24, 12).A -lt 250 -or $decoded.GetPixel(24, 12).R -lt 180) { throw 'Packaged HEIC roundtrip pixels/dimensions failed.' }
} finally { $decoded.Dispose() }
$executable = Join-Path $extractedDirectory 'Naqla.App.exe'
$first = $null
try {
    $first = Start-Process -FilePath $executable -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $first.Refresh()
    if ($first.HasExited) { throw 'The diagnostic app did not remain running.' }
    $mainWindow = $first.MainWindowHandle.ToInt64()
    $runtimeModules = @($first.Modules | Where-Object ModuleName -eq 'coreclr.dll' | ForEach-Object FileName)
    $idleImageModules = @($first.Modules | Where-Object ModuleName -Match 'Magick\.Native')
    $cpuBefore = $first.TotalProcessorTime.TotalMilliseconds
    $second = Start-Process -FilePath $executable -WindowStyle Hidden -PassThru
    $secondExited = $second.WaitForExit(5000)
    Start-Sleep -Seconds 3
    $first.Refresh()
    $idleCpu = [math]::Round($first.TotalProcessorTime.TotalMilliseconds - $cpuBefore, 2)
    $workingSet = [math]::Round($first.WorkingSet64 / 1MB, 2)
    $quit = Start-Process -FilePath $executable -ArgumentList '--quit' -WindowStyle Hidden -PassThru
    $quit.WaitForExit(5000) | Out-Null
    if (!$first.WaitForExit(10000)) { throw 'Graceful quit timed out.' }
    $logDirectory = Join-Path $env:LOCALAPPDATA 'Naqla\Diagnostics'
    $latest = Get-ChildItem -LiteralPath $logDirectory -Filter 'session-*.jsonl' -File | Sort-Object Name -Descending | Select-Object -First 1
    $lines = @(Get-Content -LiteralPath $latest.FullName -Encoding UTF8 | ForEach-Object { $_ | ConvertFrom-Json })
    $events = @($lines | Where-Object { $_.PSObject.Properties.Name -contains 'Event' } | ForEach-Object Event)
    $header = $lines[0]
    $required = @('AppStarting','MouseHookInstalled','KeyboardHookInstalled','OleAllowDropEnabled','TrayVisibleRequested','AppReady','AppExitRequested','HooksReleased','OleAllowDropDisabled','TrayRemoved','AppStopped')
    foreach ($eventName in $required) { if ($events -notcontains $eventName) { throw ('Missing lifecycle event: ' + $eventName) } }
    $released = @($lines | Where-Object Event -eq 'HooksReleased')
    if (!$released[-1].Data.Succeeded) { throw 'Native hook cleanup did not report success.' }
    $tray = @($lines | Where-Object Event -eq 'TrayVisibleRequested')
    if (!$tray[-1].Data.Visible) { throw 'NotifyIcon.Visible was false.' }
    $rawLog = [System.IO.File]::ReadAllText($latest.FullName)
    if ($rawLog.Contains($env:USERPROFILE) -or $rawLog.Contains($extractedDirectory)) { throw 'Private installation path appeared in the diagnostic log.' }
    if ($header.Build -ne '0.8.0-development.1') { throw 'Wrong app build.' }
    if ($idleImageModules.Count -ne 0) { throw 'Heavy image native engine was loaded at idle startup.' }
    foreach ($rootGuide in @('BATCH-TESTING.md','ADVANCED-PLAN.md','SMART-RENAME.md','TARGET-SIZE.md','PDF-TOOLKIT.md','FEATURE-AUDIT.md')) {
        if (!(Test-Path -LiteralPath (Join-Path $extractedDirectory $rootGuide))) { throw 'Missing offline guide link target.' }
    }
    foreach ($requiredFile in @('TESTING.html','docs\MANUAL-TEST-AR.md','docs\DIAGNOSTICS.md','docs\CONVERSIONS.md','docs\BATCH-TESTING.md','docs\ADVANCED-PLAN.md','docs\SMART-RENAME.md','docs\FEATURE-AUDIT.md','docs\PLUGIN-THREAT-MODEL.md','preview\index.html','media-engine\ffmpeg.exe','media-engine\ffprobe.exe','media-engine\LICENSE','media-engine\NOTICE.txt','heif-engine\heif-enc.exe','heif-engine\heif-dec.exe','heif-engine\NOTICE.txt','heif-engine\packages.lock.json','heif-engine\licenses\libheif\COPYING','heif-engine\licenses\x265\COPYING')) {
        if (!(Test-Path -LiteralPath (Join-Path $extractedDirectory $requiredFile))) { throw ('Missing guide/preview: ' + $requiredFile) }
    }
    foreach ($optimizerGuide in @('docs\TARGET-SIZE.md','testing\prepare-target-size-desktop-test.ps1','testing\verify-target-size-desktop-test.ps1')) {
        if (!(Test-Path -LiteralPath (Join-Path $extractedDirectory $optimizerGuide))) { throw 'Missing optimizer acceptance guide/helper.' }
    }
    foreach ($pdfFile in @('PdfSharp-wpf.dll','docs\PDF-TOOLKIT.md','preview\pdf-toolkit.html','preview\pdf-toolkit\reorder.png','preview\pdf-toolkit\watermark.png','testing\pdf-fixtures\source-a.pdf','testing\pdf-fixtures\source-b.pdf','testing\pdf-fixtures\encrypted.pdf','testing\pdf-fixtures\sources.json','licenses\PDFsharp-6.2.4-LICENSE.txt','licenses\microsoft.extensions.logging.abstractions-LICENSE.txt','licenses\microsoft.extensions.dependencyinjection.abstractions-LICENSE.txt')) {
        if (!(Test-Path -LiteralPath (Join-Path $extractedDirectory $pdfFile))) { throw ('Missing PDF runtime/preview/license/fixture: ' + $pdfFile) }
    }
    if (([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $extractedDirectory 'PdfSharp-wpf.dll'))).ProductVersion -notlike '6.2.4*') { throw 'The PDFsharp version differs from the reviewed MIT version.' }
    foreach ($unusedPdfAssembly in @('PdfSharp.WPFonts.dll','PdfSharp.Snippets-wpf.dll','PdfSharp.Quality-wpf.dll','PdfSharp.BarCodes-wpf.dll','PdfSharp.Charting-wpf.dll','PdfSharp.Cryptography.dll')) {
        if (Test-Path -LiteralPath (Join-Path $extractedDirectory $unusedPdfAssembly)) { throw 'Unused PDFsharp sample/signing/font assembly was bundled.' }
    }
    $pdfFixtureManifest = Get-Content -LiteralPath (Join-Path $extractedDirectory 'testing\pdf-fixtures\sources.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($pdfSource in $pdfFixtureManifest.Sources) {
        if ((Get-FileHash -LiteralPath (Join-Path (Join-Path $extractedDirectory 'testing\pdf-fixtures') $pdfSource.Name) -Algorithm SHA256).Hash -ne $pdfSource.SHA256) { throw 'PDF manual source hash mismatch.' }
    }
    if ($pdfFixtureManifest.ActualExplorerDragVerified -or $pdfFixtureManifest.PdfEditorDesignApproved) { throw 'Fixture manifest falsely claims manual/design acceptance.' }
    $report = [ordered]@{
        Build = $header.Build
        InstallerSHA256 = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
        ExtractedFilesCompared = $files.Count
        HashMismatches = $mismatches.Count
        HeifNativeFilesVerified = $heifLock.files.Count
        PackagedHeicRoundtripPassed = $true
        MainWindowHandle = $mainWindow
        BundledRuntimeLoaded = ($runtimeModules.Count -eq 1 -and $runtimeModules[0].StartsWith($extractedDirectory, [StringComparison]::OrdinalIgnoreCase))
        DuplicateExited = $secondExited
        DuplicateExitCode = $second.ExitCode
        ExitCode = $first.ExitCode
        IdleSampleMilliseconds = 3000
        IdleCpuMilliseconds = $idleCpu
        WorkingSetMB = $workingSet
        ImageNativeEngineLoadedAtIdle = ($idleImageModules.Count -gt 0)
        LifecycleEvents = $events
        HooksRemovedSuccessfully = $released[-1].Data.Succeeded
        TrayVisibleProperty = $tray[-1].Data.Visible
        ActualTrayScreenshotVerified = $false
        ActualExplorerDragVerified = $false
        DiagnosticLogFile = $latest.Name
        PreviewAndGuidesPresent = $true
        PdfRuntimeLicensePreviewAndFixturesPresent = $true
        PdfsharpVersion = '6.2.4'
        PdfsharpSampleFontsAndUnusedAssembliesExcluded = $true
        PdfEditorDesignApproved = $false
    }
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $root 'artifacts\diagnostic-package-smoke.json') -Encoding UTF8
    $report | ConvertTo-Json -Depth 6
    if ($mainWindow -ne 0 -or !$report.BundledRuntimeLoaded -or !$secondExited -or $second.ExitCode -ne 0 -or $first.ExitCode -ne 0) { throw 'Process/package smoke failed.' }
} finally {
    if ($null -ne $first -and !$first.HasExited) {
        Start-Process -FilePath $executable -ArgumentList '--quit' -WindowStyle Hidden -Wait
        $first.WaitForExit(10000) | Out-Null
    }
}
