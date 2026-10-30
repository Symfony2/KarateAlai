# Исправление сборщика установщика

Исправлен поиск `ISCC.exe` в `Build-Installer.ps1`. В Windows PowerShell результат конвейера `Where-Object` при единственном совпадении разворачивается в скалярную строку. При `Set-StrictMode -Version Latest` обращение к `$isccCandidates.Count` в таком случае вызывает `PropertyNotFoundException`.

Теперь отфильтрованный результат дополнительно обёрнут в `@(...)`, поэтому переменная всегда является массивом: 0, 1 или несколько путей. Остальная логика сборки не менялась.

Запуск из корня распакованного проекта:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\Installer\Build-Installer.ps1 -Version 1.0.0.0
```
