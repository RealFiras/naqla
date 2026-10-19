param([Parameter(Mandatory=$true)][string]$TestResults)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$resultsDirectory = [IO.Path]::GetFullPath($TestResults)
$previewDirectory = Join-Path $projectRoot 'preview\pdf-toolkit'
$fixtureDirectory = Join-Path $projectRoot 'assets\pdf-test-files'
New-Item -ItemType Directory -Path $previewDirectory,$fixtureDirectory -Force | Out-Null
foreach ($view in @('reorder','extract','delete','merge','numbers','watermark','rotate','compact')) {
    Copy-Item -LiteralPath (Join-Path $resultsDirectory ('native-pdf-editor-' + $view + '.png')) -Destination (Join-Path $previewDirectory ($view + '.png')) -Force
}
foreach ($count in @(1,2)) {
    Copy-Item -LiteralPath (Join-Path $resultsDirectory ('native-pdf-wheel-' + $count + '.png')) -Destination (Join-Path $previewDirectory ('wheel-' + $count + '.png')) -Force
}
$fixtures = @(
    @{Source='pdf-fixtures\editor-preview.pdf';Name='source-a.pdf';Pages=4},
    @{Source='pdf-fixtures\editor-merge.pdf';Name='source-b.pdf';Pages=2},
    @{Source='encrypted-False.pdf';Name='encrypted.pdf';Pages=1}
)
$manifest = foreach ($item in $fixtures) {
    $destination = Join-Path $fixtureDirectory $item.Name
    Copy-Item -LiteralPath (Join-Path $resultsDirectory $item.Source) -Destination $destination -Force
    [ordered]@{Name=$item.Name;Pages=$item.Pages;SHA256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash}
}
Copy-Item -LiteralPath (Join-Path $resultsDirectory 'pdf-mark.png') -Destination (Join-Path $fixtureDirectory 'watermark.png') -Force
[ordered]@{Sources=$manifest;ActualExplorerDragVerified=$false;PdfEditorDesignApproved=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $fixtureDirectory 'sources.json') -Encoding UTF8
