#ifndef AppVersion
  #error AppVersion must be supplied by the release build.
#endif
#ifndef SourceRoot
  #error SourceRoot must be supplied by the release build.
#endif
#ifndef OutputRoot
  #error OutputRoot must be supplied by the release build.
#endif

[Setup]
AppId={{5C722BC6-4642-4EAE-AFD1-2A29A7010174}
AppName=Logiciel Pharmacie
AppVersion={#AppVersion}
AppVerName=Logiciel Pharmacie {#AppVersion}
AppPublisher=Logiciel Pharmacie
DefaultDirName={autopf}\Logiciel Pharmacie
DefaultGroupName=Logiciel Pharmacie
OutputDir={#OutputRoot}
OutputBaseFilename=Logiciel-Pharmacie-{#AppVersion}-win-x64-Setup
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000
PrivilegesRequired=admin
Compression=zip
SolidCompression=no
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter=Pharmacy.Desktop.exe
RestartApplications=no
UninstallDisplayIcon={app}\Pharmacy.Desktop.exe
SetupLogging=yes
ChangesEnvironment=no

[Files]
Source: "{#SourceRoot}\desktop\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\deployment\*"; DestDir: "{app}\deployment"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#SourceRoot}\pilot.template.json"; DestDir: "{commonappdata}\LogicielPharmacie\config"; DestName: "pilot.json"; Flags: onlyifdoesntexist uninsneveruninstall

[Dirs]
Name: "{commonappdata}\LogicielPharmacie\config"; Permissions: users-readexec; Flags: uninsneveruninstall
Name: "{commonappdata}\LogicielPharmacie\backups"; Flags: uninsneveruninstall
Name: "{commonappdata}\LogicielPharmacie\support"; Permissions: users-modify; Flags: uninsneveruninstall

[Icons]
Name: "{group}\Logiciel Pharmacie"; Filename: "{app}\Pharmacy.Desktop.exe"
Name: "{autodesktop}\Logiciel Pharmacie"; Filename: "{app}\Pharmacy.Desktop.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis :"; Flags: unchecked

[Run]
Filename: "{sys}\icacls.exe"; Parameters: """{commonappdata}\LogicielPharmacie\backups"" /grant ""NT SERVICE\MSSQL$PHARMACY:(OI)(CI)M"""; StatusMsg: "Préparation du dossier de sauvegarde SQL..."; Flags: runhidden waituntilterminated
Filename: "{app}\deployment\Pharmacy.Deployment.exe"; Parameters: "preflight --config ""{commonappdata}\LogicielPharmacie\config\pilot.json"""; StatusMsg: "Contrôle des prérequis pilote..."; Flags: runhidden waituntilterminated
Filename: "{app}\deployment\Pharmacy.Deployment.exe"; Parameters: "install-or-upgrade --config ""{commonappdata}\LogicielPharmacie\config\pilot.json"""; StatusMsg: "Préparation contrôlée de la base SQL..."; Flags: runhidden waituntilterminated
Filename: "{app}\Pharmacy.Desktop.exe"; Description: "Lancer Logiciel Pharmacie"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  Version: String;
begin
  Result := True;
  if not RegQueryStringValue(HKLM32,
    'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
    'pv', Version) then
  begin
    MsgBox('Microsoft Edge WebView2 Evergreen est requis avant l''installation.', mbError, MB_OK);
    Result := False;
  end;
end;
