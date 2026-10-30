# Исправление кодировки установщика

В `Build-Installer.ps1` добавлен UTF-8 BOM. Это необходимо для Windows PowerShell 5.1: UTF-8 без BOM читается им как системная ANSI-кодировка, из-за чего русские строки превращаются в `Р...` и скрипт может завершиться ParserError.

В `KarateTournamentApp.iss` также добавлен UTF-8 BOM, чтобы русские подписи установщика корректно читались компилятором Inno Setup.

Добавлен `Build-Installer.cmd` с ASCII-именем. Он сам вызывает Windows PowerShell с `-ExecutionPolicy Bypass`, поэтому отдельная команда `Set-ExecutionPolicy` не требуется.

## Запуск

Двойной щелчок по `Installer\Build-Installer.cmd`, либо из корня проекта:

```powershell
.\Installer\Build-Installer.ps1 -Version 1.0.0.0
```

Если запускаешь PS1 напрямую, при необходимости сначала выполни `Set-ExecutionPolicy -Scope Process Bypass -Force`.
