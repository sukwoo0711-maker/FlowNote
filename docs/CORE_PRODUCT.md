# FlowNote 핵심 제품 기준

현재 유지보수 버전: **0.5.5**. 현재 변경·검수는 `docs/releases/0.5.5.md`와 해당 릴리스의 `verification.json`을 따른다. 아래 이전 버전 결과는 과거 기록이다.

기록일: 2026-09-16  
대상: 현재 데스크톱 앱. 전면 재개발·새 프레임워크·새 모델·Lite 앱이 아니다.

## 목적

빠르게 기록한다 → 도구가 관련 업무와 맥락을 연결한다 → 사용자는 하루를 업무 흐름으로 확인한다 → 필요한 구간에서 원문과 첨부를 확인한다.

FlowNote는 시간순 메모 나열이 아니다. 최근 기록 3개는 입력 직후 편의이며 파노라마를 대체하지 않는다. 사용자가 모든 메모를 읽고 하루를 다시 조립해야 하면 핵심은 미완성이다.

## 기본에 남는 것

1. 빠른 기록 — 기존 520×52 포스트잇 캡슐. 원문 저장은 분석을 기다리지 않는다. 모델이 실패해도 메모와 첨부는 남는다.
2. 작은 기억판 — 최근 기록 최대 3개, 본문이 보이는 종이, 선택 고정 1개.
3. 간단한 할 일 — 추가·완료·실행 취소. 일반 메모를 자동으로 할 일로 만들지 않는다.
4. 자동 연결 — 같은 업무의 메모·첨부 연결, 수행과 요청·계획·참고 구별, A→B→A에서 처음과 마지막 A가 같은 업무임을 표시. 기존 규칙·분석 큐·연결 결과를 재사용한다.
5. 파노라마 — 개별 카드가 아니라 업무 구간과 전환 순서. 구간을 고르면 원문·사진·파일이 펼쳐진다.

라벨·시작·보류·재개를 매번 입력하지 않는다. 자동 연결이 틀렸을 때만 변경·해제·분리한다. 단서가 부족하면 미연결로 둔다.

## 기본 모드에서 켜지 않는 것

- 새 AI 모델·실행기, 범용 비서·챗봇, 자동 보고서, Jira 자동 연동
- 고급 타이머·집중시간·생산성 점수, 복잡한 프로젝트 관리, 신규 차트
- 화면·창 제목·메신저·클립보드 상시 수집
- 작업시간 추정. `09:00~10:10`은 관련 기록 범위이지 “70분 작업”이 아니다
- “재현 안 됨”을 공식 업무 완료로 바꾸는 변환

기본 분석 모드는 **규칙만**이다. 규칙 worker는 모델을 켜지 않는다. 설정에서 **로컬 보조**를 직접 켠 경우에만 동봉 엔진이 127.0.0.1에서 뜬다. `--assist-smoke`는 시드 JSON을 쓰며, 그 결과만으로 핵심 완료를 선언하지 않는다.

## 보존하는 것

사용자 데이터, 첨부 사본, 초안, 완료/다시 열림 이력, 기존 분석 대기열과 결과, 시간순·업무별·파노라마, 하루 정리(복사·ZIP), 저장 폴더 열기, 한글 IME, 포커스 보호, 창 위치·DPI, 테스트와 복구 경로. 캡슐을 큰 메모장으로 되돌리지 않는다.

## 보류된 이전 지시

`docs/assist-v4/*`, `docs/assist-v4_1/*`의 모델 확대, 타이머·작업시간 추정, 복잡한 보고, 상시 수집은 **보류**다. “AI와 자동 연결을 기본 경로에서 모두 제외”는 이 문서로 정정한다. 자동 연결은 핵심이고, 주변만 줄인다.

## 완료 기준

사용자가 모든 메모를 하나씩 읽지 않아도, 오늘 어떤 업무를 어떤 순서로 이어갔고, 어떤 것은 중간 요청이었으며, 어느 업무로 다시 돌아왔는지 파노라마에서 이해할 수 있는가.

자동 연결이 실제로 돌지 않는 목록만 보고 “업무 흐름 구현 완료”라고 하지 않는다.

## 과거 0.5.0 실행 빌드

게시 폴더: `artifacts/win-x64/FlowNote.Desktop.exe`  
공개 패키지: `artifacts/dist/FlowNote-0.5.0-win-x64-core.zip` / `FlowNote-0.5.0-win-x64-core-setup.exe`  
빌드 ID: `fn-0.5.0`

## 검수 (2026-09-16 출시 스냅샷)

