$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_common.ps1")
$root = Get-FlowNoteRoot
Set-Location $root

$exe = Join-Path $root "artifacts\assist-v4_1\app\FlowNote.Desktop.exe"
if (-not (Test-Path $exe)) {
    throw "Publish the app first: powershell -NoProfile -File scripts/assist-v4_1/Build.ps1"
}

$hasModel = $false
try {
    Copy-LockedRuntime -Root $root -DestinationAppRoot (Split-Path -Parent $exe) -IncludeModel
    $hasModel = $true
} catch {
    Write-Host "Assets not ready; starting rules/demo mode. $($_.Exception.Message)"
}

$data = Join-Path $root "artifacts\assist-v4_1\demo-data"
New-Item -ItemType Directory -Force -Path $data | Out-Null
$args = @("--demo", "--data-root", $data)
if ($hasModel) {
    $args += @("--assist-live")
    Write-Host "Starting isolated live demo. FlowNote owns the engine; inputs are seeded without expected answers."
} else {
    $args += @("--assist-demo")
    Write-Host "Starting isolated demo without a ready model. Seeded expected links are not MODEL_REAL."
}
Start-Process -FilePath $exe -ArgumentList $args
Write-Host "Data: $data"
