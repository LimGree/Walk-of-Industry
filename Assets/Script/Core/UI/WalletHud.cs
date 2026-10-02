using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class WalletHud : MonoBehaviour
{
    public static WalletHud Instance { get; private set; }

    VisualElement shop;
    Label coinsText;
    Label rubiesText;
    Label buildCostText;
    Label shopCoins;
    Label shopRubies;
    Label offer1;
    Label offer5;
    Label offerAll;
    VisualElement toastHost;
    InputAction shopAction;

    bool shopOpen;
    public bool IsShopOpen => shopOpen;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Build();
        BindInput();
        UiNotification.BindHost(toastHost);
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged += Refresh;
        DecorSystem.Changed += OnDecorChanged;
        Refresh();
    }

    void OnEnable()
    {
        BindInput();
    }

    void OnDisable()
    {
        if (shopAction != null)
            shopAction.performed -= OnShopPerformed;
        shopAction = null;
    }

    void OnDestroy()
    {
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnChanged -= Refresh;
        DecorSystem.Changed -= OnDecorChanged;
        UiNotification.UnbindHost(toastHost);
        if (Instance == this)
            Instance = null;
    }

    void BindInput()
    {
        if (shopAction != null)
            return;
        InputSystem_Actions actions = KeybindStore.Shared;
        shopAction = actions != null ? actions.asset.FindAction("Player/Shop", false) : null;
        if (shopAction != null)
            shopAction.performed += OnShopPerformed;
    }

    void Update()
    {
        if (shopAction != null)
            return;
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            HotkeyToggle();
    }

    public const string WindowId = "shop";

    void OnShopPerformed(InputAction.CallbackContext ctx)
    {
        HotkeyToggle();
    }

    void HotkeyToggle()
    {
        UiStack.Hotkey(WindowId, () => SetShopOpen(true), () => SetShopOpen(false), 80);
    }

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 80);
        var stack = IndustryUi.El("HudStack", "hud-stack");
        var chip = IndustryUi.El("Chip", "hud-chip");
        var coinLine = IndustryUi.El("Coins", "hud-line");
        coinLine.Add(IndustryUi.Icon(GameHudIcons.Coin, "resource-chip__icon"));
        coinsText = IndustryUi.Text("C", "0", "gold");
        coinLine.Add(coinsText);
        UiTooltip.Bind(coinLine, UiLocale.T("shop.coins"), UiLocale.T("shop.coins_tip"));
        var rubyLine = IndustryUi.El("Rubies", "hud-line");
        rubyLine.Add(IndustryUi.Icon(GameHudIcons.Ruby, "resource-chip__icon"));
        rubiesText = IndustryUi.Text("R", "0", "ruby");
        rubyLine.Add(rubiesText);
        UiTooltip.Bind(rubyLine, UiLocale.T("shop.rubies"), UiLocale.T("shop.rubies_tip"));
        var top = IndustryUi.El("BalRow", "hud-line");
        top.Add(coinLine);
        top.Add(rubyLine);
        chip.Add(top);
        buildCostText = IndustryUi.Text("BuildCost", "", "hud-cost", "gold");
        IndustryUi.Show(buildCostText, false);
        chip.Add(buildCostText);
        stack.Add(chip);
        toastHost = IndustryUi.El("HudToasts", "hud-toasts");
        toastHost.pickingMode = PickingMode.Ignore;
        stack.Add(toastHost);
        root.Add(stack);
        UiLook.RegisterHud(chip);

        shop = IndustryUi.OverlayPanel(UiLocale.T("overlay.shop"), GameHudIcons.Ruby, () => SetShopOpen(false));
        IndustryUi.Show(shop, false);
        VisualElement shopPanel = IndustryUi.PanelOf(shop);
        shopPanel.AddToClassList("win-medium");
        IndustryUi.WindowHints(shop, (KeybindStore.Hint("Shop"), UiLocale.T("win.close")));
        VisualElement body = shop.Q("Body") ?? shopPanel;

        // Вкладки: декорации ([[DecorShopView]]) и обмен рубинов.
        var tabs = IndustryUi.El("ShopTabs", "shop-tabs");
        var tabRow = IndustryUi.El("Tabs", "tab-row");
        tabDecor = IndustryUi.Btn(DecorText.T("decor.tab.decor"), () => SetTab(true), "tab");
        tabExchange = IndustryUi.Btn(DecorText.T("decor.tab.exchange"), () => SetTab(false), "tab");
        tabRow.Add(tabDecor);
        tabRow.Add(tabExchange);
        tabs.Add(tabRow);
        tabs.Add(IndustryUi.El("Spacer", "grow"));
        var mini = IndustryUi.El("Mini", "shop-mini");
        mini.Add(IndustryUi.Icon(GameHudIcons.Ruby, "shop-mini-icon"));
        miniRubies = IndustryUi.Text("R", "0", "shop-mini-value", "ruby");
        mini.Add(miniRubies);
        mini.Add(IndustryUi.Icon(GameHudIcons.Coin, "shop-mini-icon"));
        miniCoins = IndustryUi.Text("C", "0", "shop-mini-value", "gold");
        mini.Add(miniCoins);
        tabs.Add(mini);
        body.Add(tabs);

        decorView = new DecorShopView();
        body.Add(decorView.Build());

        exchangeView = IndustryUi.El("Exchange", "col");
        body.Add(exchangeView);
        VisualElement panel = exchangeView;

        panel.Add(IndustryUi.Section(UiLocale.T("shop.balance")));
        var balances = IndustryUi.El("Bal", "shop-balances");
        var coinTile = IndustryUi.El("Coins", "shop-stat");
        coinTile.Add(IndustryUi.Icon(GameHudIcons.Coin, "shop-stat-icon"));
        shopCoins = IndustryUi.Text("SC", "0", "shop-stat-value", "gold");
        coinTile.Add(shopCoins);
        coinTile.Add(IndustryUi.Text("L", UiLocale.T("shop.coins"), "shop-stat-label"));
        balances.Add(coinTile);
        var rubyTile = IndustryUi.El("Rubies", "shop-stat");
        rubyTile.Add(IndustryUi.Icon(GameHudIcons.Ruby, "shop-stat-icon"));
        shopRubies = IndustryUi.Text("SR", "0", "shop-stat-value", "ruby");
        rubyTile.Add(shopRubies);
        rubyTile.Add(IndustryUi.Text("L", UiLocale.T("shop.rubies"), "shop-stat-label"));
        balances.Add(rubyTile);
        panel.Add(balances);

        panel.Add(IndustryUi.Section(UiLocale.T("shop.exchange_title")));
        offer1 = AddOffer(panel, () => Exchange(1));
        offer5 = AddOffer(panel, () => Exchange(5));
        offerAll = AddOffer(panel, () =>
        {
            if (PlayerWallet.Instance != null)
                Exchange(PlayerWallet.Instance.Rubies);
        });
        root.Add(shop);
        SetTab(true);
    }

    Button tabDecor;
    Button tabExchange;
    Label miniRubies;
    Label miniCoins;
    DecorShopView decorView;
    VisualElement exchangeView;
    bool decorTab = true;
    bool decorDirty;
    int lastRubies = -1;
    float nextDecorRefresh;

    void SetTab(bool decor)
    {
        decorTab = decor;
        IndustryUi.SetOn(tabDecor, decor, "is-selected");
        IndustryUi.SetOn(tabExchange, !decor, "is-selected");
        if (decorView != null)
            IndustryUi.Show(decorView.Root, decor);
        IndustryUi.Show(exchangeView, !decor);
        IndustryUi.WindowSubtitle(shop, decor
            ? DecorText.T("decor.sub")
            : UiLocale.T("shop.sub", Economy.CoinsPerRuby));
        if (decor && decorView != null)
            decorView.Refresh();
    }

    /// <summary>Открыть магазин сразу на вкладке декораций (плитка «Магазин» в сумке).</summary>
    public void OpenDecorShop()
    {
        if (!shopOpen)
            SetShopOpen(true);
        SetTab(true);
    }

    void OnDecorChanged()
    {
        decorDirty = true;
    }

    static Label AddOffer(VisualElement panel, System.Action onClick)
    {
        var row = IndustryUi.El("Offer", "set-row", "shop-offer");
        row.Add(IndustryUi.Icon(GameHudIcons.Ruby, "icon-32"));
        var label = IndustryUi.Text("L", "", "set-label", "grow");
        row.Add(label);
        row.Add(IndustryUi.Icon(GameHudIcons.Coin, "icon-32"));
        row.Add(IndustryUi.Btn(UiLocale.T("shop.exchange"), onClick, "btn-small", "btn-primary"));
        panel.Add(row);
        return label;
    }

    public void ToggleShop()
    {
        SetShopOpen(!IsShopOpen);
    }

    public void SetShopOpen(bool open)
    {
        shopOpen = open;
        IndustryUi.Show(shop, open);
        if (open)
            UiStack.Opened(WindowId, () => SetShopOpen(false), 80);
        else
            UiStack.Closed(WindowId);
        Refresh();
        if (open)
            decorDirty = true;
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    void Exchange(int rubies)
    {
        if (PlayerWallet.Instance != null && PlayerWallet.Instance.TryExchangeRubies(rubies))
            UiAudio.PlayConfirm();
        else
            UiAudio.PlayError();
        Refresh();
    }

    float nextHudTick;

    void LateUpdate()
    {
        // Сетка декораций перестраивается не чаще двух раз в секунду (монеты меняются постоянно).
        if (decorDirty && shopOpen && decorTab && decorView != null && Time.unscaledTime >= nextDecorRefresh)
        {
            decorDirty = false;
            nextDecorRefresh = Time.unscaledTime + 0.5f;
            decorView.Refresh();
        }

        if (Time.unscaledTime < nextHudTick)
            return;
        nextHudTick = Time.unscaledTime + 0.2f;
        RefreshBuildCost();
    }

    void Refresh()
    {
        PlayerWallet wallet = PlayerWallet.Instance;
        int coins = wallet != null ? wallet.Coins : 0;
        int rubies = wallet != null ? wallet.Rubies : 0;
        if (coinsText != null) coinsText.text = IndustryUi.Money(coins);
        if (rubiesText != null) rubiesText.text = IndustryUi.Money(rubies);
        if (shopCoins != null) shopCoins.text = IndustryUi.Money(coins);
        if (shopRubies != null) shopRubies.text = IndustryUi.Money(rubies);
        if (miniCoins != null) miniCoins.text = IndustryUi.Money(coins);
        if (miniRubies != null) miniRubies.text = IndustryUi.Money(rubies);
        // Сетку декораций трогают только рубины (кнопки «Купить»): монеты меняются постоянно,
        // а перестройка под курсором съедает клик.
        if (rubies != lastRubies)
        {
            lastRubies = rubies;
            decorDirty = true;
        }
        if (offer1 != null) offer1.text = UiLocale.T("shop.offer1", Economy.CoinsPerRuby);
        if (offer5 != null) offer5.text = UiLocale.T("shop.offer5", 5 * Economy.CoinsPerRuby);
        if (offerAll != null)
            offerAll.text = rubies <= 0
                ? UiLocale.T("shop.none")
                : UiLocale.T("shop.all", rubies, rubies * Economy.CoinsPerRuby);
        RefreshBuildCost();
    }

    void RefreshBuildCost()
    {
        if (buildCostText == null)
            return;
        PlayerBuilder builder = GameManager.Instance != null ? GameManager.Instance.playerBuilder : null;
        BuildingData data = builder != null && builder.isBuildMode ? builder.CurrentBuildingData : null;
        int unit = Economy.BuildCost(data);
        if (data == null || unit <= 0)
        {
            IndustryUi.Show(buildCostText, false);
            return;
        }

        string name = data.Title;
        int count = builder.PreviewBuildCount;
        if (builder.IsLineStrokeActive && count > 1)
            buildCostText.text = UiLocale.T("hud.build_line", name, count, IndustryUi.Money(unit * count));
        else
            buildCostText.text = UiLocale.T("hud.build_cost", name, IndustryUi.Money(unit));
        IndustryUi.Show(buildCostText, true);
    }
}
