# DevCommands — команды консоли

**Файлы:** `Assets/Script/Core/Debug/` — `DevCommands.cs` (общие) + части `DevCommands.Build.cs`, `.Items.cs`, `.Logistics.cs`, `.Player.cs`, `.Production.cs`, `.Sim.cs`; реестр `DevRegistry.cs`, покадровое `DevRuntime.cs`, линии `DevGizmos.cs`. Окно — [[DevConsole]].

## Как устроена команда

Статический метод `string X(DevArgs a)` с атрибутом `[DevCommand("имя", "синтаксис", ...)]`. Синтаксис: `a|b` — слова на выбор, `<item>` — значение (подсказки по id), `[...]` — необязательно, `...` — дальше что угодно. Атрибутов может быть несколько — по одному на вариант. Из них [[DevConsole]] строит `/help`, автодополнение с нечётким поиском и проверку ввода (`DevRegistry`).

## Группы

- **Стройка:** `build`, `destroy`, `rotate`, `wipe`, `freebuild`, `grid`, `decor`, `locate`, `count`.
- **Предметы и деньги:** `give`, `item`, `fill`, `money`, `ruby`, `economy`, `unlock`, `research`.
- **Логистика:** `belt`/`belts`, `pipe`, `clearcargo`, `killitems`, `drones` (path, recall), `throughput`.
- **Игрок и камера:** `tp`, `pos`, `speed`, `noclip`, `cam first|third|front|free|top`, `hud`, `lang`.
- **Производство:** `machine` (finish…), `setrecipe`, `craft`, `idle`, `stat`, `break`, `repair`, `breakdown off|chance|info|now`, `minigame type|win|fail`, `tutorial`.
- **Симуляция и сейвы:** `pause`, `step`, `timescale`, `time`, `timeskip`, `fps`, `log`, `save as`, `load`, `saves`, `snapshot`, `restore`, `reset`, `godsave`, `bugreport`, `validate`, `regenWorldMap`, `spawn`, `dump`, `gizmos`, `help`.

## DevRuntime

То, что нужно каждый кадр: свободная и верхняя камера, скрытый HUD, покадровый шаг симуляции (`/step`), журнал ошибок для `/bugreport`. В новой сцене режим камеры и скрытый HUD не переносятся.

## DevGizmos

Линии в мире (`/gizmos`, `/grid show`, `/drones path`): порты зданий, радиусы генераторов, занятость клеток, чанки [[WorldSim]], маршруты дронов. Один меш из линий, пересобирается 5 раз в секунду.

## /bugreport

Папка `persistentDataPath/bugreports/<время>/`: `info.txt`, `console.log`, `errors.log`, `screenshot.png`, `save.json`.
