@echo off
REM Build a verified indie game by default. Explicit arguments select other modes.
cd /d "%~dp0"
if exist ".venv\Scripts\activate.bat" (
  call .venv\Scripts\activate.bat
)
if "%~1"=="" (
  python main.py --build-indie --export --output "..\generated\Lanternfall_%RANDOM%_%RANDOM%"
) else (
  python main.py %*
)
pause
