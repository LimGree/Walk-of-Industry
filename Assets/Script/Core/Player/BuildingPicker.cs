using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// СКМ «взять здание под прицелом» (как «pick block» в Minecraft). Работает везде: включает стройку,
/// кладёт тип здания в хотбар и копирует поворот. Shift+СКМ — ещё и настройки (рецепт, фильтр,
/// выход экстрактора): их получат следующие поставленные здания этого типа.
/// По жиле без здания — экстрактор, по воде — водокачка. Клавиша — действие PickBuilding в настройках.
/// </summary>
public class BuildingPicker : MonoBehaviour
{
    PlayerBuilder builder;
    PlayerInventory inventory;
    InputAction action;

    // Скопированные настройки (Shift+СКМ). Живут, пока не взяли другое здание обычным СКМ.
    static BuildingData copiedFor;
    static RecipeData copiedRecipe;
    static ItemData copiedFilter;
    static bool copiedHasFilter;
    static bool copiedFront;

    void Start()
    {
        builder = FindFirstObjectByType<PlayerBuilder>();
        inventory = FindFirstObjectByType<PlayerInventory>();
        action = KeybindStore.GetAction("PickBuilding");
        if (action != null)
            action.performed += OnPick;
    }

    void OnDestroy()
    {
        if (action != null)
            action.performed -= OnPick;
    }

    void OnPick(InputAction.CallbackContext ctx)
    {
        if (Blocked())
            return;
        Pick();
    }

    bool Blocked()
    {
        if (UiStack.GameplayBlocked || PhotoMode.IsActive)
            return true;
        if (BeltRide.Instance != null && BeltRide.Instance.IsRiding)
            return true;
        return false;
    }

    void Pick()
    {
        if (builder == null)
            builder = FindFirstObjectByType<PlayerBuilder>();
        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();
        Camera cam = Camera.main;
        if (builder == null || inventory == null || cam == null)
            return;

        var ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, builder.maxBuildDistance, ~0, QueryTriggerInteraction.Ignore))
            return;

        BuildingBase source = hit.collider.GetComponentInParent<BuildingBase>();
        BuildingData data = null;
        float yaw = builder.PlacementYaw;
        if (source != null && source.data != null && source.IsPlaced)
        {
            data = source.data;
            yaw = source.transform.eulerAngles.y;
        }
        else
        {
            source = null;
            data = DataForGround(hit.point);
        }

        if (data == null)
            return;

        if (ResearchSystem.Instance != null && !ResearchSystem.Instance.IsBuildingUnlocked(data))
        {
            UiAudio.PlayError();
            UiNotification.Push(UiLocale.T("pick.title"), UiLocale.T("pick.locked", data.Title), UiStatus.Warning);
            return;
        }

        bool withSettings = source != null && ShiftHeld();
        string settings = withSettings ? CopySettings(source) : null;
        if (!withSettings)
            ClearCopied();

        int slot = PutInHotbar(data);
        if (!builder.isBuildMode)
            builder.EnterBuildMode(false);
        builder.SetPlacementYaw(yaw);

        UiAudio.PlaySelect();
        if (InventoryUI.Instance != null)
            InventoryUI.Instance.FlashSlot(slot);
        UiNotification.Push(
            UiLocale.T("pick.title"),
            string.IsNullOrEmpty(settings) ? data.Title : UiLocale.T("pick.with_settings", data.Title, settings),
            UiStatus.Ready);
    }

    static BuildingData DataForGround(Vector3 point)
    {
        Vector2Int cell = BuildingLinker.WorldToCell(point);
        if (ResourceNode.GetAt(cell) != null)
            return GameDatabase.FindBuilding("extractor");
        if (WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsWater(cell))
            return GameDatabase.FindBuilding("water_extractor");
        return null;
    }

    static bool ShiftHeld()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
    }

    /// <summary>
    /// Как «pick block» в Minecraft: уже есть в хотбаре — выбрать этот слот; иначе в текущий пустой,
    /// потом в первый пустой, иначе заменить текущий.
    /// </summary>
    int PutInHotbar(BuildingData data)
    {
        int slot = inventory.IndexOf(data);
        if (slot < 0)
        {
            int current = inventory.IsEmptyToolSelected ? -1 : inventory.selectedIndex;
            bool currentEmpty = current >= 0 && inventory.hotbar != null && current < inventory.hotbar.Length
                && inventory.hotbar[current] == null;
            if (currentEmpty)
                slot = current;
            else
            {
                slot = inventory.FirstEmptyHotbarSlot();
                if (slot < 0)
                    slot = current >= 0 ? current : 0;
            }

            inventory.SetHotbarSlot(slot, data);
        }

        inventory.SelectSlot(slot);
        return slot;
    }

    // ---------- Настройки (Shift+СКМ) ----------

    static void ClearCopied()
    {
        copiedFor = null;
        copiedRecipe = null;
        copiedFilter = null;
        copiedHasFilter = false;
        copiedFront = false;
    }

    static string CopySettings(BuildingBase b)
    {
        ClearCopied();
        copiedFor = b.data;
        string text = null;
        switch (b)
        {
            case CrafterBuilding c when c.currentRecipe != null:
                copiedRecipe = c.currentRecipe;
                text = UiLocale.T("pick.recipe", c.currentRecipe.Title);
                break;
            case RoboticArm arm:
                copiedHasFilter = true;
                copiedFilter = arm.Filter;
                text = UiLocale.T("pick.filter", arm.Filter != null ? arm.Filter.Title : UiLocale.T("machine.any"));
                break;
            case Conveyor belt when belt.Filter != null:
                copiedHasFilter = true;
                copiedFilter = belt.Filter;
                text = UiLocale.T("pick.filter", belt.Filter.Title);
                break;
            case DroneLoadStation st:
                copiedHasFilter = true;
                copiedFilter = st.filter;
                text = UiLocale.T("pick.filter", st.filter != null ? st.filter.Title : UiLocale.T("machine.any"));
                break;
            case Extractor ex when ex.requireFrontOutput:
                copiedFront = true;
                text = UiLocale.T("pick.front");
                break;
        }

        if (text == null)
            ClearCopied();
        return text;
    }

    /// <summary>Только что поставленное здание получает скопированные настройки, если тип совпадает.</summary>
    public static void ApplyCopied(BuildingBase b)
    {
        if (b == null || copiedFor == null || b.data != copiedFor)
            return;
        if (copiedRecipe != null && b is CrafterBuilding crafter)
        {
            if (ResearchSystem.Instance == null || ResearchSystem.Instance.IsRecipeUnlocked(copiedRecipe))
                crafter.SetRecipe(copiedRecipe);
        }

        if (copiedHasFilter)
        {
            if (b is RoboticArm arm)
                arm.SetFilter(copiedFilter);
            else if (b is Conveyor belt)
                belt.SetFilter(copiedFilter);
            else if (b is DroneLoadStation st)
                st.SetFilter(copiedFilter);
        }

        if (copiedFront && b is Extractor ex)
            ex.requireFrontOutput = true;
    }
}
