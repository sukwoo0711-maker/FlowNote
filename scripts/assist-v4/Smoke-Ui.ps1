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
    throw "Publish first: powershell -NoProfile -File scripts/assist-v4/Build.ps1"
}

$dataRoot = Join-Path $root "artifacts\assist-v4\smoke-data"
$smokeOut = Join-Path $root "artifacts\assist-v4\smoke-output"
if (Test-Path $dataRoot) { Remove-Item -Recurse -Force $dataRoot }
if (Test-Path $smokeOut) { Remove-Item -Recurse -Force $smokeOut }
New-Item -ItemType Directory -Force -Path $dataRoot, $smokeOut, (Join-Path $root "docs\assist-v4\screenshots") | Out-Null

Get-Process -Name FlowNote.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force

$p = Start-Process -FilePath $exe -ArgumentList @(
    "--assist-smoke",
    "--data-root", $dataRoot,
    "--smoke-out", $smokeOut
) -PassThru
if (-not $p.WaitForExit(90000)) {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    Get-Content (Join-Path $smokeOut "assist-smoke-result.txt") -ErrorAction SilentlyContinue
    Get-Content (Join-Path $smokeOut "assist-smoke-started.txt") -ErrorAction SilentlyContinue
    throw "Assist UI smoke timed out after 90s"
}
if ($p.ExitCode -ne 0) {
    Get-Content (Join-Path $smokeOut "assist-smoke-result.txt") -ErrorAction SilentlyContinue
    throw "Assist UI smoke failed with exit $($p.ExitCode)"
}

Copy-Item -Force (Join-Path $smokeOut "v4-*.png") (Join-Path $root "docs\assist-v4\screenshots")
Copy-Item -Force (Join-Path $smokeOut "assist-smoke-result.txt") (Join-Path $root "docs\assist-v4\screenshots")
Get-Content (Join-Path $smokeOut "assist-smoke-result.txt")
Write-Host "Assist UI smoke PASS. Screenshots in docs/assist-v4/screenshots"
