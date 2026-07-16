; installer/ULM.iss — Inno Setup Skript für den optionalen Windows-Installer.
;
; ULM bleibt in erster Linie eine self-contained Single-File-EXE ohne Installationszwang (siehe
; build-release.sh) — dieses Skript verpackt genau diese bereits gebaute EXE zusätzlich als
; klassischen Setup mit Startmenü-Eintrag, optionalem Desktop-Icon, Eintrag unter "Programme und
; Features" und Deinstaller. Wer keinen Installer möchte, nutzt weiterhin die portable .exe direkt.
;
; Nutzung (Version wird von build-release.sh per /DAppVersion=... übergeben):
;   iscc installer\ULM.iss /DAppVersion=2.32.0
;
; Voraussetzung: dotnet publish muss vorher gelaufen sein (die Quelle unten liegt im
; Standard-Publish-Ausgabeverzeichnis von build-release.sh).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

[Setup]
AppId={{B1E1B1A2-6C3E-4B7A-9B0E-4E6D2E6E9C41}
AppName=Universal Linux Manager
AppVersion={#AppVersion}
AppPublisher=ULM Project
AppPublisherURL=https://zwilling10.github.io/ULM/
AppSupportURL=https://github.com/zwilling10/ULM
DefaultDirName={autopf}\Universal Linux Manager
DefaultGroupName=Universal Linux Manager
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\UniversalLinuxManager.exe
OutputDir=..\release
OutputBaseFilename=UniversalLinuxManager-Setup-v{#AppVersion}-win-x64
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE
SetupIconFile=..\Assets\AppIcon.ico
WizardStyle=modern
; ULM benötigt keine Admin-Rechte für den Alltagsbetrieb (Ventoy-Installation fordert bei Bedarf
; selbst per Verb="runas" Rechte an, siehe MainViewModel.StartVentoyInstall) — "lowest" erlaubt
; eine Installation pro Benutzer in dessen lokalem AppData, ganz ohne UAC-Abfrage beim Setup selbst.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\bin\Release\net8.0-windows\win-x64\publish\UniversalLinuxManager.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Universal Linux Manager"; Filename: "{app}\UniversalLinuxManager.exe"
Name: "{group}\{cm:UninstallProgram,Universal Linux Manager}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Universal Linux Manager"; Filename: "{app}\UniversalLinuxManager.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\UniversalLinuxManager.exe"; Description: "{cm:LaunchProgram,Universal Linux Manager}"; Flags: nowait postinstall skipifsilent
