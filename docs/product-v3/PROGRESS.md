# V3 진행

마지막 갱신: 2026-09-15  
저장소: `C:\GitRepositories\FlowNote` (로컬 `.git` 없음, 브랜치 없음)

## 상태

P0–P5 구현과 자동 검수는 이 작업에서 끝냈다.  
SCENARIO.md를 사람이 화면에서 처음부터 끝까지 누른 시연, 실제 Windows IME, 투명 히트테스트, 150% DPI, 10,000건 재측정, 초보자 사용성은 하지 않았다.

판정은 **부분 구현**이다. 유료 판매나 해자를 확보한 상태가 아니다.

## 재개 지점

다음에 손볼 때는 아래부터다.

1. `artifacts/win-x64/FlowNote.Desktop.exe --demo --data-root <새 폴더>`로 SCENARIO.md를 사람이 완주하고 선택 ZIP을 그 세션에서 저장한다.
2. 하루 정리의 다른 날짜 근거를 기록 ID 입력이 아니라 날짜 탐색으로 고르게 한다.
3. 실제 한국어 IME, 접힌 캡슐 히트테스트, 150% DPI를 OS에서 검수한다.
4. 설정 화면에 백업/복원 UI가 없다. 서비스는 `SqliteBackupService`만 있다.

## 실행 명령

Windows PowerShell (이 PC에 `pwsh` 없음):

```
powershell -NoProfile -File scripts/test.ps1
powershell -NoProfile -File scripts/publish.ps1
powershell -NoProfile -File scripts/smoke.ps1
```

데모: `artifacts/win-x64/FlowNote.Desktop.exe --demo --data-root <isolated>`  
`--smoke`와 같이 쓰면 시나리오 시딩을 하지 않는다.

## 이번 빌드 증거

- 테스트: Core 44 + Infrastructure 42 = 86 통과 / 0 실패
- 스모크: `artifacts/smoke-output/smoke-result.txt` PASS, entries=16
- 실행 파일: `artifacts/win-x64/FlowNote.Desktop.exe` (2026-09-15 10:02)
- 예시 ZIP: `artifacts/v3/selected-report.zip` (단위 테스트가 시나리오 DB에서 생성)
- 화면: `docs/screenshots/v3/`
