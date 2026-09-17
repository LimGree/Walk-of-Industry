# BuildingVisuals

**Файл:** `Assets/Script/Core/Buildings/BuildingVisuals.cs`

Уровни модели (`Visual/Level1|Level2`), вложенный `Ghost`, стрелки I/O.

- Поставленное здание: гост выкл., стрелки по режиму стройки ([[SocketArrow]]).
- Призрак: берётся тот же префаб (`SourceForGhost`), включается вложенный `Ghost` если есть, иначе живая модель + [[GhostTint]].
- Стрелки код не создаёт — только `BindNamed` на уже поставленные `IoArrow_In` / `IoArrow_Out`.

Связи: [[BuildingPrefabLayout]] · [[GhostTint]] · [[SocketArrow]] · [[PlayerBuilder]]
