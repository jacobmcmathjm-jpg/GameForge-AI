@echo off
title GameForge AI Engine v0.8.5 - One Click Launcher
color 0A
echo =====================================================
echo GameForge AI Engine v0.8.5
echo One-Click Launcher
echo =====================================================
echo.

where node >nul 2>nul
if %errorlevel% neq 0 (
  echo Node.js is not installed.
  echo.
  echo Please install Node.js LTS from:
  echo https://nodejs.org/
  echo.
  echo After installing Node.js, double-click this file again.
  pause
  exit /b 1
)

where npm >nul 2>nul
if %errorlevel% neq 0 (
  echo npm was not found. Reinstall Node.js LTS and make sure npm is included.
  pause
  exit /b 1
)

if not exist "node_modules" (
  echo First-time setup detected.
  echo Installing GameForge dependencies. This may take a few minutes...
  echo.
  npm install
  if %errorlevel% neq 0 (
    echo.
    echo Dependency install failed. Check the error above.
    pause
    exit /b 1
  )
)

echo.
echo Starting GameForge in stable window mode...
echo.
npx electron . --disable-gpu --disable-gpu-compositing --disable-accelerated-2d-canvas --disable-features=CalculateNativeWinOcclusion,VizDisplayCompositor
pause