# PlayerInteractor

**Файл:** `Assets/Script/Core/Player/PlayerInteractor.cs`

## Зачем нужен

Каждый кадр смотрит, куда смотрит камера. Если луч попал в объект с [[IInteractable]] — запоминает его. По клавише взаимодействия вызывает `Interact`.

Так открываются станки, склады, лаборатория, рука.

## Связи

[[IInteractable]] · [[BuildingBase]] · [[MachineUI]] · [[GameManager]] · [[WalletHud]] · [[SelectionActionsUI]] · [[InputHintUI]] (текст подсказки)

## Поля

- `interactDistance` — как далеко луч (по умолчанию 4 метра)
- `currentInteractable` — цель прямо сейчас

## Методы

### CheckForInteractable
Луч из камеры вперёд. Берёт компонент `IInteractable` у попавшего объекта или его родителя.

### OnInteract
Если пауза или уже открыт станок/магазин/выделение — молчит. Иначе `currentInteractable.Interact(игрок)`.

---

# IInteractable

**Там же в файле.** Это не класс, а **договор**: «у меня есть метод Interact». Любое здание, которое можно открыть руками, подписывает этот договор.

Связанные реализации: [[CrafterBuilding]], [[Extractor]], [[OilExtractor]], [[WaterExtractor]], [[StorageContainer]], [[RoboticArm]], [[ResearchLab]].
