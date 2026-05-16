@echo off
cd /d "%~dp0"
where code >nul 2>nul
if errorlevel 1 (
  echo Visual Studio Code command "code" not found.
  echo Open VS Code manually and choose File > Open Folder > this GF folder.
  pause
  exit /b 1
)
code .
