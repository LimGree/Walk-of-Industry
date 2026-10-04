using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; private set; }

    public int Coins { get; private set; }
    public int Rubies { get; private set; }

    public event System.Action OnChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResetToNewWorld();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    const float UpkeepEvery = 10f;
    float upkeepTimer;
    float upkeepDebt;

    void Update()
    {
        float dt = Time.deltaTime;
        EconomyLedger.Tick(dt, Coins, Rubies);
        TickUpkeep(dt);
    }

    /// <summary>Содержание: 0.1 монеты в минуту за здание, списывается раз в 10 с (ниже нуля не уходит).</summary>
    void TickUpkeep(float dt)
    {
        if (dt <= 0f || GameSettings.Sandbox)
            return;
        upkeepTimer += dt;
        if (upkeepTimer < UpkeepEvery)
            return;
        upkeepDebt += WorldSim.Buildings.Count * Economy.UpkeepPerBuildingPerMinute * (upkeepTimer / 60f);
        upkeepTimer = 0f;
        int due = Mathf.FloorToInt(upkeepDebt);
        if (due <= 0)
            return;
        upkeepDebt -= due;
        AddCoins(-Mathf.Min(due, Coins), MoneySource.Upkeep);
    }

    public void ResetToNewWorld()
    {
        Coins = Economy.StartingCoins;
        Rubies = Economy.StartingRubies;
        EconomyLedger.Reset(Coins, Rubies);
        LabMarket.Reset();
        OnChanged?.Invoke();
    }

    public bool CanAfford(int coins)
    {
        return coins <= 0 || Coins >= coins;
    }

    public bool TrySpendCoins(int amount, MoneySource source = MoneySource.Other)
    {
        if (amount <= 0)
            return true;
        if (Coins < amount)
            return false;
        Coins -= amount;
        ProductionStats.Instance?.RecordCoinsSpent(amount);
        EconomyLedger.Record(source, -amount, 0);
        OnChanged?.Invoke();
        return true;
    }

    public void AddCoins(int amount, MoneySource source = MoneySource.Other)
    {
        if (amount == 0)
            return;
        int before = Coins;
        Coins = Mathf.Max(0, Coins + amount);
        EconomyLedger.Record(source, Coins - before, 0);
        if (Coins < before)
            ProductionStats.Instance?.RecordCoinsSpent(before - Coins);
        if (amount > 0)
            ProductionStats.Instance?.RecordCoinsGained(amount);
        OnChanged?.Invoke();
        AchievementSystem.NotifyCoins(Coins);
    }

    public void AddRubies(int amount, MoneySource source = MoneySource.Other)
    {
        if (amount == 0)
            return;
        int before = Rubies;
        Rubies = Mathf.Max(0, Rubies + amount);
        EconomyLedger.Record(source, 0, Rubies - before);
        if (amount > 0)
            ProductionStats.Instance?.RecordRubiesGained(amount);
        OnChanged?.Invoke();
    }

    public bool TrySpendRubies(int amount, MoneySource source = MoneySource.Other)
    {
        if (amount <= 0)
            return true;
        if (Rubies < amount)
            return false;
        Rubies -= amount;
        EconomyLedger.Record(source, 0, -amount);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Штраф рубинами: чего не хватает — добирается монетами по двойному курсу, ниже нуля не уходит.
    /// Возвращает сколько рубинов реально списано.
    /// </summary>
    public int PayRubyPenalty(int amount)
    {
        if (amount <= 0)
            return 0;
        int fromRubies = Mathf.Min(Rubies, amount);
        Rubies -= fromRubies;
        int missing = amount - fromRubies;
        int coins = 0;
        if (missing > 0)
        {
            coins = Mathf.Min(Coins, missing * Economy.CoinsPerRuby * 2);
            Coins -= coins;
            ProductionStats.Instance?.RecordCoinsSpent(coins);
        }
        EconomyLedger.Record(MoneySource.Repair, -coins, -fromRubies);
        OnChanged?.Invoke();
        return fromRubies;
    }

    public bool TryExchangeRubies(int rubies)
    {
        if (rubies <= 0 || Rubies < rubies)
            return false;
        Rubies -= rubies;
        EconomyLedger.Record(MoneySource.Exchange, 0, -rubies);
        AddCoins(rubies * Economy.CoinsPerRuby, MoneySource.Exchange);
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>Купить рубины за монеты по курсу <see cref="Economy.CoinsPerRubyBuy"/>.</summary>
    public bool TryBuyRubies(int rubies)
    {
        int cost = rubies * Economy.CoinsPerRubyBuy;
        if (rubies <= 0 || Coins < cost)
            return false;
        Coins -= cost;
        ProductionStats.Instance?.RecordCoinsSpent(cost);
        EconomyLedger.Record(MoneySource.Exchange, -cost, 0);
        AddRubies(rubies, MoneySource.Exchange);
        return true;
    }

    public void CaptureSave(SaveData save)
    {
        if (save == null)
            return;
        save.coins = Coins;
        save.rubies = Rubies;
    }

    public void ApplySave(SaveData save)
    {
        if (save == null)
        {
            ResetToNewWorld();
            return;
        }

        Coins = Mathf.Max(0, save.coins);
        Rubies = Mathf.Max(0, save.rubies);
        bool emptyWorld = save.buildings == null || save.buildings.Count == 0;
        if (Coins == 0 && Rubies == 0 && (save.version < 3 || emptyWorld))
            Coins = Economy.StartingCoins;
        EconomyLedger.Reset(Coins, Rubies);
        LabMarket.Reset();
        OnChanged?.Invoke();
    }
}
