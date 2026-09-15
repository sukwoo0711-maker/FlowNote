# U0 — 현황 확인과 컴포넌트 표본

기록일: 2026-09-15  
단계: **U0만**. 시각 승인 전이라 메인/플로팅 전체 화면 재배치는 하지 않았다.

---

## 수행 단계

1. `docs/PROGRESS.md`, Desktop 소스, `FlowNote_UI_Rescue_v1_1.md`를 읽었다.
2. `references/00-current-app.png`, `references/01-concept-timeline.png`를 실제로 열었다. 두 파일 모두 존재한다.
3. 나열된 UI 문제의 코드 원인을 조사했다.
4. 제품과 같은 UserControl을 추출하고 `--ui-preview` 개발 프리뷰를 만들었다. 합성 fixture만 사용하며 운영 DB를 열지 않는다.
5. Windows에서 실행해 실창을 캡처했다.

현재 구현 단계: **기능이 돌아가는 중간 프로토타입**이다. `docs/PROGRESS.md`는 “사용 가능한 win-x64 exe”와 PHASE 05–06 타임라인을 기능 검증 화면으로 기록한다. 최종 제품 UI가 아니다.

---

## 참고 이미지

| 파일 | 확인 |
|---|---|
| `FlowNote_UI_Rescue_v1_1/references/00-current-app.png` | 열람. 현재 앱 캡처. 좌측 오늘/시간순/업무별 혼재, 우측 빈 상세 352열, ‘업무 미연결/그룹’, 제목·본문 ‘플로우노트’ 반복, 플로팅 placeholder와 캐럿, OS 제목표시줄 불일치. |
| `FlowNote_UI_Rescue_v1_1/references/01-concept-timeline.png` | 열람. 톤·시간축·노드 참고용. 모바일 프레임, 홍보 문구, 멀티플랫폼, 주간 히트맵은 옮기지 않음. 승인된 데스크톱 기준 화면이 아니다. |

---

## 확인된 원인

형식: 관찰 / 확인된 코드 원인 / 수정 파일 / 검증 방법

### 1. 전역 탐색과 보기 전환이 섞임

- **관찰:** 좌측에 ‘오늘’(채움 버튼)과 ‘시간순/업무별’(보조 버튼)이 같은 열에 있다. 캡처에서 시간순이 선택처럼 보이지 않는다. 파노라마 진입은 없다.
- **확인된 원인:** `MainWindow.xaml` 좌측 `StackPanel`이 `TodayCommand`(날짜)와 `ChronologicalCommand`/`ByWorkCommand`(보기)를 같은 버튼 계층에 둔다. ‘오늘’은 기본 `Button` 스타일(강조 채움)이라 항상 선택된 탐색처럼 보인다. `AppSession.ViewMode` 기본값은 `Chronological`이지만 선택 chrome이 없다. 파노라마 보기/명령은 코드에 없다.
- **수정 파일 (U0):** 전체 내비 재배치는 U1/U2. 이번엔 원인만 기록.
- **검증:** 좌측 XAML과 `TimelineViewMode` 검색. 파노라마 심볼 없음.
- **가설 아님.**

### 2. 시간축·노드 표현

- **관찰:** 시간 라벨과 카드는 있으나 축/노드가 없다.
- **확인된 원인:** 구 `ListBox.ItemTemplate`이 64 DIP 시간 열 + 내용만 있었고 노드 열이 없었다.
- **수정 파일:** `TimelineEntryCard.xaml`, `TimelineEventRow.xaml` (64 / 24 노드열). 제품 `MainWindow`는 `TimelineRowSelector`로 이 템플릿을 쓴다. 헤더/상세 접기 등 화면 구조는 U2.
- **검증:** U0 프리뷰 캡처의 파란/초록 노드.

### 3. 미선택 상세 패널 상시 노출

