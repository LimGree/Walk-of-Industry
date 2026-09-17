# Weather

**Файл:** `Assets/Script/Core/World/Weather.cs`  
**Тип:** статический

Три режима: ясно, дождь, гроза. Не симуляция климата — множитель поверх [[DayNight]]: пасмурное небо, ближе туман, слабее солнце/звёзды. Завод не останавливается.

`Cloud` / `Rain` плавно едут к выбранному режиму. `Flash` — вспышка молнии.

Крутит [[WeatherCycle]]. Звук: `ambient/amb_rain`, удар `world/world_thunder` (Mixkit, см. `Resources/Audio/weather-credits.txt`).
