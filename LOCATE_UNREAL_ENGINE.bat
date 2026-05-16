@echo off
setlocal EnableExtensions
title GameForge Unreal Engine Locator

set "ROOT=%~dp0"
set "CONFIG=%ROOT%GameForge_Unreal_Path.txt"
set "LOG=%ROOT%Unreal_Detection_Log.txt"

echo GameForge Unreal Engine Locator > "%LOG%"
echo Started: %DATE% %TIME% >> "%LOG%"
echo. >> "%LOG%"

echo Searching for UnrealEditor.exe...

set "FOUND_EDITOR="
set "FOUND_UAT="

for %%D in (
  "C:\Program Files\Epic Games\UE_5.7"
  "C:\Program Files\Epic Games\UE_5.6"
  "C:\Program Files\Epic Games\UE_5.5"
  "C:\Program Files\Epic Games\UE_5.4"
  "D:\Epic Games\UE_5.7"
  "D:\Epic Games\UE_5.6"
  "D:\Epic Games\UE_5.5"
  "E:\Epic Games\UE_5.7"
  "E:\Epic Games\UE_5.6"
  "E:\Epic Games\UE_5.5"
) do (
  if exist "%%~D\Engine\Binaries\Win64\UnrealEditor.exe" (
    set "FOUND_EDITOR=%%~D\Engine\Binaries\Win64\UnrealEditor.exe"
    set "FOUND_UAT=%%~D\Engine\Build\BatchFiles\RunUAT.bat"
    goto found
  )
)

echo Default locations did not find Unreal.
echo.
echo Find UnrealEditor.exe manually.
echo Usually:
echo C:\Program Files\Epic Games\UE_5.7\Engine\Binaries\Win64\UnrealEditor.exe
echo.
set /p MANUAL="Paste FULL UnrealEditor.exe path: "

if not exist "%MANUAL%" (
  echo.
  echo ERROR: That file does not exist.
  pause
  exit /b 1
)

set "FOUND_EDITOR=%MANUAL%"
for %%I in ("%MANUAL%") do set "ENGINE_BIN=%%~dpI"
for %%I in ("%ENGINE_BIN%..\..\..") do set "ENGINE_ROOT=%%~fI"
set "FOUND_UAT=%ENGINE_ROOT%\Build\BatchFiles\RunUAT.bat"
goto found

:found
echo FOUND_EDITOR=%FOUND_EDITOR% > "%CONFIG%"
echo FOUND_UAT=%FOUND_UAT% >> "%CONFIG%"

echo.
echo Found UnrealEditor:
echo %FOUND_EDITOR%
echo.
echo Expected RunUAT:
echo %FOUND_UAT%
echo.
echo Saved to:
echo %CONFIG%
echo.
echo Restart GameForge and try Generate again.
pause
