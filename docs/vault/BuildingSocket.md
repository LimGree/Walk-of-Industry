# BuildingSocket

**Файл:** `Assets/Script/Core/Buildings/BuildingSocket.cs`

## SocketType

Два значения: `Input` (вход) и `Output` (выход). Как вилка и розетка.

## BuildingSocket

Маленький компонент на дочернем объекте здания. Помечает, **с какой стороны** предмет входит или выходит.

## Зачем нужен

[[BuildingLinker]] смотрит, куда «смотрит» сокет (клетка перед ним), и соединяет выход одного станка со входом другого. У лент сокетов нет — их клеит [[BeltRules]] / `ExitDir`.

Оси префаба: [[BuildingPrefabLayout]]. На сокет можно кинуть [[SocketArrow]] (`IoArrow_In` / `IoArrow_Out`).

## Поля

- `type` — вход или выход
- `connectedSocket` — парный сокет соседа (может быть пусто)
- принадлежность родителю-зданию

## Методы

- найти клетку перед собой;
- соединиться / отсоединиться;
- `DisconnectAll` при сносе здания.

Связи: [[BuildingBase]], [[BuildingLinker]], [[Conveyor]].
