# WorldCatalog

**Файл:** `Assets/Script/Core/World/WorldCatalog.cs`  
**Тип:** статический

Список миров на диске компьютера игрока, не в сцене.

Папка: `Application.persistentDataPath/worlds/` (у Windows это AppData). Один раз умеет перенести старые сейвы из папки «Walk to biome».

## Типы

- `WorldInfo` — id, имя, сид, даты
- `WorldIndex` — список в `index.json`

## Методы

- `ListWorlds` / `CreateWorld` / `DeleteWorld` / `SetActive`
- `SavePath` / `ActiveSavePath` / `WorldsFolder` — последнюю открывает кнопка в [[SettingsHub]]

[[MainMenu]] рисует карточки отсюда. [[SaveSystem]] пишет в активный мир.
