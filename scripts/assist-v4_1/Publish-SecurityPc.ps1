$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_common.ps1")
$root = Get-FlowNoteRoot
Set-Location $root

& (Join-Path $root "scripts\publish.ps1") -FlowNotePublishProfile SecurityPc
Assert-DotNetOk "publish.ps1"

$publishDir = Join-Path $root "artifacts\win-x64"
$stageName = "FlowNote-0.4.0-win-x64-security-pc"
$stage = Join-Path $root "artifacts\assist-v4_1\package\$stageName"
$dist = Join-Path $root "artifacts\assist-v4_1\dist"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force -Path $stage, $dist | Out-Null
Copy-Item -Force -Recurse (Join-Path $publishDir "*") $stage

Copy-LockedRuntime -Root $root -DestinationAppRoot $stage -IncludeModel

foreach ($extra in @("fixtures")) {
    $path = Join-Path $stage $extra
    if (Test-Path $path) { Remove-Item -Recurse -Force $path }
}
Get-ChildItem -Path $stage -Recurse -Include createdump.exe,*.pdb,ggml-rpc-server.exe |
    Remove-Item -Force -ErrorAction SilentlyContinue

Copy-Item -Force (Join-Path $root "packaging\SECURITY_PC.txt") (Join-Path $stage "SECURITY_PC.txt")
Copy-Item -Force (Join-Path $root "packaging\PORTABLE.txt") (Join-Path $stage "PORTABLE.txt")

$hashes = Join-Path $stage "FILEHASHES.txt"
$lines = Get-ChildItem -Path $stage -Recurse -File | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($stage.Length).TrimStart("\", "/")
    "{0}  {1}" -f (Get-FileSha256Lower $_.FullName), $rel.Replace("\", "/")
}
Set-Content -Path $hashes -Value $lines -Encoding ascii

powershell -NoProfile -File (Join-Path $PSScriptRoot "Verify-Package.ps1") -PackageRoot $stage -Profile SecurityPc
if ($LASTEXITCODE -ne 0) { throw "Verify-Package failed: $LASTEXITCODE" }

$zip = Join-Path $dist "$stageName.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
Push-Location $stage
try {
    & tar.exe -a -c -f $zip *
    if ($LASTEXITCODE -ne 0) {
        throw "tar zip failed: $LASTEXITCODE"
    }
} finally {
    Pop-Location
}
$zipHash = Get-FileSha256Lower $zip
$zipName = Split-Path $zip -Leaf
$sumLine = "{0}  {1}" -f $zipHash, $zipName
Set-Content -Path (Join-Path $dist "$stageName.sha256") -Value $sumLine -Encoding ascii
Set-Content -Path (Join-Path $dist "SHA256SUMS.txt") -Value $sumLine -Encoding ascii
Write-Host "Security PC pack: $zip"
Write-Host "SHA-256 $zipHash"
