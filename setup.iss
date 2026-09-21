[Setup]
AppName=JeidaIMS
AppVersion=1.0.0
AppPublisher=Jeida Crafts & Prints
DefaultDirName={autopf}\JeidaIMS
DefaultGroupName=JeidaIMS
OutputDir=.\Installer
OutputBaseFilename=JeidaIMS_Installer
Compression=lzma2
SolidCompression=yes
SetupIconFile=.\src\InventoryManagement.UI\app.ico
ArchitecturesInstallIn64BitMode=x64
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\InventoryManagement.UI.exe

[Files]
Source: ".\Publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\JeidaIMS"; Filename: "{app}\InventoryManagement.UI.exe"
Name: "{group}\Uninstall JeidaIMS"; Filename: "{uninstallexe}"
Name: "{autodesktop}\JeidaIMS"; Filename: "{app}\InventoryManagement.UI.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
