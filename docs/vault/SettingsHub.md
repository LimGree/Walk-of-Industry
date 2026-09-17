# SettingsHub

**Файл:** `Assets/Script/Core/UI/SettingsHub.cs`

Рисует экран настроек с **вкладками без перезагрузки**:

Общие · Управление · Звук · Карта · Миникарта

Его зовут [[MainMenu]] и [[IndustryPause]].

## Общие

Язык, подсказки, свет, **дальность объектов** (руды/деревья/здания, 16–140 м), туман от 1 м, яркость, сутки ([[DayNight]]), погода ([[Weather]]: ясно / дождь / гроза), качество, разрешение, режим окна, V-Sync, FPS, кнопка папки миров ([[WorldCatalog.WorldsFolder]]). Подробности слайдеров — [[GameSettings]].

## Остальные вкладки

- Управление → [[KeybindSettingsUI]]
- Звук → [[GameAudio.AddMixerSliders]]
- Карта → [[MapSettingsUI.FillWorld]]
- Миникарта → [[MapSettingsUI.FillMinimap]]

Кнопки вкладок — чипы [[SettingsControls]].
