# GridSystem

**Файл:** `Assets/Script/Core/Grid/GridSystem.cs`

## Зачем нужен

Перевод «метры в мире Unity» ↔ «клетка (x, z)». По умолчанию клетка = 1 метр.

При старте чистит [[GridOccupancy]] и вешает на себя визуализатор сетки, стрелки IO, [[WorldBiomeMap]], [[WorldResourceScatterer]].

## Методы

- `WorldToCell` — точка мира → индексы клетки
- `CellToWorld` / `GetCellCenter` — обратно, центр клетки
- `SnapToGrid` — прилипить точку к узлу сетки

Все строители и карта ходят сюда, а не считают `Round` кто во что горазд. Исключение: [[BuildingLinker.WorldToCell]] — тонкая обёртка.
