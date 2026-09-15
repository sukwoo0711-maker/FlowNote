# V4 저장소 맵

조사 시각: 2026-09-15  
저장소: `C:\GitRepositories\FlowNote`  
브랜치: `main` (origin: `sukwoo0711-maker/FlowNote`)

## 수정 대상 (기존 앱)

| 영역 | 경로 |
|---|---|
| 솔루션 | `FlowNote.sln` |
| SDK | .NET 10.0.401 / `net10.0-windows` |
| 플로팅 | `src/FlowNote.Desktop/Views/FloatingCapsuleWindow.xaml` |
| 메인 | `src/FlowNote.Desktop/MainWindow.xaml` |
| 저장 | `src/FlowNote.Infrastructure/Persistence/SqliteEntryService.cs` |
| 스키마 | `src/FlowNote.Infrastructure/Persistence/SchemaSql.cs` (현재 version 2) |
| 실행 | `artifacts/win-x64/FlowNote.Desktop.exe` |

새 프로젝트를 만들지 않는다. Assist는 Core/Infrastructure/Desktop에 추가한다.

## 데이터 경로

| profile | 경로 | mutex |
|---|---|---|
| live | `%LOCALAPPDATA%\FlowNote\live` | `Local\FlowNote.Desktop.Live` |
| demo | `%LOCALAPPDATA%\FlowNote\demo` 또는 `--data-root` | Demo / data-root 해시 |
| assist 검수 | `--data-root` 아래 `FlowNote\live` | data-root별 |

운영 live DB에 fixture를 넣지 않는다.

## 미커밋 (조사 시점)

- `FlowNote_Local_Assist_V4/`
- `FlowNote_Local_Assist_V4_Implementation_Pack.zip`
- `docs/assist-v4/`

reset/clean 하지 않음.
