$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_common.ps1")
$root = Get-FlowNoteRoot
Set-Location $root

$exe = Join-Path $root "artifacts\assist-v4_1\app\FlowNote.Desktop.exe"
if (-not (Test-Path $exe)) {
    throw "Publish the app first: powershell -NoProfile -File scripts/assist-v4_1/Build.ps1"
}

Copy-LockedRuntime -Root $root -DestinationAppRoot (Split-Path -Parent $exe) -IncludeModel

$data = Join-Path $root "artifacts\assist-v4_1\engine-probe-data"
$out = Join-Path $root "docs\assist-v4_1\engine-capabilities.json"
New-Item -ItemType Directory -Force -Path $data, (Split-Path -Parent $out) | Out-Null
if (Test-Path $out) { Remove-Item -Force $out }

$p = Start-Process -FilePath $exe -ArgumentList @(
    "--assist-engine-probe",
    "--data-root", $data,
    "--eval-out", $out
) -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit(480000)) {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    throw "ENGINE_REAL probe timed out after 8 minutes"
}
if ($p.ExitCode -ne 0) {
    if (Test-Path $out) { Get-Content $out }
    throw "ENGINE_REAL probe failed with exit $($p.ExitCode)"
}
Write-Host "ENGINE_REAL probe wrote $out"
