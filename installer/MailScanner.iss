#define MyAppName "MailScanner"
#define MyAppPublisher "DieListe01"
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\\artifacts\\installer"
#endif

[Setup]
AppId={{E54CF8B2-0C4A-4A39-9B5A-7E18C4808D58}}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=no
UsePreviousAppDir=no
AlwaysShowDirOnReadyPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=MailScanner-Setup-{#AppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\MailScanner.exe

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\MailScanner.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\MailScanner.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Desktop-Verknuepfung erstellen"; GroupDescription: "Zusaetzliche Symbole:"; Flags: unchecked

[Run]
Filename: "{app}\MailScanner.exe"; Description: "{#MyAppName} starten"; Flags: nowait postinstall skipifsilent
