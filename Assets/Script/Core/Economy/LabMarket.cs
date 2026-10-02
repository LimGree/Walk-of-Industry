using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Цены сдачи в лабу.
/// <para><b>Базовая цена</b> — от сложности производства: сырьё с жилы стоит <see cref="RawValue"/> (5 штук = 1 монета),
/// переработанное — сырьё, ушедшее на одну штуку, × (1 + глубина цепочки). Крафт выгоднее сдачи руды,
/// и чем выше ступень, тем больше наценка (слиток ×2, стальной слиток ×3, квантовое ядро ×7).</para>
/// <para><b>Насыщение рынка</b> — каждая проданная штука копит «насыщение» предмета, оно спадает со временем
/// (τ = <see cref="RecoverSeconds"/>). Цена падает вдвое, когда предмета сдают примерно на
/// <see cref="HalfPriceCoinsPerMinute"/> монет базовой цены в минуту, но не ниже <see cref="FloorMultiplier"/>.
/// Одна гигантская линия упирается в потолок — выгоднее делать разное.</para>
/// Дробные монеты копятся: целые зачисляются, остаток ждёт следующей продажи.
/// </summary>
public static class LabMarket
{
    public const float RawValue = 0.2f;
    public const float FloorMultiplier = 0.1f;
    public const float RecoverSeconds = 120f;
    public const float HalfPriceCoinsPerMinute = 120f;
    const int MaxDepth = 24;

    sealed class Info
    {
        public float baseValue;
        public float rawCost;
        public int depth;
        public float saturation;
        public float lastTime;
        /// <summary>Сколько штук в минуту роняют цену вдвое.</summary>
        public float halfRate;
    }

    static readonly Dictionary<ItemData, Info> infos = new Dictionary<ItemData, Info>(64);
    static Dictionary<ItemData, RecipeData> producers;
    static float remainder;

    /// <summary>Новый мир/загрузка: рынок спокойный, дробный остаток сброшен.</summary>
    public static void Reset()
    {
        foreach (KeyValuePair<ItemData, Info> pair in infos)
            pair.Value.saturation = 0f;
        remainder = 0f;
    }

    /// <summary>Базовая цена (без насыщения), монет за штуку.</summary>
    public static float BaseValue(ItemData item)
    {
        Info info = InfoOf(item);
        return info != null ? info.baseValue : 0f;
    }

    /// <summary>Ступень переработки: 0 — сырьё, 1 — слиток, 2 — пластина/сталь…</summary>
    public static int Depth(ItemData item)
    {
        Info info = InfoOf(item);
        return info != null ? info.depth : 0;
    }

    /// <summary>Текущий множитель цены от насыщения (1 — рынок свободен, 0.1 — завален).</summary>
    public static float Multiplier(ItemData item)
    {
        Info info = InfoOf(item);
        if (info == null)
            return 1f;
        Decay(info);
        return MultiplierOf(info);
    }

    /// <summary>Цена прямо сейчас, монет за штуку.</summary>
    public static float CurrentValue(ItemData item)
    {
        Info info = InfoOf(item);
        if (info == null)
            return 0f;
        Decay(info);
        return info.baseValue * MultiplierOf(info);
    }

    /// <summary>Продать одну штуку: вернуть целые монеты к зачислению (дробь копится).</summary>
    public static int Sell(ItemData item)
    {
        Info info = InfoOf(item);
        if (info == null || info.baseValue <= 0f)
            return 0;
        Decay(info);
        float value = info.baseValue * MultiplierOf(info);
        info.saturation += 1f;
        remainder += value;
        int coins = Mathf.FloorToInt(remainder);
        remainder -= coins;
        return coins;
    }

    /// <summary>Предметы, которые уже продавались (для вкладки «Экономика»), по убыванию насыщения.</summary>
    public static List<ItemData> TrackedItems()
    {
        var list = new List<ItemData>(infos.Count);
        foreach (KeyValuePair<ItemData, Info> pair in infos)
        {
            if (pair.Key == null)
                continue;
            Decay(pair.Value);
            if (pair.Value.saturation > 0.05f)
                list.Add(pair.Key);
        }
        list.Sort((a, b) => Multiplier(a).CompareTo(Multiplier(b)));
        return list;
    }

    static float MultiplierOf(Info info)
    {
        // Насыщение в покое при сдаче r штук/мин: s = r·τ/60. Половина цены при r = halfRate.
        float halfSaturation = info.halfRate * RecoverSeconds / 60f;
        float m = Mathf.Pow(0.5f, info.saturation / Mathf.Max(0.01f, halfSaturation));
        return Mathf.Max(FloorMultiplier, m);
    }

    static void Decay(Info info)
    {
        float now = Time.time;
        float dt = now - info.lastTime;
        info.lastTime = now;
        if (dt > 0f && info.saturation > 0f)
            info.saturation *= Mathf.Exp(-dt / RecoverSeconds);
    }

    static Info InfoOf(ItemData item)
    {
        if (item == null)
            return null;
        if (infos.TryGetValue(item, out Info info))
            return info;

        EnsureProducers();
        Cost(item, 0, out float raw, out int depth);
        info = new Info
        {
            rawCost = raw,
            depth = depth,
            baseValue = RawValue * raw * (1 + depth),
            lastTime = Time.time
        };
        info.halfRate = Mathf.Clamp(HalfPriceCoinsPerMinute / Mathf.Max(0.01f, info.baseValue), 2f, 1200f);
        infos[item] = info;
        return info;
    }

    static void EnsureProducers()
    {
        if (producers != null)
            return;
        producers = new Dictionary<ItemData, RecipeData>();
        RecipeData[] all = GameDatabase.AllRecipes();
        for (int i = 0; i < all.Length; i++)
        {
            RecipeData r = all[i];
            if (r == null || r.outputs == null || r.inputs == null || r.inputs.Count == 0)
                continue;
            for (int o = 0; o < r.outputs.Count; o++)
            {
                ItemData output = r.outputs[o].item;
                if (output == null || r.outputs[o].amount <= 0)
                    continue;
                // Несколько рецептов на один предмет — берём самый дешёвый по сырью (считается позже).
                if (!producers.ContainsKey(output))
                    producers[output] = r;
            }
        }
    }

    /// <summary>Сколько сырья уходит на штуку и глубина цепочки. Сырьё — то, что не крафтится (добыча с жил).</summary>
    static void Cost(ItemData item, int guard, out float raw, out int depth)
    {
        raw = 1f;
        depth = 0;
        if (item == null || guard > MaxDepth || producers == null || !producers.TryGetValue(item, out RecipeData recipe))
        {
            // Предмет без рецепта, но и не с жилы — по старой цене из ассета, чтобы не было бесплатных.
            if (item != null && !Economy.IsExtractorResource(item) && (producers == null || !producers.ContainsKey(item)) && item.sellValue > 1)
                raw = item.sellValue;
            return;
        }

        int outAmount = 1;
        for (int o = 0; o < recipe.outputs.Count; o++)
        {
            if (recipe.outputs[o].item == item)
                outAmount = Mathf.Max(1, recipe.outputs[o].amount);
        }

        float sum = 0f;
        int deepest = 0;
        for (int i = 0; i < recipe.inputs.Count; i++)
        {
            ItemStack stack = recipe.inputs[i];
            if (stack.item == null || stack.amount <= 0)
                continue;
            Cost(stack.item, guard + 1, out float r, out int d);
            sum += r * stack.amount;
            deepest = Mathf.Max(deepest, d);
        }
        raw = Mathf.Max(0.01f, sum / outAmount);
        depth = deepest + 1;
    }
}
