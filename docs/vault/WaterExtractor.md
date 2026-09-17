# WaterExtractor

**Файл:** `Assets/Script/Core/Buildings/WaterExtractor.cs`

Водокачка. Ресурс `water`. Ставится **только на воду** (флаги в [[BuildingData]] + проверка биома [[WorldBiomeMap]]).

Логика как у нефти: таймер → выдать воду в трубы. Звук `bld_water_loop`.

Связи: [[BuildingBase]], [[IInteractable]], [[Pipe]], [[MachineUI]].
