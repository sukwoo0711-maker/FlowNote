# Floating Capsule v2 — 인수 체크리스트

기록: 2026-09-14. 실행 빌드 `artifacts/win-x64/FlowNote.Desktop.exe`. 스모크 `--data-root artifacts/smoke-data`.
환경: Windows 11 Home x64 빌드 26200, 주 모니터 3440×1440, DPI 96(100%), .NET 10.0.401.
캡슐 DIP 520×52, 창 DIP 536×68, OS bounds 536×68 px at 2880,24 (`docs/screenshots/capsule/capsule-metrics.txt`).

상태: PASS=실행 근거 있음 / PARTIAL=일부만 / FAIL=실제 실패 / NOT RUN=미실행 / BLOCKED=환경·정책상 차단.

| ID | 항목 | 실행 | 기대 결과 | 상태 | 증거 |
|---|---|---|---|---|---|
| V01 | 기본 크기 | 스모크 실창 | 가시 표면520×52 DIP | PASS | capsule-metrics.txt, 01-capsule-idle-light.png |
| V02 | 실제 창 크기 | GetWindowRect | 536×68, 여백 8 DIP | PASS | capsule-metrics.txt `windowDip=536x68` `osBoundsPx=536x68` |
| V03 | 기본 제목표시줄 | PrintWindow | 제목줄/최소/최대/X/grip 없음 | PASS | 02-floating-empty-screen.png, windowStyle=None |
| V04 | 형태 | 흰 배경 OS 캡처 | 반경26 알약. 흰 사각 창 아님 | PASS | 01-capsule-idle-light.png. 어두운 CopyFromScreen은 대비 약함 → V07 |
| V05 | 기본 내용 | 초안없음 TODO0 | 한 줄+아이콘만 | PASS | 01-capsule-idle-light.png, 02-floating-empty-screen.png |
| V06 | 버튼 구조 | 빈/입력 비교 | 오늘·고정·접기·숨기기 없음. 기록 슬롯 유지 | PASS | idle vs 03-capsule-typing.png |
| V07 | 재질·대비 | 흰 문서 / 어두운 배경 | 짙은 청록–슬레이트, 흰 입력폼 없음 | PARTIAL | 흰 배경 PASS. `02-capsule-idle-dark.png`는 캡슐이 거의 안 보임(대비/z-order). PrintWindow 흑 배경은 02-floating-empty-screen.png |
| V08 | 최근 패널 | 오늘 최신3 | 아래 패널, 3행, 유지/하루 전체 | PASS | 04-recent-3.png |
| V09 | TODO 패널 | 3개 열기 | 3개+추가, 배지, 빈 입력 상시 없음 | PARTIAL | 05-todo-3.png. 0개/20개 전용 캡처는 없음 |
| V10 | 첨부 실물 | PNG 초안 | 실제 썸네일·이름·크기·제거 | PASS | 06-image-draft.png (녹색 64×40 fixture PNG) |
| V11 | 상태 구별 | 메인 시간순 | 생성/완료/다시 열림 문구·아이콘 | PASS | 03-main-timeline-screen.png |
| V12 | 크기 복원 | settings `floating.height=500` | 창 높이 68 유지 | PASS | 스모크가 500을 쓴 뒤 metrics 68 |
| B01 | 한 줄 기록 | VM 저장+타이핑 캡처 | 1회 저장, `HH:mm 기록됨`, 높이 유지 | PASS | smoke-result PASS, 03-capsule-typing.png, 04-recent-3.png 플래시 |
| B02 | 빈 입력 | CanSave | 빈 입력 기록 불가 | PASS | smoke `empty-cansave` |
| B03 | 한글 IME | 실 IME 키 | 조합 확정 Enter 미기록 | NOT RUN | 가드 단위테스트 PASS (`ImeEnterGuardTests`). 실 키보드 미실행 |
| B04 | 입력 범위 | 다른 컨트롤 Enter | 메모 핸들러 미가로채기 | NOT RUN | 코드: 메모/할 일/긴메모에 가드 분리. 수동 미실행 |
| B05 | 긴 메모 | Shift+Enter/붙여넣기 | 줄바꿈 유지 | NOT RUN | 코드 있음. 스모크는 실패 본문으로 긴 메모 패널만 캡처 |
| B06 | 초안 복구 | 스모크 2회 | 재시작 후 본문·첨부 | PASS | smoke run 2, 06-floating-after-restart |
| B07 | 첨부 입력 | VM AddPending | 자동 Entry 없음, 이미지 단독 가능 | PARTIAL | 이미지 단독 저장 PASS. Ctrl+V/드래그/파일창 클릭은 코드만 |
| B08 | 실패/재시도 | 20001자 | 다시 기록, 본문 유지 | PASS | 07-save-error.png, smoke assertion |
| B09 | 피드백 충돌 | 플래시 후 다른 입력 | 새 입력이 플래시를 대체 | PASS | 저장 플래시 후 후속 스모크 입력. 즉시 타이핑 수동은 없음 |
| B10 | 패널 전환 | Recent/Todo/Draft | 하나뿐, 접으면 창 축소 | PASS | 캡처 전환 + 완료 후 04-floating-screen 캡슐만 |
| B11 | 패널 유지 | 외부 앱 클릭 | 유지 ON이면 패널 잔류 | NOT RUN | 설정 키 `floating.pinnedPreview` 저장됨. 외부 앱 미실행 |
| B12 | 할 일 완료 | 완료/실행취소/재완료 | 이력 보존 | PASS | smoke + 03-main-timeline-screen.png |
| B13 | 메인 연결 | 같은 날 기록 | 타임라인에 동일 메모 | PARTIAL | 데이터/화면 연결 PASS. 최근행 마우스 클릭은 미실행 |
| B14 | 숨김/복귀 | 트레이 | 초안 보존 | NOT RUN | 로고 숨기기·트레이 코드 있음. 스모크 미실행 |
| C01 | 사용자 캡처 | 영역 드래그 | 초안 첨부, 확정 전 Entry 없음 | NOT RUN | `GdiRegionCaptureService`+오버레이 구현. 드래그 제스처 미실행 |
| C02 | 캡처 취소 | Esc/0크기 | 빈 이미지 없음 | NOT RUN | 코드 있음 |
| C03 | 캡처 위치 | 다른 DPI/음수 모니터 | 좌표 일치 | NOT RUN | 이 PC 100% 단일 모니터 |
| C04 | 캡처 권한 | 차단 | 우회 없음 | NOT RUN | 차단 시나리오 없음 |
| C05 | 비감시 | 유휴 60s | 상시 캡처/클립보드/녹화 없음 | PASS | idle-60s.txt, 캡처 타이머 없음 |
| W01 | 포커스 | 저장 갱신 | Activate 없음 | NOT RUN | 코드는 DataChanged에서 Activate하지 않음. 타인 앱 미재현 |
| W02 | 패널 활성화 | 파일창/메뉴 | 오닫힘 없음 | NOT RUN | BeginAttach로 파일창 중 닫힘 억제. 미실행 |
| W03 | 마우스 | 로고 드래그/클릭 | 임계치 구분 | NOT RUN | 코드 있음 |
| W04 | 투명 히트테스트 | 바깥 여백 클릭 | 뒤 앱 통과 | NOT RUN | AllowsTransparency. OS 클릭 통과 미측정 |
| W05 | 화면 가장자리 | 아래 공간 부족 | 패널 위쪽 | NOT RUN | 코드 `ShouldOpenPanelAbove`. 우상단 배치라 아래 공간은 충분 |
| W06 | DPI | 100% | DIP 일치 | PARTIAL | 100% PASS. 125/150/200 NOT RUN |
| W07 | 키보드/접근성 | 이름/버튼 | 실제 Button/TextBox | PARTIAL | AutomationProperties 있음. Tab/고대비 수동 없음 |
| P01 | 성능 | 60s+Show | 실측 | PASS | idle-60s.txt, capsule-warm-show.txt. 목표와 결과 구별 |
| R01 | 기존회귀 | test+smoke | 삭제/약화 없음 | PASS | Core 32 + Infra 28 = 60. 기존 50 유지+캡슐 10. smoke PASS entries=13 |
| E01 | 실행증거 | PrintWindow+배경 | RTT만 아님 | PASS | *-screen.png, 01-capsule-idle-light.png. dark CopyFromScreen는 약함 |
| E02 | 배포 | publish exe | 제품 플로팅=캡슐 | PASS | artifacts/win-x64/FlowNote.Desktop.exe, FloatingCapsuleWindow |

