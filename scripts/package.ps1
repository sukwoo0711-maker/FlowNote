$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$version = "0.5.0"
$publishDir = Join-Path $root "artifacts\win-x64"
$stageDir = Join-Path $root "artifacts\package\app"
$distDir = Join-Path $root "artifacts\dist"
$portableName = "FlowNote-$version-win-x64-portable"
$portableRoot = Join-Path $root "artifacts\package\$portableName"
$zipPath = Join-Path $distDir "$portableName.zip"

Write-Host "Publishing $version"
& (Join-Path $root "scripts\publish.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "publish.ps1 failed with exit $LASTEXITCODE"
}

if (-not (Test-Path (Join-Path $publishDir "FlowNote.Desktop.exe"))) {
    throw "Published exe not found: $publishDir\FlowNote.Desktop.exe"
}

if (Test-Path $stageDir) { Remove-Item -Recurse -Force $stageDir }
if (Test-Path $portableRoot) { Remove-Item -Recurse -Force $portableRoot }
New-Item -ItemType Directory -Force -Path $stageDir, $portableRoot, $distDir | Out-Null

Copy-Item -Recurse -Force (Join-Path $publishDir "*") $stageDir
Copy-Item -Recurse -Force (Join-Path $publishDir "*") $portableRoot
Copy-Item -Force (Join-Path $root "packaging\PORTABLE.txt") (Join-Path $portableRoot "PORTABLE.txt")

if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $portableRoot "*") -DestinationPath $zipPath -CompressionLevel Optimal

$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 7\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "ISCC.exe not found. Install Inno Setup, then re-run scripts/package.ps1"
}

Write-Host "Building setup with $iscc"
& $iscc (Join-Path $root "packaging\setup.iss")
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup failed with exit $LASTEXITCODE"
}

$setupPath = Join-Path $distDir "FlowNote-$version-win-x64-setup.exe"
if (-not (Test-Path $setupPath)) {
    throw "Setup exe not found: $setupPath"
}

$checksums = Join-Path $distDir "SHA256SUMS.txt"
$lines = foreach ($file in @($zipPath, $setupPath)) {
    $hash = (Get-FileHash -Algorithm SHA256 -Path $file).Hash.ToLowerInvariant()
    "{0}  {1}" -f $hash, (Split-Path $file -Leaf)
}
Set-Content -Path $checksums -Value $lines -Encoding ascii

Write-Host "Packaged:"
Get-Item $zipPath, $setupPath, $checksums | ForEach-Object { "{0}`t{1}" -f $_.Length, $_.FullName }
