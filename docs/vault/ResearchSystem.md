# ResearchSystem

**Файл:** `Assets/Script/Core/Research/ResearchSystem.cs`

Дерево технологий. Живой список узлов — `GameDatabase.researches` ([[GameDatabase]]), сцена дублирует его в `allResearchNodes`.

## Старт

`startingBuildings`: extractor, conveyor, research_lab. Стартовых рецептов нет.

Лимит лабораторий: `baseLabLimit` = 1, потолок 3. +1 слот за `research_extractor_2` и `research_assembler_2`.

## Цикл

1. Как только предки открыты, узел **сам активен**. Выбирать «текущее» не нужно. Доступных может быть несколько сразу.
2. Предмет с ленты / `SubmitItem` копится **во все** активные узлы, которым он нужен. Набрали стоимость — узел открыт.
3. Узел даёт здания (`unlockedBuildings`) и рецепты (`unlockedRecipes`). При живом завершении — тост под балансом [[UiNotification]] и рубины. [[DevConsole]] может `UnlockBuilding` / `UnlockRecipe` / `CompleteResearch` без тоста.

Предмет, засчитанный в исследование, всё равно продаётся за монеты. Если он никому из активных не нужен — шестерёнки идут в скорость лент, остальное продаётся.

Старые паки (`research_basic_automation` и т.д.) в папке Research не в дереве. Старые сейвы с их id не мапятся — новая игра.

Цены и граф — лист «Стоимость лабы» в рабочем xlsx. Первая глава: `research_smelter` (140 iron_ore + 140 cooper_ore) → плавильня. Слитки — отдельные узлы после неё.

## Связи

[[ResearchLab]] · [[ResearchUI]] · [[MachineUI]] · [[ResearchTree]] · [[ResearchNodeData]] · [[PlayerWallet]] · [[BeltSpeedSystem]] · [[Economy]] · [[SaveData]]
