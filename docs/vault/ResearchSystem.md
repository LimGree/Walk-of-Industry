# ResearchSystem

**Файл:** `Assets/Script/Core/Research/ResearchSystem.cs`

Дерево технологий. Живой список узлов — `GameDatabase.researches` ([[GameDatabase]]), сцена дублирует его в `allResearchNodes`.

## Старт

`startingBuildings`: extractor, conveyor, research_lab. Стартовых рецептов нет.

Лимит лабораторий: `baseLabLimit` = 1, потолок 3. +1 слот за `research_extractor_2` и `research_assembler_2`.

## Цикл

1. Как только предки открыты, узел **сам активен**. Выбирать «текущее» не нужно. Доступных может быть несколько сразу.
2. Предмет с ленты / `SubmitItem` копится **во все** активные узлы, которым он нужен. Набрали стоимость — узел открыт.
3. Узел даёт здания (`unlockedBuildings`) и рецепты (`unlockedRecipes`). При живом завершении — тост под балансом [[UiNotification]] и рубины. [[DevConsole]] может `UnlockBuilding` / `UnlockRecipe` / `CompleteResearch` без тоста. `/research skipall` гоняет `CompleteAllResearch`: события UI копятся до конца цикла, чтобы дерево не падало на середине.
4. `research_underground_conveyor` («Логистика») открывает и подземку, и [[Splitter]]. Вместе со сплиттером открывается фильтр на ленте (`BeltFilterUnlocked`).

Предмет, засчитанный в исследование, всё равно продаётся за монеты. Если он никому из активных не нужен — шестерёнки идут в скорость лент, остальное продаётся.

Старые паки (`Basic Automation`, `mechanical_engineering`, `electronics`, `advanced_automation`, `petrochemistry`, `computing`, `ai_systems`), а также `research_iron_ingot`, `research_cooper_ingot` и `research_splitter` убраны в `_unused_assets/Assets/Data/Research/`. Старые сейвы: `ApplyRemovedResearch` отдаёт то, что эти узлы давали (слитки — теперь в `research_smelter`, сплиттер — в `research_underground_conveyor` «Логистика»).

Цены и граф — лист «Стоимость лабы» в рабочем xlsx и `ADDING_RESEARCH.md`. Первая глава: `research_smelter` (100 iron_ore + 100 cooper_ore) → плавильня **и сразу оба рецепта слитков** — станок не стоит «мёртвым» в обучении. Полный граф с глубинами — в игре на [[ProductionMapUI]] (K); проверка данных — [[ContentValidator]].

В окне [[ResearchTree]] у узла есть «Ведёт к: …» и переход на карту производства по предмету цены.

## Связи

[[ResearchLab]] · [[ResearchUI]] · [[MachineUI]] · [[ResearchTree]] · [[ResearchNodeData]] · [[PlayerWallet]] · [[BeltSpeedSystem]] · [[Economy]] · [[SaveData]]

Цена сданного предмета — `LabMarket.Sell` ([[LabMarket]]): от сложности производства, с насыщением рынка; дробные монеты копятся. Монеты идут с источником `LabSale` ([[EconomyLedger]]).
