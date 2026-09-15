$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path (Join-Path $root "FlowNote.sln"))) {
    $root = Split-Path -Parent $PSScriptRoot
}
Set-Location $root

$exe = Join-Path $root "artifacts\assist-v4\app\FlowNote.Desktop.exe"
if (-not (Test-Path $exe)) {
    $exe = Join-Path $root "artifacts\win-x64\FlowNote.Desktop.exe"
}
if (-not (Test-Path $exe)) {
    throw "Publish the app first: powershell -NoProfile -File scripts/assist-v4/Build.ps1"
}

$data = Join-Path $root "artifacts\assist-v4\demo-data"
New-Item -ItemType Directory -Force -Path $data | Out-Null
Write-Host "Starting isolated assist demo. Data: $data"
Write-Host "This seeds fixture expected assignments. It is not a MODEL_REAL pass."
Start-Process -FilePath $exe -ArgumentList @("--demo", "--assist-demo", "--data-root", $data)
