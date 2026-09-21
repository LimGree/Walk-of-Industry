using UnityEngine;
using System.Collections.Generic;

public class ResearchSystem : MonoBehaviour
{
    public static ResearchSystem Instance { get; private set; }

    [Header("All Research Nodes")]
    public List<ResearchNodeData> allResearchNodes = new List<ResearchNodeData>();

    [Header("Level 0 — старт")]
    public List<BuildingData> startingBuildings = new List<BuildingData>();
    public List<RecipeData> startingRecipes = new List<RecipeData>();

    [Header("Лимит лабораторий")]
    [Tooltip("Сейчас на карте можно поставить столько лабораторий.")]
    public int baseLabLimit = 1;
    [Tooltip("Потолок после апгрейдов 4–5 уровня.")]
    public int maxLabLimit = 3;
    [Tooltip("id исследований, каждое из которых даёт +1 слот лаборатории (уровни 4 и 5).")]
    public string[] extraLabSlotResearchIds =
    {
        "research_extractor_2",
        "research_assembler_2"
    };

    public const string UndergroundResearchId = "research_underground_conveyor";

    private readonly HashSet<ResearchNodeData> unlockedResearch = new HashSet<ResearchNodeData>();
    private readonly HashSet<BuildingData> unlockedBuildings = new HashSet<BuildingData>();
    private readonly HashSet<RecipeData> unlockedRecipes = new HashSet<RecipeData>();
    private readonly Dictionary<ResearchNodeData, Dictionary<ItemData, int>> submittedByNode =
        new Dictionary<ResearchNodeData, Dictionary<ItemData, int>>();
    int eventHold;
    bool pendingUnlocks;
    bool pendingProgress;

    public ResearchNodeData CurrentResearch
    {
        get
        {
            List<ResearchNodeData> available = GetAvailableResearch();
            return available.Count > 0 ? available[0] : null;
        }
    }

    public event System.Action OnUnlocksChanged;
    public event System.Action OnResearchProgressChanged;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        GrantStartingUnlocks();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void GrantStartingUnlocks()
    {
        unlockedBuildings.Clear();
        unlockedRecipes.Clear();

        if (startingBuildings != null)
        {
            for (int i = 0; i < startingBuildings.Count; i++)
            {
                if (startingBuildings[i] != null)
                    unlockedBuildings.Add(startingBuildings[i]);
            }
        }

        if (startingRecipes != null)
        {
            for (int i = 0; i < startingRecipes.Count; i++)
            {
                if (startingRecipes[i] != null)
                    unlockedRecipes.Add(startingRecipes[i]);
            }
        }

    }

    public bool IsResearchUnlocked(ResearchNodeData node)
    {
        return node != null && unlockedResearch.Contains(node);
    }

    public bool IsResearchIdUnlocked(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        foreach (var node in unlockedResearch)
        {
            if (node != null && IdsEqual(node.id, id))
                return true;
        }

        return false;
    }

    public bool CanStartResearch(ResearchNodeData node)
    {
        if (node == null) return false;
        if (IsResearchUnlocked(node)) return false;

        if (node.requiredResearches != null)
        {
            for (int i = 0; i < node.requiredResearches.Count; i++)
            {
                ResearchNodeData req = node.requiredResearches[i];
                if (req != null && !IsResearchUnlocked(req))
                    return false;
            }
        }

        return true;
    }

    public bool SetCurrentResearch(ResearchNodeData node)
    {
        return node != null && CanStartResearch(node);
    }

    public bool TrySubmitItem(ItemData item)
    {
        if (item == null)
            return false;

        bool countedForResearch = false;
        List<ResearchNodeData> available = GetAvailableResearch();
        for (int i = 0; i < available.Count; i++)
        {
            ResearchNodeData node = available[i];
            if (!NeedsItem(node, item))
                continue;
            Dictionary<ItemData, int> bag = ProgressOf(node);
            int need = RequiredAmount(node, item);
            bag.TryGetValue(item, out int have);
            if (have >= need)
                continue;
            bag[item] = have + 1;
            countedForResearch = true;
            if (IsNodeComplete(node))
                CompleteResearch(node, grantReward: true);
        }

        if (countedForResearch)
            NotifyProgress();

        if (!countedForResearch && Economy.IsGear(item) && BeltSpeedSystem.Instance != null)
        {
            BeltSpeedSystem.Instance.SubmitGear();
            return true;
        }

        int coins = Economy.SellValue(item);
        if (coins > 0 && PlayerWallet.Instance != null)
            PlayerWallet.Instance.AddCoins(coins);

        return true;
    }

