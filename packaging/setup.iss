#define MyAppName "FlowNote"
#define MyAppVersion "0.4.0"
#define MyAppPublisher "sukwoo0711-maker"
#define MyAppURL "https://github.com/sukwoo0711-maker/FlowNote"
#define MyAppExeName "FlowNote.Desktop.exe"

[Setup]
AppId={{8F3C2A11-6D54-4B9E-9A71-2C1E0F84B6D3}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={localappdata}\Programs\FlowNote
DefaultGroupName=FlowNote
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\dist
OutputBaseFilename=FlowNote-0.4.0-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
CloseApplications=yes
SetupLogging=yes
MinVersion=10.0
DisableWelcomePage=no

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕 화면에 바로 가기 만들기"; GroupDescription: "바로 가기:"; Flags: unchecked

[Files]
Source: "..\artifacts\package\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\FlowNote"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\FlowNote 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\FlowNote"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "FlowNote 실행"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM FlowNote.Desktop.exe /F"; Flags: runhidden; RunOnceId: "StopFlowNote"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
