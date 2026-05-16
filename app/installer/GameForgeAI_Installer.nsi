; GameForge AI Engine NSIS Installer Script
; Requires NSIS installed on Windows.
; This creates a more traditional .exe installer.
; Note: This installer assumes dependencies were already prepared/built.

Name "GameForge AI Engine"
OutFile "GameForgeAIEngine_Setup.exe"
InstallDir "$LOCALAPPDATA\GameForgeAIEngine"
RequestExecutionLevel user

Page directory
Page instfiles

Section "Install"
  SetOutPath "$INSTDIR"
  File /r "..\*.*"

  CreateShortCut "$DESKTOP\GameForge AI Engine.lnk" "$INSTDIR\Launch GameForge AI.bat"
  CreateShortCut "$SMPROGRAMS\GameForge AI Engine.lnk" "$INSTDIR\Launch GameForge AI.bat"

  WriteUninstaller "$INSTDIR\Uninstall.exe"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\GameForge AI Engine.lnk"
  Delete "$SMPROGRAMS\GameForge AI Engine.lnk"
  RMDir /r "$INSTDIR"
SectionEnd