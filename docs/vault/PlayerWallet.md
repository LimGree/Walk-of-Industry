# PlayerWallet

**Файл:** `Assets/Script/Core/Economy/PlayerWallet.cs`

Кошелёк игрока. Один на мир (висит на [[GameManager]]).

## Поля

`Coins`, `Rubies`. Событие `OnChanged` — HUD и кнопки обновляются.

## Методы

- `CanAfford` / `TrySpendCoins` / `AddCoins`
- `AddRubies` / `TryExchangeRubies` (рубины → монеты по [[Economy.CoinsPerRuby]])
- запись в [[SaveData]], чтение; пустой старый сейв получает стартовые 1000 монет

Траты и доходы дублируются в [[ProductionStats]].

## Учёт и содержание

- Каждый метод принимает источник `MoneySource` (`AddCoins(n, MoneySource.LabSale)` и т.д.) и пишет движение в [[EconomyLedger]]. Без источника — «Прочее».
- `Update` двигает игровое время журнала и раз в 10 с списывает **содержание**: 0.1 монеты в минуту за каждое здание (ниже нуля не уходит, в песочнице нет).
- Новый мир — **10 000** монет. Загрузка/новый мир сбрасывает журнал и рынок лабы ([[LabMarket]]).
