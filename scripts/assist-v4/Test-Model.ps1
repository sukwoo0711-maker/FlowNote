$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not (Test-Path (Join-Path $root "FlowNote.sln"))) {
    $root = Split-Path -Parent $PSScriptRoot
}
Set-Location $root

$outDir = Join-Path $root "docs\assist-v4"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$evalPath = Join-Path $outDir "MODEL_EVAL.json"

$ollama = Get-Command ollama -ErrorAction SilentlyContinue
if ($null -eq $ollama) {
    $payload = @{
        layer = "MODEL_REAL"
        status = "BLOCKED"
        reason = "Ollama CLI not installed on this PC. No model pull was performed."
        endpoint = "http://127.0.0.1:11435"
        model = "qwen3:4b"
        cases_run = 0
        semantic_auto_apply = $false
        timestamp_utc = [DateTimeOffset]::UtcNow.ToString("o")
    } | ConvertTo-Json
    Set-Content -Path $evalPath -Value $payload -Encoding utf8
    Write-Host "MODEL_REAL BLOCKED: $evalPath"
    exit 0
}

Write-Host "Ollama is installed, but this script does not pull models or start a server without an explicit prep step."
$payload = @{
    layer = "MODEL_REAL"
    status = "BLOCKED"
    reason = "CLI present but dedicated 11435 eval was not authorized/prepared in this run."
    endpoint = "http://127.0.0.1:11435"
    model = "qwen3:4b"
    cases_run = 0
    semantic_auto_apply = $false
    timestamp_utc = [DateTimeOffset]::UtcNow.ToString("o")
} | ConvertTo-Json
Set-Content -Path $evalPath -Value $payload -Encoding utf8
exit 0
