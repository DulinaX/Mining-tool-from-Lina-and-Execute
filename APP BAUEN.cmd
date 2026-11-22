@echo off
title Exi und Lina Mining Tool - App bauen
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\bauen-einfach.ps1"
echo.
pause
