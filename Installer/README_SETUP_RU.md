# Создание установщика KarateTournamentApp

## Выбранный способ

Используется обычный Windows Setup.exe на базе Inno Setup. WPF-приложение публикуется как **self-contained для win-x64**: .NET 10 Desktop Runtime и зависимости включаются в каталог публикации, а затем в установщик.

Поэтому на компьютере организатора **не нужно отдельно устанавливать .NET 10 Desktop Runtime**, а Setup.exe не должен скачивать Runtime из интернета.

В установщик включается WPF-приложение `KarateTournamentApp` и его зависимости, включая `KarateClassLibrary` и EPPlus. Отдельный `KarateTournament.Api` и PostgreSQL не включаются: текущая WPF-точка входа не запускает API и не подключается к PostgreSQL.

## Требования для СОЗДАНИЯ установщика

На компьютере разработчика:

1. Windows x64.
2. .NET 10 SDK (не только Runtime): https://dotnet.microsoft.com/download/dotnet/10.0
3. Inno Setup 7 (рекомендуется) или Inno Setup 6: https://jrsoftware.org/isdl.php
4. Доступ к NuGet.org для восстановления пакета EPPlus.

Установка Inno Setup 7 через winget:

```powershell
winget install --id JRSoftware.InnoSetup.7 -e -s winget -i
```

Официальная страница загрузки: https://jrsoftware.org/isdl.php

## Создание Setup.exe

Открой PowerShell в корневой папке проекта `KarateTournamentApp`, где находятся папки `KarateTournamentApp`, `KarateClassLibrary` и `Installer`.

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\Installer\Build-Installer.ps1 -Version 1.0.0.0
```

Важно: сначала меняется ExecutionPolicy для текущего окна, затем запускается скрипт. Ещё проще — запустить двойным щелчком `Installer\Build-Installer.cmd`: этот ASCII-именованный файл сам запускает Windows PowerShell с `-ExecutionPolicy Bypass` и передаёт ему все аргументы.

Для другой версии можно запустить из PowerShell:

```powershell
.\Installer\Build-Installer.ps1 -Version 1.0.0.1
```

Или из командной строки:

```cmd
Installer\Build-Installer.cmd -Version 1.0.0.1
```

Скрипт:

- проверит наличие .NET 10 SDK и Inno Setup Compiler;
- восстановит WPF-проект только из nuget.org (частный GitLab feed не используется этим приложением);
- опубликует приложение в режиме self-contained для `win-x64`;
- соберёт русскоязычный Setup.exe.

Готовый файл появится здесь:

```text
Installer\Output\KarateTournamentApp_Setup_1.0.0.0_x64.exe
```

Для следующего выпуска увеличивай версию, например до `1.0.0.1`.

## Что требуется на компьютере ПОЛЬЗОВАТЕЛЯ

- Windows x64, совместимая с используемым .NET 10.
- Отдельная установка .NET 10 не нужна: Runtime включён в приложение.
- Права администратора не требуются: по умолчанию программа устанавливается в `%LOCALAPPDATA%\Programs\KarateTournamentApp`.
- Интернет для установки не требуется, если Setup.exe уже передан пользователю.

**Важно по Windows 10:** по актуальной таблице Microsoft .NET 10 для Windows 10 указан для редакций Enterprise/LTSC; Windows 11 поддерживается. Self-contained включает Runtime, но не расширяет список поддерживаемых операционных систем. На обычных редакциях Windows 10 (Home/Pro) нужно отдельно проверить запуск на целевом компьютере до распространения.

## Обновление существующей установки

С тем же `AppId` и увеличенной версией запусти новый Setup.exe на уже установленной машине. Установщик обновит файлы программы в той же папке. Перед обновлением приложение нужно закрыть; Inno Setup попросит закрыть работающую программу.

Файлы турниров, которые пользователь сохраняет через диалоги импорта/экспорта в JSON/Excel, остаются в выбранных пользователем местах и не удаляются обновлением или удалением программы.

Это обычный офлайн-установщик, а не ClickOnce: автоматической проверки обновлений при каждом запуске нет. Чтобы обновить приложение, нужно передать и запустить новый Setup.exe.

## Подпись установщика

Сценарий собирает неподписанный Setup.exe. Для публичного распространения рекомендуется подписывать установщик сертификатом Authenticode. Самоподписанный сертификат не создаёт общедоверенную подпись и не устраняет предупреждения SmartScreen для сторонних пользователей.

## Проверка результата

После сборки проверь:

1. Установи Setup.exe на чистую Windows x64-машину без .NET 10 Desktop Runtime.
2. Запусти приложение.
3. Проверь импорт Excel, жеребьёвку, табло и экспорт/импорт JSON.
4. Установи Setup.exe более новой версии поверх старой и проверь, что программа обновилась, а файлы турниров остались на месте.

## Лицензионное замечание

В текущем проекте используется EPPlus 7.0.0. Для EPPlus 5/6/7 действует Polyform Noncommercial либо коммерческая лицензия. Если приложение будет использоваться в коммерческой организации или в коммерческом продукте, до распространения необходимо проверить применимость лицензии EPPlus: https://www.epplussoftware.com/en/LicenseOverview/

Inno Setup также указывает требование коммерческой лицензии при коммерческом использовании. Проверь текущие условия на официальной странице загрузки перед коммерческой сборкой: https://jrsoftware.org/isdl.php


### Если Inno Setup установлен, но сборщик его не находит

Найди компилятор:

```powershell
where.exe ISCC.exe
Get-ChildItem 'C:\Program Files*', $env:LOCALAPPDATA\Programs, $env:LOCALAPPDATA\Microsoft\WinGet\Packages -Filter ISCC.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
```

Если команда покажет путь, передай его явно:

```powershell
.\Installer\Build-Installer.ps1 -Version 1.0.0.0 -InnoSetupCompilerPath 'C:\полный\путь\ISCC.exe'
```

Скрипт также ищет Inno Setup через PATH, записи удаления Windows, обычные каталоги Program Files и каталог WinGet.
