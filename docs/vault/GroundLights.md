# GroundLights — свет огней на земле

**Файлы:** `Assets/Script/Core/World/GroundLights.cs`, шейдер `Assets/Resources/WalkToBiomeGroundLit.shader`.

Земля — тайлы карты биомов ([[WorldBiomeMap]], `BiomeOverlay`) на неосвещаемом шейдере: текстура × оттенок дня/ночи. Фонари декораций, огонь печей и фонарик игрока её не освещали, а в прямом рендере на огромный тайл давалось лишь несколько попиксельных огней.

`GroundLights` (на `BiomeOverlay`) раз в секунду находит точечные и прожекторные `Light`, каждый кадр берёт до 32 включённых и ближайших к камере и отдаёт их глобальными массивами (`_WalkPointPos/Color/Dir`, `_WalkPointCount`) шейдеру `Hidden/WalkToBiome/GroundLit`. Шейдер — как Unlit (туман, оттенок мира), плюс свет этих огней с мягким затуханием до `range`.
