# Новый рецепт

Рецепт — это ScriptableObject. Моделей у него нет: иконка в UI берётся у выходного предмета.

Сначала должны существовать все предметы входа / выхода и здание-станок.

## Что подготовить

1. Item Data на каждый вход и выход
2. Building Data станка (`smelter`, `constructor`, `assembler`, `refinery`, …)
3. Recipe Data в `Assets/ScriptableObjects/recipes/`

## 1. Создать ассет

`Create → Builderment → Recipe Data`  
Имя файла как `id`: `recipe_steel_ingot.asset`.

| Поле | Что писать |
|---|---|
| `id` | `recipe_<предмет>`, без пробелов |
| `displayName` | имя в карточке станка |
| `inputs` | предмет + количество на один цикл |
| `outputs` | предмет + количество |
| `craftTime` | секунды одного цикла |
| `requiredBuilding` | Building Data станка |
| `allowedBuildingIds` | строковый `id` станка: `smelter`, `constructor`, `assembler` |

`allowedBuildingIds` важнее `requiredBuilding`. Если список не пустой, рецепт покажется только станкам с этими `id`.

Если оба поля пустые, рецепт всплывёт **на всех** крафтерах. Так не оставляй.

После заполнения `requiredBuilding` Unity сама допишет его `id` в `allowedBuildingIds` (OnValidate). Проверь, что там нет мусора и пробелов.

## 2. Куда добавить ссылку

1. `Assets/Resources/GameDatabase.asset` → массив **Recipes**.
2. Разблокировка — одно из двух:
   - сразу: `ResearchSystem` в `SampleScene` → **Starting Recipes**;
   - после узла: у исследования **Unlocked Recipes**.

Без GameDatabase станок рецепт не покажет.  
Без starting / research карточки не будет даже на правильном станке (`IsRecipeUnlocked`).

## 3. Как станок его видит

`CrafterBuilding` + `MachineUI` берут `GameDatabase.AllRecipes()`, фильтруют:

1. `recipe.AllowsBuilding(это здание)`
2. исследование уже открыло рецепт

Отдельный список рецептов на префабе станка не нужен.

Текущие `id` станков:

| Станок | `id` |
|---|---|
| Плавильня | `smelter` |
| Конструктор | `constructor` |
| Сборщик | `assembler` |
| Нефтезавод | `refinery` |

Новый крафтер: его `Building Data.id` пиши в `allowedBuildingIds` у рецептов.

## Пример

Стальной слиток:

- вход: 2× `iron_ingot`, 1× `coal_ore`
- выход: 1× `steel_ingot`
- время: 3
- станок: `smelter`
- исследование: `research_advanced_automation`

## Чеклист

- [ ] все Item Data входа / выхода уже в GameDatabase
- [ ] Recipe Data, `id` без пробелов
- [ ] inputs / outputs / craftTime заполнены
- [ ] `requiredBuilding` + `allowedBuildingIds` указывают на нужный станок
- [ ] запись в `GameDatabase.recipes`
- [ ] starting **или** `unlockedRecipes` у исследования

## Частые ошибки

- Рецепт есть в папке, но не в GameDatabase.
- Рецепт в GameDatabase, но ни starting, ни research — в станке пусто.
- `allowedBuildingIds: assembler`, а ставишь в конструктор.
- Пробел в `id` (`recipe_gear `) — поиск по строке не найдёт.
- Слоты inputs ссылаются на Missing Item — цикл не стартанёт.
- Выходной предмет без иконки — карточка рецепта без картинки.
