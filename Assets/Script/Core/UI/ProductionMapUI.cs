using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Карта производства (K): что из чего делается, на каком станке, где добывать, как подключить.
/// Слева — граф по ступеням (сырьё → … → ИИ-модуль), у выбранного предмета подсвечены ингредиенты и потребители.
/// Справа — «Как сделать» (цепочка до руды с числом станков на выбранную скорость), «Как подключить»
/// (схема входов/выходов станка), «Где используется». Данные — [[ProductionGraph]].
/// </summary>
public class ProductionMapUI : MonoBehaviour
{
    public const string WindowId = "production_map";
    const int SortOrder = 112;
    const string PrefHideLocked = "pm.hideLocked";
    const string PrefRate = "pm.rate";
    static readonly int[] Rates = { 10, 30, 60, 120 };

    public static ProductionMapUI Instance { get; private set; }
    public bool IsOpen { get; private set; }

    VisualElement overlay;
    VisualElement graph;
    VisualElement edges;
    VisualElement detail;
    TextField search;
    Button hideLockedBtn;
    readonly List<Button> rateBtns = new List<Button>();
    readonly Dictionary<ItemData, VisualElement> tiles = new Dictionary<ItemData, VisualElement>();
    InputAction mapAction;

    ItemData selected;
    bool hideLocked;
    int rate = 30;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        hideLocked = PlayerPrefs.GetInt(PrefHideLocked, 0) == 1;
        rate = PlayerPrefs.GetInt(PrefRate, 30);
        Build();
        SetOpen(false);
        InputSystem_Actions actions = KeybindStore.Shared;
        mapAction = actions != null ? actions.asset.FindAction("Player/ProductionMap", false) : null;
        if (mapAction != null)
            mapAction.performed += OnHotkey;
    }

    void OnDestroy()
    {
        if (mapAction != null)
            mapAction.performed -= OnHotkey;
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (mapAction != null || KeybindStore.BlocksGameplayInput)
            return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.kKey.wasPressedThisFrame)
            Hotkey();
    }

    void OnHotkey(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput && !IsOpen)
            return;
        Hotkey();
    }

    void Hotkey()
    {
        UiStack.Hotkey(WindowId, () =>
        {
            if (!IsOpen)
                Open(selected);
        }, () => SetOpen(false), SortOrder);
    }

    /// <summary>Открыть карту на предмете (null — на последнем/первом открытом).</summary>
    public void Open(ItemData item)
    {
        if (LoadingScreen.IsLoading)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (PhotoMode.IsActive)
            return;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            MachineUI.Instance.Close();
        if (item != null)
            selected = item;
        SetOpen(true);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        IndustryUi.Show(overlay, open);
        if (open)
        {
            UiStack.Opened(WindowId, () => SetOpen(false), SortOrder);
            Rebuild();
        }
        else
            UiStack.Closed(WindowId);

        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    // ---------- Построение окна ----------

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, SortOrder);
        overlay = IndustryUi.OverlayPanel(UiLocale.T("pm.title"), null, () => SetOpen(false));
        overlay.AddToClassList("pm-window");
        VisualElement body = overlay.Q("Body") ?? IndustryUi.PanelOf(overlay);
        IndustryUi.WindowHints(overlay,
            (UiLocale.T("bag.lmb"), UiLocale.T("pm.hint_pick")),
            (KeybindStore.Hint("ProductionMap"), UiLocale.T("win.close")));

        var toolbar = IndustryUi.El("Toolbar", "win-toolbar");
        search = new TextField { name = "PmSearch" };
        search.AddToClassList("pm-search");
        search.RegisterValueChangedCallback(_ => ApplyFilter());
        toolbar.Add(search);
        hideLockedBtn = IndustryUi.Btn(UiLocale.T("pm.hide_locked"), ToggleHideLocked, "tab");
        toolbar.Add(hideLockedBtn);
        toolbar.Add(IndustryUi.El("Spacer", "grow"));
        toolbar.Add(IndustryUi.Text("RateCap", UiLocale.T("pm.rate"), "caption"));
        var rates = IndustryUi.El("Rates", "tab-row");
        for (int i = 0; i < Rates.Length; i++)
        {
            int r = Rates[i];
            Button b = IndustryUi.Btn(UiLocale.T("pm.per_min", r), () => SetRate(r), "tab");
            rateBtns.Add(b);
            rates.Add(b);
        }

        toolbar.Add(rates);
        body.Add(toolbar);

        var split = IndustryUi.El("Split", "row", "grow", "pm-split");
        var graphScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        graphScroll.AddToClassList("scroll");
        graphScroll.AddToClassList("grow");
        graphScroll.AddToClassList("pm-graph-scroll");
        var content = IndustryUi.El("GraphContent", "pm-graph-content");
        graph = IndustryUi.El("Graph", "row", "pm-graph");
        edges = IndustryUi.El("Edges", "pm-edges");
        edges.pickingMode = PickingMode.Ignore;
        edges.generateVisualContent += PaintEdges;
        content.Add(graph);
        content.Add(edges);
        content.RegisterCallback<GeometryChangedEvent>(_ => edges.MarkDirtyRepaint());
        graphScroll.Add(content);
        split.Add(graphScroll);

        var detailScroll = new ScrollView();
        detailScroll.AddToClassList("scroll");
        detailScroll.AddToClassList("pm-detail-scroll");
        detail = IndustryUi.El("Detail", "col", "pm-detail");
        detailScroll.Add(detail);
        split.Add(detailScroll);
        body.Add(split);

        overlay.pickingMode = PickingMode.Position;
        root.Add(overlay);
    }

    void ToggleHideLocked()
    {
        hideLocked = !hideLocked;
        PlayerPrefs.SetInt(PrefHideLocked, hideLocked ? 1 : 0);
        ApplyFilter();
    }

    void SetRate(int r)
    {
        rate = r;
        PlayerPrefs.SetInt(PrefRate, r);
        UiAudio.PlayTab();
        SyncToolbar();
        FillDetail();
    }

    void SyncToolbar()
    {
        IndustryUi.SetOn(hideLockedBtn, hideLocked, "tab-on");
        for (int i = 0; i < rateBtns.Count; i++)
            IndustryUi.SetOn(rateBtns[i], Rates[i] == rate, "tab-on");
    }

    void Rebuild()
    {
        ProductionGraph.Invalidate();
        if (selected == null)
            selected = DefaultSelection();
        BuildGraph();
        SyncToolbar();
        ApplyFilter();
        FillDetail();
    }

    static ItemData DefaultSelection()
    {
        // Первый открытый, но ещё не сделанный предмет — «что дальше».
        IReadOnlyList<ProductionGraph.Node> all = ProductionGraph.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (!all[i].IsRaw && ProductionGraph.StateOf(all[i]) == ProductionGraph.State.Open)
                return all[i].item;
        }

        return all.Count > 0 ? all[0].item : null;
    }

    void BuildGraph()
    {
        graph.Clear();
        tiles.Clear();
        IReadOnlyList<ProductionGraph.Node> all = ProductionGraph.All;
        int max = ProductionGraph.MaxDepth;
        var columns = new VisualElement[max + 1];
        for (int d = 0; d <= max; d++)
        {
            var col = IndustryUi.El("Col" + d, "col", "pm-col");
            col.Add(IndustryUi.Text("Head", d == 0 ? UiLocale.T("pm.raw") : UiLocale.T("pm.tier", d), "label-caps", "pm-col-head"));
            columns[d] = col;
            graph.Add(col);
        }

        for (int i = 0; i < all.Count; i++)
        {
            ProductionGraph.Node n = all[i];
            if (n.IsRaw && n.extractor == null && n.consumers.Count == 0)
                continue;
            VisualElement tile = Tile(n);
            tiles[n.item] = tile;
            columns[Mathf.Clamp(n.depth, 0, max)].Add(tile);
        }
    }

    VisualElement Tile(ProductionGraph.Node n)
    {
        ItemData item = n.item;
        var tile = IndustryUi.El("Pm_" + GameDatabase.Normalize(item.id), "row", "pm-tile");
        tile.Add(IndustryUi.Icon(item.icon, "pm-tile-icon"));
        var text = IndustryUi.El("Txt", "col", "grow");
        text.Add(IndustryUi.Text("Name", item.Title, "pm-tile-name"));
        BuildingData where = n.IsRaw ? n.extractor : RecipeCodex.BuildingOf(n.MainRecipe);
        if (where != null)
            text.Add(IndustryUi.Text("Where", where.Title, "pm-tile-where"));
        tile.Add(text);
        tile.Add(IndustryUi.Text("Lock", "🔒", "pm-tile-lock"));
        tile.pickingMode = PickingMode.Position;
        tile.RegisterCallback<ClickEvent>(_ => Select(item));
        UiTooltip.Bind(tile, item.Title, item.Info);
        return tile;
    }

    void Select(ItemData item)
    {
        if (item == null)
            return;
        selected = item;
        UiAudio.PlayTab();
        ApplyFilter();
        FillDetail();
    }

    /// <summary>Классы плиток: состояние, выбор, ингредиенты/потребители выбранного, поиск.</summary>
    void ApplyFilter()
    {
        SyncToolbar();
        string query = search != null ? search.value : "";
        ProductionGraph.Node sel = ProductionGraph.Get(selected);
        var inputs = new HashSet<ItemData>();
        var outputs = new HashSet<ItemData>();
        if (sel != null)
        {
            RecipeData main = sel.MainRecipe;
            for (int i = 0; main != null && main.inputs != null && i < main.inputs.Count; i++)
                inputs.Add(main.inputs[i].item);
            for (int c = 0; c < sel.consumers.Count; c++)
            {
                RecipeData r = sel.consumers[c];
                for (int o = 0; r.outputs != null && o < r.outputs.Count; o++)
                    outputs.Add(r.outputs[o].item);
            }
        }

        foreach (KeyValuePair<ItemData, VisualElement> pair in tiles)
        {
            ProductionGraph.Node n = ProductionGraph.Get(pair.Key);
            ProductionGraph.State state = ProductionGraph.StateOf(n);
            VisualElement t = pair.Value;
            t.EnableInClassList("pm-locked", state == ProductionGraph.State.Locked);
            t.EnableInClassList("pm-made", state == ProductionGraph.State.Made);
            t.EnableInClassList("pm-selected", pair.Key == selected);
            t.EnableInClassList("pm-input", inputs.Contains(pair.Key));
            t.EnableInClassList("pm-output", outputs.Contains(pair.Key));
            bool match = string.IsNullOrEmpty(query) || Contains(pair.Key.Title, query) || Contains(pair.Key.id, query);
            bool visible = match && !(hideLocked && state == ProductionGraph.State.Locked);
            IndustryUi.Show(t, visible);
        }

        edges?.MarkDirtyRepaint();
    }

    static bool Contains(string text, string query)
    {
        return !string.IsNullOrEmpty(text) && text.IndexOf(query.Trim(), System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // ---------- Стрелки графа (только для выбранного) ----------

    void PaintEdges(MeshGenerationContext ctx)
    {
        ProductionGraph.Node sel = ProductionGraph.Get(selected);
        if (sel == null || !tiles.TryGetValue(sel.item, out VisualElement target) || target.resolvedStyle.display == DisplayStyle.None)
            return;
        Painter2D p = ctx.painter2D;
        p.lineWidth = 2f;

        RecipeData main = sel.MainRecipe;
        for (int i = 0; main != null && main.inputs != null && i < main.inputs.Count; i++)
        {
            if (tiles.TryGetValue(main.inputs[i].item, out VisualElement from))
                Arrow(p, from, target, new Color(0.35f, 0.85f, 1f, 0.9f));
        }

        for (int c = 0; c < sel.consumers.Count; c++)
        {
            RecipeData r = sel.consumers[c];
            for (int o = 0; r.outputs != null && o < r.outputs.Count; o++)
            {
                if (tiles.TryGetValue(r.outputs[o].item, out VisualElement to))
                    Arrow(p, target, to, new Color(1f, 0.75f, 0.3f, 0.75f));
            }
        }
    }

    void Arrow(Painter2D p, VisualElement from, VisualElement to, Color color)
    {
        if (from.resolvedStyle.display == DisplayStyle.None || to.resolvedStyle.display == DisplayStyle.None)
            return;
        Rect a = from.ChangeCoordinatesTo(edges, from.contentRect);
        Rect b = to.ChangeCoordinatesTo(edges, to.contentRect);
        bool forward = b.center.x >= a.center.x;
        Vector2 start = forward ? new Vector2(a.xMax + 4f, a.center.y) : new Vector2(a.xMin - 4f, a.center.y);
        Vector2 end = forward ? new Vector2(b.xMin - 6f, b.center.y) : new Vector2(b.xMax + 6f, b.center.y);
        float mid = (start.x + end.x) * 0.5f;
        p.strokeColor = color;
        p.BeginPath();
        p.MoveTo(start);
        p.BezierCurveTo(new Vector2(mid, start.y), new Vector2(mid, end.y), end);
        p.Stroke();
        float dir = forward ? -1f : 1f;
        p.fillColor = color;
        p.BeginPath();
        p.MoveTo(end);
        p.LineTo(end + new Vector2(dir * 7f, -4f));
        p.LineTo(end + new Vector2(dir * 7f, 4f));
        p.ClosePath();
        p.Fill();
    }

    // ---------- Правая панель ----------

    void FillDetail()
    {
        detail.Clear();
        ProductionGraph.Node n = ProductionGraph.Get(selected);
        if (n == null)
        {
            detail.Add(IndustryUi.Empty(UiLocale.T("pm.empty")));
            return;
        }

        var head = IndustryUi.El("Head", "row", "pm-head");
        head.Add(IndustryUi.Well(n.item.icon, "card-well-lg"));
        var meta = IndustryUi.El("Meta", "col", "grow");
        meta.Add(IndustryUi.Text("T", n.item.Title, "heading-3"));
        ProductionGraph.State state = ProductionGraph.StateOf(n);
        string badge = state == ProductionGraph.State.Made ? UiLocale.T("pm.state_made")
            : state == ProductionGraph.State.Open ? UiLocale.T("pm.state_open") : UiLocale.T("pm.state_locked");
        meta.Add(IndustryUi.Text("State", badge, "badge", state == ProductionGraph.State.Locked ? "badge-locked" : "badge-ready"));
        if (!string.IsNullOrEmpty(n.item.Info))
            meta.Add(IndustryUi.Text("D", n.item.Info, "muted"));
        head.Add(meta);
        detail.Add(head);

        var actions = IndustryUi.El("Actions", "row", "pm-actions");
        bool tracked = GoalSystem.Instance != null && GoalSystem.Instance.TrackedItem == n.item;
        actions.Add(IndustryUi.Btn(tracked ? UiLocale.T("pm.untrack") : UiLocale.T("pm.track"), () => ToggleTrack(n.item), "btn-small", tracked ? "btn-ghost" : "btn-primary"));
        ResearchNodeData blocker = ProductionGraph.Blocker(n);
        if (blocker != null)
            actions.Add(IndustryUi.Btn(UiLocale.T("pm.open_tree"), () => OpenTree(blocker), "btn-small"));
        detail.Add(actions);

        if (blocker != null)
            detail.Add(IndustryUi.Text("Blocker", UiLocale.T("pm.needs_research", blocker.Title), "pm-warn"));

        // Как сделать
        detail.Add(IndustryUi.Text("HowCap", UiLocale.T("pm.how", rate), "label-caps", "pm-section"));
        var tree = IndustryUi.El("Tree", "col", "pm-tree");
        var seen = new HashSet<ItemData>();
        AddChain(tree, n, rate, 0, seen);
        detail.Add(tree);

        // Как подключить
        BuildingData machine = n.IsRaw ? n.extractor : RecipeCodex.BuildingOf(n.MainRecipe);
        if (machine != null)
        {
            detail.Add(IndustryUi.Text("WireCap", UiLocale.T("pm.connect", machine.Title), "label-caps", "pm-section"));
            detail.Add(PortDiagram(machine, n));
        }

        // Где используется
        if (n.consumers.Count > 0 || n.researchUses.Count > 0)
        {
            detail.Add(IndustryUi.Text("UseCap", UiLocale.T("pm.used_in"), "label-caps", "pm-section"));
            var uses = IndustryUi.El("Uses", "row", "pm-chips");
            for (int i = 0; i < n.consumers.Count; i++)
            {
                RecipeData r = n.consumers[i];
                ItemData product = IndustryUi.FirstItem(r.outputs);
                if (product != null)
                    uses.Add(ItemChip(product));
            }

            detail.Add(uses);
            if (n.researchUses.Count > 0)
            {
                var res = IndustryUi.El("Res", "row", "pm-chips");
                for (int i = 0; i < n.researchUses.Count; i++)
                {
                    ResearchNodeData node = n.researchUses[i];
                    Button b = IndustryUi.Btn(UiLocale.T("pm.research_chip", node.Title), () => OpenTree(node), "btn-small", "btn-ghost");
                    res.Add(b);
                }

                detail.Add(res);
            }
        }
    }

    /// <summary>Строка цепочки + рекурсивно ингредиенты. Скорость — в предметах в минуту.</summary>
    void AddChain(VisualElement parent, ProductionGraph.Node n, float perMin, int depth, HashSet<ItemData> seen)
    {
        if (n == null || depth > 9)
            return;
        var row = IndustryUi.El("Row", "row", "pm-row");
        row.style.paddingLeft = depth * 16;
        ProductionGraph.State state = ProductionGraph.StateOf(n);
        row.EnableInClassList("pm-locked", state == ProductionGraph.State.Locked);
        if (depth > 0)
            row.Add(IndustryUi.Text("Branch", "└", "pm-branch"));
        row.Add(IndustryUi.Icon(n.item.icon, "pm-row-icon"));
        var name = IndustryUi.Text("Name", n.item.Title, "pm-row-name");
        name.RegisterCallback<ClickEvent>(_ => Select(n.item));
        row.Add(name);
        row.Add(IndustryUi.Text("Rate", UiLocale.T("pm.rate_value", Format(perMin)), "pm-row-rate"));

        var where = IndustryUi.El("Where", "row", "pm-row-where");
        if (n.IsRaw)
        {
            if (n.extractor != null)
            {
                int count = Mathf.CeilToInt(perMin / Mathf.Max(0.01f, ProductionGraph.ExtractPerMinute(n.extractor)) - 0.001f);
                where.Add(IndustryUi.Text("M", UiLocale.T("pm.machines", n.extractor.Title, Mathf.Max(1, count)), "pm-machine"));
                if (!n.item.isFluid && TryNearestVein(n.item, out Vector2Int cell, out float dist))
                {
                    where.Add(IndustryUi.Text("Dist", UiLocale.T("pm.vein_dist", Mathf.RoundToInt(dist)), "caption"));
                    ItemData item = n.item;
                    where.Add(IndustryUi.Btn(UiLocale.T("pm.show_map"), () => ShowOnMap(item, cell), "btn-small", "btn-ghost"));
                }
            }
            else
                where.Add(IndustryUi.Text("M", UiLocale.T("pm.no_source"), "caption"));
        }
        else
        {
            RecipeData recipe = n.MainRecipe;
            BuildingData b = RecipeCodex.BuildingOf(recipe);
            float one = ProductionGraph.PerMinute(recipe, n.item);
            int count = one > 0f ? Mathf.CeilToInt(perMin / one - 0.001f) : 1;
            where.Add(IndustryUi.Text("M", UiLocale.T("pm.machines", b != null ? b.Title : "?", Mathf.Max(1, count)), "pm-machine"));
            if (!ProductionGraph.RecipeUnlocked(recipe))
            {
                ResearchNodeData r = ProductionGraph.Blocker(n);
                if (r != null)
                    where.Add(IndustryUi.Text("Lock", "🔒 " + r.Title, "caption", "pm-warn"));
            }
        }

        parent.Add(row);
        parent.Add(where);
        where.style.paddingLeft = depth * 16 + 30;

        if (n.IsRaw || !seen.Add(n.item))
            return;
        RecipeData main = n.MainRecipe;
        for (int i = 0; main.inputs != null && i < main.inputs.Count; i++)
        {
            ItemStack input = main.inputs[i];
            ProductionGraph.Node child = ProductionGraph.Get(input.item);
            float need = ProductionGraph.InputPerMinute(main, n.item, input, perMin);
            AddChain(parent, child, need, depth + 1, seen);
        }

        seen.Remove(n.item);
    }

    static string Format(float v)
    {
        return v >= 10f ? Mathf.RoundToInt(v).ToString() : v.ToString("0.#");
    }

    VisualElement ItemChip(ItemData item)
    {
        var chip = IndustryUi.El("Chip", "row", "pm-chip");
        chip.Add(IndustryUi.Icon(item.icon, "pm-row-icon"));
        chip.Add(IndustryUi.Text("N", item.Title, "pm-chip-name"));
        chip.pickingMode = PickingMode.Position;
        chip.RegisterCallback<ClickEvent>(_ => Select(item));
        return chip;
    }

    // ---------- Как подключить ----------

    /// <summary>
    /// Схема сверху: квадрат станка, зелёные стрелки внутрь (входы), оранжевая наружу (выход), синие — трубы.
    /// Стороны — по правилу префабов (<see cref="BuildingPrefabLayout"/>): выход спереди, вход сзади, второй вход справа (у конструктора оба входа сзади).
    /// </summary>
    VisualElement PortDiagram(BuildingData machine, ProductionGraph.Node n)
    {
        var wrap = IndustryUi.El("Ports", "col", "pm-ports");
        var box = IndustryUi.El("Box", "pm-port-box");
        box.Add(IndustryUi.Icon(machine.icon, "pm-port-icon"));
        wrap.Add(box);

        List<Port> ports = PortsOf(machine);
        RecipeData recipe = n.IsRaw ? null : n.MainRecipe;
        int solidIndex = 0;
        for (int i = 0; i < ports.Count; i++)
        {
            Port port = ports[i];
            string label;
            if (!port.input)
                label = UiLocale.T("pm.port_out", n.item.Title);
            else if (recipe != null)
                label = InputLabel(recipe, port.fluid, ref solidIndex);
            else
                label = UiLocale.T("pm.port_in_any");
            var el = IndustryUi.El("Port", "pm-port", port.side);
            el.AddToClassList(port.fluid ? "pm-port-fluid" : port.input ? "pm-port-in" : "pm-port-out");
            el.Add(IndustryUi.Text("Arrow", port.input ? ArrowIn(port.side) : ArrowOut(port.side), "pm-port-arrow"));
            el.Add(IndustryUi.Text("Label", label, "pm-port-label"));
            box.Add(el);
        }

        string note = recipe != null && CountSolidInputs(recipe) > 1 ? UiLocale.T("pm.note_any_input") : "";
        if (recipe != null && HasFluid(recipe))
            note += (note.Length > 0 ? " " : "") + UiLocale.T("pm.note_fluid");
        if (!string.IsNullOrEmpty(note))
            wrap.Add(IndustryUi.Text("Note", note, "caption", "pm-port-note"));
        return wrap;
    }

    struct Port
    {
        public string side;
        public bool input;
        public bool fluid;
    }

    static List<Port> PortsOf(BuildingData machine)
    {
        var list = new List<Port>(4);
        string id = GameDatabase.Normalize(machine.id);
        switch (id)
        {
            case "research_lab":
                list.Add(new Port { side = "pm-back", input = true });
                list.Add(new Port { side = "pm-left", input = true });
                list.Add(new Port { side = "pm-right", input = true });
                list.Add(new Port { side = "pm-front", input = true });
                return list;
            case "chemical_plant":
                list.Add(new Port { side = "pm-back", input = true });
                list.Add(new Port { side = "pm-left", input = true, fluid = true });
                list.Add(new Port { side = "pm-front", input = false });
                return list;
            case "constructor":
                list.Add(new Port { side = "pm-back-l", input = true });
                list.Add(new Port { side = "pm-back-r", input = true });
                list.Add(new Port { side = "pm-front", input = false });
                return list;
            case "power_generator":
                list.Add(new Port { side = "pm-back", input = true });
                list.Add(new Port { side = "pm-left", input = true, fluid = true });
                return list;
            case "refinery":
                list.Add(new Port { side = "pm-back", input = true, fluid = true });
                list.Add(new Port { side = "pm-front", input = false });
                return list;
            case "oil_extractor":
            case "water_extractor":
                list.Add(new Port { side = "pm-front", input = false, fluid = true });
                return list;
        }

        int inputs = 0;
        int outputs = 0;
        if (machine.prefab != null)
        {
            BuildingBase b = machine.prefab.GetComponent<BuildingBase>();
            if (b != null)
            {
                inputs = b.inputSockets != null ? b.inputSockets.Length : 0;
                outputs = b.outputSockets != null ? b.outputSockets.Length : 0;
            }
        }

        if (inputs > 0)
            list.Add(new Port { side = "pm-back", input = true });
        if (inputs > 1)
            list.Add(new Port { side = "pm-right", input = true });
        if (outputs > 0)
            list.Add(new Port { side = "pm-front", input = false });
        return list;
    }

    static string InputLabel(RecipeData recipe, bool fluid, ref int solidIndex)
    {
        int n = 0;
        for (int i = 0; recipe.inputs != null && i < recipe.inputs.Count; i++)
        {
            ItemData item = recipe.inputs[i].item;
            if (item == null || item.isFluid != fluid)
                continue;
            if (fluid || n == solidIndex)
            {
                if (!fluid)
                    solidIndex++;
                return item.Title + " ×" + recipe.inputs[i].amount;
            }

            n++;
        }

        return UiLocale.T("pm.port_free");
    }

    static int CountSolidInputs(RecipeData recipe)
    {
        int n = 0;
        for (int i = 0; recipe.inputs != null && i < recipe.inputs.Count; i++)
        {
            if (recipe.inputs[i].item != null && !recipe.inputs[i].item.isFluid)
                n++;
        }

        return n;
    }

    static bool HasFluid(RecipeData recipe)
    {
        for (int i = 0; recipe.inputs != null && i < recipe.inputs.Count; i++)
        {
            if (recipe.inputs[i].item != null && recipe.inputs[i].item.isFluid)
                return true;
        }

        for (int i = 0; recipe.outputs != null && i < recipe.outputs.Count; i++)
        {
            if (recipe.outputs[i].item != null && recipe.outputs[i].item.isFluid)
                return true;
        }

        return false;
    }

    static string ArrowIn(string side)
    {
        switch (side)
        {
            case "pm-back":
            case "pm-back-l":
            case "pm-back-r": return "↑";
            case "pm-front": return "↓";
            case "pm-left": return "→";
            default: return "←";
        }
    }

    static string ArrowOut(string side)
    {
        switch (side)
        {
            case "pm-back":
            case "pm-back-l":
            case "pm-back-r": return "↓";
            case "pm-front": return "↑";
            case "pm-left": return "←";
            default: return "→";
        }
    }

    // ---------- Действия ----------

    static bool TryNearestVein(ItemData item, out Vector2Int cell, out float dist)
    {
        var cells = new List<Vector2Int>(1);
        Vector3 from = TutorialSystem.PlayerPos;
        TutorialSystem.CollectVeinCells(item.id, cells, 1, from);
        if (cells.Count == 0)
        {
            cell = default;
            dist = 0f;
            return false;
        }

        cell = cells[0];
        Vector3 pos = TutorialSystem.CellWorld(cell);
        dist = Vector2.Distance(new Vector2(from.x, from.z), new Vector2(pos.x, pos.z));
        return true;
    }

    void ShowOnMap(ItemData item, Vector2Int cell)
    {
        if (MapMarkerSystem.Instance != null)
            MapMarkerSystem.Instance.Add(cell, item.Title);
        UiNotification.Push(UiLocale.T("pm.marker_added", item.Title), UiLocale.T("pm.marker_sub"), UiStatus.Ready);
        SetOpen(false);
        if (WorldMapUI.Instance != null)
            WorldMapUI.Instance.SetOpen(true);
    }

    void OpenTree(ResearchNodeData node)
    {
        SetOpen(false);
        if (MachineUI.Instance != null)
            MachineUI.Instance.OpenResearch(node != null ? node.id : null);
    }

    void ToggleTrack(ItemData item)
    {
        if (GoalSystem.Instance == null)
            return;
        if (GoalSystem.Instance.TrackedItem == item)
            GoalSystem.Instance.Track(null);
        else
            GoalSystem.Instance.Track(item);
        UiAudio.PlayConfirm();
        FillDetail();
    }
}
