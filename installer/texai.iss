; Inno Setup script for texAi. Builds a normal Windows installer around the
; single-file self-contained publish output (../publish-single/texAi.exe).
; Installs per-user (no admin/UAC needed) since texAi is a background tool,
; not a system component.

#define MyAppName "texAi"
#define MyAppVersion "2.0.0"
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

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "startupicon"; Description: "Start texAi automatically when Windows starts"
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "..\publish-single\texAi.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\texAi"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\texAi"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\texAi"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
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
