# WorldView

**Файл:** `Assets/Script/Core/World/WorldView.cs`  
**Тип:** статический

Кэш позиции игрока и радиуса прогрузки картинки ([[GameSettings.RenderDistance]]).

`InRange(world)` — видна ли точка на земле. Этим пользуются [[BuildingBase]] (модели/коллайдеры), [[WorldResourceScatterer]], ленты (только визуал груза), [[GameAudio]] (лупы зданий).

Симуляция зданий сюда **не** завязана.
