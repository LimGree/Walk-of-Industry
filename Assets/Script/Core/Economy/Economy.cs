using UnityEngine;

public static class Economy
{
    public const int StartingCoins = 10000;

    // ----- ребаланс (журнал экономики: доход ×5 к тратам, деньги не копились ради расширения) -----
    /// <summary>Все цены построек (и монетная установка декора) ×2.</summary>
    public const float BuildPriceScale = 2f;
    /// <summary>+2.5% к цене станка за каждый уже стоящий того же типа. Логистика и декор — без роста.</summary>
    public const float CountGrowth = 0.025f;
    /// <summary>Содержание: монет в минуту за каждое здание.</summary>
    public const float UpkeepPerBuildingPerMinute = 0.1f;
    /// <summary>Ремонт: доля баланса — 3% до 10 тыс., плавно до 5% к 200 тыс.</summary>
    public const float RepairFeeMin = 0.03f;
    public const float RepairFeeMax = 0.05f;
    const int RepairFeeLowBalance = 10000;
    const int RepairFeeHighBalance = 200000;
    public const int StartingRubies = 10;
    public const int CoinsPerRuby = 50;
    /// <summary>Покупка рубина за монеты — вдвое дороже продажи, чтобы обмен туда-обратно не давал прибыли.</summary>
    public const int CoinsPerRubyBuy = 100;

    public const int BeltMaxLevel = 10;
    public const int BeltFirstGears = 1000;
    public const int BeltLastGears = 2500000;
    public const int BeltFirstCoins = 900;
    public const int BeltLastCoins = 80000;

    public const float CraftTimeMul = 1.8f;
    public const float ExtractTimeMul = 1.25f;

    /// <summary>Консоль: стройка бесплатная, возврата при сносе нет (/freebuild).</summary>
    public static bool DevFreeBuild;

