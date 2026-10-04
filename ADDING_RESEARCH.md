# Новое исследование

Исследование — узел дерева. У него иконка, цена предметами, предки и награды (здания / рецепты).

Сами награды должны уже существовать как Building Data / Recipe Data.

## Что подготовить

1. **Иконка** — PNG в `Assets/images/Researches/`
2. **Research Node** — `Assets/ScriptableObjects/Research/`
3. Готовые здания и рецепты, которые узел открывает

## 1. Иконка

PNG → `Assets/images/Researches/`.

Texture Type = **Sprite (2D and UI)**. Для квадратной иконки достаточно Sprite Mode **Single**.

## 2. Research Node

`Create → Builderment → Research Node`  
Сохранить в `Assets/ScriptableObjects/Research/`.

| Поле | Что писать |
|---|---|
| `id` | `research_<тема>`, уникальный |
| `displayName` | имя в дереве и в лаборатории |
| `description` | что даёт узел |
| `icon` | спрайт |
| `requiredItems` | что сдать в лабораторию: предмет + количество |
| `requiredResearches` | узлы, которые должны быть уже завершены |
| `unlockedBuildings` | здания, которые появятся в строительстве |
| `unlockedRecipes` | рецепты, которые появятся в станках |

Пустые награды допустимы только если узел чисто «промежуточный». Обычно хотя бы одно здание или рецепт.

Текущее дерево (для `requiredResearches`), главная ветка:

```
research_smelter (100 жел. + 100 мед. руды → плавильня + оба слитка)
  └── research_assembler (150 жел. слитков)
        ├── research_iron_rod ── research_gear ──┬── research_constructor (+ research_wire)
        │                                        ├── research_power_generator ── reliability_1/2
        │                                        └── research_underground_conveyor «Логистика» (подземка + сплиттер)
        ├── research_iron_plate ── research_steel_ingot ── steel_beam / steel_rod
        ├── research_wire / research_cooper_plate / research_plank / research_stone_brick
        └── research_silicon ── research_glass
research_constructor + silicon + wire ── research_circuit_board ── assembler_2 «Сборщик Mk2», extractor_2, oil_extractor
steel_rod + wire ── research_motor ── drones…, robotic_arm (+ storage)
oil_extractor → water_extractor → pipe → fluid_tank → refinery → plastic / rubber / sulfuric_acid → chemical_plant → battery
computer_chip + cable → advanced_circuit / nano_wire → quantum_core → ai_module
```

Полный граф с глубинами — в игре: **Карта производства (K)**; проверка данных — меню **Walk of Industry → Validate Content**.

Убраны (лежат в `_unused_assets/Assets/Data/Research/`): старое дерево `Basic Automation`, `mechanical_engineering`, `electronics`, `advanced_automation`, `petrochemistry`, `computing`, `ai_systems`, а также `research_iron_ingot`, `research_cooper_ingot` (влиты в `research_smelter`) и `research_splitter` (сплиттер — в «Логистике»). Старые сейвы: `ResearchSystem.ApplyRemovedResearch` отдаёт то, что эти узлы давали.

Новый узел вешай на того предка, после которого он должен открыться. Несколько предков = все должны быть завершены.

## 3. Куда добавить ссылку

1. `Assets/Resources/GameDatabase.asset` → массив **Researches**.  
   Дерево в UI читает этот список (`ResearchSystem.GetAllNodes`).
2. Награды уже стоят на самом узле (`unlockedBuildings` / `unlockedRecipes`). Отдельно в starting их дублировать не надо — иначе они будут доступны до исследования.

`ResearchSystem.allResearchNodes` в `SampleScene` — запасной список. Новый узел достаточно добавить в GameDatabase.

`startingBuildings` / `startingRecipes` на том же объекте — только то, что игрок имеет на старте (лента, экстрактор, доски и т.п.).

## 4. Как это открывается в игре

1. Предки узла завершены.
2. В лаборатории выбираешь узел.
3. На ленту / в приём лаборатории сдаёшь `requiredItems`.
4. Когда всё сдано — узел завершается, здания падают в хотбар / меню, рецепты появляются в станках.

Лимит лабораторий увеличивается, если `id` узла прописан в `ResearchSystem.extraLabSlotResearchIds`. Сейчас это `research_extractor_2` и `research_assembler_2`. Новому узлу это не нужно, если ты не хочешь ещё одну лабораторию.

## Чеклист

- [ ] иконка-спрайт
- [ ] Research Node, `id` уникален
- [ ] цена `requiredItems` ссылается на живые Item Data
- [ ] предки в `requiredResearches` (или пусто, если это корень)
- [ ] награды: здания и/или рецепты
- [ ] запись в `GameDatabase.researches`
- [ ] награды **не** продублированы в starting, если их нельзя иметь сразу

## Частые ошибки

- Узел в папке, но не в GameDatabase — в дереве его нет.
- Рецепт/здание в наградах, но не в GameDatabase — после исследования всё равно не находится.
- Рецепт в наградах, здание станка — нет: рецепт откроется, ставить будет некуда.
- Забыли предка — узел доступен сразу с старта.
- Сломали цепочку: новый узел требует узел, который сам требует этот новый.
- Поменяли `id` у существующего исследования — сейвы и `extraLabSlotResearchIds` разъедутся.
