using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

public class PlayerInventory : MonoBehaviour
{
    [Header("Hotbar")]
    public int hotbarSize = 9;
    public BuildingData[] hotbar;
    public int selectedIndex;

    [Header("Каталог (запасной, если нет GameDatabase)")]
    public BuildingData[] allBuildings;

    public event Action<int> OnSelectionChanged;
    public event Action OnHotbarChanged;

    InputSystem_Actions inputActions;
    InputAction inventoryAction;
    readonly HashSet<string> seenUnlocks = new HashSet<string>();

    public int EmptyToolIndex => Mathf.Max(1, hotbarSize);
    public int CycleSize => EmptyToolIndex + 1;
    public bool IsEmptyToolSelected => selectedIndex == EmptyToolIndex;

    void Awake()
    {
        inputActions = KeybindStore.Shared;
        EnsureHotbarArray();
    }

    void Start()
    {
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged += OnUnlocksChanged;

        FillEmptySlotsFromUnlocks();
    }

    public void ClearHotbar()
    {
        EnsureHotbarArray();
        for (int i = 0; i < hotbar.Length; i++)
            hotbar[i] = null;
        seenUnlocks.Clear();
        OnHotbarChanged?.Invoke();
        OnSelectionChanged?.Invoke(selectedIndex);
    }

    public void AllowAutofillAndFill()
    {
        seenUnlocks.Clear();
        FillEmptySlotsFromUnlocks();
    }

    void OnDestroy()
    {
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged -= OnUnlocksChanged;
    }

    void OnEnable()
    {
        if (inputActions != null)
            inputActions.Player.HotbarScroll.performed += OnHotbarScroll;
        inventoryAction = inputActions != null ? inputActions.asset.FindAction("Player/Inventory", false) : null;
        if (inventoryAction != null)
            inventoryAction.performed += OnInventoryToggle;
    }

    void OnDisable()
    {
        if (inputActions != null)
            inputActions.Player.HotbarScroll.performed -= OnHotbarScroll;
        if (inventoryAction != null)
            inventoryAction.performed -= OnInventoryToggle;
    }

