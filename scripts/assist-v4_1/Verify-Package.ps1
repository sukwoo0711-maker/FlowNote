param(
    [Parameter(Mandatory = $true)]
    [string]$PackageRoot,
    [switch]$ExpectModel,
    [ValidateSet("OfflineAi", "AppOnly", "SecurityPc")]
    [string]$Profile = ""
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_common.ps1")
$root = Get-FlowNoteRoot
if ([string]::IsNullOrWhiteSpace($Profile)) {
    if ($ExpectModel) { $Profile = "OfflineAi" } else { $Profile = "AppOnly" }
}

$exe = Join-Path $PackageRoot "FlowNote.Desktop.exe"
if (-not (Test-Path $exe)) {
    throw "Package is missing FlowNote.Desktop.exe"
}

$exes = @(Get-ChildItem -Path $PackageRoot -Recurse -Filter *.exe)
$exeNames = @($exes | ForEach-Object { $_.Name.ToLowerInvariant() })
if ($exeNames -contains "createdump.exe") {
    throw "createdump.exe must not ship"
}
if ($exeNames -contains "ggml-rpc-server.exe") {
    throw "ggml-rpc-server.exe must not ship"
}

$pdb = @(Get-ChildItem -Path $PackageRoot -Recurse -Filter *.pdb -ErrorAction SilentlyContinue)
if ($pdb.Count -gt 0) {
    throw "PDB files must not ship: $($pdb[0].FullName)"
}

if ($Profile -eq "SecurityPc") {
    $allowedExes = @("flownote.desktop.exe", "llama-server.exe")
    $unexpected = @($exeNames | Where-Object { $allowedExes -notcontains $_ } | Sort-Object -Unique)
    if ($unexpected.Count -gt 0) {
        throw "Security PC pack has unexpected exe: $($unexpected -join ', ')"
    }
    if ($exeNames -notcontains "flownote.desktop.exe") {
        throw "Security PC pack is missing FlowNote.Desktop.exe"
    }
    if ($exeNames -notcontains "llama-server.exe") {
        throw "Security PC pack is missing llama-server.exe (local AI is required)"
    }
    if (Test-Path (Join-Path $PackageRoot "fixtures")) {
        throw "Security PC pack must not include fixtures"
    }
    if (-not (Test-Path (Join-Path $PackageRoot "SECURITY_PC.txt"))) {
        throw "SECURITY_PC.txt missing"
    }
    if (-not (Test-Path (Join-Path $PackageRoot "FILEHASHES.txt"))) {
        throw "FILEHASHES.txt missing"
    }
    $scan = Select-String -Path (Join-Path $PackageRoot "FILEHASHES.txt") -Pattern "ggml-rpc|createdump|OLLAMA_HOST|/api/chat" -ErrorAction SilentlyContinue
    if ($scan) {
        throw "Security inventory still lists forbidden names"
    }
}

$lock = Read-EngineLock -Root $root
if (Test-UnresolvedLock -Lock $lock) {
    throw "Repository engine-lock.json is unresolved"
}
$packLock = Join-Path $PackageRoot "ai-runtime\engine-lock.json"
if (-not (Test-Path $packLock)) {
    throw "Package is missing ai-runtime/engine-lock.json"
}
$packLockObj = Get-Content -Raw -Path $packLock | ConvertFrom-Json
if (Test-UnresolvedLock -Lock $packLockObj) {
    throw "Packaged engine-lock.json is unresolved"
}

$tag = [string]$packLockObj.engine.releaseTag
$cpu = Join-Path $PackageRoot "ai-runtime\cpu\$tag"
if ($Profile -eq "OfflineAi" -or $Profile -eq "SecurityPc") {
    foreach ($file in @($packLockObj.engine.files)) {
        $path = Join-Path $cpu ([string]$file.relativePath)
        if (-not (Test-Path $path)) { throw "Missing $($file.relativePath)" }
        $actual = Get-FileSha256Lower $path
        if ($actual -ne ([string]$file.sha256).ToLowerInvariant()) {
            throw "Hash mismatch $($file.relativePath)"
        }
    }
    $model = Join-Path $PackageRoot "ai-models\Qwen3-4B-Q4_K_M.gguf"
    if (-not (Test-Path $model)) { throw "AI-ready package is missing the GGUF" }
    $modelHash = Get-FileSha256Lower $model
    if ($modelHash -ne ([string]$packLockObj.model.sha256).ToLowerInvariant()) {
        throw "Packaged GGUF hash mismatch"
    }
} else {
    Write-Host "AppOnly verification: lock present, model not required. Not AI-ready."
}

$scanJson = Get-ChildItem -Path $PackageRoot -Filter *.json -Recurse -ErrorAction SilentlyContinue |
    Select-String -Pattern "OLLAMA_HOST|/api/chat" -ErrorAction SilentlyContinue
if ($scanJson) {
    throw "Packaged JSON still contains Ollama runtime settings: $($scanJson.Path)"
}
Write-Host "Package verification passed for $PackageRoot"
