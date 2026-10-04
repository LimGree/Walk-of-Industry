using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Шаги вводной главы. Один шаг — одно действие. Порядок: руда → лаборатория → лента (результат
/// на 2-й минуте) → медь → плавильня → копия линии. Числа — rev 2 сценария (см. <see cref="TutorialSystem.Rev"/>).
/// </summary>
public enum TutorialStep
{
    Intro = 0,
    GoToIron = 1,
    BuildMode = 2,
    PlaceExtractor = 3,
    WatchOutput = 4,
    PlaceLab = 5,
    RotateBuilding = 6,
    DragBelt = 7,
    FirstBelt = 8,
    FirstOreIn = 9,
    GoToCopper = 10,
    CopperLine = 11,
    SpeedUp = 12,
    Unlocked = 13,
    PlaceSmelter = 14,
    FirstIngot = 15,
    CopyLine = 16,
    PasteLine = 17,
    OpenMachine = 18,
    Handoff = 19
}

public class TutorialSystem : MonoBehaviour
{
    /// <summary>Версия сценария в сейве. 0/1 — старые 19 шагов, 2 — без шагов поворота/протяжки, 3 — эта.</summary>
    public const int Rev = 3;

    /// <summary>Сколько лент за один штрих засчитывает шаг «протяни линию».</summary>
    const int DragBeltMin = 3;

    public const string BasicId = "research_smelter";
    public const string IronOreId = "iron_ore";
    public const string CopperOreId = "cooper_ore";
    public const string IronIngotId = "iron_ingot";
    public const string CopperIngotId = "cooper_Ingot";
    public const string IronIngotRecipeId = "recipe_iron_ingot";
    public const string CopperIngotRecipeId = "recipe_cooper_ingot";
    public const string ExtractorId = "extractor";
    public const string ConveyorId = "conveyor";
    public const string LabId = "research_lab";
    public const string SmelterId = "smelter";

    /// <summary>Шаги-«карточки»: сами уходят через столько секунд.</summary>
    const float IntroTime = 5f;
    const float WatchTime = 4f;
    const float CardTime = 4.5f;
    const float NearVein = 14f;

    public static TutorialSystem Instance { get; private set; }

    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }
    public bool BlocksHotbarAutofill => false;
    public bool BlocksPause => false;
    public bool IsModal => false;
    public TutorialStep Step { get; private set; }
    public float StepAge => Time.unscaledTime - stepEnteredAt;

    /// <summary>Шаг, который игрок может пропустить без потери смысла (ускорение ожидания).</summary>
    public bool StepIsOptional => Step == TutorialStep.SpeedUp;

    public event System.Action Changed;
    /// <summary>Обучение закончено (пройдено или пропущено) — включаются цели.</summary>
    public event System.Action Finished;

    float stepEnteredAt;
    float lastWaitOre;
    float lastWaitCheck;
    bool skipped;
    bool machineOpened;
    bool rotatedThisStep;
    bool draggedLine;
    TutorialFx fx;
    TutorialLog log;

    void Awake()
    {
        Instance = this;
        fx = GetComponent<TutorialFx>();
        if (fx == null)
            fx = gameObject.AddComponent<TutorialFx>();
        log = new TutorialLog();
        PlayerBuilder.Rotated += OnRotated;
        PlayerBuilder.LineStrokePlaced += OnLineStrokePlaced;
    }

    void OnDestroy()
    {
        PlayerBuilder.Rotated -= OnRotated;
        PlayerBuilder.LineStrokePlaced -= OnLineStrokePlaced;
        if (Instance == this)
            Instance = null;
    }

    void OnRotated()
    {
        if (IsRunning && Step == TutorialStep.RotateBuilding)
            rotatedThisStep = true;
    }

    void OnLineStrokePlaced(BuildingData data, int count)
    {
        if (IsRunning && data != null && data.IsConveyor && count >= DragBeltMin)
            draggedLine = true;
    }

    void Update()
    {
        if (!IsRunning)
        {
            fx?.Clear();
            return;
        }

        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen
            && MachineUI.Instance.CurrentBuilding is CrafterBuilding)
            machineOpened = true;

        // Финальная карточка: K открыл карту производства — обучение закончено.
        if (Step == TutorialStep.Handoff && ProductionMapUI.Instance != null && ProductionMapUI.Instance.IsOpen)
        {
            FinishHandoff();
            return;
        }

        Advance();
        fx?.Sync();
    }

    // ---------- Жизненный цикл ----------

    public void PrepareFromSave(bool hadSave, SaveData data)
    {
        if (!hadSave || data == null)
        {
            IsRunning = true;
            IsFinished = false;
            Step = TutorialStep.Intro;
            return;
        }

        if (data.version < 9 || data.tutorialFinished)
        {
            Stop(finished: true);
            return;
        }

        if (data.tutorialSkipped)
        {
            skipped = true;
            Stop(finished: true);
            return;
        }

        if (AlreadyPastTutorial())
        {
            Stop(finished: true);
            return;
        }

        IsRunning = true;
        IsFinished = false;
        int step = data.tutorialRev >= Rev
            ? data.tutorialStep
            : data.tutorialRev == 2 ? MapRev2Step(data.tutorialStep) : MapOldStep(data.tutorialStep);
        Step = (TutorialStep)Mathf.Clamp(step, 0, (int)TutorialStep.Handoff);
        stepEnteredAt = Time.unscaledTime;
    }

    /// <summary>Rev 2 → 3: перед первой лентой вставлены «поворот» и «протяжка».</summary>
    static int MapRev2Step(int old)
    {
        return old >= 6 ? old + 2 : old;
    }

    /// <summary>Старые 19 шагов → новые. Дальше <see cref="Advance"/> сам проскочит выполненное.</summary>
    static int MapOldStep(int old)
    {
        switch (old)
        {
            case 0: return (int)TutorialStep.Intro;
            case 1: return (int)TutorialStep.GoToIron;
            case 2: return (int)TutorialStep.BuildMode;
            case 3:
            case 4:
            case 5: return (int)TutorialStep.PlaceExtractor;
            case 6:
            case 7: return (int)TutorialStep.GoToCopper;
            case 8: return (int)TutorialStep.PlaceLab;
            case 9:
            case 10: return (int)TutorialStep.FirstBelt;
            case 11: return (int)TutorialStep.SpeedUp;
            case 12:
            case 13:
            case 14: return (int)TutorialStep.PlaceSmelter;
            case 15: return (int)TutorialStep.FirstIngot;
            case 16:
            case 17: return (int)TutorialStep.CopyLine;
            default: return (int)TutorialStep.Handoff;
        }
    }

    public void OnWorldReady(bool hadSave, SaveData data)
    {
        PrepareFromSave(hadSave, data);
        if (!IsRunning)
        {
            Finished?.Invoke();
            return;
        }

        // «Пропускать обучение в новых мирах».
        if (!hadSave && GameSettings.TutorialSkip)
        {
            Skip();
            return;
        }

        FillHotbar();
        if (!hadSave)
            SetStep(TutorialStep.Intro, force: true);

        Changed?.Invoke();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    public void Skip()
    {
        if (!IsRunning)
            return;
        skipped = true;
        log.Note(Step, "skip-all");
        Finish();
    }

    public void Restart()
    {
        skipped = false;
        IsFinished = false;
        IsRunning = true;
        machineOpened = false;
        draggedLine = false;
        SetStep(TutorialStep.Intro, force: true);
        fx?.Clear();
        Changed?.Invoke();
    }

    public void RepeatStep()
    {
        if (!IsRunning)
            return;
        log.Note(Step, "repeat");
        SetStep(Step, force: true);
    }

    public void SkipStep()
    {
        if (!IsRunning)
            return;
        log.Note(Step, "skip-step");
        if (Step >= TutorialStep.Handoff)
        {
            FinishHandoff();
            return;
        }

        SetStep(Step + 1);
    }

    public void JumpTo(TutorialStep target)
    {
        if (!IsRunning)
            return;
        SetStep(target, force: true);
    }

    public void FinishHandoff()
    {
        if (!IsRunning)
            return;
        Finish();
    }

    void Finish()
    {
        Stop(finished: true);
        FillHotbar();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        Changed?.Invoke();
        Finished?.Invoke();
    }

    public void CaptureSave(SaveData data)
    {
        if (data == null)
            return;
        data.tutorialStep = (int)Step;
        data.tutorialRev = Rev;
        data.tutorialFinished = IsFinished;
        data.tutorialSkipped = skipped;
    }

    public void ApplySave(SaveData data)
    {
        PrepareFromSave(data != null, data);
    }

    void Stop(bool finished)
    {
        IsRunning = false;
        IsFinished = finished;
        fx?.Clear();
    }

    void SetStep(TutorialStep next, bool force = false)
    {
        if (!force && Step == next && stepEnteredAt > 0f)
            return;
        if (Step != next)
            log.Leave(Step, StepAge);
        Step = next;
        stepEnteredAt = Time.unscaledTime;
        rotatedThisStep = false;
        lastWaitOre = CurrentOreProgress();
        lastWaitCheck = Time.unscaledTime;
        Reward(next);
        Changed?.Invoke();
    }

    void Advance()
    {
        int guard = 0;
        while (IsRunning && Step < TutorialStep.Handoff && IsSatisfied(Step) && guard++ < 20)
            SetStep(Step + 1);
        if (IsRunning && Step == TutorialStep.Handoff && StepAge > 12f)
            FinishHandoff();
    }

    /// <summary>Тост-награда при входе в шаг, который сам по себе — результат.</summary>
    static void Reward(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.FirstOreIn:
                UiNotification.Push(UiLocale.T("tut.reward.ore"), UiLocale.T("tut.reward.ore_sub"), UiStatus.Completed);
                break;
            case TutorialStep.Unlocked:
                UiNotification.Push(UiLocale.T("tut.reward.smelter"), UiLocale.T("tut.reward.smelter_sub"), UiStatus.Completed);
                break;
            case TutorialStep.CopyLine:
                UiNotification.Push(UiLocale.T("tut.reward.ingot"), "", UiStatus.Completed);
                break;
        }
    }

    bool IsSatisfied(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.Intro:
                return StepAge > IntroTime || CountExtractors(IronOreId) > 0;
            case TutorialStep.GoToIron:
                return CountExtractors(IronOreId) > 0 || DistanceToVein(IronOreId) < NearVein;
            case TutorialStep.BuildMode:
                return CountExtractors(IronOreId) > 0 || (Builder != null && Builder.isBuildMode);
            case TutorialStep.PlaceExtractor:
                return CountExtractors(IronOreId) >= 1;
            case TutorialStep.WatchOutput:
                return StepAge > WatchTime || FindLab() != null;
            case TutorialStep.PlaceLab:
                return FindLab() != null;
            case TutorialStep.RotateBuilding:
                return rotatedThisStep || HasResearch(BasicId) || Submitted(IronOreId) > 0 || Submitted(CopperOreId) > 0;
            case TutorialStep.DragBelt:
                return draggedLine || HasResearch(BasicId) || Submitted(IronOreId) > 0 || Submitted(CopperOreId) > 0;
            case TutorialStep.FirstBelt:
                return HasResearch(BasicId) || Submitted(IronOreId) > 0 || Submitted(CopperOreId) > 0;
            case TutorialStep.FirstOreIn:
                return StepAge > CardTime;
            case TutorialStep.GoToCopper:
                return HasResearch(BasicId) || CountExtractors(CopperOreId) > 0 || DistanceToVein(CopperOreId) < NearVein;
            case TutorialStep.CopperLine:
                return HasResearch(BasicId) || Submitted(CopperOreId) > 0;
            case TutorialStep.SpeedUp:
                return HasResearch(BasicId);
            case TutorialStep.Unlocked:
                return StepAge > CardTime;
            case TutorialStep.PlaceSmelter:
                return CountBuildings(SmelterId) >= 1;
            case TutorialStep.FirstIngot:
                return SmelterMade(IronIngotId) || SmelterMade(CopperIngotId);
            case TutorialStep.CopyLine:
                return CountClipboard(SmelterId) >= 1 || CountBuildings(SmelterId) >= 2;
            case TutorialStep.PasteLine:
                return CountBuildings(SmelterId) >= 2;
            case TutorialStep.OpenMachine:
                return machineOpened;
            default:
                return false;
        }
    }

    bool AlreadyPastTutorial()
    {
        return HasResearch(BasicId) && CountBuildings(SmelterId) >= 2 && HasSmelterRecipe(CopperIngotRecipeId);
    }

    // ---------- Цель шага в мире (для стрелки, столба света, маркера карты) ----------

    /// <summary>Куда смотреть на этом шаге. false — указатель не нужен.</summary>
    public bool TryGetTarget(out Vector3 pos)
    {
        pos = default;
        Vector3 from = PlayerPos;
        switch (Step)
        {
            case TutorialStep.Intro:
            case TutorialStep.GoToIron:
            case TutorialStep.BuildMode:
            case TutorialStep.PlaceExtractor:
                return TryNearestVein(IronOreId, from, out pos);
            case TutorialStep.WatchOutput:
            case TutorialStep.PlaceLab:
                return TryFirstExtractor(IronOreId, out pos);
            case TutorialStep.RotateBuilding:
            case TutorialStep.DragBelt:
            case TutorialStep.FirstBelt:
            case TutorialStep.FirstOreIn:
            case TutorialStep.SpeedUp:
                ResearchLab lab = FindLab();
                if (lab == null)
                    return false;
                pos = lab.transform.position;
                return true;
            case TutorialStep.GoToCopper:
            case TutorialStep.CopperLine:
                if (CountExtractors(CopperOreId) > 0 && Step == TutorialStep.CopperLine)
                    return TryFirstExtractor(CopperOreId, out pos);
                return TryNearestVein(CopperOreId, from, out pos);
            case TutorialStep.PlaceSmelter:
                return TryFirstExtractor(IronOreId, out pos);
            case TutorialStep.FirstIngot:
            case TutorialStep.CopyLine:
            case TutorialStep.OpenMachine:
                return TryFirstBuilding(SmelterId, out pos);
            case TutorialStep.PasteLine:
                return TryFirstExtractor(CopperOreId, out pos);
            default:
                return false;
        }
    }

    // Ближайшая жила считается перебором всех жил: шаг, Fx и стрелка спрашивают каждый кадр — кэш на 0.5 с.
    static readonly Dictionary<string, (float at, bool ok, Vector3 pos)> veinCache =
        new Dictionary<string, (float, bool, Vector3)>();

    static bool TryNearestVein(string resourceId, Vector3 from, out Vector3 pos)
    {
        float now = Time.unscaledTime;
        if (veinCache.TryGetValue(resourceId, out var hit) && now - hit.at < 0.5f)
        {
            pos = hit.pos;
            return hit.ok;
        }

        var cells = new List<Vector2Int>(1);
        CollectVeinCells(resourceId, cells, 1, from);
        bool ok = cells.Count > 0;
        pos = ok ? CellWorld(cells[0]) : default;
        veinCache[resourceId] = (now, ok, pos);
        return ok;
    }

    static bool TryFirstExtractor(string resourceId, out Vector3 pos)
    {
        Extractor e = FirstExtractor(resourceId);
        pos = e != null ? e.transform.position : default;
        return e != null;
    }

    static bool TryFirstBuilding(string id, out Vector3 pos)
    {
        BuildingBase[] list = Object.FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && list[i].data != null && IdsEqual(list[i].data.id, id))
            {
                pos = list[i].transform.position;
                return true;
            }
        }

        pos = default;
        return false;
    }

    public static Extractor FirstExtractor(string resourceId)
    {
        Extractor[] list = Object.FindObjectsByType<Extractor>(FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            Extractor e = list[i];
            if (e != null && e.resource != null && IdsEqual(e.resource.id, resourceId))
                return e;
        }

        return null;
    }

    static float DistanceToVein(string resourceId)
    {
        Vector3 from = PlayerPos;
        if (!TryNearestVein(resourceId, from, out Vector3 pos))
            return 0f;
        Vector2 a = new Vector2(from.x, from.z);
        Vector2 b = new Vector2(pos.x, pos.z);
        return Vector2.Distance(a, b);
    }

    // ---------- Ожидание исследования ----------

    public bool WaitStuck =>
        Step == TutorialStep.SpeedUp
        && Time.unscaledTime - lastWaitCheck > 45f
        && CurrentOreProgress() <= lastWaitOre + 0.5f;

    public void NoteWaitProgress()
    {
        float now = CurrentOreProgress();
        if (now > lastWaitOre + 0.5f)
        {
            lastWaitOre = now;
            lastWaitCheck = Time.unscaledTime;
        }
    }

    float CurrentOreProgress()
    {
        return Submitted(IronOreId) + Submitted(CopperOreId);
    }

    /// <summary>Грубая оценка секунд до конца исследования по текущему темпу (0 — нет данных).</summary>
    public float EstimateWaitSeconds()
    {
        int left = Mathf.Max(0, Required(IronOreId) - Submitted(IronOreId))
                   + Mathf.Max(0, Required(CopperOreId) - Submitted(CopperOreId));
        if (left <= 0)
            return 0f;
        float rate = 0f;
        Extractor[] list = Object.FindObjectsByType<Extractor>(FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            Extractor e = list[i];
            if (e == null || e.resource == null)
                continue;
            if (!IdsEqual(e.resource.id, IronOreId) && !IdsEqual(e.resource.id, CopperOreId))
                continue;
            rate += 1f / Mathf.Max(0.05f, e.CurrentInterval);
        }

        return rate > 0.01f ? left / rate : 0f;
    }

    public int Submitted(string itemId)
    {
        if (ResearchSystem.Instance == null)
            return 0;
        ItemData item = GameDatabase.FindItem(itemId);
        return item != null ? ResearchSystem.Instance.GetSubmitted(item) : 0;
    }

    public int Required(string itemId)
    {
        ResearchNodeData node = GameDatabase.FindResearch(BasicId);
        if (node == null || node.requiredItems == null)
            return 1;
        ItemData item = GameDatabase.FindItem(itemId);
        for (int i = 0; i < node.requiredItems.Count; i++)
        {
            if (node.requiredItems[i].item == item)
                return Mathf.Max(1, node.requiredItems[i].amount);
        }
        return 1;
    }

    // ---------- Проверки мира (их же используют цели и подсказки) ----------

    public static bool HasResearch(string id)
    {
        return ResearchSystem.Instance != null && ResearchSystem.Instance.IsResearchIdUnlocked(id);
    }

    public static int CountExtractors(string resourceId)
    {
        Extractor[] list = Object.FindObjectsByType<Extractor>(FindObjectsSortMode.None);
        int n = 0;
        for (int i = 0; i < list.Length; i++)
        {
            Extractor e = list[i];
            if (e == null || e.resource == null)
                continue;
            if (IdsEqual(e.resource.id, resourceId))
                n++;
        }
        return n;
    }

    public static int CountBuildings(string buildingId)
    {
        BuildingBase[] list = Object.FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        int n = 0;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && list[i].IsPlaced && list[i].data != null && IdsEqual(list[i].data.id, buildingId))
                n++;
        }
        return n;
    }

    public static ResearchLab FindLab()
    {
        ResearchLab[] labs = Object.FindObjectsByType<ResearchLab>(FindObjectsSortMode.None);
        for (int i = 0; i < labs.Length; i++)
        {
            if (labs[i] != null && labs[i].IsWorldLab)
                return labs[i];
        }
        return labs.Length > 0 ? labs[0] : null;
    }

    public static bool HasSmelterRecipe(string recipeId)
    {
        Smelter[] list = Object.FindObjectsByType<Smelter>(FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            RecipeData recipe = list[i] != null ? list[i].currentRecipe : null;
            if (recipe != null && IdsEqual(recipe.id, recipeId))
                return true;
        }
        return false;
    }

    static bool SmelterMade(string ingotId)
    {
        ItemData ingot = GameDatabase.FindItem(ingotId);
        if (ingot == null)
            return false;
        if (ProductionStats.Instance != null && ProductionStats.Instance.TotalProduced(ingot) > 0)
            return true;
        Smelter[] list = Object.FindObjectsByType<Smelter>(FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && list[i].OutputContains(ingot))
                return true;
        }
        return false;
    }

    static int CountClipboard(string buildingId)
    {
        PlayerBuilder builder = Builder;
        if (builder == null || builder.Selection == null)
            return 0;
        return builder.Selection.CountClipboard(buildingId);
    }

    static void FillHotbar()
    {
        PlayerInventory inv = Inventory;
        if (inv == null)
            return;
        inv.AllowAutofillAndFill();
    }

    public static bool IdsEqual(string a, string b)
    {
        return GameDatabase.Normalize(a) == GameDatabase.Normalize(b);
    }

    public static PlayerBuilder Builder
    {
        get
        {
            return GameManager.Instance != null
                ? GameManager.Instance.playerBuilder
                : Object.FindFirstObjectByType<PlayerBuilder>();
        }
    }

    public static PlayerInventory Inventory
    {
        get
        {
            PlayerBuilder b = Builder;
            return b != null ? b.inventory : Object.FindFirstObjectByType<PlayerInventory>();
        }
    }

    static PlayerMovement Movement
    {
        get
        {
            PlayerBuilder b = Builder;
            if (b != null && b.playerMovement != null)
                return b.playerMovement;
            if (b != null)
            {
                PlayerMovement fromBuilder = b.GetComponent<PlayerMovement>();
                if (fromBuilder != null)
                    return fromBuilder;
            }
            return Object.FindFirstObjectByType<PlayerMovement>();
        }
    }

    public static void CollectVeinCells(string resourceId, List<Vector2Int> dest, int max, Vector3 from)
    {
        dest.Clear();
        WorldResourceScatterer scatter = WorldResourceScatterer.Instance;
        if (scatter == null)
            return;

        var scored = new List<(float d, Vector2Int cell)>(64);
        IReadOnlyList<WorldResourceScatterer.VeinMark> veins = scatter.Veins;
        for (int i = 0; i < veins.Count; i++)
        {
            ResourceNode node = ResourceNode.GetAt(veins[i].cell);
            if (node == null || node.resource == null || !IdsEqual(node.resource.id, resourceId))
                continue;
            Vector3 pos = CellWorld(veins[i].cell);
            float d = (pos - from).sqrMagnitude;
            scored.Add((d, veins[i].cell));
        }

        scored.Sort((a, b) => a.d.CompareTo(b.d));
        int n = Mathf.Min(max, scored.Count);
        for (int i = 0; i < n; i++)
            dest.Add(scored[i].cell);
    }

    public static Vector3 CellWorld(Vector2Int cell)
    {
        if (WorldBiomeMap.Instance != null)
            return WorldBiomeMap.Instance.CellWorld(cell);
        if (GridSystem.Instance != null)
            return GridSystem.Instance.GetCellCenter(cell, 0f);
        return new Vector3(cell.x, 0f, cell.y);
    }

    public static Vector3 PlayerPos
    {
        get
        {
            PlayerMovement m = Movement;
            return m != null ? m.transform.position : Vector3.zero;
        }
    }
}
