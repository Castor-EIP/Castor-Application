#define AppName "Castor Studio"
#ifndef Version
  #define Version "undefined"
#endif
#define AppVersion Version
#define AppPublisher "Castor Team"
#define AppExeName "CastorStudio.exe"

[Setup]
AppId={{A1B2C3D4-CASTOR-STUDIO-APP}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}

OutputDir=Output
OutputBaseFilename=CastorStudioSetup

Compression=lzma
SolidCompression=yes

WizardStyle=modern

; Admin permission required for FFmpeg / capture
PrivilegesRequired=admin

; Clean uninstall
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Files]
; main App
Source: "publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Castor"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\Castor"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le bureau"; GroupDescription: "Options supplémentaires:"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Lancer Castor"; Flags: nowait postinstall skipifsilent