- **관찰:** 선택 없이 ‘기록을 선택하세요’ 352 DIP 열이 항상 있다.
- **확인된 원인:** `MainWindow.xaml` `ColumnDefinition Width="352"`가 고정. 선택 여부 트리거가 없다.
- **수정 파일:** U0에서 접지 않음 (전체 화면 확장 금지).
- **검증:** 컬럼 정의 확인.
- **가설 아님.**

### 4. 제목과 본문 미리보기 중복

- **관찰:** ‘플로우노트’가 제목과 미리보기에 반복되고 아래에 ‘메모’가 붙는다.
- **확인된 원인:** `TimelineRow.From`이 `TitleSnapshot`(본문 첫 줄)을 Title로, `TrimPreview(Body)`를 Preview로 넣는다. 한 줄 메모는 둘이 같다. KindLabel ‘메모’를 항상 표시했다.
- **수정 파일:** `NoteDisplayRules.cs`, `TimelineRow.DisplayPreview`/`ShowPreview`/`ShowKindLabel`, `TimelineEntryCard.xaml`.
- **검증:** Core 테스트 3개 PASS. 프리뷰 09:12 카드는 제목만 보인다.

### 5. 업무 미연결 / 그룹 헤더

- **관찰:** 기록 1개인데 ‘업무 미연결’ 큰 카드와 ‘그룹’ 라벨.
- **확인된 원인:** `MainViewModel.Reload`가 `ViewMode == ByWork`일 때 `TimelineRow.Group("업무 미연결"|제목)`을 넣는다. KindLabel은 `"그룹"`. 좌측 ‘오늘’이 강조되어 시간순으로 오해하기 쉽다. 캡처의 그룹은 **업무별이 켜진 상태**로 보는 것이 코드와 맞다.
- **가설:** 사용자가 업무별을 누른 뒤 선택 chrome이 없어 시간순으로 착각했다.
- **수정 파일:** U0 그룹 템플릿은 작은 텍스트로만 줄였다. 시간순에서 그룹을 안 만드는 로직 변경은 U2.
- **검증:** `Reload`의 `ByWork` 분기.

### 6. 입력 문자와 placeholder 겹침

- **관찰:** 플로팅 입력에 캐럿과 ‘메모를 입력하세요…’가 같은 자리에 있다.
- **확인된 원인:** placeholder가 `TextBox` 위 `TextBlock`이고 `DataTrigger Binding=Body Value=""`만 봤다. IME 조합 중에는 `TextBox.Text`에 글자가 있어도 `Body` 바인딩이 비어 있으면 overlay가 남는다.
- **가설 (IME):** 한글 조합 중 겹침. U0에서 실제 IME 키 입력은 NOT RUN.
- **수정 파일:** `QuickCaptureComposer`가 `BodyBox.Text`로 즉시 숨긴다. `IsHitTestVisible=False`.
- **검증:** 프리뷰 빈 상태 vs 입력 상태. IME는 미실행.

### 7. 두 창의 스타일 적용과 덮어쓰기

- **관찰:** 메인과 플로팅의 버튼 모양은 비슷하나 OS 제목표시줄이 다르다. 우측 상세 문구가 잘려 보인다.
- **확인된 원인:** 색/버튼/TextBox 스타일은 `App.xaml` 전역이다. 두 창 모두 `StaticResource`를 쓴다. 플로팅만 지역 overlay 스타일이 있었다. `ListBoxItem` ControlTemplate은 메인 목록에만 영향을 준다. 창 크롬은 OS 기본 `Window`. 캡처의 상세 잘림은 352열 + 긴 문장 + 창 겹침이며, 코드가 우측을 Clip한 것은 아니다.
- **가설:** 00-current-app.png의 잘림은 플로팅 창이 상세 위를 가린 캡처 구도.
- **수정 파일:** 공통 UserControl + App 리소스 병합 `Themes/ComponentTemplates.xaml`. 프레임워크/테마 라이브러리 추가 없음.

---

## 변경 파일

