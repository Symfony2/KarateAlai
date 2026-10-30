@echo off
setlocal
set "VERSION=%~1"
if "%VERSION%"=="" set "VERSION=1.0.0.0"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build-Installer.ps1" -Version "%VERSION%"
set "EXIT_CODE=%ERRORLEVEL%"
echo.
if not "%EXIT_CODE%"=="0" (
  echo Сборка установщика завершилась с ошибкой. См. сообщения выше.
) else (
  echo Готово. Установщик находится в папке Output.
)
pause
exit /b %EXIT_CODE%
