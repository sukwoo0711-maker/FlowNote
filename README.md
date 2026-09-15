# FlowNote

Windows에서 한 줄·사진 기록, 선택 업무 연결, 다음 행동, 완료 이력, 고른 내용만 담는 하루 정리를 하는 데스크톱 앱입니다.

이 저장소의 현재 공개본은 **0.3.0**입니다. 개발 완료나 유료 판매, 해자 확보를 뜻하지 않습니다.

## 받기

[Releases](https://github.com/sukwoo0711-maker/FlowNote/releases)에서 Windows x64 파일을 받습니다.

| 파일 | 용도 |
|---|---|
| `FlowNote-0.3.0-win-x64-setup.exe` | 현재 사용자 폴더에 설치, 시작 메뉴·제거 프로그램 등록 |
| `FlowNote-0.3.0-win-x64-portable.zip` | 압축을 풀고 `FlowNote.Desktop.exe` 실행. 설치 마법사 없음 |

.NET 별도 설치는 필요 없습니다. 기록은 `%LOCALAPPDATA%\FlowNote\live`에 남습니다. 설치를 지워도 이 폴더는 지우지 않습니다.

끝낼 때는 창의 X가 아니라 **트레이 → 앱 종료**입니다.

## 소스에서 실행

Windows PowerShell:

```powershell
powershell -NoProfile -File scripts/publish.ps1
.\artifacts\win-x64\FlowNote.Desktop.exe
```

설치본·포터블 묶음:

```powershell
powershell -NoProfile -File scripts/package.ps1
```

자세한 사용법은 `docs/USAGE.md`입니다.

## 요구

- Windows 10/11 x64
- 빌드: .NET SDK 10
