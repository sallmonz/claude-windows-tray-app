; Inno Setup script for Claude Usage Tray.
; Build locally (after dotnet publish):
;   iscc installer\ClaudeUsageTray.iss /DAppVersion=1.2.0
; The Release workflow on GitHub runs the same command and attaches the result.

#ifndef AppVersion
  #define AppVersion "1.2.0"
#endif
#ifndef SourceExe
  #define SourceExe "..\src\ClaudeUsageTray\bin\publish\win-x64\ClaudeUsageTray.exe"
#endif

#define AppName "Claude Usage Tray"
#define AppExe "ClaudeUsageTray.exe"
#define AppUrl "https://github.com/sallmonz/claude-windows-tray-app"
#define DotnetUrl "https://dotnet.microsoft.com/download/dotnet/10.0"

[Setup]
; Keep AppId the same forever so new versions upgrade the old one.
AppId={{DCB728B6-47EE-4E3F-92CA-C788F58493FA}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
; Per-user install: no admin rights needed.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\ClaudeUsageTray
DisableDirPage=yes
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
; A running copy is closed by [Code] below instead of asking the user to close it.
CloseApplications=no
SetupIconFile=..\assets\app.ico
OutputDir=Output
OutputBaseFilename=ClaudeUsageTray-Setup
LicenseFile=..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startup"; Description: "Start with Windows"; GroupDescription: "Options:"
Name: "showicon"; Description: "Show the icon on the taskbar (not hidden under ^)"; GroupDescription: "Options:"

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"

[Registry]
; Same value the app writes from its "Start with Windows" switch, so both stay in sync.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; \
  ValueName: "ClaudeUsageTray"; ValueData: """{app}\{#AppExe}"""; Tasks: startup
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "ClaudeUsageTray"; \
  Flags: uninsdeletevalue dontcreatekey
; Asks the app to move its tray icon out of the ^ overflow once, on its next start (see TrayIconPromoter.cs).
Root: HKCU; Subkey: "Software\ClaudeUsageTray"; ValueType: dword; ValueName: "PromoteTrayIcon"; ValueData: 1; \
  Tasks: showicon
Root: HKCU; Subkey: "Software\ClaudeUsageTray"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
// Force-close a running copy so install and uninstall never stop to ask.
procedure CloseRunningApp;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/F /IM {#AppExe}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  CloseRunningApp;
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  // usUninstall comes after the user confirmed "remove", before files are deleted.
  if CurUninstallStep = usUninstall then
    CloseRunningApp;
end;

// The app needs the .NET 10 Desktop Runtime (x64). Offer the download page if it is missing.
function HasDesktopRuntime: Boolean;
var
  Find: TFindRec;
begin
  Result := FindFirst(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*'), Find);
  if Result then
    FindClose(Find);
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not HasDesktopRuntime then
  begin
    if MsgBox('Claude Usage Tray needs the .NET 10 Desktop Runtime (x64), which is not installed.' + #13#10#13#10 +
              'Open the download page now? Setup will continue, and the app will run after you install the runtime.',
              mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', '{#DotnetUrl}', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;
