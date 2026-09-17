# ItemData

**Файл:** `Assets/Script/Core/Data/ItemData.cs`  
**Тип:** ScriptableObject

Один вид предмета: руда, слиток, шестерёнка, нефть.

## Поля

- `id` — строка-ключ (`iron_ore`)
- имя, описание, иконка, 3D-префаб для ленты
- `maxStack`, `isFuel` / `fuelValue`
- `isFluid` — нельзя класть на обычную ленту
- `sellValue` — база для [[Economy.SellValue]]

Экземпляр «три железных слитка в буфере» — это не этот объект, а счётчик. Сам ScriptableObject один на весь вид.
