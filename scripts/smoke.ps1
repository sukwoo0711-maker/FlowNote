$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$exe = Join-Path $root "artifacts\win-x64\FlowNote.Desktop.exe"
if (-not (Test-Path $exe)) {
    throw "Published exe not found: $exe"
}

$dataRoot = Join-Path $root "artifacts\smoke-data"
$smokeOut = Join-Path $root "artifacts\smoke-output"
if (Test-Path $dataRoot) { Remove-Item -Recurse -Force $dataRoot }
if (Test-Path $smokeOut) { Remove-Item -Recurse -Force $smokeOut }
New-Item -ItemType Directory -Force -Path $dataRoot, $smokeOut, (Join-Path $root "docs\screenshots") | Out-Null

# Smoke uses its own data profile and mutex; never terminate a user instance.

$common = @("--smoke", "--data-root", $dataRoot, "--smoke-out", $smokeOut)

Write-Host "Smoke run 1 (create records)"
$p1 = Start-Process -FilePath $exe -ArgumentList $common -PassThru -Wait
if ($p1.ExitCode -ne 0) {
    Get-Content (Join-Path $smokeOut "smoke-result.txt") -ErrorAction SilentlyContinue
    throw "Smoke run 1 failed with exit $($p1.ExitCode)"
}

Write-Host "Smoke run 2 (restart persistence)"
$p2 = Start-Process -FilePath $exe -ArgumentList $common -PassThru -Wait
if ($p2.ExitCode -ne 0) {
    Get-Content (Join-Path $smokeOut "smoke-result.txt") -ErrorAction SilentlyContinue
    throw "Smoke run 2 failed with exit $($p2.ExitCode)"
}

Copy-Item -Force (Join-Path $smokeOut "*.png") (Join-Path $root "docs\screenshots")
$u1 = Join-Path $root "docs\screenshots\u1"
$u2 = Join-Path $root "docs\screenshots\u2"
New-Item -ItemType Directory -Force -Path $u1, $u2 | Out-Null
Copy-Item -Force (Join-Path $smokeOut "02-floating*.png") $u1 -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "04-floating*.png") $u1 -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "06-floating*.png") $u1 -ErrorAction SilentlyContinue
$capsule = Join-Path $root "docs\screenshots\capsule"
New-Item -ItemType Directory -Force -Path $capsule | Out-Null
Copy-Item -Force (Join-Path $smokeOut "01-capsule*.png") $capsule -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "02-capsule*.png") $capsule -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "03-capsule*.png") $capsule -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "04-recent*.png") $capsule -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "05-todo*.png") $capsule -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "06-image*.png") $capsule -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "07-save*.png") $capsule -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "08-narrow*.png") $capsule -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "capsule-metrics.txt") $capsule -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "capsule-warm-show.txt") $capsule -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "03-main*.png") $u2 -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "07-main*.png") $u2 -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "08-main*.png") $u2 -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "09-main*.png") $u2 -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "10-main*.png") $u2 -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "11-main*.png") $u2 -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "12-panorama*.png") $u2 -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "13-panorama*.png") $u2 -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "14-panorama*.png") $u2 -ErrorAction SilentlyContinue
$core = Join-Path $root "docs\screenshots\core"
New-Item -ItemType Directory -Force -Path $core | Out-Null
Copy-Item -Force (Join-Path $smokeOut "12-panorama*.png") $core -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "13-panorama*.png") $core -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "14-panorama*.png") $core -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "09-main-panorama.png") $core -ErrorAction SilentlyContinue
Copy-Item -Force (Join-Path $smokeOut "smoke-result.txt") (Join-Path $root "docs\screenshots\smoke-result.txt")
$v3 = Join-Path $root "docs\screenshots\v3"
New-Item -ItemType Directory -Force -Path $v3 | Out-Null
Copy-Item -Force (Join-Path $smokeOut "v3-*.png") $v3 -ErrorAction SilentlyContinue
Get-Content (Join-Path $smokeOut "smoke-result.txt")
Write-Host "Smoke PASS. Screenshots copied to docs/screenshots"
