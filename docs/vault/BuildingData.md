# BuildingData

**Файл:** `Assets/Script/Core/Data/BuildingData.cs`  
**Тип:** ScriptableObject (меню Create → Builderment → Building Data)

Карточка здания, не само здание на сцене.

## Поля

- `id`, имя, описание, иконка
- `prefab` / `ghostPrefab` — что ставить и какой призрак
- `cornerPrefab` / `cornerGhostPrefab` — угол ленты
- `teePrefab` / `teeGhostPrefab` — T (сзади + один бок, `conveer_4_5`)
- `sidesPrefab` / `sidesGhostPrefab` — два бока без зада (`conveer_6`)
- `triplePrefab` / `tripleGhostPrefab` — три входа (`conveer_3`)
- `isPipe` — труба
- `size` — клетки footprint
- `buildCost`
- `requiresResourceNode` / `allowOnWater` / `requiresWater`

Живые экземпляры на сцене — префабы с [[BuildingBase]].
