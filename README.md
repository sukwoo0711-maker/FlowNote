# FlowNote

Windows에서 한 줄과 사진을 남기고, 최근 기록과 할 일을 작은 창에서 확인합니다.
하루 파노라마는 관련 기록을 업무 구간으로 연결합니다. 표시된 시각 범위는 실제 작업시간 측정이 아닙니다.

현재 공개 버전: **0.5.3**. 기본 분석은 규칙 기반이며 모델 가중치나 AI 서버가 필요하지 않습니다.

## 실행

[Releases](https://github.com/sukwoo0711-maker/FlowNote/releases/tag/v0.5.3)에서
`FlowNote-0.5.3-win-x64-core-setup.exe` 설치본 또는 `FlowNote-0.5.3-win-x64-core.zip` 포터블을 받습니다.
파일 해시는 `SHA256SUMS-core.txt`로 확인합니다.
압축을 모두 풀고 `FlowNote.Desktop.exe`를 실행하세요. EXE 하나만 복사하면 동작하지 않습니다.
.NET 별도 설치는 필요 없습니다.

- 처음에는 작은 캡슐만 열립니다. `하루 보기`로 메인창을 엽니다.
- 설정에서 버전과 빌드 커밋을 확인할 수 있습니다.
- 기존 기록: `%LOCALAPPDATA%\FlowNote\live`. 실행 파일 폴더와 별개입니다.
- 완전히 종료하려면 트레이 메뉴의 `앱 종료`를 사용합니다.
- 업데이트 전 기존 앱을 종료하고 새 폴더에 압축을 풉니다. 사용자 데이터 폴더를 지우지 마세요.

설치본은 현재 사용자 폴더에 설치합니다. 운영 데이터 폴더는 설치·업데이트와 분리돼 있습니다.
Windows 코드 서명과 실제 사람의 IME/DPI 사용성 검수는 완료하지 않았습니다.
로컬 모델의 과거 의미 평가 실패를 규칙 테스트 통과로 대체하지 않습니다.

## 0.5.3 수정

- 검색 결과와 선택한 원문은 백그라운드 기록 분석 후에도 유지됩니다. 다른 날짜의 검색 결과에는 날짜도 표시합니다.
- 완료 후 실행 취소한 내역을 파노라마의 `다시 열림` 마커로 보존합니다.
- 분석 제한시간 초과와 앱 종료 취소를 구분합니다. 중단된 분석은 기존 최대 3회 예산 안에서 재시도하며 원문은 유지합니다.
- 최종 시도의 lease가 만료된 작업이 영원히 진행 중으로 남지 않습니다. 만료된 결과도 적용하지 않습니다.
- v0.5.1의 캡슐·A/B 연결·원문 보존 수정을 유지합니다. 새 모델·타이머는 추가하지 않았습니다.

## 빌드

Windows x64, .NET SDK `10.0.401`이 필요합니다. NuGet 의존성은 lock 파일로 고정합니다.

```powershell
dotnet restore FlowNote.sln --locked-mode
dotnet test FlowNote.sln -c Release --no-restore
dotnet publish src/FlowNote.Desktop/FlowNote.Desktop.csproj -c Release -r win-x64 --self-contained true --no-restore -p:FlowNotePublishProfile=Core -o artifacts/win-x64
```

실행 정책을 변경하지 마세요. 스크립트를 허용하는 환경에서는 기존
`scripts/package-core.ps1`을 사용할 수 있습니다. Inno Setup이 설치된 개발 환경에서만 설치 EXE도 생성합니다.
검수와 제한 사항은 `docs/releases/0.5.3.md`를 참고하세요.
