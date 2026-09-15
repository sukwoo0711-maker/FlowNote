# U0 수정 + U1 플로팅 + U2 시간순 — 이번 실행 제출

기록일: 2026-09-15  
발주자 지시: 표본만 올리고 승인 대기하지 말 것. 디자인 최종 승인과 작업 계속 허용을 혼동하지 말 것.  
이번 실행 판정: **당시 U2까지 제품 화면에 적용했다.** 그중 **U1 세로 플로팅(360×480, 제목표시줄, 상시 TODO)은 SUPERSEDED BY docs/FLOATING_CAPSULE_V2.md.** 현재 플로팅 실행 기준으로 쓰지 마라.

---

## 산출물

| 항목 | 위치 |
|---|---|
| 실행 빌드 | `artifacts/win-x64/FlowNote.Desktop.exe` (`scripts/publish.ps1`) |
| 소스 | 이 저장소 Desktop/Core/Infrastructure |
| 실제 플로팅 창 (OS 크롬 포함) | `docs/screenshots/u1/02-floating-empty-screen.png`, `docs/screenshots/u1/04-floating-screen.png` |
| 실제 시간순 화면 (OS 크롬 포함) | `docs/screenshots/u2/03-main-timeline-screen.png`, `docs/screenshots/u2/10-main-chronological-screen.png` |
| 수정 전 표본 | `docs/screenshots/before/u0-component-preview.png` |
| 수정 후 표본 | `docs/screenshots/after/u0-component-preview.png`, `docs/screenshots/u0/u0-component-preview.png` |
| 스모크 결과 | `artifacts/smoke-output/smoke-result.txt` |

운영 live DB·첨부·초안은 건드리지 않았다. 스모크는 `--data-root artifacts/smoke-data` 데모 경로만 사용한다.

---

## 무엇을 바꿨는지

프리뷰와 제품이 **같은** `QuickCaptureComposer`, `WorkItemRow`, `TimelineEntryCard`, `TimelineEventRow`, `AttachmentTile`, `TimelineItemShell`을 쓴다. 프리뷰 전용 예쁜 컨트롤은 없다.

1. **QuickCaptureComposer** — 360 DIP 폭 기준으로 표본을 세로 배치. 내부 패딩 16, 입력 80~160 DIP, 첨부·오류 없으면 접힘. 입력은 테두리 중복 없이 표면 차이. `첨부` 보조 / `기록` 주요. 텍스트·첨부 없으면 `CanExecute`로 기록 비활성. 이미지 단독 허용. 실패 시 본문 유지, 버튼 `다시 기록`. 성공 문구는 빈 초안에 남지 않게 구분.
2. **첨부** — 합성 PNG를 실제 파일로 만들어 읽어 썸네일 표시. 로그는 아이콘·이름·크기. 입력 중 제거. 깨진 PNG는 파일명+실패. `BitmapCreateOptions.IgnoreImageCache`+Stream은 WPF 캐시 예외를 일으켜 제거했다.
3. **WorkItemRow** — 오른쪽 파란 `✓ 완료` 제거. 왼쪽 체크(표시 ~20, 히트 32×32). 완료는 이벤트 저장 후 목록에서 제거. `완료 기록이 남았습니다` + `실행 취소`(재열기 이벤트).
4. **타임라인** — 시각 / 노드·선 / 내용 공유 셸. 제목·본문 반복 제거. 발생≠기록일 때만 차이 문구. 생성/완료/다시 열림은 아이콘+라벨. 업무별 그룹명 `연결하지 않은 기록`.
5. **U1 플로팅** — 위 입력, 아래 미완료 할 일. 입력기가 할 일을 밀어내지 않음. 오늘/고정/접기/숨기기. 높이 작업 영역 제한. OS 창 크롬 유지. `ShowActivated=false`. 데이터 갱신에서 `Activate`하지 않음.
6. **U2 메인** — 좌측 전역(오늘 보기·검색), 중앙 날짜, 그 아래 `시간순 | 파노라마 | 업무별`. 시간순에 그룹 카드 없음. 미선택 시 상세 열 폭 0. 선택 시 상세, 닫으면 흐름. 좁은 창은 상세로 **전환**(타임라인 위에 덮지 않음). 파노라마는 “아직 없습니다”만. 새 파노라마 설계는 하지 않음.

---

