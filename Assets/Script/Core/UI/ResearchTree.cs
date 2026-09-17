using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public static class ResearchTree
{
    public const float NodeW = 216f;
    public const float NodeH = 80f;
    public const float GapX = 14f;
    public const float GapY = 24f;
    public const float Pad = 20f;

    public static float ColW => NodeW + GapX;
    public static float RowH => NodeH + GapY;

    public static void Build(VisualElement canvas, Action<ResearchNodeData> onSelect, string selectedId)
    {
        if (canvas == null)
            return;

        canvas.Clear();
        canvas.AddToClassList("tree-rev3");
        Layout layout = Compute();
        var paint = new PaintData();
        canvas.userData = paint;
        canvas.generateVisualContent -= PaintEdges;
        canvas.generateVisualContent += PaintEdges;

        float maxRow = 0f;
        int maxCol = 0;
        for (int i = 0; i < layout.nodes.Count; i++)
        {
            Place place = layout.nodes[i];
            if (place.node == null)
                continue;
            if (place.col > maxCol)
                maxCol = place.col;
            if (place.row > maxRow)
                maxRow = place.row;

            float x = Pad + place.row * ColW;
            float y = Pad + place.col * RowH;
            paint.pos[place.node] = new Vector2(x, y);

            string status = StatusOf(place.node);
            Button card = TreeNode(place.node, status, IdsEqual(place.node.id, selectedId), onSelect);
            card.style.position = Position.Absolute;
            card.style.left = x;
            card.style.top = y;
            card.style.width = NodeW;
            card.style.height = NodeH;
            canvas.Add(card);
        }

        paint.edges = layout.edges;
        canvas.style.width = Pad * 2f + (maxRow + 1) * ColW - GapX;
        canvas.style.height = Pad * 2f + (maxCol + 1) * RowH - GapY;
        canvas.pickingMode = PickingMode.Ignore;
        canvas.MarkDirtyRepaint();
    }

    public static void ApplyStatus(VisualElement canvas, string selectedId)
    {
        if (canvas == null)
            return;
        for (int i = 0; i < canvas.childCount; i++)
        {
            VisualElement card = canvas[i];
            ResearchNodeData node = card.userData as ResearchNodeData;
            string status = StatusOf(node);
            SetNodeClasses(card, status, node != null && IdsEqual(node.id, selectedId));
        }

        canvas.MarkDirtyRepaint();
    }

    public static void FillDetail(VisualElement detail, ResearchNodeData node, Action onStart)
    {
        if (detail == null)
            return;
        detail.Clear();
        if (node == null)
        {
            detail.Add(IndustryUi.Text("T", UiLocale.T("research.pick"), "heading-3"));
            detail.Add(IndustryUi.Text("D", UiLocale.T("research.pick_body"), "caption"));
            return;
        }

        string status = StatusOf(node);
        var head = IndustryUi.El("Head", "row");
        head.Add(IndustryUi.Icon(node.icon, "icon-48"));
        var titles = IndustryUi.El("Titles", "col", "grow");
        titles.Add(IndustryUi.Text("T", node.displayName, "heading-3"));
        titles.Add(IndustryUi.Text("S", StatusLabel(status), "badge", StatusBadge(status)));
        head.Add(titles);
        detail.Add(head);

        if (!string.IsNullOrEmpty(node.description))
            detail.Add(IndustryUi.Text("Desc", node.description, "body-text"));

        if (HasPrereqs(node))
        {
            detail.Add(IndustryUi.Text("PR", UiLocale.T("research.prereq"), "label-caps"));
            var list = IndustryUi.El("Prereqs", "col");
            for (int i = 0; i < node.requiredResearches.Count; i++)
            {
                ResearchNodeData req = node.requiredResearches[i];
                if (req == null)
                    continue;
                bool done = ResearchSystem.Instance != null && ResearchSystem.Instance.IsResearchUnlocked(req);
                var row = IndustryUi.El("P", "row");
                row.Add(IndustryUi.Icon(req.icon, "icon-24"));
                row.Add(IndustryUi.Text("N", req.displayName, "caption", "grow"));
                row.Add(IndustryUi.Text("K", done ? "✓" : "·", done ? "gold" : "muted"));
                list.Add(row);
            }
            detail.Add(list);
        }

        detail.Add(IndustryUi.Text("Need", UiLocale.T("research.need"), "label-caps"));
        var need = IndustryUi.El("NeedRow", "row", "io-row");
        if (status == "ACTIVE" && ResearchSystem.Instance != null && node.requiredItems != null)
        {
            for (int i = 0; i < node.requiredItems.Count; i++)
            {
                ItemStack stack = node.requiredItems[i];
                if (stack == null || stack.item == null)
                    continue;
                int have = ResearchSystem.Instance.GetSubmitted(node, stack.item);
                var chip = IndustryUi.El("Chip", "stack-chip");
                chip.Add(IndustryUi.Icon(stack.item.icon, "stack-icon"));
                chip.Add(IndustryUi.Text("N", have + "/" + stack.amount, "stack-count"));
                UiTooltip.Bind(chip, stack.item.displayName, have + " / " + stack.amount);
                need.Add(chip);
            }
        }
        else
            IndustryUi.AddStacks(need, node.requiredItems);
        detail.Add(need);

        if (status == "ACTIVE")
        {
            VisualElement bar = IndustryUi.ProgressBar("TreeProgress");
            IndustryUi.SetProgress(bar, ResearchSystem.Instance != null
                ? ResearchSystem.Instance.GetProgress01(node)
                : 0f);
            detail.Add(bar);
        }

        bool hasUnlock = HasUnlocks(node);
        if (hasUnlock)
        {
            detail.Add(IndustryUi.Text("Un", UiLocale.T("research.unlocks"), "label-caps"));
            var reward = IndustryUi.El("Reward", "row", "io-row");
            if (node.unlockedBuildings != null)
            {
                for (int i = 0; i < node.unlockedBuildings.Count; i++)
                {
                    if (node.unlockedBuildings[i] != null)
                        reward.Add(IndustryUi.BuildingChip(node.unlockedBuildings[i]));
                }
            }

            if (node.unlockedRecipes != null)
            {
                for (int i = 0; i < node.unlockedRecipes.Count; i++)
                {
                    RecipeData recipe = node.unlockedRecipes[i];
                    if (recipe == null)
                        continue;
                    ItemData output = IndustryUi.FirstItem(recipe.outputs);
                    var chip = IndustryUi.El("Rec", "codex-building");
                    chip.Add(IndustryUi.Icon(output != null ? output.icon : null, "icon-32"));
                    chip.Add(IndustryUi.Text("N", recipe.displayName, "caption"));
                    reward.Add(chip);
                }
            }

            detail.Add(reward);
        }

        if (status == "ACTIVE")
            detail.Add(IndustryUi.Text("Auto", UiLocale.T("research.auto"), "caption"));
    }

    public static ResearchNodeData DefaultSelection()
    {
        ResearchSystem rs = ResearchSystem.Instance;
        if (rs == null)
            return null;
        if (rs.CurrentResearch != null)
            return rs.CurrentResearch;
        List<ResearchNodeData> nodes = rs.GetAllNodes();
        ResearchNodeData firstReady = null;
        ResearchNodeData first = null;
        for (int i = 0; i < nodes.Count; i++)
        {
            ResearchNodeData node = nodes[i];
            if (node == null)
                continue;
            if (first == null)
                first = node;
            if (firstReady == null && rs.CanStartResearch(node))
                firstReady = node;
        }

        return firstReady != null ? firstReady : first;
    }

    public static string StatusOf(ResearchNodeData node)
    {
        ResearchSystem rs = ResearchSystem.Instance;
        if (rs == null || node == null)
            return "LOCKED";
        if (rs.IsResearchUnlocked(node))
            return "DONE";
        if (rs.CanStartResearch(node))
            return "ACTIVE";
        return "LOCKED";
    }

    static Button TreeNode(ResearchNodeData node, string status, bool selected, Action<ResearchNodeData> onSelect)
    {
        ResearchNodeData captured = node;
        Button card = IndustryUi.CardButton(() => onSelect?.Invoke(captured), "research-node", "research-tree-node");
        if (node != null && !string.IsNullOrEmpty(node.id))
            card.name = "Res_" + node.id;
        card.userData = node;
        SetNodeClasses(card, status, selected);
        card.Add(IndustryUi.El("Pip", "research-tree-pip"));
        card.Add(IndustryUi.Icon(node != null ? node.icon : null, "research-tree-icon"));
        var col = IndustryUi.El("Meta", "col", "grow", "research-tree-meta");
        col.Add(IndustryUi.Text("T", node != null ? node.displayName : "Research", "research-tree-title"));
        col.Add(IndustryUi.Text("S", StatusLabel(status), "research-tree-status"));
        card.Add(col);
        UiTooltip.Bind(card, node != null ? node.displayName : "Research", node != null ? node.description : "", StatusLabel(status));
        return card;
    }

    static void SetNodeClasses(VisualElement card, string status, bool selected)
    {
        if (card == null)
            return;
        card.EnableInClassList("is-complete", status == "DONE");
        card.EnableInClassList("is-active", status == "ACTIVE");
        card.EnableInClassList("is-locked", status == "LOCKED");
        card.EnableInClassList("is-ready", status == "READY");
        card.EnableInClassList("is-selected", selected);
    }

    static string StatusLabel(string status)
    {
        if (status == "DONE")
            return UiLocale.T("research.done");
        if (status == "ACTIVE")
            return UiLocale.T("research.active");
        if (status == "LOCKED")
            return UiLocale.T("research.locked");
        return UiLocale.T("research.ready");
    }

    static string StatusBadge(string status)
    {
        if (status == "DONE")
            return "badge-done";
        if (status == "ACTIVE")
            return "badge-running";
        if (status == "LOCKED")
            return "badge-locked";
        return "badge-ready";
    }

    static bool HasPrereqs(ResearchNodeData node)
    {
        if (node == null || node.requiredResearches == null)
            return false;
        for (int i = 0; i < node.requiredResearches.Count; i++)
        {
            if (node.requiredResearches[i] != null)
                return true;
        }

        return false;
    }

    static bool HasUnlocks(ResearchNodeData node)
    {
        if (node == null)
            return false;
        if (node.unlockedBuildings != null)
        {
            for (int i = 0; i < node.unlockedBuildings.Count; i++)
            {
                if (node.unlockedBuildings[i] != null)
                    return true;
            }
        }

        if (node.unlockedRecipes != null)
        {
            for (int i = 0; i < node.unlockedRecipes.Count; i++)
            {
                if (node.unlockedRecipes[i] != null)
                    return true;
            }
        }

        return false;
    }

    static Layout Compute()
    {
        var layout = new Layout();
        ResearchSystem rs = ResearchSystem.Instance;
        List<ResearchNodeData> nodes = rs != null
            ? rs.GetAllNodes()
            : new List<ResearchNodeData>(GameDatabase.AllResearches());

        var set = new HashSet<ResearchNodeData>();
        var order = new Dictionary<ResearchNodeData, int>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null || set.Contains(nodes[i]))
                continue;
            set.Add(nodes[i]);
            order[nodes[i]] = i;
        }

        var dagDepth = new Dictionary<ResearchNodeData, int>();
        var primary = new Dictionary<ResearchNodeData, ResearchNodeData>();
        var children = new Dictionary<ResearchNodeData, List<ResearchNodeData>>();
        var roots = new List<ResearchNodeData>();

        foreach (ResearchNodeData node in set)
            children[node] = new List<ResearchNodeData>();

        foreach (ResearchNodeData node in set)
        {
            ResearchNodeData parent = null;
            int best = -1;
            if (node.requiredResearches != null)
            {
                for (int i = 0; i < node.requiredResearches.Count; i++)
                {
                    ResearchNodeData req = node.requiredResearches[i];
                    if (req == null || !set.Contains(req))
                        continue;
                    layout.edges.Add(new Edge { from = req, to = node, primary = false });
                    int d = DagDepth(req, dagDepth, 0);
                    if (d > best || (d == best && parent != null && order[req] < order[parent]))
                    {
                        best = d;
                        parent = req;
                    }
                }
            }

            if (parent == null)
            {
                roots.Add(node);
                continue;
            }

            primary[node] = parent;
            children[parent].Add(node);
            for (int e = 0; e < layout.edges.Count; e++)
            {
                Edge edge = layout.edges[e];
                if (edge.from == parent && edge.to == node)
                {
                    edge.primary = true;
                    layout.edges[e] = edge;
                }
            }
        }

        foreach (KeyValuePair<ResearchNodeData, List<ResearchNodeData>> pair in children)
        {
            pair.Value.Sort((a, b) => order[a].CompareTo(order[b]));
        }

        roots.Sort((a, b) => order[a].CompareTo(order[b]));

        var row = new Dictionary<ResearchNodeData, float>();
        float next = 0f;
        for (int i = 0; i < roots.Count; i++)
            next = AssignRow(roots[i], next, children, row);

        foreach (ResearchNodeData node in set)
        {
            layout.nodes.Add(new Place
            {
                node = node,
                col = TreeCol(node, primary),
                row = row.TryGetValue(node, out float r) ? r : 0f
            });
        }

        return layout;
    }

    static float AssignRow(
        ResearchNodeData node,
        float next,
        Dictionary<ResearchNodeData, List<ResearchNodeData>> children,
        Dictionary<ResearchNodeData, float> row)
    {
        List<ResearchNodeData> kids = children[node];
        if (kids.Count == 0)
        {
            row[node] = next;
            return next + 1f;
        }

        float start = next;
        for (int i = 0; i < kids.Count; i++)
            next = AssignRow(kids[i], next, children, row);
        row[node] = (start + next - 1f) * 0.5f;
        return next;
    }

    static int TreeCol(ResearchNodeData node, Dictionary<ResearchNodeData, ResearchNodeData> primary)
    {
        int col = 0;
        ResearchNodeData walk = node;
        int guard = 0;
        while (walk != null && primary.TryGetValue(walk, out ResearchNodeData parent) && guard++ < 64)
        {
            col++;
            walk = parent;
        }

        return col;
    }

    static int DagDepth(ResearchNodeData node, Dictionary<ResearchNodeData, int> memo, int guard)
    {
        if (node == null)
            return 0;
        if (memo.TryGetValue(node, out int cached))
            return cached;
        if (guard > 32)
            return 0;
        int depth = 0;
        if (node.requiredResearches != null)
        {
            for (int i = 0; i < node.requiredResearches.Count; i++)
            {
                int d = DagDepth(node.requiredResearches[i], memo, guard + 1) + 1;
                if (d > depth)
                    depth = d;
            }
        }

        memo[node] = depth;
        return depth;
    }

    static void PaintEdges(MeshGenerationContext ctx)
    {
        VisualElement canvas = ctx.visualElement;
        PaintData paint = canvas != null ? canvas.userData as PaintData : null;
        if (paint == null || paint.edges == null)
            return;

        Painter2D painter = ctx.painter2D;
        painter.lineCap = LineCap.Round;
        painter.lineJoin = LineJoin.Round;

        for (int pass = 0; pass < 2; pass++)
        {
            bool primaryPass = pass == 1;
            for (int i = 0; i < paint.edges.Count; i++)
            {
                Edge edge = paint.edges[i];
                if (edge.primary != primaryPass)
                    continue;
                if (edge.from == null || edge.to == null)
                    continue;
                if (!paint.pos.TryGetValue(edge.from, out Vector2 a))
                    continue;
                if (!paint.pos.TryGetValue(edge.to, out Vector2 b))
                    continue;

                Vector2 start = new Vector2(a.x + NodeW * 0.5f, a.y + NodeH);
                Vector2 end = new Vector2(b.x + NodeW * 0.5f, b.y);
                string status = StatusOf(edge.to);
                Color color = EdgeColor(status, edge.primary);
                painter.strokeColor = color;
                painter.lineWidth = edge.primary ? 3f : 1.6f;
                DrawElbow(painter, start, end, edge.primary);
                DrawArrow(painter, end, color);
            }
        }
    }

    static void DrawElbow(Painter2D painter, Vector2 start, Vector2 end, bool primary)
    {
        float span = end.y - start.y;
        painter.BeginPath();
        painter.MoveTo(start);
        if (span <= 2f)
        {
            painter.LineTo(end);
            painter.Stroke();
            return;
        }

        if (Mathf.Abs(start.x - end.x) < 1.5f)
        {
            painter.LineTo(end);
            painter.Stroke();
            return;
        }

        float bus;
        if (primary)
            bus = start.y + Mathf.Clamp(span * 0.38f, 8f, 16f);
        else
            bus = start.y + Mathf.Clamp(span * 0.72f, 12f, span - 8f);

        float gutter = start.x;
        if (!primary)
            gutter = SnapToGutter((start.x + end.x) * 0.5f);

        painter.LineTo(new Vector2(start.x, bus));
        if (!primary && Mathf.Abs(gutter - start.x) > 1.5f)
        {
            painter.LineTo(new Vector2(gutter, bus));
            float childBus = end.y - Mathf.Min(10f, span * 0.22f);
            painter.LineTo(new Vector2(gutter, childBus));
            painter.LineTo(new Vector2(end.x, childBus));
        }
        else
            painter.LineTo(new Vector2(end.x, bus));
        painter.LineTo(end);
        painter.Stroke();
    }

    static float SnapToGutter(float x)
    {
        float gutter0 = Pad + NodeW + GapX * 0.5f;
        float slot = Mathf.Round((x - gutter0) / ColW);
        return gutter0 + slot * ColW;
    }

    static void DrawArrow(Painter2D painter, Vector2 tip, Color color)
    {
        painter.strokeColor = color;
        painter.fillColor = color;
        painter.lineWidth = 1.5f;
        painter.BeginPath();
        painter.MoveTo(tip);
        painter.LineTo(tip + new Vector2(-6f, -10f));
        painter.LineTo(tip + new Vector2(6f, -10f));
        painter.ClosePath();
        painter.Fill();
    }

    static Color EdgeColor(string status, bool primary)
    {
        if (!primary)
            return new Color(0.55f, 0.72f, 0.84f, 0.7f);
        if (status == "DONE")
            return new Color(0.45f, 0.78f, 0.55f, 0.95f);
        if (status == "ACTIVE")
            return new Color(1f, 0.72f, 0.28f, 1f);
        if (status == "READY")
            return new Color(0.95f, 0.7f, 0.28f, 0.95f);
        return new Color(0.7f, 0.76f, 0.82f, 0.8f);
    }

    static bool IdsEqual(string a, string b)
    {
        return GameDatabase.Normalize(a) == GameDatabase.Normalize(b);
    }

    struct Place
    {
        public ResearchNodeData node;
        public int col;
        public float row;
    }

    struct Edge
    {
        public ResearchNodeData from;
        public ResearchNodeData to;
        public bool primary;
    }

    class Layout
    {
        public readonly List<Place> nodes = new List<Place>();
        public readonly List<Edge> edges = new List<Edge>();
    }

    class PaintData
    {
        public readonly Dictionary<ResearchNodeData, Vector2> pos = new Dictionary<ResearchNodeData, Vector2>();
        public List<Edge> edges;
    }
}

