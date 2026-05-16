@echo off
title GameForge AI Engine v0.8.4 - Safe Mode
echo Starting GameForge in GPU-safe mode...
echo.
npm start -- --disable-gpu --disable-gpu-compositing --disable-accelerated-2d-canvas
pause