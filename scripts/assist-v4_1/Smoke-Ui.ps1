$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_common.ps1")
$root = Get-FlowNoteRoot
Set-Location $root

$exe = Join-Path $root "artifacts\assist-v4_1\app\FlowNote.Desktop.exe"
if (-not (Test-Path $exe)) {
    throw "Publish first: powershell -NoProfile -File scripts/assist-v4_1/Build.ps1"
}

$dataRoot = Join-Path $root "artifacts\assist-v4_1\smoke-data"
$smokeOut = Join-Path $root "artifacts\assist-v4_1\smoke-output"
$shotDir = Join-Path $root "docs\assist-v4_1\screenshots"
if (Test-Path $dataRoot) { Remove-Item -Recurse -Force $dataRoot }
if (Test-Path $smokeOut) { Remove-Item -Recurse -Force $smokeOut }
New-Item -ItemType Directory -Force -Path $dataRoot, $smokeOut, $shotDir | Out-Null

$p = Start-Process -FilePath $exe -ArgumentList @(
    "--assist-smoke",
    "--data-root", $dataRoot,
    "--smoke-out", $smokeOut
) -PassThru
if (-not $p.WaitForExit(90000)) {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    throw "Assist UI smoke timed out after 90s"
}
if ($p.ExitCode -ne 0) {
    Get-Content (Join-Path $smokeOut "assist-smoke-result.txt") -ErrorAction SilentlyContinue
    throw "Assist UI smoke failed with exit $($p.ExitCode)"
}
Copy-Item -Force (Join-Path $smokeOut "*") $shotDir
Write-Host "Assist UI smoke PASS. Copied captures to $shotDir"
