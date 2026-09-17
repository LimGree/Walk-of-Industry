# BuildingPrefabLayout

**Файл:** `Assets/Script/Core/Buildings/BuildingPrefabLayout.cs`

Правило префаба здания **до поворота игроком**:

| Локаль | Смысл |
|---|---|
| **+Z** | перед, основной **выход** |
| **−Z** | зад, основной **вход** |
| **+X / −X** | доп. входы (конструктор / жидкость) |
| pivot | центр footprint |

Иерархия:

```
Building
  Visual/Level1
  Visual/Level2     (выкл.)
  Ghost             (выкл. в мире, вкл. у призрака)
  InputSocket       forward = −Z
    IoArrow_In        (ставишь руками)
  OutputSocket      forward = +Z
    IoArrow_Out       (ставишь руками)
```

Стрелки — отдельные префабы [[SocketArrow]] (`IoArrow_In` / `IoArrow_Out`). Кидаешь дочерними на сокеты зданий и на StartPoint / EndPoint лент и труб. Код их не спавнит.

Гост берётся из того же префаба (`BuildingVisuals.PrepareGhostInstance`), не из отдельного `ghostPrefab` (он только запасной для лент).

Меню Unity: **Walk of Industry → Normalize Building Prefabs** — сокеты и Visual/Ghost. Стрелки — **Walk of Industry → Create IO Arrow Prefabs**, дальше руками.

Ленты: выход = `transform.forward` = +Z, формы — отдельные дети/префабы, без I/O-сокетов.

Связи: [[BuildingSocket]] · [[BuildingVisuals]] · [[PlayerBuilder]] · [[BuildingData]]
