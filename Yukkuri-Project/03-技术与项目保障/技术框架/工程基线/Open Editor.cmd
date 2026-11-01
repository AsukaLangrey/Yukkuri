@echo off
powershell.exe -NoProfile -File "%~dp0tools\open-editor.ps1"
if errorlevel 1 pause
