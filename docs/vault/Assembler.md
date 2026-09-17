# Assembler

**Файл:** `Assets/Script/Core/Buildings/Assembler.cs`  
**Предок:** [[CrafterBuilding]]

Сборщик. Крафтит сложные рецепты. Уровень 2 включает модель `Assembler_level_2` и скорость `upgradedCraftSpeed`. Кнопка апгрейда только после `research_assembler_2` (тот же `id`, не новое здание).

Коллайдер подгоняется под клетку, коллайдеры детских моделей выключаются (чтобы клик попадал в родителя).

Открывается через [[IInteractable]] → [[MachineUI]]. Звук работы задаёт базовый `WorkClip` у крафтера (`bld_assembler_loop`).

Похожие братья: [[Constructor]], [[Smelter]], [[ChemicalPlant]], [[Refinery]] — та же схема «найти named child уровня, апгрейд до 2».
