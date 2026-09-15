$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path (Join-Path $root "FlowNote.sln"))) {
    $root = Split-Path -Parent $PSScriptRoot
}
Set-Location $root

& (Join-Path $root "scripts\test.ps1") Release
