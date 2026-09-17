# Extractor

**Файл:** `Assets/Script/Core/Buildings/Extractor.cs`  
**Предок:** [[BuildingBase]] + [[IInteractable]]

## Зачем нужен

Качает ресурс из [[ResourceNode]] в той же клетке: руда, камень, дерево, песок…

Каждый `CurrentInterval` секунд (интервал × [[Economy.ExtractTimeMul]]) пытается забрать 1 единицу у жилы и вытолкнуть в выход.

Уровень 2: более частый цикл (`upgradedExtractInterval`) и другая модель `extractor_level_2`. Кнопка апгрейда только после `research_extractor_2`.

## Методы

### BindToNearbyNode
Ищет жилу в своей клетке. Нет жилы — стоит и молчит.

### Update
Если есть ресурс и место на выходе — тикает таймер, `TryConsume` у жилы, `TryOutputToAny`. Луп звука `bld_extractor_loop`.

### TryUpgradeBuilding
Уровень 2, сброс таймера, смена видимой модели.

### Interact
Открывает [[MachineUI]].
