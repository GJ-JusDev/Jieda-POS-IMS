[Setup]
AppName=Jeida Crafts & Prints IMS
AppVersion=1.0
DefaultDirName={pf}\JeidaIMS
DefaultGroupName=Jeida IMS
OutputDir=C:\Users\windows-11\Documents\IMS\Installer
OutputBaseFilename=JeidaIMS_Installer
Compression=lzma2
SolidCompression=yes
SetupIconFile=C:\Users\windows-11\Documents\IMS\src\InventoryManagement.UI\app.ico
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "C:\Users\windows-11\Documents\IMS\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Jeida IMS"; Filename: "{app}\InventoryManagement.UI.exe"
Name: "{commondesktop}\Jeida IMS"; Filename: "{app}\InventoryManagement.UI.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
