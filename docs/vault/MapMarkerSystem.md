# MapMarkerSystem

**Файл:** `Assets/Script/Core/Map/MapMarkerSystem.cs`

Именованные метки на карте (как вейпоинты в Xaero).

## Данные

Список [[SaveData]] `MapMarkerSave`: клетка, имя, цвет, скрыта ли.

## Голограммы в мире

Для каждой видимой метки — табличка лицом к камере. Близко (< ~14 м) прячется. Далеко — висит над горизонтом.

Размер средний (после жалоб «слишком огромные / слишком мелкие»). Текст не зеркалить: табличка смотрит `LookRotation(pos - camera)`.

## API

`Add` / `Remove` / `Rename` / `SetColor` / `SetHidden` / сейв.

Рисует метки на карте [[WorldMapUI]]. Телепорт — [[PlayerMovement.TeleportToCell]], только если [[MapSettings.AllowTeleport]].
