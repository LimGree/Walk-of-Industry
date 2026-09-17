# Как добавлять контент

Пошаговые гайды по типам:

- [Здания](ADDING_BUILDINGS.md) — модель, гост, префаб, иконка, Building Data
- [Предметы](ADDING_ITEMS.md) — модель, иконка, Item Data
- [Рецепты](ADDING_RECIPES.md) — Recipe Data и привязка к станку
- [Исследования](ADDING_RESEARCH.md) — узел дерева, цена, награды

Общее правило: ассет сам по себе ничего не включает. Его нужно положить в каталог и разблокировать.

## Каталог

`Assets/Resources/GameDatabase.asset` — единственный обязательный список.

| Тип | Поле в GameDatabase |
|---|---|
| Здание | `buildings` |
| Предмет | `items` |
| Рецепт | `recipes` |
| Исследование | `researches` |

Если записи нет в этом ассете, игра её не видит: нет в меню строительства, нет в списке рецептов станка, нет в дереве исследований.

## Разблокировка

Каталог ≠ доступ в новой игре.

| Что | Сразу в новой игре | После исследования |
|---|---|---|
| Здание | `ResearchSystem.startingBuildings` в `SampleScene` | `unlockedBuildings` у узла |
| Рецепт | `ResearchSystem.startingRecipes` в `SampleScene` | `unlockedRecipes` у узла |

Предметы не разблокируются отдельно. Они появляются, когда их добыли или скрафтили.

## id

Латиница, `snake_case`, без пробелов в начале и конце.

Примеры: `smelter`, `steel_ingot`, `recipe_steel_rod`, `research_advanced_automation`.

`id` должен быть уникален в своём типе. Сохранение зданий идёт по `id` — не меняй его у уже существующих ассетов.

## Где что лежит

| Что | Папка |
|---|---|
| Модели зданий, госты | `Assets/models/Builders/` |
| Модели предметов | `Assets/models/items/` |
| Игровые префабы зданий | `Assets/prefabs/Builders/` |
| Иконки зданий | `Assets/images/Builder_icon/` |
| Иконки предметов | `Assets/images/item_icons/` |
| Иконки исследований | `Assets/images/Researches/` |
| Building Data | `Assets/ScriptableObjects/Builders/` |
| Item Data | `Assets/ScriptableObjects/items/` |
| Recipe Data | `Assets/ScriptableObjects/recipes/` |
| Research Node | `Assets/ScriptableObjects/Research/` |
| Каталог | `Assets/Resources/GameDatabase.asset` |
