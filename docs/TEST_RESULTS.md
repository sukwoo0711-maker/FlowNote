# 검수 결과

현행 핵심 제품 검수는 `docs/CORE_PRODUCT.md`다. 아래는 2026-09-15 V3 기록이다.

기록일: 2026-09-15  
단계: V3 제품 흐름. 외형 최종 승인 아님. 판매 성공·해자 확보를 이 결과로 주장하지 않는다.  
앱 빌드: FlowNote.Desktop Release / `net10.0-windows` / SDK 10.0.401 / self-contained win-x64  
OS: Microsoft Windows 11 Home x64 빌드 26200  
표시: 주 모니터 3440×1440, 시스템 DPI 96(100%), `capsule-metrics.txt` dpiScale=1  
실행 파일: `artifacts/win-x64/FlowNote.Desktop.exe` (2026-09-15 10:02, 162,304 bytes 본체)

이 파일의 PASS는 실제로 실행·관찰한 항목에만 사용한다. RenderTargetBitmap과 `*-screen.png`(PrintWindow)를 같은 OS 검증으로 합치지 않는다.  
V3 검수표: `FlowNote_Agent_Execution_v3/FlowNote_Agent_Execution_v3/02_ACCEPTANCE_TESTS.md`.

---

## V3 요약

| 구분 | 결과 |
|---|---|
| `scripts/test.ps1` | Core **44** + Infrastructure **42** = **86 통과 / 0 실패**. 기존 테스트를 삭제하지 않음 |
| `scripts/smoke.ps1` | PASS, entries=16, `artifacts/smoke-output/smoke-result.txt` |
| 예시 ZIP | `artifacts/v3/selected-report.zip` — 단위 테스트 시나리오 DB. 미선택 PRIVATE 본문/파일명 없음 |
| 사람 완주 | SCENARIO.md GUI, 초보자 사용성 = **HUMAN NOT RUN** |

## V3 항목

