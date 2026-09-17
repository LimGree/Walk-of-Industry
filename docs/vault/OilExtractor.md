# OilExtractor

**Файл:** `Assets/Script/Core/Buildings/OilExtractor.cs`

Нефтяная вышка. Не привязана к жиле камня: ресурс `crude_oil` из [[GameDatabase]]. При постановке бросает случайную **богатство** (4 ступени, разный интервал). Запас бесконечный.

Интервал тоже множится на [[Economy.ExtractTimeMul]].

Связи: [[BuildingBase]], [[IInteractable]], [[MachineUI]], [[GameAudio]] (`bld_oil_loop`).
