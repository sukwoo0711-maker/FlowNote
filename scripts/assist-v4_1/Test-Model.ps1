param(
    [int]$Repeats = 3
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_common.ps1")
$root = Get-FlowNoteRoot
Set-Location $root

$exe = Join-Path $root "artifacts\assist-v4_1\app\FlowNote.Desktop.exe"
if (-not (Test-Path $exe)) {
    throw "Publish the app first: powershell -NoProfile -File scripts/assist-v4_1/Build.ps1"
}

Copy-LockedRuntime -Root $root -DestinationAppRoot (Split-Path -Parent $exe) -IncludeModel

$data = Join-Path $root "artifacts\assist-v4_1\model-eval-data"
$out = Join-Path $root "docs\assist-v4_1\MODEL_EVAL.json"
New-Item -ItemType Directory -Force -Path $data, (Split-Path -Parent $out) | Out-Null

$p = Start-Process -FilePath $exe -ArgumentList @(
    "--assist-eval",
    "--eval-repeats", "$Repeats",
    "--data-root", $data,
    "--eval-out", $out
) -PassThru -WindowStyle Hidden
if (-not $p.WaitForExit(5400000)) {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    throw "MODEL_REAL eval timed out after 90 minutes"
}
if ($p.ExitCode -ne 0) {
    if (Test-Path $out) { Get-Content $out -TotalCount 40 }
    throw "MODEL_REAL eval failed with exit $($p.ExitCode)"
}
Write-Host "MODEL_REAL eval wrote $out"
