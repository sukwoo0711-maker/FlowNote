# Assist V4 데모 안내

이 데모는 **합성 fixture expected JSON을 시드**한다. 로컬 모델이 문장을 분류한 결과가 아니다.

## 실행

1. `powershell -NoProfile -File scripts/assist-v4/Build.ps1`
2. `powershell -NoProfile -File scripts/assist-v4/Run-Demo.ps1`

자동 GUI 스모크(같은 합성 시드, 사람 워크스루 아님):

`powershell -NoProfile -File scripts/assist-v4/Smoke-Ui.ps1`

격리 데이터: `artifacts/assist-v4/demo-data` (`--data-root`). 운영 `%LOCALAPPDATA%\FlowNote\live`에 넣지 않는다.

날짜 **2026-09-15**로 이동하면 A/B 시나리오 다섯 메모가 있다.

## 시드가 의미하는 것

| 시각 | 기록 | 시드 역할 |
|---|---|---|
| 09:00 | 인버터 과전류 | A PERFORMED |
| 09:20 | 세탁 코스 요청, 나중에 | B REQUEST_LATER (A를 끊지 않음) |
| 09:45 | 인버터 계속 | A PERFORMED |
| 10:10 | 이 검토는 끝냈다 | A COMPLETION_MENTION (공식 완료 이벤트 아님) |
| 10:15 | 세탁 표를 펼침 | B PERFORMED 시작 |

공식 WorkItem 수는 0이다. 막대는 실작업시간이 아니다.

## 모델 데모가 필요할 때

Ollama 설치와 `qwen3:4b` 준비는 별도 허가 절차다. 이 스크립트는 모델을 받지 않는다. 설치 전에는 설정이 **규칙만**이 맞다.
