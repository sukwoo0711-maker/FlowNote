$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

[xml]$buildProps = Get-Content -LiteralPath (Join-Path $root "Directory.Build.props") -Raw
$version = [string]$buildProps.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) { throw "Missing product version" }
$publishDir = Join-Path $root "artifacts\win-x64"
$stageName = "FlowNote-$version-win-x64-core"
$stageDir = Join-Path $root "artifacts\package\$stageName"
$distDir = Join-Path $root "artifacts\dist"
$zipPath = Join-Path $distDir "$stageName.zip"

Write-Host "Publishing core (no AI model, no engine)"
& (Join-Path $root "scripts\publish.ps1") -FlowNotePublishProfile Core
if ($LASTEXITCODE -ne 0) {
    throw "publish.ps1 failed with exit $LASTEXITCODE"
}

$exe = Join-Path $publishDir "FlowNote.Desktop.exe"
if (-not (Test-Path $exe)) {
    throw "Published exe not found: $exe"
}

$banned = Get-ChildItem -Path $publishDir -Recurse -File | Where-Object {
    $_.Name -match '(?i)llama-server|\.gguf$|ggml-rpc-server'
}
if ($banned) {
    throw ("Core pack still contains AI binaries: " + ($banned.FullName -join ", "))
}

if (Test-Path $stageDir) { Remove-Item -Recurse -Force $stageDir }
New-Item -ItemType Directory -Force -Path $stageDir, $distDir | Out-Null
Copy-Item -Recurse -Force (Join-Path $publishDir "*") $stageDir

if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path (Join-Path $stageDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$setupPath = $null
$isccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if ($iscc) {
    $appDir = Join-Path $root "artifacts\package\app"
    if (Test-Path $appDir) { Remove-Item -Recurse -Force $appDir }
    New-Item -ItemType Directory -Force -Path $appDir | Out-Null
    Copy-Item -Recurse -Force (Join-Path $publishDir "*") $appDir
    $iss = Join-Path $root "packaging\setup-core.iss"
    Write-Host "Building core setup with $iscc"
    & $iscc "/DMyAppVersion=$version" $iss
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit $LASTEXITCODE"
    }
    $setupPath = Join-Path $distDir "FlowNote-$version-win-x64-core-setup.exe"
    if (-not (Test-Path $setupPath)) {
        throw "Core setup exe not found: $setupPath"
    }
}

$checksums = Join-Path $distDir "SHA256SUMS-core.txt"
$targets = @($zipPath)
if ($setupPath) { $targets += $setupPath }
$lines = foreach ($file in $targets) {
    $hash = (Get-FileHash -Algorithm SHA256 -Path $file).Hash.ToLowerInvariant()
    "{0}  {1}" -f $hash, (Split-Path $file -Leaf)
}
Set-Content -Path $checksums -Value $lines -Encoding ascii

Write-Host "Core pack ready (no GGUF, no llama-server)"
Get-Item ($targets + $checksums) | ForEach-Object { "{0}`t{1}" -f $_.Length, $_.FullName }
