@echo off
title GameForge AI Engine v0.8.4 - CPU Mode
echo Starting GameForge with GPU disabled...
echo.
npx electron . --disable-gpu --disable-software-rasterizer --disable-gpu-compositing
pause