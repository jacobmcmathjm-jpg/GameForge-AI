@echo off
title GameForge AI Engine v3.0.0 - Fast Start
cd /d "%~dp0"
npm run start:stable
if %errorlevel% neq 0 pause
