@echo off
setlocal EnableExtensions
title Repair GameForge AI

cd /d "%~dp0app"

echo Repairing GameForge dependencies...
where npm >nul 2>nul
if errorlevel 1 (
  echo Node.js/npm not found. Install Node.js LTS first.
  pause
  exit /b 10
)

echo Cleaning npm cache check...
npm cache verify

echo Reinstalling packages...
npm install
if errorlevel 1 (
  echo Repair failed. Send screenshot.
  pause
  exit /b 12
)

echo Running launch doctor...
node tools\launch-doctor.js

echo Repair complete. You can now run START_GAMEFORGE_FAST.vbs.
pause
