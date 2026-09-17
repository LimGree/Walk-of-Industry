# LoadingScreen

**Файл:** `Assets/Script/Core/UI/LoadingScreen.cs`  
**Тип:** компонент на сцене `Loading`

## Зачем нужен

Прокладка между меню и игрой. Полоска прогресса **плавно** догоняет цель (`shown` → `target`), на долгих шагах чуть ползёт сама, чтобы не казалось, что игра зависла.

Этапы: сцена → биомы [[WorldBiomeMap]] → жилы [[WorldResourceScatterer]] (корутина, `ScatterProgress`) → сейв [[SaveSystem.LoadGameRoutine]].

## Связи

[[MainMenu]] (кто грузит эту сцену), [[IndustryUi]], [[UiLocale]]
