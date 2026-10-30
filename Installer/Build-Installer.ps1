[CmdletBinding()]
param(
    [Parameter()]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string]$Version = '1.0.0.0',

    [Parameter()]
    [ValidateSet('win-x64')]
    [string]$RuntimeIdentifier = 'win-x64',

    [Parameter()]
    [switch]$SkipRestore,

    [Parameter()]
    [string]$InnoSetupCompilerPath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$installerDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $installerDir
$appProject = Join-Path $repoRoot 'KarateTournamentApp\KarateTournamentApp.csproj'
$issFile = Join-Path $installerDir 'KarateTournamentApp.iss'
$publishDir = Join-Path $installerDir 'publish\win-x64'
$outputDir = Join-Path $installerDir 'Output'
$nugetSource = 'https://api.nuget.org/v3/index.json'

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [Parameter(Mandatory = $true)][string]$Description
    )

    Write-Host "`n==> $Description" -ForegroundColor Cyan
    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Description завершилось с кодом $LASTEXITCODE."
    }
}

if (-not (Test-Path $appProject)) {
    throw "Не найден WPF-проект: $appProject. Запусти скрипт из распакованной папки проекта."
}
if (-not (Test-Path $issFile)) {
    throw "Не найден сценарий Inno Setup: $issFile"
}

$dotnet = Get-Command 'dotnet' -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw 'Не найден dotnet. Установи .NET 10 SDK и перезапусти PowerShell.'
}

$sdkList = & $dotnet.Source --list-sdks
if (-not ($sdkList | Where-Object { $_ -match '^10\.' })) {
    throw "Для сборки требуется .NET 10 SDK. Установленные SDK:`n$($sdkList -join "`n")`nЗагрузка: https://dotnet.microsoft.com/download/dotnet/10.0"
}

# Wrap the FILTERED pipeline in @(...). Without this outer array wrapper,
# PowerShell unwraps a single result into a scalar string. Under StrictMode,
# that string has no .Count property and the installer build stops here.
# Locate Inno Setup compiler. Winget can install Inno Setup under a path
# that differs from the classic Program Files locations, so check PATH,
# registry uninstall records, Winget links/packages and standard folders.
$isccCandidates = [System.Collections.Generic.List[string]]::new()

if (-not [string]::IsNullOrWhiteSpace($InnoSetupCompilerPath)) {
    $isccCandidates.Add($InnoSetupCompilerPath.Trim('"'))
}

$isccCommand = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
if ($isccCommand -and $isccCommand.Source) {
    $isccCandidates.Add($isccCommand.Source)
}

