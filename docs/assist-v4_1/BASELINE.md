# V4.1 baseline (2026-09-15)

조사 시각: 이 문서를 작성한 작업 세션. 저장소는 `C:\GitRepositories\FlowNote`. 브랜치 `main`, 작업 시작 커밋 `706bb48d737527d5cbae7c488dccd0ebd7803f05`. V4.1 코드는 이 커밋 위의 작업 트리 변경이다. `git reset`/`clean`은 하지 않았다.

## 앱

- 솔루션: `FlowNote.sln` (.NET 10, WPF `net10.0-windows`, RID `win-x64`)
- 진입: `src/FlowNote.Desktop/FlowNote.Desktop.csproj` → `FlowNote.Desktop.exe`
- 제품 버전: `Directory.Build.props` **0.4.0** (V4.1은 작업 범위 이름이며 강제 버전 bump 없음)
- 데이터: `%LOCALAPPDATA%\FlowNote\live|demo`, `--data-root`로 격리
- 기존 테스트: `dotnet test FlowNote.sln -c Release` — V4.1 이전 CORE 109 + 이번 Embedded 테스트. 기존 assertion을 삭제하지 않음.

## 기존 V4 AI

- 런타임 DI는 더 이상 Ollama를 선택하지 않는다. `OllamaContextInference`는 역사 비교용으로 남기고 앱 시작 경로에서 생성하지 않는다.
- 설정 기본은 **규칙만**. 문자열 `외부 AI 없음` 유지 (assist-smoke).
- outbox/lease/user lock/규칙 엔진/프로젝터는 유지.

## 이 PC

- Windows 11 Home x64 build 26200, Ryzen 7 9800X3D, RAM ≈ 61.6 GiB, RTX 5080 16GB (CUDA는 이번 필수 경로가 아님).
- 상세: `docs/assist-v4_1/ENVIRONMENT.json` (`scripts/assist-v4_1/Inspect-Environment.ps1`).
- 사용자 Ollama 프로세스/설치는 조회만 하고 종료·삭제하지 않음.

## 고정 자산 (관측)

- llama.cpp nightly **b10964** commit `b29c606e28a01b1bc8c1351026a0fa6e616bf6c4`
- zip `llama-b10964-bin-win-cpu-x64.zip` SHA-256 `917f39c076402c421224824607397af20f53625a60defc20e8dd22446bf4c5d7` / 18427629 bytes
- 모델 `Qwen3-4B-Q4_K_M.gguf` revision `bc640142c66e1fdd12af0bd68f40445458f3869b` SHA-256 `7485fe6f11af29433bc51cab58009521f205840f5b4ae3a32fa7f92e8534fdf5` / 2497280256 bytes
- lock: `packaging/ai-runtime/engine-lock.json` (null/REPLACE/latest 없음)

## UI

- 기본 캡슐 520×52 DIP. 설정에 모델 가져오기 / 다시 시도 / AI 없이 계속. 포트·API 키 입력 필드 없음.
