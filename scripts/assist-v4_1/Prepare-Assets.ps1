param(
    [switch]$AllowDownload,
    [string]$OfflineSource
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_common.ps1")
$root = Get-FlowNoteRoot
Set-Location $root

$lock = Read-EngineLock -Root $root
if (Test-UnresolvedLock -Lock $lock) {
    throw "packaging/ai-runtime/engine-lock.json is unresolved"
}

$downloadDir = Join-Path $root "artifacts\assist-v4_1\downloads"
$runtimeDir = Join-Path $root "artifacts\assist-v4_1\runtime\cpu\$($lock.engine.releaseTag)"
New-Item -ItemType Directory -Force -Path $downloadDir, $runtimeDir | Out-Null

$zipName = "llama-b10964-bin-win-cpu-x64.zip"
$zipPath = Join-Path $downloadDir $zipName
$modelPath = Join-Path $downloadDir "Qwen3-4B-Q4_K_M.gguf"

if ($OfflineSource) {
    $offZip = Join-Path $OfflineSource $zipName
    $offModel = Join-Path $OfflineSource "Qwen3-4B-Q4_K_M.gguf"
    if (Test-Path $offZip) { Copy-Item -Force $offZip $zipPath }
    if (Test-Path $offModel) { Copy-HardOrFile -Source $offModel -Destination $modelPath }
}

if ($AllowDownload) {
    if (-not (Test-Path $zipPath)) {
        Invoke-WebRequest -UseBasicParsing -Uri ([string]$lock.engine.assetUrl) -OutFile $zipPath
    }
    if (-not (Test-Path $modelPath)) {
        $modelUrl = "https://huggingface.co/Qwen/Qwen3-4B-GGUF/resolve/$($lock.model.revision)/Qwen3-4B-Q4_K_M.gguf"
        Invoke-WebRequest -UseBasicParsing -Uri $modelUrl -OutFile $modelPath
    }
}

if (-not (Test-Path $zipPath)) {
    throw "Engine zip missing. Re-run with -AllowDownload or -OfflineSource."
}
$zipHash = Get-FileSha256Lower $zipPath
if ($zipHash -ne ([string]$lock.engine.archiveSha256).ToLowerInvariant()) {
    throw "Engine zip SHA-256 mismatch"
}
if ((Get-Item $zipPath).Length -ne [int64]$lock.engine.archiveBytes) {
    throw "Engine zip size mismatch"
}

$needExtract = -not (Test-Path (Join-Path $runtimeDir "llama-server.exe"))
if ($needExtract) {
    Expand-Archive -Force -Path $zipPath -DestinationPath $runtimeDir
}

foreach ($file in @($lock.engine.files)) {
    $path = Join-Path $runtimeDir ([string]$file.relativePath)
    if (-not (Test-Path $path)) {
        throw "Extracted engine is missing $($file.relativePath)"
    }
    $actual = Get-FileSha256Lower $path
    if ($actual -ne ([string]$file.sha256).ToLowerInvariant() -or ((Get-Item $path).Length -ne [int64]$file.bytes)) {
        throw "Prepared file mismatch: $($file.relativePath)"
    }
}

if (-not (Test-Path $modelPath)) {
    throw "GGUF missing. Re-run with -AllowDownload or -OfflineSource."
}
$modelHash = Get-FileSha256Lower $modelPath
if ($modelHash -ne ([string]$lock.model.sha256).ToLowerInvariant() -or ((Get-Item $modelPath).Length -ne [int64]$lock.model.bytes)) {
    throw "GGUF SHA-256/size mismatch"
}

$stage = Join-Path $root "artifacts\assist-v4_1\pack"
if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-LockedRuntime -Root $root -DestinationAppRoot $stage -IncludeModel
Write-Host "Prepared assets at $stage"
