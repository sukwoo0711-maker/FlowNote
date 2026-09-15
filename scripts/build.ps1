$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$configuration = "Release"
if ($args.Count -ge 1 -and -not [string]::IsNullOrWhiteSpace($args[0])) {
    $configuration = [string]$args[0]
}

dotnet --info
dotnet restore FlowNote.sln --locked-mode
dotnet build FlowNote.sln -c $configuration --no-restore
