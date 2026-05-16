@echo off
title GameForge AI Engine v3.0.0 - Direct Start
cd /d "%~dp0"
call npx electron . --disable-gpu --disable-gpu-compositing --disable-accelerated-2d-canvas --disable-features=VizDisplayCompositor
if %errorlevel% neq 0 pause
