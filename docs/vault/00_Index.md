# Walk of Industry — карта проекта

Как открыть в Obsidian: [[Как_открыть]]

Это оглавление «под капотом». Каждый файл — одна деталь игры. Клик по `[[Имя]]` открывает карточку.

Игра — фабрика от первого лица: ходишь по миру, ставишь здания на сетку, предметы едут по конвейерам, исследования открывают рецепты, монеты тратятся на постройки.

**С чего начать, если всё перепуталось:** [[Архитектура]] → [[GameManager]] → [[PlayerBuilder]] → [[BuildingBase]] → [[SaveSystem]]

Сценарий первого входа (пока без кода): [[Обучение]]

Текущая альфа для тестеров: [[Альфа]] (сейчас **0.1.0-alpha.6**, сборка через [[AlphaBuild]]).

---

## Как устроены сцены

- [[MainMenu]] — главное меню (миры, настройки)
- [[LoadingScreen]] — экран загрузки между меню и игрой
- [[GameManager]] — «дирижёр» игровой сцены, пауза, подключение систем
- Сцены Unity: `Assets/Scenes/MainMenu.unity`, `Loading.unity`, `SampleScene.unity` (сама игра)

Код: `Assets/Script/Core/` — папки по смыслу (не свалка Systems):

- `Audio/` · `Buildings/` · `Data/` · `Economy/` · `Editor/` · `Grid/` · `Items/` · `Logistics/` · `Map/` · `Player/` · `Research/` · `Save/` · `Settings/` · `UI/` · `World/`
- `GameManager.cs` лежит в корне Core.

Связи сцен и префабов идут по GUID в `.meta`, не по пути. Скрипт переезжает только вместе со своим `.meta`.

---

## Игрок и строительство

- [[PlayerMovement]] — ходьба, камера, прыжок, зум
- [[PlayerBuilder]] — режим стройки: поставить / снести / линия конвейеров (при входе в мир выключен)
- [[PlayerInventory]] — хотбар зданий
- [[PlayerInteractor]] — кнопка «взаимодействие» по взгляду, интерфейс [[IInteractable]]
- [[BuildSelectionController]] — выделение, копирование, вставка, перенос группы зданий
- [[BuildGridVisualizer]] — сетка под ногами в режиме стройки (`BuildGridPlane`, не белый квадрат)
- [[BuildIoArrowVisualizer]] — выключен; стрелки I/O только на префабах ([[SocketArrow]])

---

## Здания и производство

- [[BuildingBase]] — общий предок всех зданий
- [[CrafterBuilding]] — общий предок станков, которые крафтят по рецепту
- [[BuildingSocket]] / [[SocketType]] — «розетки» входа и выхода
- [[BuildingLinker]] — кто с кем соседствует на сетке
- [[BuildingData]] — карточка здания в каталоге (цена, префаб, размер)
- [[BuildingPrefabLayout]] — +Z выход, −Z вход; гост и уровни в одном префабе
- [[BuildingVisuals]] · [[GhostTint]] — призрак с albedo и прозрачностью
- [[SocketArrow]] — стрелки I/O на префабе; в игре только в режиме стройки

Станки-крафтеры (почти близнецы, разный звук/модель):

- [[Assembler]] · [[Constructor]] · [[Smelter]] · [[ChemicalPlant]] · [[Refinery]]

Добыча:

- [[Extractor]] — руда/дерево/камень с жилы [[ResourceNode]]
- [[OilExtractor]] · [[WaterExtractor]]

Логистика:

- [[BeltRules]] — таблица форм и путей ленты
- [[Conveyor]] · [[UndergroundConveyor]] · [[Pipe]] (труба = конвейер только для жидкостей)
- [[Splitter]] · [[RoboticArm]]
- [[StorageContainer]] · [[FluidStorageTank]]
- [[BeltItemView]] — как предмет выглядит на ленте
- [[BeltSpeedSystem]] — общая скорость всех лент (прокачка в лаборатории)

Прочее:

- [[ResearchLab]] — сдача предметов в исследования и продажа
- [[PowerGenerator]] — заготовка под топливо (логика питания зданий ещё не доведена)
- [[ResourceNode]] — жила ресурса в мире

---

## Сетка мира и ландшафт

