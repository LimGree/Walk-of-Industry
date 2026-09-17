# BuildingLinker

**Файл:** `Assets/Script/Core/Logistics/BuildingLinker.cs`  
**Тип:** статический класс (объекта на сцене нет)

## Зачем нужен

Кто стоит в клетке и кто кого кормит.

Лента: соседство + `ExitDir`. Станок: клетка сокета (`GetSocketCell`) / направление выхода. Лента сокетов не имеет — станок отдаёт ей предмет, если клетка перед выходом занята лентой.

## Связи

[[BuildingBase]] · [[BuildingSocket]] · [[Conveyor]] · [[BeltRules]] · [[GridOccupancy]] · [[GridSystem]]

## Важные идеи

- `SuppressRelink` — во время загрузки мира здания появляются пачкой. Загрузчик ставит флаг, потом один раз `RelinkAll`.
- `RelinkAround` — пересчитать маски лент вокруг здания (worklist 3×3) и сокеты станков.
- `GetBuildingAt` — кто в клетке.
- `GetSocketCell` — клетка позиции сокета.
- `FeedsInto` — у ленты по `ExitDir`, у станка по выходу.
- `HasInputFrom` / `AcceptsFromCell` — вход смотрит на источник.
- `WorldToCell` — обёртка над сеткой.
