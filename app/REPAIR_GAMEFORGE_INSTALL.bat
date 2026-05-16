@echo off
setlocal EnableExtensions EnableDelayedExpansion
title Repair GameForge AI Engine v3.0.0
color 0E

cd /d "%~dp0"
set "INSTALL_DIR=%LOCALAPPDATA%\GameForgeAIEngine"
set "DESKTOP_SHORTCUT=%USERPROFILE%\Desktop\GameForge AI Engine.lnk"
set "START_MENU_DIR=%APPDATA%\Microsoft\Windows\Start Menu\Programs\GameForge AI Engine"
set "START_MENU_SHORTCUT=%START_MENU_DIR%\GameForge AI Engine.lnk"
set "TARGET=%INSTALL_DIR%\START_GAMEFORGE_SAFE_DIAGNOSTIC.cmd"
set "REPAIR_LOG=%~dp0repair_log.txt"

echo ============================================================ > "%REPAIR_LOG%"
echo GameForge Repair v3.0.0 >> "%REPAIR_LOG%"
echo Started: %date% %time% >> "%REPAIR_LOG%"
echo ============================================================ >> "%REPAIR_LOG%"

echo ============================================================
echo  Repair GameForge AI Engine
echo ============================================================
echo This will re-copy files, reinstall dependencies,
echo run health checks, and recreate shortcuts.
echo.
pause

if not exist package.json (
  echo ERROR: Run this from the extracted latest GameForge folder.
  pause
  exit /b 1
)

if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"

robocopy "%~dp0" "%INSTALL_DIR%" /E /XD node_modules dist .git /XF launch_log.txt install_log.txt repair_log.txt debug_logs.zip /NFL /NDL /NJH /NJS /NP >> "%REPAIR_LOG%" 2>&1
if %errorlevel% GEQ 8 (
  echo ERROR: Copy failed.
  pause
  exit /b 1
)

cd /d "%INSTALL_DIR%"
call npm install >> "%REPAIR_LOG%" 2>&1
if %errorlevel% neq 0 (
  echo ERROR: npm install failed.
  pause
  exit /b 1
)

node --check main.js >> "%REPAIR_LOG%" 2>&1
if %errorlevel% neq 0 pause & exit /b 1
node --check preload.js >> "%REPAIR_LOG%" 2>&1
if %errorlevel% neq 0 pause & exit /b 1
node --check src\app.js >> "%REPAIR_LOG%" 2>&1
if %errorlevel% neq 0 pause & exit /b 1

if not exist "%START_MENU_DIR%" mkdir "%START_MENU_DIR%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$s=(New-Object -COM WScript.Shell).CreateShortcut('%DESKTOP_SHORTCUT%'); $s.TargetPath='%TARGET%'; $s.WorkingDirectory='%INSTALL_DIR%'; $s.IconLocation='%SystemRoot%\System32\shell32.dll,13'; $s.Save()"
powershell -NoProfile -ExecutionPolicy Bypass -Command "$s=(New-Object -COM WScript.Shell).CreateShortcut('%START_MENU_SHORTCUT%'); $s.TargetPath='%TARGET%'; $s.WorkingDirectory='%INSTALL_DIR%'; $s.IconLocation='%SystemRoot%\System32\shell32.dll,13'; $s.Save()"

echo.
echo Repair complete.
pause
endlocal
