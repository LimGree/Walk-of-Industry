using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Цели после вводной главы (слой B) и подсказки «в первый раз» (слой C).
/// Вехи идут по порядку механик: сборщик → шестерни → провод → логистика → конструктор → … → ИИ-модуль.
/// Активна всегда одна веха; условия — «исследовать / поставить N / сделать N» (сделано — по [[ProductionStats]]).
/// Отслеживаемый предмет — из [[ProductionMapUI]]: цепочка до руды с галочками.
/// Сейв: <see cref="SaveData.milestonesDone"/>, <see cref="SaveData.hintsSeen"/>, <see cref="SaveData.trackedItemId"/>.
/// </summary>
public class GoalSystem : MonoBehaviour
{
    const float CheckEvery = 1f;
    const float HintEvery = 2f;
    public const float HintShowSeconds = 9f;

    public enum CondKind { Research, Build, Produce }

    public struct Cond
    {
        public CondKind kind;
        public string id;
        public int count;

        public static Cond R(string id) => new Cond { kind = CondKind.Research, id = id, count = 1 };
        public static Cond B(string id, int n = 1) => new Cond { kind = CondKind.Build, id = id, count = n };
        public static Cond P(string id, int n) => new Cond { kind = CondKind.Produce, id = id, count = n };
    }

    public class Milestone
    {
        public string id;
        public int reward;
        public Cond[] conds;
        public string Title => UiLocale.T("goal." + id);
        public string Info => UiLocale.T("goal." + id + ".info");
    }

    /// <summary>Вехи. Порядок = порядок механик в игре (см. docs/План_обучения_и_прогрессии.md, 4.2).</summary>
    public static readonly Milestone[] Milestones =
    {
        new Milestone { id = "assembler", reward = 1500, conds = new[] { Cond.R("research_assembler"), Cond.B("assembler"), Cond.P("iron_rod", 20) } },
        new Milestone { id = "gears", reward = 2000, conds = new[] { Cond.R("research_gear"), Cond.P("gear", 50) } },
        new Milestone { id = "wire", reward = 2500, conds = new[] { Cond.R("research_wire"), Cond.B("assembler", 3), Cond.P("wire", 100) } },
        new Milestone { id = "logistics", reward = 3000, conds = new[] { Cond.R("research_underground_conveyor"), Cond.B("splitter"), Cond.B("underground_conveyor", 2) } },
        new Milestone { id = "constructor", reward = 4000, conds = new[] { Cond.R("research_constructor"), Cond.B("constructor"), Cond.R("research_circuit_board"), Cond.P("circuit_board", 10) } },
        new Milestone { id = "sand", reward = 4000, conds = new[] { Cond.R("research_silicon"), Cond.R("research_glass"), Cond.P("silicon", 50), Cond.P("glass", 50) } },
        new Milestone { id = "steel", reward = 5000, conds = new[] { Cond.R("research_steel_ingot"), Cond.R("research_steel_beam"), Cond.R("research_steel_rod"), Cond.P("steel_ingot", 50) } },
        new Milestone { id = "power", reward = 5000, conds = new[] { Cond.R("research_power_generator"), Cond.B("power_generator"), Cond.R("research_reliability_1") } },
        new Milestone { id = "motor", reward = 6000, conds = new[] { Cond.R("research_motor"), Cond.P("motor", 20) } },
        new Milestone { id = "storage", reward = 6000, conds = new[] { Cond.R("research_storage"), Cond.B("storage_container"), Cond.R("research_robotic_arm"), Cond.B("robotic_arm") } },
        new Milestone { id = "fluids", reward = 8000, conds = new[] { Cond.R("research_oil_extractor"), Cond.R("research_refinery"), Cond.B("pipe", 5), Cond.P("plastic", 50) } },
        new Milestone { id = "chemistry", reward = 9000, conds = new[] { Cond.R("research_sulfuric_acid"), Cond.R("research_chemical_plant"), Cond.R("research_battery"), Cond.P("battery", 20) } },
        new Milestone { id = "drones", reward = 9000, conds = new[] { Cond.R("research_drones"), Cond.B("drone_load_station"), Cond.B("drone_unload_station") } },
        new Milestone { id = "computers", reward = 12000, conds = new[] { Cond.R("research_computer_chip"), Cond.R("research_cable"), Cond.R("research_advanced_circuit"), Cond.P("advanced_circuit", 20) } },
        new Milestone { id = "final", reward = 20000, conds = new[] { Cond.R("research_quantum_core"), Cond.R("research_ai_module"), Cond.P("ai_module", 1) } },
    };

