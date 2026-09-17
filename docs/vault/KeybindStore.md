# KeybindStore

**Файл:** `Assets/Script/Core/Settings/KeybindStore.cs`

Единственный живой экземпляр [[InputSystem_Actions]] на всю игру. Все скрипты берут `KeybindStore.Shared`, а не создают свои — иначе переназначения не совпадут.

## Что хранит

JSON переназначений в PlayerPrefs `InputBindingOverrides`.

## Zoom

Действие `Player/Zoom` есть в `.inputactions`. Если в старом сгенерированном C# его ещё нет, `EnsureZoomAction` добавляет кнопку C на лету. Список биндов в настройках включает «Зум камеры».

## Методы

- `BuildEntries` — что показать в UI
- `StartRebind` — «нажмите новую клавишу», Esc отмена
- `ResetBinding` / `ResetAll`
- `Hint("Jump")` — текст для подсказок внизу экрана
- `BlocksGameplayInput` — пока слушаем новую клавишу **или в поле ввода текст** (метка, `UiModal`, [[DevConsole]]), хоткеи молчат. `SetPlayerMapEnabled` глушит карту Player, пока открыта консоль.

Связи: почти весь ввод. UI: [[KeybindSettingsUI]], [[InputHintUI]].
