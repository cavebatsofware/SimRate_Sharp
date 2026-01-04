; SimRate Sharp Installer Script for Inno Setup
; https://jrsoftware.org/isinfo.php

#define MyAppName "SimRate Sharp"
#define MyAppVersion "3.1.0"
#define MyAppPublisher "SimRate Sharp Contributors"
#define MyAppURL "https://github.com/cavebatsofware/SimRate_Sharp"
#define MyAppExeName "SimRateSharp.exe"

[Setup]
AppId={{8F2A5C1D-3B4E-4F6A-9D2C-1E8F7A6B5C4D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=LICENSE-GPL-3.0-only.md
OutputDir=installer_output
OutputBaseFilename=SimRateSharp_Setup_v{#MyAppVersion}
SetupIconFile=app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startupicon"; Description: "Launch at Windows startup"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "SimRateSharp\bin\Release\net10.0-windows\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "SimRateSharp\bin\Release\net10.0-windows\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "SimRateSharp\bin\Release\net10.0-windows\*.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "README.md"; DestDir: "{app}"; Flags: ignoreversion isreadme

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{#MyAppName} (Debug Mode)"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--debug"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\SimRateSharp"
