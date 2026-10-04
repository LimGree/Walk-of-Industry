using System.Collections.Generic;
using UnityEngine;

public abstract class CrafterBuilding : BuildingBase, IInteractable
{
    [Header("Crafter")]
    public RecipeData currentRecipe;
    public float craftProgress;

    [Header("Input buffer")]
    [Tooltip("Макс. множитель запасов относительно рецепта (2 = два крафта вперёд).")]
    public int inputBufferMultiplier = 2;

    [Header("Upgrade")]
    public int level = 1;

    [Header("Debug")]
    public bool showDebug;

    protected readonly Dictionary<ItemData, int> inputBuffer = new Dictionary<ItemData, int>();
    float simCarry;

    public virtual float CraftSpeed
    {
        get
        {
            float baseSpeed = level >= 2 ? 2f : 1f;
            return baseSpeed * PowerGenerator.GetNearbySpeedMultiplier(transform.position);
        }
    }
    public override bool CanUpgradeBuilding => false;
    protected virtual string WorkClip => "bld_assembler_loop";

    public override int ReadLevel()
    {
        return level;
    }

    public override void ApplyLevel(int savedLevel)
    {
        if (savedLevel >= 2)
            level = 2;
    }

    public override bool TryUpgradeBuilding()
    {
        if (!CanUpgradeBuilding)
            return false;
        level = 2;
        return true;
    }

    public float GetEffectiveCraftTime()
    {
        if (currentRecipe == null)
            return 0f;
        float speed = Mathf.Max(0.01f, CraftSpeed);
        return Economy.CraftNeed(currentRecipe) / speed;
    }

    public override void OnPlaced()
    {
        BuildingPrefabLayout.ApplyPrimarySockets(this);
        base.OnPlaced();
        craftProgress = 0f;
        inputBuffer.Clear();
        TryAutoRecipe(null);
    }

    protected virtual void Update()
    {
        simCarry += Time.deltaTime;
        if (simCarry < 0.12f)
            return;
        float dt = simCarry;
        simCarry = 0f;

        float breakMul = BreakWorkMul;
        bool working = breakMul > 0f && currentRecipe != null && HasEnoughInputs() && HasSpaceForRecipeOutputs();
        GameAudio.Loop(this, WorkClip, working && WorldView.InRange(transform.position));
        if (breakMul <= 0f)
            return;

        if (currentRecipe == null)
            return;

        if (!HasEnoughInputs())
            return;

        float need = Economy.CraftNeed(currentRecipe);
        if (!HasSpaceForRecipeOutputs())
        {
            craftProgress = Mathf.Min(craftProgress, need);
            return;
        }

        craftProgress += dt * CraftSpeed * breakMul * DevWorkMul;

        if (craftProgress >= need)
        {
            if (TryCraft())
                craftProgress = 0f;
            else
                craftProgress = need;
        }
    }

    protected virtual bool AcceptsItem(ItemData item)
    {
        return item != null && !item.isFluid;
    }

    /// <summary>Консоль (/machine finish): завершить цикл сейчас. false — нет ингредиентов или места на выходе.</summary>
    public bool DevFinishCycle()
    {
        if (!TryCraft())
            return false;
        craftProgress = 0f;
        return true;
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (currentRecipe == null && item != null && AutoRecipeFromItem)
            TryAutoRecipe(item);
        if (currentRecipe == null || !AcceptsItem(item))
        {
            NoteRejected(item);
            return false;
        }

        int required = 0;
        bool needed = false;
        if (currentRecipe.inputs != null)
        {
            for (int i = 0; i < currentRecipe.inputs.Count; i++)
            {
                ItemStack stack = currentRecipe.inputs[i];
                if (stack.item == item)
                {
                    needed = true;
                    required = stack.amount;
                    break;
                }
            }
        }

        if (!needed)
        {
            NoteRejected(item);
            return false;
        }

        int maxKeep = Mathf.Max(1, required) * Mathf.Max(1, inputBufferMultiplier);
        inputBuffer.TryGetValue(item, out int have);
        if (have >= maxKeep)
            return false;

        inputBuffer[item] = have + 1;
        if (showDebug)
            Debug.Log(GetType().Name + " получил: " + item.Title + ". Теперь: " + inputBuffer[item]);
        return true;
    }

    public int CountInput(ItemData item)
    {
        if (item == null)
            return 0;
        inputBuffer.TryGetValue(item, out int have);
        return have;
    }

    /// <summary>
    /// Почему стоит: "recipe" — рецепта нет; "wrong" — на вход пришёл предмет, который рецепт не берёт
    /// (лента встала); "input" — ждёт ингредиент; "output" — выход забит; null — работает.
    /// </summary>
    public string IdleReason()
    {
        if (currentRecipe == null)
            return "recipe";
        if (!HasEnoughInputs())
            return RecentlyRejected ? "wrong" : "input";
        if (!HasSpaceForRecipeOutputs())
            return "output";
        return null;
    }

    /// <summary>Причина простоя одной строкой для метки над станком и списков.</summary>
    public string IdleText()
    {
        switch (IdleReason())
        {
            case "recipe":
                return UiLocale.T("idle.no_recipe", KeybindStore.Hint("Interact"));
            case "wrong":
                return UiLocale.T("idle.wrong_item", LastRejected != null ? LastRejected.Title : "?");
            case "input":
                ItemData missing = MissingInput();
                return missing != null ? UiLocale.T("idle.waiting", missing.Title) : UiLocale.T("idle.no_input");
            case "output":
                return UiLocale.T("idle.output_full");
            default:
                return null;
        }
    }

