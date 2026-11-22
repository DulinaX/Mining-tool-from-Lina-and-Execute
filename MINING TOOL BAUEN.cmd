@echo off
title Mining Tool - App bauen
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\bauen-weitergabe.ps1"
echo.
pause
