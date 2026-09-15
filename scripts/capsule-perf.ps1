$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$exe = Join-Path $root "artifacts\win-x64\FlowNote.Desktop.exe"
if (-not (Test-Path $exe)) { throw "Published exe not found: $exe" }
$data = Join-Path $root "artifacts\capsule-perf-data"
$out = Join-Path $root "artifacts\capsule-perf"
New-Item -ItemType Directory -Force -Path $data, $out | Out-Null
Get-Process -Name FlowNote.Desktop -ErrorAction SilentlyContinue | Stop-Process -Force
$proc = Start-Process -FilePath $exe -ArgumentList @("--demo", "--data-root", $data) -PassThru
Start-Sleep -Seconds 3
$p = Get-Process -Id $proc.Id
$cpu1 = $p.TotalProcessorTime.TotalMilliseconds
$ws1 = $p.WorkingSet64
$priv1 = $p.PrivateMemorySize64
$t1 = [Diagnostics.Stopwatch]::StartNew()
Start-Sleep -Seconds 60
$p.Refresh()
$cpu2 = $p.TotalProcessorTime.TotalMilliseconds
$ws2 = $p.WorkingSet64
$priv2 = $p.PrivateMemorySize64
$wall = $t1.Elapsed.TotalMilliseconds
$logical = [System.Environment]::ProcessorCount
$idlePct = if ($wall -gt 0) { (($cpu2 - $cpu1) / ($wall * $logical)) * 100 } else { 0 }
$report = @"
process=$($p.ProcessName)
pid=$($p.Id)
logicalProcessors=$logical
idleWallMs=$([int]$wall)
cpuDeltaMs=$([int]($cpu2 - $cpu1))
idleCpuPercentOfMachine=$([math]::Round($idlePct, 4))
workingSetStartBytes=$ws1
workingSetEndBytes=$ws2
privateStartBytes=$priv1
privateEndBytes=$priv2
note=Main window is also open; this is not floating-only memory.
"@
Set-Content -Path (Join-Path $out "idle-60s.txt") -Value $report -Encoding UTF8
Stop-Process -Id $proc.Id -Force
Get-Content (Join-Path $out "idle-60s.txt")