Core 78 · Infrastructure 60 · `--smoke` 2회 PASS (격리 `artifacts/smoke-data`). 아래 감사 후 검수와 섞지 않는다.

| 조건 | 결과 | 근거 |
|---|---|---|
| 원문 저장·기본 기능 | PASS | 스모크 한글 메모·첨부·최근 3·할 일 완료/취소 |
| 라벨 없이 A/B 연결 | PASS | 단위 테스트 + 스모크 실저장 후 워커 Drain. 인버터 구간 2개 동일 ThreadId, 코스표 수행 1개, 요청 마커 |
| 파노라마 구간·원문 | PASS | `12-panorama-ab.png` 요약에 인버터→코스표→인버터. 상세에 09:00 원문 |
| 잘못된 연결 수정 보존 | PASS | `14-panorama-unlinked.png` 09:45 해제 후 ManualClear/lock 유지 |
| 기존 기능 회귀 | PASS | 캡슐 520×52, 최근 3, 핀, 접기, 시간순/업무별, 하루 정리 유지 |
| 미연결 | PASS | “확인”은 미연결. 단서 없는 짧은 메모는 새 업무로 만들지 않음 |
| 로컬 보조(llama) 실모델 | NOT RUN | 기본 경로가 아님. 엔진 기동·추론은 이번 범위 밖 |
| 사람 IME·다른 DPI | NOT RUN | 스모크 자동 입력. DPI 1.0만 관찰 |

사용성(직관적인지)은 사람이 쓰기 전에 통과로 보지 않는다.

## 현황 구분

- 이미 동작: 규칙 엔진, 분석 큐, 할당/별칭, DayFlowProjector, 사용자 해제/재연결
- 구현돼 있었으나 꺼져 있던 것: 기본 Off + RulesOnly New 차단 + 파노라마 스텁. 기본을 규칙만으로 되돌리고 구간 UI를 연결함
- 새로 만들지 않은 것: 모델, 실행기, 차트, 타이머, 챗봇
- 파노라마가 소비하는 데이터: `DayFlowProjection`의 수행 구간·요청 마커·미연결

## 남은 오판·한계

- 같은 날 다른 업무가 있으면 파노라마 요약 앞에 그 구간이 붙는다. A/B만 있는 날이 아니다.
- 수행 후속어(초기화·순서·재현 등)만 있는 메모는 직전 수행 업무에 붙인다. 그 단어가 새 업무 이름이면 오연결될 수 있다.
- 자동 일반 토큰은 전역 별칭을 덮어쓰지 않는다. 사용자 별칭은 여전히 가져갈 수 있다.
- 펼친 원문은 오른쪽 상세와 구간 토글로 확인할 수 있으나, 구간이 많으면 목록 스크롤이 필요하다.
- `--assist-smoke` 시드 JSON은 모델 실측이 아니다.
- 규칙 경로는 대표 A/B와 일부 반례만 검증했다. 새 표현은 Unknown/보류로 남을 수 있다.

## 감사 후 수정 (fn-64337c4-remed-dirty)

`01_READONLY_AUDIT.md` / `03_EVIDENCE_MANIFEST.json`은 저장소에 없었다. 지적 위치는 현재 소스에서 다시 확인한 뒤 고쳤다. 커밋·push·모델 다운로드·패키지 재배포는 하지 않았다.

이번 실행: Core 85 PASS · Infrastructure 67 PASS · 격리 `--smoke` PASS (`artifacts/remed-smoke-data`, `artifacts/remed-smoke-out/smoke-result.txt`, 범위에 `ab-unlabeled-flow`). DPI 1.0. 규칙 경로만. MODEL_REAL 아님.

