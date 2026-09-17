# StorageContainer

**Файл:** `Assets/Script/Core/Buildings/StorageContainer.cs`  
**Предок:** [[BuildingBase]] + [[IInteractable]]

Склад. Принимает предметы в словарь «предмет → количество». Отдаёт на выход, если подключена лента. UI сетки слотов рисует [[MachineUI]].

## Методы

- `TryReceiveItem` — кладёт в хранилище, если не переполнен;
- выталкивание на выход по таймеру/запросу;
- сохранение списка стаков в [[SaveData]].

Наследник: [[FluidStorageTank]] (тот же склад, но для жидкостей).
