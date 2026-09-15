$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$candidates = @(
    (Join-Path $root "src\FlowNote.Desktop\bin\Release\net10.0-windows\FlowNote.Desktop.exe"),
    (Join-Path $root "src\FlowNote.Desktop\bin\Release\net10.0-windows\win-x64\FlowNote.Desktop.exe"),
    (Join-Path $root "artifacts\win-x64\FlowNote.Desktop.exe")
) | Where-Object { Test-Path $_ } | Sort-Object { (Get-Item $_).LastWriteTimeUtc } -Descending
$exe = $candidates | Select-Object -First 1
if (-not $exe) {
    throw "FlowNote.Desktop.exe not found. Run scripts/build.ps1 first."
}
Write-Host "Using $exe"

$out = Join-Path $root "docs\screenshots\u0"
New-Item -ItemType Directory -Force -Path $out | Out-Null
Get-Process -Name FlowNote.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
$p = Start-Process -FilePath $exe -ArgumentList @("--ui-preview", "--smoke", "--smoke-out", $out) -PassThru -Wait
if ($p.ExitCode -ne 0) {
    Get-Content (Join-Path $out "u0-result.txt") -ErrorAction SilentlyContinue
    throw "U0 preview capture failed with exit $($p.ExitCode)"
}
Get-Content (Join-Path $out "u0-result.txt")
Copy-Item -Force (Join-Path $out "u0-component-preview.png") (Join-Path $root "docs\screenshots\u0-component-preview.png")
