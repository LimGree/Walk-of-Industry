using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Плюшки персонажа из магазина (вкладка «Снаряжение»): всё за рубины, покупка на мир.
/// Пассивные (ботинки, прыжки, скидки) действуют сразу; снаряжение (крылья, крюк, спальник) кладётся
/// в хотбар снаряжения ([[GearHotbar]]); косметика включается в окне персонажа.
/// Сейв: <see cref="SaveData.perks"/> — строки «id=уровень».
/// Выбор и цены — docs/Плюшки_персонажа.html (отчёт пользователя).
/// </summary>
public class PerkSystem : MonoBehaviour
{
    public enum Cat { Move, Work, Economy, Style }
    public enum Kind { Passive, Gear, Cosmetic }

    public sealed class Def
    {
        public string id;
        public Cat cat;
        public Kind kind;
        /// <summary>Цена каждого уровня в рубинах (длина = число уровней).</summary>
        public int[] prices;
        /// <summary>Нужна раньше купленная плюшка (тройной прыжок ← двойной).</summary>
        public string requires;
        public bool Leveled => prices.Length > 1;
        public string Title => UiLocale.T("perk." + id);
        public string Info => UiLocale.T("perk." + id + ".info");
    }

    static Def D(string id, Cat cat, Kind kind, params int[] prices) =>
        new Def { id = id, cat = cat, kind = kind, prices = prices };

    static Def D(string id, Cat cat, Kind kind, string requires, params int[] prices) =>
        new Def { id = id, cat = cat, kind = kind, prices = prices, requires = requires };

    /// <summary>Цены — ×6 от первого варианта (решение по плейтесту).</summary>
    public static readonly Def[] All =
    {
        // Передвижение
        D("boots", Cat.Move, Kind.Passive, 90, 300, 900),
        D("spring", Cat.Move, Kind.Passive, 90, 300, 900),
        D("double", Cat.Move, Kind.Passive, 360),
        D("triple", Cat.Move, Kind.Passive, "double", 540),
        D("dash", Cat.Move, Kind.Passive, 240),
        D("wings", Cat.Move, Kind.Gear, 720),
        D("grapple", Cat.Move, Kind.Gear, 360),
        D("flippers", Cat.Move, Kind.Gear, 300),
        D("cart", Cat.Move, Kind.Passive, 180),
        D("cart2", Cat.Move, Kind.Passive, "cart", 480),
        // Тросовая дорога — в магазине декора («Опора троса», [[Zipline]]): постройка, а не плюшка.
        // Работа
        D("repairkit", Cat.Work, Kind.Passive, 240),
        D("lamp", Cat.Work, Kind.Passive, 60),
        D("sleep", Cat.Work, Kind.Gear, 150),
        // Экономика
        D("discount", Cat.Economy, Kind.Passive, 1200, 3600),
        D("refund", Cat.Economy, Kind.Passive, 600),
        D("insurance", Cat.Economy, Kind.Passive, 720),
        // Внешний вид
        D("horn", Cat.Style, Kind.Cosmetic, "cart", 60),
        D("cart_copper", Cat.Style, Kind.Cosmetic, "cart", 90),
        D("cart_gold", Cat.Style, Kind.Cosmetic, "cart", 210),
        D("cart_steam", Cat.Style, Kind.Cosmetic, "cart", 180),
        D("wings_stripes", Cat.Style, Kind.Cosmetic, "wings", 90),
        D("wings_sunset", Cat.Style, Kind.Cosmetic, "wings", 90),
        D("trail_sparks", Cat.Style, Kind.Cosmetic, 120),
        D("trail_steam", Cat.Style, Kind.Cosmetic, 120),
        D("trail_leaves", Cat.Style, Kind.Cosmetic, 120),
        D("outfit_welder", Cat.Style, Kind.Cosmetic, 150),
        D("outfit_engineer", Cat.Style, Kind.Cosmetic, 150),
        D("outfit_miner", Cat.Style, Kind.Cosmetic, 180),
        D("outfit_night", Cat.Style, Kind.Cosmetic, 240),
        D("hat_hardhat_lamp", Cat.Style, Kind.Cosmetic, 120),
        D("hat_miner", Cat.Style, Kind.Cosmetic, 90),
        D("hat_crown", Cat.Style, Kind.Cosmetic, 180),
        D("pet", Cat.Style, Kind.Cosmetic, 420),
    };

    public static PerkSystem Instance { get; private set; }
    public event Action Changed;

    /// <summary>Растёт при любой покупке/смене косметики — внешний вид персонажа пересобирается.</summary>
    public static int Version { get; private set; }

    void Raise()
    {
        Version++;
        Changed?.Invoke();
    }

