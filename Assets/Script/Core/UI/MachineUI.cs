using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class MachineUI : MonoBehaviour
{
    public static MachineUI Instance { get; private set; }

    [Header("Legacy uGUI (hidden)")]
    public GameObject machinePanel;
    public GameObject extractorContent;
    public GameObject smelterContent;
    public GameObject assemblerContent;
    public GameObject researchLabContent;

    [Header("Data")]
    public ItemData[] availableResources;
    public RecipeData[] allRecipes;

    public bool IsOpen { get; private set; }

    VisualElement overlay;
    Label statusLabel;
    VisualElement progress;
    VisualElement tabRow;
    Button tabResearch;
    Button tabRecipes;
    Button tabBelts;
    Button tabStats;
    Button tabIdle;
    VisualElement bodyHost;
    ScrollView bodyList;
    VisualElement researchPage;
    VisualElement recipePage;
    VisualElement beltPage;
    VisualElement statsPage;
    VisualElement idlePage;
    ScrollView idleList;
    VisualElement researchView;
    VisualElement researchCanvas;
    VisualElement researchDetail;
    readonly ResearchTreeNav researchNav = new ResearchTreeNav();
    string selectedResearchId;
    ScrollView recipeList;
    ScrollView beltList;
    ScrollView statsList;
    VisualElement storageGrid;
    VisualElement storageScroll;
    Label storageSummary;
    VisualElement armGrid;
    VisualElement armScroll;
    Label armSummary;
    Button upgradeBtn;
    VisualElement recipeSearchHost;
    TextField recipeSearch;
    VisualElement codexSearchHost;
    TextField codexSearch;
    VisualElement filterSearchHost;
    TextField filterSearch;
    string upgradeTipTitle = "";
    string upgradeTipBody = "";
    readonly List<VisualElement> storageSlots = new List<VisualElement>();

    BuildingBase currentBuilding;
    public bool IsLabView { get; private set; }
    int labTab;
    float nextStatsRefresh;

    const int TabResearch = 0;
    const int TabRecipes = 1;
    const int TabBelts = 2;
    const int TabStats = 3;
    const int TabIdle = 4;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    void OnEnable()
    {
        BindLiveEvents();
    }

    void OnDisable()
    {
        UnbindLiveEvents();
    }

    void OnDestroy()
    {
        UnbindLiveEvents();
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        Build();
        BindLiveEvents();
        IndustryUi.HideLegacy(this, machinePanel, extractorContent, smelterContent, assemblerContent, researchLabContent);
        IndustryUi.DisableHudCanvas(this);
    }

    void BindLiveEvents()
    {
        UnbindLiveEvents();
        if (BeltSpeedSystem.Instance != null)
            BeltSpeedSystem.Instance.OnChanged += OnLiveChanged;
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged += OnLiveChanged;
        if (ResearchSystem.Instance != null)
        {
            ResearchSystem.Instance.OnUnlocksChanged += OnLiveChanged;
            ResearchSystem.Instance.OnResearchProgressChanged += OnLiveChanged;
        }
    }

    void UnbindLiveEvents()
    {
        if (BeltSpeedSystem.Instance != null)
            BeltSpeedSystem.Instance.OnChanged -= OnLiveChanged;
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged -= OnLiveChanged;
        if (ResearchSystem.Instance != null)
        {
            ResearchSystem.Instance.OnUnlocksChanged -= OnLiveChanged;
            ResearchSystem.Instance.OnResearchProgressChanged -= OnLiveChanged;
        }
    }

    void Update()
    {
        if (IsOpen && (currentBuilding != null || IsLabView))
            UpdateProgress();
    }

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 110);
        overlay = IndustryUi.OverlayPanel("Building", null, Close);
        VisualElement panel = IndustryUi.PanelOf(overlay);
        VisualElement header = overlay.Q("Header");
        Button close = overlay.Q<Button>("Close");
        upgradeBtn = IndustryUi.Btn(UiLocale.T("machine.upgrade"), OnUpgradeClicked, "btn-small", "btn-primary", "upgrade-btn");
        if (header != null)
        {
            if (close != null)
                header.Insert(header.IndexOf(close), upgradeBtn);
            else
                header.Add(upgradeBtn);
        }
        IndustryUi.Show(upgradeBtn, false);
        upgradeBtn.RegisterCallback<PointerEnterEvent>(OnUpgradeHover);
        upgradeBtn.RegisterCallback<PointerLeaveEvent>(_ => UiTooltip.Hide());
        VisualElement uxmlBody = overlay.Q("Body");
        if (uxmlBody != null)
            IndustryUi.Show(uxmlBody, false);

        var statusRow = IndustryUi.El("StatusRow", "status-row");
        statusRow.Add(IndustryUi.El("Dot", "status-dot"));
        statusLabel = IndustryUi.Text("Status", "", "body-small", "grow");
        statusRow.Add(statusLabel);
        progress = IndustryUi.ProgressBar("Progress");
        int insertAt = header != null ? panel.IndexOf(header) + 1 : 0;
        panel.Insert(insertAt++, statusRow);
        panel.Insert(insertAt++, progress);

        tabRow = IndustryUi.El("Tabs", "tab-row");
        tabResearch = IndustryUi.TabBtn(UiLocale.T("machine.tab_research"), () => OpenLabTab(TabResearch));
        tabRecipes = IndustryUi.TabBtn(UiLocale.T("machine.tab_recipes"), () => OpenLabTab(TabRecipes));
        tabBelts = IndustryUi.TabBtn(UiLocale.T("machine.tab_belts"), () => OpenLabTab(TabBelts));
        tabStats = IndustryUi.TabBtn(UiLocale.T("machine.tab_stats"), () => OpenLabTab(TabStats));
        tabIdle = IndustryUi.TabBtn(UiLocale.T("machine.tab_idle"), () => OpenLabTab(TabIdle));
        tabRow.Add(tabResearch);
        tabRow.Add(tabRecipes);
        tabRow.Add(tabBelts);
        tabRow.Add(tabStats);
        tabRow.Add(tabIdle);
        if (header != null)
            panel.Insert(panel.IndexOf(progress) + 1, tabRow);
        else
            panel.Add(tabRow);

        bodyHost = IndustryUi.El("Body", "col", "grow");
        recipeSearchHost = IndustryUi.El("RecipeSearchHost", "search-host");
        recipeSearchHost.Add(IndustryUi.Text("RL", UiLocale.T("machine.search_recipe"), "label-caps"));
        recipeSearch = new TextField { name = "RecipeSearch" };
        recipeSearch.AddToClassList("field");
        recipeSearch.AddToClassList("search-field");
        recipeSearch.RegisterValueChangedCallback(_ =>
        {
            if (currentBuilding is CrafterBuilding crafter)
                RefreshRecipeList(crafter);
        });
        recipeSearchHost.Add(recipeSearch);
        recipeSearchHost.pickingMode = PickingMode.Position;
        bodyHost.Add(recipeSearchHost);

        bodyList = IndustryUi.Scroll("BodyList");
        bodyHost.Add(bodyList);

        researchPage = IndustryUi.El("ResearchPage", "research-layout", "grow");
        researchView = IndustryUi.El("ResearchView", "research-view", "grow");
        researchView.pickingMode = PickingMode.Position;
        researchCanvas = IndustryUi.El("ResearchCanvas", "research-canvas");
        researchView.Add(researchCanvas);
        researchNav.Attach(researchView, researchCanvas);
        researchDetail = IndustryUi.El("ResearchDetail", "research-detail", "col");
        researchPage.Add(researchView);
        researchPage.Add(researchDetail);

        recipePage = IndustryUi.El("RecipePage", "col", "grow");
        codexSearchHost = IndustryUi.El("CodexSearchHost", "search-host");
        codexSearchHost.Add(IndustryUi.Text("CL", UiLocale.T("machine.search_recipe"), "label-caps"));
        var searchRow = IndustryUi.El("CodexSearchRow", "row", "codex-search-row");
        searchRow.Add(IndustryUi.Text("Plus", "+", "codex-search-plus"));
        codexSearch = new TextField { name = "CodexSearch" };
        codexSearch.AddToClassList("field");
        codexSearch.AddToClassList("search-field");
        codexSearch.AddToClassList("grow");
        if (codexSearch.textEdition != null)
            codexSearch.textEdition.placeholder = UiLocale.T("codex.search");
        codexSearch.RegisterValueChangedCallback(_ => FillRecipeTab());
        searchRow.Add(codexSearch);
        codexSearchHost.Add(searchRow);
        recipeList = IndustryUi.Scroll("RecipeList");
        recipePage.Add(codexSearchHost);
        recipePage.Add(recipeList);

        beltPage = IndustryUi.El("BeltPage", "col", "grow");
        beltList = IndustryUi.Scroll("BeltList");
        beltPage.Add(beltList);

        statsPage = IndustryUi.El("StatsPage", "col", "grow");
        statsList = IndustryUi.Scroll("StatsList");
        statsPage.Add(statsList);

        idlePage = IndustryUi.El("IdlePage", "col", "grow");
        idleList = IndustryUi.Scroll("IdleList");
        idlePage.Add(idleList);

        storageSummary = IndustryUi.Text("StorageSum", "", "body-text");
        storageScroll = IndustryUi.Scroll("StorageScroll");
        storageGrid = IndustryUi.El("StorageGrid", "grid");
        storageScroll.Add(storageGrid);
        armSummary = IndustryUi.Text("ArmSum", "", "body-text");
        filterSearchHost = IndustryUi.El("FilterSearchHost", "search-host");
        filterSearchHost.Add(IndustryUi.Text("FL", UiLocale.T("machine.search_filter"), "label-caps"));
        filterSearch = new TextField { name = "FilterSearch" };
        filterSearch.AddToClassList("field");
        filterSearch.AddToClassList("search-field");
        filterSearch.RegisterValueChangedCallback(_ =>
        {
            if (currentBuilding is RoboticArm arm)
                RefreshArmFilter(arm);
            else if (currentBuilding is Conveyor belt)
                RefreshBeltFilter(belt);
        });
        filterSearchHost.Add(filterSearch);
        filterSearchHost.pickingMode = PickingMode.Position;
        armScroll = IndustryUi.Scroll("ArmScroll");
        armGrid = IndustryUi.El("ArmGrid", "col");
        armScroll.Add(armGrid);

        bodyHost.Add(researchPage);
        bodyHost.Add(recipePage);
        bodyHost.Add(beltPage);
        bodyHost.Add(statsPage);
        bodyHost.Add(idlePage);
        bodyHost.Add(storageSummary);
        bodyHost.Add(storageScroll);
        bodyHost.Add(armSummary);
        bodyHost.Add(filterSearchHost);
        bodyHost.Add(armScroll);
        panel.Add(bodyHost);

        IndustryUi.Show(overlay, false);
        root.Add(overlay);
        HidePages();
    }

    public void Open(BuildingBase building)
    {
        BindLiveEvents();
        if (building == null)
            return;
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        if (overlay == null)
            Build();
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            WalletHud.Instance.SetShopOpen(false);
        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
            SelectionActionsUI.Instance.SetOpen(false);

        currentBuilding = building;
        IsLabView = building is ResearchLab;
        IsOpen = true;
        HidePages();

        if (building is OilExtractor oil)
        {
            SetHeader("Нефтекачалка  ·  " + oil.RichnessName, building);
            ShowOilExtractorUI(oil);
        }
        else if (building is WaterExtractor water)
        {
            SetHeader(water.data != null ? water.data.displayName : "Водокачка", building);
            ShowWaterExtractorUI(water);
        }
        else if (building is Extractor extractor)
        {
            SetHeader("Extractor  ·  ур. " + extractor.level, building);
            ShowExtractorUI(extractor);
        }
        else if (building is CrafterBuilding crafter)
        {
            string title = building.data != null ? building.data.displayName : crafter.GetType().Name;
            Assembler assembler = crafter as Assembler;
            if (assembler != null)
                title += "  ·  ур. " + assembler.level;
            SetHeader(title, building);
            ShowCrafterUI(crafter);
        }
        else if (building is PowerGenerator gen)
        {
            SetHeader(building.data != null ? building.data.displayName : "Generator", building);
            ShowPowerUI(gen);
        }
        else if (building is ResearchLab)
        {
            SetHeader("Research Lab", building);
            ShowResearchLabUI();
        }
        else if (building is StorageContainer storage)
        {
            SetHeader(building.data != null ? building.data.displayName : "Склад", building);
            ShowStorageUI(storage);
        }
        else if (building is RoboticArm arm)
        {
            SetHeader("Роборука", building);
            ShowRoboticArmUI(arm);
        }
        else if (building is Conveyor belt)
        {
            SetHeader(building.data != null ? building.data.displayName : "Конвейер", building);
            ShowConveyorUI(belt);
        }
        else
        {
            SetHeader(building.data != null ? building.data.displayName : "Building", building);
            IndustryUi.Show(bodyList, true);
        }

        BindUpgrade(building);
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

    public void ToggleLab()
    {
        if (IsOpen && IsLabView)
            Close();
        else
            OpenLab();
    }

    public void OpenLab()
    {
        ResearchLab lab = FindWorldLab();
        if (lab != null)
        {
            Open(lab);
            return;
        }

        BindLiveEvents();
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);
        if (overlay == null)
            Build();
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            WalletHud.Instance.SetShopOpen(false);
        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
            SelectionActionsUI.Instance.SetOpen(false);

        currentBuilding = null;
        IsLabView = true;
        IsOpen = true;
        HidePages();
        BuildingData labData = GameDatabase.FindBuilding("research_lab");
        IndustryUi.SetHeader(overlay, labData != null ? labData.displayName : "Research Lab", labData != null ? labData.icon : null);
        ShowResearchLabUI();
        BindUpgrade(null);
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

    static ResearchLab FindWorldLab()
    {
        ResearchLab[] labs = UnityEngine.Object.FindObjectsByType<ResearchLab>(FindObjectsSortMode.None);
        for (int i = 0; i < labs.Length; i++)
        {
            if (labs[i] != null && labs[i].IsWorldLab)
                return labs[i];
        }

        return labs.Length > 0 ? labs[0] : null;
    }

    public void Close()
    {
        IsOpen = false;
        IsLabView = false;
        currentBuilding = null;
        KeybindStore.SuppressGameplay();
        IndustryUi.Show(upgradeBtn, false);
        IndustryUi.Show(overlay, false);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            SetPlayerControl(true);
        }
    }

    void SetHeader(string title, BuildingBase building)
    {
        Sprite icon = building != null && building.data != null ? building.data.icon : null;
        IndustryUi.SetHeader(overlay, title, icon);
    }

    void SetStatus(string text, UiStatus status)
    {
        if (statusLabel != null)
            statusLabel.text = text ?? "";
        VisualElement row = statusLabel != null ? statusLabel.parent : null;
        if (row != null)
            UiStatusUtil.Apply(row, status);
    }

    static void SetPlayerControl(bool enabled)
    {
        PlayerMovement movement = FindFirstObjectByType<PlayerMovement>();
        if (movement == null)
            return;
        movement.canMove = enabled;
        movement.canLook = enabled;
    }

    void HidePages()
    {
        IndustryUi.Show(tabRow, false);
        IndustryUi.Show(bodyList, false);
        IndustryUi.Show(researchPage, false);
        IndustryUi.Show(recipePage, false);
        IndustryUi.Show(beltPage, false);
        IndustryUi.Show(statsPage, false);
        IndustryUi.Show(idlePage, false);
        IndustryUi.Show(storageSummary, false);
        IndustryUi.Show(storageScroll, false);
        IndustryUi.Show(armSummary, false);
        IndustryUi.Show(armScroll, false);
        IndustryUi.Show(recipeSearchHost, false);
        IndustryUi.Show(filterSearchHost, false);
        IndustryUi.Show(progress, false);
        if (statusLabel != null)
        {
            statusLabel.text = "";
            VisualElement row = statusLabel.parent;
            if (row != null)
                UiStatusUtil.Apply(row, UiStatus.Neutral);
        }
    }

    void ShowOilExtractorUI(OilExtractor oil)
    {
        IndustryUi.Show(bodyList, true);
        bodyList.Clear();
        var outputs = new List<ItemStack>();
        if (oil.resource != null)
            outputs.Add(new ItemStack(oil.resource, oil.CurrentItemsPerCycle));
        string title = oil.resource != null ? "Добыча  " + oil.resource.displayName : "Нет нефти";
        bodyList.Add(IndustryUi.RecipeCard(title, null, outputs, true, null));
        bodyList.Add(IndustryUi.ActionCard(
            "Скважина",
            oil.RichnessName + "  ·  " + oil.CurrentInterval.ToString("0.##") + " с / цикл  ·  бесконечный запас",
            false,
            null));
    }

    void ShowWaterExtractorUI(WaterExtractor pump)
    {
        IndustryUi.Show(bodyList, true);
        bodyList.Clear();
        var outputs = new List<ItemStack>();
        if (pump.resource != null)
            outputs.Add(new ItemStack(pump.resource, pump.CurrentItemsPerCycle));
        string title = pump.resource != null ? "Добыча  " + pump.resource.displayName : "Нет воды";
        bodyList.Add(IndustryUi.RecipeCard(title, null, outputs, true, null));
        bodyList.Add(IndustryUi.ActionCard(
            "Источник",
            pump.CurrentInterval.ToString("0.##") + " с / цикл  ·  бесконечный запас",
            false,
            null));
    }

    void ShowPowerUI(PowerGenerator gen)
    {
        IndustryUi.Show(bodyList, true);
        bodyList.Clear();
        string fuel = gen.Powered
            ? UiLocale.T("machine.power_on", gen.RemainingFuel.ToString("0.0"))
            : UiLocale.T("machine.power_off");
        bodyList.Add(IndustryUi.ActionCard(
            UiLocale.T("machine.powered"),
            fuel + "  ·  ×" + gen.speedMultiplier.ToString("0.0") + "  ·  " + gen.powerRadius.ToString("0") + " м",
            gen.Powered,
            null));
    }

    void ShowExtractorUI(Extractor extractor)
    {
        IndustryUi.Show(bodyList, true);
        bodyList.Clear();
        var outputs = new List<ItemStack>();
        if (extractor.resource != null)
            outputs.Add(new ItemStack(extractor.resource, extractor.CurrentItemsPerCycle));
        string title = extractor.resource != null
            ? "Mining  " + extractor.resource.displayName
            : "No resource node";
        bodyList.Add(IndustryUi.RecipeCard(title, null, outputs, true, null));
        bool front = extractor.requireFrontOutput;
        bodyList.Add(IndustryUi.ActionCard(
            UiLocale.T("machine.extractor_front"),
            front ? UiLocale.T("machine.extractor_front_on") : UiLocale.T("machine.extractor_front_off"),
            false,
            () =>
            {
                extractor.requireFrontOutput = !extractor.requireFrontOutput;
                ShowExtractorUI(extractor);
            }));
        float power = PowerGenerator.GetNearbySpeedMultiplier(extractor.transform.position);
        if (power > 1.01f)
            bodyList.Add(IndustryUi.ActionCard(UiLocale.T("machine.powered"), "×" + power.ToString("0.0"), true, null));
    }

    void ShowCrafterUI(CrafterBuilding crafter)
    {
        IndustryUi.Show(recipeSearchHost, true);
        IndustryUi.Show(bodyList, true);
        IndustryUi.Show(progress, true);
        if (recipeSearch != null)
            recipeSearch.value = "";
        RefreshRecipeList(crafter);
    }

    void RefreshRecipeList(CrafterBuilding crafter)
    {
        if (bodyList == null)
            return;
        bodyList.Clear();
        if (crafter == null)
            return;

        RecipeData[] catalog = GameDatabase.AllRecipes();
        BuildingData thisBuildingData = crafter.data;
        RecipeData selected = crafter.currentRecipe;
        string query = recipeSearch != null ? recipeSearch.value : "";

        if (catalog != null)
        {
            for (int i = 0; i < catalog.Length; i++)
            {
                RecipeData recipe = catalog[i];
                if (recipe == null)
                    continue;
                if (!recipe.AllowsBuilding(thisBuildingData))
                    continue;
                if (ResearchSystem.Instance != null && !ResearchSystem.Instance.IsRecipeUnlocked(recipe))
                    continue;
                if (!MatchesRecipe(recipe, query))
                    continue;

                RecipeData captured = recipe;
                VisualElement card = IndustryUi.RecipeCard(
                    recipe,
                    selected == recipe,
                    () =>
                    {
                        crafter.SetRecipe(captured);
                        RefreshRecipeList(crafter);
                    });
                if (!string.IsNullOrEmpty(recipe.id))
                    card.name = "Rec_" + recipe.id.Trim();
                bodyList.Add(card);
            }
        }

        SetStatus(
            selected != null ? selected.displayName : "Select a recipe",
            selected != null ? UiStatus.Running : UiStatus.Ready);
    }

    static bool TryPaidUpgrade(BuildingBase building)
    {
        if (building == null || !building.CanUpgradeBuilding)
            return false;
        int cost = Economy.UpgradeCost(building);
        if (PlayerWallet.Instance != null && !PlayerWallet.Instance.TrySpendCoins(cost))
            return false;
        if (building.TryUpgradeBuilding())
            return true;
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.AddCoins(cost);
        return false;
    }

    void BindUpgrade(BuildingBase building)
    {
        bool can = building != null && building.CanUpgradeBuilding;
        IndustryUi.Show(upgradeBtn, can);
        if (!can || upgradeBtn == null)
            return;

        int cost = Economy.UpgradeCost(building);
        string money = IndustryUi.Money(cost);
        bool canPay = PlayerWallet.Instance == null || PlayerWallet.Instance.CanAfford(cost);
        IndustryUi.SetButtonLabel(upgradeBtn, UiLocale.T("machine.upgrade"));
        upgradeTipTitle = UiLocale.T("machine.upgrade_cost", money);
        if (building is Extractor extractor)
        {
            string now = extractor.CurrentInterval.ToString("0.##") + " с / " + extractor.CurrentItemsPerCycle;
            string next = (extractor.upgradedExtractInterval * Economy.ExtractTimeMul).ToString("0.##") + " с / "
                + Mathf.Max(1, extractor.upgradedItemsPerCycle);
            upgradeTipBody = UiLocale.T("machine.upgrade_ext", now, next);
        }
        else if (building is CrafterBuilding crafter)
        {
            upgradeTipBody = UiLocale.T("machine.upgrade_craft", "×" + crafter.CraftSpeed.ToString("0.##"));
        }
        else
            upgradeTipBody = "";

        if (!canPay)
            upgradeTipBody = string.IsNullOrEmpty(upgradeTipBody)
                ? UiLocale.T("machine.upgrade_need")
                : upgradeTipBody + "  ·  " + UiLocale.T("machine.upgrade_need");
    }

    void OnUpgradeClicked()
    {
        if (currentBuilding == null || !currentBuilding.CanUpgradeBuilding)
            return;
        if (!TryPaidUpgrade(currentBuilding))
        {
            UiAudio.PlayError();
            return;
        }
        GameAudio.World("world_upgrade", currentBuilding.transform.position);
        Open(currentBuilding);
    }

    void OnUpgradeHover(PointerEnterEvent evt)
    {
        if (upgradeBtn == null || upgradeBtn.style.display == DisplayStyle.None)
            return;
        UiTooltip.Show(upgradeBtn, upgradeTipTitle, upgradeTipBody, (Vector2)evt.position);
    }

    static bool MatchesRecipe(RecipeData recipe, string query)
    {
        if (string.IsNullOrEmpty(query))
            return true;
        if (MatchesQuery(recipe.displayName, query) || MatchesQuery(recipe.id, query))
            return true;
        if (MatchesStacks(recipe.inputs, query) || MatchesStacks(recipe.outputs, query))
            return true;
        return false;
    }

    static bool MatchesStacks(IList<ItemStack> stacks, string query)
    {
        if (stacks == null)
            return false;
        for (int i = 0; i < stacks.Count; i++)
        {
            ItemData item = stacks[i] != null ? stacks[i].item : null;
            if (item != null && (MatchesQuery(item.displayName, query) || MatchesQuery(item.id, query)))
                return true;
        }
        return false;
    }

    static bool MatchesQuery(string value, string query)
    {
        if (string.IsNullOrEmpty(query))
            return true;
        return !string.IsNullOrEmpty(value)
            && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    void ShowStorageUI(StorageContainer storage)
    {
        IndustryUi.Show(storageSummary, true);
        IndustryUi.Show(storageScroll, true);
        RefreshStorageSlots(storage);
    }

    void RefreshStorageSlots(StorageContainer storage)
    {
        if (storage == null || storageGrid == null)
            return;

        int count = storage.SlotCount;
        while (storageSlots.Count < count)
        {
            VisualElement cell = IndustryUi.StorageCell();
            storageSlots.Add(cell);
            storageGrid.Add(cell);
        }

        for (int i = 0; i < storageSlots.Count; i++)
        {
            IndustryUi.Show(storageSlots[i], i < count);
            if (i < count)
                IndustryUi.BindStorage(storageSlots[i], storage.GetSlot(i));
        }

        if (storageSummary != null)
        {
            string typeName = storage.StoredType != null ? storage.StoredType.displayName : "пусто";
            storageSummary.text = $"{typeName}  ·  стеки {storage.UsedSlotCount} / {count}";
        }
    }

    void ShowConveyorUI(Conveyor belt)
    {
        IndustryUi.Show(armSummary, true);
        IndustryUi.Show(filterSearchHost, true);
        IndustryUi.Show(armScroll, true);
        if (filterSearch != null)
            filterSearch.value = "";
        RefreshBeltFilter(belt);
    }

    void RefreshBeltFilter(Conveyor belt)
    {
        if (belt == null || armGrid == null)
            return;
        armGrid.Clear();
        string query = filterSearch != null ? filterSearch.value : "";
        if (MatchesQuery(UiLocale.T("machine.filter_any"), query))
        {
            armGrid.Add(IndustryUi.FilterCard(
                null,
                UiLocale.T("machine.filter_any"),
                belt.Filter == null ? UiLocale.T("machine.filter_on") : UiLocale.T("machine.filter_off"),
                belt.Filter == null,
                () =>
                {
                    belt.SetFilter(null);
                    RefreshBeltFilter(belt);
                }));
        }

        bool fluids = belt is Pipe;
        var seen = new HashSet<string>();
        ItemData[] items = GameDatabase.AllItems();
        if (items != null)
        {
            for (int i = 0; i < items.Length; i++)
            {
                ItemData item = items[i];
                if (item == null || string.IsNullOrEmpty(item.id) || !seen.Add(item.id))
                    continue;
                if (item.isFluid != fluids)
                    continue;
                if (!MatchesQuery(item.displayName, query) && !MatchesQuery(item.id, query))
                    continue;
                ItemData captured = item;
                bool selected = belt.Filter == item;
                armGrid.Add(IndustryUi.FilterCard(
                    item.icon,
                    item.displayName,
                    selected ? UiLocale.T("machine.filter_set") : UiLocale.T("machine.filter_only"),
                    selected,
                    () =>
                    {
                        belt.SetFilter(captured);
                        RefreshBeltFilter(belt);
                    }));
            }
        }

        if (armSummary != null)
        {
            string name = belt.Filter != null ? belt.Filter.displayName : UiLocale.T("machine.filter_any");
            armSummary.text = UiLocale.T("machine.belt_filter", name);
        }
    }

    void ShowRoboticArmUI(RoboticArm arm)
    {
        IndustryUi.Show(armSummary, true);
        IndustryUi.Show(filterSearchHost, true);
        IndustryUi.Show(armScroll, true);
        if (filterSearch != null)
            filterSearch.value = "";
        RefreshArmFilter(arm);
    }

    void RefreshArmFilter(RoboticArm arm)
    {
        if (arm == null || armGrid == null)
            return;

        armGrid.Clear();
        string query = filterSearch != null ? filterSearch.value : "";
        if (MatchesQuery(UiLocale.T("machine.filter_any"), query))
        {
            armGrid.Add(IndustryUi.FilterCard(
                null,
                UiLocale.T("machine.filter_any"),
                arm.filter == null ? UiLocale.T("machine.filter_on") : UiLocale.T("machine.filter_off"),
                arm.filter == null,
                () =>
                {
                    arm.SetFilter(null);
                    RefreshArmFilter(arm);
                }));
        }

        var seen = new HashSet<string>();
        ItemData[] items = GameDatabase.AllItems();
        if (items != null)
        {
            for (int i = 0; i < items.Length; i++)
            {
                ItemData item = items[i];
                if (item == null || string.IsNullOrEmpty(item.id) || !seen.Add(item.id))
                    continue;
                if (!MatchesQuery(item.displayName, query) && !MatchesQuery(item.id, query))
                    continue;
                ItemData captured = item;
                bool selected = arm.filter == item;
                armGrid.Add(IndustryUi.FilterCard(
                    item.icon,
                    item.displayName,
                    selected ? UiLocale.T("machine.filter_set") : UiLocale.T("machine.filter_only"),
                    selected,
                    () =>
                    {
                        arm.SetFilter(captured);
                        RefreshArmFilter(arm);
                    }));
            }
        }

        UpdateArmSummary(arm);
    }

    void UpdateArmSummary(RoboticArm arm)
    {
        if (armSummary == null || arm == null)
            return;
        string name = arm.filter != null ? arm.filter.displayName : "любые";
        string held = arm.HeldItem != null ? arm.HeldItem.displayName : "пусто";
        armSummary.text = $"Фильтр: {name}   ·   в руке: {held}";
    }

    void ShowResearchLabUI()
    {
        IndustryUi.Show(tabRow, true);
        IndustryUi.Show(progress, true);
        OpenLabTab(labTab);
    }

    void OnLiveChanged()
    {
        if (!IsOpen)
            return;
        if (IsLabView)
        {
            BindUpgrade(currentBuilding);
            if (labTab == TabResearch)
                FillResearchTab();
            else if (labTab == TabRecipes)
                FillRecipeTab();
            else if (labTab == TabBelts)
                RebuildBeltTree();
            else if (labTab == TabIdle)
                RebuildIdleList();
            else
                RebuildStatsList();
            return;
        }

        if (currentBuilding == null)
            return;
        BindUpgrade(currentBuilding);
    }

    void OpenLabTab(int tab)
    {
        if (labTab != tab)
            UiAudio.PlayTab();
        labTab = tab;
        IndustryUi.Show(researchPage, tab == TabResearch);
        IndustryUi.Show(recipePage, tab == TabRecipes);
        IndustryUi.Show(beltPage, tab == TabBelts);
        IndustryUi.Show(statsPage, tab == TabStats);
        IndustryUi.Show(idlePage, tab == TabIdle);
        IndustryUi.SetOn(tabResearch, tab == TabResearch, "tab-on");
        IndustryUi.SetOn(tabRecipes, tab == TabRecipes, "tab-on");
        IndustryUi.SetOn(tabBelts, tab == TabBelts, "tab-on");
        IndustryUi.SetOn(tabStats, tab == TabStats, "tab-on");
        IndustryUi.SetOn(tabIdle, tab == TabIdle, "tab-on");

        if (tab == TabResearch)
            FillResearchTab();
        else if (tab == TabRecipes)
            FillRecipeTab();
        else if (tab == TabBelts)
            RebuildBeltTree();
        else if (tab == TabIdle)
            RebuildIdleList();
        else
            RebuildStatsList();
    }

    void FillRecipeTab()
    {
        if (recipeList == null)
            return;
        recipeList.Clear();
        string query = codexSearch != null ? codexSearch.value : "";
        List<RecipeCodex.Entry> entries = RecipeCodex.Build();
        for (int i = 0; i < entries.Count; i++)
        {
            RecipeCodex.Entry entry = entries[i];
            if (entry.item == null)
                continue;
            if (!RecipeCodex.Matches(entry, query))
                continue;
            recipeList.Add(IndustryUi.CodexCard(entry));
        }
    }

    void FillResearchTab()
    {
        if (researchCanvas == null || ResearchSystem.Instance == null)
            return;

        if (string.IsNullOrEmpty(selectedResearchId))
        {
            ResearchNodeData pick = ResearchTree.DefaultSelection();
            selectedResearchId = pick != null ? pick.id : null;
        }

        int nodes = CountResearchNodes();
        if (researchCanvas.childCount != nodes || !researchCanvas.ClassListContains("tree-rev3"))
        {
            ResearchTree.Build(researchCanvas, OnPickResearch, selectedResearchId);
            ScrollResearchIntoView(selectedResearchId);
        }
        else
            ResearchTree.ApplyStatus(researchCanvas, selectedResearchId);

        ResearchNodeData selected = FindResearchNode(selectedResearchId);
        ResearchTree.FillDetail(researchDetail, selected, () => StartPickedResearch(selected));
    }

    void OnPickResearch(ResearchNodeData node)
    {
        if (node == null)
            return;
        selectedResearchId = node.id;
        FillResearchTab();
    }

    void StartPickedResearch(ResearchNodeData node)
    {
        if (node == null || ResearchSystem.Instance == null)
            return;
        if (ResearchSystem.Instance.SetCurrentResearch(node))
            UiAudio.PlayConfirm();
    }

    void ScrollResearchIntoView(string id)
    {
        if (researchCanvas == null || string.IsNullOrEmpty(id))
            return;
        VisualElement card = researchCanvas.Q("Res_" + id);
        if (card != null)
            researchNav.Focus(card);
    }

    static int CountResearchNodes()
    {
        if (ResearchSystem.Instance == null)
            return 0;
        List<ResearchNodeData> nodes = ResearchSystem.Instance.GetAllNodes();
        int n = 0;
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null)
                n++;
        }

        return n;
    }

    static ResearchNodeData FindResearchNode(string id)
    {
        if (string.IsNullOrEmpty(id) || ResearchSystem.Instance == null)
            return null;
        List<ResearchNodeData> nodes = ResearchSystem.Instance.GetAllNodes();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && GameDatabase.Normalize(nodes[i].id) == GameDatabase.Normalize(id))
                return nodes[i];
        }

        return GameDatabase.FindResearch(id);
    }

    void RebuildBeltTree()
    {
        if (beltList == null)
            return;

        beltList.Clear();
        BeltSpeedSystem belts = BeltSpeedSystem.Instance;
        int level = belts != null ? belts.Level : 0;
        int have = belts != null ? belts.GearsTowardNext : 0;
        int max = Economy.BeltMaxLevel;
        ItemData gear = GameDatabase.FindItem("gear");
        Sprite gearIcon = gear != null ? gear.icon : null;

        beltList.Add(IndustryUi.Text("Head", UiLocale.T("belt.title"), "title"));
        beltList.Add(IndustryUi.Text("Hint", UiLocale.T("belt.hint"), "muted"));

        if (belts != null && belts.IsMaxed)
        {
            beltList.Add(IndustryUi.Text("Max", UiLocale.T("belt.maxed", belts.Multiplier.ToString("0.##")), "gold"));
        }
        else
        {
            int gearCost = belts != null ? belts.NextGearCost : Economy.BeltGearCost(1);
            int coinCost = belts != null ? belts.NextCoinCost : Economy.BeltCoinCost(1);
            int left = Mathf.Max(0, gearCost - have);
            bool gearsReady = belts != null && belts.GearsReady;
            bool canBuy = belts != null && belts.CanBuyNext;
            string buyLabel = canBuy
                ? UiLocale.T("belt.buy", IndustryUi.Money(coinCost))
                : !gearsReady
                    ? UiLocale.T("belt.need_gears", left)
                    : UiLocale.T("belt.need_coins");
            Button buy = IndustryUi.Btn(buyLabel, () =>
            {
                if (BeltSpeedSystem.Instance != null && BeltSpeedSystem.Instance.TryBuyNext())
                    UiAudio.PlayConfirm();
            }, "btn-primary");
            buy.SetEnabled(canBuy);
            beltList.Add(buy);
            beltList.Add(IndustryUi.Text("Pay", UiLocale.T("belt.coins", IndustryUi.Money(coinCost)), "gold"));
        }

        int lastShown = Mathf.Min(max, level + 1);
        for (int i = 0; i <= lastShown; i++)
        {
            bool unlocked = i <= level;
            bool next = i == level + 1 && i <= max;
            float speedNow = Economy.BeltMultiplier(i);
            float speedPrev = i == 0 ? speedNow : Economy.BeltMultiplier(i - 1);
            int cost = i == 0 ? 0 : Economy.BeltGearCost(i);
            string state = unlocked
                ? (i == level ? UiLocale.T("belt.current") : UiLocale.T("belt.open"))
                : UiLocale.T("belt.next");
            string speedText = i == 0
                ? UiLocale.T("belt.speed", speedNow.ToString("0.##"))
                : UiLocale.T("belt.speed_to", speedPrev.ToString("0.##"), speedNow.ToString("0.##"));
            string costText = i == 0
                ? UiLocale.T("belt.start")
                : next
                    ? UiLocale.T("belt.gears", have, cost)
                    : UiLocale.T("belt.gears_cost", cost);

            beltList.Add(BeltNode(
                UiLocale.T("belt.level", i) + "  ·  " + state,
                speedText,
                costText,
                gearIcon,
                unlocked,
                next));
        }
    }

    static VisualElement BeltNode(string title, string speed, string cost, Sprite icon, bool unlocked, bool next)
    {
        var row = IndustryUi.El("Belt", "card");
        if (next)
            row.AddToClassList("card-on");
        var mark = IndustryUi.El("Mark", "belt-mark");
        mark.style.backgroundColor = next
            ? new Color(0.86f, 0.65f, 0.29f)
            : unlocked
                ? new Color(0.43f, 0.77f, 0.51f)
                : new Color(0.35f, 0.38f, 0.4f);
        row.Add(mark);
        row.Add(IndustryUi.Icon(icon, "icon-32"));
        var col = IndustryUi.El("C", "col", "grow");
        col.Add(IndustryUi.Text("T", title, unlocked || next ? "body-text" : "muted"));
        col.Add(IndustryUi.Text("S", speed, "gold"));
        col.Add(IndustryUi.Text("K", cost, next ? "warn" : "muted"));
        row.Add(col);
        return row;
    }

    void RebuildIdleList()
    {
        if (idleList == null)
            return;
        idleList.Clear();
        MachineIdleHud watch = MachineIdleHud.Instance;
        if (watch == null || watch.Rows.Count == 0)
        {
            idleList.Add(IndustryUi.Text("Empty", UiLocale.T("idle.empty"), "muted"));
            nextStatsRefresh = Time.unscaledTime + 0.8f;
            return;
        }

        bool canTp = MapSettings.AllowTeleport;
        for (int i = 0; i < watch.Rows.Count; i++)
        {
            MachineIdleHud.IdleRow row = watch.Rows[i];
            if (row.building == null)
                continue;
            string why = row.reason == "input"
                ? UiLocale.T("idle.no_input")
                : UiLocale.T("idle.output_full");
            string sub = why + "  ·  " + row.cell.x + "," + row.cell.y;
            Vector2Int cell = row.cell;
            idleList.Add(IndustryUi.ActionCard(
                row.Name,
                canTp ? sub + "  ·  " + UiLocale.T("map.teleport") : sub,
                canTp,
                canTp ? () => JumpIdle(cell) : null));
        }

        nextStatsRefresh = Time.unscaledTime + 0.8f;
    }

    static void JumpIdle(Vector2Int cell)
    {
        if (!MapSettings.AllowTeleport)
            return;
        if (Instance != null)
            Instance.Close();
        PlayerMovement player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        if (player != null)
            player.TeleportToCell(cell);
    }

    void RebuildStatsList()
    {
        if (statsList == null)
            return;

        statsList.Clear();
        ProductionStats stats = ProductionStats.Instance;
        PlayerWallet wallet = PlayerWallet.Instance;

        AddMoneyStat(
            GameHudIcons.Coin,
            "Монеты",
            wallet != null ? wallet.Coins : 0,
            stats != null ? stats.CoinsPerMinute() : 0f,
            stats != null ? stats.CoinsSpentPerMinute() : 0f,
            stats != null ? stats.CoinsGainedTotal : 0,
            stats != null ? stats.CoinsSpentTotal : 0);
        AddMoneyStat(
            GameHudIcons.Ruby,
            "Рубины",
            wallet != null ? wallet.Rubies : 0,
            stats != null ? stats.RubiesPerMinute() : 0f,
            0f,
            stats != null ? stats.RubiesGainedTotal : 0,
            0);

        if (stats != null)
        {
            var ids = new HashSet<string>();
            foreach (var pair in stats.ProducedTotal)
                ids.Add(pair.Key);
            foreach (var pair in stats.ConsumedTotal)
                ids.Add(pair.Key);
            var sorted = new List<string>(ids);
            sorted.Sort();
            float maxRate = 0.01f;
            for (int i = 0; i < sorted.Count; i++)
            {
                maxRate = Mathf.Max(maxRate, stats.ProducedPerMinute(sorted[i]));
                maxRate = Mathf.Max(maxRate, stats.ConsumedPerMinute(sorted[i]));
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                string id = sorted[i];
                ItemData item = GameDatabase.FindItem(id);
                stats.ProducedTotal.TryGetValue(id, out int made);
                stats.ConsumedTotal.TryGetValue(id, out int used);
                float plus = stats.ProducedPerMinute(id);
                float minus = stats.ConsumedPerMinute(id);
                statsList.Add(IndustryUi.RateBar(
                    item != null ? item.icon : null,
                    (item != null ? item.displayName : id) + "   всего +" + made + " −" + used,
                    plus,
                    minus,
                    maxRate));
            }
        }

        nextStatsRefresh = Time.unscaledTime + 0.6f;
    }

    void AddMoneyStat(Sprite icon, string name, int now, float plusMin, float minusMin, int gained, int spent)
    {
        string detail = "сейчас " + now + "   всего +" + gained + (spent > 0 ? "  −" + spent : "")
            + "   ·   +" + plusMin.ToString("0.#") + "/мин"
            + (minusMin > 0f ? "   −" + minusMin.ToString("0.#") + "/мин" : "");
        statsList.Add(IndustryUi.StatRow(icon, name, detail));
    }

    void UpdateProgress()
    {
        if (currentBuilding is CrafterBuilding crafter)
        {
            float t = 0f;
            float need = Economy.CraftNeed(crafter.currentRecipe);
            if (need > 0f)
                t = crafter.craftProgress / need;
            IndustryUi.SetProgress(progress, t);
        }
        else if (currentBuilding is StorageContainer storage)
        {
            RefreshStorageSlots(storage);
        }
        else if (currentBuilding is RoboticArm arm)
        {
            UpdateArmSummary(arm);
        }
        else if (IsLabView || currentBuilding is ResearchLab)
        {
            ResearchSystem rs = ResearchSystem.Instance;
            int active = rs != null ? rs.GetAvailableResearch().Count : 0;
            float t = rs != null ? rs.GetCurrentProgress01() : 0f;
            IndustryUi.SetProgress(progress, t);
            string name = active > 0
                ? UiLocale.T("research.active_count", active)
                : UiLocale.T("research.none");
            SetStatus(name + "  " + (t * 100f).ToString("0") + "%",
                active > 0 ? UiStatus.Running : UiStatus.Ready);

            if (labTab == TabStats && Time.unscaledTime >= nextStatsRefresh)
                RebuildStatsList();
            if (labTab == TabIdle && Time.unscaledTime >= nextStatsRefresh)
                RebuildIdleList();
        }
    }
}
