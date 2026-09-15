# V4 P0 기준선

- 기존 테스트(V3 포함)를 삭제하지 않는다.
- Ollama CLI: **없음** (`Get-Command ollama` 실패). MODEL_REAL은 설치 없이 BLOCKED.
- 포트 11434/11435: 리스너 없음.
- OS: Windows 11 Home x64 26200, 3440×1440, RAM ≈ 61.6 GiB, CPU Ryzen 7 9800X3D, GPU RTX 5080.
- 환경 JSON: `docs/assist-v4/ENVIRONMENT.json` (Read-Environment.ps1, 설치 없음).

baseline 테스트 개수는 이번 세션에서 `scripts/test.ps1`으로 다시 센다. 과거 86을 복사하지 않는다.
