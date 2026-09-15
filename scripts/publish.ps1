$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

dotnet restore FlowNote.sln --locked-mode
dotnet publish src/FlowNote.Desktop/FlowNote.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  --no-restore `
  -p:PublishSingleFile=false `
  -p:PublishTrimmed=false `
  -o artifacts/win-x64

$fixtureDest = Join-Path $root "artifacts\win-x64\fixtures\v3"
New-Item -ItemType Directory -Force -Path $fixtureDest | Out-Null
Copy-Item -Force (Join-Path $root "fixtures\v3\*") $fixtureDest