    static bool NeedsItem(ResearchNodeData node, ItemData item)
    {
        return RequiredAmount(node, item) > 0;
    }

    static int RequiredAmount(ResearchNodeData node, ItemData item)
    {
        if (node == null || item == null || node.requiredItems == null)
            return 0;
        for (int i = 0; i < node.requiredItems.Count; i++)
        {
            ItemStack req = node.requiredItems[i];
            if (req.item == item)
                return Mathf.Max(0, req.amount);
        }

        return 0;
    }

    Dictionary<ItemData, int> ProgressOf(ResearchNodeData node)
    {
        if (node == null)
            return new Dictionary<ItemData, int>();
        Dictionary<ItemData, int> bag;
        if (!submittedByNode.TryGetValue(node, out bag) || bag == null)
        {
            bag = new Dictionary<ItemData, int>();
            submittedByNode[node] = bag;
        }

        return bag;
    }

    bool IsNodeComplete(ResearchNodeData node)
    {
        if (node == null || node.requiredItems == null)
            return false;
        Dictionary<ItemData, int> bag = ProgressOf(node);
        for (int i = 0; i < node.requiredItems.Count; i++)
        {
            ItemStack req = node.requiredItems[i];
            if (req.item == null)
                continue;
            bag.TryGetValue(req.item, out int have);
            if (have < req.amount)
                return false;
        }

        return true;
    }

    public void CompleteResearch(ResearchNodeData node)
    {
        CompleteResearch(node, grantReward: true);
    }

    public void CompleteResearch(ResearchNodeData node, bool grantReward)
    {
        if (node == null || IsResearchUnlocked(node))
            return;

        unlockedResearch.Add(node);

        if (node.unlockedBuildings != null)
        {
            for (int i = 0; i < node.unlockedBuildings.Count; i++)
            {
                if (node.unlockedBuildings[i] != null)
                    unlockedBuildings.Add(node.unlockedBuildings[i]);
            }
        }

        if (node.unlockedRecipes != null)
        {
            for (int i = 0; i < node.unlockedRecipes.Count; i++)
            {
                if (node.unlockedRecipes[i] != null)
                    unlockedRecipes.Add(node.unlockedRecipes[i]);
            }
        }

        submittedByNode.Remove(node);

        if (grantReward)
        {
            int rubies = Economy.RubyReward(node);
            if (rubies > 0 && PlayerWallet.Instance != null)
                PlayerWallet.Instance.AddRubies(rubies);
            Debug.Log($"[Research] Completed: {node.displayName}  +{rubies} ruby");
            UiAudio.PlayNotify();
            UiNotification.Push(
                UiLocale.T("hud.research_done"),
                node.displayName,
                UiStatus.Completed);
        }
        else
        {
            Debug.Log($"[Research] Completed: {node.displayName}");
        }

        NotifyUnlocks();
        NotifyProgress();
        UnlockCompanionBuildings(node);
    }

    void UnlockCompanionBuildings(ResearchNodeData node)
    {
        if (node == null || !IdsEqual(node.id, UndergroundResearchId))
            return;

        BuildingData splitterBuilding = GameDatabase.FindBuilding("splitter");
        if (splitterBuilding != null)
            UnlockBuilding(splitterBuilding);
    }

    void NotifyUnlocks()
    {
        if (eventHold > 0)
        {
            pendingUnlocks = true;
            return;
        }

        OnUnlocksChanged?.Invoke();
    }

    void NotifyProgress()
    {
        if (eventHold > 0)
        {
            pendingProgress = true;
            return;
        }

        OnResearchProgressChanged?.Invoke();
    }

    void FlushHeldEvents()
    {
        if (eventHold > 0)
            return;
        if (pendingUnlocks)
        {
            pendingUnlocks = false;
            OnUnlocksChanged?.Invoke();
        }

        if (pendingProgress)
        {
            pendingProgress = false;
            OnResearchProgressChanged?.Invoke();
        }
    }