## 시각 수정 사이클 (제품 화면, 최대 3)

합격은 횟수가 아니라 관찰이다.

1. 표본을 360 DIP 세로 배치, 체크 행, 실제 파일 첨부 타일로 바꿨다. 정상 PNG가 실패와 같은 모습이었다.
2. 썸네일을 `StreamSource`+`OnLoad`로 읽어 정상 PNG와 `broken.png` 실패를 구분했다.
3. 업무별 그룹 템플릿이 앱 브러시보다 먼저 병합되어 렌더가 죽던 문제를 고쳤다. 빈 초안에 `다시 기록`이 남던 상태, 좁은 창 덮개, 이미지 단독 제목 `메모`, occluded `CopyFromScreen`의 타인 화면 유출을 고쳤다. 메인 창 OS 캡처는 `PrintWindow`로 해당 창만 담는다.

---

## 테스트와 smoke가 실제로 검사하는 것

`scripts/test.ps1`: Core **22** + Infrastructure **28** = **50 통과 / 0 실패**. 기존 테스트를 삭제하거나 약화하지 않았다. 추가분은 `TimelineDisplayRules`(시각 중복 숨김, 노드 종류), `FileSizeDisplay`.

`scripts/u0-preview.ps1` (`--ui-preview --smoke`):

- 하는 일: 운영 DB를 열지 않고 합성 fixture 프리뷰를 연 뒤 `RenderTargetBitmap` PNG를 저장한다.
- **assertion:** Composer 4개, 각 폭 360±2 DIP, 빈 입력 `기록` 비활성, 실패 표본 `다시 기록`, `✓ 완료` 버튼 없음, `첨부` 있고 `파일` 없음.
- **하지 않는 일:** IME, 다중 DPI, 다른 앱 포커스, 초보자 사용성, 제품 플로팅/메인 창. 종료 코드 0을 전체 UI 합격으로 확대하지 않는다.

`scripts/smoke.ps1` (게시 exe, 데모 `--data-root` 두 번):

- 빈 입력 `CanSave=false`
- 한글 본문 + 로그 + 이미지 저장, 첨부 사본 2개
- 이미지 단독 저장
- 본문 길이 초과로 실제 저장 실패 → `다시 기록`, 본문 유지 후 비움
- 할 일 완료 → 목록 제거 → 실행 취소(다시 열림 이벤트) → 다시 완료
- 시간순 / 업무별(`연결하지 않은 기록`) / 파노라마(행 0, 미구현 문구)
- 상세 닫기, 좁은 폭 전환
- 재시작 후 같은 한글 본문·첨부·완료 이력
- `RenderTargetBitmap` + `PrintWindow` 창 캡처 시도 (`notes=` 비어 있으면 캡처 파일 기록됨)

명령 종료 0은 위 assertion의 PASS이지, IME·다중배율·타인 앱 포커스·초보자 사용성 합격이 아니다.

---

## 미검증 · 미해결

| 항목 | 상태 |
|---|---|
| 한국어 IME 조합·확정 중 겹침 | NOT RUN (자동 스모크가 IME를 누르지 않음) |
| 다중 DPI/배율 전환 | NOT RUN (이 PC 96 DPI 100%만) |
| 다른 프로그램 입력 중 포커스 뺏김 수동 재현 | NOT RUN. 코드는 갱신 시 `Activate`하지 않음. |
| 초보자 사용성 | NOT RUN (참여자 없음). 통과로 쓰지 않음. |
| 외형 최종 승인 | 하지 않음 |
| 파노라마 본구현 | 없음. 미구현으로 표시함 |
| 이미지 단독 카드가 파일명을 제목과 타일에 같이 씀 | 남은 시각 이슈. 저장 데이터는 유지 |
| K01 `.git` 없음, K02 스택 미확정, K07 첨부 Prepare 선이동 | 기존과 동일 |

---

## 보안·데이터

- 회사/개인 자료로 데모하지 않음. 첨부·캡처는 합성 PNG/로그.
- 한 차례 `CopyFromScreen`이 가려진 메인 창 자리에 다른 앱을 담아 유출 위험이 있어 그 파일은 제출본에서 버렸고, `PrintWindow`로 해당 창만 다시 찍었다.
- 운영 `%LOCALAPPDATA%\FlowNote\live`는 사용하지 않음.
