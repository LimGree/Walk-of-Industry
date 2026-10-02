using System;
using System.Collections.Generic;
using UnityEngine;
using Cat = DecorCatalog.Cat;
using Def = DecorCatalog.Def;

/// <summary>
/// Декорации мира ([[Decorations]]). Живёт на [[GameManager]].
/// Покупка за рубины открывает декорацию навсегда (установка — за монеты, как у зданий).
/// Считает «красоту завода» (пороги дают рубины), коллекции по категориям, скидку дня,
/// трофеи за достижения, ночной свет ближних декораций и пользу: фонари — реже поломки,
/// зона отдыха — легче ремонт. Напольные декорации держит в своей сетке (здания ставятся поверх).
/// </summary>
public class DecorSystem : MonoBehaviour
{
    public static DecorSystem Instance { get; private set; }
    public static event Action Changed;

    public const int TintCostCoins = 10;
    public const float LampRadius = 6f;
    public const float LampBreakMul = 0.3f;
    public const float RestRadius = 8f;
    public const float RestBonusSeconds = 25f;
    public const int DealPercent = 40;
    public const int MaxLights = 16;
    public const float LightDistance = 45f;
    public const int FullCopies = 10;

    public static readonly int[] BeautySteps = { 25, 75, 150, 300, 500, 800 };
    public static readonly int[] BeautyRewards = { 3, 5, 8, 12, 18, 25 };
    public static readonly Cat[] SetCats = { Cat.Light, Cat.Nature, Cat.Industry, Cat.Road, Cat.Rest, Cat.Monument };
    public static readonly int[] SetRewards = { 5, 6, 5, 4, 5, 8 };

    static readonly Color[] Palette =
    {
        new Color(0.16f, 0.42f, 0.50f),   // исходный бирюзовый
        new Color(0.78f, 0.18f, 0.14f),
        new Color(0.95f, 0.50f, 0.12f),
        new Color(0.95f, 0.78f, 0.20f),
        new Color(0.25f, 0.60f, 0.25f),
        new Color(0.20f, 0.40f, 0.85f),
        new Color(0.55f, 0.30f, 0.75f),
        new Color(0.88f, 0.88f, 0.86f),
        new Color(0.12f, 0.12f, 0.13f),
    };

    public static int PaintCount => Palette.Length;
    public static int TintCost => GameSettings.Sandbox ? 0 : TintCostCoins;

    static readonly List<Decoration> Placed = new List<Decoration>(256);
    static readonly HashSet<Decoration> PlacedSet = new HashSet<Decoration>();
    static readonly Dictionary<Vector2Int, Decoration> Floor = new Dictionary<Vector2Int, Decoration>();
    static readonly List<Vector2Int> CellScratch = new List<Vector2Int>(16);
    static readonly Material[] paints = new Material[16];
    static float beauty;
    static bool beautyDirty = true;

    readonly HashSet<string> owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> claims = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    float nextLights;

    public static IReadOnlyList<Decoration> All => Placed;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        DecorCatalog.Ensure();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ---------- Владение и магазин ----------

    public enum Offer { Owned, Trophy, OffSeason, Buy }

    public static bool IsSandboxWorld => WorldCatalog.Active != null && WorldCatalog.Active.sandbox;

    /// <summary>Сезонные продаются с декабря по февраль (по часам компьютера).</summary>
    public static bool InSeason
    {
        get
        {
            int month = DateTime.Now.Month;
            return month == 12 || month == 1 || month == 2;
        }
    }

    public static bool IsOwned(BuildingData data)
    {
        return IsOwned(DecorCatalog.Find(data));
    }

    public static bool IsOwned(Def def)
    {
        if (def == null)
            return false;
        if (IsSandboxWorld)
            return true;
        if (def.IsTrophy)
            return AchievementSystem.Instance != null && AchievementSystem.Instance.IsUnlocked(def.trophy);
        return Instance != null && Instance.owned.Contains(def.id);
    }

    public static Offer OfferFor(Def def)
    {
        if (IsOwned(def))
            return Offer.Owned;
        if (def.IsTrophy)
            return Offer.Trophy;
        if (def.seasonal && !InSeason)
            return Offer.OffSeason;
        return Offer.Buy;
    }

