# V4가 대체하는 이전 지시

작성: 2026-09-15  
원본: `FlowNote_Local_Assist_V4/08_SUPERSEDED.md`  
실제 저장소: `C:\GitRepositories\FlowNote`

V3의 **외부 AI 금지·수동 연결만 허용**은 이번 로컬 어시스트 범위에서 대체한다.
계속 금지: 화면/클립보드/창 제목 감시, OCR·사진 의미 분석, xAI/Grok API, Jira/SSO 우회, 근무시간 추정, 자동 공식 TODO 완료, 빈 DB로 migration 실패 처리.

| 이전 | V4 |
|---|---|
| V3 AI 호출 전체 금지 | 사용자 PC loopback Ollama만 허용. 기본 `127.0.0.1:11435` |
| 매 기록 업무 선택 | 저장 후 연결. 저장 전 후보창 금지 |
| 자동 WorkItem 완료 | 금지. ContextThread/ActionCandidate와 WorkItem 분리 |
| 포스터 92%/점심/집중시간 | 구현하지 않음 |
| ONNX+GGUF 동시 | Ollama 한 경로만 |
| 설정 문구 “AI 설정 없음” | 끔 / 규칙만 / 로컬 어시스트. 외부 AI 없음 |

캡슐 520×52·무제목표시줄·성공 문구 overlay는 유지한다.