    public static GoalSystem Instance { get; private set; }

    /// <summary>Цели видны: обучение закончено или пропущено.</summary>
    public bool Active => TutorialSystem.Instance == null || !TutorialSystem.Instance.IsRunning;
    public Milestone Current { get; private set; }
    public ItemData TrackedItem { get; private set; }
    public bool IsMilestoneDone(string id) => done.Contains(id);
    public IReadOnlyList<string> HintsSeen => hintsSeen;

    /// <summary>Подсказка на экране сейчас (null — нет).</summary>
    public HintDef ShowingHint { get; private set; }
    public float ShowingSince { get; private set; }

    public event Action Changed;

    readonly HashSet<string> done = new HashSet<string>();
    readonly List<string> hintsSeen = new List<string>();
    readonly Queue<HintDef> hintQueue = new Queue<HintDef>();
    readonly Dictionary<string, float> condSince = new Dictionary<string, float>();
    float nextCheck;
    float nextHint;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        float now = Time.unscaledTime;
        if (now >= nextCheck)
        {
            nextCheck = now + CheckEvery;
            if (Active)
                CheckMilestone();
        }

        if (now >= nextHint)
        {
            nextHint = now + HintEvery;
            ScanHints();
        }

        if (ShowingHint != null && now - ShowingSince > HintShowSeconds)
        {
            ShowingHint = null;
            Changed?.Invoke();
        }

