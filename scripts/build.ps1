param([string]$Runtime = 'win-x64')
$ErrorActionPreference = 'Stop'
# Developer-only workflow. Prepare optional packs as documented before full acceptance.
& (Join-Path $PSScriptRoot 'package-development.ps1') -Runtime $Runtime -RunAcceptance
