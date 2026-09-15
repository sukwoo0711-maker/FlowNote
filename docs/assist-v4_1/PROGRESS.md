# V4.1 진행

| 단계 | 상태 | 메모 |
|---|---|---|
| M0 조사 | 완료 | `docs/assist-v4_1/BASELINE.md` |
| M1 자산/lock | 완료 | b10964 CPU zip + Qwen3-4B-Q4_K_M.gguf 해시 고정 |
| M2 Job Object | 코드+단위시험 | `NativeJobProcess`, ping 자식, TCP listen PID |
| M3 HTTP 어댑터 | 코드+단위시험 | `ManagedLlamaInference` → `/v1/chat/completions` |
| M4 큐 | 기존 V4 유지 | AnalysisWorker가 자산 없으면 엔진 기동 시도 |
| M5 설정 UX | 코드 | 가져오기/다시 시도/AI 없이 계속. 포트 필드 없음 |
| M6 회귀 | CORE PASS / MODEL_REAL FAIL | 24×3 전원 기권. 엔진 JSON 경로는 동작 |
| M7 패키징 | 부분 | 스테이징 검증 PASS, ZIP64 오프라인 팩 생성. 깨끗한 VM NOT RUN |

활성 DI에 Ollama fallback 없음.
