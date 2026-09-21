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

    VisualElement root;
    VisualElement hotbarRoot;
    VisualElement bag;
    VisualElement bagGrid;
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

        Build();
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
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.OnUnlocksChanged -= RefreshHotbar;
    }

    void Update()
    {
        float target = wantVisible ? 1f : 0f;
        if (Mathf.Approximately(anim, target))
            return;
        anim = Mathf.MoveTowards(anim, target, Time.unscaledDeltaTime * slideSpeed);
        ApplySlide(anim);
    }

    void OnBuildModeChanged(bool enabled)
    {
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
            RefreshBag();
        else
            PlayerPrefs.SetInt("UiBagHintSeen", 1);
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
        UiTooltip.Bind(emptySlot, "Пустой инструмент", "Снять выбор здания");
        bar.Add(emptySlot);
        hotbarRoot.Add(bar);
        root.Add(hotbarRoot);

        bag = IndustryUi.El("Bag", "bag");
        var panel = IndustryUi.El("BagPanel", "panel", "bag-panel");
        panel.Add(IndustryUi.Text("T", UiLocale.T("overlay.inventory"), "bag-title"));
        if (PlayerPrefs.GetInt("UiBagHintSeen", 0) == 0)
            panel.Add(IndustryUi.Text("H", UiLocale.T("bag.hint"), "bag-hint"));
        var scroll = IndustryUi.Scroll("BagScroll");
        bagGrid = IndustryUi.El("Grid", "bag-grid");
        scroll.Add(bagGrid);
        panel.Add(scroll);
        bag.Add(panel);
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
                    return data != null ? data.displayName : "";
                },
                () =>
                {
                    BuildingData data = inventory != null && inventory.hotbar != null
                        && captured >= 0 && captured < inventory.hotbar.Length
                        ? inventory.hotbar[captured]
                        : null;
                    return data != null ? data.description : "";
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
        hotbarRoot.style.opacity = wantVisible || t > 0.01f ? 1f : 0f;
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

    void UpdateSelection(int selectedIndex)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            IndustryUi.SetOn(slots[i], i == selectedIndex, "slot-on");
            IndustryUi.SetOn(slots[i], i == selectedIndex, "is-selected");
        }
        bool emptyOn = inventory != null && inventory.IsEmptyToolSelected;
        IndustryUi.SetOn(emptySlot, emptyOn, "slot-on");
        IndustryUi.SetOn(emptySlot, emptyOn, "is-selected");
    }

    void RefreshBag()
    {
        if (bagGrid == null || inventory == null)
            return;
        bagGrid.Clear();
        List<BuildingData> unlocked = inventory.GetUnlockedBuildings();
        for (int i = 0; i < unlocked.Count; i++)
        {
            BuildingData building = unlocked[i];
            int slot = inventory.IndexOf(building);
            bool onBar = slot >= 0;
            string subtitle = onBar ? "слот " + (slot + 1) : "";
            VisualElement card = IndustryUi.BuildingCard(building, true, subtitle, onBar, null, compact: true);
            BuildingData captured = building;
            card.RegisterCallback<PointerDownEvent>(evt => OnBagDown(evt, captured));
            card.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            card.RegisterCallback<PointerUpEvent>(OnPointerUp);
            bagGrid.Add(card);
        }
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
