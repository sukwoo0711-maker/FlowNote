# V4.1 검수 결과

작성: 2026-09-15. 환경은 `docs/assist-v4_1/ENVIRONMENT.json`.

판정 요약은 대화 첫 줄을 따른다. 아래는 레이어별 증거다.

## CORE — PASS

명령: `powershell -NoProfile -File scripts/assist-v4_1/Test-Core.ps1`  
결과: Core 59 + Infrastructure 59, 실패 0. 기존 V4 assertion을 삭제하지 않았다. 앱 DI는 `ManagedLlamaInference`만 사용한다.

## ENGINE_REAL — PASS

명령: `powershell -NoProfile -File scripts/assist-v4_1/Test-Engine.ps1` (exit 0)  
증거: `docs/assist-v4_1/engine-capabilities.json`

관측: 앱 관리 `llama-server` b10964 CPU, alias `flownote-local-*`, port 55766, `pidOwned=true`, 무키 요청 `engine-auth-failed`, `--offline --no-webui --host 127.0.0.1`, `json_object+schema` 응답.

## MODEL_REAL — FAIL

명령: `powershell -NoProfile -File scripts/assist-v4_1/Test-Model.ps1 -Repeats 3` (exit 1)  
증거: `docs/assist-v4_1/MODEL_EVAL.json`

| 지표 | 관측 | 목표 |
|---|---|---|
| unique sentences × repeats | 24 × 3 = 72 | 72 |
| schema_pass_rate (post-validator ErrorCode==null) | 0.708 | ≥0.95 |
| determinate_accuracy | 0 | ≥0.85 |
| coverage_non_abstain | 0 | 전부 기권 아님 |
| forced_link_on_abstain | 0 | 0 |
| all_abstain | true | false |

정답을 입력에 넣지 않았다. 스키마 enum을 약화하지 않았다. 다른 모델로 바꿔 통과시키지 않았다. 엔진은 JSON을 반환하나 이 GGUF·프롬프트 조합은 평가 문장에서 사실상 전원 기권이다.

## WINDOWS_UI — PASS (smoke 범위)

명령: `powershell -NoProfile -File scripts/assist-v4_1/Smoke-Ui.ps1` (exit 0)  
증거: `docs/assist-v4_1/screenshots/` (캡슐 520×52, 설정에 가져오기/다시 시도/AI 없이 계속, 포트 입력 없음).

시드는 A_deferred **expected JSON**이며 MODEL_REAL이 아니다. IME 대 다른 앱 포커스, 150% DPI는 이 세션에서 NOT RUN.

## PACKAGING — 부분

- 스테이징 폴더 해시 검증: `Verify-Package.ps1 -ExpectModel` **PASS**
- ZIP64: `artifacts/assist-v4_1/dist/FlowNote-0.4.0-win-x64-offline-ai.zip` (2521554989 bytes) SHA-256 `7d4793d24d9ae6eeaf56c9874c85d6770e977f85efbbc715d8d1ebf8b8551808`
- SDK/Ollama 없는 별도 Windows 사용자·VM 기동: **NOT RUN** (이 PC는 SDK가 있는 개발 환경)
- CUDA 엔진 팩: **NOT RUN** (CPU만 검증)

## E001–E064 요약

PASS로 관측한 것: E001 조사, E002 기존 테스트 유지, E003 비AI 회귀 테스트 유지, E004 활성 DI/v4.1 스크립트에서 Ollama 제거, E005 unresolved lock 거부, E017 직렬 Chat 게이트, E020 무키 401, E021 loopback+차단 플래그, E033 `/v1/chat/completions`, E048/E054/E055/E058 smoke 범위, E062 스크립트 실제 실행.

FAIL: E040 24×3 의미 평가.

NOT RUN / 부분: E008 GPU, E013 한글 경로 이중 프로필 실측, E014 읽기전용 설치, E024/E025 강제종료 20회, E027 idle 120초 경계 실측, E031 타 AI 프로세스 병행, E041 네트워크 관찰 도구, E043 실제 모델 A/B 서비스 완주, E056 IME, E057 타 앱 포커스, E059 150% DPI, E060 자원 반복 측정, E061 깨끗한 VM.
