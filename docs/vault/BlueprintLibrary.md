# BlueprintLibrary

**Файл:** `Assets/Script/Core/World/BlueprintLibrary.cs`

Именованные копии групп зданий. Не один буфер Ctrl+C: каждый чертёж — папка с `blueprint.json` и `icon.png` (снимок сверху).

## Где лежит

- Этот мир: `worlds/<id>/blueprints/<guid>/`
- Все миры: `persistentDataPath/blueprints/<guid>/`

Вкладки независимы: сохранение и удаление только в открытой.

В JSON: смещения клеток, yaw, уровень, рецепт, фильтр ленты/руки, пары подземки.

Снимок: ортокамера сверху по AABB выделенных зданий. Игрок на кадр прячется.

Связи: [[BlueprintLibraryUI]] · [[BuildSelectionController]] · [[WorldCatalog]]
