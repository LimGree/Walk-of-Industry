# SocketArrow

**Файл:** `Assets/Script/Core/Buildings/SocketArrow.cs`

Маркер на модели стрелки I/O. Саму стрелку код **не создаёт** — ставишь префаб руками.

| Префаб | Цвет | Куда |
|---|---|---|
| `Assets/prefabs/IO/IoArrow_In` | оранжевый | InputSocket, StartPoint ленты/трубы |
| `Assets/prefabs/IO/IoArrow_Out` | зелёный | OutputSocket, EndPoint ленты/трубы |

Как вставить:

1. Открой префаб здания / конвейера / трубы.
2. Перетащи `IoArrow_In` или `IoArrow_Out` **дочерним** на сокет или поинт.
3. Local Position `(0,0,0)`, Local Rotation `(0,0,0)`. Остриё = **+Z** = forward родителя.
4. Масштаб можно подкрутить, поворот не надо — сокет уже смотрит наружу.

Модель: `Assets/models/Builders/io_arrow.obj`.

В Scene View стрелка всегда видна. В игре — только в режиме строительства.

На лентах и трубах в стройке не все стрелки: на прямой и на углу только свободный край (соседняя клетка не лента/труба того же типа); на перекрёстке (T / бока / тройник) — стрелки этой клетки. Станки и сплиттер показывают все свои стрелки. После возврата в зону прогрузки правило применяется снова ([[BuildingBase]] world cull).

Если префабов нет в Project — меню **Walk of Industry → Create IO Arrow Prefabs**.

Связи: [[BuildingPrefabLayout]] · [[BuildingSocket]]
