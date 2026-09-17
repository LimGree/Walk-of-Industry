# GameAudio

**Файл:** `Assets/Script/Core/Audio/GameAudio.cs`

Единая колонка звука. Клипы лежат в `Resources/Audio/<папка>/` и грузятся по ключу вроде `ui/ui_click`.

## Шины громкости (PlayerPrefs `AudioVol_*`)

master, ui, world, buildings, music, ambient, player — слайдеры в настройках.

## Источники

- UI one-shot
- музыка меню/паузы (loop)
- **два** амбиент-источника: на границе биомов кроссфейд, клип меняется только после затухания
- дождь — отдельный loop `ambient/amb_rain`; гром — `world/world_thunder`
- лупы зданий 3D, тихие (`LoopVolume = 0.12`), с короткой задержкой выключения, чтобы не трещать

## Полезные вызовы

`Ui`, `UiHover`, `World`, `Player`, `Loop(здание, ключ, on)`, `PlayMusic`, `SetPaused`, `AddMixerSliders`.

Нет клипа — тишина, ошибки нет. Часть задуманных файлов (рука, лента) в папке может отсутствовать.