## 환경

- 빌드: `scripts/publish.ps1` → `artifacts/win-x64/FlowNote.Desktop.exe`
- OS: Microsoft Windows 11 Home x64 26200
- 모니터: 3440×1440, 배율 100% (96 DPI)
- 데이터: 스모크 데모 `artifacts/smoke-data` (live DB 미사용)
- 캡슐: 520×52 DIP / 창 536×68 DIP / OS 536×68 px / scale 1.0

## 필수 실제 캡처

| 파일 | 위치 |
|---|---|
| 01-capsule-idle-light | `docs/screenshots/capsule/01-capsule-idle-light.png` |
| 02-capsule-idle-dark | `docs/screenshots/capsule/02-capsule-idle-dark.png` (대비 약함. 보조: 02-floating-empty-screen.png) |
| 03-capsule-typing | `docs/screenshots/capsule/03-capsule-typing.png` |
| 04-recent-3 | `docs/screenshots/capsule/04-recent-3.png` |
| 05-todo-3 | `docs/screenshots/capsule/05-todo-3.png` |
| 06-image-draft | `docs/screenshots/capsule/06-image-draft.png` |
| 07-save-error | `docs/screenshots/capsule/07-save-error.png` |
| 08-narrow-or-high-dpi | `docs/screenshots/capsule/08-narrow-or-high-dpi.png` (현재 100%만) |
