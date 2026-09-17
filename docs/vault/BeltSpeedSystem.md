# BeltSpeedSystem

**Файл:** `Assets/Script/Core/Logistics/BeltSpeedSystem.cs`

Общая прокачка **всех** конвейеров в мире. Не отдельная лента.

## Как прокачать

1. Сдавать шестерёнки в [[ResearchLab]] сверх нужд исследования → `SubmitGear` (просто счётчик, **уровень сам не растёт**).
2. В UI лаборатории, вкладка лент, когда шестерёнок хватает, нажать оплату монетами → `TryBuyNext`.

Максимум 10 уровней. Формулы — [[Economy]].

## Поля

`Level`, `GearsTowardNext`, `CanBuyNext`, `Multiplier`.

## Методы

`ResetToNewWorld`, `CaptureSave` / `ApplySave` (уровень режется до максимума, если старый сейв накрутил автопрокачкой).
