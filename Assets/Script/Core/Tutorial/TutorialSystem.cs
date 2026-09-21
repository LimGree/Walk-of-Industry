using System.Collections.Generic;
using UnityEngine;

public enum TutorialStep
{
    Welcome = 0,
    LookMove = 1,
    BuildMode = 2,
    Hotbar = 3,
    IronOne = 4,
    IronThree = 5,
    Copy = 6,
    PasteCopper = 7,
    Lab = 8,
    StartResearch = 9,
    Belts = 10,
    WaitChapter1 = 11,
    PlaceSmelter = 12,
    ResearchIronIngot = 13,
    PickRecipe = 14,
    FirstSmelt = 15,
    ResearchCopperIngot = 16,
    CopySmelter = 17,
    Farewell = 18
}

public class TutorialSystem : MonoBehaviour
{
    public const string BasicId = "research_smelter";
    public const string IronIngotResearchId = "research_iron_ingot";
    public const string CopperIngotResearchId = "research_cooper_ingot";
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

    public static TutorialSystem Instance { get; private set; }

    public bool IsRunning { get; private set; }
    public bool IsFinished { get; private set; }
    public bool BlocksHotbarAutofill => IsRunning;
    public bool BlocksPause => IsRunning && (Step == TutorialStep.Welcome || Step == TutorialStep.Farewell);
    public bool IsModal => BlocksPause;
    public TutorialStep Step { get; private set; }
    public float StepAge => Time.unscaledTime - stepEnteredAt;

    public event System.Action Changed;

    Vector3 startPos;
    float startYaw;
    float stepEnteredAt;
    float lastWaitOre;
    float lastWaitCheck;
    bool skipped;
    TutorialStep floor;
    TutorialFx fx;

    void Awake()
    {
        Instance = this;
        fx = GetComponent<TutorialFx>();
        if (fx == null)
            fx = gameObject.AddComponent<TutorialFx>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (!IsRunning)
        {
            fx?.Clear();
            return;
        }

        if (Step != TutorialStep.Welcome && Step != TutorialStep.Farewell)
            Advance();

        if (Step >= TutorialStep.Belts && Step <= TutorialStep.WaitChapter1
            && !HasResearch(BasicId) && !IsBasicActive()
            && TutorialStep.StartResearch >= floor)
            SetStep(TutorialStep.StartResearch);

        fx?.Sync();
    }

