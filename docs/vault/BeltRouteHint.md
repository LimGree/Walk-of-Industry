# BeltRouteHint и TutorialLog

**Файлы:** `Assets/Script/Core/Tutorial/BeltRouteHint.cs`, `Tutorial/TutorialLog.cs`

## BeltRouteHint

Голубой маршрут ленты в обучении ([[TutorialFx]]): путь по свободным клеткам от любого выхода одного здания до любого входа другого (A*, 4 стороны). Только для показа — ничего не строит. Клетки с жилой дороже (лента встанет, но лучше обойти), вода запрещена.

## TutorialLog

Лог плейтеста обучения: `persistentDataPath/tutorial_log.txt`. Строка на событие: время, шаг, секунды на шаге, что случилось (`leave` / `skip-step` / `skip-all` / `repeat`). Шаги, где игрок сидит дольше всех, — первые кандидаты на переписывание. См. [[Обучение]].
