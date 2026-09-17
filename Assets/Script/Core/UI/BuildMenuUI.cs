using System;
using UnityEngine;
using UnityEngine.UIElements;

public class BuildMenuUI : MonoBehaviour
{
    public static BuildMenuUI Instance { get; private set; }

    static readonly string[] Categories =
    {
        "All", "Logistics", "Production", "Storage", "Extraction", "Research"
    };

    [Header("References")]
    public PlayerInventory inventory;
    public PlayerBuilder playerBuilder;
    public GameObject menuPanel;
    public Transform buttonsParent;
    public GameObject buttonPrefab;

    [Header("Data")]
    public BuildingData[] availableBuildings;

    public bool IsOpen { get; private set; }

    VisualElement overlay;
    ScrollView grid;
    TextField search;
    string category = "All";
    VisualElement catRow;

    void Awake()
    {
        Instance = this;
        if (playerBuilder == null)
            playerBuilder = FindFirstObjectByType<PlayerBuilder>();
        if (inventory == null && playerBuilder != null)
            inventory = playerBuilder.inventory;
    }

    void Start()
    {
        Build();
        IndustryUi.HideLegacy(this, menuPanel);
        IndustryUi.DisableHudCanvas(this);
        IndustryUi.Show(overlay, false);
        IsOpen = false;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged -= CreateButtons;
    }

    void OnEnable()
    {
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged += CreateButtons;
    }

    void OnDisable()
    {
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged -= CreateButtons;
    }

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 100);
        overlay = IndustryUi.OverlayPanel(UiLocale.T("overlay.build"), null, () => CloseMenu(true));
        VisualElement panel = IndustryUi.PanelOf(overlay);
        var searchHost = IndustryUi.El("SearchHost", "search-host");
        searchHost.Add(IndustryUi.Text("SL", UiLocale.T("build.search"), "label-caps"));
        search = new TextField { name = "Search" };
        search.AddToClassList("field");
        search.AddToClassList("search-field");
        search.RegisterValueChangedCallback(_ => CreateButtons());
        searchHost.Add(search);
        panel.Add(searchHost);
        catRow = IndustryUi.El("Cats", "cat-row");
        for (int i = 0; i < Categories.Length; i++)
        {
            string cat = Categories[i];
            Button chip = IndustryUi.Btn(CatLabel(cat), () =>
            {
                category = cat;
                RefreshCats();
                CreateButtons();
            }, "cat-chip");
            chip.RemoveFromClassList("btn");
            chip.userData = cat;
            catRow.Add(chip);
        }
        panel.Add(catRow);
        grid = IndustryUi.Scroll("BuildGrid");
        var wrap = IndustryUi.El("Grid", "grid");
        wrap.name = "Cards";
        grid.Add(wrap);
        panel.Add(grid);
        root.Add(overlay);
        RefreshCats();
    }

    void CreateButtons()
    {
        if (grid == null)
            return;
        VisualElement wrap = grid.Q("Cards") ?? grid.contentContainer;
        wrap.Clear();
        if (!wrap.ClassListContains("grid"))
            wrap.AddToClassList("grid");

        BuildingData[] catalog = ResolveCatalog();
        if (catalog == null)
            return;

        string query = search != null ? search.value : "";
        for (int i = 0; i < catalog.Length; i++)
        {
            BuildingData data = catalog[i];
            if (data == null)
                continue;
            if (!Matches(data, query, category))
                continue;
            bool unlocked = ResearchSystem.Instance == null
                || ResearchSystem.Instance.IsBuildingUnlocked(data);
            BuildingData captured = data;
            wrap.Add(IndustryUi.BuildingCard(data, unlocked, null, false, unlocked ? () => SelectBuilding(captured) : null));
        }
    }

    void RefreshCats()
    {
        if (catRow == null)
            return;
        for (int i = 0; i < catRow.childCount; i++)
        {
            VisualElement child = catRow[i];
            string cat = child.userData as string;
            IndustryUi.SetOn(child, cat == category, "is-selected");
        }
    }

    static string CatLabel(string cat)
    {
        switch (cat)
        {
            case "Logistics": return UiLocale.T("cat.logistics");
            case "Production": return UiLocale.T("cat.production");
            case "Storage": return UiLocale.T("cat.storage");
            case "Extraction": return UiLocale.T("cat.extraction");
            case "Research": return UiLocale.T("cat.research");
            default: return UiLocale.T("cat.all");
        }
    }

    static bool Matches(BuildingData building, string query, string cat)
    {
        if (building == null)
            return false;
        if (cat != "All" && IndustryUi.BuildingCategory(building) != cat)
            return false;
        return string.IsNullOrEmpty(query)
            || (building.displayName != null
                && building.displayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            || (building.id != null
                && building.id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    BuildingData[] ResolveCatalog()
    {
        if (availableBuildings != null && availableBuildings.Length > 0)
            return availableBuildings;
        BuildingData[] fromDb = GameDatabase.AllBuildings();
        if (fromDb != null && fromDb.Length > 0)
            return fromDb;
        if (inventory != null && inventory.allBuildings != null && inventory.allBuildings.Length > 0)
            return inventory.allBuildings;
        return null;
    }

    void SelectBuilding(BuildingData building)
    {
        if (inventory == null || building == null)
            return;
        inventory.Equip(building);
        CloseMenu(restorePlayerControl: true);
        playerBuilder?.OnBuildMenuClosedAfterSelection();
    }

    public void OpenMenu()
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        IsOpen = true;
        if (overlay == null)
            Build();
        CreateButtons();
        IndustryUi.SetHeader(overlay, UiLocale.T("overlay.build"), null);
        IndustryUi.Show(overlay, true);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            SetPlayerControl(false);
        }
    }

    public void ToggleMenu()
    {
        if (IsOpen)
            CloseMenu(restorePlayerControl: true);
        else
            OpenMenu();
    }

    public void CloseMenu(bool restorePlayerControl = true)
    {
        IsOpen = false;
        IndustryUi.Show(overlay, false);
        if (!restorePlayerControl)
            return;
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            SetPlayerControl(true);
        }
    }

    void SetPlayerControl(bool enabled)
    {
        PlayerMovement movement = null;
        if (playerBuilder != null && playerBuilder.playerMovement != null)
            movement = playerBuilder.playerMovement;
        else
            movement = FindFirstObjectByType<PlayerMovement>();
        if (movement == null)
            return;
        movement.canMove = enabled;
        movement.canLook = enabled;
    }
}