public sealed class ResearchTreeNav
{
    const float MinZoom = 0.35f;
    const float MaxZoom = 1.85f;
    const float ZoomOut = 0.86f;
    const float ZoomIn = 1.16f;

    VisualElement view;
    VisualElement canvas;
    float zoom = 1f;
    Vector2 pan;
    bool panning;
    int panPointer = -1;
    Vector2 last;
    bool attached;

    public void Attach(VisualElement host, VisualElement tree)
    {
        view = host;
        canvas = tree;
        if (view == null || canvas == null || attached)
            return;
        attached = true;
        view.RegisterCallback<WheelEvent>(OnWheel, TrickleDown.TrickleDown);
        view.RegisterCallback<PointerDownEvent>(OnDown, TrickleDown.TrickleDown);
        view.RegisterCallback<PointerMoveEvent>(OnMove);
        view.RegisterCallback<PointerUpEvent>(OnUp);
        view.RegisterCallback<PointerCaptureOutEvent>(OnCaptureOut);
        canvas.style.transformOrigin = new TransformOrigin(Length.Percent(0), Length.Percent(0));
        Apply();
    }

    public void Focus(VisualElement card)
    {
        if (view == null || canvas == null || card == null)
            return;
        float vw = view.resolvedStyle.width;
        float vh = view.resolvedStyle.height;
        if (vw < 8f || vh < 8f)
        {
            view.schedule.Execute(() => Focus(card));
            return;
        }

        float x = Read(card.style.left) + ResearchTree.NodeW * 0.5f;
        float y = Read(card.style.top) + ResearchTree.NodeH * 0.5f;
        pan = new Vector2(vw * 0.5f - x * zoom, vh * 0.28f - y * zoom);
        Clamp();
        Apply();
    }

