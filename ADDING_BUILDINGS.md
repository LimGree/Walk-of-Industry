# Новое здание

Нужны четыре визуала и один ScriptableObject, плюс запись в каталог и разблокировка.

## Что подготовить

1. **Модель** — `.fbx` в `Assets/models/Builders/`
2. **Гост-модель** — отдельный префаб только с мешами, без логики
3. **Игровой префаб** — в `Assets/prefabs/Builders/`
4. **Иконка** — PNG в `Assets/images/Builder_icon/`
5. **Building Data** — `Assets/ScriptableObjects/Builders/`

Пример уже в проекте: `Smelter` — `Smelter.fbx` → `Smelter_model.prefab` + `Smelter_model_ghost.prefab` → `Smelter_01.prefab` → `Smelter.asset`.

## 1. Модель

Положи FBX в `Assets/models/Builders/<имя>_from_blender/` (как у остальных).

Из модели сделай визуальный префаб (`*_model.prefab`) — его потом вложишь в игровой префаб. Pivot модели должен совпадать с центром footprint: клетка `1×1` — центр клетки, `2×2` — центр квадрата из четырёх клеток. Ось Y вверх, «лицо» станка лучше смотрит в `+Z`.

## 2. Гост

Скопируй визуальный префаб → `*_model_ghost.prefab`.

На госте:

- нет `BuildingBase` и наследников;
- нет рабочих сокетов / логики;
- коллайдеры выключены или сняты;
- масштаб и pivot как у боевого префаба.

В игре материал госта подменяется на зелёный / красный. Если `ghostPrefab` пустой, берётся боевой `prefab` — так делать не надо.

Для конвейера / трубы нужен ещё угловой гост: `cornerGhostPrefab`.

## 3. Иконка

PNG → `Assets/images/Builder_icon/`.

В Inspector:

- Texture Type = **Sprite (2D and UI)**
- если спрайт с полями — Sprite Mode **Multiple**, вырежи квадрат предмета
- если иконка уже обрезана — Sprite Mode **Single**

## 4. Игровой префаб

Создай префаб в `Assets/prefabs/Builders/`. На корне:

| Компонент | Зачем |
|---|---|
| Скрипт здания | наследник `BuildingBase` |
| Collider | клик, снос, взаимодействие |
| `BuildingSocket` на детях | вход / выход для лент |

Сокеты — пустые объекты на границе footprint (для `1×1` обычно `z = ±0.5`). Направление наружу считается по смещению от центра здания, не по `forward`.

В инспекторе скрипта заполни:

- `data` — тот же Building Data, который создашь ниже
- `inputSockets` / `outputSockets` — ссылки на детей

### Какой скрипт вешать

| Тип | Скрипт | База |
|---|---|---|
| Плавильня, конструктор, сборщик, нефтезавод | новый класс `: CrafterBuilding` | крафт по рецептам, UI станка |
| Склад | `StorageContainer` | твёрдые предметы |
| Резервуар | `FluidStorageTank` | жидкости, только трубы |
| Экстрактор | `Extractor` | только на жиле `ResourceNode` |
| Нефтевышка | `OilExtractor` | жила + жидкость |
| Хим. завод | `ChemicalPlant` | жидкости и твёрдые, сложная химия |
| Лаборатория | `ResearchLab` | дерево исследований |
| Рука | `RoboticArm` | перекладка |
| Конвейер | `Conveyor` | линия, прямой / угол |
| Подземный конвейер | `UndergroundConveyor` | вход+выход парой, только прямая |
| Труба | `Pipe` | как конвейер, только жидкости |
| Сплиттер | `splitter` | развилка ленты |

Обычный крафтер: новый файл в `Assets/Script/Core/Buildings/`, класс пустой наследник `CrafterBuilding` (как `Smelter` / `Constructor`). Рецепты подхватятся сами, если у них совпадёт `id` здания.

Новый тип с особой логикой — отдельный скрипт и правка `MachineUI`, если нужно своё окно.

## 5. Building Data

`Create → Builderment → Building Data`  
Сохранить в `Assets/ScriptableObjects/Builders/`.

| Поле | Что писать |
|---|---|
| `id` | уникальный, `snake_case`: `smelter`, `constructor` |
| `displayName` | имя в UI |
| `description` | коротко |
| `prefab` | игровой префаб из `Assets/prefabs/Builders/` |
| `ghostPrefab` | гост из `Assets/models/Builders/` |
| `icon` | спрайт иконки |
| `size` | клетки: X по миру X, Y по миру Z. Pivot = центр |
| `canRotate` | обычно включено |
| `cornerPrefab` | только конвейер / труба — угловой префаб |
| `cornerGhostPrefab` | гост угла |
| `isPipe` | труба |
| `requiresResourceNode` | ставить только на жилу |
| `allowOnWater` | можно на воду / океан |
| `requiresWater` | только на воду (все клетки footprint — озеро или океан) |

`id == "extractor"` и `id == "research_lab"` зашиты в коде (жилы и лимит лабораторий). Новым шахтёрам ставь `requiresResourceNode`, не копируй `id`.

## 6. Куда добавить ссылку

1. `Assets/Resources/GameDatabase.asset` → массив **Buildings**.
2. Разблокировка — одно из двух:
   - сразу доступно: объект `ResearchSystem` в `SampleScene` → **Starting Buildings**;
   - после узла: у исследования поле **Unlocked Buildings**.

Без пункта 1 здания нет в меню. Без пункта 2 карточка серая / поставить нельзя.

`PlayerInventory.allBuildings` в сцене — запасной список, если GameDatabase пустой. Новый контент туда дублировать не нужно.

## Чеклист

- [ ] FBX модели
- [ ] `*_model.prefab` и `*_model_ghost.prefab`
- [ ] игровой префаб: скрипт, коллайдер, сокеты, `data`
- [ ] иконка-спрайт
- [ ] Building Data, `id` уникален
- [ ] `prefab` + `ghostPrefab` + `icon` + `size` заполнены
- [ ] запись в `GameDatabase.buildings`
- [ ] starting **или** `unlockedBuildings` у исследования
- [ ] для крафтера есть рецепты с этим `id` в `allowedBuildingIds`

## Частые ошибки

- Забыли GameDatabase — здания нет в B-меню.
- Забыли starting / research — видно, но залочено.
- Pivot не в центре footprint — станок встаёт со сдвигом, сокеты не стыкуются с лентой.
- На госте живой `BuildingBase` — гост регистрируется на сетке.
- `size` не совпадает с моделью — соседние клетки перекрываются или остаются дыры.
- У крафтера нет сокетов — ленты не подключаются.
