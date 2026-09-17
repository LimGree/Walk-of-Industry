# GhostTint

**Файл:** `Assets/Script/Core/Buildings/GhostTint.cs`  
Шейдер: `Assets/Resources/WalkToBiomeGhost.shader`

Полупрозрачный призрак постройки. С каждого исходного материала копируется albedo (`_MainTex` / `_BaseMap`), все слои `sharedMaterials` красятся тинтом valid/invalid. Текстурные куски больше не остаются глухим Opaque.

Вешается на экземпляр призрака из [[BuildingVisuals]]. [[PlayerBuilder]] / [[BuildSelectionController]] вызывают `GhostTint.Apply`. Стрелки [[SocketArrow]] не перекрашиваются.

Связи: [[BuildingVisuals]] · [[PlayerBuilder]] · [[RuntimeMaterials]]
