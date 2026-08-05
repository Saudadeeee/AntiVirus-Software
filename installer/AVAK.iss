; ---------------------------------------------------------------------------
;  AVAK installer - Inno Setup 6
;
;  Build with:  iscc installer\AVAK.iss
;  Requires:    publish.bat to have produced .\dist first
;
;  Deliberate choices:
;    * per-user install by default (no UAC prompt, no Program Files write)
;    * the Explorer context menu is registered by the app, not the installer,
;      so uninstalling never leaves an orphaned verb behind
;    * user data under %LOCALAPPDATA%\AVAK is preserved unless the user opts out
; ---------------------------------------------------------------------------

#define AppName        "AVAK"
#define AppVersion     "3.0.0"
#define AppPublisher   "AVAK"
#define AppURL         "https://example.com/avak"
#define AppExe         "AVAK.exe"

[Setup]
AppId={{9C2F1E4A-6B3D-4E75-9F0C-1D8A2B5C7E31}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}/support
AppUpdatesURL={#AppURL}/download
VersionInfoVersion={#AppVersion}

DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline

LicenseFile=..\LICENSE.txt
InfoAfterFile=..\README.md
SetupIconFile=..\assets\avak.ico
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern

OutputDir=..\dist\installer
OutputBaseFilename=AVAK-{#AppVersion}-setup
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
; Uncomment when you ship a Vietnamese build:
; Name: "vietnamese"; MessagesFile: "compiler:Languages\Vietnamese.isl"

[Tasks]
Name: "desktopicon";  Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon";  Description: "Start AVAK with Windows (minimised to the notification area)"; GroupDescription: "Startup:"; Flags: unchecked

[Files]
; publish.bat emits a single self-contained executable plus the editable content
Source: "..\dist\{#AppExe}";              DestDir: "{app}"; Flags: ignoreversion
Source: "..\dist\content\*";              DestDir: "{app}\content"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\dist\plugins\*";              DestDir: "{app}\plugins"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\dist\LICENSE.txt";            DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\dist\PRIVACY.md";             DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\dist\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\dist\README.md";              DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#AppName}";               Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}";     Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}";         Filename: "{app}\{#AppExe}"; Tasks: desktopicon
Name: "{userstartup}\{#AppName}";         Filename: "{app}\{#AppExe}"; Parameters: "--tray"; Tasks: startupicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start {#AppName} now"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; the app owns its shell verb, so let it clean up after itself
Filename: "{app}\{#AppExe}"; Parameters: "--unregister-shell"; Flags: runhidden; RunOnceId: "AvakShellVerb"

[UninstallDelete]
Type: filesandordirs; Name: "{app}\content"
Type: filesandordirs; Name: "{app}\plugins"

[Code]
var
  KeepDataPage: TInputOptionWizardPage;

procedure InitializeWizard;
begin
  KeepDataPage := CreateInputOptionPage(wpSelectTasks,
    'Your AVAK data',
    'Quarantined files, settings and scan history',
    'AVAK keeps everything under %LOCALAPPDATA%\AVAK. Choose what should happen when you uninstall.',
    True, False);
  KeepDataPage.Add('Keep my quarantine, settings and history');
  KeepDataPage.Add('Delete everything AVAK stored');
  KeepDataPage.SelectedValueIndex := 0;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\AVAK');
    if DirExists(DataDir) then
    begin
      if MsgBox('Also delete your AVAK settings, scan history and quarantined files?' + #13#10 + #13#10 +
                DataDir + #13#10 + #13#10 +
                'Choose No if you plan to reinstall.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
