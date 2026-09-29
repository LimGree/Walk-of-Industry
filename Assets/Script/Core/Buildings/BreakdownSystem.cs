using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Ночные поломки станков и добычи. Живёт на [[GameManager]].
/// Ночью (21:00–5:00) в случайные моменты ломает до 5% ломаемых зданий за сутки,
/// дальние и работающие — чаще. AFK добавляет поломки, неремонт сутки — ломает соседа.
/// Выключены сутки — раз в 10 минут пара зданий. Чинит игрок мини-игрой ([[RepairUI]]).
/// </summary>
public class BreakdownSystem : MonoBehaviour
{
    public const int MinEligible = 40;
    public const float NightStart = 21f;
    public const float NightEnd = 5f;
    public const float QuarterChance = 0.18f;
    public const float MaxBrokenShare = 0.25f;
    public const float EscalateAfterHours = 24f;
    public const float AfkAfterSeconds = 180f;
    public const float RealModeInterval = 600f;
    public const int RealModeCount = 2;
    public const int RepairAchievementCount = 10;
    public const string ReliabilityResearch1 = "research_reliability_1";
    public const string ReliabilityResearch2 = "research_reliability_2";

    public const int GameWires = 0;
    public const int GameSignal = 1;
    public const int GameEngine = 2;
    public const int GameMath = 3;
    public const int GameCaptcha = 4;
    public const int GameCount = 5;

    public static BreakdownSystem Instance { get; private set; }
    public static event Action Changed;

    static readonly HashSet<BuildingBase> Broken = new HashSet<BuildingBase>();
    static readonly List<BuildingBase> Scratch = new List<BuildingBase>(128);

    float clock;
    int nightKey = int.MinValue;
    int brokenTonight;
    int budgetTonight;
    readonly List<float> plan = new List<float>();
    float nextAfkRoll;
    float realTimer;
    int repairs;

    float lastActivity;
    Vector3 lastCamPos;
    Quaternion lastCamRot;
    float nextScan;
    float loadToastAt;

    readonly Dictionary<BuildingBase, Transform> marks = new Dictionary<BuildingBase, Transform>();
    static Font markFont;
    static Material smokeMat;

    /// <summary>Часы «поломочного времени»: идут как игровые, не прыгают от /timeskip и /time.</summary>
    public float Clock => clock;
    public int RepairsTotal => repairs;
    public static int BrokenCount => CountBroken();
    public bool IsAfk => Time.unscaledTime - lastActivity > AfkAfterSeconds;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        lastActivity = Time.unscaledTime;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        foreach (var pair in marks)
        {
            if (pair.Value != null)
                Destroy(pair.Value.gameObject);
        }

