; Inno Setup script for texAi. Builds a normal Windows installer around the
; self-contained publish folder (../publish). Installs per-user (no admin/UAC
; needed) since texAi is a background tool, not a system component.

#define MyAppName "texAi"
; Must match <Version> in texAi.csproj and the GitHub release tag.
#define MyAppVersion "2.0.3"
#define MyAppPublisher "cthboss001"
#define MyAppExeName "texAi.exe"

[Setup]
AppId={{94B3C29A-EF00-4714-A224-76A16EA6BC84}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=texAi-Setup
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\Assets\icon.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startupicon"; Description: "Start texAi automatically when Windows starts"
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
; The whole self-contained publish folder, not a one-file bundle. 2.0.2
; shipped a PublishSingleFile exe on its own, and a single-file WPF build leaves
; its native _cor3 DLLs loose beside the exe unless
; IncludeNativeLibrariesForSelfExtract is set, so every launch died with
; DllNotFoundException before any window existed.
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\texAi"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\texAi"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
; --background keeps a login in the tray; any other launch opens the dashboard.
Name: "{userstartup}\texAi"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--background"; Tasks: startupicon

[Run]
; Two entries because the two cases want different things. UpdateService runs
; this installer with /VERYSILENT and relies on it to bring texAi back up, and a
; silent update should land back in the tray, not pop the dashboard. Someone
; clicking through the wizard gets the dashboard, so they can see it worked.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--background"; Flags: nowait; Check: WizardSilent
Filename: "{app}\{#MyAppExeName}"; Description: "Launch texAi now"; Flags: nowait postinstall skipifsilent

; Deliberately not listed under [UninstallDelete]: %AppData%\texAi\settings.json
; is the user's own hotkeys and model choice, and an uninstall is often a
; reinstall. It holds nothing sensitive, since no rewritten text is ever
; written to disk.

[Code]
procedure KillRunningTexAi;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM texAi.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;

function InitializeSetup(): Boolean;
begin
  KillRunningTexAi;
  Result := True;
end;

function InitializeUninstall(): Boolean;
begin
  KillRunningTexAi;
  Result := True;
end;