| ID | 상태 | 증거 / 이유 |
|---|---|---|
| V3-01 | PASS | 작업 전후 경로 `C:\GitRepositories\FlowNote`. `.git` 없음. 기존 테스트 유지 후 86 |
| V3-02 | PASS | `docs/product-v3/SUPERSEDED.md` |
| V3-03 | PASS | 제품 `FloatingCapsuleWindow` / `MainWindow`. `--ui-preview`만 바꾸지 않음 |
| V3-04 | PASS | `docs/screenshots/capsule/capsule-metrics.txt` 520×52 DIP, OS 536×68, WindowStyle=None |
| V3-05 | NOT RUN | 접힌 패널 위치에서 다른 앱 클릭 미실시 |
| V3-06 | PARTIAL | 성공은 overlay `SuccessFlash`. 스모크 `docs/screenshots/v3/v3-capsule-saved.png`. Ctrl+A/복사/빈 Enter 수동 없음 |
| V3-07 | NOT RUN | 실제 Windows IME 미실시. 단위 가드만 |
| V3-08 | NOT RUN | Shift+Enter 수동 없음 |
| V3-09 | PARTIAL | 20000자 초과 스모크 `07-save-error.png`. 파일/DB 실패 주입 UI 없음 |
| V3-10 | PASS | 이미지 단독 스모크 + 단위 테스트 |
| V3-11 | NOT RUN | 패널 유지/Escape/외부창 조합 수동 없음 |
| V3-12 | NOT RUN | 다른 앱 입력 중 갱신 미실시 |
| V3-13 | NOT RUN | 영역 캡처 드래그 미실시 |
| V3-14 | PASS | 미연결 한글 메모 스모크 |
| V3-15 | PARTIAL | 스모크가 ContinueRecording 후 저장. 마우스 클릭 아님. `v3-capsule-linked.png`, `v3-work-context.png` |
| V3-16 | NOT RUN | A 초안 중 B 조회 수동 없음. 코드는 조회만으로 초안을 바꾸지 않음 |
| V3-17 | NOT RUN | 초안 충돌 MessageBox 경로 코드만. 스모크 미호출 |
| V3-18 | PARTIAL | `WorkLinkPersistenceTests`. 빈 세션 미연결은 단위. UI 재시작 초안 장면 없음 |
| V3-19 | PASS | `WorkLinkPersistenceTests` 재연결, recorded_at 유지 |
| V3-20 | PASS | 생명주기 이벤트 재연결 거부 단위 테스트 |
| V3-21 | NOT RUN | 초안 중 완료 후 저장 수동 없음 |
| V3-22 | PASS | `NextActionPersistenceTests` |
| V3-23 | PARTIAL | 공백/201자 단위. IME·UI 취소는 `v3-next-action-edit.png`만 캡처 |
| V3-24 | PARTIAL | 상세 `다음 행동으로 표시` 버튼 있음. 스모크 미실행 |
| V3-25 | PASS | 출처 편집 후 다음 행동 문구 유지 단위 테스트 |
| V3-26 | PARTIAL | 재연결 시 행동 이동 없음 단위. 휴지통 UI 없음(soft delete API) |
| V3-27 | PASS | 동일 request_id / stale version 단위 |
| V3-28 | PARTIAL | 완료와 다음 행동 비우기가 같은 트랜잭션. 중간 실패 주입 테스트는 없음 |
| V3-29 | PARTIAL | 재열기만으로 활성 문구 없음 단위. `이전 다음 행동 사용` UI 스모크 없음 |
| V3-30 | PASS | as-of 투영 단위 테스트 |
| V3-31 | PASS | migration 이전 날짜에 현재 값을 만들지 않음 단위 |
| V3-32 | PASS | `V3ScenarioAndReportTests` — 마지막 기록은 N-A3, 완료 이벤트가 밀어내지 않음 |
| V3-33 | PARTIAL | 첨부 중복 억제 쿼리. 삭제 후 출처 이동 UI 미실시 |
| V3-34 | NOT RUN | 느린 A / 빠른 B 레이스 미실시 |
| V3-35 | PARTIAL | 이어서 기록은 캡슐 연결만. 타이머 없음. 전체 흐름 날짜범위 수동 없음 |
| V3-36 | PASS | `ContinueWorkSorter` 단위 |
| V3-37 | PARTIAL | 시간순/업무별/날짜 이동 스모크. 파노라마는 미구현 안내 |
| V3-38 | PARTIAL | 빈 날짜 `01-main-empty.png`. 짧은 메모 1개만 있는 날은 따로 없음 |
| V3-39 | PARTIAL | 혼합 타임라인 `03-main-timeline.png`. 카드 제목은 `이미지 기록` |
| V3-40 | NOT RUN | Tab/Space/Enter 접근성 수동 없음 |
| V3-41 | PARTIAL | 폭 720 `11-main-narrow.png`. 960×640 전용 캡처 없음 |
| V3-42 | PARTIAL | 업무별 `08-main-bywork.png`. 파노라마 `09-main-panorama.png` 안내만 |
| V3-43 | PASS | 스모크 완료/실행취소/재완료 + 단위 |
| V3-44 | PARTIAL | 근거 없는 완료는 서비스가 허용. 빈 업무 체크 전용 스모크 없음 |
| V3-45 | PASS | `v3-day-report.png` 선택 0 · 파일 0 |
| V3-46 | PARTIAL | 업무 필터 목록. 그룹으로 당일 leaf 일괄 선택은 없음 |
| V3-47 | NOT RUN | 부모 해제 시 자식 해제 코드 있음. UI 미클릭 |
| V3-48 | PASS | 시나리오 ZIP에 PRIVATE-DO-NOT-EXPORT / sample-uart.log 파일명 없음 |
| V3-49 | PARTIAL | 기록 ID 입력으로 기간 밖 추가. 날짜 탐색 없음 |
| V3-50 | PARTIAL | as-of 단위. 전날 보고 UI 스모크 없음 |
| V3-51 | PARTIAL | `ReportBuilder.IsStale` 단위. 같은 창에서 원문 수정 후 장면 없음 |
| V3-52 | NOT RUN | 복사본 해시 변경 주입 없음 |
| V3-53 | NOT RUN | 보고 항목에서 원문 보기 미실시 |
| V3-54 | PASS | ZIP `report.md` / `manifest.json` / `attachments/` 상대 경로 단위 |
| V3-55 | PARTIAL | 저장 대화상자 취소 메시지 있음. 디스크 오류 주입 없음 |
| V3-56 | PASS | `Migration_2_preserves_existing_notes`. 실패 migration은 version 3 |
| V3-57 | PASS | `Backup_restores_next_action`. 설정 UI 없음 |
| V3-58 | PASS | 기존 날짜/seq 단위 테스트 유지 |
| V3-59 | NOT RUN | 150% DPI, 모니터 이동/제거 없음. 이번 캡처는 100% |
| V3-60 | NOT RUN | 이번 빌드 10,000건·유휴 60초 재측정 없음. 이전: `docs/screenshots/capsule/idle-60s.txt` (CPU 0%, WS ≈ 125 MiB, 메인 창 포함) |
| V3-61 | NOT RUN | 네트워크 제한·정책 환경 미실시. 원격 분석/AI/API는 추가하지 않음 |
| V3-62 | PARTIAL | `V3ScenarioSeeder` 1회 시딩 단위. 게시본에 `fixtures/v3` 복사. `--demo` GUI 전환 수동 없음 |
| V3-63 | NOT RUN | SCENARIO.md를 게시 exe에서 사람이 완주하지 않음 |
| V3-64 | PASS | 게시 경로 exe로 스모크 2회 기동 |
| V3-65 | HUMAN NOT RUN | 참여자 없음 |

