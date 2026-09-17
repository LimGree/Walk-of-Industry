# Conveyor

**Файл:** `Assets/Script/Core/Logistics/Conveyor.cs`  
**Предок:** [[BuildingBase]]  
**Правила:** [[BeltRules]]

## Зачем нужен

Лента 1×1. Выход всегда спереди (`transform.forward` → `ExitDir`). Форму игрок не выбирает: маска входов из соседей, чей **выход** кормит эту клетку.

Один объект, меш формы **создаётся только для текущей** (угол/T/бока/тройник не висят выключенными). Груз — пул моделей [[BeltItemView]]. Тик всех лент — [[WorldSim]], не `Update` на каждой. Сокетов на ленте нет.

| Входы относительно ExitDir | Форма | Префаб |
|---|---|---|
| нет / только зад | Straight | `prefab` |
| один бок | Corner (лево/право) | `cornerPrefab` |
| зад + один бок | Tee | `teePrefab` |
| оба бока | Sides | `sidesPrefab` |
| зад + оба бока | Triple | `triplePrefab` |

После place/remove/rotate — worklist 3×3, снапшот маски ([[BuildingLinker]]).

Линия: один `strokeYaw`, **Smart Yaw только на последней клетке**. Призраки (одиночный, линия, копия) считают маску через `Conveyor.PreviewExits`. Стоящая лента, в которую смотрит гост, на превью тоже меняет форму (`ShowIncomingVisual`), симуляция груза не трогается.

Сдача дальше: клетка `Cell + ExitDir`. Станок принимает, если его вход смотрит на ленту.

Стрелки I/O в режиме стройки: на прямой и углу только незакрытый край; на T / боках / тройнике — на самой клетке перекрёстка.

## Связи

[[BeltRules]] · [[BuildingLinker]] · [[BeltItemView]] · [[Pipe]] · [[PlayerBuilder]] · [[BeltSpeedSystem]]