    void Update()
    {
        if (!CanUseHotbar())
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.digit1Key.wasPressedThisFrame) SelectSlot(0);
        else if (keyboard.digit2Key.wasPressedThisFrame) SelectSlot(1);
        else if (keyboard.digit3Key.wasPressedThisFrame) SelectSlot(2);
        else if (keyboard.digit4Key.wasPressedThisFrame) SelectSlot(3);
        else if (keyboard.digit5Key.wasPressedThisFrame) SelectSlot(4);
        else if (keyboard.digit6Key.wasPressedThisFrame) SelectSlot(5);
        else if (keyboard.digit7Key.wasPressedThisFrame) SelectSlot(6);
        else if (keyboard.digit8Key.wasPressedThisFrame) SelectSlot(7);
        else if (keyboard.digit9Key.wasPressedThisFrame) SelectSlot(8);
        else if (keyboard.digit0Key.wasPressedThisFrame) SelectEmptyTool();
    }

    void OnInventoryToggle(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            return;
        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            return;
        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
            return;

        PlayerBuilder builder = ResolveBuilder();
        if (builder == null || !builder.isBuildMode)
            return;

        InventoryUI ui = InventoryUI.Instance;
        if (ui != null)
            ui.ToggleBag();
    }

    void OnHotbarScroll(InputAction.CallbackContext ctx)
    {
        if (!CanUseHotbar())
            return;

        Vector2 scroll = ctx.ReadValue<Vector2>();
        if (scroll.y > 0.1f)
            SelectSlot(selectedIndex <= 0 ? EmptyToolIndex : selectedIndex - 1);
        else if (scroll.y < -0.1f)
            SelectSlot(selectedIndex >= EmptyToolIndex ? 0 : selectedIndex + 1);
    }

    bool CanUseHotbar()
    {
        if (KeybindStore.BlocksGameplayInput)
            return false;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            return false;
        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            return false;
        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
            return false;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return false;
        PlayerBuilder builder = ResolveBuilder();
        return builder != null && builder.isBuildMode;
    }

    static PlayerBuilder ResolveBuilder()
    {
        return GameManager.Instance != null
            ? GameManager.Instance.playerBuilder
            : FindFirstObjectByType<PlayerBuilder>();
    }

    void OnUnlocksChanged()
    {
        FillEmptySlotsFromUnlocks();
    }

    public void RefreshHotbarFromUnlocks()
    {
        FillEmptySlotsFromUnlocks();
    }

    void EnsureHotbarArray()
    {
        if (hotbar == null || hotbar.Length != hotbarSize)
        {
            BuildingData[] next = new BuildingData[hotbarSize];
            if (hotbar != null)
            {
                int copy = Mathf.Min(hotbar.Length, hotbarSize);
                Array.Copy(hotbar, next, copy);
            }
            hotbar = next;
        }
    }

    void FillEmptySlotsFromUnlocks()
    {
        if (TutorialSystem.Instance != null && TutorialSystem.Instance.BlocksHotbarAutofill)
            return;

        EnsureHotbarArray();
        List<BuildingData> unlocked = GetUnlockedBuildings();
        for (int i = 0; i < unlocked.Count; i++)
        {
            BuildingData building = unlocked[i];
            if (building == null || string.IsNullOrEmpty(building.id))
                continue;
            if (!seenUnlocks.Add(building.id))
                continue;
            if (IndexOf(building) >= 0)
                continue;
            int empty = FirstEmptyHotbarSlot();
            if (empty < 0)
                continue;
            hotbar[empty] = building;
        }

        OnHotbarChanged?.Invoke();
        OnSelectionChanged?.Invoke(selectedIndex);
    }

    public List<BuildingData> GetCatalogBuildings()
    {
        var list = new List<BuildingData>();
        BuildingData[] catalog = GameDatabase.AllBuildings();
        if (catalog == null || catalog.Length == 0)
            catalog = allBuildings;
        if (catalog == null)
            return list;

        for (int i = 0; i < catalog.Length; i++)
        {
            if (catalog[i] != null)
                list.Add(catalog[i]);
        }

        return list;
    }

    public List<BuildingData> GetUnlockedBuildings()
    {
        var list = GetCatalogBuildings();
        if (ResearchSystem.Instance == null)
            return list;

        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (!ResearchSystem.Instance.IsBuildingUnlocked(list[i]))
                list.RemoveAt(i);
        }

        return list;
    }

    public bool IsUnlocked(BuildingData building)
    {
        return building != null
            && (ResearchSystem.Instance == null || ResearchSystem.Instance.IsBuildingUnlocked(building));
    }

    public BuildingData GetSelectedBuilding()
    {
        if (IsEmptyToolSelected)
            return null;
        if (selectedIndex < 0 || selectedIndex >= hotbar.Length)
            return null;

        BuildingData building = hotbar[selectedIndex];
        if (building != null && ResearchSystem.Instance != null
            && !ResearchSystem.Instance.IsBuildingUnlocked(building))
            return null;
        return building;
    }

    public bool HasEmptySlotSelected()
    {
        return IsEmptyToolSelected;
    }

    public void SelectSlot(int index)
    {
        int max = EmptyToolIndex;
        if (index < 0)
            index = max;
        if (index > max)
            index = 0;
        if (selectedIndex == index)
        {
            OnSelectionChanged?.Invoke(selectedIndex);
            return;
        }

        selectedIndex = index;
        OnSelectionChanged?.Invoke(selectedIndex);
        GameAudio.Ui("ui_select");
    }

    public void SelectEmptyTool()
    {
        SelectSlot(EmptyToolIndex);
    }

    public int IndexOf(BuildingData building)
    {
        if (building == null || hotbar == null)
            return -1;
        for (int i = 0; i < hotbar.Length; i++)
        {
            if (hotbar[i] == building)
                return i;
        }
        return -1;
    }

    public int FirstEmptyHotbarSlot()
    {
        if (hotbar == null)
            return -1;
        for (int i = 0; i < hotbar.Length; i++)
        {
            if (hotbar[i] == null)
                return i;
        }
        return -1;
    }

    public int CountOccupiedSlots()
    {
        int n = 0;
        if (hotbar == null)
            return 0;
        for (int i = 0; i < hotbar.Length; i++)
        {
            if (hotbar[i] != null)
                n++;
        }
        return n;
    }

    public void SetHotbarSlot(int index, BuildingData building)
    {
        EnsureHotbarArray();
        if (index < 0 || index >= hotbarSize)
            return;

        if (building != null && ResearchSystem.Instance != null
            && !ResearchSystem.Instance.IsBuildingUnlocked(building))
        {
            Debug.LogWarning("[Inventory] " + building.displayName + " ещё не открыто");
            return;
        }

        int existing = IndexOf(building);
        if (building != null && existing >= 0 && existing != index)
            hotbar[existing] = null;

        hotbar[index] = building;
        OnHotbarChanged?.Invoke();
        OnSelectionChanged?.Invoke(selectedIndex);
    }

    public void Equip(BuildingData building)
    {
        EquipToFirstEmpty(building);
    }

    public bool EquipToFirstEmpty(BuildingData building)
    {
        if (building == null)
            return false;
        if (IndexOf(building) >= 0)
            return false;

        int slot = FirstEmptyHotbarSlot();
        if (slot < 0)
            return false;

        SetHotbarSlot(slot, building);
        return true;
    }

    public void SwapOrPlace(BuildingData building, int slot)
    {
        EnsureHotbarArray();
        if (building == null || slot < 0 || slot >= hotbarSize)
            return;

        int from = IndexOf(building);
        if (from == slot)
            return;

        BuildingData occupant = hotbar[slot];
        if (from >= 0)
        {
            hotbar[from] = occupant;
            hotbar[slot] = building;
            OnHotbarChanged?.Invoke();
            OnSelectionChanged?.Invoke(selectedIndex);
            return;
        }

        SetHotbarSlot(slot, building);
    }

    public void UnequipSlot(int index)
    {
        SetHotbarSlot(index, null);
    }

    public List<string> CaptureHotbarIds()
    {
        EnsureHotbarArray();
        var ids = new List<string>(hotbarSize);
        for (int i = 0; i < hotbarSize; i++)
            ids.Add(hotbar[i] != null ? hotbar[i].id : "");
        return ids;
    }

    public void ApplyHotbarIds(List<string> ids)
    {
        EnsureHotbarArray();
        Array.Clear(hotbar, 0, hotbar.Length);
        if (ids == null)
        {
            FillEmptySlotsFromUnlocks();
            return;
        }

        bool any = false;
        int n = Mathf.Min(hotbarSize, ids.Count);
        for (int i = 0; i < n; i++)
        {
            BuildingData building = GameDatabase.FindBuilding(ids[i]);
            hotbar[i] = building;
            if (building != null)
                any = true;
        }

        List<BuildingData> unlocked = GetUnlockedBuildings();
        for (int i = 0; i < unlocked.Count; i++)
        {
            if (unlocked[i] != null && !string.IsNullOrEmpty(unlocked[i].id))
                seenUnlocks.Add(unlocked[i].id);
        }

        if (!any)
            FillEmptySlotsFromUnlocks();
        OnHotbarChanged?.Invoke();
        OnSelectionChanged?.Invoke(selectedIndex);
    }
}
