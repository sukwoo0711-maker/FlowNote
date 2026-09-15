# 구현 참고와 확인 범위

이 문서의 출처는 Microsoft 공식 문서다. 명세의 크기·색·상호작용·성능 목표는 FlowNote에 제안한 설계값이며 Microsoft 표준 수치가 아니다.
라이브러리의 실제 버전, API 지원, 프로젝트의 실행 방식은 구현 worker가 현재 환경에서 확인해야 한다.

## 1. 불규칙한 형태의 WPF 창

`AllowsTransparency=True`는 비직사각형 창을 위한 기능이며 `WindowStyle=None`과 함께 사용한다.
Background가 Transparent인 일반 불투명 창과 구별해야 한다.

https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency?view=windowsdesktop-10.0
https://learn.microsoft.com/en-us/dotnet/desktop/wpf/windows/

## 2. OS 모서리와 직접 그린 캡슐

Windows 문서는 per-pixel alpha layering 또는 window regions를 쓰는 창은 OS 모서리 둥글림의 적용 대상이 될 수 없다고 설명한다.
따라서 직접 그리는 26 DIP 반경 캡슐과 DWM 기본 창 둥글림을 같은 방법으로 취급하지 않는다.

https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/ui/apply-rounded-corners

## 3. 알파 반투명 ≠ Acrylic 배경 블러

Microsoft는 Acrylic을 배경과 관계를 갖는 반투명 유리 재질로 설명한다. WinUI의 SystemBackdrop 예제는 WPF의 그대로 사용 가능한 속성이 아니다.
이번 기본 요구는 짙은 반투명 유리풍 표면이다. 실제 backdrop blur는 지원·성능·창 모양을 확인한 선택적 경로이고 기본 완료 조건을 대체하지 않는다.

https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/materials
https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops

## 4. 처음 표시할 때의 활성화

ShowActivated=False는 Show 전에 설정해야 처음 표시할 때의 활성화를 막는다. 사용자 클릭 이후에는 정상 활성화/비활성화가 진행된다.
이 속성을 전체 포커스 문제의 만능 해결책으로 해석하지 않는다.

https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.showactivated?view=windowsdesktop-10.0

## 5. 한글 조합과 입력 이벤트

TextInput은 장치 독립적인 텍스트 입력 이벤트이며 IME의 여러 키 이벤트가 하나의 텍스트 입력으로 이어질 수 있다.
composition 관리와 실제 Windows 검수가 필요하다. Enter key code만으로 조합 완료와 기록 명령을 구분했다고 가정하지 않는다.

https://learn.microsoft.com/en-us/dotnet/api/system.windows.uielement.textinput?view=windowsdesktop-10.0
https://learn.microsoft.com/en-us/dotnet/api/system.windows.input.textcompositionmanager?view=windowsdesktop-10.0

## 6. 캡처와 증거

RenderTargetBitmap은 Visual을 bitmap으로 변환한다. 이것은 외부 데스크톱 합성 화면·창 경계·z-order·포커스 동작의 직접 증거가 아니다.
영역 캡처에는 현재 타깃이 지원하는 OS 캡처 API가 필요하다. Graphics.CopyFromScreen은 화면 직사각형의 픽셀을 복사하는 후보 API다.
문서의 예제를 대상 SDK 확인 없이 붙여넣지 않는다. 특히 DIP→물리좌표·다중모니터·권한·선택 오버레이 숨김을 별도로 처리한다.

https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.rendertargetbitmap?view=windowsdesktop-10.0
https://learn.microsoft.com/en-us/dotnet/api/system.drawing.graphics.copyfromscreen

## 7. 이 패키지에서 확인한 것과 확인하지 않은 것

확인: 이전 플로팅 명세의 관련 부분, 사용자가 제공한 두 PNG, 위 공식 기술 문서, 패키지 내 파일/JSON/ZIP 유효성.
미확인: 사용자 저장소 소스·실행 빌드, worker의 실제 캡처 구현, Windows 실창/IME/배율/성능.
패키지는 구현 지시서다. 실제 앱 완성·동작·시각 승인 결과로 소개하지 않는다.