- [[GridSystem]] — клетка ↔ метры
- [[GridFootprint]] — сколько клеток занимает здание
- [[GridOccupancy]] — кто стоит в какой клетке
- [[WorldBiomeMap]] / [[WorldBiome]] — лес, поле, горы, вода (океан за горизонт, ходить по нему нельзя; озёра можно)
- [[WorldResourceScatterer]] — расстановка жил и деревьев
- [[WorldSim]] — тик лент и кулл зданий 32 м
- [[DevConsole]] — тройное Ё, команды `/`
- Полный разбор генерации (биомы, скопления, жилы) — `docs/Генерация_биомов_и_жил.md`
- Спека переделки — `Генерация_биомов_и_жил_v2.md` на рабочем столе; в коде уже фазы A/B из неё
- [[WorldView]] — радиус прогрузки картинки (логика зданий не гасится)
- [[ResourceNodePrefabs]] — какие модели жил использовать
- [[RuntimeMaterials]] — материалы, которые точно есть в билде
- [[DayNight]] / [[DayNightCycle]] — сутки, рассвет и закат
- [[Weather]] / [[WeatherCycle]] — ясно, дождь, гроза

---

## Экономика, исследования, сохранения

- [[Economy]] — цены продажи, стоимость лент, замедление крафта
- [[PlayerWallet]] — монеты и рубины
- [[ResearchSystem]] — дерево исследований
- [[ResearchTree]] — граф в окне лаборатории (зум колесом, ПКМ — панорама)
- [[ResearchNodeData]] · [[RecipeData]] · [[ItemData]] · [[ItemStack]] · [[ItemInstance]]
- [[GameDatabase]] — каталог всего (здания, предметы, рецепты, исследования)
- [[ProductionStats]] — сколько чего произведено
- [[SaveSystem]] · [[SaveData]] — запись мира в JSON
- [[WorldCatalog]] — список миров на диске

---

## Карта

- [[WorldMapUI]] — миникарта и полная карта
- [[MapMarkerSystem]] — метки и голограммы
- [[MapExploration]] — исследованные / неисследованные клетки
- [[MapSettings]] · [[MapSettingsUI]]
- [[MapIcons]] — стрелка игрока на карте

---

## Настройки и управление

- [[SettingsHub]] — вкладки настроек
- [[SettingsControls]] — тумблеры, слайдеры, чипы
- [[GameSettings]] — свет, небо, туман, дальность объектов 16–140 м, разрешение, FPS (PlayerPrefs)
- [[KeybindStore]] · [[KeybindSettingsUI]] · [[InputSystem_Actions]]
- [[IndustryPause]] — меню паузы
- [[UiLocale]] — русский / английский

---

## Интерфейс

- [[IndustryUi]] — фабрика кнопок и панелей UI Toolkit
- [[UiRuntime]] · [[UiTheme]] · [[UiFactory]] · [[UiMotion]]
- [[UiModal]] · [[UiNotification]] · [[UiTooltip]] · [[UiAudio]] · [[UiStatus]]
- [[BuildMenuUI]] · [[InventoryUI]] · [[MachineUI]] · [[ResearchUI]] · [[ResearchTree]] · [[RecipeCodex]]
- [[BuildingPrefabLayout]] · [[SocketArrow]]
- [[OverlayUi]] · [[WalletHud]] · [[InputHintUI]] · [[CrosshairHud]]
- [[SelectionActionsUI]] · [[LoadingScreen]] · [[HotbarSlotView]] · [[BagBuildingCard]]
- [[GameHudIcons]] · [[GameBranding]]

---

## Звук

- [[GameAudio]] — клипы из `Resources/Audio/`, микшер, амбиент биомов

---

## Редактор и сборка (не часть игры для игрока)

- [[AlphaBuild]] — сборка Windows-альфы из меню Unity
- [[BuildingFootprintEditor]] — подсказки footprint в Scene View

---

## Дизайн без кода

- [[Обучение]] — сценарий туториала первого мира
- [[TutorialSystem]] · [[TutorialUI]] · [[TutorialFx]] — шаги, HUD/панель, подсветка жил и зданий

---

## Данные и папки без кода

- [[Папки_и_ассеты]] — модели, иконки, рецепты, USS, сцены, звуки

---

## Как читать карточки

В каждой карточке:

1. **Зачем нужен** — одной фразой
2. **Связи** — кто с кем дружит (`[[ссылки]]`)
3. **Поля** — что помнит объект
4. **Методы** — что делает, по шагам, простым языком
