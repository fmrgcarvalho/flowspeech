; SpeechDesk installer (Inno Setup 6)
; Built by .github/workflows/release.yml:
;   iscc /DAppVersion=0.1.0 /DSourceDir=..\publish /DOutputDir=..\dist installer\SpeechDesk.iss
;
; Per-user install: no administrator rights needed, and the keyboard hook works for normal apps.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif

#define AppName "SpeechDesk"
#define AppExe "SpeechDesk.exe"
#define AppUrl "https://github.com/fmrgcarvalho/speechdesk"

[Setup]
AppId={{7D3F9B2E-5A41-4C8E-9E0B-3C6F2A9D1E57}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=fmrgcarvalho
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir={#OutputDir}
OutputBaseFilename=SpeechDesk-setup-win-x64
SetupIconFile=..\src\SpeechDesk.App\Assets\speechdesk.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ShowLanguageDialog=auto
CloseApplications=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"

[CustomMessages]
english.StartWithWindows=Start SpeechDesk when Windows starts
portuguese.StartWithWindows=Iniciar o SpeechDesk com o Windows
english.LaunchApp=Start SpeechDesk now
portuguese.LaunchApp=Iniciar o SpeechDesk agora

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartWithWindows}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

; Settings, history and downloaded models stay in %AppData%\SpeechDesk so they survive updates.