$standardCompilerPaths = @(
    (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Links\ISCC.exe')
)
foreach ($candidate in $standardCompilerPaths) {
    if ($candidate) { $isccCandidates.Add($candidate) }
}

# Find install locations registered by Inno Setup (including per-user installs).
$uninstallRoots = @(
    'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall',
    'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
)
foreach ($root in $uninstallRoots) {
    if (Test-Path -LiteralPath $root) {
        foreach ($entry in (Get-ChildItem -LiteralPath $root -ErrorAction SilentlyContinue)) {
            $app = Get-ItemProperty -LiteralPath $entry.PSPath -ErrorAction SilentlyContinue
            if ($null -eq $app) {
                continue
            }

            # Registry uninstall keys are not uniform: many applications do not
            # define DisplayName, InstallLocation or UninstallString. Under
            # StrictMode, direct access to a missing property throws. Read the
            # properties defensively and ignore incomplete/non-Inno entries.
            $displayNameProperty = $app.PSObject.Properties['DisplayName']
            $installLocationProperty = $app.PSObject.Properties['InstallLocation']
            $uninstallStringProperty = $app.PSObject.Properties['UninstallString']

            $displayName = if ($null -ne $displayNameProperty) { [string]$displayNameProperty.Value } else { '' }
            if ($displayName -notmatch 'Inno Setup') {
                continue
            }

            $installLocation = if ($null -ne $installLocationProperty) { [string]$installLocationProperty.Value } else { '' }
            if (-not [string]::IsNullOrWhiteSpace($installLocation)) {
                $isccCandidates.Add((Join-Path $installLocation 'ISCC.exe'))
            }

            $uninstallString = if ($null -ne $uninstallStringProperty) { [string]$uninstallStringProperty.Value } else { '' }
            if (-not [string]::IsNullOrWhiteSpace($uninstallString)) {
                $uninstallExe = [regex]::Match($uninstallString, '"(?<path>[^"]+\.exe)"')
                if ($uninstallExe.Success) {
                    $installDir = Split-Path -Parent $uninstallExe.Groups['path'].Value
                    $isccCandidates.Add((Join-Path $installDir 'ISCC.exe'))
                }
            }
        }
    }
}

# Last-resort targeted search in common Inno Setup / WinGet installation roots.
$searchRoots = @(
    (Join-Path $env:ProgramFiles 'Inno Setup 7'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 7'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 7'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6'),
    (Join-Path $env:LOCALAPPDATA 'Microsoft\WinGet\Packages')
)
foreach ($root in $searchRoots) {
    if ($root -and (Test-Path -LiteralPath $root -PathType Container)) {
        $found = Get-ChildItem -LiteralPath $root -Filter 'ISCC.exe' -File -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 5 -ExpandProperty FullName
        foreach ($path in @($found)) {
            if (-not [string]::IsNullOrWhiteSpace([string]$path)) {
                $isccCandidates.Add([string]$path)
            }
        }
    }
}

# Remove duplicates and keep only paths that point to a real file.
$validIsccCandidates = [string[]]@(
    $isccCandidates |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and (Test-Path -LiteralPath $_ -PathType Leaf) } |
        Select-Object -Unique
)

if ($validIsccCandidates.Length -gt 0) {
    $isccPath = $validIsccCandidates[0]
}
else {
    $searched = $standardCompilerPaths -join "`n  "
    throw @"
Не найден Inno Setup Compiler (ISCC.exe).
Winget сообщает, что пакет установлен, но исполняемый файл не найден в стандартных местах.
Проверь расположение командами:
  where.exe ISCC.exe
  winget list --id JRSoftware.InnoSetup.7
  Get-ChildItem 'C:\Program Files*', `$env:LOCALAPPDATA\Programs, `$env:LOCALAPPDATA\Microsoft\WinGet\Packages -Filter ISCC.exe -Recurse -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName
Если найдёшь ISCC.exe, передай его полный путь:
  .\Installer\Build-Installer.ps1 -Version $Version -InnoSetupCompilerPath 'C:\полный\путь\ISCC.exe'
Проверены стандартные пути:
  $searched
"@
}
Write-Host 'Проект:      ' $appProject
Write-Host 'Версия:      ' $Version
Write-Host 'Платформа:   ' $RuntimeIdentifier
Write-Host 'ISCC:        ' $isccPath
Write-Host 'Режим:       Self-contained (.NET 10 включается в публикацию)'

if (-not $SkipRestore) {
    # Desktop app depends on public EPPlus and the local KarateClassLibrary project.
    # Restricting the source avoids unnecessary requests to a private GitLab feed
    # that this WPF app does not reference.
    Invoke-Checked -FilePath $dotnet.Source -Arguments @(
        'restore', $appProject,
        '--runtime', $RuntimeIdentifier,
        '--source', $nugetSource
    ) -Description 'Восстановление NuGet-пакетов WPF-приложения'
}

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

Invoke-Checked -FilePath $dotnet.Source -Arguments @(
    'publish', $appProject,
    '--configuration', 'Release',
    '--runtime', $RuntimeIdentifier,
    '--self-contained', 'true',
    '--no-restore',
    '--output', $publishDir,
    "-p:Version=$Version",
    '-p:PublishSingleFile=false',
    '-p:DebugType=None',
    '-p:DebugSymbols=false'
) -Description 'Публикация WPF-приложения вместе с .NET 10 Runtime'

$exePath = Join-Path $publishDir 'KarateTournamentApp.exe'
if (-not (Test-Path $exePath)) {
    throw "После публикации не найден исполняемый файл: $exePath"
}

Push-Location $installerDir
try {
    Invoke-Checked -FilePath $isccPath -Arguments @(
        "/DMyAppVersion=$Version",
        $issFile
    ) -Description 'Создание установщика Setup.exe'
}
finally {
    Pop-Location
}

$setupFile = Join-Path $outputDir "KarateTournamentApp_Setup_${Version}_x64.exe"
if (-not (Test-Path $setupFile)) {
    throw "Компилятор завершился, но установщик не найден: $setupFile"
}

$sizeMb = [Math]::Round((Get-Item $setupFile).Length / 1MB, 1)
Write-Host "`nУстановщик готов: $setupFile" -ForegroundColor Green
Write-Host "Размер: $sizeMb MB"
Write-Host 'Для установки на компьютере пользователя .NET 10 отдельно не требуется: runtime включён в приложение.' -ForegroundColor Green
