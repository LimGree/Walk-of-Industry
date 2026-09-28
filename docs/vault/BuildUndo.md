# BuildUndo

**Файл:** `Assets/Script/Core/Save/BuildUndo.cs`

Отмена стройки: Ctrl+Z. Повтор: Ctrl+Y. До 50 шагов, файл `worlds/<id>/undo.json` — переживает сессию (стек undo и redo).

Шаги: постановка, снос, перенос, поворот. Монеты откатываются вместе с действием. Новое действие очищает redo.

Связи: [[BuildSelectionController]] · [[PlayerBuilder]] · [[SaveSystem]] · [[WorldCatalog]]
