param(
    [string] $FlowNotePublishProfile = ""
)

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$out = Join-Path $root "artifacts\win-x64"
if (Test-Path $out) {
    Remove-Item -Recurse -Force $out
}

dotnet restore FlowNote.sln --locked-mode
if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed: $LASTEXITCODE" }

$publishArgs = @(
    "publish", "src/FlowNote.Desktop/FlowNote.Desktop.csproj",
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "--no-restore",
    "-p:PublishSingleFile=false",
    "-p:PublishTrimmed=false",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-o", "artifacts/win-x64"
)
if (-not [string]::IsNullOrWhiteSpace($FlowNotePublishProfile)) {
    $publishArgs += "-p:FlowNotePublishProfile=$FlowNotePublishProfile"
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }

Get-ChildItem -Path $out -Recurse -Include createdump.exe,*.pdb |
    Remove-Item -Force -ErrorAction SilentlyContinue

if ($FlowNotePublishProfile -ne "SecurityPc" -and $FlowNotePublishProfile -ne "Core") {
    $fixtureDest = Join-Path $out "fixtures\v3"
    New-Item -ItemType Directory -Force -Path $fixtureDest | Out-Null
    Copy-Item -Force (Join-Path $root "fixtures\v3\*") $fixtureDest
}

if ($FlowNotePublishProfile -eq "Core") {
    foreach ($extra in @("fixtures", "ai-runtime", "ai-models")) {
        $path = Join-Path $out $extra
        if (Test-Path $path) { Remove-Item -Recurse -Force $path }
    }
    Get-ChildItem -Path $out -Recurse -Include llama-server.exe,llama-server-impl.dll,*.gguf,ggml-rpc-server.exe |
        Remove-Item -Force -ErrorAction SilentlyContinue
    Copy-Item -Force (Join-Path $root "packaging\CORE.txt") (Join-Path $out "CORE.txt")
    Copy-Item -Force (Join-Path $root "packaging\PORTABLE-CORE.txt") (Join-Path $out "PORTABLE.txt")
}
