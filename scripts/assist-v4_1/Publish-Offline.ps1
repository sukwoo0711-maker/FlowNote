param(
    [switch]$AppOnly
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_common.ps1")
$root = Get-FlowNoteRoot
Set-Location $root

& (Join-Path $PSScriptRoot "Build.ps1")
$app = Join-Path $root "artifacts\assist-v4_1\app"
if (-not (Test-Path (Join-Path $app "FlowNote.Desktop.exe"))) {
    throw "Published exe missing"
}

$stageName = if ($AppOnly) { "FlowNote-0.4.0-win-x64-apponly" } else { "FlowNote-0.4.0-win-x64-offline-ai" }
$stage = Join-Path $root "artifacts\assist-v4_1\package\$stageName"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item -Force -Recurse (Join-Path $app "*") $stage
if ($AppOnly) {
    Copy-Item -Force (Get-EngineLockPath -Root $root) (Join-Path $stage "ai-runtime\engine-lock.json")
    Copy-Item -Force (Join-Path $root "packaging\ai-runtime\model-lock.json") (Join-Path $stage "ai-runtime\model-lock.json")
    $notice = Join-Path $stage "AI-NOT-INCLUDED.txt"
    Set-Content -Path $notice -Value "This AppOnly folder does not include llama-server or the GGUF. It is not AI-ready. Use 설정 > 모델 파일 가져오기 after adding a verified runtime pack, or use the offline-ai zip." -Encoding utf8
} else {
    Copy-LockedRuntime -Root $root -DestinationAppRoot $stage -IncludeModel
    if (Test-Path (Join-Path $stage "ggml-rpc-server.exe")) {
        throw "ggml-rpc-server.exe must not be shipped as the app engine"
    }
}

$dist = Join-Path $root "artifacts\assist-v4_1\dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null
$zip = Join-Path $dist "$stageName.zip"
if (Test-Path $zip) { Remove-Item -Force $zip }
# Compress-Archive is ZIP32 (~2 GiB) and cannot hold the GGUF. Use tar/libarchive ZIP64.
Push-Location $stage
try {
    & tar.exe -a -c -f $zip *
    if ($LASTEXITCODE -ne 0) {
        throw "tar zip failed: $LASTEXITCODE"
    }
} finally {
    Pop-Location
}
$hash = Get-FileSha256Lower $zip
Set-Content -Path (Join-Path $dist "$stageName.sha256") -Value ("{0}  {1}" -f $hash, (Split-Path $zip -Leaf)) -Encoding ascii
Write-Host "Packaged $zip"
if ($AppOnly) {
    Write-Host "Label: AppOnly (not AI-ready)"
} else {
    Write-Host "Label: Offline AI pack (app + engine + model)"
}
