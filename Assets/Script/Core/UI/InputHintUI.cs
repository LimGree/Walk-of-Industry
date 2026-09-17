using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

public class InputHintUI : MonoBehaviour
{
    public static InputHintUI Instance { get; private set; }

    public const string PrefsKey = "ShowInputHints";
    const int MaxHints = 10;

    public VisualElement Bar => bar;

    public static bool HintsEnabled
    {
        get => PlayerPrefs.GetInt(PrefsKey, 1) != 0;
        set
        {
            PlayerPrefs.SetInt(PrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    PlayerBuilder builder;
    PlayerInventory inventory;
    PlayerInteractor interactor;
    BuildSelectionController selection;

    VisualElement bar;
    readonly StringBuilder key = new StringBuilder(256);
    string lastKey;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        BindRefs();
        BuildUi();
        KeybindStore.Changed += OnBindsChanged;
        UiLocale.Changed += OnBindsChanged;
    }

    void OnDestroy()
    {
        KeybindStore.Changed -= OnBindsChanged;
        UiLocale.Changed -= OnBindsChanged;
        if (Instance == this)
            Instance = null;
    }

    void OnBindsChanged()
    {
        lastKey = null;
    }

    void BindRefs()
    {
        if (builder == null && GameManager.Instance != null)
            builder = GameManager.Instance.playerBuilder;
        if (inventory == null && builder != null)
            inventory = builder.inventory;
        if (interactor == null && builder != null)
            interactor = builder.GetComponent<PlayerInteractor>();
        if (selection == null && builder != null)
            selection = builder.Selection;
    }

    void BuildUi()
    {
        VisualElement root = IndustryUi.Mount(this, 90);
        bar = IndustryUi.El("Hints", "hint-bar");
        bar.pickingMode = PickingMode.Ignore;
        root.Add(bar);
    }

    void LateUpdate()
    {
        if (!HintsEnabled)
        {
            if (lastKey == "off")
                return;
            lastKey = "off";
            if (bar != null)
                bar.Clear();
            return;
        }

        BindRefs();
        Apply(Collect());
    }

    List<(string key, string label)> Collect()
    {
        var hints = new List<(string, string)>(MaxHints);

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            Add(hints, KeybindStore.Hint("Pause"), UiLocale.T("hint.resume"));
            return hints;
        }

        if (InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen)
        {
            Add(hints, KeybindStore.Hint("Inventory"), UiLocale.T("hint.close_inv"));
            Add(hints, UiLocale.T("mouse.lmb"), UiLocale.T("hint.free_slot"));
            Add(hints, UiLocale.T("hint.drag"), UiLocale.T("hint.drag_bar"));
            Add(hints, UiLocale.T("mouse.rmb"), UiLocale.T("hint.remove_bar"));
            Add(hints, KeybindStore.Hint("Pause"), UiLocale.T("hint.close"));
            return hints;
        }

        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
        {
            Add(hints, ShopHint(), UiLocale.T("hint.close_shop"));
            Add(hints, KeybindStore.Hint("Pause"), UiLocale.T("hint.close"));
            return hints;
        }

        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
        {
            Add(hints, SelectionHint(), UiLocale.T("hint.close_sel"));
            Add(hints, KeybindStore.Hint("Pause"), UiLocale.T("hint.close"));
            return hints;
        }

        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
        {
            Add(hints, KeybindStore.Hint("Pause"), UiLocale.T("hint.close"));
            return hints;
        }

        if (ResearchUI.Instance != null && ResearchUI.Instance.IsOpen)
        {
            Add(hints, KeybindStore.Hint("Research"), UiLocale.T("hint.close_research"));
            Add(hints, KeybindStore.Hint("Pause"), UiLocale.T("hint.close"));
            return hints;
        }

        if (BuildMenuUI.Instance != null && BuildMenuUI.Instance.IsOpen)
        {
            Add(hints, KeybindStore.Hint("Pause"), UiLocale.T("hint.close"));
            return hints;
        }

        if (WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen)
        {
            Add(hints, UiLocale.T("mouse.wheel"), UiLocale.T("hint.zoom"));
            Add(hints, UiLocale.T("mouse.lmb"), UiLocale.T("hint.pan_map"));
            Add(hints, UiLocale.T("mouse.lmb"), UiLocale.T("hint.measure_map"));
            Add(hints, UiLocale.T("mouse.rmb"), UiLocale.T("hint.marker_map"));
            Add(hints, KeybindStore.Hint("MoveSelection"), UiLocale.T("hint.close_map"));
            Add(hints, KeybindStore.Hint("Pause"), UiLocale.T("hint.close_map"));
            return hints;
        }

        if (builder == null || !builder.isBuildMode)
        {
            Add(hints, KeybindStore.Hint("BuildMode"), UiLocale.T("hint.build_mode"));
            Add(hints, KeybindStore.Hint("MoveSelection"), UiLocale.T("hint.map"));
            Add(hints, ShopHint(), UiLocale.T("hint.shop"));
            Add(hints, KeybindStore.Hint("Zoom"), UiLocale.T("hint.zoom_hold"));
            Add(hints, UiLocale.T("mouse.wheel"), UiLocale.T("hint.zoom_map"));
            if (interactor != null && interactor.HasInteractableTarget)
                Add(hints, KeybindStore.Hint("Interact"), interactor.InteractableHint);
            Add(hints, KeybindStore.Hint("Pause"), UiLocale.T("hint.pause"));
            return hints;
        }

        if (selection != null && selection.IsPasteActive)
        {
            Add(hints, KeybindStore.Hint("Place"), UiLocale.T("hint.confirm_paste"));
            Add(hints, KeybindStore.Hint("Rotate"), UiLocale.T("hint.rotate_group"));
            Add(hints, Combo("Modifier", "Rotate"), UiLocale.T("hint.rotate_in_place"));
            Add(hints, KeybindStore.Hint("ClearSelection"), UiLocale.T("hint.cancel"));
            Add(hints, UiLocale.T("hint.look_away"), UiLocale.T("hint.cancel"));
            return hints;
        }

        if (selection != null && selection.IsMoveActive)
        {
            Add(hints, KeybindStore.Hint("Place"), UiLocale.T("hint.confirm_move"));
            Add(hints, KeybindStore.Hint("Rotate"), UiLocale.T("hint.rotate_group"));
            Add(hints, Combo("Modifier", "Rotate"), UiLocale.T("hint.rotate_in_place"));
            Add(hints, KeybindStore.Hint("ClearSelection"), UiLocale.T("hint.cancel"));
            Add(hints, UiLocale.T("hint.look_away"), UiLocale.T("hint.cancel"));
            return hints;
        }

        if (builder.IsLineStrokeActive)
        {
            Add(hints, UiLocale.T("hint.release_place", KeybindStore.Hint("Place")), UiLocale.T("hint.place_line"));
            Add(hints, UiLocale.T("hint.look_away"), UiLocale.T("hint.cancel"));
            return hints;
        }

        if (selection != null && selection.IsSelectionMode)
        {
            Add(hints, KeybindStore.Hint("Place"), UiLocale.T("hint.select_cells"));
            if (selection.HasSelectedBuildings)
            {
                Add(hints, KeybindStore.Hint("Copy"), UiLocale.T("hint.copy"));
                Add(hints, KeybindStore.Hint("MoveSelection"), UiLocale.T("hint.move"));
                Add(hints, KeybindStore.Hint("Rotate"), UiLocale.T("hint.rotate_center"));
                Add(hints, Combo("Modifier", "Rotate"), UiLocale.T("hint.rotate_in_place"));
                Add(hints, KeybindStore.Hint("Delete"), UiLocale.T("hint.delete"));
                Add(hints, SelectionHint(), UiLocale.T("hint.sel_settings"));
                Add(hints, KeybindStore.Hint("ClearSelection"), UiLocale.T("hint.clear_sel"));
            }
            if (selection.HasClipboard)
                Add(hints, KeybindStore.Hint("Paste"), UiLocale.T("hint.paste"));
            Add(hints, KeybindStore.Hint("SelectMode"), UiLocale.T("hint.exit_edit"));
            Add(hints, KeybindStore.Hint("BuildMode"), UiLocale.T("hint.exit_build"));
            return hints;
        }

        Add(hints, KeybindStore.Hint("BuildMode"), UiLocale.T("hint.exit_build"));
        Add(hints, KeybindStore.Hint("MoveSelection"), UiLocale.T("hint.map"));
        Add(hints, KeybindStore.Hint("Inventory"), UiLocale.T("hint.inventory"));
        if (builder.HasHeldBuilding)
        {
            Add(hints, KeybindStore.Hint("Place"), UiLocale.T("hint.place"));
            Add(hints, KeybindStore.Hint("Rotate"), UiLocale.T("hint.rotate"));
        }
        else
        {
            Add(hints, KeybindStore.Hint("SelectMode"), UiLocale.T("hint.edit_mode"));
            if (selection != null && selection.HasClipboard)
                Add(hints, KeybindStore.Hint("Paste"), UiLocale.T("hint.paste"));
        }

        Add(hints, KeybindStore.Hint("Demolish"), UiLocale.T("hint.demolish"));
        Add(hints, ShopHint(), UiLocale.T("hint.shop"));
        Add(hints, UiLocale.T("mouse.wheel"), UiLocale.T("hint.hotbar"));
        Add(hints, KeybindStore.Hint("Pause"), UiLocale.T("hint.pause"));
        return hints;
    }

    static string Combo(string modifierAction, string actionName)
    {
        return KeybindStore.Hint(modifierAction) + "+" + KeybindStore.Hint(actionName);
    }

    static string ShopHint()
    {
        string hint = KeybindStore.Hint("Shop");
        return hint == "—" ? "H" : hint;
    }

    static string SelectionHint()
    {
        string hint = KeybindStore.Hint("SelectionPanel");
        return hint == "—" ? "O" : hint;
    }

    static void Add(List<(string, string)> hints, string key, string label)
    {
        if (hints.Count >= MaxHints)
            return;
        hints.Add((key, label));
    }

    void Apply(List<(string key, string label)> hints)
    {
        key.Length = 0;
        for (int i = 0; i < hints.Count; i++)
        {
            key.Append(hints[i].key);
            key.Append('|');
            key.Append(hints[i].label);
            key.Append(';');
        }

        string now = key.ToString();
        if (now == lastKey)
            return;
        lastKey = now;

        if (bar == null)
            return;
        bar.Clear();
        for (int i = 0; i < hints.Count; i++)
        {
            var chip = IndustryUi.El("H", "hint");
            chip.pickingMode = PickingMode.Ignore;
            var cap = IndustryUi.El("K", "keycap");
            cap.pickingMode = PickingMode.Ignore;
            cap.Add(IndustryUi.Text("KT", hints[i].key, "hint-key"));
            chip.Add(cap);
            chip.Add(IndustryUi.Text("L", hints[i].label, "hint-label"));
            bar.Add(chip);
        }
    }
}
