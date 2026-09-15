# Assist V4 인수 결과

실행일: 2026-09-15  
저장소: `C:\GitRepositories\FlowNote`  
판정 한 줄: **부분 구현 — CORE PASS — MODEL_REAL BLOCKED — WINDOWS_UI 부분**

이 문서는 통과·차단·미실행을 섞지 않는다. 규칙/픽스처 시드 성공을 모델 통과로 세지 않는다.

## 층별 판정

| 층 | 판정 | 근거 |
|---|---|---|
| CORE | **PASS** | `dotnet test FlowNote.sln -c Release` → Core 59, Infrastructure 50, **합계 109 통과 / 0 실패**. 마이그레이션 3, outbox, 규칙, A/B projector, 정정 lock, 저장 비대기 포함. |
| MODEL_REAL | **BLOCKED** | Ollama CLI 없음. 모델 pull/11435 기동 없음. `docs/assist-v4/MODEL_EVAL.json`. `semantic_auto_apply`는 false 유지. |
| WINDOWS_UI | **부분** | `--assist-smoke` **PASS** (실제 WPF 창, 격리 `--data-root`). 시드는 A_deferred **expected JSON**이라 MODEL_REAL이 아니다. 사람 IME·150% DPI·다른 앱 포커스·HUMAN 워크스루는 **NOT RUN**. |

## 구현된 것 (기존 앱)

- 스키마 마이그레이션 **3**: ContextThread / Assignment / Mention / ActionCandidate / AnalysisJob / AnalysisRun / Correction / Alias.
- 메모 저장 트랜잭션에 analysis outbox 삽입. UI 저장 경로는 모델을 기다리지 않음.
- AnalysisWorker: lease 120s, 재시도 3, backoff 5s/30s, 회로차단 3/60s. DB ctor에서 시작하지 않음. `App`에서 시작. `--assist-smoke`에서는 worker를 켜지 않음.
- 규칙 R1 lock/해제, R2 유일 이슈키, R3 별칭. 역할은 UNKNOWN일 수 있음. 규칙만으로 PERFORMED 확정 없음.
- Ollama 클라이언트 코드: `http://127.0.0.1:11435`, qwen3:4b, `/api/chat`, stream=false, think=false, proxy/redirect 없음. **실호출 평가 없음**.
- 사용자 정정: 다른 묶음 / 새 묶음 / 해제+lock / undo. 늦은 job은 stale.
- DayFlowProjector: 요청이 수행 에피소드를 쪼개지 않음. 실제 수행 전환만 분리.
- 캡슐 높이 계약은 스모크가 520×52를 유지. 메인 미니맵·설정 OFF/규칙만/로컬 보조.
- `--assist-demo` / `--assist-smoke` + 격리 `--data-root`: fixture **expected JSON을 시드** (MODEL_REAL 아님).
- 선택 보고 markdown에 `파생 연결:` provenance. 자동 후보를 공식 완료/next_action으로 바꾸지 않음.
- 백업은 DB 파일 전체 복사 → 파생 테이블 포함.

## 스키마

| 버전 | 내용 |
|---|---|
| 1 | 초기 Entry/WorkItem/첨부 |
| 2 | next_action |
| 3 | assist 파생 테이블 (이번) |

실패 마이그레이션 검사는 버전 **4** 잘못된 SQL로 유지. 기존 행 보존 테스트 통과.

## CORE에서 확인한 것

- A 수행 중 B 요청만 있으면 A 에피소드가 갈라지지 않음. B 수행은 이후 기록부터.
- A→B 수행→A 수행은 에피소드 3개, 1번째와 3번째 thread ID 동일.
- 저장 후 30초 fake를 붙여도 저장 완료는 2초 안.
- 사용자 해제+lock 뒤 규칙은 재연결하지 않음.
- 기존 테스트 삭제/약화 없음.

## WINDOWS_UI에서 확인한 것 (`--assist-smoke`)

실제 publish exe, 격리 `artifacts/assist-v4/smoke-data`. 결과: `docs/assist-v4/screenshots/assist-smoke-result.txt`.

| 확인 | 결과 |
|---|---|
| 2026-09-15 메모 5개 | PASS |
| 미니맵 lane 2개, 첫 lane 관측 공백, 둘째 lane 요청, 요청이 A lane을 끊지 않음 | PASS |
| 범례 `기록 기반`, 금지 문구(점심/집중/92%/추천/근무시간) 없음 | PASS |
| 타임라인 요청 배지, 완료 언급 배지 | PASS |
| 연결 해제 → `manual_clear` + lock | PASS |
| 설정 기본 `규칙만`, `외부 AI 없음` | PASS |
| 캡슐 저장 후 `기록됨` overlay, 크기 520×52 DIP | PASS |
| 좁은 창 720 캡처 | 캡처함 |

캡처: `docs/assist-v4/screenshots/v4-minimap-ab.png`, `v4-timeline-request.png`, `v4-detail-correction.png`, `v4-settings.png`, `v4-capsule-saved.png`, `v4-narrow.png`.

이 스모크는 합성 시드다. 모델이 문장을 분류한 GUI가 아니다. 사람 클릭·IME·고배율 HUMAN PASS가 아니다.

## 실패·차단

| 항목 | 상태 | 이유 |
|---|---|---|
| qwen3:4b 실측 | BLOCKED | Ollama 미설치. 설치/pull 권한 없음. |
| 의미 자동 연결 기본값 | 꺼짐 | 평가 미실시. fake/시드를 semantic_auto_apply로 켜지 않음. |
| 외부 API 대체 | 하지 않음 | 계약 준수. |

## 미검증 (코드는 있으나 이 환경에서 사람/실측 없음)

| 항목 | 상태 |
|---|---|
| 한국어 IME 조합 중 Enter | NOT RUN |
| OS 배율 150%/다른 모니터 | NOT RUN (스모크 dpiScale 기본) |
| 다른 앱 입력 중 포커스 | NOT RUN |
| 트레이/다중 인스턴스 충돌 사람 확인 | NOT RUN (`--data-root` mutex 해시는 코드에 있음) |
| 메인에서 A/B를 사람이 클릭해 보는 워크스루 | NOT RUN (자동 스모크만) |
| 설정에서 LOCAL_ASSIST 켠 뒤 모델 오류 UI | NOT RUN |
| 개발 SDK 없는 PC에서 publish 실행 | NOT RUN (이 PC에서 publish는 됨) |
| Worker WorkingSet/GPU | NOT RUN |
| canonical 문장을 모델이 분류 | NOT RUN |

## 실행 빌드

- `artifacts/win-x64/FlowNote.Desktop.exe`
- `artifacts/assist-v4/app/` (동일 publish 복사)

## 스크립트

```text
powershell -NoProfile -File scripts/assist-v4/Test-Core.ps1
powershell -NoProfile -File scripts/assist-v4/Test-Model.ps1
powershell -NoProfile -File scripts/assist-v4/Build.ps1
powershell -NoProfile -File scripts/assist-v4/Smoke-Ui.ps1
powershell -NoProfile -File scripts/assist-v4/Run-Demo.ps1
```

Run-Demo와 Smoke-Ui는 **합성 expected 시드**다. 모델이 연결했다고 표시하지 말 것.
