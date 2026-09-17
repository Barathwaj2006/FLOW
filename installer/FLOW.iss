; ==============================================================================
; FLOW — AI Voice Productivity Platform for Windows 10/11 x64
; Inno Setup 6 Production Installer Configuration
; ==============================================================================

#define MyAppName "FLOW"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "FLOW Voice Productivity"
#define MyAppURL "https://github.com/Barathwaj2006/FLOW"
#define MyAppExeName "Flow.Host.Windows.exe"
#define SourceDir "..\artifacts\publish\win-x64"

[Setup]
; Unique application GUID for Windows Add/Remove Programs
AppId={{8B589DF1-E4F2-491A-851F-1444155B77DF}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion} (Windows Native x64)
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases

; Default to %ProgramFiles%\FLOW (mandatory secure path for uiAccess="true")
DefaultDirName={autopf}\FLOW
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=FLOW-Setup-v{#MyAppVersion}-win-x64
UninstallDisplayIcon={app}\{#MyAppExeName}

; Compression & 64-bit Architecture
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

; Administrative privileges required for %ProgramFiles% and UIPI uiAccess registration
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog

; Modern UI Style
WizardStyle=modern
WizardResizable=no
DisableWelcomePage=no

; Process management
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startwithwindows"; Description: "Start FLOW automatically on Windows sign-in (recommended)"; GroupDescription: "Startup Options:"; Flags: checkedonce

[Files]
; Core application files from self-contained publish directory
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} (Diagnostic Status)"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--status"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Windows Startup autorun key (launches quietly minimized to tray)
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "FLOW"; ValueData: """{app}\{#MyAppExeName}"" --minimized"; Flags: uninsdeletevalue; Tasks: startwithwindows
; Application registry registration
Root: HKLM; Subkey: "Software\FLOW"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\FLOW"; ValueType: string; ValueName: "Version"; ValueData: "{#MyAppVersion}"; Flags: uninsdeletekey

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Helper function to check if FLOW is currently running
function IsFlowRunning(): Boolean;
var
  ResultCode: Integer;
begin
  Result := False;
  // Use tasklist to verify if Flow.Host.Windows.exe is active
  if Exec('tasklist', '/FI "IMAGENAME eq Flow.Host.Windows.exe" /NH', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    // Handled natively by CloseApplications=yes
  end;
end;

// Verify WebView2 Runtime on user machine
function IsWebView2Available(): Boolean;
var
  InstallPath: String;
begin
  Result := RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', InstallPath) or
            RegQueryStringValue(HKCU, 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', InstallPath);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not IsWebView2Available() then
  begin
    Log('[FLOW Setup] Microsoft Edge WebView2 Runtime is recommended for web hub views.');
  end;
end;
