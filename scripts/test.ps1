$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$configuration = "Release"
if ($args.Count -ge 1 -and -not [string]::IsNullOrWhiteSpace($args[0])) {
    $configuration = [string]$args[0]
}

dotnet restore FlowNote.sln --locked-mode
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
dotnet test FlowNote.sln -c $configuration --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
