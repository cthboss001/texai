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
; The whole self-contained publish folder, not a one-file bundle. A
; PublishSingleFile exe unpacks its native WPF libraries to %TEMP%\.net on
; launch and throws DllNotFoundException out of a window procedure if that copy
; is missing or half-written, which kills the process with no visible error.
; Shipping the files as files removes the unpacking step, and the user never
; sees the difference: this folder is %LocalAppData%\texAi either way.
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\texAi"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\texAi"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\texAi"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
; No skipifsilent: UpdateService runs this installer with /VERYSILENT for the
; background auto-update, and relies on this entry to bring texAi back up
; afterwards. A silent install with nothing left running would look like the
; update broke the app.
Filename: "{app}\{#MyAppExeName}"; Description: "Launch texAi now"; Flags: nowait postinstall

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