    readonly Dictionary<string, int> levels = new Dictionary<string, int>();
    /// <summary>Включённая косметика по группам (cart_, wings_, trail_, outfit_, hat_): id или пусто.</summary>
    readonly Dictionary<string, string> worn = new Dictionary<string, string>();

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static Def Find(string id)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].id == id)
                return All[i];
        }

        return null;
    }

    // ---------- Владение ----------

    public static int Level(string id)
    {
        if (Instance == null)
            return 0;
        Instance.levels.TryGetValue(id, out int l);
        return l;
    }

    public static bool Has(string id) => Level(id) > 0;

    public static bool IsMaxed(Def d) => Level(d.id) >= d.prices.Length;

    /// <summary>Цена следующего уровня (0 — всё куплено).</summary>
    public static int NextPrice(Def d)
    {
        int l = Level(d.id);
        return l < d.prices.Length ? d.prices[l] : 0;
    }

    public static bool RequirementMet(Def d) => string.IsNullOrEmpty(d.requires) || Has(d.requires);

    public bool TryBuy(Def d)
    {
        if (d == null || IsMaxed(d) || !RequirementMet(d))
            return false;
        int price = NextPrice(d);
        // Песочница — всё бесплатно, как постройки.
        if (!GameSettings.Sandbox)
        {
            if (PlayerWallet.Instance == null || !PlayerWallet.Instance.TrySpendRubies(price, MoneySource.Perk))
                return false;
        }

        levels[d.id] = Level(d.id) + 1;
        if (d.kind == Kind.Cosmetic && !d.id.StartsWith("pet") && !d.id.StartsWith("horn"))
            Wear(d.id);
        if (d.kind == Kind.Gear)
            GearHotbar.Instance?.AddOwned(d.id);
        Raise();
        return true;
    }

    // ---------- Косметика ----------

    public static string GroupOf(string id)
    {
        int i = id.IndexOf('_');
        return i > 0 ? id.Substring(0, i) : id;
    }

    public static string Worn(string group)
    {
        if (Instance == null)
            return null;
        Instance.worn.TryGetValue(group, out string id);
        return id;
    }

    public static bool IsWorn(string id) => Worn(GroupOf(id)) == id;

    /// <summary>Надеть (или снять, если уже надето).</summary>
    public void Wear(string id)
    {
        if (!Has(id))
            return;
        string g = GroupOf(id);
        if (worn.TryGetValue(g, out string cur) && cur == id)
            worn.Remove(g);
        else
            worn[g] = id;
        Raise();
    }

    /// <summary>Питомец и гудок — включатели без группы.</summary>
    public static bool PetOn => Has("pet") && Worn("pet") != "off";

    public void TogglePet()
    {
        if (!Has("pet"))
            return;
        if (Worn("pet") == "off")
            worn.Remove("pet");
        else
            worn["pet"] = "off";
        Raise();
    }

    // ---------- Эффекты (читают движение, экономика, ремонт) ----------

    /// <summary>Ходьба и бег: +15% за уровень ботинок.</summary>
    public static float SpeedMul => 1f + 0.15f * Level("boots");

    static readonly float[] JumpTable = { 1.2f, 1.6f, 2.0f, 2.5f };

    /// <summary>Высота прыжка по уровню пружин (м).</summary>
    public static float JumpHeight(float baseHeight)
    {
        int l = Mathf.Clamp(Level("spring"), 0, JumpTable.Length - 1);
        return l == 0 ? baseHeight : JumpTable[l];
    }

    /// <summary>Прыжков в воздухе: двойной +1, тройной +1.</summary>
    public static int AirJumps => (Has("double") ? 1 : 0) + (Has("triple") ? 1 : 0);

    /// <summary>Множитель цены построек.</summary>
    public static float BuildPriceMul
    {
        get
        {
            int l = Level("discount");
            return l >= 2 ? 0.8f : l == 1 ? 0.9f : 1f;
        }
    }

    /// <summary>Доля возврата при сносе.</summary>
    public static float RefundShare => Has("refund") ? 1f : 0.75f;

    /// <summary>Множитель платы за ремонт и штормовой урон.</summary>
    public static float RepairFeeMul => Has("insurance") ? 0.5f : 1f;

    // ---------- Ремкомплект ----------

    public const int KitCharges = 5;
    int kitDay;
    int kitUsed;

    /// <summary>Сколько быстрых ремонтов осталось сегодня (0 — нет ремкомплекта или кончились).</summary>
    public int KitLeft
    {
        get
        {
            if (!Has("repairkit"))
                return 0;
            if (kitDay != DayNight.Day)
                return KitCharges;
            return Mathf.Max(0, KitCharges - kitUsed);
        }
    }

    public bool TryUseKit()
    {
        if (KitLeft <= 0)
            return false;
        if (kitDay != DayNight.Day)
        {
            kitDay = DayNight.Day;
            kitUsed = 0;
        }

        kitUsed++;
        Raise();
        return true;
    }

    // ---------- Сейв ----------

    public void CaptureSave(SaveData data)
    {
        if (data == null)
            return;
        data.perks = new List<string>();
        foreach (var p in levels)
            data.perks.Add(p.Key + "=" + p.Value);
        data.repairKitDay = kitDay;
        data.repairKitUsed = kitUsed;
        data.perksWorn = new List<string>();
        foreach (var p in worn)
            data.perksWorn.Add(p.Key + "=" + p.Value);
    }

    public void ApplySave(SaveData data)
    {
        levels.Clear();
        worn.Clear();
        if (data != null)
        {
            Parse(data.perks, (k, v) =>
            {
                if (int.TryParse(v, out int l) && Find(k) != null)
                    levels[k] = Mathf.Clamp(l, 0, Find(k).prices.Length);
            });
            Parse(data.perksWorn, (k, v) => worn[k] = v);
            kitDay = data.repairKitDay;
            kitUsed = data.repairKitUsed;
        }

        Raise();
    }

    static void Parse(List<string> rows, Action<string, string> put)
    {
        if (rows == null)
            return;
        for (int i = 0; i < rows.Count; i++)
        {
            string r = rows[i];
            int eq = r != null ? r.IndexOf('=') : -1;
            if (eq > 0)
                put(r.Substring(0, eq), r.Substring(eq + 1));
        }
    }
}
