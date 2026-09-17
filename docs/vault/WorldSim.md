# WorldSim

**Файл:** `Assets/Script/Core/World/WorldSim.cs`

Один тикер логистики и кулла. Вешается на [[GridSystem]].

- `Update`: все живые [[Conveyor]] / [[Splitter]] — шаг груза и картинка каждый кадр.
- `LateUpdate`: `SimFlush` только зданий с непустым буфером; кулл мешей по сетке **32 м** вокруг игрока.
