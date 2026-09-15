# 기술 근거 — 공식 문서

확인일: 2026-09-14. 이 파일은 기술 선택의 근거이며 실제 구현/성능 검증 결과가 아니다. 특정 NuGet 패치 번호는 개발 시점에 다시 확인하고 고정한다.

## R1. WPF와 Windows 플랫폼

공식 문서는 WPF가 XAML, 데이터 바인딩, 스타일·템플릿을 제공하고 Windows에서만 실행된다고 설명한다. 따라서 이 패키지의 WPF 선택은 Windows 우선 출시라는 잠정 조건에 의존한다.

`https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/`

## R2. .NET 지원 정책

확인 시점에 .NET 10은 LTS 활성 지원 계열이다. 지원 중 패치를 사용하고 재현 가능한 버전을 고정한다. Self-contained 배포에는 런타임도 포함되므로 이후 런타임 패치가 필요할 때 앱 패키지를 갱신할 책임이 있다.

`https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core`

## R3. Topmost의 의미

Topmost는 일반 창보다 높은 z-order를 뜻한다. 모든 topmost 창보다 무조건 위라는 의미가 아니다. 앱의 기능 설명과 검수 범위를 일반 데스크톱 창 기준으로 한정한다.

`https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.topmost?view=windowsdesktop-10.0`

## R4. 상단 고정과 활성화의 분리

ShowActivated는 처음 창을 표시할 때 활성화 여부를 제어한다. 표시 전에 설정해야 하며 이후 사용자의 선택에 의한 활성화는 별도다. 이 속성 하나만으로 이후 모든 포커스 문제를 해결했다고 주장하지 않고 실제 동작을 검수한다.

`https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.showactivated?view=windowsdesktop-10.0`

## R5. SQLite Async 주의점

Microsoft.Data.Sqlite의 ADO.NET Async 메서드는 동기 실행된다는 공식 제한이 있다. 무거운 DB 작업을 UI 스레드 밖에서 실행하고 연결·쓰기를 안전하게 관리해야 한다.

`https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/async`

## R6. SQLite 백업

BackupDatabase로 앱 실행 중 DB 백업이 가능하다. 문서는 백업 중 다른 연결의 쓰기가 차단될 수 있다는 점도 설명한다. 첨부파일은 DB 백업에 자동 포함되지 않으므로 앱 수준의 일관성 있는 패키징이 별도로 필요하다.

`https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/backup`

## R7. 전역 단축키

RegisterHotKey로 전역 단축키를 정의한다. 실패 반환과 이미 사용 중인 조합을 처리해야 한다. 키 입력 전체를 수집하는 구현은 필요하지 않다.

`https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey`

## R8. WPF 목록 성능

WPF의 UI 가상화·컨테이너 재사용을 확인하고, 가상화를 무력화하는 컨트롤 구조를 피한다. UI 가상화는 모든 데이터가 자동으로 지연 로드된다는 뜻이 아니므로 조회 범위 제한도 별도로 적용한다.

`https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/optimizing-performance-controls`

## 문서와 설계 판단의 구분

팔레트, 화면 크기, 타임라인 버킷, 파일 크기 제한, 집계 규칙, DB 모델, 단계 분할, 성능 목표는 이 제품을 위해 제안한 명세다. Microsoft가 이 제품의 UX·성능·보안을 보증한 것이 아니다.
