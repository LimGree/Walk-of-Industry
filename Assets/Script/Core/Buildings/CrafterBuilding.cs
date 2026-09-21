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

    public virtual float CraftSpeed => level >= 2 ? 2f : 1f;
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
    }

    protected virtual void Update()
    {
        simCarry += Time.deltaTime;
        if (simCarry < 0.12f)
            return;
        float dt = simCarry;
        simCarry = 0f;

        bool working = currentRecipe != null && HasEnoughInputs() && HasSpaceForRecipeOutputs();
        GameAudio.Loop(this, WorkClip, working && WorldView.InRange(transform.position));

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

        craftProgress += dt * CraftSpeed;

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

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (currentRecipe == null || !AcceptsItem(item))
            return false;

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
            return false;

        int maxKeep = Mathf.Max(1, required) * Mathf.Max(1, inputBufferMultiplier);
        inputBuffer.TryGetValue(item, out int have);
        if (have >= maxKeep)
            return false;

        inputBuffer[item] = have + 1;
        if (showDebug)
            Debug.Log(GetType().Name + " получил: " + item.displayName + ". Теперь: " + inputBuffer[item]);
        return true;
    }

    public int CountInput(ItemData item)
    {
        if (item == null)
            return 0;
        inputBuffer.TryGetValue(item, out int have);
        return have;
    }

    public virtual void SetRecipe(RecipeData recipe)
    {
        currentRecipe = recipe;
        craftProgress = 0f;
        inputBuffer.Clear();
    }

    public void Interact(GameObject interactor)
    {
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
            for (int n = 0; n < output.amount; n++)
            {
                if (!TryOutputToAny(output.item) && showDebug)
                    Debug.LogWarning(GetType().Name + ": буфер переполнен при выдаче " + output.item.displayName);
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
