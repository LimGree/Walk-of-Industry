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
            ToggleShop();
    }

    void OnShopPerformed(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        ToggleShop();
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

        shop = IndustryUi.OverlayPanel(UiLocale.T("overlay.shop"), GameHudIcons.Ruby, () => SetShopOpen(false));
        IndustryUi.Show(shop, false);
        VisualElement panel = IndustryUi.PanelOf(shop);
        var balances = IndustryUi.El("Bal", "card");
        balances.Add(IndustryUi.Icon(GameHudIcons.Coin, "icon-48"));
        shopCoins = IndustryUi.Text("SC", "0 монет", "body-text", "grow");
        balances.Add(shopCoins);
        balances.Add(IndustryUi.Icon(GameHudIcons.Ruby, "icon-48"));
        shopRubies = IndustryUi.Text("SR", "0 рубинов", "body-text");
        balances.Add(shopRubies);
        panel.Add(balances);

        var rate = IndustryUi.El("Rate", "card");
        rate.Add(IndustryUi.Icon(GameHudIcons.Ruby, "icon-32"));
        rate.Add(IndustryUi.Text("One", "1", "gold"));
        rate.Add(IndustryUi.Text("Arr", "  →  ", "title"));
        rate.Add(IndustryUi.Icon(GameHudIcons.Coin, "icon-32"));
        rate.Add(IndustryUi.Text("Val", Economy.CoinsPerRuby.ToString(), "gold"));
        panel.Add(rate);

        offer1 = AddOffer(panel, () => Exchange(1));
        offer5 = AddOffer(panel, () => Exchange(5));
        offerAll = AddOffer(panel, () =>
        {
            if (PlayerWallet.Instance != null)
                Exchange(PlayerWallet.Instance.Rubies);
        });
        root.Add(shop);
    }

    static Label AddOffer(VisualElement panel, System.Action onClick)
    {
        var card = IndustryUi.El("Offer", "card");
        card.Add(IndustryUi.Icon(GameHudIcons.Ruby, "icon-32"));
        var label = IndustryUi.Text("L", "", "body-text", "grow");
        card.Add(label);
        card.Add(IndustryUi.Icon(GameHudIcons.Coin, "icon-32"));
        card.Add(IndustryUi.Btn(UiLocale.T("shop.exchange"), onClick, "btn-small", "btn-primary"));
        panel.Add(card);
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
        if (open && SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
            SelectionActionsUI.Instance.SetOpen(false);
        Refresh();
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

        string name = data.displayName;
        int count = builder.PreviewBuildCount;
        if (builder.IsLineStrokeActive && count > 1)
            buildCostText.text = UiLocale.T("hud.build_line", name, count, IndustryUi.Money(unit * count));
        else
            buildCostText.text = UiLocale.T("hud.build_cost", name, IndustryUi.Money(unit));
        IndustryUi.Show(buildCostText, true);
    }
}