    public static Def Deal()
    {
        IReadOnlyList<Def> all = DecorCatalog.All;
        int count = 0;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].Sellable && !all[i].seasonal)
                count++;
        }

        if (count == 0)
            return null;
        int seed = WorldCatalog.Active != null ? WorldCatalog.Active.seed : 0;
        int pick;
        unchecked
        {
            int h = seed * 7919 ^ (DayNight.Day * 104729) ^ 0x5bd1e995;
            h ^= h >> 13;
            h *= 0x2c1b3c6d;
            pick = (h & 0x7fffffff) % count;
        }

        for (int i = 0; i < all.Count; i++)
        {
            if (!all[i].Sellable || all[i].seasonal)
                continue;
            if (pick-- == 0)
                return all[i];
        }

        return null;
    }

    public static bool IsDeal(Def def)
    {
        return def != null && def == Deal();
    }

    public static int Price(Def def)
    {
        if (def == null)
            return 0;
        if (!IsDeal(def))
            return def.rubies;
        return Mathf.Max(1, Mathf.RoundToInt(def.rubies * (100 - DealPercent) / 100f));
    }

    public bool TryBuy(Def def)
    {
        if (def == null || OfferFor(def) != Offer.Buy)
            return false;
        int price = Price(def);
        if (PlayerWallet.Instance == null || !PlayerWallet.Instance.TrySpendRubies(price))
            return false;
        owned.Add(def.id);
        UiAudio.PlayConfirm();
        UiNotification.Push(NotifyKind.General, DecorText.T("decor.toast_bought"),
            DecorText.T("decor.toast_bought_body", def.Title), UiStatus.Completed);
        RaiseUnlocks();
        CheckSets(true);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Отладка: открыть все / забрать все (команда /decor).</summary>
    public void GrantAll(bool grant)
    {
        if (grant)
        {
            IReadOnlyList<Def> all = DecorCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].Sellable)
                    owned.Add(all[i].id);
            }
        }
        else
            owned.Clear();

        RaiseUnlocks();
        Changed?.Invoke();
    }

    public bool Grant(Def def)
    {
        if (def == null || !def.Sellable || !owned.Add(def.id))
            return false;
        RaiseUnlocks();
        Changed?.Invoke();
        return true;
    }

    public int OwnedCount()
    {
        int n = 0;
        IReadOnlyList<Def> all = DecorCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (IsOwned(all[i]))
                n++;
        }

        return n;
    }

    static void RaiseUnlocks()
    {
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.RaiseUnlocksChanged();
    }

    /// <summary>Достижение открыто — может выдать декорацию-трофей.</summary>
    public static void OnAchievement(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;
        IReadOnlyList<Def> all = DecorCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (!all[i].IsTrophy || all[i].trophy != id)
                continue;
            if (!AchievementSystem.Mute)
                UiNotification.Push(NotifyKind.Achievement, DecorText.T("decor.toast_trophy"), all[i].Title, UiStatus.Completed);
            RaiseUnlocks();
            Changed?.Invoke();
        }
    }

    // ---------- Коллекции ----------

    public static void SetProgress(Cat cat, out int have, out int total)
    {
        have = 0;
        total = 0;
        IReadOnlyList<Def> all = DecorCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            Def d = all[i];
            if (d.cat != cat || !d.Sellable || d.seasonal)
                continue;
            total++;
            if (IsOwned(d))
                have++;
        }
    }

    public static int SetReward(Cat cat)
    {
        int i = Array.IndexOf(SetCats, cat);
        return i >= 0 ? SetRewards[i] : 0;
    }

    public bool IsClaimed(string key)
    {
        return claims.Contains(key);
    }

    void CheckSets(bool toast)
    {
        for (int i = 0; i < SetCats.Length; i++)
        {
            SetProgress(SetCats[i], out int have, out int total);
            string key = "set_" + SetCats[i].ToString().ToLowerInvariant();
            if (total == 0 || have < total || !claims.Add(key))
                continue;
            if (PlayerWallet.Instance != null)
                PlayerWallet.Instance.AddRubies(SetRewards[i]);
            if (toast)
            {
                UiAudio.PlayNotify();
                UiNotification.Push(NotifyKind.Achievement, DecorText.T("decor.toast_set"),
                    DecorText.T("decor.toast_set_body", DecorCatalog.CategoryTitle(SetCats[i]), SetRewards[i]), UiStatus.Completed);
            }
        }
    }

    // ---------- Красота ----------

    public static float Beauty
    {
        get
        {
            if (beautyDirty)
                RecalcBeauty();
            return beauty;
        }
    }

    static void RecalcBeauty()
    {
        beautyDirty = false;
        var counts = new Dictionary<Def, int>();
        for (int i = 0; i < Placed.Count; i++)
        {
            Decoration d = Placed[i];
            Def def = d != null ? d.Def : null;
            if (def == null)
                continue;
            counts.TryGetValue(def, out int n);
            counts[def] = n + 1;
        }

        float sum = 0f;
        foreach (var pair in counts)
        {
            int full = Mathf.Min(pair.Value, FullCopies);
            int rest = pair.Value - full;
            sum += pair.Key.beauty * (full + rest * 0.25f);
        }

        beauty = sum;
    }

    /// <summary>Следующий порог красоты и награда; false — все получены.</summary>
    public bool NextBeautyStep(out int step, out int reward)
    {
        for (int i = 0; i < BeautySteps.Length; i++)
        {
            if (claims.Contains("beauty_" + BeautySteps[i]))
                continue;
            step = BeautySteps[i];
            reward = BeautyRewards[i];
            return true;
        }

        step = 0;
        reward = 0;
        return false;
    }

    void CheckBeauty()
    {
        if (AchievementSystem.Mute)
            return;
        float b = Beauty;
        for (int i = 0; i < BeautySteps.Length; i++)
        {
            if (b < BeautySteps[i])
                break;
            if (!claims.Add("beauty_" + BeautySteps[i]))
                continue;
            if (PlayerWallet.Instance != null)
                PlayerWallet.Instance.AddRubies(BeautyRewards[i]);
            UiAudio.PlayNotify();
            UiNotification.Push(NotifyKind.Achievement, DecorText.T("decor.toast_beauty", BeautySteps[i]),
                DecorText.T("decor.toast_beauty_body", BeautyRewards[i]), UiStatus.Completed);
        }
    }

    // ---------- Реестр поставленных ----------

    public static void Register(Decoration d)
    {
        if (d == null || !PlacedSet.Add(d))
            return;
        Placed.Add(d);
        beautyDirty = true;
        if (Instance != null)
            Instance.CheckBeauty();
        Changed?.Invoke();
    }

    public static void Unregister(Decoration d)
    {
        if (d == null)
            return;
        UnregisterFloor(d);
        if (!PlacedSet.Remove(d))
            return;
        Placed.Remove(d);
        beautyDirty = true;
        d.SetLightsOn(false);
        Changed?.Invoke();
    }

    public static void RegisterFloor(Decoration d)
    {
        if (d == null)
            return;
        UnregisterFloor(d);
        GridFootprint.CollectCells(d.transform.position, d.FootprintSize, CellScratch);
        for (int i = 0; i < CellScratch.Count; i++)
            Floor[CellScratch[i]] = d;
    }

    public static void UnregisterFloor(Decoration d)
    {
        if (d == null || Floor.Count == 0)
            return;
        CellScratch.Clear();
        foreach (var pair in Floor)
        {
            if (pair.Value == d)
                CellScratch.Add(pair.Key);
        }

        for (int i = 0; i < CellScratch.Count; i++)
            Floor.Remove(CellScratch[i]);
        CellScratch.Clear();
    }

    public static Decoration FloorAt(Vector2Int cell)
    {
        Floor.TryGetValue(cell, out Decoration d);
        return d != null ? d : null;
    }

    public static bool IsFloorFree(Vector2Int min, Vector2Int size, HashSet<GameObject> ignore = null)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                Decoration d = FloorAt(new Vector2Int(min.x + x, min.y + z));
                if (d != null && (ignore == null || !ignore.Contains(d.gameObject)))
                    return false;
            }
        }

        return true;
    }

    public static bool IsFloorData(BuildingData data)
    {
        Def def = DecorCatalog.Find(data);
        return def != null && def.IsFloor;
    }

    /// <summary>Свободна ли клетка для data: напольные — в своей сетке, остальные — в сетке зданий.</summary>
    public static bool IsAreaFreeFor(BuildingData data, Vector2Int min, Vector2Int size, object reservation, HashSet<GameObject> ignore)
    {
        return IsFloorData(data)
            ? IsFloorFree(min, size, ignore)
            : GridOccupancy.IsAreaFree(min, size, reservation, ignore);
    }

    // ---------- Польза ----------

    /// <summary>Множитель шанса ночной поломки: под фонарём — реже.</summary>
    public static float BreakWeight(Vector3 pos)
    {
        return HasPerkNear(DecorCatalog.Perk.Lamp, pos, LampRadius) ? LampBreakMul : 1f;
    }

    /// <summary>Рядом зона отдыха (скамейка, кулер, автомат…) — ремонт мягче.</summary>
    public static bool RestNear(Vector3 pos)
    {
        return HasPerkNear(DecorCatalog.Perk.Rest, pos, RestRadius);
    }

    static bool HasPerkNear(DecorCatalog.Perk perk, Vector3 pos, float radius)
    {
        float r2 = radius * radius;
        for (int i = 0; i < Placed.Count; i++)
        {
            Decoration d = Placed[i];
            if (d == null || d.Def == null || d.Def.perk != perk)
                continue;
            Vector3 dp = d.transform.position - pos;
            dp.y = 0f;
            if (dp.sqrMagnitude <= r2)
                return true;
        }

        return false;
    }

    // ---------- Свет ----------

    static readonly List<Decoration> LightScratch = new List<Decoration>(64);

    public static bool IsNight
    {
        get
        {
            if (!GameSettings.DayNightEnabled)
                return false;
            float h = DayNight.Hour;
            return h >= DayNight.Dusk - 0.8f || h < DayNight.Dawn + 0.4f;
        }
    }

    void Update()
    {
        if (Time.unscaledTime < nextLights)
            return;
        nextLights = Time.unscaledTime + 0.4f;
        UpdateLights();
    }

    static void UpdateLights()
    {
        Camera cam = Camera.main;
        bool night = IsNight;
        LightScratch.Clear();
        Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
        float max2 = LightDistance * LightDistance;
        for (int i = 0; i < Placed.Count; i++)
        {
            Decoration d = Placed[i];
            if (d == null || d.Lights.Count == 0)
                continue;
            if (night && cam != null && d.IsShown && (d.transform.position - eye).sqrMagnitude <= max2)
                LightScratch.Add(d);
            else
                d.SetLightsOn(false);
        }

        if (LightScratch.Count > MaxLights)
        {
            LightScratch.Sort((a, b) =>
                (a.transform.position - eye).sqrMagnitude.CompareTo((b.transform.position - eye).sqrMagnitude));
            for (int i = MaxLights; i < LightScratch.Count; i++)
                LightScratch[i].SetLightsOn(false);
            LightScratch.RemoveRange(MaxLights, LightScratch.Count - MaxLights);
        }

        for (int i = 0; i < LightScratch.Count; i++)
            LightScratch[i].SetLightsOn(true);
    }

    // ---------- Краска ----------

    public static Material Paint(int index)
    {
        index = Mathf.Clamp(index, 0, Palette.Length - 1);
        if (paints[index] != null)
            return paints[index];
        Material baseMat = ModelLibrary.FindMaterial(Decoration.PaintMaterial);
        if (index == 0 && baseMat != null)
        {
            paints[0] = baseMat;
            return baseMat;
        }

        Material m = baseMat != null ? new Material(baseMat) : RuntimeMaterials.Create(Palette[index]);
        m.name = Decoration.PaintMaterial + "_" + index;
        m.color = Palette[index];
        paints[index] = m;
        return m;
    }

    public static Color PaintColor(int index)
    {
        return Palette[Mathf.Clamp(index, 0, Palette.Length - 1)];
    }

    // ---------- Сохранение ----------

    public void CaptureSave(SaveData save)
    {
        if (save == null)
            return;
        save.decorOwned = new List<string>(owned);
        save.decorClaims = new List<string>(claims);
    }

    public void ApplySave(SaveData save)
    {
        owned.Clear();
        claims.Clear();
        if (save != null)
        {
            if (save.decorOwned != null)
            {
                for (int i = 0; i < save.decorOwned.Count; i++)
                {
                    if (!string.IsNullOrEmpty(save.decorOwned[i]))
                        owned.Add(save.decorOwned[i]);
                }
            }

            if (save.decorClaims != null)
            {
                for (int i = 0; i < save.decorClaims.Count; i++)
                {
                    if (!string.IsNullOrEmpty(save.decorClaims[i]))
                        claims.Add(save.decorClaims[i]);
                }
            }
        }

        beautyDirty = true;
        Changed?.Invoke();
    }

    public void ResetToNewWorld()
    {
        owned.Clear();
        claims.Clear();
        beautyDirty = true;
        Changed?.Invoke();
    }
}
