# RecipeData

**Файл:** `Assets/Script/Core/Data/RecipeData.cs`

Рецепт: список входов [[ItemStack]], список выходов, `craftTime` в секундах, какое здание имеет право его варить (`requiredBuilding` / `allowedBuildingIds`), какое исследование открывает.

Читает [[CrafterBuilding]]. Время на практике × [[Economy.CraftNeed]].
