@echo off
title Mining Tool - Version zum Weitergeben bauen
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\bauen-weitergabe.ps1"
echo.
pause