    void OnWheel(WheelEvent evt)
    {
        if (view == null || canvas == null)
            return;
        float factor = evt.delta.y > 0f ? ZoomOut : ZoomIn;
        Vector2 mouse = view.WorldToLocal(evt.mousePosition);
        Vector2 content = (mouse - pan) / Mathf.Max(0.01f, zoom);
        zoom = Mathf.Clamp(zoom * factor, MinZoom, MaxZoom);
        pan = mouse - content * zoom;
        Clamp();
        Apply();
        evt.StopImmediatePropagation();
    }

    void OnDown(PointerDownEvent evt)
    {
        if (evt.button != 1 || view == null)
            return;
        panning = true;
        panPointer = evt.pointerId;
        last = evt.position;
        view.CapturePointer(evt.pointerId);
        view.AddToClassList("is-panning");
        evt.StopImmediatePropagation();
    }

    void OnMove(PointerMoveEvent evt)
    {
        if (!panning)
            return;
        Vector2 now = evt.position;
        pan += now - last;
        last = now;
        Clamp();
        Apply();
        evt.StopPropagation();
    }

    void OnUp(PointerUpEvent evt)
    {
        if (!panning || evt.pointerId != panPointer)
            return;
        EndPan(evt.pointerId);
    }

    void OnCaptureOut(PointerCaptureOutEvent evt)
    {
        if (panning)
            EndPan(panPointer);
    }