## V3 화면 증거

RenderTarget: `docs/screenshots/v3/v3-capsule-linked.png`, `v3-capsule-saved.png`, `v3-work-context.png`, `v3-next-action-edit.png`, `v3-day-report.png`, `v3-day-report-preview.png`.  
PrintWindow: `v3-capsule-linked-screen.png`, `v3-work-context-screen.png`.  
빈 날짜 / 혼합 / 좁은 창 / 실패: `01-main-empty.png`, `03-main-timeline.png`, `11-main-narrow.png`, `07-save-error.png`.

---

## 캡슐 v2 / 이전 기록 (2026-09-14)

아래 A* 항목은 캡슐 v2 당시 기록이다. 테스트 개수 60은 당시 숫자이며 지금은 86이다.

이 파일의 PASS는 실제로 실행·관찰한 항목에만 사용한다. 캡슐 인수표는 `FlowNote_Floating_Capsule_v2/02_ACCEPTANCE.md`.

---

## 캡슐 v2 (이번)

- 가시 표면 520×52 DIP, OS 창 536×68 px, WindowStyle=None. 증거 `docs/screenshots/capsule/capsule-metrics.txt`.
- `scripts/test.ps1`: Core **32** + Infrastructure **28** = **60 통과 / 0 실패**. 기존 50을 삭제하지 않았고 IME 가드·레이아웃·최근3 선택 테스트를 추가했다.
- `scripts/smoke.ps1` PASS, entries=13. PrintWindow + 흰 배경 OS 캡처.
- 유휴 60s: CPU 기계 용량 대비 측정값 0%, WorkingSet ≈ 125.5 MiB (메인 창 포함, 플로팅만이 아님). Show 12회 dispatcher idle p95 3ms (프레임 완료 대기가 아님).  
앱 빌드: FlowNote.Desktop Release / `net10.0-windows` / SDK 10.0.401 / self-contained win-x64  
OS: Microsoft Windows 11 Home x64 빌드 26200  
표시: 주 모니터 3440×1440, 시스템 DPI 96(100%)

이 파일의 PASS는 실제로 실행·관찰한 항목에만 사용한다. `--ui-preview --smoke` 또는 `scripts/smoke.ps1` 종료 0을 전체 UI 검수 합격으로 쓰지 않는다.

자세한 범위: `docs/UI_RESCUE_U1U2.md`.

---

## 실행한 항목

### A01 환경 — 깨끗한 빌드

