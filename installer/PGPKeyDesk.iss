; Inno Setup 6 script for PGPKeyDesk (Windows x64, self-contained publish output).
; Build:  ISCC.exe /DAppVersion=2.0.1 [/DPublishDir=..\publish] PGPKeyDesk.iss
; Normally invoked through installer\build-installer.ps1 (parses the version from the vbproj).

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

#define AppName      "PGPKeyDesk"
#define AppExeName   "PGPKeyDesk.exe"
#define AppPublisher "PGPKeyDesk contributors"
#define AppURL       "https://github.com/domezos2024/OpenGPG"
#define FileProgId   "PGPKeyDesk.File"

#if !FileExists(PublishDir + "\" + AppExeName)
  #error "Publish output not found. Run: dotnet publish PGPKeyDesk.vbproj -c Release -r win-x64 --self-contained true -o publish (or use build-installer.ps1)"
#endif

[Setup]
; Fixed AppId: never change it, it identifies the product for upgrade-in-place and uninstall.
AppId={{55697A54-DB30-4B56-A276-6D601DD7C3B5}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/issues
AppUpdatesURL={#AppURL}/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription={#AppName} Setup
DefaultDirName={autopf}\PGPKeyDesk
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayName={#AppName}
UninstallDisplayIcon={app}\{#AppExeName}
LicenseFile=..\LICENSE
SetupIconFile=..\app.ico
OutputDir=Output
OutputBaseFilename=PGPKeyDesk-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
CloseApplications=yes
RestartApplications=no
ChangesAssociations=yes
UsePreviousTasks=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german";  MessagesFile: "compiler:Languages\German.isl"

[CustomMessages]
english.DeleteDataPrompt=Do you also want to delete your PGPKeyDesk user data (profiles, keys, keyring)?%n%nFolder: %1%n%nWARNING: profiles.json contains your encrypted private keys. Without a backup they cannot be recovered.
german.DeleteDataPrompt=Sollen auch Ihre PGPKeyDesk-Benutzerdaten (Profile, Schlüssel, Schlüsselbund) gelöscht werden?%n%nOrdner: %1%n%nWARNUNG: profiles.json enthält Ihre verschlüsselten privaten Schlüssel. Ohne Sicherung sind diese nicht wiederherstellbar.
english.AssocTask=Add "Open with PGPKeyDesk" for .asc, .pgp and .gpg files (does not change default programs)
german.AssocTask="Mit PGPKeyDesk öffnen" für .asc-, .pgp- und .gpg-Dateien hinzufügen (ändert keine Standardprogramme)
english.OldWindows=PGPKeyDesk requires Windows 10 version 1809 or newer (64-bit).
german.OldWindows=PGPKeyDesk benötigt Windows 10 Version 1809 oder neuer (64 Bit).

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "fileassoc"; Description: "{cm:AssocTask}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; App Paths entry
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExeName}"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName}"; Flags: uninsdeletekey
Root: HKA; Subkey: "Software\Microsoft\Windows\CurrentVersion\App Paths\{#AppExeName}"; ValueType: string; ValueName: "Path"; ValueData: "{app}"
; Optional "Open with" registration (task fileassoc, unchecked by default). Never sets a default handler.
Root: HKA; Subkey: "Software\Classes\{#FileProgId}"; ValueType: string; ValueName: ""; ValueData: "PGPKeyDesk OpenPGP file"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\{#FileProgId}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#AppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\{#FileProgId}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#AppExeName}"" ""%1"""; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.asc\OpenWithProgids"; ValueType: string; ValueName: "{#FileProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.pgp\OpenWithProgids"; ValueType: string; ValueName: "{#FileProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\.gpg\OpenWithProgids"; ValueType: string; ValueName: "{#FileProgId}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup(): Boolean;
var
  V: TWindowsVersion;
begin
  Result := True;
  GetWindowsVersionEx(V);
  // Defensive duplicate of MinVersion=10.0.17763 (gives a localized message).
  if (V.Major < 10) or ((V.Major = 10) and (V.Build < 17763)) then
  begin
    MsgBox(CustomMessage('OldWindows'), mbCriticalError, MB_OK);
    Result := False;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{userappdata}\PGPKeyDesk');
    // Never delete user data silently: skipped for /SILENT and /VERYSILENT; default button is NO.
    if (not UninstallSilent) and DirExists(DataDir) then
      if MsgBox(FmtMessage(CustomMessage('DeleteDataPrompt'), [DataDir]),
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
  end;
end;