    public int CompleteAllResearch(bool grantReward = false)
    {
        eventHold++;
        int completed = 0;
        try
        {
            bool progressed = true;
            int guard = 0;
            while (progressed && guard++ < 512)
            {
                progressed = false;
                List<ResearchNodeData> nodes = GetAllNodes();
                for (int i = 0; i < nodes.Count; i++)
                {
                    ResearchNodeData node = nodes[i];
                    if (node == null || IsResearchUnlocked(node))
                        continue;
                    CompleteResearch(node, grantReward);
                    completed++;
                    progressed = true;
                }
            }
        }
        finally
        {
            eventHold--;
            FlushHeldEvents();
        }

        return completed;
    }

    public bool IsBuildingUnlocked(BuildingData building)
    {
        return building != null && unlockedBuildings.Contains(building);
    }

    public bool UnlockBuilding(BuildingData building)
    {
        if (building == null || unlockedBuildings.Contains(building))
            return false;
        unlockedBuildings.Add(building);
        NotifyUnlocks();
        return true;
    }

    public bool IsRecipeUnlocked(RecipeData recipe)
    {
        return recipe != null && unlockedRecipes.Contains(recipe);
    }

    public bool UnlockRecipe(RecipeData recipe)
    {
        if (recipe == null || unlockedRecipes.Contains(recipe))
            return false;
        unlockedRecipes.Add(recipe);
        NotifyUnlocks();
        return true;
    }

    public float GetCurrentProgress01()
    {
        List<ResearchNodeData> available = GetAvailableResearch();
        if (available.Count == 0)
            return 0f;
        float sum = 0f;
        for (int i = 0; i < available.Count; i++)
            sum += GetProgress01(available[i]);
        return sum / available.Count;
    }

    public float GetProgress01(ResearchNodeData node)
    {
        if (node == null || node.requiredItems == null || node.requiredItems.Count == 0)
            return 0f;

        int totalRequired = 0;
        int totalSubmitted = 0;
        Dictionary<ItemData, int> bag = ProgressOf(node);
        for (int i = 0; i < node.requiredItems.Count; i++)
        {
            ItemStack req = node.requiredItems[i];
            if (req.item == null)
                continue;
            totalRequired += Mathf.Max(0, req.amount);
            bag.TryGetValue(req.item, out int have);
            totalSubmitted += Mathf.Min(have, Mathf.Max(0, req.amount));
        }

        return totalRequired > 0 ? (float)totalSubmitted / totalRequired : 0f;
    }

    public int GetSubmitted(ItemData item)
    {
        if (item == null)
            return 0;
        int best = 0;
        foreach (var pair in submittedByNode)
        {
            if (pair.Value == null)
                continue;
            pair.Value.TryGetValue(item, out int have);
            if (have > best)
                best = have;
        }

        return best;
    }

    public int GetSubmitted(ResearchNodeData node, ItemData item)
    {
        if (node == null || item == null)
            return 0;
        ProgressOf(node).TryGetValue(item, out int have);
        return have;
    }

    public int GetMaxResearchLabs()
    {
        int extra = 0;
        if (extraLabSlotResearchIds != null)
        {
            for (int i = 0; i < extraLabSlotResearchIds.Length; i++)
            {
                if (IsResearchIdUnlocked(extraLabSlotResearchIds[i]))
                    extra++;
            }
        }

        return Mathf.Clamp(baseLabLimit + extra, 1, Mathf.Max(1, maxLabLimit));
    }

    public int CountPlacedLabs()
    {
        ResearchLab[] labs = FindObjectsByType<ResearchLab>(FindObjectsSortMode.None);
        int count = 0;
        for (int i = 0; i < labs.Length; i++)
        {
            if (labs[i] != null && labs[i].IsWorldLab)
                count++;
        }

        return count;
    }

    public bool CanPlaceAnotherLab()
    {
        return CountPlacedLabs() < GetMaxResearchLabs();
    }

    public List<ResearchNodeData> GetAvailableResearch()
    {
        var list = new List<ResearchNodeData>();
        List<ResearchNodeData> nodes = GetAllNodes();
        if (nodes == null)
            return list;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (CanStartResearch(nodes[i]))
                list.Add(nodes[i]);
        }

