# Pipe

**Файл:** `Assets/Script/Core/Logistics/Pipe.cs`  
**Предок:** [[Conveyor]]

## Зачем нужен

Та же логика ленты ([[BeltRules]]), но возит **только жидкости** (`ItemData.isFluid`). Ставится линией, как конвейер. Груз на трубе не рисуется.

Короткий класс: переопределяет приём предмета — отвергает твёрдые.

Связи: [[Conveyor]], [[ItemData]], [[BuildingData]] (`isPipe`).
