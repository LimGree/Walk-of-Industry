# Новый предмет

Нужны иконка, модель для ленты и Item Data. Потом запись в каталог.

## Что подготовить

1. **Иконка** — PNG в `Assets/images/item_icons/`
2. **Модель** — `.fbx` в `Assets/models/items/`
3. **Item Data** — `Assets/ScriptableObjects/items/`

Пример: стальной слиток — `steel_ingot.png` + `steel_ingot.fbx` + `steel_ingot.asset`.

Отдельный игровой префаб в `Assets/prefabs/` для обычного предмета не нужен. На конвейер инстансится `worldPrefab` (корень FBX).

## 1. Иконка

Положи PNG в `Assets/images/item_icons/`.

Inspector:

- Texture Type = **Sprite (2D and UI)**
- Alpha Is Transparency
- Sprite Mode **Multiple**, если на картинке поля / фон — вырежи сам предмет
- Sprite Mode **Single**, если уже квадрат без полей

В Item Data тащи именно **спрайт** (`steel_ingot_0`), не текстуру целиком.

## 2. Модель

FBX → `Assets/models/items/`.

Pivot по центру предмета, размер примерно как у соседних слитков / стержней — иначе на ленте будет гигант или точка.

В Item Data в `worldPrefab` перетащи сам FBX (корневой объект модели). Так сделаны железо, шестерня, сталь.

На модели не должно быть своей логики и лишних коллайдеров.

## 3. Item Data

`Create → Builderment → Item Data`  
Сохранить в `Assets/ScriptableObjects/items/`.

| Поле | Что писать |
|---|---|
| `id` | уникальный: `iron_ingot`, `steel_rod`, `crude_oil` |
| `displayName` | имя в UI и на карте |
| `description` | коротко |
| `icon` | спрайт из `item_icons` |
| `worldPrefab` | FBX модели |
| `maxStack` | обычно `100`, жидкости можно больше |
| `isFuel` / `fuelValue` | топливо (если появится генератор) |
| `isFluid` | нефть / вода: крафтеры с ленты не примут, только трубы |

`id` без пробелов. Уже существующие `recipe_gear ` со пробелом — так не делать.

## 4. Куда добавить ссылку

`Assets/Resources/GameDatabase.asset` → массив **Items**.

Этого достаточно, чтобы предмет существовал в игре.

Дальше по роли:

| Роль | Что ещё сделать |
|---|---|
| Крафт | рецепт, см. [ADDING_RECIPES.md](ADDING_RECIPES.md) |
| Добыча из земли | повесь этот Item Data на `ResourceNode` жилы (или на префаб жилы в `Assets/prefabs/resourses/`) |
| Жидкость | `isFluid = true`, рецепт на нефтевышку / нефтезавод |
| Награда / цена исследования | слот в `requiredItems` у узла |

Предметы не кладутся в `startingBuildings`. Стартовый крафт открывается через рецепт.

## Чеклист

- [ ] PNG-иконка, импорт как Sprite
- [ ] FBX в `Assets/models/items/`
- [ ] Item Data с `id`, иконкой, `worldPrefab`
- [ ] запись в `GameDatabase.items`
- [ ] если крафтится — есть рецепт и он разблокирован
- [ ] если жила — Item Data стоит на `ResourceNode`

## Частые ошибки

- В `icon` перетащили Texture, а не Sprite — в UI пусто.
- Забыли GameDatabase — предмет не находится по `id`, рецепт с Missing reference в рантайме.
- Нет `worldPrefab` — на ленте слот пустой.
- `isFluid` не включили у нефти — её начнут возить конвейеры.
- Сменил `id` у уже используемого предмета — сломаются сохранения и ссылки в рецептах.
