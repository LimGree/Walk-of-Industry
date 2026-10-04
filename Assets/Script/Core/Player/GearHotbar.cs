using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Хотбар снаряжения (вне режима стройки): крылья, крюк-кошка, ласты, спальник — то, что берут в руки.
/// Выглядит как хотбар стройки ([[InventoryUI]]): 1–9 и колесо мыши — выбор, 0 — пустые руки,
/// название выбранного всплывает над хотбаром на 4 с. I вне стройки — сумка: вкладка «Снаряжение»
/// (клик — в хотбар/убрать) и «Внешний вид» (клик — надеть/снять костюм, шляпу, скины, след, дрона).
/// Использование: крылья — Пробел, крюк и спальник — ЛКМ (крылья/крюк/ласты — в [[PlayerMovement]]).
/// </summary>
public class GearHotbar : MonoBehaviour
{
    public const int Size = 9;
    public const string WindowId = "gear_bag";

    public static GearHotbar Instance { get; private set; }

    public readonly string[] slots = new string[Size];
    public int Selected { get; private set; } = -1;
    public event System.Action Changed;
    public bool BagOpen { get; private set; }

    /// <summary>id снаряжения в руках (null — пустые руки).</summary>
    public string Current => Selected >= 0 && Selected < Size ? slots[Selected] : null;

    public static bool Holding(string id) => Instance != null && Instance.Current == id;

    VisualElement hotbarRoot;
    readonly List<VisualElement> slotEls = new List<VisualElement>();
    VisualElement emptySlot;
    Label selectedLabel;
    IVisualElementScheduledItem hideLabel;

