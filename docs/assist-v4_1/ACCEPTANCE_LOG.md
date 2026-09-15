# V4.1 실행 로그

## Inspect-Environment.ps1
상태: 성공. `docs/assist-v4_1/ENVIRONMENT.json`

## Prepare-Assets.ps1
상태: 성공 (기존 zip/GGUF 검증, 다운로드 없음). `artifacts/assist-v4_1/pack`

## Build.ps1 / Test-Core
상태: 성공. `dotnet test` Core 59 + Infrastructure 59. Publish `artifacts/assist-v4_1/app`

## Test-Engine.ps1
상태: **PASS** (exit 0, ~18s after WPF UI-thread deadlock fix)
증거: `docs/assist-v4_1/engine-capabilities.json`
- alias `flownote-local-*`, port 55766 (not 11434/11435)
- pidOwned=true, authFailedWithoutKey=true
- `--offline --no-webui --no-slots --no-agent --host 127.0.0.1`
- responseFormat `json_object+schema`, probeError=null

첫 시도는 OnStartup 스레드에서 `GetResult()`로 HttpClient 대기가 교착되어 8분 타임아웃. `Task.Run`으로 분리 후 통과.

## Test-Model.ps1
상태: **FAIL** exit 1 (~13.5분, 72건). `docs/assist-v4_1/MODEL_EVAL.json` — schema 70.8%, 정확도 0, all_abstain.

## Smoke-Ui.ps1
상태: **PASS** exit 0. `docs/assist-v4_1/screenshots/`

## Publish-Offline.ps1
Compress-Archive는 2GiB ZIP32 한도에 실패. `tar.exe -a` ZIP64로 교체 후
`artifacts/assist-v4_1/dist/FlowNote-0.4.0-win-x64-offline-ai.zip` 생성.
Verify-Package -ExpectModel: PASS.