| ID | 재현 | 수정 | 회귀 | 미검증 | 현재 위치 |
|---|---|---|---|---|---|
| F01 | 저장된 72회 모델 FAIL를 이번 실행으로 바꾸지 않음 | 품질 게이트 유지. 규칙 E2E를 MODEL_REAL로 보고하지 않음 | 해당 없음 | 현재 fingerprint 실모델 재평가 | `docs/CORE_PRODUCT.md`, 엔진은 LocalAssist opt-in |
| F02 | 이슈키/별칭만으로 Performed가 되던 규칙 | 식별과 `ClassifyRole` 분리. 계획/요청/부정/혼합 반례 추가 | Core 85 | 새 구어 표현 일반화 | `AssistText.cs`, `RulesEngine.cs`, `AssistCoreTests.cs` |
| F03 | 자동 일반 토큰이 마지막 thread 별칭을 훔침 | 자동 별칭 `DO NOTHING`, `IsAutoAlias` 건너뜀 | Infra 별칭/A/B | 사용자 별칭 충돌의 모든 단어 | `SqliteAssistStore.WriteAlias` |
| F04 | 연결만 바꿔도 Role이 Unknown | 재연결은 Role 유지, 해제는 Unknown+ManualClear, 모델 재부착 불가 | `Relink_keeps_role_*` | UI에서 역할 문구 가독성 | `SqliteAssistStore.CorrectAssignment` |
| F05 | 늦은 응답이 옛 snapshot으로 succeeded | 트랜잭션 안 live job/entry 재조회, lease 불일치 시 무기록 | 편집/삭제/정정 Barrier 경쟁 3건 | 실제 llama 지연 | `ApplyInference`, `AssistPersistenceTests` |
| F06 | 250ms 폴링, 하루 N+1 첨부/job | Pulse+2초 복구대기, 첨부/mentions/job 일괄, LiveDots 비가시 정지 | Infra worker·smoke | CPU/메모리 전후 측정, 150% 배율 | `AnalysisWorker`, `MainViewModel.Reload`, `LiveDots` |
| F07 | 클릭마다 새 request_id, refresh 실패=저장실패 | 제출 request_id 유지, refresh 분리, 제출 초안만 정리, 중복 첨부는 참조 확인 후 삭제 | `Duplicate_request_id_*` | 저장 중 입력+첨부 동시 실기기 | `FloatingViewModel.SaveNoteAsync`, `SqliteEntryService` |
| F08 | A→A 복귀 오표시, 공식 완료 누락, 혼합 메모 중복 | 다른 업무 개입 시에만 복귀, 공식 마커, mention은 원문 1회 | Core projector/read-model, smoke A/B | 빈 날짜 UI 손탐색 | `DayFlowProjector`, `DayFlowReadModel`, `MainViewModel.ReloadPanorama` |
| F09 | 투명 버튼 전경 불명, 로컬 BorderBrush가 Trigger 차단, 색 해시 프로세스마다 다름 | 읽기용 Foreground, Style BorderBrush, 결정적 Accent, 내부 상태어는 한글 상세 | Desktop 빌드 | 사람 제목 가독성 판정 | `MainWindow.xaml`, `AccentFor`, `JobStatusLabel` |
| F10 | 감사 원본 없음. IME/타앱 포커스/150%로 해석 | 코드 경로 유지, 이번 창 검수 없음 | 스모크 자동 입력 | 실 Windows IME·150% | 기존 입력/포커스 코드 |
| F11 | fire-and-forget, 최대시도 후 영원한 retry_wait | Task 보관, 취소 후 대기·dispose, 취소/timeout/max 구분, 정책 전환 시 과거 job stale | `Max_attempts_finish_as_failed` | 종료 중 긴 Infer 3초 초과 | `App.StopAssistWorker`, `FinishJob`, `SetMode` |
| F12 | 감사 원본 없음. 패키지/릴리스로 해석 | 0.5.0 경량 핵심 패키지 | 코어 패키지 스크립트 | 엔진·모델 풀팩은 v0.4.0 유지 | `scripts/package-core.ps1`, GitHub `v0.5.0` |
| F13 | 백업이 해시·aside 없이 덮어씀 | manifest 해시, 임시검증→aside→교체→실패복구 | `Backup_restores_*`, `Restore_rejects_tampered_*` | 운영 DB에 대한 수동 복구 시연 | `SqliteBackupService` |

규칙 경로 정확도(이번 단위 테스트): 대표 A/B·계획·요청·혼합 mention·이슈키≠수행은 기대대로. 단서 부족은 Abstain/미연결. 모든 메모를 보류로 처리해 성공 처리하지 않았다.

실제 모델 경로: NOT RUN. 저장된 모델 FAIL와 분리해서 보존한다.


## 0.5.2 후속 수리

현재 기준은 `docs/releases/0.5.2.md`와 동일 빌드의 `verification.json`이다. 검색 보존·파노라마 다시 열림·분석 큐 경계만 수리하며 모델과 타이머는 확장하지 않는다.

## 0.5.4 퀵 메모와 포스트잇
계약은 `docs/releases/0.5.4.md`다. 메모는 제목/발췌 대신 단일 본문으로 표시하며, 업무 연결 구조는 유지한다. 사진 클릭 확대와 실제 등록된 퀵 단축키를 제공한다.

## 0.5.5 포스트잇 캡처
최신 계약은 `docs/releases/0.5.5.md`다. 플로팅 기록 면과 하루 메모가 같은 노란 종이다. 캡슐 크기 520×52와 업무 연결·파노라마는 유지한다.