```text
ID: A01
상태: PASS
환경 / 앱 빌드: Windows 11 Home x64 26200, SDK 10.0.401, FlowNote.sln Release
입력 fixture / 재현 단계:
  1. scripts/build.ps1 (dotnet restore --locked-mode, build -c Release)
  2. scripts/test.ps1 (dotnet test -c Release --locked-mode restore)
  실제 결과: 경고 0, 오류 0. 테스트 통과 50 (Core 22 + Infrastructure 28), 실패 0.
기대 결과와의 차이: 없음.
명령 / 로그 / 실제 캡처 경로: scripts/build.ps1, scripts/test.ps1
수정 후 재검수 결과:
  publish -r win-x64가 lock 파일에 RID를 남겨 NU1004가 났고, Directory.Build.props에 RuntimeIdentifiers=win-x64를 넣은 뒤 --force-evaluate로 잠금 파일을 맞췄다. publish는 restore --locked-mode 후 --no-restore로 수행한다.
```

### A02 시작 — 첫 실행 안내

```text
ID: A02
상태: PASS
환경 / 앱 빌드: artifacts/win-x64/FlowNote.Desktop.exe --smoke
입력 fixture / 재현 단계: 신규 --data-root에서 기동. 상단 배너 표시 후 확인으로 닫힘.
실제 결과: 로컬 저장, 비암호화, 로그인 시 시작 OFF, 기록 창 항상 위 기본 ON을 안내. 로그인 없이 기록 가능.
기대 결과와의 차이: 모달 MessageBox가 아니라 메인 창 배너다.
명령 / 로그 / 실제 캡처 경로: docs/screenshots/00-onboarding.png, docs/screenshots/01-onboarding.png
```

### A04 입력 — 빈 입력 / 이미지 단독

```text
ID: A04
상태: PASS (단위 테스트). UI에서 공백만 저장 버튼은 같은 NoteRules를 탄다.
환경 / 앱 빌드: FlowNote.Infrastructure.Tests Release
입력 fixture / 재현 단계: Whitespace_only_note_is_rejected, Image_only_note_is_saved_without_body_text, Attachment_only_note_without_text_is_allowed_by_rule
실제 결과: 공백만 거부. 파일만 있는 기록은 저장.
기대 결과와의 차이: UI에서 공백 저장을 마우스로 누르는 장면은 별도 캡처하지 않음.
```

### A05 입력 — 영속 저장

```text
ID: A05
상태: PASS
환경 / 앱 빌드: artifacts/win-x64/FlowNote.Desktop.exe --smoke (2회 기동)
입력 fixture / 재현 단계:
  1. 한글 본문 "보드 전원 시퀀스를 확인했다" + smoke-note.txt + smoke-board.png 저장
  2. 할 일 추가 후 완료
  3. 프로세스 종료 후 같은 --data-root로 재실행
실제 결과: 재실행 후 원문·첨부 사본·완료 이력 유지. entries=3.
기대 결과와의 차이: 없음.
명령 / 로그 / 실제 캡처 경로:
  artifacts/smoke-output/smoke-result.txt
  docs/screenshots/03-main-timeline.png
  docs/screenshots/05-main-after-restart.png
```

### A09 요청 중복

```text
ID: A09
상태: PASS (단위 테스트 Same_request_id_does_not_create_a_second_entry, Same_complete_request_id_does_not_duplicate_event)
```

### A15–A21 TODO 완료 이력

```text
ID: A15–A21
상태: PASS (단위 테스트 WorkItemPersistenceTests, DayAggregatorTests). UI 스모크에서 완료 후 활성 목록 0개, 타임라인에 완료 행.
실제 캡처: docs/screenshots/03-main-timeline.png, docs/screenshots/04-floating-after-complete.png
```

### A25–A27, A29–A30 첨부

```text
ID: A25–A27, A29–A30
상태: PASS (단위 테스트 + 스모크 첨부 2개). A28 복사/커밋 실패 주입, A31 깨진 PNG UI, A32 클립보드 잠금은 NOT RUN.
캡처: docs/screenshots/03-main-timeline.png (썸네일·smoke-note.txt, smoke-board.png)
```

### A33 창간 일치

