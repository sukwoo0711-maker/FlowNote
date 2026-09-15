$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

function Get-FlowNoteRoot {
    $root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
    if (-not (Test-Path (Join-Path $root "FlowNote.sln"))) {
        $root = Split-Path -Parent $PSScriptRoot
    }
    if (-not (Test-Path (Join-Path $root "FlowNote.sln"))) {
        throw "FlowNote.sln not found from $PSScriptRoot"
    }
    return $root
}

function Get-EngineLockPath {
    param([string]$Root)
    return (Join-Path $Root "packaging\ai-runtime\engine-lock.json")
}

function Read-EngineLock {
    param([string]$Root)
    $path = Get-EngineLockPath -Root $Root
    if (-not (Test-Path $path)) {
        throw "engine-lock.json missing: $path"
    }
    return (Get-Content -Raw -Path $path | ConvertFrom-Json)
}

function Test-UnresolvedLock {
    param($Lock)
    $bad = {
        param($value)
        if ([string]::IsNullOrWhiteSpace($value)) { return $true }
        if ($value -match 'null|REPLACE|^latest$|^main$') { return $true }
        if ($value -match '^0+$') { return $true }
        return $false
    }
    if (& $bad $Lock.engine.releaseTag) { return $true }
    if (& $bad $Lock.engine.commit) { return $true }
    if (& $bad $Lock.engine.archiveSha256) { return $true }
    if (& $bad $Lock.model.sha256) { return $true }
    if ($null -eq $Lock.engine.files -or @($Lock.engine.files).Count -eq 0) { return $true }
    return $false
}

function Get-FileSha256Lower {
    param([string]$Path)
    return (Get-FileHash -Algorithm SHA256 -Path $Path).Hash.ToLowerInvariant()
}

function Copy-HardOrFile {
    param([string]$Source, [string]$Destination)
    $destDir = Split-Path -Parent $Destination
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    }
    if (Test-Path $Destination) {
        Remove-Item -Force $Destination
    }
    $linked = $false
    try {
        New-Item -ItemType HardLink -Path $Destination -Target $Source -ErrorAction Stop | Out-Null
        $linked = $true
    } catch {
        $linked = $false
    }
    if (-not $linked) {
        Copy-Item -Force $Source $Destination
    }
}

function Copy-LockedRuntime {
    param(
        [string]$Root,
        [string]$DestinationAppRoot,
        [switch]$IncludeModel
    )
    $lock = Read-EngineLock -Root $Root
    if (Test-UnresolvedLock -Lock $lock) {
        throw "engine-lock.json is unresolved; refusing to copy runtime"
    }
    $tag = [string]$lock.engine.releaseTag
    $srcCpu = Join-Path $Root "artifacts\assist-v4_1\runtime\cpu\$tag"
    if (-not (Test-Path (Join-Path $srcCpu "llama-server.exe"))) {
        throw "Prepared engine files missing at $srcCpu. Run Prepare-Assets.ps1 first."
    }
    $destCpu = Join-Path $DestinationAppRoot "ai-runtime\cpu\$tag"
    New-Item -ItemType Directory -Force -Path $destCpu | Out-Null
    foreach ($file in @($lock.engine.files)) {
        $name = [string]$file.relativePath
        if ($name -match '\.\.|/|\\' -or $name -match 'rpc-server') {
            throw "Refusing locked path: $name"
        }
        $src = Join-Path $srcCpu $name
        $dst = Join-Path $destCpu $name
        if (-not (Test-Path $src)) {
            throw "Missing engine file $src"
        }
        $actual = Get-FileSha256Lower $src
        if ($actual -ne ([string]$file.sha256).ToLowerInvariant() -or ((Get-Item $src).Length -ne [int64]$file.bytes)) {
            throw "Hash/size mismatch for $name"
        }
        Copy-Item -Force $src $dst
    }
    Copy-Item -Force (Get-EngineLockPath -Root $Root) (Join-Path $DestinationAppRoot "ai-runtime\engine-lock.json")
    Copy-Item -Force (Join-Path $Root "packaging\ai-runtime\model-lock.json") (Join-Path $DestinationAppRoot "ai-runtime\model-lock.json")
    $licDest = Join-Path $DestinationAppRoot "licenses"
    New-Item -ItemType Directory -Force -Path $licDest | Out-Null
    Copy-Item -Force (Join-Path $Root "packaging\licenses\*") $licDest
    if ($IncludeModel) {
        $modelSrc = Join-Path $Root "artifacts\assist-v4_1\downloads\Qwen3-4B-Q4_K_M.gguf"
        if (-not (Test-Path $modelSrc)) {
            throw "Prepared GGUF missing at $modelSrc. Run Prepare-Assets.ps1 first."
        }
        $modelHash = Get-FileSha256Lower $modelSrc
        if ($modelHash -ne ([string]$lock.model.sha256).ToLowerInvariant() -or ((Get-Item $modelSrc).Length -ne [int64]$lock.model.bytes)) {
            throw "Model hash/size mismatch"
        }
        $modelDestDir = Join-Path $DestinationAppRoot "ai-models"
        New-Item -ItemType Directory -Force -Path $modelDestDir | Out-Null
        Copy-HardOrFile -Source $modelSrc -Destination (Join-Path $modelDestDir "Qwen3-4B-Q4_K_M.gguf")
    }
}

function Assert-DotNetOk {
    param([string]$Action)
    if ($LASTEXITCODE -ne 0) {
        throw "$Action failed with exit $LASTEXITCODE"
    }
}
