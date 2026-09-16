# FlowNote

Windows에서 한 줄과 사진을 남기고, 최근 기록과 할 일을 작은 창에서 확인합니다.
하루 파노라마는 관련 기록을 업무 구간으로 연결합니다. 표시된 시각 범위는 실제 작업시간 측정이 아닙니다.

현재 공개 버전: **0.5.1**. 기본 분석은 규칙 기반이며 모델 가중치나 AI 서버가 필요하지 않습니다.

## 실행

[Releases](https://github.com/sukwoo0711-maker/FlowNote/releases/tag/v0.5.1)에서
`FlowNote-0.5.1-win-x64-core.zip`과 `SHA256SUMS-core.txt`를 받습니다.
압축을 모두 풀고 `FlowNote.Desktop.exe`를 실행하세요. EXE 하나만 복사하면 동작하지 않습니다.
.NET 별도 설치는 필요 없습니다.

- 처음에는 작은 캡슐만 열립니다. `하루 보기`로 메인창을 엽니다.
- 설정에서 버전과 빌드 커밋을 확인할 수 있습니다.
- 기존 기록: `%LOCALAPPDATA%\FlowNote\live`. 실행 파일 폴더와 별개입니다.
- 완전히 종료하려면 트레이 메뉴의 `앱 종료`를 사용합니다.
- 업데이트 전 기존 앱을 종료하고 새 폴더에 압축을 풉니다. 사용자 데이터 폴더를 지우지 마세요.

이 릴리스는 포터블 ZIP입니다. 새 설치 EXE를 배포했다고 표시하지 않습니다.
Windows 코드 서명과 실제 사람의 IME/DPI 사용성 검수는 완료하지 않았습니다.
로컬 모델의 과거 의미 평가 실패를 규칙 테스트 통과로 대체하지 않습니다.

## 0.5.1 수정

- 수행 기록과 나중에 볼 요청이 한 문장에 섞일 때 재귀 호출을 막았습니다.
- 처음 등장한 후속 요청도 보존하며, 요청 마커만으로 업무 복귀를 표시하지 않습니다.
- 완료를 부정한 문장을 완료 언급으로 분류하지 않는 회귀 조건을 추가했습니다.
- 편집 후 늦은 분석 오류, 바뀐 lease 및 모델 버전의 오래된 응답을 거부합니다.
- 파노라마/할 일/설정에서 검색 결과를 볼 수 있고, 좁은 창에서도 원문 상세를 엽니다.
- 기본 시작은 캡슐만 표시합니다. 테스트 스크립트는 다른 FlowNote 프로세스를 종료하지 않습니다.

## 빌드

Windows x64, .NET SDK `10.0.401`이 필요합니다. NuGet 의존성은 lock 파일로 고정합니다.

```powershell
dotnet restore FlowNote.sln --locked-mode
dotnet test FlowNote.sln -c Release --no-restore
dotnet publish src/FlowNote.Desktop/FlowNote.Desktop.csproj -c Release -r win-x64 --self-contained true --no-restore -p:FlowNotePublishProfile=Core -o artifacts/win-x64
```

실행 정책을 변경하지 마세요. 스크립트를 허용하는 환경에서는 기존
`scripts/package-core.ps1`을 사용할 수 있습니다. Inno Setup이 설치된 개발 환경에서만 설치 EXE도 생성합니다.
검수와 제한 사항은 `docs/releases/0.5.1.md`를 참고하세요.
