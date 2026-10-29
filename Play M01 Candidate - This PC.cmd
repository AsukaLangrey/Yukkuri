@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -File "%~dp0tools\launch-m01.ps1" -ExportDirectory builds/candidates/m01-clang-expression -LocalCompatibility
if errorlevel 1 pause

