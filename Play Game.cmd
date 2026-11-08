@echo off
cd /d "%~dp0"
if exist "builds\playtest\M01-CLANG-20261027-074048\Play This PC.cmd" (
  call "builds\playtest\M01-CLANG-20261027-074048\Play This PC.cmd"
  exit /b
)
if not exist "builds\windows\Yukkuri.exe" (
  echo Windows build is missing.
  if exist "README.txt" echo See README.txt.
  if exist "README.md" echo See README.md.
  if exist "Play Current.cmd" echo Run Play Current.cmd to play from source.
  pause
  exit /b 1
)
start "" "builds\windows\Yukkuri.exe"
