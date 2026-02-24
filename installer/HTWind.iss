#define MyAppName "HTWind"
#ifndef MyAppExeName
  #define MyAppExeName "HTWind.exe"
#endif
#ifndef SourceDir
  #define SourceDir "..\\out\\publish"
#endif
#ifndef MyAppVersion
  #define MyAppVersion GetVersionNumbersString(AddBackslash(SourceDir) + MyAppExeName)
#endif
#ifndef MyAppPublisher
  #define MyAppPublisher "sametcn99"
#endif
#ifndef MyAppUrl
  #define MyAppUrl "https://github.com/sametcn99/HTWind"
#endif

[Setup]
AppId={{D04BDB23-BE34-4B40-9F8A-D7FB476C3A47}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}
AppUpdatesURL={#MyAppUrl}
AppCopyright=Copyright (C) 2026 {#MyAppPublisher}
DefaultDirName={autopf}\\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
LicenseFile=..\\LICENSE
InfoAfterFile=thanks.txt
OutputDir=..\\dist
OutputBaseFilename=HTWind-setup-{#MyAppVersion}
SetupIconFile=..\\assets\\favicon.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\\{#MyAppExeName}
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalShortcuts}"

[Files]
Source: "{#SourceDir}\\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"
Name: "{autodesktop}\\{#MyAppName}"; Filename: "{app}\\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\\{#MyAppExeName}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[CustomMessages]
CreateDesktopIcon=Create a desktop shortcut
AdditionalShortcuts=Shortcut options:
LaunchApp=Start HTWind now
UninstallDataWarning=Uninstall will remove widgets and related data stored in AppData\Local\HTWind. If you are uninstalling to update HTWind, back up this folder before continuing.

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\\HTWind"
Type: filesandordirs; Name: "{localappdata}\\{#MyAppExeName}.WebView2"
Type: filesandordirs; Name: "{localappdata}\\HTWind.WebView2"

[Code]
function InitializeUninstall(): Boolean;
begin
  Result := True;

  if not UninstallSilent then
  begin
    SuppressibleMsgBox(ExpandConstant('{cm:UninstallDataWarning}'), mbInformation, MB_OK, IDOK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RegDeleteValue(HKCU, 'SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run', '{#MyAppName}');
  end;
end;
