# UiLook

**Файл:** `Assets/Script/Core/UI/UiLook.cs`

Каждый корень `IndustryUi.Mount` регистрируется здесь и получает классы темы: `accent-blue/green/red`, `anim-reduced/anim-off`, `hi-contrast`, `cb-protan/deutan/tritan`. Переопределения токенов — секция `UI LOOK` в `Industry.uss`.

`RegisterHud(el)` — элемент берёт прозрачность из «Прозрачность HUD» (баланс [[WalletHud]], подсказки [[InputHintUI]], миникарта [[WorldMapUI]]; хотбар [[InventoryUI]] считает сам).

`Good` / `Bad` / `Warn` — цвета статусов для кода с учётом режима дальтоников.
