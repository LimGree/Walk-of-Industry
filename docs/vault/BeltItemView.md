# BeltItemView

**Файл:** `Assets/Script/Core/Logistics/BeltItemView.cs`

Модель груза на ленте: `worldPrefab` предмета (пул `Rent` / `Release`). Рука робота — `Create`.

Дальность — [[WorldView]] / [[GameSettings.RenderDistance]]. Тик — [[WorldSim]]. При передаче на следующую клетку визуал не шарится: источник отпускает, приёмник берёт свой.
