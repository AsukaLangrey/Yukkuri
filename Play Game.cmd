@echo off
cd /d "%~dp0"
if exist "builds\playtest\M01-CLANG-20261027-074048\Play This PC.cmd" (
  call "builds\playtest\M01-CLANG-20261027-074048\Play This PC.cmd"
  exit /b
)
if not exist "builds\windows\Yukkuri.exe" (
  echo Windows build is missing. See README.txt.
  pause
  exit /b 1
)
start "" "builds\windows\Yukkuri.exe"
