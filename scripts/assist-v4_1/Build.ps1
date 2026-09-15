$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_common.ps1")
$root = Get-FlowNoteRoot
Set-Location $root

dotnet restore (Join-Path $root "FlowNote.sln") --locked-mode
Assert-DotNetOk "dotnet restore"
dotnet test (Join-Path $root "FlowNote.sln") -c Release --no-restore --nologo
Assert-DotNetOk "dotnet test"
& (Join-Path $root "scripts\publish.ps1")
Assert-DotNetOk "publish.ps1"

$dest = Join-Path $root "artifacts\assist-v4_1\app"
if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Copy-Item -Force -Recurse (Join-Path $root "artifacts\win-x64\*") $dest
New-Item -ItemType Directory -Force -Path (Join-Path $dest "ai-runtime") | Out-Null
Copy-Item -Force (Get-EngineLockPath -Root $root) (Join-Path $dest "ai-runtime\engine-lock.json")
Copy-Item -Force (Join-Path $root "packaging\ai-runtime\model-lock.json") (Join-Path $dest "ai-runtime\model-lock.json")
Write-Host "Built app at $dest"
