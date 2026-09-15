param()
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "_common.ps1")
$root = Get-FlowNoteRoot
Set-Location $root

$cpu = Get-CimInstance Win32_Processor | Select-Object -First 1
$os = Get-CimInstance Win32_OperatingSystem
$gpu = Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, AdapterRAM
$dotnet = & dotnet --info
$head = (git rev-parse HEAD).Trim()
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
$payload = [ordered]@{
    collected_utc = [DateTimeOffset]::UtcNow.ToString("o")
    repo = $root
    branch = $branch
    commit = $head
    os = $os.Caption
    os_build = $os.BuildNumber
    os_arch = $os.OSArchitecture
    cpu_name = $cpu.Name
    logical_processors = $cpu.NumberOfLogicalProcessors
    ram_gb = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
    gpu = @($gpu | ForEach-Object { [ordered]@{ name = $_.Name; driver = $_.DriverVersion; adapter_ram = $_.AdapterRAM } })
    sdk_info = ($dotnet | Out-String)
    engine_zip_present = Test-Path (Join-Path $root "artifacts\assist-v4_1\downloads\llama-b10964-bin-win-cpu-x64.zip")
    gguf_present = Test-Path (Join-Path $root "artifacts\assist-v4_1\downloads\Qwen3-4B-Q4_K_M.gguf")
    ollama_command = [bool](Get-Command ollama -ErrorAction SilentlyContinue)
}
$outDir = Join-Path $root "docs\assist-v4_1"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$json = $payload | ConvertTo-Json -Depth 6
Set-Content -Path (Join-Path $outDir "ENVIRONMENT.json") -Value $json -Encoding utf8
Write-Host "Wrote docs/assist-v4_1/ENVIRONMENT.json"
