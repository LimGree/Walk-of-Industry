# Модели зданий (low-poly, генератор)

**Код:**
- `Assets/Script/Core/Buildings/ModelLibrary.cs` — загрузка OBJ из `Resources/Models` и подмена материалов на `Resources/Models/Materials/wi_*.mat`;
- `Assets/Script/Core/Buildings/BuildingRestyle.cs` — подмена старых моделей в экземплярах зданий, порты, ленты и трубы.

**Файлы:**
- `Assets/Resources/Models/*.obj` + `wi.mtl` — модели, которые берёт игра;
- `Assets/Resources/Models/Materials/wi_*.mat` — палитра (Standard; `wi_lamp`, `wi_fire`, `wi_red`, `wi_blue` светятся);
- `Assets/models/Builders/generated/` — копии OBJ для Blender и `source/` с генератором (`wi_lib.py` — примитивы и рендер иконок, `wi_models.py` — все модели, `wi_install.py` — установка в проект);
- `Assets/images/Builder_icon/gen/*.png` — иконки зданий (прописаны в [[BuildingData]] и в узлах исследований);
- старые FBX удалены; префабы в `prefabs/Builders` содержат новые OBJ напрямую (вложенные экземпляры моделей из `Resources/Models`, материалы привязаны через `externalObjects` в `.meta` OBJ).

## Префабы

В префабах лежит `WiVisual/WiModel` (у сборщика и экстрактора — держатели `Assembler_Level_1/2`, `extractor_level_1/2` с моделями уровней), у лент — `BeltVisual/WiBelt/belt_straight`, у форм лент — `WiBelt/<вариант>`. Корневой BoxCollider добавлен там, где его раньше давала модель. `BuildingRestyle.Apply` видит `WiVisual` и только достраивает порты. Перезапись префабов: `generated/source/rebake_prefabs.py`.

## Как работает подмена (для префабов без WiVisual)

Иерархия префабов не меняется. В `BuildingVisuals.ApplyPlaced` (поставили или загрузили) и `PrepareGhostInstance` (призрак) вызывается `BuildingRestyle.Apply`:

1. Снимаются `Renderer`/`MeshFilter` старой модели. Сокеты, стрелки I/O, груз на ленте, значки поломки и коллайдеры остаются.
2. Новая модель вешается в тот же объект, выравнивается по корню здания (масштаб и поворот старого FBX не мешают).
3. У зданий с уровнями (сборщик, экстрактор) `assembler_1/2`, `extractor_1/2` кладутся внутрь старых `Level1/Level2` (`Assembler_Level_1`, `extractor_level_1`…), поэтому апгрейд переключает их как раньше.
4. **Порты:** на каждый реальный сокет ставится `port_in` (медная рамка, жёлтый шеврон внутрь), `port_out` (стальная рамка, зелёный шеврон наружу) или `port_fluid` (фланец). Стоят на стороне сокета и всегда совпадают с логикой. У НПЗ порты пересобираются при смене рецепта (`RefreshPorts`).

Модели нет в Resources — остаётся старая. Выключить подмену целиком: `BuildingRestyle.Enabled = false`.

## Ленты и трубы

`Conveyor.ApplyVisual` → `BuildingRestyle.SyncBelt`. На каждую форму и направление входа своя модель в осях ленты (выход +Z):

`belt_straight`, `belt_corner_l/r`, `belt_tee_l/r`, `belt_sides`, `belt_triple` (у труб то же с префиксом `pipe_`).

Доворот и зеркало формы из `BeltRules.GetVisual` снимаются: шевроны всегда смотрят по ходу груза. Фильтр ленты красит края — рельсы с материалом `wi_edge` (`PaintBeltEdges`, по индексу материала). Без фильтра они бирюзовые.

У трубы нет префабов T/бока/тройник: `Conveyor.CreateChildVisual` делает пустой держатель, модель `pipe_tee_*`/`pipe_sides`/`pipe_triple` подставляется туда же. Слияние труб работает, как у лент; делит поток [[PipeSplitter]]. Полотно на высоте 0.18, груз едет на 0.35, как раньше. Сплиттер ниже 0.29 — `ResolveItemHeight` груз не поднимает.

## Как поменять модель

- **В Blender:** открыть `generated/<имя>.obj`, поправить, экспортировать OBJ с тем же именем в `Assets/Resources/Models/` (материалы по именам `wi_*`, объекты `Arrows` и `Prop_*` не переименовывать).
- **Кодом:** поправить `wi_models.py` и запустить `python wi_install.py "<проект>" <партия>`. Партии: 0 — дроны, 1 — печь, сборщик, экстрактор, склад; 2 — ленты, трубы, сплиттер, подземка, рука; 3 — конструктор, химзавод, НПЗ, лаборатория; 4 — нефтекачалка, водокачка, бак, генератор; 5 — трубный сплиттер.