        return list;
    }

    public List<ResearchNodeData> GetAllNodes()
    {
        ResearchNodeData[] fromDb = GameDatabase.AllResearches();
        if (fromDb != null && fromDb.Length > 0)
            return new List<ResearchNodeData>(fromDb);
        if (allResearchNodes != null && allResearchNodes.Count > 0)
            return allResearchNodes;
        return new List<ResearchNodeData>();
    }

    [ContextMenu("Debug/Complete Current Research")]
    void DebugCompleteCurrentResearch()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Research] Skip research only works in Play Mode.");
            return;
        }

        ResearchNodeData node = CurrentResearch;
        if (node == null)
        {
            List<ResearchNodeData> available = GetAvailableResearch();
            if (available.Count == 0)
            {
                Debug.LogWarning("[Research] Nothing to complete.");
                return;
            }

            node = available[0];
            Debug.Log($"[Research] No active research, completing next available: {node.displayName}");
        }

        CompleteResearch(node);
    }

    [ContextMenu("Debug/Complete All Research")]
    void DebugCompleteAllResearch()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Research] Skip research only works in Play Mode.");
            return;
        }

        int completed = CompleteAllResearch(grantReward: false);
        Debug.Log($"[Research] Debug completed {completed} node(s).");
    }

    public ResearchSaveData CaptureSave()
    {
        var save = new ResearchSaveData();
        foreach (var node in unlockedResearch)
        {
            if (node != null && !string.IsNullOrEmpty(node.id))
                save.unlockedResearchIds.Add(node.id);
        }

        if (CurrentResearch != null)
            save.currentResearchId = CurrentResearch.id;

        foreach (var pair in submittedByNode)
        {
            if (pair.Key == null || string.IsNullOrEmpty(pair.Key.id) || pair.Value == null)
                continue;
            var row = new ResearchProgressSave { researchId = pair.Key.id };
            foreach (var itemPair in pair.Value)
            {
                if (itemPair.Key == null || string.IsNullOrEmpty(itemPair.Key.id) || itemPair.Value <= 0)
                    continue;
                row.submitted.Add(new ItemAmountSave
                {
                    itemId = itemPair.Key.id,
                    amount = itemPair.Value
                });
            }

            if (row.submitted.Count > 0)
                save.inProgress.Add(row);
        }

        return save;
    }

    public void ApplySave(ResearchSaveData save)
    {
        GrantStartingUnlocks();
        unlockedResearch.Clear();
        submittedByNode.Clear();

        if (save == null)
            return;

        eventHold++;
        try
        {
            if (save.unlockedResearchIds != null)
            {
                for (int i = 0; i < save.unlockedResearchIds.Count; i++)
                {
                    ResearchNodeData node = FindNode(save.unlockedResearchIds[i]);
                    if (node != null)
                        CompleteResearch(node, grantReward: false);
                }
            }

            if (save.inProgress != null)
            {
                for (int i = 0; i < save.inProgress.Count; i++)
                    ApplyProgressRow(save.inProgress[i]);
            }
            else if (!string.IsNullOrEmpty(save.currentResearchId) && save.submittedItems != null)
            {
                ApplyProgressRow(new ResearchProgressSave
                {
                    researchId = save.currentResearchId,
                    submitted = save.submittedItems
                });
            }
        }
        finally
        {
            eventHold--;
            FlushHeldEvents();
        }
    }

    void ApplyProgressRow(ResearchProgressSave row)
    {
        if (row == null || row.submitted == null)
            return;
        ResearchNodeData node = FindNode(row.researchId);
        if (node == null || IsResearchUnlocked(node) || !CanStartResearch(node))
            return;
        Dictionary<ItemData, int> bag = ProgressOf(node);
        for (int i = 0; i < row.submitted.Count; i++)
        {
            ItemAmountSave entry = row.submitted[i];
            ItemData item = FindItem(entry.itemId);
            if (item != null && entry.amount > 0)
                bag[item] = entry.amount;
        }

        if (IsNodeComplete(node))
            CompleteResearch(node, grantReward: false);
    }

    ResearchNodeData FindNode(string id)
    {
        ResearchNodeData fromDb = GameDatabase.FindResearch(id);
        if (fromDb != null)
            return fromDb;

        if (string.IsNullOrEmpty(id) || allResearchNodes == null)
            return null;

        for (int i = 0; i < allResearchNodes.Count; i++)
        {
            ResearchNodeData node = allResearchNodes[i];
            if (node != null && IdsEqual(node.id, id))
                return node;
        }

        return null;
    }

    static bool IdsEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;
        return a.Trim() == b.Trim();
    }

    static ItemData FindItem(string id)
    {
        return GameDatabase.FindItem(id);
    }
}
