# Assist V4 진행

마지막 갱신: 2026-09-15  
저장소: `C:\GitRepositories\FlowNote`

## 판정

부분 구현 — CORE PASS (109 tests) — MODEL_REAL BLOCKED — WINDOWS_UI 부분(`--assist-smoke` PASS, 사람 검수 NOT RUN)

## 단계

| 단계 | 상태 | 메모 |
|---|---|---|
| P0 | 완료 | Ollama 없음. 설치하지 않음. |
| P1 | 완료 | schema 3. 실패 검사 마이그레이션은 4. |
| P2 | 완료 | save 트랜잭션 outbox. 30s fake에 저장 비대기 테스트. |
| P3 | 완료 | 규칙/validator/정책. 결정적 유닛 테스트. |
| P4 | 코드만 | Ollama 클라이언트 구현. 실측 BLOCKED. |
| P5 | 완료(fake/규칙) | assignment/정정/후보. 모델 origin 실적용 없음. |
| P6 | 완료 | projector A deferred / interrupted. |
| P7 | 코드+assist-smoke | 미니맵·배지·정정. 자동 A/B GUI 스모크 PASS. 사람 IME/DPI/워크스루 NOT RUN. |
| P8 | 코드+설정 스모크 | 보고 provenance, 설정 규칙만/외부 AI 없음. 백업은 DB 전체. |
| P9 | 부분 | 테스트/publish/assist-smoke. 모델 eval·고배율·IME·E2E 모델 경로 NOT RUN. |

재개:

```text
powershell -NoProfile -File scripts/assist-v4/Build.ps1
powershell -NoProfile -File scripts/assist-v4/Smoke-Ui.ps1
powershell -NoProfile -File scripts/assist-v4/Test-Core.ps1
```

모델 실측은 사용자 허가 후 Ollama 전용 11435 준비 때만 가능하다.
