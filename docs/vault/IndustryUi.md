# IndustryUi

**Файл:** `Assets/Script/Core/UI/IndustryUi.cs`

Кухня интерфейса. Все окна игры — UI Toolkit, не старый uGUI.

## Что делает

- вешает `UIDocument` на объект, грузит `PanelSettings` с темой (`Resources/UI/IndustryPanel` + `IndustryTheme.tss`) и шрифт из `Resources/UI/`;
- `Btn`, `Text`, `El`, карточки рецептов/зданий/исследований;
- `Show` / `SetButtonLabel` / `SetOn` / `Money`;
- прячет старые Canvas, если они ещё торчат на префабах.

Без этого класса каждое меню писало бы стили руками. Связанные: [[UiTheme]], [[UiRuntime]], [[UiFactory]], почти все `*UI`.
