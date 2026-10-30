@echo off
setlocal
title KarateTournamentApp Installer Builder
set "SCRIPT=%~dp0Build-Installer.ps1"
if not exist "%SCRIPT%" (
  echo ERROR: Build-Installer.ps1 was not found.
  pause
  exit /b 1
)
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT%" %*
set "RC=%ERRORLEVEL%"
if not "%RC%"=="0" (
  echo.
  echo Installer build failed with exit code %RC%.
  pause
)
exit /b %RC%
