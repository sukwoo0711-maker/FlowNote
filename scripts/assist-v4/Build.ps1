$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path (Join-Path $root "FlowNote.sln"))) {
    $root = Split-Path -Parent $PSScriptRoot
}
Set-Location $root

& (Join-Path $root "scripts\publish.ps1")

$dest = Join-Path $root "artifacts\assist-v4\app"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item -Force -Recurse (Join-Path $root "artifacts\win-x64\*") $dest

$fixtureSrc = Join-Path $root "fixtures\assist-v4"
if (Test-Path $fixtureSrc) {
    New-Item -ItemType Directory -Force -Path (Join-Path $dest "fixtures\assist-v4") | Out-Null
    Copy-Item -Force -Recurse $fixtureSrc (Join-Path $dest "fixtures")
}

Write-Host "Published to $dest"
