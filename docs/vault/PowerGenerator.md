# PowerGenerator

**Файлы:** `Assets/Script/Core/Buildings/PowerGenerator.cs`, префаб `Assets/Prefabs/Buildings/PowerGenerator.prefab`, данные `PowerGenerator.asset`  
**Предок:** [[BuildingBase]]

Модель `Assets/Art/Models/Buildings/Generator from blender/Generator.fbx`, иконка `Assets/Art/Icons/Buildings/Generator.png`. Исследование `research_power_generator` после шестерёнок.

## Что делает

Жжёт топливо и, пока горит, ускоряет крафт и добычу в радиусе (`GetNearbySpeedMultiplier`). Не электрическая сеть.

## Топливо

- **Уголь** (и брёвна) — лентой **сзади**.
- **Нефть** — трубой **слева** (−X). Этот второй вход-сокет создаётся кодом, префабу он не нужен.
- Каждому предмету — свои секунды работы; 0 — не топливо.
- У топки есть потолок запаса в секундах: когда полна, предмет остаётся на ленте/в трубе, а не сгорает впустую.

Сейв таймера — в `stateFloat`. Иконка питания — в [[MachineUI]]. Радиус видно в консоли `/gizmos` ([[DevCommands]]).
