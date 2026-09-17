# ResearchNodeData

**Файл:** `Assets/Script/Core/Data/ResearchNodeData.cs`

Узел дерева: `id`, имя, `requiredItems` (сдача в лабу), `requiredResearches` (предки), `unlockedBuildings` / `unlockedRecipes`, `rubyReward`.

Живое дерево — 40 узлов `research_*` в `Assets/ScriptableObjects/Research/`. Старые 7 паков в той же папке не подключены к [[GameDatabase]].

Апгрейды того же здания (не новый id): `research_extractor_2`, `research_assembler_2`.

Связи: [[ResearchSystem]] · [[ResearchTree]] · [[GameDatabase]]
