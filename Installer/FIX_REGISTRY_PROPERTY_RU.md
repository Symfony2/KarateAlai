# Исправление поиска Inno Setup в реестре

Скрипт больше не предполагает, что каждый раздел Uninstall содержит свойства `DisplayName`, `InstallLocation` и `UninstallString`. Такие записи встречаются в реестре Windows, поэтому под `Set-StrictMode -Version Latest` прямой доступ к отсутствующему свойству приводил к `PropertyNotFoundException`.

Теперь свойства проверяются через `PSObject.Properties`; отсутствующие поля заменяются пустой строкой, а неполные записи безопасно пропускаются. Это исправление рассчитано на Windows PowerShell 5.1 и PowerShell 7.
