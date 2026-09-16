# V4.1에서 대체된 실행 계약

**2026-09-16:** 아래 V4.1 로컬 보조 확대도 핵심 제품의 기본 실행 경로가 아니다. 기본은 AI 끔. 현행 기준은 `docs/CORE_PRODUCT.md`.

이 파일은 **활성 실행 절차가 아니다**. 아래 항목은 V4.1 앱 경로에서 사용하지 않는다.

| 이전 V4 | V4.1 대체 |
|---|---|
| 사용자 Ollama 설치 / `ollama serve` / `ollama pull` | 앱이 동봉 `llama-server.exe`를 Job Object로 기동 |
| `qwen3:4b` tag + Ollama digest | `Qwen3-4B-Q4_K_M.gguf` + SHA-256 + engine lock fingerprint |
| 고정 `127.0.0.1:11435` | 앱이 고른 loopback ephemeral 포트 |
| `POST /api/chat` | `POST /v1/chat/completions` |
| `OLLAMA_*`, `keep_alive` | 자식 환경에서 `OLLAMA_*`/`LLAMA_ARG_*`/proxy 제거, idle 120초 후 자기 자식만 종료 |
| `scripts/assist-v4/Test-Model.ps1`의 Ollama CLI 분기 | `scripts/assist-v4_1/Test-Model.ps1` → `FlowNote.Desktop.exe --assist-eval` |
| 사용 안내의 “Ollama가 있을 때만 실측” | `docs/USAGE.md`의 동봉 엔진 설명 |

역사 문서 `docs/assist-v4/*`, `FlowNote_Local_Assist_V4/*`의 Ollama 문장은 보존하되 현행 실행 계약이 아니다. 사용자 PC의 기존 Ollama 설치·프로세스는 삭제/종료하지 않는다.
