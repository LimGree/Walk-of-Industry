using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    [Header("References")]
    public PlayerInventory inventory;
    public Transform hotbarParent;
    public GameObject slotPrefab;
    public PlayerBuilder playerBuilder;

    [Header("Show / hide")]
    public float slideDistance = 140f;
    public float slideSpeed = 7.5f;
    public float bounce = 1.15f;

    public bool IsBagOpen { get; private set; }

    public const string WindowId = "bag";

    /// <summary>Курсор над окном сумки: колесо не должно листать хотбар.</summary>
    public bool PointerOverBag { get; private set; }

    /// <summary>Сумка открыта — клики мыши принадлежат UI, а не стройке/выделению.</summary>
    public static bool BlocksWorldMouse => Instance != null && Instance.IsBagOpen;

    static readonly string[] BagCategories = { "All", "Logistics", "Production", "Extraction", "Storage", "Research", "Special", "Decor" };

    VisualElement root;
    VisualElement hotbarRoot;
    VisualElement bag;
    VisualElement bagPanel;
    VisualElement bagGrid;
    VisualElement bagTabs;
    Label bagCount;
    string bagFilter = "All";
    VisualElement ghost;
    Image ghostIcon;
    readonly List<VisualElement> slots = new List<VisualElement>();
    readonly List<Image> slotIcons = new List<Image>();
    VisualElement emptySlot;

    BuildingData dragBuilding;
    int dragHotbar = -1;
    bool dragging;
    bool pointerDown;
    Vector2 pressPos;
    float anim;
    bool wantVisible;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();
        if (playerBuilder == null)
            playerBuilder = FindFirstObjectByType<PlayerBuilder>();

        if (inventory != null)
        {
            inventory.OnSelectionChanged += UpdateSelection;
            inventory.OnHotbarChanged += RefreshHotbar;
        }
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged += RefreshHotbar;
        if (playerBuilder != null)
            playerBuilder.OnBuildModeChanged += OnBuildModeChanged;
        GameSettings.Changed += OnSettingsChanged;

        Build();
        hotbarRoot.RegisterCallback<PointerEnterEvent>(_ => { hotbarHovered = true; NoteActivity(); });
        hotbarRoot.RegisterCallback<PointerLeaveEvent>(_ => { hotbarHovered = false; NoteActivity(); });
        NoteActivity();
        IndustryUi.HideLegacy(this, hotbarParent != null ? hotbarParent.gameObject : null);
        IndustryUi.DisableHudCanvas(this);
        RefreshHotbar();

        wantVisible = playerBuilder != null && playerBuilder.isBuildMode;
        anim = wantVisible ? 1f : 0f;
        ApplySlide(anim);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (inventory != null)
        {
            inventory.OnSelectionChanged -= UpdateSelection;
            inventory.OnHotbarChanged -= RefreshHotbar;
        }
        if (playerBuilder != null)
            playerBuilder.OnBuildModeChanged -= OnBuildModeChanged;
        GameSettings.Changed -= OnSettingsChanged;
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged -= RefreshHotbar;
    }

    void Update()
    {
        TickIdleFade();
        float target = wantVisible ? 1f : 0f;
        if (Mathf.Approximately(anim, target))
            return;
        float speed = GameSettings.UiAnimations == 2 ? 1000f : slideSpeed;
        anim = Mathf.MoveTowards(anim, target, Time.unscaledDeltaTime * speed);
        ApplySlide(anim);
    }

    float lastActivity;
    float idleAlpha = 1f;
    bool hotbarHovered;

    /// <summary>Любое действие с хотбаром — хотбар снова виден («Прятать HUD»).</summary>
    public void NoteActivity()
    {
        lastActivity = Time.unscaledTime;
    }

    void TickIdleFade()
    {
        int secs = GameSettings.HideHudSeconds;
        bool idle = secs > 0 && !IsBagOpen && !hotbarHovered && Time.unscaledTime - lastActivity > secs;
        float target = idle ? 0.12f : 1f;
        if (Mathf.Approximately(idleAlpha, target) && !hudOpacityDirty)
            return;
        idleAlpha = Mathf.MoveTowards(idleAlpha, target, Time.unscaledDeltaTime * (idle ? 1.5f : 6f));
        hudOpacityDirty = false;
        ApplySlide(anim);
    }

    bool hudOpacityDirty = true;

    void OnSettingsChanged()
    {
        hudOpacityDirty = true;
        lastLabelIndex = -2;
    }

    void OnBuildModeChanged(bool enabled)
    {
        NoteActivity();
        wantVisible = enabled;
        if (!enabled)
            SetBagOpen(false);
    }

    public void ToggleBag()
    {
        SetBagOpen(!IsBagOpen);
    }

    public void SetBagOpen(bool open)
    {
        if (IsBagOpen == open)
        {
            if (open)
                RefreshBag();
            return;
        }

        IsBagOpen = open;
        if (open)
            UiStack.Opened(WindowId, () => SetBagOpen(false), 55);
        else
            UiStack.Closed(WindowId);
        NoteActivity();
        if (!open)
            PointerOverBag = false;
        if (open)
            RefreshBag();
        IndustryUi.Show(bag, open);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
        else
        {
            UnityEngine.Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            UnityEngine.Cursor.visible = open;
        }
    }

    void Build()
    {
        root = IndustryUi.Mount(this, 55);
        root.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
        root.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);

        hotbarRoot = IndustryUi.El("HotbarRoot", "hotbar-root");
        var bar = IndustryUi.El("Hotbar", "hotbar");
        int size = inventory != null ? inventory.hotbarSize : 9;
        for (int i = 0; i < size; i++)
        {
            VisualElement slot = MakeSlot(i, (i + 1).ToString(), false);
            bar.Add(slot);
            slots.Add(slot);
            slotIcons.Add(slot.Q<Image>());
        }

        emptySlot = MakeSlot(-1, "0", true);
        emptySlot.AddToClassList("is-empty");
        UiTooltip.Bind(emptySlot, UiLocale.T("hotbar.empty_tool"), UiLocale.T("hotbar.empty_tool_body"));
        bar.Add(emptySlot);
        hotbarRoot.Add(bar);
        selectedLabel = IndustryUi.Text("SelectedName", "", "heading-3");
        selectedLabel.pickingMode = PickingMode.Ignore;
        selectedLabel.style.position = Position.Absolute;
        selectedLabel.style.bottom = Length.Percent(100);
        selectedLabel.style.left = 0;
        selectedLabel.style.right = 0;
        selectedLabel.style.marginBottom = 8;
        selectedLabel.style.unityTextAlign = TextAnchor.LowerCenter;
        selectedLabel.style.color = new Color(1f, 1f, 1f, 0.95f);
        selectedLabel.style.textShadow = new TextShadow { offset = new Vector2(1f, 1f), blurRadius = 3f, color = new Color(0f, 0f, 0f, 0.85f) };
        selectedLabel.style.opacity = 0f;
        selectedLabel.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
        selectedLabel.style.transitionDuration = new List<TimeValue> { new TimeValue(0.25f, TimeUnit.Second) };
        hotbarRoot.Add(selectedLabel);
        root.Add(hotbarRoot);

        bag = IndustryUi.El("Bag", "bag");
        bagPanel = IndustryUi.El("BagPanel", "panel", "bag-panel");
        bagPanel.RegisterCallback<PointerEnterEvent>(_ => PointerOverBag = true);
        bagPanel.RegisterCallback<PointerLeaveEvent>(_ => PointerOverBag = false);

        var header = IndustryUi.El("Header", "bag-header");
        header.Add(IndustryUi.Text("T", UiLocale.T("overlay.inventory"), "bag-title"));
        bagCount = IndustryUi.Text("Count", "", "bag-count");
        header.Add(bagCount);
        header.Add(IndustryUi.El("Spacer", "grow"));
        var close = IndustryUi.El("Close", "bag-close");
        close.Add(IndustryUi.Keycap(KeybindStore.Hint("Inventory")));
        close.Add(IndustryUi.Text("L", UiLocale.T("bag.close"), "hint-label"));
        close.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;
            SetBagOpen(false);
            evt.StopPropagation();
        });
        header.Add(close);
        bagPanel.Add(header);

        bagTabs = IndustryUi.El("Tabs", "bag-tabs");
        bagPanel.Add(bagTabs);

        var scroll = IndustryUi.Scroll("BagScroll");
        scroll.AddToClassList("bag-scroll");
        scroll.mode = ScrollViewMode.Vertical;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        bagGrid = IndustryUi.El("Grid", "bag-grid");
        scroll.Add(bagGrid);
        bagPanel.Add(scroll);

        var footer = IndustryUi.El("Footer", "bag-footer");
        AddFooterHint(footer, UiLocale.T("bag.lmb"), UiLocale.T("bag.hint_click"));
        AddFooterHint(footer, UiLocale.T("bag.drag"), UiLocale.T("bag.hint_drag"));
        AddFooterHint(footer, UiLocale.T("bag.rmb"), UiLocale.T("bag.hint_rmb"));
        bagPanel.Add(footer);

        bag.Add(bagPanel);
        IndustryUi.Show(bag, false);
        root.Add(bag);

        ghost = IndustryUi.El("Ghost", "drag-ghost");
        ghost.pickingMode = PickingMode.Ignore;
        ghostIcon = new Image { pickingMode = PickingMode.Ignore };
        ghostIcon.AddToClassList("slot-icon");
        ghost.Add(ghostIcon);
        IndustryUi.Show(ghost, false);
        root.Add(ghost);
    }

    VisualElement MakeSlot(int index, string key, bool empty)
    {
        var slot = IndustryUi.El(empty ? "Empty" : "Slot_" + index, empty ? "slot" : "slot", empty ? "empty-slot" : "hotbar-slot");
        if (!empty)
            slot.AddToClassList("hotbar-slot");
        slot.userData = index;
        slot.Add(IndustryUi.Text("Key", key, "slot-key"));
        slot.Add(IndustryUi.Icon(null, "slot-icon"));
        int captured = index;
        slot.RegisterCallback<PointerDownEvent>(evt => OnSlotDown(evt, captured, empty));
        slot.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        slot.RegisterCallback<PointerUpEvent>(OnPointerUp);
        if (!empty)
        {
            UiTooltip.Bind(slot,
                () =>
                {
                    BuildingData data = inventory != null && inventory.hotbar != null
                        && captured >= 0 && captured < inventory.hotbar.Length
                        ? inventory.hotbar[captured]
                        : null;
                    return data != null ? data.Title : "";
                },
                () =>
                {
                    BuildingData data = inventory != null && inventory.hotbar != null
                        && captured >= 0 && captured < inventory.hotbar.Length
                        ? inventory.hotbar[captured]
                        : null;
                    return data != null ? data.Info : "";
                });
        }

        return slot;
    }

    void ApplySlide(float t)
    {
        if (hotbarRoot == null)
            return;
        float e = wantVisible ? EaseOutBack(t) : 1f - EaseOutBack(1f - t);
        float y = Mathf.Lerp(Mathf.Abs(slideDistance), 0f, e);
        hotbarRoot.style.translate = new Translate(0, y);
        hotbarRoot.style.opacity = wantVisible || t > 0.01f ? GameSettings.HudOpacity * idleAlpha : 0f;
        if (!wantVisible && t <= 0.01f)
            IndustryUi.Show(hotbarRoot, false);
        else
            IndustryUi.Show(hotbarRoot, true);
    }

    float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        if (t <= 0f || t >= 1f)
            return t;
        float overshoot = 1f + bounce;
        return 1f + overshoot * Mathf.Pow(t - 1f, 3f) + bounce * Mathf.Pow(t - 1f, 2f);
    }

    public VisualElement FindBagCard(string buildingId)
    {
        if (bagGrid == null || string.IsNullOrEmpty(buildingId))
            return null;
        return bagGrid.Q("Bag_" + buildingId);
    }

    public VisualElement FirstEmptySlot()
    {
        if (inventory == null || inventory.hotbar == null)
            return null;
        for (int i = 0; i < slots.Count && i < inventory.hotbar.Length; i++)
        {
            if (inventory.hotbar[i] == null)
                return slots[i];
        }
        return slots.Count > 0 ? slots[0] : null;
    }

    public VisualElement FindHotbarBuilding(string buildingId)
    {
        if (inventory == null || inventory.hotbar == null || string.IsNullOrEmpty(buildingId))
            return null;
        for (int i = 0; i < slots.Count && i < inventory.hotbar.Length; i++)
        {
            if (inventory.hotbar[i] != null && TutorialSystem.IdsEqual(inventory.hotbar[i].id, buildingId))
                return slots[i];
        }
        return null;
    }

    public void RefreshHotbar()
    {
        if (inventory == null)
            return;
        for (int i = 0; i < slotIcons.Count; i++)
        {
            BuildingData building = inventory.hotbar != null && i < inventory.hotbar.Length
                ? inventory.hotbar[i]
                : null;
            IndustryUi.SetIcon(slotIcons[i], building != null ? building.icon : null);
        }
        UpdateSelection(inventory.selectedIndex);
        if (IsBagOpen)
            RefreshBag();
    }

    Label selectedLabel;
    IVisualElementScheduledItem hideLabel;
    int lastLabelIndex = -2;

    /// <summary>Над хотбаром на 4 с: «Название · ◈ цена» выбранного здания.</summary>
    void ShowSelectedLabel(int selectedIndex)
    {
        if (selectedLabel == null || inventory == null || selectedIndex == lastLabelIndex)
            return;
        lastLabelIndex = selectedIndex;
        if (Time.timeSinceLevelLoad < 1.5f)
            return;
        if (!GameSettings.HotbarLabel)
        {
            selectedLabel.style.opacity = 0f;
            return;
        }
        BuildingData data = inventory.hotbar != null && selectedIndex >= 0 && selectedIndex < inventory.hotbar.Length
            ? inventory.hotbar[selectedIndex]
            : null;
        if (data == null)
        {
            selectedLabel.style.opacity = 0f;
            return;
        }

        selectedLabel.text = UiLocale.T("hotbar.label", data.Title, IndustryUi.Money(Economy.BuildCost(data)));
        selectedLabel.style.opacity = 1f;
        hideLabel?.Pause();
        hideLabel = selectedLabel.schedule.Execute(() => selectedLabel.style.opacity = 0f).StartingIn(4000);
    }

    /// <summary>Короткая вспышка слота (СКМ «взять здание»).</summary>
    public void FlashSlot(int index)
    {
        if (index < 0 || index >= slots.Count)
            return;
        VisualElement slot = slots[index];
        slot.style.scale = new Scale(new Vector3(1.18f, 1.18f, 1f));
        slot.style.borderTopColor = slot.style.borderBottomColor = slot.style.borderLeftColor = slot.style.borderRightColor = new Color(1f, 0.85f, 0.3f);
        slot.schedule.Execute(() =>
        {
            slot.style.scale = StyleKeyword.Null;
            slot.style.borderTopColor = slot.style.borderBottomColor = slot.style.borderLeftColor = slot.style.borderRightColor = StyleKeyword.Null;
        }).StartingIn(220);
        lastLabelIndex = -2;
        ShowSelectedLabel(index);
    }

    void UpdateSelection(int selectedIndex)
    {
        NoteActivity();
        ShowSelectedLabel(selectedIndex);
        for (int i = 0; i < slots.Count; i++)
        {
            IndustryUi.SetOn(slots[i], i == selectedIndex, "slot-on");
            IndustryUi.SetOn(slots[i], i == selectedIndex, "is-selected");
        }
        bool emptyOn = inventory != null && inventory.IsEmptyToolSelected;
        IndustryUi.SetOn(emptySlot, emptyOn, "slot-on");
        IndustryUi.SetOn(emptySlot, emptyOn, "is-selected");
    }

    void AddFooterHint(VisualElement footer, string key, string label)
    {
        var hint = IndustryUi.El("Hint", "bag-footer-hint");
        hint.Add(IndustryUi.Keycap(key));
        hint.Add(IndustryUi.Text("L", label, "hint-label"));
        footer.Add(hint);
    }

    void RefreshBag()
    {
        if (bagGrid == null || inventory == null)
            return;
        List<BuildingData> unlocked = inventory.GetUnlockedBuildings();

        var counts = new Dictionary<string, int>();
        int buildingsTotal = 0;
        for (int i = 0; i < unlocked.Count; i++)
        {
            string cat = IndustryUi.BuildingCategory(unlocked[i]);
            counts.TryGetValue(cat, out int n);
            counts[cat] = n + 1;
            if (cat != DecorTab)
                buildingsTotal++;
        }
        if (bagFilter != "All" && bagFilter != DecorTab && !counts.ContainsKey(bagFilter))
            bagFilter = "All";

        if (bagCount != null)
            bagCount.text = unlocked.Count.ToString();
        RefreshBagTabs(buildingsTotal, counts);

        bagGrid.Clear();
        for (int i = 0; i < unlocked.Count; i++)
        {
            BuildingData building = unlocked[i];
            string cat = IndustryUi.BuildingCategory(building);
            // «Все» — здания; декорации только на своей вкладке.
            if (bagFilter == "All" ? cat == DecorTab : cat != bagFilter)
                continue;
            bagGrid.Add(MakeBagTile(building, cat));
        }

        if (bagFilter == DecorTab)
            bagGrid.Add(MakeShopTile());
    }

    const string DecorTab = "Decor";

    /// <summary>Последняя плитка вкладки «Декорации»: открыть магазин.</summary>
    VisualElement MakeShopTile()
    {
        var tile = IndustryUi.El("Bag_shop", "bag-tile", "bag-tile-shop", "cat-decor");
        tile.Add(IndustryUi.El("Stripe", "bag-tile-stripe"));
        var well = IndustryUi.El("Well", "bag-tile-well");
        well.Add(IndustryUi.Icon(GameHudIcons.Ruby, "bag-tile-icon"));
        tile.Add(well);
        tile.Add(IndustryUi.Text("T", UiLocale.T("overlay.shop"), "bag-tile-name"));
        tile.Add(IndustryUi.Text("Cost", "+ " + KeybindStore.Hint("Shop"), "bag-tile-cost"));
        foreach (VisualElement child in tile.Query<VisualElement>().ToList())
        {
            if (child != tile)
                child.pickingMode = PickingMode.Ignore;
        }

        UiTooltip.Bind(tile, UiLocale.T("overlay.shop"), DecorText.T("decor.sub"));
        tile.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;
            evt.StopPropagation();
            SetBagOpen(false);
            if (WalletHud.Instance != null)
                WalletHud.Instance.OpenDecorShop();
        });
        return tile;
    }

    void RefreshBagTabs(int total, Dictionary<string, int> counts)
    {
        if (bagTabs == null)
            return;
        bagTabs.Clear();
        for (int i = 0; i < BagCategories.Length; i++)
        {
            string cat = BagCategories[i];
            int n = cat == "All" ? total : (counts.TryGetValue(cat, out int c) ? c : 0);
            if (n <= 0 && cat != "All" && cat != DecorTab)
                continue;
            var chip = IndustryUi.El("Tab_" + cat, "bag-chip", "cat-" + cat.ToLowerInvariant());
            chip.Add(IndustryUi.El("Dot", "bag-chip-dot"));
            chip.Add(IndustryUi.Text("L", UiLocale.T("cat." + cat.ToLowerInvariant()), "bag-chip-label"));
            chip.Add(IndustryUi.Text("N", n.ToString(), "bag-chip-count"));
            IndustryUi.SetOn(chip, cat == bagFilter, "is-selected");
            string captured = cat;
            chip.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;
                if (bagFilter != captured)
                {
                    bagFilter = captured;
                    RefreshBag();
                }
                evt.StopPropagation();
            });
            bagTabs.Add(chip);
        }
    }

    /// <summary>Плитка фиксированного размера: иконка в «колодце», имя в 2 строки, цена, номер слота хотбара.</summary>
    VisualElement MakeBagTile(BuildingData building, string cat)
    {
        int slot = inventory.IndexOf(building);
        bool onBar = slot >= 0;
        string catLabel = UiLocale.T("cat." + cat.ToLowerInvariant());

        var tile = IndustryUi.El("Bag_" + building.id, "bag-tile", "cat-" + cat.ToLowerInvariant());
        if (onBar)
            tile.AddToClassList("is-equipped");
        tile.Add(IndustryUi.El("Stripe", "bag-tile-stripe"));

        var well = IndustryUi.El("Well", "bag-tile-well");
        well.Add(IndustryUi.Icon(building.icon, "bag-tile-icon"));
        tile.Add(well);

        tile.Add(IndustryUi.Text("T", building.Title, "bag-tile-name"));
        int cost = Economy.BuildCost(building);
        tile.Add(IndustryUi.Text("Cost", cost > 0 ? "◈ " + IndustryUi.Money(cost) : catLabel, "bag-tile-cost"));

        if (onBar)
            tile.Add(IndustryUi.Text("Slot", (slot + 1).ToString(), "bag-tile-slot"));

        foreach (VisualElement child in tile.Query<VisualElement>().ToList())
        {
            if (child != tile)
                child.pickingMode = PickingMode.Ignore;
        }

        string tipBody = string.IsNullOrEmpty(building.Info) ? catLabel : building.Info;
        string tipFoot = onBar ? UiLocale.T("hotbar.slot", slot + 1) : (cost > 0 ? UiLocale.T("wallet.coins_n", IndustryUi.Money(cost)) : null);
        UiTooltip.Bind(tile, building.Title, tipBody, tipFoot);

        BuildingData captured = building;
        tile.RegisterCallback<PointerDownEvent>(evt => OnBagDown(evt, captured));
        tile.RegisterCallback<PointerMoveEvent>(OnPointerMove);
        tile.RegisterCallback<PointerUpEvent>(OnPointerUp);
        return tile;
    }

    void OnSlotDown(PointerDownEvent evt, int index, bool empty)
    {
        if (inventory == null)
            return;
        if (evt.button == 1)
        {
            if (!empty && IsBagOpen)
                inventory.UnequipSlot(index);
            evt.StopPropagation();
            return;
        }

        if (evt.button != 0)
            return;

        pointerDown = true;
        pressPos = (Vector2)evt.position;
        dragHotbar = empty ? -1 : index;
        dragBuilding = !empty && inventory.hotbar != null && index >= 0 && index < inventory.hotbar.Length
            ? inventory.hotbar[index]
            : null;
        dragging = false;
        ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    void OnBagDown(PointerDownEvent evt, BuildingData building)
    {
        if (inventory == null || building == null)
            return;
        if (evt.button == 1)
        {
            int have = inventory.IndexOf(building);
            if (have >= 0)
                inventory.UnequipSlot(have);
            evt.StopPropagation();
            return;
        }

        if (evt.button != 0)
            return;
        pointerDown = true;
        pressPos = (Vector2)evt.position;
        dragHotbar = -2;
        dragBuilding = building;
        dragging = false;
        ((VisualElement)evt.currentTarget).CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    void OnPointerMove(PointerMoveEvent evt)
    {
        if (!pointerDown)
            return;
        Vector2 pos = (Vector2)evt.position;
        if (!dragging && (pos - pressPos).sqrMagnitude > 64f && dragBuilding != null && IsBagOpen)
        {
            dragging = true;
            IndustryUi.SetIcon(ghostIcon, dragBuilding.icon);
            IndustryUi.Show(ghost, true);
            GameAudio.Ui("ui_drag_start");
        }

        if (dragging && ghost != null)
        {
            ghost.style.left = pos.x - 24f;
            ghost.style.top = pos.y - 24f;
            int dest = HitHotbar(pos);
            for (int i = 0; i < slots.Count; i++)
                IndustryUi.SetOn(slots[i], i == dest, "is-drop-ok");
        }
    }

    void OnPointerUp(PointerUpEvent evt)
    {
        if (!pointerDown)
            return;
        pointerDown = false;
        Vector2 pos = (Vector2)evt.position;
        VisualElement cap = evt.currentTarget as VisualElement;
        if (cap != null && cap.HasPointerCapture(evt.pointerId))
            cap.ReleasePointer(evt.pointerId);
        else if (evt.target is VisualElement target && target.HasPointerCapture(evt.pointerId))
            target.ReleasePointer(evt.pointerId);
        IndustryUi.Show(ghost, false);
        for (int i = 0; i < slots.Count; i++)
            IndustryUi.SetOn(slots[i], false, "is-drop-ok");

        if (dragging && dragBuilding != null && inventory != null)
        {
            int dest = HitHotbar(pos);
            if (dest >= 0)
            {
                inventory.SwapOrPlace(dragBuilding, dest);
                GameAudio.Ui("ui_drag_drop");
            }
            else if (dragHotbar >= 0 && HitBag(pos))
            {
                inventory.UnequipSlot(dragHotbar);
                GameAudio.Ui("ui_drag_drop");
            }
        }
        else if (!dragging && inventory != null)
        {
            if (dragHotbar == -1)
                inventory.SelectEmptyTool();
            else if (dragHotbar >= 0)
                inventory.SelectSlot(dragHotbar);
            else if (dragHotbar == -2 && dragBuilding != null)
                inventory.EquipToFirstEmpty(dragBuilding);
        }

        dragging = false;
        dragBuilding = null;
        dragHotbar = -1;
        evt.StopPropagation();
    }

    int HitHotbar(Vector2 panelPos)
    {
        if (root == null || root.panel == null)
            return -1;
        VisualElement hit = root.panel.Pick(panelPos);
        while (hit != null)
        {
            if (hit.ClassListContains("hotbar-slot") && hit.userData is int index)
                return index;
            hit = hit.parent;
        }
        return -1;
    }

    bool HitBag(Vector2 panelPos)
    {
        if (root == null || root.panel == null || bag == null)
            return false;
        VisualElement hit = root.panel.Pick(panelPos);
        while (hit != null)
        {
            if (hit == bag || hit.name == "BagPanel")
                return true;
            hit = hit.parent;
        }
        return false;
    }
}
