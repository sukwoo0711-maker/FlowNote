# FlowNote V3 — 보존된 구현 계약

원본 발주: `FlowNote_Agent_Execution_v3/FlowNote_Agent_Execution_v3/01_MASTER_WORK_ORDER.md`  
시각 계약: `03_VISUAL_CONTRACT.md`  
검수표: `02_ACCEPTANCE_TESTS.md`  
시나리오: `fixtures/SCENARIO.md`, `fixtures/scenario.json`

이 파일은 원문을 대체하지 않는다. 구현 중 고정한 해석만 적는다.

## 완주 흐름

한 줄/사진 기록 → 선택적 업무 연결 → 다음 행동 한 줄 → 마지막 기록·자료·다음 행동 확인 → 같은 업무에 이어서 기록 → 완료 이력 보존 → 선택한 원문·파일만 보고.

## 스키마

- 기존 `work_items`, `entries`, `entry_revisions`, `work_item_revisions`, `drafts.work_item_id`를 재사용한다.
- migration 2: `work_items`에 다음 행동 투영 4열, `work_item_revisions.request_id` 고유 인덱스.
- 다음 행동 이력은 `work_item_revisions.previous_values_json` (`kind=next_action`).
- 일반 메모 재연결만 허용. 생명주기 이벤트의 업무 ID는 바꾸지 않는다.

## 제외

새 프레임워크, AI, 자동 수집, 결제, 근무시간 환산, 새 파노라마 엔진, 운영 DB 시딩.