    public static bool IsExtractorResource(ItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.id))
            return false;
        string id = item.id.Trim().ToLowerInvariant();
        return id == "log" || id == "sand" || id == "stone" || id == "coal_ore"
            || id == "cooper_ore" || id == "copper_ore" || id == "iron_ore" || id == "sulfur"
            || id == "crude_oil" || id == "water";
    }

    /// <summary>Растёт ли цена здания с их количеством: станки — да; ленты, трубы, подземки, сплиттеры и декор — нет.</summary>
    public static bool ScalesWithCount(BuildingData data)
    {
        if (data == null || data.IsConveyor || data.IsPairedStraight)
            return false;
        string id = GameDatabase.Normalize(data.id);
        return !id.Contains("splitter") && !id.StartsWith("decor_");
    }

    static float CountFactor(BuildingData data, int countBefore)
    {
        return ScalesWithCount(data) ? 1f + CountGrowth * Mathf.Max(0, countBefore) : 1f;
    }

    /// <summary>
    /// Цена постройки: база ×<see cref="BuildPriceScale"/> × настройка «Цены на постройку» × рост с количеством.
    /// queued — сколько таких уже стоит в очереди той же операции (линия, вставка), чтобы каждый следующий был дороже.
    /// </summary>
    public static int BuildCost(BuildingData data, int queued = 0)
    {
        if (data == null || GameSettings.Sandbox || DevFreeBuild)
            return 0;
        float cost = Mathf.Max(0, data.buildCost) * BuildPriceScale * GameSettings.CostMultiplier
            * CountFactor(data, WorldSim.CountOf(data) + queued) * PerkSystem.BuildPriceMul;
        return Mathf.Max(0, Mathf.RoundToInt(cost));
    }

    /// <summary>Цена count штук подряд (с ростом цены внутри пачки).</summary>
    public static int BuildCostBatch(BuildingData data, int count)
    {
        int sum = 0;
        for (int i = 0; i < count; i++)
            sum += BuildCost(data, i);
        return sum;
    }

    /// <summary>
    /// Возврат при сносе — 75% цены последнего такого здания (сносимое ещё стоит и учтено в количестве).
    /// Множитель цен не выше 1, чтобы смена настройки не давала фармить деньги.
    /// </summary>
    public static int RefundCoins(BuildingData data)
    {
        if (data == null || GameSettings.Sandbox || DevFreeBuild)
            return 0;
        float mul = Mathf.Min(1f, GameSettings.CostMultiplier);
        float cost = Mathf.Max(0, data.buildCost) * BuildPriceScale * mul * CountFactor(data, WorldSim.CountOf(data) - 1);
        // «Договор о возврате» — 100% вместо 75%. Скидка «Карты клиента» в возврат тоже входит: не больше, чем заплатил.
        return Mathf.Max(0, Mathf.RoundToInt(cost * PerkSystem.BuildPriceMul * PerkSystem.RefundShare));
    }

    /// <summary>Сколько монет снять за ремонт: 3–5% баланса, процент растёт с балансом.</summary>
    public static int RepairFee(int balance)
    {
        if (balance <= 0 || GameSettings.Sandbox)
            return 0;
        float t = Mathf.InverseLerp(RepairFeeLowBalance, RepairFeeHighBalance, balance);
        return Mathf.RoundToInt(balance * Mathf.Lerp(RepairFeeMin, RepairFeeMax, t) * PerkSystem.RepairFeeMul);
    }

    public static void PayRefund(BuildingBase building)
    {
        int coins = RefundCoins(building != null ? building.data : null);
        if (coins > 0 && PlayerWallet.Instance != null)
            PlayerWallet.Instance.AddCoins(coins, MoneySource.Refund);
    }

    public static int UpgradeCost(BuildingBase building)
    {
        int baseCost = BuildCost(building != null ? building.data : null);
        return Mathf.Max(20, baseCost * 2);
    }

    public static int RubyReward(ResearchNodeData node)
    {
        if (node == null)
            return 0;
        if (node.rubyReward > 0)
            return node.rubyReward;

        int items = 0;
        if (node.requiredItems != null)
        {
            for (int i = 0; i < node.requiredItems.Count; i++)
                items += Mathf.Max(0, node.requiredItems[i].amount);
        }

        return Mathf.Max(5, 4 + items / 8);
    }

    public static float CraftNeed(RecipeData recipe)
    {
        if (recipe == null)
            return 0f;
        return Mathf.Max(0.05f, recipe.craftTime) * CraftTimeMul;
    }

    public static int BeltUpgradeCost(int nextLevel)
    {
        return BeltGearCost(nextLevel);
    }

    public static int BeltGearCost(int nextLevel)
    {
        return GeometricCost(nextLevel, BeltFirstGears, BeltLastGears);
    }

    public static int BeltCoinCost(int nextLevel)
    {
        return GeometricCost(nextLevel, BeltFirstCoins, BeltLastCoins);
    }

    static int GeometricCost(int nextLevel, int first, int last)
    {
        int level = Mathf.Clamp(nextLevel, 1, BeltMaxLevel);
        if (level <= 1)
            return first;
        if (level >= BeltMaxLevel)
            return last;
        float t = (level - 1) / (float)(BeltMaxLevel - 1);
        float cost = first * Mathf.Pow(last / (float)first, t);
        return Mathf.Max(1, Mathf.RoundToInt(cost));
    }

    public static float BeltMultiplier(int level)
    {
        int lv = Mathf.Clamp(level, 0, BeltMaxLevel);
        if (lv <= 0)
            return 1f;
        if (lv >= BeltMaxLevel)
            return 9.5f;
        const float first = 1.2f;
        const float last = 9.5f;
        float t = (lv - 1) / (float)(BeltMaxLevel - 1);
        return first * Mathf.Pow(last / first, t);
    }

    public static bool IsGear(ItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.id))
            return false;
        string id = item.id.Trim().ToLowerInvariant();
        return id == "gear" || id == "gears";
    }
}
