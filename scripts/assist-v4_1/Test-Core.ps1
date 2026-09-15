$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_common.ps1")
$root = Get-FlowNoteRoot
Set-Location $root

dotnet restore (Join-Path $root "FlowNote.sln") --locked-mode
Assert-DotNetOk "dotnet restore"
dotnet test (Join-Path $root "FlowNote.sln") -c Release --no-restore --nologo
Assert-DotNetOk "dotnet test"
Write-Host "CORE tests passed"
