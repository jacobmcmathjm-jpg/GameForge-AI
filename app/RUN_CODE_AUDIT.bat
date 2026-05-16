@echo off
title GameForge v3.0.0 Code Audit
cd /d "%~dp0"
echo Running GameForge code audit...
where node >nul 2>nul
if %errorlevel% neq 0 (
  echo Node.js not found. Install Node.js LTS first.
  pause
  exit /b 1
)
for %%F in (main.js preload.js src\app.js src\ai\pipelineRegistry.js src\ai\safePipeline.js) do (
  echo Checking %%F
  node --check "%%F"
  if %errorlevel% neq 0 (
    echo Syntax check failed on %%F
    pause
    exit /b 1
  )
)
echo Audit complete. Core files passed syntax checks.
pause
