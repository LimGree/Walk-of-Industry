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
