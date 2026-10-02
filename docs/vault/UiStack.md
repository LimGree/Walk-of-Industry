# UiStack

**Файл:** `Assets/Script/Core/UI/UiStack.cs`

Стек игровых окон и единый шлюз ввода.

## Окна в стеке

| id | окно | панель |
|---|---|---|
| bag | сумка [[InventoryUI]] | 55 |
| shop | магазин [[WalletHud]] | 80 |
| selection | [[SelectionActionsUI]] | 85 |
| blueprints | [[BlueprintLibraryUI]] | 86 |
| drones | [[DroneStationUI]] | 87 |
| repair | [[RepairUI]] | 88 |
| map | полная карта [[WorldMapUI]] | 95 |
| build | [[BuildMenuUI]] (сейчас не открывается) | 100 |
| machine | [[MachineUI]] / исследования [[ResearchUI]] | 110 |

Окно зовёт `Opened(id, close, baseOrder)` при открытии и `Closed(id)` при закрытии. Панель открытого окна получает порядок `300 + 4·место` — последнее открытое рисуется сверху (ниже паузы 500 и модалки 900).

## Правила

- **Esc** ([[GameManager]]): модалка → перетаскивание миникарты → верхнее окно (`CloseTop`) → пауза.
- **Горячая клавиша окна** (`Hotkey`): окно сверху — закрыть; открыто ниже — поднять; закрыто — открыть поверх.
- **Пауза** закрывает все окна (`CloseAll`), смена сцены обнуляет стек.
- `GameplayBlocked` — любое окно, модалка, пауза, консоль, ввод текста, модальный шаг обучения. Тогда выключено всё: камера, ходьба, зум, фонарик, E, СКМ, стройка, выделение, хотбар. Используют [[PlayerBuilder]], [[BuildSelectionController]], [[PlayerInventory]], [[PlayerMovement]], [[PlayerInteractor]], [[BuildingPicker]].
- `HotkeysBlocked` — то же без окон (окна друг другу не мешают).

## Каркас окна

`IndustryUi.OverlayPanel` строит окно из кода в стиле [[SettingsHub]]: шапка (иконка, заголовок, подзаголовок `WindowSubtitle`, «Esc — закрыть»), тело, подвал `WindowHints`. Плюс `SideNav`, `Section`, `Empty`. Стили — секции `WINDOWS` и `WINDOW SIZES` в `Industry.uss` (`win-narrow`, `win-medium`, `win-auto`).

Иконки в карточках рецептов, фильтров и книги рецептов — `IndustryUi.Well` (круглая подложка, как в сумке). Дерево исследований и связи между узлами — на токенах игры (`--success`, `--accent`), без старой серо-синей палитры. Экран загрузки — карточка меню со статусом, процентом и случайным советом (`load.tip1..6`).