    public void PrepareFromSave(bool hadSave, SaveData data)
    {
        if (!hadSave || data == null)
        {
            IsRunning = true;
            IsFinished = false;
            Step = TutorialStep.Welcome;
            floor = TutorialStep.Welcome;
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
        int step = Mathf.Clamp(data.tutorialStep, 0, (int)TutorialStep.Farewell);
        Step = (TutorialStep)step;
        floor = Step;
        stepEnteredAt = Time.unscaledTime;
        CapturePose();
    }

    public void OnWorldReady(bool hadSave, SaveData data)
    {
        PrepareFromSave(hadSave, data);
        if (!IsRunning)
            return;

        if (!hadSave)
        {
            ClearHotbar();
            SetStep(TutorialStep.Welcome);
        }

        Changed?.Invoke();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    public void AcceptWelcome()
    {
        if (!IsRunning || Step != TutorialStep.Welcome)
            return;
        SetStep(TutorialStep.LookMove);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    public void Skip()
    {
        if (!IsRunning)
            return;
        skipped = true;
        Stop(finished: true);
        FillHotbar();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        Changed?.Invoke();
    }

    public void Restart()
    {
        skipped = false;
        IsFinished = false;
        IsRunning = true;
        floor = TutorialStep.Welcome;
        Step = TutorialStep.Welcome;
        stepEnteredAt = Time.unscaledTime;
        fx?.Clear();
        Changed?.Invoke();
    }

    public void RepeatStep()
    {
        if (!IsRunning)
            return;
        SetStep(Step, force: true);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    public void SkipToLab()
    {
        JumpTo(TutorialStep.Lab);
    }

    public void SkipToBelts()
    {
        JumpTo(TutorialStep.Belts);
    }

    public void JumpTo(TutorialStep target)
    {
        if (!IsRunning)
            return;
        if (target <= TutorialStep.Welcome)
        {
            AcceptWelcome();
            return;
        }

        if (target >= TutorialStep.Hotbar)
            FillHotbar();
        if (target > floor)
            floor = target;
        SetStep(target, force: true);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    public void SkipStep()
    {
        if (!IsRunning)
            return;
        if (Step == TutorialStep.Welcome)
        {
            AcceptWelcome();
            return;
        }

        if (Step == TutorialStep.Farewell)
        {
            FinishFarewell();
            return;
        }

        TutorialStep next = Step + 1;
        if (next > TutorialStep.Farewell)
            next = TutorialStep.Farewell;
        if (next > floor)
            floor = next;
        SetStep(next);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    public void FinishFarewell()
    {
        if (!IsRunning)
            return;
        Stop(finished: true);
        FillHotbar();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        Changed?.Invoke();
    }

    public void CaptureSave(SaveData data)
    {
        if (data == null)
            return;
        data.tutorialStep = (int)Step;
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
        Step = next;
        stepEnteredAt = Time.unscaledTime;
        lastWaitOre = CurrentOreProgress();
        lastWaitCheck = Time.unscaledTime;
        if (next == TutorialStep.LookMove)
            CapturePose();
        Changed?.Invoke();
    }

    void Advance()
    {
        int guard = 0;
        while (Step < TutorialStep.Farewell && IsSatisfied(Step) && guard++ < 20)
            SetStep(Step + 1);
    }

    bool IsSatisfied(TutorialStep step)
    {
        switch (step)
        {
            case TutorialStep.Welcome:
                return false;
            case TutorialStep.LookMove:
                return HasLookedOrMoved();
            case TutorialStep.BuildMode:
                return Builder != null && Builder.isBuildMode;
            case TutorialStep.Hotbar:
                return HotbarHas(ExtractorId) && HotbarHas(ConveyorId) && HotbarHas(LabId);
            case TutorialStep.IronOne:
                return CountExtractors(IronOreId) >= 1;
            case TutorialStep.IronThree:
                return CountExtractors(IronOreId) >= 3;
            case TutorialStep.Copy:
                return CountClipboard(ExtractorId) >= 3 || CountExtractors(CopperOreId) >= 2;
            case TutorialStep.PasteCopper:
                return CountExtractors(CopperOreId) >= 2;
            case TutorialStep.Lab:
                return FindLab() != null;
            case TutorialStep.StartResearch:
                return HasResearch(BasicId) || CanStartBasic();
            case TutorialStep.Belts:
                return HasResearch(BasicId) || (Submitted(IronOreId) > 0 && Submitted(CopperOreId) > 0);
            case TutorialStep.WaitChapter1:
                return HasResearch(BasicId);
            case TutorialStep.PlaceSmelter:
                return CountBuildings(SmelterId) >= 1;
            case TutorialStep.ResearchIronIngot:
                return HasResearch(IronIngotResearchId);
            case TutorialStep.PickRecipe:
                return HasSmelterRecipe(IronIngotRecipeId);
            case TutorialStep.FirstSmelt:
                return SmelterGotIron();
            case TutorialStep.ResearchCopperIngot:
                return HasResearch(CopperIngotResearchId);
            case TutorialStep.CopySmelter:
                return CountBuildings(SmelterId) >= 2 && HasSmelterRecipe(CopperIngotRecipeId);
            case TutorialStep.Farewell:
                return false;
            default:
                return true;
        }
    }

    bool AlreadyPastTutorial()
    {
        return HasResearch(BasicId) && HasSmelterRecipe(IronIngotRecipeId);
    }

    bool HasLookedOrMoved()
    {
        PlayerMovement move = Movement;
        if (move == null)
            return StepAge > 8f;
        if ((move.transform.position - startPos).sqrMagnitude >= 64f)
            return true;
        float yaw = Mathf.DeltaAngle(startYaw, move.transform.eulerAngles.y);
        if (Mathf.Abs(yaw) >= 90f)
            return true;
        if (Mathf.Abs(move.Pitch) >= 40f)
            return true;
        return false;
    }

    void CapturePose()
    {
        PlayerMovement move = Movement;
        if (move == null)
            return;
        startPos = move.transform.position;
        startYaw = move.transform.eulerAngles.y;
    }

    public bool WaitStuck =>
        (Step == TutorialStep.WaitChapter1
            || Step == TutorialStep.ResearchIronIngot
            || Step == TutorialStep.ResearchCopperIngot)
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
        if (Step == TutorialStep.ResearchIronIngot)
            return Submitted(IronOreId);
        if (Step == TutorialStep.ResearchCopperIngot)
            return Submitted(CopperOreId);
        return Submitted(IronOreId) + Submitted(CopperOreId);
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
        return Required(itemId, ResearchIdForStep(Step));
    }

    public int Required(string itemId, string researchId)
    {
        ResearchNodeData node = GameDatabase.FindResearch(researchId);
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

    static string ResearchIdForStep(TutorialStep step)
    {
        if (step == TutorialStep.ResearchIronIngot)
            return IronIngotResearchId;
        if (step == TutorialStep.ResearchCopperIngot)
            return CopperIngotResearchId;
        return BasicId;
    }

    static bool HasResearch(string id)
    {
        return ResearchSystem.Instance != null && ResearchSystem.Instance.IsResearchIdUnlocked(id);
    }

    static bool CanStartBasic()
    {
        if (ResearchSystem.Instance == null)
            return false;
        ResearchNodeData node = GameDatabase.FindResearch(BasicId);
        return node != null && ResearchSystem.Instance.CanStartResearch(node);
    }

    static bool IsBasicActive()
    {
        return CanStartBasic() || HasResearch(BasicId);
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
            if (list[i] != null && list[i].data != null && IdsEqual(list[i].data.id, buildingId))
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

    static bool HasSmelterRecipe(string recipeId)
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

    static bool SmelterGotIron()
    {
        ItemData ore = GameDatabase.FindItem(IronOreId);
        ItemData ingot = GameDatabase.FindItem(IronIngotId);
        Smelter[] list = Object.FindObjectsByType<Smelter>(FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            Smelter s = list[i];
            if (s == null)
                continue;
            if (ore != null && s.CountInput(ore) > 0)
                return true;
            if (ingot != null && s.OutputContains(ingot))
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

    static bool HotbarHas(string id)
    {
        PlayerInventory inv = Inventory;
        if (inv == null || inv.hotbar == null)
            return false;
        for (int i = 0; i < inv.hotbar.Length; i++)
        {
            if (inv.hotbar[i] != null && IdsEqual(inv.hotbar[i].id, id))
                return true;
        }
        return false;
    }

    static void ClearHotbar()
    {
        PlayerInventory inv = Inventory;
        if (inv == null)
            return;
        inv.ClearHotbar();
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