    /// <summary>Первый ингредиент рецепта, которого не хватает.</summary>
    public ItemData MissingInput()
    {
        if (currentRecipe == null || currentRecipe.inputs == null)
            return null;
        for (int i = 0; i < currentRecipe.inputs.Count; i++)
        {
            ItemStack need = currentRecipe.inputs[i];
            if (need.item == null)
                continue;
            inputBuffer.TryGetValue(need.item, out int have);
            if (have < need.amount)
                return need.item;
        }

        return null;
    }

    public ItemData LastRejected { get; private set; }
    float lastRejectedAt = -100f;
    bool RecentlyRejected => LastRejected != null && Time.time - lastRejectedAt < 3f;

    void NoteRejected(ItemData item)
    {
        if (item == null)
            return;
        LastRejected = item;
        lastRejectedAt = Time.time;
    }

    /// <summary>
    /// Можно ли выбрать рецепт по пришедшему предмету (внутри приёма). Станки, у которых рецепт
    /// переключает сокеты (НПЗ), — нельзя: переподключение посреди передачи предмета.
    /// </summary>
    protected virtual bool AutoRecipeFromItem => true;

    /// <summary>Открытые рецепты, которые можно поставить в этот станок.</summary>
    public List<RecipeData> AvailableRecipes()
    {
        var list = new List<RecipeData>(4);
        RecipeData[] all = GameDatabase.AllRecipes();
        if (all == null || data == null)
            return list;
        for (int i = 0; i < all.Length; i++)
        {
            RecipeData r = all[i];
            if (r == null || !r.AllowsBuilding(data))
                continue;
            if (ResearchSystem.Instance != null && !ResearchSystem.Instance.IsRecipeUnlocked(r))
                continue;
            list.Add(r);
        }

        return list;
    }

    /// <summary>
    /// Авторецепт: без предмета — если доступен ровно один рецепт; с предметом — если ровно один
    /// доступный рецепт берёт этот предмет. Выключается настройкой.
    /// </summary>
    public bool TryAutoRecipe(ItemData item)
    {
        if (currentRecipe != null || !GameSettings.AutoRecipe)
            return false;
        List<RecipeData> list = AvailableRecipes();
        RecipeData pick = null;
        int matches = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (item != null && !UsesInput(list[i], item))
                continue;
            pick = list[i];
            matches++;
        }

        if (matches != 1 || pick == null)
            return false;
        SetRecipe(pick);
        return currentRecipe == pick;
    }

    static bool UsesInput(RecipeData recipe, ItemData item)
    {
        if (recipe == null || recipe.inputs == null)
            return false;
        for (int i = 0; i < recipe.inputs.Count; i++)
        {
            if (recipe.inputs[i].item == item)
                return true;
        }

        return false;
    }

    public virtual void SetRecipe(RecipeData recipe)
    {
        currentRecipe = recipe;
        craftProgress = 0f;
        inputBuffer.Clear();
    }

    public void Interact(GameObject interactor)
    {
        if (RepairUI.TryOpen(this))
            return;
        if (MachineUI.Instance != null)
            MachineUI.Instance.Open(this);
        else
            Debug.LogError("MachineUI.Instance == null!");
    }

    protected bool HasEnoughInputs()
    {
        if (currentRecipe == null || currentRecipe.inputs == null)
            return false;

        for (int i = 0; i < currentRecipe.inputs.Count; i++)
        {
            ItemStack required = currentRecipe.inputs[i];
            if (required.item == null)
                return false;
            if (!inputBuffer.TryGetValue(required.item, out int have) || have < required.amount)
                return false;
        }

        return true;
    }

    protected bool HasSpaceForRecipeOutputs()
    {
        if (currentRecipe == null || currentRecipe.outputs == null)
            return false;

        int total = 0;
        for (int i = 0; i < currentRecipe.outputs.Count; i++)
            total += Mathf.Max(0, currentRecipe.outputs[i].amount);

        return HasOutputSpace(total);
    }

    protected bool TryCraft()
    {
        if (!HasEnoughInputs() || !HasSpaceForRecipeOutputs())
            return false;

        for (int i = 0; i < currentRecipe.inputs.Count; i++)
        {
            ItemStack required = currentRecipe.inputs[i];
            inputBuffer[required.item] -= required.amount;
            ProductionStats.Instance?.RecordConsumed(required.item, required.amount);
        }

        for (int i = 0; i < currentRecipe.outputs.Count; i++)
        {
            ItemStack output = currentRecipe.outputs[i];
            if (output.item == null)
                continue;
            ProductionStats.Instance?.RecordProduced(output.item, output.amount);
            if (i == 0)
                BuildingFx.Burst(this, output.item);
            for (int n = 0; n < output.amount; n++)
            {
                if (!TryOutputToAny(output.item) && showDebug)
                    Debug.LogWarning(GetType().Name + ": буфер переполнен при выдаче " + output.item.Title);
            }
        }

        return true;
    }

    public override void WriteSave(BuildingSaveData save)
    {
        base.WriteSave(save);
        if (save == null)
            return;
        save.recipeId = currentRecipe != null ? currentRecipe.id : "";
        save.craftProgress = craftProgress;
        save.inputBuffer = SaveItems.FromCounts(inputBuffer);
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        if (save == null)
            return;

        currentRecipe = GameDatabase.FindRecipe(save.recipeId);
        craftProgress = Mathf.Max(0f, save.craftProgress);
        SaveItems.ToCounts(save.inputBuffer, inputBuffer);
        if (currentRecipe != null)
            craftProgress = Mathf.Min(craftProgress, Economy.CraftNeed(currentRecipe));
    }
}