    VisualElement bag;
    VisualElement bagTabs;
    VisualElement bagGrid;
    Label bagCount;
    string bagTab = "gear";
    bool sleeping;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Build();
        if (PerkSystem.Instance != null)
            PerkSystem.Instance.Changed += Refresh;
        UiLocale.Changed += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (PerkSystem.Instance != null)
            PerkSystem.Instance.Changed -= Refresh;
        UiLocale.Changed -= Refresh;
        if (Instance == this)
            Instance = null;
    }

    static bool BuildMode()
    {
        PlayerBuilder b = GameManager.Instance != null ? GameManager.Instance.playerBuilder : null;
        return b != null && b.isBuildMode;
    }

    void Update()
    {
        // В стройке руки заняты зданиями.
        if (BuildMode() && Selected >= 0)
            Select(-1);
        if (BuildMode() && BagOpen)
            SetBagOpen(false);

        bool visible = !BuildMode() && HasAnyGear() && !PhotoMode.IsActive && !LoadingScreen.IsLoading;
        IndustryUi.Show(hotbarRoot, visible);

        if (BuildMode() || UiStack.GameplayBlocked || KeybindStore.BlocksGameplayInput || LoadingScreen.IsLoading)
            return;
        if (BeltRide.Instance != null && BeltRide.Instance.IsRiding)
            return;
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;
        for (int i = 0; i < Size; i++)
        {
            if (kb[Key.Digit1 + i].wasPressedThisFrame)
                Select(Selected == i ? -1 : i);
        }

        if (kb.digit0Key.wasPressedThisFrame)
            Select(-1);

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;
        if (visible)
            Wheel(mouse.scroll.ReadValue().y);
        if (mouse.leftButton.wasPressedThisFrame)
            UseCurrent();
    }

    /// <summary>Колесо листает занятые слоты и «пустые руки» по кругу (зум камеры колесом — с зажатым зумом, не мешаем).</summary>
    void Wheel(float y)
    {
        if (Mathf.Abs(y) < 0.1f)
            return;
        InputAction zoom = KeybindStore.GetAction("Zoom");
        if (zoom != null && zoom.IsPressed())
            return;
        if (GameSettings.InvertHotbarWheel)
            y = -y;
        int dir = y > 0f ? -1 : 1;
        int cur = Selected; // -1 — пустые руки, стоят после 9-го
        for (int step = 0; step < Size + 1; step++)
        {
            cur += dir;
            if (cur >= Size)
                cur = -1;
            else if (cur < -1)
                cur = Size - 1;
            if (cur == -1 || !string.IsNullOrEmpty(slots[cur]))
                break;
        }

        Select(cur);
    }

    // ---------- Снаряжение ----------

    public bool HasAnyGear()
    {
        for (int i = 0; i < PerkSystem.All.Length; i++)
        {
            PerkSystem.Def d = PerkSystem.All[i];
            if (d.kind == PerkSystem.Kind.Gear && PerkSystem.Has(d.id))
                return true;
        }

        return false;
    }

    public void AddOwned(string id)
    {
        if (string.IsNullOrEmpty(id) || System.Array.IndexOf(slots, id) >= 0)
            return;
        for (int i = 0; i < Size; i++)
        {
            if (string.IsNullOrEmpty(slots[i]))
            {
                slots[i] = id;
                Refresh();
                Changed?.Invoke();
                return;
            }
        }
    }

    void RemoveFromBar(string id)
    {
        int i = System.Array.IndexOf(slots, id);
        if (i < 0)
            return;
        slots[i] = null;
        if (Selected == i)
            Selected = -1;
        Refresh();
        Changed?.Invoke();
    }

    public void Select(int index)
    {
        int next = index >= 0 && index < Size && !string.IsNullOrEmpty(slots[index]) ? index : -1;
        if (next == Selected)
            return;
        Selected = next;
        if (next >= 0)
            UiAudio.PlaySelect();
        ShowSelectedLabel();
        Refresh();
        Changed?.Invoke();
    }

    /// <summary>ЛКМ вне стройки: крюк — выстрел, спальник — сон. Крылья — на Пробеле в [[PlayerMovement]].</summary>
    void UseCurrent()
    {
        switch (Current)
        {
            case "grapple":
                PlayerMovement pm = FindFirstObjectByType<PlayerMovement>();
                if (pm != null)
                    pm.FireGrapple();
                break;
            case "sleep":
                TrySleep();
                break;
        }
    }

    // ---------- Спальник ----------

    static bool IsNight => DayNight.Hour >= 20f || DayNight.Hour < 5.5f;

    void TrySleep()
    {
        if (sleeping)
            return;
        if (!IsNight)
        {
            UiNotification.Push(UiLocale.T("gear.sleep_day"), "", UiStatus.Warning);
            UiAudio.PlayError();
            return;
        }

        StartCoroutine(SleepRoutine());
    }

    /// <summary>Ночь проматывается за ~5 с через <see cref="DayNight.Advance"/> — поломки и смена дня идут как обычно.</summary>
    System.Collections.IEnumerator SleepRoutine()
    {
        sleeping = true;
        UiNotification.Push(UiLocale.T("gear.sleep_start"), "", UiStatus.Running);
        float target = 6.5f;
        float left = DayNight.Hour >= target ? 24f - DayNight.Hour + target : target - DayNight.Hour;
        const float duration = 5f;
        float perSecond = left / duration;
        float done = 0f;
        while (done < left)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            {
                yield return null;
                continue;
            }

            float step = Mathf.Min(left - done, perSecond * Time.unscaledDeltaTime);
            DayNight.Advance(step);
            GameSettings.ApplyAtmosphere();
            done += step;
            yield return null;
        }

        sleeping = false;
        UiNotification.Push(UiLocale.T("gear.sleep_done"), "", UiStatus.Completed);
    }

    // ---------- Хотбар (как у стройки) ----------

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 58);
        root.pickingMode = PickingMode.Ignore;

        hotbarRoot = IndustryUi.El("GearHotbarRoot", "hotbar-root");
        hotbarRoot.pickingMode = PickingMode.Ignore;
        var bar = IndustryUi.El("GearHotbar", "hotbar");
        for (int i = 0; i < Size; i++)
        {
            var slot = IndustryUi.El("Gear_" + i, "slot", "hotbar-slot");
            slot.Add(IndustryUi.Text("Key", (i + 1).ToString(), "slot-key"));
            slot.Add(IndustryUi.Icon(null, "slot-icon"));
            slotEls.Add(slot);
            bar.Add(slot);
        }

        emptySlot = IndustryUi.El("GearEmpty", "slot", "empty-slot", "is-empty");
        emptySlot.Add(IndustryUi.Text("Key", "0", "slot-key"));
        emptySlot.Add(IndustryUi.Icon(null, "slot-icon"));
        bar.Add(emptySlot);
        hotbarRoot.Add(bar);

        selectedLabel = IndustryUi.Text("GearSelectedName", "", "heading-3");
        selectedLabel.pickingMode = PickingMode.Ignore;
        selectedLabel.style.position = Position.Absolute;
        selectedLabel.style.bottom = Length.Percent(100);
        selectedLabel.style.left = 0;
        selectedLabel.style.right = 0;
        selectedLabel.style.marginBottom = 8;
        selectedLabel.style.unityTextAlign = TextAnchor.LowerCenter;
        selectedLabel.style.whiteSpace = WhiteSpace.Normal;
        selectedLabel.style.color = new Color(1f, 1f, 1f, 0.95f);
        selectedLabel.style.textShadow = new TextShadow { offset = new Vector2(1f, 1f), blurRadius = 3f, color = new Color(0f, 0f, 0f, 0.85f) };
        selectedLabel.style.opacity = 0f;
        selectedLabel.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("opacity") };
        selectedLabel.style.transitionDuration = new List<TimeValue> { new TimeValue(0.25f, TimeUnit.Second) };
        hotbarRoot.Add(selectedLabel);
        root.Add(hotbarRoot);

        BuildBag(root);
    }

    /// <summary>Над хотбаром на 4 с: «Название — как пользоваться».</summary>
    void ShowSelectedLabel()
    {
        if (selectedLabel == null)
            return;
        PerkSystem.Def d = Current != null ? PerkSystem.Find(Current) : null;
        if (d == null)
        {
            selectedLabel.style.opacity = 0f;
            return;
        }

        string how = HintFor(d.id);
        selectedLabel.text = string.IsNullOrEmpty(how) ? d.Title : d.Title + "\n<size=13>" + how + "</size>";
        selectedLabel.style.opacity = 1f;
        hideLabel?.Pause();
        hideLabel = selectedLabel.schedule.Execute(() => selectedLabel.style.opacity = 0f).StartingIn(4000);
    }

    // ---------- Сумка (как у стройки) ----------

    void BuildBag(VisualElement root)
    {
        bag = IndustryUi.El("GearBag", "bag");
        var panel = IndustryUi.El("GearBagPanel", "panel", "bag-panel");
        var header = IndustryUi.El("Header", "bag-header");
        header.Add(IndustryUi.Text("T", UiLocale.T("gear.bag"), "bag-title"));
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
        panel.Add(header);

        bagTabs = IndustryUi.El("Tabs", "bag-tabs");
        panel.Add(bagTabs);

        var scroll = IndustryUi.Scroll("GearBagScroll");
        scroll.AddToClassList("bag-scroll");
        scroll.mode = ScrollViewMode.Vertical;
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        bagGrid = IndustryUi.El("Grid", "bag-grid");
        scroll.Add(bagGrid);
        panel.Add(scroll);

        var footer = IndustryUi.El("Footer", "bag-footer");
        AddFooterHint(footer, UiLocale.T("bag.lmb"), UiLocale.T("gear.bag_click"));
        AddFooterHint(footer, "1–9", UiLocale.T("gear.bag_keys"));
        panel.Add(footer);

        bag.Add(panel);
        IndustryUi.Show(bag, false);
        root.Add(bag);
    }

    static void AddFooterHint(VisualElement footer, string key, string label)
    {
        var hint = IndustryUi.El("Hint", "bag-footer-hint");
        hint.Add(IndustryUi.Keycap(key));
        hint.Add(IndustryUi.Text("L", label, "hint-label"));
        footer.Add(hint);
    }

    public void ToggleBag()
    {
        UiStack.Hotkey(WindowId, () => SetBagOpen(true), () => SetBagOpen(false), 57);
    }

    public void SetBagOpen(bool open)
    {
        if (open && (GameManager.Instance != null && GameManager.Instance.IsPaused || LoadingScreen.IsLoading))
            return;
        if (BagOpen == open)
            return;
        BagOpen = open;
        IndustryUi.Show(bag, open);
        if (open)
        {
            UiStack.Opened(WindowId, () => SetBagOpen(false), 57);
            Refresh();
        }
        else
            UiStack.Closed(WindowId);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    void Refresh()
    {
        // Чего нет — не держим в слотах.
        for (int i = 0; i < Size; i++)
        {
            if (!string.IsNullOrEmpty(slots[i]) && !PerkSystem.Has(slots[i]))
                slots[i] = null;
        }

        for (int i = 0; i < slotEls.Count; i++)
        {
            string id = slots[i];
            IndustryUi.SetIcon(slotEls[i].Q<Image>(), string.IsNullOrEmpty(id) ? null : PerkShopView.Icon(id));
            IndustryUi.SetOn(slotEls[i], i == Selected, "slot-on");
            IndustryUi.SetOn(slotEls[i], i == Selected, "is-selected");
        }

        if (emptySlot != null)
        {
            IndustryUi.SetOn(emptySlot, Selected < 0, "slot-on");
            IndustryUi.SetOn(emptySlot, Selected < 0, "is-selected");
        }

        if (BagOpen)
            RefreshBag();
    }

    void RefreshBag()
    {
        if (bagGrid == null)
            return;
        int gear = 0, style = 0;
        for (int i = 0; i < PerkSystem.All.Length; i++)
        {
            PerkSystem.Def d = PerkSystem.All[i];
            if (!PerkSystem.Has(d.id))
                continue;
            if (d.kind == PerkSystem.Kind.Gear)
                gear++;
            else if (d.kind == PerkSystem.Kind.Cosmetic && d.id != "horn")
                style++;
        }

        bagCount.text = (gear + style).ToString();
        bagTabs.Clear();
        AddTab("gear", UiLocale.T("gear.tab_gear"), gear);
        AddTab("style", UiLocale.T("gear.tab_style"), style);

        bagGrid.Clear();
        for (int i = 0; i < PerkSystem.All.Length; i++)
        {
            PerkSystem.Def d = PerkSystem.All[i];
            if (!PerkSystem.Has(d.id))
                continue;
            if (bagTab == "gear" && d.kind == PerkSystem.Kind.Gear)
                bagGrid.Add(GearTile(d));
            else if (bagTab == "style" && d.kind == PerkSystem.Kind.Cosmetic && d.id != "horn")
                bagGrid.Add(StyleTile(d));
        }

        bagGrid.Add(ShopTile());
    }

    void AddTab(string id, string label, int n)
    {
        var chip = IndustryUi.El("Tab_" + id, "bag-chip", "cat-decor");
        chip.Add(IndustryUi.El("Dot", "bag-chip-dot"));
        chip.Add(IndustryUi.Text("L", label, "bag-chip-label"));
        chip.Add(IndustryUi.Text("N", n.ToString(), "bag-chip-count"));
        IndustryUi.SetOn(chip, id == bagTab, "is-selected");
        chip.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;
            bagTab = id;
            UiAudio.PlayToggle();
            RefreshBag();
            evt.StopPropagation();
        });
        bagTabs.Add(chip);
    }

    VisualElement Tile(PerkSystem.Def d, bool on, string sub, string slotBadge)
    {
        var tile = IndustryUi.El("Gear_" + d.id, "bag-tile", "cat-decor");
        if (on)
            tile.AddToClassList("is-equipped");
        tile.Add(IndustryUi.El("Stripe", "bag-tile-stripe"));
        var well = IndustryUi.El("Well", "bag-tile-well");
        well.Add(IndustryUi.Icon(PerkShopView.Icon(d.id), "bag-tile-icon"));
        tile.Add(well);
        tile.Add(IndustryUi.Text("T", d.Title, "bag-tile-name"));
        tile.Add(IndustryUi.Text("Cost", sub, "bag-tile-cost"));
        if (!string.IsNullOrEmpty(slotBadge))
            tile.Add(IndustryUi.Text("Slot", slotBadge, "bag-tile-slot"));
        foreach (VisualElement child in tile.Query<VisualElement>().ToList())
        {
            if (child != tile)
                child.pickingMode = PickingMode.Ignore;
        }

        UiTooltip.Bind(tile, d.Title, d.Info);
        return tile;
    }

    VisualElement GearTile(PerkSystem.Def d)
    {
        int slot = System.Array.IndexOf(slots, d.id);
        bool onBar = slot >= 0;
        VisualElement tile = Tile(d, onBar, onBar ? UiLocale.T("gear.on_bar") : UiLocale.T("gear.to_bar"), onBar ? (slot + 1).ToString() : null);
        string id = d.id;
        tile.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;
            if (System.Array.IndexOf(slots, id) >= 0)
                RemoveFromBar(id);
            else
                AddOwned(id);
            UiAudio.PlayToggle();
            RefreshBag();
            evt.StopPropagation();
        });
        return tile;
    }

    VisualElement StyleTile(PerkSystem.Def d)
    {
        bool on = d.id == "pet" ? PerkSystem.PetOn : PerkSystem.IsWorn(d.id);
        VisualElement tile = Tile(d, on, on ? UiLocale.T("gear.worn") : UiLocale.T("perkshop.wear"), null);
        string id = d.id;
        tile.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;
            if (id == "pet")
                PerkSystem.Instance?.TogglePet();
            else
                PerkSystem.Instance?.Wear(id);
            UiAudio.PlayToggle();
            evt.StopPropagation();
        });
        return tile;
    }

    VisualElement ShopTile()
    {
        var tile = IndustryUi.El("Gear_shop", "bag-tile", "bag-tile-shop", "cat-decor");
        tile.Add(IndustryUi.El("Stripe", "bag-tile-stripe"));
        var well = IndustryUi.El("Well", "bag-tile-well");
        well.Add(IndustryUi.Icon(GameHudIcons.Ruby, "bag-tile-icon"));
        tile.Add(well);
        tile.Add(IndustryUi.Text("T", UiLocale.T("perkshop.tab"), "bag-tile-name"));
        tile.Add(IndustryUi.Text("Cost", "+ " + KeybindStore.Hint("Shop"), "bag-tile-cost"));
        foreach (VisualElement child in tile.Query<VisualElement>().ToList())
        {
            if (child != tile)
                child.pickingMode = PickingMode.Ignore;
        }

        tile.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;
            SetBagOpen(false);
            WalletHud.Instance?.OpenGearShop();
            evt.StopPropagation();
        });
        return tile;
    }

    static string HintFor(string id)
    {
        switch (id)
        {
            case "wings": return UiLocale.T("gear.hint_wings", KeybindStore.Hint("Jump"));
            case "grapple": return UiLocale.T("gear.hint_grapple");
            case "sleep": return UiLocale.T("gear.hint_sleep");
            case "flippers": return UiLocale.T("gear.hint_flippers");
            default: return "";
        }
    }

    // ---------- Сейв ----------

    public void CaptureSave(SaveData data)
    {
        if (data == null)
            return;
        data.gearHotbar = new List<string>(slots);
    }

    public void ApplySave(SaveData data)
    {
        for (int i = 0; i < Size; i++)
            slots[i] = null;
        Selected = -1;
        if (data != null && data.gearHotbar != null)
        {
            for (int i = 0; i < Size && i < data.gearHotbar.Count; i++)
                slots[i] = string.IsNullOrEmpty(data.gearHotbar[i]) ? null : data.gearHotbar[i];
        }

        // Старые сейвы: ласты были пассивными — положить в хотбар.
        if (PerkSystem.Has("flippers"))
            AddOwned("flippers");
        Refresh();
        Changed?.Invoke();
    }
}
