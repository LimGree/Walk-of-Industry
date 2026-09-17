# SaveSystem

**Файл:** `Assets/Script/Core/Save/SaveSystem.cs`

## Зачем нужен

Кладёт мир в `save.json` и поднимает его обратно.

Путь: [[WorldCatalog.ActiveSavePath]] = `persistentDataPath/worlds/<id>/save.json`.

## Когда сохраняет

- кнопка в паузе;
- автосейв раз в `autoSaveInterval` (по умолчанию 120 с), если не пауза;
- выход из приложения / сворачивание.

## SaveGame по шагам

1. Собрать [[SaveData]]
2. Поза игрока из [[PlayerMovement]]
3. Все [[BuildingBase]] в сцене → `WriteSave`
4. Исследования, кошелёк, ленты, метки, исследование карты, статистика, хотбар
5. JSON на диск

## LoadGame

Нет файла — сбросить кошелёк/ленты/метки/статистику (новый мир).  
Есть файл — снести все здания, заспавнить по каталогу, `RelinkAll`, `ReadSave`, поза игрока.

Пока спавн идёт, `BuildingLinker.SuppressRelink = true`.
