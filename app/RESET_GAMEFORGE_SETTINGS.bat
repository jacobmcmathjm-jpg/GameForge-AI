@echo off
title Reset GameForge Settings / Cache
color 0C

set "INSTALL_DIR=%LOCALAPPDATA%\GameForgeAIEngine"
set "DOCS=%USERPROFILE%\Documents"

echo ============================================================
echo  Reset GameForge Settings / Cache
echo ============================================================
echo This will not remove your installed app.
echo It will remove common generated/cache folders.
echo.
echo Potential folders:
echo %LOCALAPPDATA%\GameForgeAIEngine\launch_log.txt
echo %DOCS%\GameForgePhotorealMode
echo %DOCS%\GameForgeModelGatherer
echo %DOCS%\GameForgeAnimationAssetGatherer
echo %DOCS%\GameForgeAutoImportedAssets
echo.
pause

if exist "%INSTALL_DIR%\launch_log.txt" del "%INSTALL_DIR%\launch_log.txt"
if exist "%DOCS%\GameForgePhotorealMode" rmdir /S /Q "%DOCS%\GameForgePhotorealMode"
if exist "%DOCS%\GameForgeModelGatherer" rmdir /S /Q "%DOCS%\GameForgeModelGatherer"
if exist "%DOCS%\GameForgeAnimationAssetGatherer" rmdir /S /Q "%DOCS%\GameForgeAnimationAssetGatherer"
if exist "%DOCS%\GameForgeAutoImportedAssets" rmdir /S /Q "%DOCS%\GameForgeAutoImportedAssets"

echo Reset complete.
pause