        marks.Clear();
    }

    // ---------- Реестр ----------

    public static void Track(BuildingBase building)
    {
        if (building == null || !building.IsBroken)
            return;
        Broken.Add(building);
        Changed?.Invoke();
    }

    public static void Forget(BuildingBase building)
    {
        if (building == null || !Broken.Remove(building))
            return;
        if (Instance != null)
            Instance.DropMark(building);
        Changed?.Invoke();
    }

    public static bool IsBrokenBuilding(GameObject go)
    {
        if (go == null)
            return false;
        BuildingBase b = go.GetComponent<BuildingBase>();
        return b != null && b.IsBroken;
    }

    public static void CollectBroken(List<BuildingBase> into)
    {
        into.Clear();
        foreach (BuildingBase b in Broken)
        {
            if (b != null && b.IsBroken)
                into.Add(b);
        }
    }

    static int CountBroken()
    {
        int n = 0;
        foreach (BuildingBase b in Broken)
        {
            if (b != null && b.IsBroken)
                n++;
        }

        return n;
    }

    public static bool CanBreak(BuildingBase b)
    {
        return b != null && b.IsPlaced
            && (b is CrafterBuilding || b is Extractor || b is OilExtractor || b is WaterExtractor
                || b is DroneLoadStation || b is DroneUnloadStation);
    }

    static void CollectEligible(List<BuildingBase> into, bool includeBroken)
    {
        into.Clear();
        IReadOnlyList<BuildingBase> all = AllBuildings();
        for (int i = 0; i < all.Count; i++)
        {
            BuildingBase b = all[i];
            if (!CanBreak(b))
                continue;
            if (!includeBroken && b.IsBroken)
                continue;
            into.Add(b);
        }
    }

    static readonly List<BuildingBase> AllScratch = new List<BuildingBase>(256);

    static IReadOnlyList<BuildingBase> AllBuildings()
    {
        AllScratch.Clear();
        AllScratch.AddRange(FindObjectsByType<CrafterBuilding>(FindObjectsSortMode.None));
        AllScratch.AddRange(FindObjectsByType<Extractor>(FindObjectsSortMode.None));
        AllScratch.AddRange(FindObjectsByType<OilExtractor>(FindObjectsSortMode.None));
        AllScratch.AddRange(FindObjectsByType<WaterExtractor>(FindObjectsSortMode.None));
        AllScratch.AddRange(FindObjectsByType<DroneLoadStation>(FindObjectsSortMode.None));
        AllScratch.AddRange(FindObjectsByType<DroneUnloadStation>(FindObjectsSortMode.None));
        return AllScratch;
    }

    public static int EligibleCount()
    {
        CollectEligible(Scratch, true);
        return Scratch.Count;
    }

    // ---------- Баланс ----------

    public static float DailyRate()
    {
        ResearchSystem rs = ResearchSystem.Instance;
        if (rs != null && rs.IsResearchIdUnlocked(ReliabilityResearch2))
            return 0.02f;
        if (rs != null && rs.IsResearchIdUnlocked(ReliabilityResearch1))
            return 0.035f;
        return 0.05f;
    }

    public static int DailyBudget(int eligible)
    {
        if (eligible <= 0)
            return 0;
        return Mathf.Max(1, Mathf.FloorToInt(eligible * DailyRate()));
    }

    /// <summary>Доплата рубинами за долгий неремонт: до 12 ч — 0, до суток — 1, дольше — 2.</summary>
    public int RepairSurcharge(BuildingBase b)
    {
        if (b == null || !b.IsBroken)
            return 0;
        float age = clock - b.BrokenAtClock;
        if (age >= EscalateAfterHours)
            return 2;
        if (age >= 12f)
            return 1;
        return 0;
    }

    public float BrokenAgeHours(BuildingBase b)
    {
        return b != null && b.IsBroken ? Mathf.Max(0f, clock - b.BrokenAtClock) : 0f;
    }

    bool AutoAllowed()
    {
        if (!GameSettings.BreakdownsEnabled)
            return false;
        if (!WorldCatalog.HasActive)
            return false;
        if (TutorialSystem.Instance != null && TutorialSystem.Instance.IsRunning)
            return false;
        if (PhotoMode.IsActive)
            return false;
        return true;
    }

    // ---------- Тик ----------

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        TrackActivity();
        if (loadToastAt > 0f && Time.unscaledTime >= loadToastAt)
        {
            loadToastAt = 0f;
            int broken = CountBroken();
            if (broken > 0)
                UiNotification.Push(
                    UiLocale.T("breakdown.load_title"),
                    UiLocale.T("breakdown.load_body", broken),
                    UiStatus.Warning);
        }

        float dt = Time.deltaTime;
        if (dt > 0f)
            clock += 24f * dt / (Mathf.Max(1f, GameSettings.DayLengthMinutes) * 60f);

        Billboard();
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            SyncMarks();
            if (AutoAllowed())
            {
                if (GameSettings.DayNightEnabled)
                    TickNight();
                else
                    TickRealTime(1f);
                TickEscalation();
            }
        }
    }

    void TrackActivity()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        Transform t = cam.transform;
        if ((t.position - lastCamPos).sqrMagnitude > 0.0025f || Quaternion.Angle(t.rotation, lastCamRot) > 0.5f)
            lastActivity = Time.unscaledTime;
        lastCamPos = t.position;
        lastCamRot = t.rotation;
    }

    static bool IsNightHour(float h)
    {
        return h >= NightStart || h < NightEnd;
    }

    void TickNight()
    {
        float h = DayNight.Hour;
        if (!IsNightHour(h))
        {
            plan.Clear();
            return;
        }

        int key = h >= NightStart ? DayNight.Day : DayNight.Day - 1;
        if (key != nightKey)
            PlanNight(key, h);

        while (plan.Count > 0 && clock >= plan[0])
        {
            plan.RemoveAt(0);
            if (brokenTonight < budgetTonight)
                AutoBreak();
        }

        if (IsAfk && brokenTonight < budgetTonight && clock >= nextAfkRoll)
        {
            nextAfkRoll = clock + 0.5f;
            if (UnityEngine.Random.value < 0.5f)
                AutoBreak();
        }
    }

    void PlanNight(int key, float hour)
    {
        nightKey = key;
        brokenTonight = 0;
        plan.Clear();
        int eligible = EligibleCount();
        budgetTonight = eligible >= MinEligible ? DailyBudget(eligible) : 0;
        nextAfkRoll = clock + 0.5f;
        if (budgetTonight <= 0)
            return;

        float left = hour >= NightStart ? 24f - hour + NightEnd : NightEnd - hour;
        if (left < 0.4f)
            return;
        int count = Mathf.Max(1, Mathf.CeilToInt(budgetTonight * 0.6f));
        // Не пачкой: ночь режется на равные окна, в каждом — случайный момент.
        float slot = (left - 0.2f) / count;
        for (int i = 0; i < count; i++)
            plan.Add(clock + 0.1f + slot * i + UnityEngine.Random.Range(slot * 0.15f, slot * 0.85f));
    }

    void TickRealTime(float dt)
    {
        realTimer += dt * (IsAfk ? 2f : 1f);
        if (realTimer < RealModeInterval)
            return;
        realTimer = 0f;
        int eligible = EligibleCount();
        if (eligible < MinEligible)
            return;
        int room = DailyBudget(eligible) - CountBroken();
        int n = Mathf.Min(RealModeCount, room);
        for (int i = 0; i < n; i++)
            AutoBreak();
    }

    void TickEscalation()
    {
        Scratch.Clear();
        foreach (BuildingBase b in Broken)
        {
            if (b != null && b.IsBroken && !b.BreakEscalated && clock - b.BrokenAtClock >= EscalateAfterHours)
                Scratch.Add(b);
        }

        for (int i = 0; i < Scratch.Count; i++)
        {
            BuildingBase src = Scratch[i];
            src.BreakEscalated = true;
            BuildingBase victim = Nearest(src.transform.position, 40f);
            if (victim == null || !UnderShareCap())
                continue;
            BreakNow(victim, false);
            UiNotification.Push(
                UiLocale.T("breakdown.spread_title"),
                UiLocale.T("breakdown.spread_body", Name(src), Name(victim)),
                UiStatus.Error);
        }
    }

    bool UnderShareCap()
    {
        int eligible = EligibleCount();
        return CountBroken() < Mathf.Max(1, Mathf.FloorToInt(eligible * MaxBrokenShare));
    }

    void AutoBreak()
    {
        if (!UnderShareCap())
            return;
        BuildingBase victim = PickVictim();
        if (victim == null)
            return;
        BreakNow(victim, true);
        brokenTonight++;
    }

    BuildingBase PickVictim()
    {
        CollectEligible(Scratch, false);
        if (Scratch.Count == 0)
            return null;
        Vector3 player = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        float total = 0f;
        var weights = new float[Scratch.Count];
        for (int i = 0; i < Scratch.Count; i++)
        {
            BuildingBase b = Scratch[i];
            float dist = Vector3.Distance(player, b.transform.position);
            float w = (IsWorking(b) ? 3f : 1f) * (1f + dist / 25f);
            weights[i] = w;
            total += w;
        }

        float roll = UnityEngine.Random.value * total;
        for (int i = 0; i < Scratch.Count; i++)
        {
            roll -= weights[i];
            if (roll <= 0f)
                return Scratch[i];
        }

        return Scratch[Scratch.Count - 1];
    }

    static BuildingBase Nearest(Vector3 pos, float radius)
    {
        CollectEligible(Scratch, false);
        BuildingBase best = null;
        float bestD = radius * radius;
        for (int i = 0; i < Scratch.Count; i++)
        {
            float d = (Scratch[i].transform.position - pos).sqrMagnitude;
            if (d > 0.01f && d < bestD)
            {
                bestD = d;
                best = Scratch[i];
            }
        }

        return best;
    }

    static bool IsWorking(BuildingBase b)
    {
        if (b is CrafterBuilding c)
            return c.currentRecipe != null && c.IdleReason() == null;
        if (b is Extractor e)
            return e.resource != null && !e.IsOutputJammed;
        if (b is DroneLoadStation ls)
            return ls.FlyingCount() > 0 || ls.ReadyCount > 0;
        if (b is DroneUnloadStation us)
            return us.Total > 0 || us.IncomingDrones() > 0;
        return b.HasOutputSpace(1);
    }

    // ---------- Поломка / ремонт ----------

    public static int RollGame(BuildingBase b)
    {
        return UnityEngine.Random.Range(0, GameCount);

        // Вариант с распределением по типу здания (пока выключен):
        // if (b is Smelter) return GameEngine;
        // if (b is ChemicalPlant || b is Refinery) return GameWires;
        // if (b is Extractor || b is OilExtractor || b is WaterExtractor) return GameSignal;
        // if (b is Assembler) return UnityEngine.Random.value < 0.5f ? GameMath : GameCaptcha;
        // return UnityEngine.Random.Range(0, GameCount);
    }

    public void BreakNow(BuildingBase b, bool notify)
    {
        if (!CanBreak(b) || b.IsBroken)
            return;
        int mode = UnityEngine.Random.value < QuarterChance ? 2 : 1;
        b.SetBroken(mode, clock, RollGame(b));
        Broken.Add(b);
        EnsureMark(b);
        if (notify)
        {
            // Слышно издалека: звук у камеры, а не у станка.
            Vector3 at = Camera.main != null ? Camera.main.transform.position : b.transform.position;
            GameAudio.World("world_breakdown", at);
            Vector2Int cell = BuildingLinker.WorldToCell(b.transform.position);
            UiNotification.Push(
                UiLocale.T("breakdown.toast_title"),
                UiLocale.T(mode == 2 ? "breakdown.toast_weak" : "breakdown.toast_body", Name(b), cell.x, cell.y),
                UiStatus.Error);
        }
        else
            GameAudio.World("world_breakdown", b.transform.position);

        Changed?.Invoke();
    }

    public void Repair(BuildingBase b, bool byPlayer)
    {
        if (b == null || !b.IsBroken)
            return;
        b.ClearBroken();
        Broken.Remove(b);
        DropMark(b);
        if (byPlayer)
        {
            repairs++;
            if (!AchievementSystem.Mute && AchievementSystem.Instance != null && repairs >= RepairAchievementCount)
                AchievementSystem.Instance.Unlock("mechanic");
        }

        Changed?.Invoke();
    }

    /// <summary>Одна «ночная» поломка прямо сейчас: тот же выбор жертвы и тост (консоль).</summary>
    public void ForceOne()
    {
        AutoBreak();
    }

    /// <summary>Сломать долю ломаемых зданий (консоль). Игнорирует порог 40 и настройку.</summary>
    public int BreakPercent(float percent)
    {
        CollectEligible(Scratch, false);
        int total = EligibleCount();
        int n = Mathf.Clamp(Mathf.CeilToInt(total * Mathf.Clamp01(percent / 100f)), 0, Scratch.Count);
        int done = 0;
        for (int i = 0; i < n; i++)
        {
            BuildingBase victim = PickVictim();
            if (victim == null)
                break;
            BreakNow(victim, false);
            done++;
        }

        return done;
    }

    public int RepairPercent(float percent)
    {
        var list = new List<BuildingBase>();
        CollectBroken(list);
        int n = Mathf.Clamp(Mathf.CeilToInt(list.Count * Mathf.Clamp01(percent / 100f)), 0, list.Count);
        for (int i = 0; i < n; i++)
        {
            int pick = UnityEngine.Random.Range(i, list.Count);
            (list[i], list[pick]) = (list[pick], list[i]);
            Repair(list[i], false);
        }

        return n;
    }

    public string Describe()
    {
        int eligible = EligibleCount();
        var inv = CultureInfo.InvariantCulture;
        string mode = GameSettings.DayNightEnabled ? "night" : "realtime/10min";
        return "broken " + CountBroken() + " / eligible " + eligible
            + " (min " + MinEligible + ")"
            + "\nrate " + (DailyRate() * 100f).ToString("0.#", inv) + "%/day, budget " + DailyBudget(eligible)
            + ", tonight " + brokenTonight + "/" + budgetTonight + ", planned " + plan.Count
            + "\nmode " + mode + ", enabled " + GameSettings.BreakdownsEnabled
            + ", afk " + IsAfk + ", repairs " + repairs
            + "\nclock " + clock.ToString("0.00", inv) + "h";
    }

    static string Name(BuildingBase b)
    {
        return b != null && b.data != null && !string.IsNullOrEmpty(b.data.displayName) ? b.data.displayName : "Building";
    }

    // ---------- Сейв ----------

    public void CaptureSave(SaveData data)
    {
        if (data == null)
            return;
        if (data.extras == null)
            data.extras = new List<SaveKeyValue>();
        var inv = CultureInfo.InvariantCulture;
        data.extras.Add(new SaveKeyValue { key = "breakClock", value = clock.ToString("0.###", inv) });
        data.extras.Add(new SaveKeyValue { key = "breakNight", value = nightKey.ToString(inv) });
        data.extras.Add(new SaveKeyValue { key = "breakTonight", value = brokenTonight.ToString(inv) });
        data.extras.Add(new SaveKeyValue { key = "breakReal", value = realTimer.ToString("0.#", inv) });
        data.extras.Add(new SaveKeyValue { key = "repairs", value = repairs.ToString(inv) });
    }

    public void ApplySave(SaveData data)
    {
        ResetToNewWorld();
        if (data == null || data.extras == null)
            return;
        var inv = CultureInfo.InvariantCulture;
        for (int i = 0; i < data.extras.Count; i++)
        {
            SaveKeyValue row = data.extras[i];
            if (row == null)
                continue;
            switch (row.key)
            {
                case "breakClock": float.TryParse(row.value, NumberStyles.Float, inv, out clock); break;
                case "breakNight": int.TryParse(row.value, NumberStyles.Integer, inv, out nightKey); break;
                case "breakTonight": int.TryParse(row.value, NumberStyles.Integer, inv, out brokenTonight); break;
                case "breakReal": float.TryParse(row.value, NumberStyles.Float, inv, out realTimer); break;
                case "repairs": int.TryParse(row.value, NumberStyles.Integer, inv, out repairs); break;
            }
        }

        // Ночь, в которую сохранились: бюджет тот же, план добьём на остаток ночи.
        if (GameSettings.DayNightEnabled && IsNightHour(DayNight.Hour))
        {
            int key = DayNight.Hour >= NightStart ? DayNight.Day : DayNight.Day - 1;
            if (key == nightKey)
            {
                int done = brokenTonight;
                PlanNight(key, DayNight.Hour);
                brokenTonight = done;
                int keep = Mathf.Max(0, plan.Count - done);
                while (plan.Count > keep)
                    plan.RemoveAt(plan.Count - 1);
            }
        }

        SyncMarks();
        // Сводка чуть позже: при загрузке тост спрятался бы под экраном загрузки.
        if (CountBroken() > 0)
            loadToastAt = Time.unscaledTime + 3f;

        Changed?.Invoke();
    }

    public void ResetToNewWorld()
    {
        clock = 0f;
        nightKey = int.MinValue;
        brokenTonight = 0;
        budgetTonight = 0;
        realTimer = 0f;
        repairs = 0;
        plan.Clear();
        loadToastAt = 0f;
        lastActivity = Time.unscaledTime;
    }

    // ---------- Значок и дым ----------

    void SyncMarks()
    {
        var stale = new List<BuildingBase>();
        foreach (var pair in marks)
        {
            if (pair.Key == null || !pair.Key.IsBroken)
                stale.Add(pair.Key);
        }

        for (int i = 0; i < stale.Count; i++)
            DropMark(stale[i]);

        Broken.RemoveWhere(b => b == null || !b.IsBroken);
        foreach (BuildingBase b in Broken)
            EnsureMark(b);
    }

    void EnsureMark(BuildingBase b)
    {
        if (b == null)
            return;
        if (marks.TryGetValue(b, out Transform existing) && existing != null)
            return;
        marks[b] = CreateMark(b);
    }

    void DropMark(BuildingBase b)
    {
        if (b == null)
        {
            marks.Remove(b);
            return;
        }

        if (!marks.TryGetValue(b, out Transform mark))
            return;
        marks.Remove(b);
        if (mark != null)
            Destroy(mark.gameObject);
    }

    static Transform CreateMark(BuildingBase b)
    {
        var root = new GameObject("BreakdownMark");
        root.transform.SetParent(b.transform, false);
        float top = TopHeight(b);
        root.transform.localPosition = Vector3.zero;

        var bang = new GameObject("BreakBang");
        bang.transform.SetParent(root.transform, false);
        bang.transform.position = b.transform.position + Vector3.up * (top + 0.55f);
        TextMesh tm = bang.AddComponent<TextMesh>();
        tm.text = "!";
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.characterSize = 0.22f;
        tm.fontSize = 64;
        tm.fontStyle = FontStyle.Bold;
        tm.color = new Color(1f, 0.18f, 0.12f, 1f);
        if (markFont == null)
        {
            markFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (markFont == null)
                markFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (markFont != null)
        {
            tm.font = markFont;
            bang.GetComponent<MeshRenderer>().sharedMaterial = markFont.material;
        }

        MeshRenderer rend = bang.GetComponent<MeshRenderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;

        var smoke = new GameObject("BreakSmoke");
        smoke.transform.SetParent(root.transform, false);
        smoke.transform.position = b.transform.position + Vector3.up * Mathf.Max(0.6f, top * 0.8f);
        BuildSmoke(smoke);
        return root.transform;
    }

    static float TopHeight(BuildingBase b)
    {
        Renderer[] rends = b.GetComponentsInChildren<Renderer>();
        float maxY = 1.2f;
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null || !rends[i].enabled || rends[i] is ParticleSystemRenderer)
                continue;
            maxY = Mathf.Max(maxY, rends[i].bounds.max.y - b.transform.position.y);
        }

        return maxY;
    }

    static void BuildSmoke(GameObject go)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.22f, 0.22f, 0.24f, 0.55f),
            new Color(0.42f, 0.42f, 0.44f, 0.45f));
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.04f;

        var emission = ps.emission;
        emission.rateOverTime = 7f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 14f;
        shape.radius = 0.2f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        color.color = grad;

        var rend = go.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        if (smokeMat == null)
        {
            // Свой шейдер частиц: общий Unlit пишет глубину и не читает цвет частицы —
            // края дыма затирали небо и значок «!».
            Shader particle = Resources.Load<Shader>("WalkToBiomeParticle");
            if (particle == null)
                particle = Shader.Find("Hidden/WalkToBiome/Particle");
            if (particle != null)
            {
                smokeMat = new Material(particle) { name = "BreakSmoke" };
                smokeMat.SetTexture("_MainTex", BuildPuff());
                smokeMat.SetColor("_Color", new Color(0.5f, 0.5f, 0.52f, 0.85f));
                RuntimeMaterials.ApplyWorldGfx(smokeMat);
            }
            else
                smokeMat = RuntimeMaterials.Create(BuildPuff(), new Color(0.5f, 0.5f, 0.52f, 0.8f));
        }

        rend.sharedMaterial = smokeMat;
        ps.Play();
    }

    static Texture2D BuildPuff()
    {
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f;
                float dy = (y + 0.5f) / n - 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }

        tex.Apply(false, true);
        return tex;
    }

    void Billboard()
    {
        Camera cam = Camera.main;
        if (cam == null || marks.Count == 0)
            return;
        Vector3 camPos = cam.transform.position;
        foreach (var pair in marks)
        {
            if (pair.Value == null)
                continue;
            Transform bang = pair.Value.Find("BreakBang");
            if (bang == null)
                continue;
            Vector3 toCam = camPos - bang.position;
            if (toCam.sqrMagnitude < 0.01f)
                continue;
            bang.rotation = Quaternion.LookRotation(toCam);
        }
    }
}