        if (ShowingHint == null && hintQueue.Count > 0)
        {
            ShowingHint = hintQueue.Dequeue();
            ShowingSince = now;
            UiAudio.PlayOpen();
            Changed?.Invoke();
        }
    }

    // ---------- Вехи ----------

    void CheckMilestone()
    {
        Milestone m = FirstOpen();
        if (m != Current)
        {
            Current = m;
            Changed?.Invoke();
        }

        if (m == null)
            return;
        for (int i = 0; i < m.conds.Length; i++)
        {
            if (!IsDone(m.conds[i]))
                return;
        }

        done.Add(m.id);
        if (PlayerWallet.Instance != null && m.reward > 0)
            PlayerWallet.Instance.AddCoins(m.reward, MoneySource.Research);
        UiNotification.Push(UiLocale.T("goal.done", m.Title), UiLocale.T("goal.reward", IndustryUi.Money(m.reward)), UiStatus.Completed);
        UiAudio.PlayConfirm();
        Current = FirstOpen();
        Changed?.Invoke();
    }

    Milestone FirstOpen()
    {
        for (int i = 0; i < Milestones.Length; i++)
        {
            if (!done.Contains(Milestones[i].id))
                return Milestones[i];
        }

        return null;
    }

    public bool IsDone(Cond c)
    {
        return Progress(c) >= c.count;
    }

    public int Progress(Cond c)
    {
        switch (c.kind)
        {
            case CondKind.Research:
                return TutorialSystem.HasResearch(c.id) ? 1 : 0;
            case CondKind.Build:
                return TutorialSystem.CountBuildings(c.id);
            case CondKind.Produce:
                ItemData item = GameDatabase.FindItem(c.id);
                return item != null && ProductionStats.Instance != null ? ProductionStats.Instance.TotalProduced(item) : 0;
            default:
                return 0;
        }
    }

    public static string CondText(Cond c)
    {
        switch (c.kind)
        {
            case CondKind.Research:
                ResearchNodeData r = GameDatabase.FindResearch(c.id);
                return UiLocale.T("goal.c_research", r != null ? r.Title : c.id);
            case CondKind.Build:
                BuildingData b = GameDatabase.FindBuilding(c.id);
                return UiLocale.T("goal.c_build", b != null ? b.Title : c.id);
            default:
                ItemData item = GameDatabase.FindItem(c.id);
                return UiLocale.T("goal.c_produce", item != null ? item.Title : c.id);
        }
    }

    /// <summary>Предмет, к которому ведёт условие (для перехода на Карту производства).</summary>
    public static ItemData CondItem(Cond c)
    {
        if (c.kind == CondKind.Produce)
            return GameDatabase.FindItem(c.id);
        if (c.kind == CondKind.Research)
        {
            ResearchNodeData r = GameDatabase.FindResearch(c.id);
            if (r != null && r.unlockedRecipes != null && r.unlockedRecipes.Count > 0 && r.unlockedRecipes[0] != null)
                return IndustryUi.FirstItem(r.unlockedRecipes[0].outputs);
            if (r != null && r.requiredItems != null && r.requiredItems.Count > 0)
                return r.requiredItems[0].item;
        }

        return null;
    }

    // ---------- Отслеживание цепочки ----------

    public void Track(ItemData item)
    {
        TrackedItem = item;
        Changed?.Invoke();
    }

    /// <summary>Шаги отслеживаемой цепочки: предмет + «есть ли уже» (для сырья — экстрактор на жиле, для крафта — сделан хоть раз).</summary>
    public List<(ItemData item, bool ok)> TrackedSteps()
    {
        var list = new List<(ItemData, bool)>(8);
        if (TrackedItem == null)
            return list;
        var seen = new HashSet<ItemData>();
        Collect(ProductionGraph.Get(TrackedItem), seen, list, 0);
        list.Reverse(); // от руды к цели
        return list;
    }

    static void Collect(ProductionGraph.Node n, HashSet<ItemData> seen, List<(ItemData, bool)> list, int depth)
    {
        if (n == null || depth > 9 || !seen.Add(n.item))
            return;
        bool ok;
        if (n.IsRaw)
            ok = n.extractor != null && (TutorialSystem.CountExtractors(n.item.id) > 0 || Produced(n.item) > 0);
        else
            ok = Produced(n.item) > 0;
        list.Add((n.item, ok));
        RecipeData main = n.MainRecipe;
        for (int i = 0; main != null && main.inputs != null && i < main.inputs.Count; i++)
            Collect(ProductionGraph.Get(main.inputs[i].item), seen, list, depth + 1);
    }

    static int Produced(ItemData item)
    {
        return ProductionStats.Instance != null ? ProductionStats.Instance.TotalProduced(item) : 0;
    }

    // ---------- Подсказки «в первый раз» ----------

    public class HintDef
    {
        public string id;
        public bool important;
        /// <summary>Когда показать (один раз на мир).</summary>
        public Func<GoalSystem, bool> when;
        /// <summary>Текст с живыми клавишами — и для карточки, и для справочника.</summary>
        public Func<string> text;
        public string Title => UiLocale.T("tip." + id + ".title");
        public string Text => text != null ? text() : "";
    }

    static readonly HintDef[] Hints =
    {
        new HintDef { id = "no_recipe", important = true, when = g => g.Held("no_recipe", AnyIdle("recipe"), 6f), text = () => UiLocale.T("tip.no_recipe", KeybindStore.Hint("Interact")) },
        new HintDef { id = "wrong_item", important = true, when = g => g.Held("wrong_item", AnyIdle("wrong"), 4f), text = () => UiLocale.T("tip.wrong_item") },
        new HintDef { id = "extractor_jam", important = true, when = g => g.Held("extractor_jam", AnyExtractorJam(), 8f), text = () => UiLocale.T("tip.extractor_jam", KeybindStore.Hint("Rotate")) },
        new HintDef { id = "two_inputs", important = true, when = g => Unlocked("constructor"), text = () => UiLocale.T("tip.two_inputs", KeybindStore.Hint("ProductionMap")) },
        new HintDef { id = "second_input", important = true, when = g => g.Held("second_input", ConstructorHalfFed(), 15f), text = () => UiLocale.T("tip.second_input") },
        new HintDef { id = "research_done", important = false, when = g => CountResearch() >= 2, text = () => UiLocale.T("tip.research_done", KeybindStore.Hint("Research")) },
        new HintDef { id = "map", important = true, when = g => TutorialSystem.HasResearch("research_assembler"), text = () => UiLocale.T("tip.map", KeybindStore.Hint("ProductionMap")) },
        new HintDef { id = "hotbar_full", important = false, when = g => HotbarFull(), text = () => UiLocale.T("tip.hotbar_full", KeybindStore.Hint("Inventory")) },
        new HintDef { id = "night", important = true, when = g => DayNight.Hour >= BreakdownSystem.NightStart - 1f && WorldSim.Buildings.Count > 6, text = () => UiLocale.T("tip.night") },
        new HintDef { id = "breakdown", important = true, when = g => AnyBroken(), text = () => UiLocale.T("tip.breakdown", KeybindStore.Hint("Interact")) },
        new HintDef { id = "copy", important = false, when = g => CountCrafters() >= 4, text = () => UiLocale.T("tip.copy", KeybindStore.Hint("SelectMode"), KeybindStore.Hint("Copy"), KeybindStore.Hint("Paste")) },
        new HintDef { id = "pipe", important = true, when = g => Unlocked("pipe"), text = () => UiLocale.T("tip.pipe") },
        new HintDef { id = "refinery_flip", important = true, when = g => RefineryOn("recipe_sulfuric_acid"), text = () => UiLocale.T("tip.refinery_flip") },
        new HintDef { id = "long_belts", important = false, when = g => TutorialSystem.CountBuildings("conveyor") > 80, text = () => UiLocale.T("tip.long_belts") },
        new HintDef { id = "idle_list", important = false, when = g => IdleCount() >= 3, text = () => UiLocale.T("tip.idle_list", KeybindStore.Hint("Research")) },
    };

    public static IReadOnlyList<HintDef> AllHints => Hints;

    public static HintDef FindHint(string id)
    {
        for (int i = 0; i < Hints.Length; i++)
        {
            if (Hints[i].id == id)
                return Hints[i];
        }

        return null;
    }

    void ScanHints()
    {
        if (!Active || GameSettings.Hints >= 2)
            return;
        bool onlyImportant = GameSettings.Hints == 1;
        for (int i = 0; i < Hints.Length; i++)
        {
            HintDef h = Hints[i];
            if (hintsSeen.Contains(h.id) || (onlyImportant && !h.important))
                continue;
            bool fire = false;
            try
            {
                fire = h.when(this);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Hints] " + h.id + ": " + e.Message);
            }

            if (!fire)
                continue;
            hintsSeen.Add(h.id);
            hintQueue.Enqueue(h);
        }
    }

    public void DismissHint()
    {
        if (ShowingHint == null)
            return;
        ShowingHint = null;
        Changed?.Invoke();
    }

    /// <summary>Условие держится непрерывно <paramref name="seconds"/> секунд.</summary>
    bool Held(string key, bool now, float seconds)
    {
        if (!now)
        {
            condSince.Remove(key);
            return false;
        }

        if (!condSince.TryGetValue(key, out float since))
        {
            condSince[key] = Time.unscaledTime;
            return false;
        }

        return Time.unscaledTime - since >= seconds;
    }

    static bool AnyIdle(string reason)
    {
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] is CrafterBuilding c && c != null && c.IsPlaced && !c.IsBroken && c.IdleReason() == reason)
                return true;
        }

        return false;
    }

    static int IdleCount()
    {
        return MachineIdleHud.Instance != null ? MachineIdleHud.Instance.Rows.Count : 0;
    }

    static bool AnyExtractorJam()
    {
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] is Extractor e && e != null && e.IsPlaced && e.IsOutputJammed)
                return true;
        }

        return false;
    }

    /// <summary>Конструктор с рецептом на 2 ингредиента: один есть, второго нет.</summary>
    static bool ConstructorHalfFed()
    {
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            if (!(all[i] is CrafterBuilding c) || c == null || c.currentRecipe == null || c.currentRecipe.inputs == null)
                continue;
            if (c.currentRecipe.inputs.Count < 2 || c.IdleReason() != "input")
                continue;
            bool any = false;
            for (int k = 0; k < c.currentRecipe.inputs.Count; k++)
            {
                if (c.CountInput(c.currentRecipe.inputs[k].item) > 0)
                    any = true;
            }

            if (any)
                return true;
        }

        return false;
    }

    static bool Unlocked(string buildingId)
    {
        BuildingData b = GameDatabase.FindBuilding(buildingId);
        return b != null && ResearchSystem.Instance != null && ResearchSystem.Instance.IsBuildingUnlocked(b);
    }

    static int CountResearch()
    {
        ResearchNodeData[] all = GameDatabase.AllResearches();
        int n = 0;
        for (int i = 0; all != null && i < all.Length; i++)
        {
            if (all[i] != null && ResearchSystem.Instance != null && ResearchSystem.Instance.IsResearchUnlocked(all[i]))
                n++;
        }

        return n;
    }

    static bool HotbarFull()
    {
        PlayerInventory inv = TutorialSystem.Inventory;
        if (inv == null || inv.hotbar == null || ResearchSystem.Instance == null)
            return false;
        int unlocked = 0;
        BuildingData[] all = GameDatabase.AllBuildings();
        for (int i = 0; all != null && i < all.Length; i++)
        {
            if (all[i] != null && ResearchSystem.Instance.IsBuildingUnlocked(all[i]))
                unlocked++;
        }

        return unlocked > inv.hotbar.Length;
    }

    static bool AnyBroken()
    {
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].IsBroken)
                return true;
        }

        return false;
    }

    static int CountCrafters()
    {
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        int n = 0;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] is CrafterBuilding)
                n++;
        }

        return n;
    }

    static bool RefineryOn(string recipeId)
    {
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] is Refinery r && r != null && r.currentRecipe != null && TutorialSystem.IdsEqual(r.currentRecipe.id, recipeId))
                return true;
        }

        return false;
    }

    // ---------- Сейв ----------

    public void CaptureSave(SaveData data)
    {
        if (data == null)
            return;
        data.milestonesDone = new List<string>(done);
        data.hintsSeen = new List<string>(hintsSeen);
        data.trackedItemId = TrackedItem != null ? TrackedItem.id : "";
    }

    public void ApplySave(SaveData data)
    {
        done.Clear();
        hintsSeen.Clear();
        hintQueue.Clear();
        condSince.Clear();
        ShowingHint = null;
        TrackedItem = null;
        Current = null;
        if (data != null)
        {
            if (data.milestonesDone != null)
            {
                for (int i = 0; i < data.milestonesDone.Count; i++)
                    done.Add(data.milestonesDone[i]);
            }

            if (data.hintsSeen != null)
                hintsSeen.AddRange(data.hintsSeen);
            TrackedItem = GameDatabase.FindItem(data.trackedItemId);
            // Старый мир без вех: молча засчитать уже выполненные, без наград.
            if (data.milestonesDone == null || data.milestonesDone.Count == 0)
                MarkAlreadyDone();
        }

        Changed?.Invoke();
    }

    void MarkAlreadyDone()
    {
        for (int i = 0; i < Milestones.Length; i++)
        {
            Milestone m = Milestones[i];
            bool all = true;
            for (int c = 0; c < m.conds.Length && all; c++)
            {
                if (m.conds[c].kind == CondKind.Research && !IsDone(m.conds[c]))
                    all = false;
            }

            if (!all)
                break;
            done.Add(m.id);
        }
    }
}