    void EndPan(int pointerId)
    {
        panning = false;
        panPointer = -1;
        if (view != null)
        {
            if (view.HasPointerCapture(pointerId))
                view.ReleasePointer(pointerId);
            view.RemoveFromClassList("is-panning");
        }
    }

    void Clamp()
    {
        if (view == null || canvas == null)
            return;
        float vw = view.resolvedStyle.width;
        float vh = view.resolvedStyle.height;
        float cw = ContentWidth() * zoom;
        float ch = ContentHeight() * zoom;
        if (vw < 8f || vh < 8f)
            return;
        pan.x = ClampAxis(pan.x, vw, cw);
        pan.y = ClampAxis(pan.y, vh, ch);
    }

    static float ClampAxis(float panValue, float viewSize, float contentSize)
    {
        float margin = 72f;
        if (contentSize + margin * 2f <= viewSize)
            return (viewSize - contentSize) * 0.5f;
        float min = viewSize - contentSize - margin;
        float max = margin;
        return Mathf.Clamp(panValue, min, max);
    }

    float ContentWidth()
    {
        float w = canvas.style.width.keyword == StyleKeyword.Auto ? canvas.layout.width : canvas.style.width.value.value;
        if (w < 1f)
            w = canvas.layout.width;
        return Mathf.Max(1f, w);
    }

    float ContentHeight()
    {
        float h = canvas.style.height.keyword == StyleKeyword.Auto ? canvas.layout.height : canvas.style.height.value.value;
        if (h < 1f)
            h = canvas.layout.height;
        return Mathf.Max(1f, h);
    }

    void Apply()
    {
        if (canvas == null)
            return;
        canvas.style.translate = new Translate(pan.x, pan.y);
        canvas.style.scale = new Scale(new Vector3(zoom, zoom, 1f));
    }

    static float Read(StyleLength length)
    {
        return length.keyword == StyleKeyword.Auto ? 0f : length.value.value;
    }
}
