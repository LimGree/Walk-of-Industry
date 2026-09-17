# AlphaBuild

**Файл:** `Assets/Editor/AlphaBuild.cs`  
**Только в редакторе Unity** (`#if UNITY_EDITOR`). В игровой сборке этого кода нет.

Пункт меню `Walk of Industry / Build Windows Alpha`. Собирает Windows 64-bit в `Builds/Windows/WalkOfIndustry-<версия>/`.

Текущая версия игрока: `PlayerSettings.bundleVersion` в `ProjectSettings/ProjectSettings.asset` (сейчас **0.1.0-alpha.6**). Папка билда берёт её как суффикс.

Умеет сработать по файлу-триггеру `Temp/request-windows-alpha-build.txt` (для внешних скриптов). Пишет статус в `Builds/Windows/last-build-status.txt`.

Не часть геймплея.
