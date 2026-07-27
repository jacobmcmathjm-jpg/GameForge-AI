@echo off
REM Launch Jarvis autonomous daemon on Windows 11
cd /d "%~dp0"
if exist ".venv\Scripts\activate.bat" (
  call .venv\Scripts\activate.bat
)
python main.py %*
pause
