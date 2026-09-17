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

Текущее дерево (для `requiredResearches`):

```
research_basic_automation
        └── research_mechanical_engineering
                    └── research_electronics
                                ├── research_advanced_automation
                                │           └── research_petrochemistry
                                └── research_computing
                                    (ещё требует research_petrochemistry)
                                            └── research_ai_systems
```

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

Лимит лабораторий увеличивается, если `id` узла прописан в `ResearchSystem.extraLabSlotResearchIds`. Сейчас это `research_advanced_automation` и `research_petrochemistry`. Новому узлу это не нужно, если ты не хочешь ещё одну лабораторию.

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