```text
ID: A33
상태: PASS (스모크가 같은 ViewModel/DB로 플로팅 저장 후 메인 목록을 다시 그림)
캡처: docs/screenshots/03-main-timeline.png, docs/screenshots/04-floating-after-complete.png
```

### A70 납품 실행 파일

```text
ID: A70
상태: PARTIAL
실제 결과: self-contained win-x64 폴더를 이 개발 PC에서 실행해 기록→첨부→완료→타임라인→재실행을 재현했다. SDK 없는 별도 Windows, 내보내기는 하지 않았다.
명령: scripts/publish.ps1, scripts/smoke.ps1
실행 파일: artifacts/win-x64/FlowNote.Desktop.exe (162,304 bytes 본체 + 런타임 동봉, 파일 262개)
```

---

## 아직 실행하지 않은 항목

| ID | 상태 | 이유 |
|---|---|---|
| A03 | NOT RUN | 일반/데모 전환 후 자료 섞임 검사를 수동으로 하지 않음. 경로는 분리됨(단위 테스트). |
| A06 | NOT RUN | 실제 한국어 IME 조합 중 Enter/Ctrl+Enter를 사람이 치지 않음. |
| A07 | PARTIAL | 초안 본문+첨부 스테이징 복구는 단위 테스트 PASS. UI에서 프로세스 강제 종료 후 복구 장면은 캡처하지 않음. |
| A08 | NOT RUN | 저장 실패 주입 UI. |
| A10–A14 | PARTIAL | 자정 경계·동일 시각 seq·사용자 발생시각은 단위 테스트 PASS. 타임라인 UI에서 자정/밀집 스크롤은 미실행. |
| A22–A24 | PARTIAL | 재열기/취소는 단위 테스트. UI 탭 없음. |
| A28, A31, A32 | NOT RUN | 실패 주입·깨진 이미지·클립보드 잠금. |
| A35–A45 | NOT RUN/PARTIAL | 파노라마·업무 관리 탭·필터 일부 없음. 검색 단위 테스트 PASS, 검색 UI 캡처 없음. |
| A46–A55 | NOT RUN | 내보내기·백업·휴지통 미구현. |
| A56–A64 | PARTIAL | 핀/트레이는 구현. 전역 단축키·DPI 전환·좁은 창 오버레이 미검증. |
| A65–A68 | NOT RUN | 오프라인·성능 측정 미실행. |
| A69 | PARTIAL | 필수 캡처 목록 중 onboarding, empty, chronological, floating을 실창으로 남김. panorama/report/settings는 없음. |
| A71–A76 | PARTIAL | 마이그레이션 실패 시 데이터 보존 단위 테스트 있음. DST UI, 자동실행, 복원 없음. |

---

## 캡처 메타데이터

| 파일 | 내용 | 데모 | 빌드 | 해상도 | 배율 |
|---|---|---|---|---|---|
| docs/screenshots/00-onboarding.png | 첫 실행 안내 배너 | smoke data-root | self-contained win-x64 | 창 렌더 | 100% |
| docs/screenshots/01-main-empty.png | 기록 0개인 오늘 | smoke | 동일 | 창 렌더 | 100% |
| docs/screenshots/02-floating-empty.png | 기록 창 (확장) | smoke | 동일 | 창 렌더 | 100% |
| docs/screenshots/03-main-timeline.png | 메모+첨부+생성+완료 | smoke | 동일 | 창 렌더 | 100% |
| docs/screenshots/04-floating-after-complete.png | 완료 후 활성 TODO 0 | smoke | 동일 | 창 렌더 | 100% |
| docs/screenshots/05-main-after-restart.png | 재실행 후 동일 타임라인 | smoke | 동일 | 창 렌더 | 100% |
| docs/screenshots/06-floating-after-restart.png | 재실행 후 기록 창 | smoke | 동일 | 창 렌더 | 100% |

캡처 방법은 실행 중인 WPF 창의 `RenderTargetBitmap`이다. 참고 PNG를 창으로 쓰지 않았다. 스모크는 저장/완료를 ViewModel API로 수행했고, 그 API는 저장·완료 버튼과 같다. 마우스로 버튼을 누른 영상은 없다.