신규:
- `src/FlowNote.Core/Rules/NoteDisplayRules.cs`
- `tests/FlowNote.Core.Tests/NoteDisplayRulesTests.cs`
- `src/FlowNote.Desktop/Controls/QuickCaptureComposer.xaml(.cs)`
- `src/FlowNote.Desktop/Controls/WorkItemRow.xaml(.cs)`
- `src/FlowNote.Desktop/Controls/TimelineEntryCard.xaml(.cs)`
- `src/FlowNote.Desktop/Controls/TimelineEventRow.xaml(.cs)`
- `src/FlowNote.Desktop/Controls/TimelineRowTemplateSelector.cs`
- `src/FlowNote.Desktop/Themes/ComponentTemplates.xaml`
- `src/FlowNote.Desktop/Preview/ComponentPreviewWindow.xaml(.cs)`
- `src/FlowNote.Desktop/Preview/PreviewFixtures.cs`
- `scripts/u0-preview.ps1`
- `docs/UI_RESCUE_U0.md` (이 파일)

수정:
- `App.xaml`, `App.xaml.cs` (`--ui-preview`, 운영 DB 미오픈)
- `MainWindow.xaml` (공유 `TimelineRowSelector`)
- `Views/FloatingDockWindow.xaml(.cs)` (공유 Composer/WorkItemRow)
- `ViewModels/MainViewModel.cs` (`DisplayPreview` 등)
- `Smoke/SmokeHarness.cs` (`WindowCapture` 요소 렌더)

보존: SQLite 스키마, 서비스, 명령, 기존 테스트. 데이터 초기화 없음.

프리뷰 실행: `FlowNote.Desktop.exe --ui-preview`  
합성 데이터만. `%LOCALAPPDATA%\FlowNote`를 열지 않는다.

---

## 실제 실행/테스트 결과

| 항목 | 상태 |
|---|---|
| `scripts/test.ps1` | PASS. Core 18 + Infrastructure 28 = 46, 실패 0 |
| `--ui-preview --smoke` | PASS. `docs/screenshots/u0/u0-result.txt` |
| 기능 회귀 (저장/완료/첨부 스모크) | NOT RUN (U0 범위 밖. 기존 테스트는 유지) |
| 시각 검수 | **대기.** worker 승인 아님. 표본 캡처만 제출 |
| 사용성 검증 | NOT RUN |
| IME / DPI 전환 | NOT RUN |

---

## 실제 캡처와 실행 환경

| 파일 | 내용 |
|---|---|
| `docs/screenshots/u0/u0-component-preview.png` | Composer 빈/입력/첨부/오류, WorkItemRow, EntryCard(중복 제목 제거·선택), EventRow |
| `docs/screenshots/u0/u0-component-preview-window.png` | 같은 창의 보이는 영역 |

환경: Windows 11 Home x64 26200, SDK 10.0.401, Release `net10.0-windows`, 주 모니터 3440×1440, 시스템 DPI 96(100%).  
캡처: 실행 중인 WPF `RenderTargetBitmap`. 생성 이미지/목업 아님. fixture는 `PreviewFixtures` 한글 합성 데이터.

---

## 미검증 사항

- 한국어 IME 조합 중 placeholder
- 100/125/150/200% DPI
- 프리뷰가 아닌 제품 메인/플로팅의 새 카드 시각 승인
- 업무별 그룹을 시간순에서 숨기는 UX (로직은 아직 ByWork에서만 그룹 생성)
- 상세 패널 접기, 날짜/보기 분리, 플로팅 높이 자동, 파노라마

---

## 시각 승인 대기 항목

발주자/지정 디자이너 승인 대상:
- QuickCaptureComposer 빈/입력/첨부/오류
- WorkItemRow 완료 버튼(체크 path + ‘완료’)
- TimelineEntryCard 시간·노드·카드 (한 줄 중복 제거)
- TimelineEventRow 간결 이벤트

승인 전에는 U1 플로팅 전체 재배치로 넘어가지 않는다.

---

## 다음 한 단계

**U1 — 플로팅 창.** 승인된 Composer/WorkItemRow로 F01만 정리하고 실제 저장/첨부/완료 서비스에 연결한다.
