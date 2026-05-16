@echo off
setlocal
title Collect GameForge Debug Logs
color 0B

cd /d "%~dp0"
set "INSTALL_DIR=%LOCALAPPDATA%\GameForgeAIEngine"
set "OUT=%~dp0GameForge_Debug_Logs"
set "ZIP=%~dp0GameForge_Debug_Logs.zip"

if exist "%OUT%" rmdir /S /Q "%OUT%"
mkdir "%OUT%"

echo Collecting debug logs...

if exist "%INSTALL_DIR%\launch_log.txt" copy "%INSTALL_DIR%\launch_log.txt" "%OUT%\installed_launch_log.txt" >nul
if exist "%~dp0launch_log.txt" copy "%~dp0launch_log.txt" "%OUT%\source_launch_log.txt" >nul
if exist "%~dp0install_log.txt" copy "%~dp0install_log.txt" "%OUT%\install_log.txt" >nul
if exist "%~dp0repair_log.txt" copy "%~dp0repair_log.txt" "%OUT%\repair_log.txt" >nul
if exist "%~dp0DIAGNOSTICS_CROSS_REFERENCE_REPORT.md" copy "%~dp0DIAGNOSTICS_CROSS_REFERENCE_REPORT.md" "%OUT%\DIAGNOSTICS_CROSS_REFERENCE_REPORT.md" >nul
if exist "%INSTALL_DIR%\package.json" copy "%INSTALL_DIR%\package.json" "%OUT%\installed_package.json" >nul
if exist "%~dp0package.json" copy "%~dp0package.json" "%OUT%\source_package.json" >nul

echo System info > "%OUT%\system_info.txt"
echo Date: %date% %time% >> "%OUT%\system_info.txt"
echo Source folder: %~dp0 >> "%OUT%\system_info.txt"
echo Install folder: %INSTALL_DIR% >> "%OUT%\system_info.txt"
where node >> "%OUT%\system_info.txt" 2>&1
node -v >> "%OUT%\system_info.txt" 2>&1
where npm >> "%OUT%\system_info.txt" 2>&1
call npm -v >> "%OUT%\system_info.txt" 2>&1

if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Compress-Archive -Path '%OUT%\*' -DestinationPath '%ZIP%' -Force"

echo.
echo Debug logs collected:
echo %ZIP%
pause
endlocal
