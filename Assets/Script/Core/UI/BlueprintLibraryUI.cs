using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Image = UnityEngine.UIElements.Image;

public class BlueprintLibraryUI : MonoBehaviour
{
    public static BlueprintLibraryUI Instance { get; private set; }
    public bool IsOpen { get; private set; }

    const string PrefTab = "bp.lib.tab";
    const string PrefView = "bp.lib.view";
    const string PrefTile = "bp.lib.tile";

    BuildSelectionController selection;
    VisualElement overlay;
    VisualElement grid;
    Label status;
    Button tabWorld;
    Button tabGlobal;
    Button viewTiles;
    Button viewList;
    Button sizeLarge;
    Button sizeMedium;
    Button sizeSmall;
    VisualElement sizeRow;
    InputAction libraryAction;

    bool globalTab;
    bool listView;
    int tileSize;
    string selectedId;
    readonly List<BlueprintRecord> records = new List<BlueprintRecord>();
    readonly List<Sprite> icons = new List<Sprite>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        selection = FindFirstObjectByType<BuildSelectionController>();
        globalTab = PlayerPrefs.GetInt(PrefTab, 0) == 1;
        listView = PlayerPrefs.GetInt(PrefView, 0) == 1;
        tileSize = Mathf.Clamp(PlayerPrefs.GetInt(PrefTile, 1), 0, 2);
        Build();
        SetOpen(false);
        BindInput();
    }

    void OnDestroy()
    {
        if (libraryAction != null)
            libraryAction.performed -= OnLibraryPerformed;
        ReleaseIcons();
        if (Instance == this)
            Instance = null;
    }

    void BindInput()
    {
        InputSystem_Actions actions = KeybindStore.Shared;
        libraryAction = actions != null ? actions.asset.FindAction("Player/Blueprints", false) : null;
        if (libraryAction != null)
            libraryAction.performed += OnLibraryPerformed;
    }

    void Update()
    {
        if (libraryAction != null)
            return;
        if (KeybindStore.BlocksGameplayInput)
            return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.pKey.wasPressedThisFrame)
            Toggle();
    }

    void OnLibraryPerformed(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        Toggle();
    }

    public void Toggle()
    {
        if (IsOpen)
        {
            SetOpen(false);
            return;
        }

        PlayerBuilder builder = GameManager.Instance != null ? GameManager.Instance.playerBuilder : null;
        if (builder == null)
            builder = FindFirstObjectByType<PlayerBuilder>();
        if (builder == null || !builder.isBuildMode)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (PhotoMode.IsActive)
            return;
        SetOpen(true);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        IndustryUi.Show(overlay, open);
        if (open)
        {
            if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
                WalletHud.Instance.SetShopOpen(false);
            if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
                SelectionActionsUI.Instance.SetOpen(false);
            if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
                MachineUI.Instance.Close();
            if (ResearchUI.Instance != null && ResearchUI.Instance.IsOpen)
                ResearchUI.Instance.Close();
            if (WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen)
                WorldMapUI.Instance.SetOpen(false);
            if (InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen)
                InventoryUI.Instance.SetBagOpen(false);
            Rebuild();
        }

        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 86);
        overlay = IndustryUi.OverlayPanel(UiLocale.T("overlay.blueprints"), null, () => SetOpen(false));
        VisualElement body = overlay.Q("Body") ?? IndustryUi.PanelOf(overlay);

        var tabs = IndustryUi.El("Tabs", "tab-row");
        tabWorld = IndustryUi.Btn(UiLocale.T("bp.tab_world"), () => SetTab(false), "tab");
        tabGlobal = IndustryUi.Btn(UiLocale.T("bp.tab_global"), () => SetTab(true), "tab");
        tabs.Add(tabWorld);
        tabs.Add(tabGlobal);
        body.Add(tabs);

        var views = IndustryUi.El("Views", "tab-row");
        viewTiles = IndustryUi.Btn(UiLocale.T("bp.view_tiles"), () => SetListView(false), "tab");
        viewList = IndustryUi.Btn(UiLocale.T("bp.view_list"), () => SetListView(true), "tab");
        views.Add(viewTiles);
        views.Add(viewList);
        body.Add(views);

        sizeRow = IndustryUi.El("Sizes", "tab-row");
        sizeLarge = IndustryUi.Btn(UiLocale.T("bp.size_large"), () => SetTileSize(0), "tab");
        sizeMedium = IndustryUi.Btn(UiLocale.T("bp.size_medium"), () => SetTileSize(1), "tab");
        sizeSmall = IndustryUi.Btn(UiLocale.T("bp.size_small"), () => SetTileSize(2), "tab");
        sizeRow.Add(sizeLarge);
        sizeRow.Add(sizeMedium);
        sizeRow.Add(sizeSmall);
        body.Add(sizeRow);

        var actions = IndustryUi.El("Actions", "row");
        actions.Add(IndustryUi.Btn(UiLocale.T("bp.save"), SaveSelected, "btn-small", "btn-primary"));
        actions.Add(IndustryUi.Btn(UiLocale.T("bp.insert"), InsertSelected, "btn-small"));
        actions.Add(IndustryUi.Btn(UiLocale.T("bp.rename"), RenameSelected, "btn-small"));
        actions.Add(IndustryUi.Btn(UiLocale.T("menu.delete"), DeleteSelected, "btn-small"));
        body.Add(actions);

        status = IndustryUi.Text("Status", "", "muted");
        body.Add(status);

        var scroll = new ScrollView();
        scroll.AddToClassList("scroll");
        scroll.AddToClassList("grow");
        grid = IndustryUi.El("Grid", "bp-grid");
        scroll.Add(grid);
        body.Add(scroll);

        overlay.pickingMode = PickingMode.Position;
        root.Add(overlay);
    }

    void SetTab(bool global)
    {
        globalTab = global;
        PlayerPrefs.SetInt(PrefTab, global ? 1 : 0);
        selectedId = null;
        Rebuild();
    }

    void SetListView(bool list)
    {
        listView = list;
        PlayerPrefs.SetInt(PrefView, list ? 1 : 0);
        Rebuild();
    }

    void SetTileSize(int size)
    {
        tileSize = Mathf.Clamp(size, 0, 2);
        PlayerPrefs.SetInt(PrefTile, tileSize);
        Rebuild();
    }

    void Rebuild()
    {
        if (grid == null)
            return;
        ReleaseIcons();
        grid.Clear();
        grid.EnableInClassList("bp-grid", !listView);
        grid.EnableInClassList("bp-list", listView);
        IndustryUi.Show(sizeRow, !listView);
        SetTabOn(tabWorld, !globalTab);
        SetTabOn(tabGlobal, globalTab);
        SetTabOn(viewTiles, !listView);
        SetTabOn(viewList, listView);
        SetTabOn(sizeLarge, tileSize == 0);
        SetTabOn(sizeMedium, tileSize == 1);
        SetTabOn(sizeSmall, tileSize == 2);

        records.Clear();
        records.AddRange(BlueprintLibrary.List(globalTab));
        if (records.Count == 0)
        {
            status.text = UiLocale.T("bp.empty");
            return;
        }

        status.text = UiLocale.T("bp.count", records.Count);
        for (int i = 0; i < records.Count; i++)
            AddCard(records[i]);
    }

    void AddCard(BlueprintRecord rec)
    {
        if (rec == null || rec.file == null)
            return;
        Sprite icon = BlueprintLibrary.LoadIcon(rec.iconPath);
        if (icon != null)
            icons.Add(icon);

        string sizeClass = tileSize == 0 ? "bp-lg" : (tileSize == 1 ? "bp-md" : "bp-sm");
        var card = IndustryUi.El("Bp", listView ? "bp-row" : "bp-card", sizeClass);
        if (rec.file.id == selectedId)
            card.AddToClassList("bp-on");

        var thumb = new Image();
        thumb.AddToClassList("bp-thumb");
        if (icon != null)
        {
            thumb.sprite = icon;
            thumb.scaleMode = ScaleMode.ScaleToFit;
        }
        card.Add(thumb);

        var meta = IndustryUi.El("Meta", "col", "grow");
        int n = rec.file.buildings != null ? rec.file.buildings.Length : 0;
        meta.Add(IndustryUi.Text("N", rec.file.name, "bp-name"));
        meta.Add(IndustryUi.Text("C", UiLocale.T("bp.meta", n, rec.file.created ?? ""), "muted"));
        card.Add(meta);

        string capturedId = rec.file.id;
        card.RegisterCallback<ClickEvent>(_ => SelectAndLoad(capturedId));
        grid.Add(card);
    }

    void SelectAndLoad(string id)
    {
        selectedId = id;
        InsertSelected();
        string loaded = status != null ? status.text : "";
        Rebuild();
        if (status != null && !string.IsNullOrEmpty(loaded))
            status.text = loaded;
    }

    BlueprintRecord Selected()
    {
        for (int i = 0; i < records.Count; i++)
        {
            if (records[i] != null && records[i].file != null && records[i].file.id == selectedId)
                return records[i];
        }
        return null;
    }

    void SaveSelected()
    {
        if (selection == null)
            selection = FindFirstObjectByType<BuildSelectionController>();
        if (selection == null)
            return;
        if (!globalTab && !WorldCatalog.HasActive)
        {
            status.text = UiLocale.T("bp.no_world");
            return;
        }

        var pieces = new List<BlueprintBuilding>();
        if (!selection.TrySnapshotSelection(pieces))
        {
            status.text = UiLocale.T("bp.need_selection");
            return;
        }

        byte[] png = BlueprintLibrary.CaptureTopDown(selection.SelectedBuildings);
        UiModal.Prompt(
            UiLocale.T("bp.save_title"),
            UiLocale.T("bp.save_body"),
            UiLocale.T("bp.save"),
            UiLocale.T("bp.untitled"),
            name =>
            {
                BlueprintRecord rec = BlueprintLibrary.Save(name, pieces, png, globalTab);
                if (rec != null && rec.file != null)
                    selectedId = rec.file.id;
                Rebuild();
                status.text = UiLocale.T("bp.saved");
            });
    }

    void InsertSelected()
    {
        BlueprintRecord rec = Selected();
        if (rec == null || rec.file == null)
            return;
        if (selection == null)
            selection = FindFirstObjectByType<BuildSelectionController>();
        if (selection == null)
            return;
        if (!selection.LoadBlueprint(rec.file.buildings))
        {
            status.text = UiLocale.T("bp.load_fail");
            return;
        }
        SetOpen(false);
    }

    void RenameSelected()
    {
        BlueprintRecord rec = Selected();
        if (rec == null || rec.file == null)
            return;
        UiModal.Prompt(
            UiLocale.T("bp.rename_title"),
            UiLocale.T("bp.rename_body"),
            UiLocale.T("bp.rename"),
            rec.file.name,
            name =>
            {
                BlueprintLibrary.Rename(rec, name);
                Rebuild();
            });
    }

    void DeleteSelected()
    {
        BlueprintRecord rec = Selected();
        if (rec == null || rec.file == null)
            return;
        UiModal.Confirm(
            UiLocale.T("bp.delete_title"),
            UiLocale.T("bp.delete_body", rec.file.name),
            UiLocale.T("menu.delete"),
            () =>
            {
                BlueprintLibrary.Delete(rec);
                selectedId = null;
                Rebuild();
            });
    }

    static void SetTabOn(Button button, bool on)
    {
        if (button == null)
            return;
        button.EnableInClassList("tab-on", on);
        button.EnableInClassList("is-selected", on);
    }

    void ReleaseIcons()
    {
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i] == null)
                continue;
            if (icons[i].texture != null)
                Destroy(icons[i].texture);
            Destroy(icons[i]);
        }
        icons.Clear();
    }
}
