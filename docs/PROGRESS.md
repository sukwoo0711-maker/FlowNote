# 진행 상황

마지막 갱신: 2026-09-15  
현재 단계: **V3 제품 흐름 부분 구현.** 캡슐 v2 외형은 유지한다. 메인 시간축·업무 맥락·다음 행동·선택 보고를 이 빌드에 넣었다.

---

## 지금 상태

- 로컬 `C:\GitRepositories\FlowNote`. GitHub 원격은 비어 있고 로컬 `.git` 없음(K01).
- 실행 파일: `artifacts/win-x64/FlowNote.Desktop.exe` (`scripts/publish.ps1`).
- 플로팅은 `FloatingCapsuleWindow` (520×52 DIP, 제목줄 없음).
- 유효 요구 우선순위: 사용자 최신 요청 > V3 명세 > V3 검수표 > 기존 명세.
- 충돌 지침: `docs/product-v3/SUPERSEDED.md`.

---

## 이 실행에서 한 일

- 한 줄/사진 기록 → 선택 업무 연결 → 다음 행동 → 업무 맥락 → 이어서 기록 → 완료 이력 → 선택 보고를 기존 앱에 구현했다.
- schema migration 2: `work_items.next_action_*` 4열, `work_item_revisions.request_id`.
- `--demo`만 시나리오를 1회 시딩한다. 운영/라이브 DB에는 넣지 않는다.
- 파노라마 엔진은 만들지 않았다. 안내 문구만 유지한다.

---

## 빌드·테스트 증거

- `scripts/test.ps1`: Core 44 + Infrastructure 42 = **86 통과 / 0 실패**. 기존 테스트를 삭제하지 않았다.
- `scripts/publish.ps1` + `scripts/smoke.ps1`: PASS, entries=16.
- V3 캡처: `docs/screenshots/v3/`.
- 예시 ZIP: `artifacts/v3/selected-report.zip`.
- 인수표 상세: `docs/TEST_RESULTS.md`.

Windows UI 실행 가능: **가능**. 실 IME·영역 드래그·다중 DPI·투명 히트테스트·SCENARIO.md 수동 완주·초보자 사용성은 NOT RUN.

---

## 다음 작업자 지시

캡슐을 세로 메모장으로 되돌리지 마라. 파노라마를 구현 완료처럼 그리지 마라. 운영 DB를 스모크/데모 경로로 바꾸지 마라. 유료 판매 성공이나 해자를 확보했다고 쓰지 마라.
