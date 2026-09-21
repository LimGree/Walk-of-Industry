# PowerGenerator

**Файлы:** `Assets/Script/Core/Buildings/PowerGenerator.cs`, префаб `Assets/prefabs/Builders/PowerGenerator.prefab`, данные `PowerGenerator.asset`  
**Предок:** [[BuildingBase]]

Модель `Assets/models/Builders/Generator from blender/Generator.fbx`, иконка `Assets/images/Builder_icon/Generator.png`. Исследование `research_power_generator` после шестерёнок. Топливо — уголь, вход сзади.

## Зачем задуман

Сжигать топливо (`fuelItem`) и давать бонус скорости зданиям в радиусе.

## Что есть сейчас

- принимает только выбранное топливо;
- таймер горения `remainingFuelTime`;
- флаг `isPowered`, пока таймер > 0.

## Что делает

Жрёт уголь/брёвна (или `fuelItem`). Пока горит — `GetNearbySpeedMultiplier` ускоряет крафт и добычу в радиусе. Сейв таймера в `stateFloat`. Иконка питания в [[MachineUI]].

Не электрическая сеть.
