using UnityEngine;

public static class Economy
{
    public const int StartingCoins = 2500;
    public const int StartingRubies = 10;
    public const int CoinsPerRuby = 50;

    public const int BeltMaxLevel = 10;
    public const int BeltFirstGears = 1000;
    public const int BeltLastGears = 2500000;
    public const int BeltFirstCoins = 900;
    public const int BeltLastCoins = 80000;

    public const float CraftTimeMul = 1.8f;
    public const float ExtractTimeMul = 1.25f;

    public static int SellValue(ItemData item)
    {
        if (item == null)
            return 0;
        int v = RawSellValue(item);
        if (IsExtractorResource(item))
            return v;
        if (v <= 0)
            return 1;
        return Mathf.Max(1, Mathf.RoundToInt(v / 5f));
    }

    static int RawSellValue(ItemData item)
    {
        int v = Mathf.Max(0, item.sellValue);
        if (v <= 1)
            return v;
        if (v == 2)
            return 4;
        if (v <= 4)
            return v * 3;
        if (v <= 8)
            return Mathf.RoundToInt(v * 2.6f);
        if (v <= 16)
            return Mathf.RoundToInt(v * 2.1f);
        return Mathf.RoundToInt(v * 1.55f);
    }

    public static bool IsExtractorResource(ItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.id))
            return false;
        string id = item.id.Trim().ToLowerInvariant();
        return id == "log" || id == "sand" || id == "stone" || id == "coal_ore"
            || id == "cooper_ore" || id == "copper_ore" || id == "iron_ore" || id == "sulfur"
            || id == "crude_oil" || id == "water";
    }

    public static int BuildCost(BuildingData data)
    {
        return data != null ? Mathf.Max(0, data.buildCost) : 0;
    }

    public static int RefundCoins(BuildingData data)
    {
        return Mathf.Max(0, Mathf.RoundToInt(BuildCost(data) * 0.75f));
    }

    public static void PayRefund(BuildingBase building)
    {
        int coins = RefundCoins(building != null ? building.data : null);
        if (coins > 0 && PlayerWallet.Instance != null)
            PlayerWallet.Instance.AddCoins(coins);
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
