using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class SelectionActionsUI : MonoBehaviour
{
    public static SelectionActionsUI Instance { get; private set; }

    BuildSelectionController selection;
    VisualElement overlay;
    VisualElement mainView;
    VisualElement recipeView;
    ScrollView list;
    ScrollView recipeList;
    Label summary;
    Label recipeTitle;
    List<BuildingBase> recipeBucket;
    int lastCount = -1;
    InputAction panelAction;

    bool open;
    public bool IsOpen => open;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        selection = FindFirstObjectByType<BuildSelectionController>();
        Build();
        SetOpen(false);
        BindInput();
    }

    void OnDestroy()
    {
        if (panelAction != null)
            panelAction.performed -= OnPanelPerformed;
        if (Instance == this)
            Instance = null;
    }

    void BindInput()
    {
        if (panelAction != null)
            return;
        InputSystem_Actions actions = KeybindStore.Shared;
        panelAction = actions != null ? actions.asset.FindAction("Player/SelectionPanel", false) : null;
        if (panelAction != null)
            panelAction.performed += OnPanelPerformed;
    }

    void OnPanelPerformed(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        Toggle();
    }

    void Update()
    {
        if (selection == null)
            selection = FindFirstObjectByType<BuildSelectionController>();
        if (panelAction == null
            && !KeybindStore.BlocksGameplayInput
            && Keyboard.current != null
            && Keyboard.current.oKey.wasPressedThisFrame)
            Toggle();

        int count = selection != null ? selection.SelectedBuildings.Count : 0;
        if (count != lastCount)
        {
            lastCount = count;
            if (IsOpen)
                Rebuild();
        }
    }

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 85);
        overlay = IndustryUi.OverlayPanel(UiLocale.T("overlay.selected"), null, () => SetOpen(false));
        VisualElement panel = IndustryUi.PanelOf(overlay);
        summary = IndustryUi.Text("Summary", "", "muted");
        panel.Add(summary);
        mainView = IndustryUi.El("Main", "col", "grow");
        list = new ScrollView();
        list.AddToClassList("scroll");
        mainView.Add(list);
        panel.Add(mainView);

        recipeView = IndustryUi.El("Recipes", "col", "grow");
        var recHead = IndustryUi.El("RH", "row");
        recipeTitle = IndustryUi.Text("RT", UiLocale.T("select.recipe_title"), "title", "grow");
        recHead.Add(recipeTitle);
        recHead.Add(IndustryUi.Btn(UiLocale.T("menu.back"), ShowMain, "btn-small"));
        recipeView.Add(recHead);
        recipeList = new ScrollView();
        recipeView.Add(recipeList);
        IndustryUi.Show(recipeView, false);
        panel.Add(recipeView);
        root.Add(overlay);
    }

    public void Toggle()
    {
        SetOpen(!IsOpen);
    }

    public void SetOpen(bool open)
    {
        this.open = open;
        IndustryUi.Show(overlay, open);
        if (!open)
            ShowMain();
        if (open && WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            WalletHud.Instance.SetShopOpen(false);
        if (open)
            Rebuild();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    void ShowMain()
    {
        IndustryUi.Show(mainView, true);
        IndustryUi.Show(recipeView, false);
    }

    void Rebuild()
    {
        if (list == null)
            return;
        list.Clear();

        var groups = new Dictionary<string, List<BuildingBase>>();
        var order = new List<string>();
        IReadOnlyList<BuildingBase> buildings = selection != null
            ? selection.SelectedBuildings
            : (IReadOnlyList<BuildingBase>)System.Array.Empty<BuildingBase>();

        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingBase b = buildings[i];
            if (b == null || b.data == null)
                continue;
            string key = GroupKey(b);
            if (!groups.TryGetValue(key, out List<BuildingBase> bucket))
            {
                bucket = new List<BuildingBase>();
                groups[key] = bucket;
                order.Add(key);
            }
            bucket.Add(b);
        }

        order.Sort();
        summary.text = buildings.Count == 0
            ? UiLocale.T("select.empty")
            : UiLocale.T("select.count", buildings.Count, groups.Count);

        for (int i = 0; i < order.Count; i++)
            AddGroupCard(groups[order[i]]);
    }

    static string GroupKey(BuildingBase building)
    {
        string typeId = building.data != null ? building.data.id : building.GetType().Name;
        string extra = "";
        if (building is CrafterBuilding crafter)
            extra = crafter.currentRecipe != null ? crafter.currentRecipe.id : "none";
        else if (building is Extractor extractor)
            extra = extractor.resource != null ? extractor.resource.id : "none";
        else if (building is OilExtractor oil)
            extra = oil.resource != null ? oil.resource.id : "oil";
        else if (building is WaterExtractor water)
            extra = water.resource != null ? water.resource.id : "water";
        return typeId + "|lv" + building.ReadLevel() + "|" + extra;
    }

    void AddGroupCard(List<BuildingBase> bucket)
    {
        BuildingData data = bucket[0].data;
        int level = bucket[0].ReadLevel();
        CrafterBuilding crafter = bucket[0] as CrafterBuilding;
        RecipeData recipe = crafter != null ? crafter.currentRecipe : null;
        ItemData node = null;
        if (bucket[0] is Extractor ex) node = ex.resource;
        else if (bucket[0] is OilExtractor oil) node = oil.resource;
        else if (bucket[0] is WaterExtractor water) node = water.resource;

        Sprite icon = data != null ? data.icon : null;
        if (recipe != null && recipe.outputs != null && recipe.outputs.Count > 0 && recipe.outputs[0].item != null)
            icon = recipe.outputs[0].item.icon;
        else if (node != null)
            icon = node.icon;

        string title = (data != null ? data.displayName : "Здание").ToUpperInvariant() + "  ×" + bucket.Count;
        string detail = "ур. " + level + (recipe != null ? "  ·  " + recipe.displayName : crafter != null ? "  ·  рецепт не выбран" : node != null ? "  ·  " + node.displayName : "");

        var card = IndustryUi.El("G", "card");
        card.Add(IndustryUi.Icon(icon, "icon-48"));
        var col = IndustryUi.El("C", "col", "grow");
        col.Add(IndustryUi.Text("T", title, "heading-3"));
        col.Add(IndustryUi.Text("D", detail, "caption"));
        var actions = IndustryUi.El("A", "row");
        int upgradeable = 0;
        int cost = 0;
        for (int i = 0; i < bucket.Count; i++)
        {
            if (bucket[i] != null && bucket[i].CanUpgradeBuilding)
            {
                upgradeable++;
                cost += Economy.UpgradeCost(bucket[i]);
            }
        }

        bool canPay = upgradeable > 0 && (PlayerWallet.Instance == null || PlayerWallet.Instance.CanAfford(cost));
        string upLabel = upgradeable > 0
            ? UiLocale.T("select.upgrade", upgradeable, IndustryUi.Money(cost))
            : UiLocale.T("select.max");
        actions.Add(IndustryUi.Btn(upLabel, () =>
        {
            if (canPay)
                UpgradeGroup(bucket);
        }, "btn-small", canPay ? "btn-primary" : "btn-ghost"));
        if (crafter != null)
            actions.Add(IndustryUi.Btn(UiLocale.T("select.recipe"), () => OpenRecipes(bucket), "btn-small"));
        col.Add(IndustryUi.Text("Hint", UiLocale.T("select.dismantle"), "caption"));
        col.Add(actions);
        card.Add(col);
        list.Add(card);
    }

    void UpgradeGroup(List<BuildingBase> bucket)
    {
        for (int i = 0; i < bucket.Count; i++)
        {
            BuildingBase b = bucket[i];
            if (b == null || !b.CanUpgradeBuilding)
                continue;
            int cost = Economy.UpgradeCost(b);
            if (PlayerWallet.Instance != null && !PlayerWallet.Instance.TrySpendCoins(cost))
                break;
            if (!b.TryUpgradeBuilding() && PlayerWallet.Instance != null)
                PlayerWallet.Instance.AddCoins(cost);
        }
        Rebuild();
    }

    void OpenRecipes(List<BuildingBase> bucket)
    {
        recipeBucket = bucket;
        IndustryUi.Show(mainView, false);
        IndustryUi.Show(recipeView, true);
        recipeTitle.text = bucket[0].data != null
            ? "Рецепт: " + bucket[0].data.displayName + "  ур. " + bucket[0].ReadLevel()
            : "Рецепт";
        recipeList.Clear();
        BuildingData data = bucket[0].data;
        RecipeData[] catalog = GameDatabase.AllRecipes();
        for (int i = 0; i < catalog.Length; i++)
        {
            RecipeData recipe = catalog[i];
            if (recipe == null || !recipe.AllowsBuilding(data))
                continue;
            if (ResearchSystem.Instance != null && !ResearchSystem.Instance.IsRecipeUnlocked(recipe))
                continue;
            RecipeData captured = recipe;
            Sprite icon = recipe.outputs != null && recipe.outputs.Count > 0 && recipe.outputs[0].item != null
                ? recipe.outputs[0].item.icon
                : null;
            var card = IndustryUi.El("R", "card");
            card.Add(IndustryUi.Icon(icon, "card-icon"));
            var col = IndustryUi.El("C", "col", "grow");
            col.Add(IndustryUi.Text("N", recipe.displayName, "body-text"));
            col.Add(IndustryUi.Text("I", RecipeLine(recipe), "muted"));
            card.Add(col);
            card.Add(IndustryUi.Btn(UiLocale.T("select.choose"), () => ApplyRecipe(captured), "btn-small", "btn-primary"));
            recipeList.Add(card);
        }
    }

    static string RecipeLine(RecipeData recipe)
    {
        if (recipe == null || recipe.inputs == null)
            return "";
        var parts = new List<string>();
        for (int i = 0; i < recipe.inputs.Count; i++)
        {
            if (recipe.inputs[i].item != null)
                parts.Add(recipe.inputs[i].amount + " " + recipe.inputs[i].item.displayName);
        }
        return string.Join(" + ", parts);
    }

    void ApplyRecipe(RecipeData recipe)
    {
        if (recipe == null || recipeBucket == null)
            return;
        for (int i = 0; i < recipeBucket.Count; i++)
        {
            CrafterBuilding crafter = recipeBucket[i] as CrafterBuilding;
            if (crafter != null)
                crafter.SetRecipe(recipe);
        }
        ShowMain();
        Rebuild();
    }
}
