# FlowNote 플로팅 캡슐 v2 — 현재 제품 지시

상태: **현재 플로팅 창의 유일한 유효 지시.** 세로 메모장 설계를 대체한다.
원본 패키지: `FlowNote_Floating_Capsule_v2/` (시작점·인수표·토큰·참조 PNG).
구현 위치: `src/FlowNote.Desktop/Views/FloatingCapsuleWindow.xaml`.

이 문서가 다음 문서의 **플로팅 외형·창 구조·Enter 동작·기본 TODO 배치**를 대체한다.

| 이전 지시 | 처리 |
|---|---|
| `docs/SPEC.md` F01의 360×500, 미니 360×60, 제목표시줄, 오늘/고정/접기/숨기기, 상시 TODO, Enter 줄바꿈 | **SUPERSEDED BY this file** |
| `01_MASTER_IMPLEMENTATION_PROMPT.md` F01 동일 구절 | **SUPERSEDED BY this file** |
| `FlowNote_UI_Rescue_v1_1` 플로팅 360 DIP·80~96 편집기·빈 높이 280~340·상시 하단 TODO | **SUPERSEDED BY this file** |
| `docs/UI_RESCUE_U1U2.md` U1 세로 플로팅 적용 설명 | 당시 납품 기록. **현재 플로팅 실행 기준이 아님** |
| `03_ACCEPTANCE_TESTS.md` 02-floating-expanded / 03-floating-compact | 캡슐 캡처 목록으로 교체 |
| U0 표본 승인 대기 후 플로팅 적용 | 폐기. 플로팅은 중간 승인 없이 완주 |

**대체하지 않는 규칙:** 로컬 SQLite, 사용자 데이터·초안·첨부 사본, 기록/발생 시각, 완료/재열기 이력, 25MB·10파일·실행파일 차단, 메인 시간순/파노라마/업무별 화면 재설계 금지, 클라우드·AI·Jira API·자동 감시 금지.

## 폐기한 UI 조건

- 360×500 세로창, 기본 80~96 DIP 여러 줄 입력기
- 기본 제목 표시줄, 오늘/고정됨/접기/숨기기 텍스트 버튼 줄
- 상시 펼쳐진 TODO와 하단 빈 추가 입력란
- `QuickCaptureComposer`를 캡슐에 우겨 넣기
- 구형 Height/MinHeight 복원, 표본만 제출하고 멈추기

참조 `FlowNote_Floating_Capsule_v2/references/01_REJECTED_tall_notepad.png`는 재현 대상이 아니다.

## 이번 기본 상태 (필수)

참조 `FlowNote_Floating_Capsule_v2/references/02_TARGET_floating_capsule.png`의 **창 본체만** 따른다. 홍보 문구·화살표·배경·Ctrl+Shift+L 인쇄 문구는 구현하지 않는다.

- 가시 캡슐 520×52 DIP, 끝 반경 26 DIP
- 제목 표시줄 없음 (`WindowStyle=None`, `AllowsTransparency=True`)
- 짙은 청록–슬레이트 표면, 한 줄 입력, 로고 메뉴와 작은 인라인 조작
- 패널이 없으면 실제 창도 축소 (여백 각 8 DIP 이하, 전형 536×68 DIP)
- 일반 입력만으로 창 높이를 늘리지 않음
- 최근 기록 / TODO / 첨부·긴 메모는 필요할 때 작은 패널 하나
- `FloatingLayoutVersion=2`. 구형 360×500 높이는 무시. 위치·핀·데이터는 보존
- 한 줄 Enter 또는 인라인 버튼으로 기록. 한글 IME 조합 확정 Enter는 기록하지 않음
- 사용자 실행 1회 영역 캡처만 초안 첨부로 허용

상세 수치·토큰·인수 ID는 패키지 `01_FLOATING_CAPSULE_OVERRIDE.md`, `design_tokens.json`, `02_ACCEPTANCE.md`와 같다.

## 이번 납품 증거

- 실행 파일: `artifacts/win-x64/FlowNote.Desktop.exe`
- 창 클래스: `src/FlowNote.Desktop/Views/FloatingCapsuleWindow.xaml`
- 캡처: `docs/screenshots/capsule/`
- 인수 채움: `FlowNote_Floating_Capsule_v2/02_ACCEPTANCE.md`
- 성능: `docs/screenshots/capsule/idle-60s.txt`, `artifacts/smoke-output/capsule-warm-show.txt`
