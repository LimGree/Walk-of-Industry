using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class WorldMapUI : MonoBehaviour
{
    public static WorldMapUI Instance { get; private set; }

    public bool IsOpen { get; private set; }

    static readonly Color BuildingColor = new Color(0.55f, 1f, 0.82f, 1f);
    static readonly Color BeltColor = new Color(0.82f, 0.62f, 0.28f, 1f);

    InputSystem_Actions input;
    VisualElement fullRoot;
    VisualElement miniHost;
    VisualElement miniFrame;
    VisualElement miniClip;
    VisualElement miniOverlay;
    VisualElement fullWrap;
    VisualElement fullOverlay;
    VisualElement waypointPanel;
    VisualElement waypointList;
    VisualElement editPanel;
    MapView miniImage;
    MapView fullImage;
    VisualElement miniMarker;
    VisualElement fullMarker;
    MeasureLayer measureLayer;
    Label tooltip;
    Label coordLabel;
    Label zoomLabel;
    Label measureLabel;
    Label miniInfo;
    Label miniClock;
    Label worldCompass;
    Label[] compass;
    Button measureBtn;
    bool layerVeins = true;
    bool layerBuildings = true;
    bool layerBelts = true;
    Button teleportBtn;
    TextField waypointSearch;
    TextField editName;
    int lastFogVersion = -1;
    Texture2D mapTex;
    Rect viewUv = new Rect(0f, 0f, 1f, 1f);
    Rect miniUv = new Rect(0f, 0f, 1f, 1f);
    bool dragging;
    bool rmbHold;
    bool rmbDragged;
    bool rmbHasCell;
    Vector2Int rmbCell;
    bool measuring;
    bool measureTool;
    bool measureOn;
    bool mapTimeFrozen;
    Vector2 lastMouse;
    Vector2Int measureA;
    Vector2Int measureB;
    MapMarkerSave editing;
    string waypointFilter = "";
    readonly List<VisualElement> miniPins = new List<VisualElement>();
    readonly List<VisualElement> fullPins = new List<VisualElement>();
    float nextMiniTick;

    public static bool MiniRoundPref
    {
        get => MapSettings.MiniRound;
        set => MapSettings.MiniRound = value;
    }

    public static bool MiniFollowPref
    {
        get => MapSettings.MiniFollow;
        set => MapSettings.MiniFollow = value;
    }

    public bool MiniRound
    {
        get => MapSettings.MiniRound;
        set => MapSettings.MiniRound = value;
    }

    public bool MiniFollow
    {
        get => MapSettings.MiniFollow;
        set => MapSettings.MiniFollow = value;
    }

    public float MiniZoom
    {
        get => MapSettings.MiniZoom;
        set => MapSettings.MiniZoom = value;
    }

    public float MiniSize
    {
        get => MapSettings.MiniSize;
        set => MapSettings.MiniSize = value;
    }

    void Awake()
    {
        Instance = this;
        input = KeybindStore.Shared;
        BuildUi();
        ApplyMiniSize();
        ApplyMiniStyle();
        MapSettings.Changed += OnMapSettingsChanged;
    }

    void OnEnable()
    {
        if (input != null)
            input.Player.MoveSelection.performed += OnMapToggle;
        BindMarkers();
    }

    void OnDisable()
    {
        if (input != null)
            input.Player.MoveSelection.performed -= OnMapToggle;
        if (MapMarkerSystem.Instance != null)
            MapMarkerSystem.Instance.OnChanged -= RefreshPins;
    }

    void Start()
    {
        BindMarkers();
        RefreshPins();
    }

    void BindMarkers()
    {
        if (MapMarkerSystem.Instance == null)
            return;
        MapMarkerSystem.Instance.OnChanged -= RefreshPins;
        MapMarkerSystem.Instance.OnChanged += RefreshPins;
    }

    void OnDestroy()
    {
        MapSettings.Changed -= OnMapSettingsChanged;
        if (MapMarkerSystem.Instance != null)
            MapMarkerSystem.Instance.OnChanged -= RefreshPins;
        if (Instance == this)
            Instance = null;
        if (mapTex != null)
            Destroy(mapTex);
        RestoreMapTime();
    }

    void OnMapSettingsChanged()
    {
        ApplyMiniSize();
        ApplyMiniStyle();
        RefreshPins();
        if (miniImage != null)
            miniImage.ShowGrid = MapSettings.MiniGrid;
        if (fullImage != null)
            fullImage.ShowGrid = MapSettings.WorldGrid;
        IndustryUi.Show(teleportBtn, MapSettings.AllowTeleport && editing != null);
        lastFogVersion = -1;
    }

    void LateUpdate()
    {
        if (mapTex == null)
            Rebuild();

        bool miniTick = Time.unscaledTime >= nextMiniTick;
        if (miniTick)
            nextMiniTick = Time.unscaledTime + 0.2f;

        if (IsOpen || miniTick)
            UpdateFogOverlay();

        UpdateMiniView();
        UpdatePlayerMark(miniMarker, miniOverlay, miniUv, MiniFollow ? PlayerYaw() : 0f, true);
        UpdateCompass();
        if (miniTick)
        {
            UpdateMiniInfo();
            UpdateMiniClock();
            LayoutPins();
        }

        if (IsOpen)
        {
            if (fullImage != null)
            {
                fullImage.ViewUv = viewUv;
                fullImage.ViewYaw = 0f;
                fullImage.ShowGrid = MapSettings.WorldGrid;
            }
            UpdatePlayerMark(fullMarker, fullOverlay, viewUv, 0f, false);
            UpdateMeasureVisual();
            PanWithKeys();
            IndustryUi.Show(worldCompass, MapSettings.WorldCompass);
            LayoutPins();
        }
        else if (tooltip != null)
            IndustryUi.Show(tooltip, false);
    }

    void OnMapToggle(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            return;
        if (BuildMenuUI.Instance != null && BuildMenuUI.Instance.IsOpen)
            return;
        if (ResearchUI.Instance != null && ResearchUI.Instance.IsOpen)
            return;
        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
            return;

        if (IsOpen)
        {
            SetOpen(false);
            return;
        }

        if (SelectionHoldsMapKey())
            return;

        SetOpen(true);
    }

    static bool SelectionHoldsMapKey()
    {
        PlayerBuilder builder = GameManager.Instance != null
            ? GameManager.Instance.playerBuilder
            : FindFirstObjectByType<PlayerBuilder>();
        if (builder == null || !builder.isBuildMode)
            return false;
        BuildSelectionController sel = builder.Selection;
        if (sel == null)
            return false;
        return sel.IsPasteActive || sel.IsMoveActive
            || (sel.IsSelectionMode && sel.HasSelectedBuildings);
    }

    public void SetOpen(bool open)
    {
        IsOpen = open;
        IndustryUi.Show(fullRoot, open);
        IndustryUi.Show(miniHost, !open && MapSettings.MiniVisible);
        if (open)
        {
            dragging = false;
            measuring = false;
            Rebuild();
            CenterOnPlayer(0.28f);
            RefreshPins();
            RebuildWaypointList();
            GameAudio.Ui("ui_map_open");
            if (MapSettings.WorldPause && Time.timeScale > 0f)
            {
                Time.timeScale = 0f;
                mapTimeFrozen = true;
            }
        }
        else
        {
            RestoreMapTime();
            IndustryUi.Show(tooltip, false);
            if (UiModal.IsOpen)
                UiModal.Hide();
        }
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    void RestoreMapTime()
    {
        if (!mapTimeFrozen)
            return;
        mapTimeFrozen = false;
        if (GameManager.Instance == null || !GameManager.Instance.IsPaused)
            Time.timeScale = 1f;
    }

    public static void AddMinimapSettings(VisualElement parent)
    {
        MapSettingsUI.Fill(parent);
    }

    public byte[] EncodePreviewPng(int maxSize = 256)
    {
        Rebuild();
        if (mapTex == null)
            return null;
        int w = mapTex.width;
        int h = mapTex.height;
        int max = Mathf.Max(w, h);
        if (max <= maxSize)
            return mapTex.EncodeToPNG();
        float s = maxSize / (float)max;
        int nw = Mathf.Max(1, Mathf.RoundToInt(w * s));
        int nh = Mathf.Max(1, Mathf.RoundToInt(h * s));
        var small = new Texture2D(nw, nh, TextureFormat.RGBA32, false);
        small.filterMode = FilterMode.Point;
        for (int y = 0; y < nh; y++)
        {
            int sy = Mathf.Clamp(Mathf.FloorToInt(y / s), 0, h - 1);
            for (int x = 0; x < nw; x++)
            {
                int sx = Mathf.Clamp(Mathf.FloorToInt(x / s), 0, w - 1);
                small.SetPixel(x, y, mapTex.GetPixel(sx, sy));
            }
        }

        small.Apply(false, false);
        byte[] png = small.EncodeToPNG();
        Destroy(small);
        return png;
    }

    public void Rebuild()
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady || map.BiomeTexture == null)
            return;

        int w = map.MapWidth;
        int h = map.MapHeight;
        if (mapTex == null || mapTex.width != w || mapTex.height != h)
        {
            if (mapTex != null)
                Destroy(mapTex);
            mapTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            mapTex.filterMode = FilterMode.Point;
            mapTex.wrapMode = TextureWrapMode.Clamp;
        }

        Color[] pixels = map.BiomeTexture.GetPixels();
        var scatter = WorldResourceScatterer.Instance;
        if (layerVeins && scatter != null)
        {
            var veins = scatter.Veins;
            for (int i = 0; i < veins.Count; i++)
                PaintCell(pixels, w, h, veins[i].cell, veins[i].color);
        }

        PaintBuildings(pixels, w, h);
        mapTex.SetPixels(pixels);
        mapTex.Apply(false, false);
        if (miniImage != null)
        {
            miniImage.MapTexture = mapTex;
            miniImage.ShowGrid = MapSettings.MiniGrid;
            miniImage.SetMapBounds(map.MapMinX, map.MapMinZ, map.MapWidth, map.MapHeight);
        }
        if (fullImage != null)
        {
            fullImage.MapTexture = mapTex;
            fullImage.ShowGrid = MapSettings.WorldGrid;
            fullImage.SetMapBounds(map.MapMinX, map.MapMinZ, map.MapWidth, map.MapHeight);
        }
    }

    void PaintBuildings(Color[] pixels, int w, int h)
    {
        var objects = new List<GameObject>(64);
        GridOccupancy.CollectAllOccupiedObjects(objects, new HashSet<int>());
        var cells = new List<Vector2Int>(8);
        for (int i = 0; i < objects.Count; i++)
        {
            GameObject go = objects[i];
            if (go == null)
                continue;
            bool belt = go.GetComponent<Conveyor>() != null;
            if (belt && !layerBelts)
                continue;
            if (!belt && !layerBuildings)
                continue;
            Color color = belt ? BeltColor : BuildingColor;
            if (!GridOccupancy.TryGetCells(go, cells))
                continue;
            for (int c = 0; c < cells.Count; c++)
                PaintCell(pixels, w, h, cells[c], color);
        }
    }

    static void PaintCell(Color[] pixels, int w, int h, Vector2Int cell, Color color)
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null)
            return;
        int x = cell.x - map.MapMinX;
        int z = cell.y - map.MapMinZ;
        if (x < 0 || z < 0 || x >= w || z >= h)
            return;
        pixels[z * w + x] = color;
    }

    void BuildUi()
    {
        VisualElement root = IndustryUi.Mount(this, 95);
        miniHost = IndustryUi.El("MiniHost", "mini-host");
        miniFrame = IndustryUi.El("Mini", "mini-map", "map-frame");
        miniClip = IndustryUi.El("MiniClip", "mini-clip");
        miniImage = MakeMapImage("MiniImage");
        miniOverlay = IndustryUi.El("MiniOverlay", "map-overlay");
        miniOverlay.pickingMode = PickingMode.Ignore;
        miniMarker = MakePlayerMark("MiniMark");
        miniOverlay.Add(miniMarker);
        compass = new[]
        {
            MakeCompass("N", "N"),
            MakeCompass("E", "E"),
            MakeCompass("S", "S"),
            MakeCompass("W", "W")
        };
        for (int i = 0; i < compass.Length; i++)
            miniOverlay.Add(compass[i]);
        miniClip.Add(miniImage);
        miniClip.Add(miniOverlay);
        miniFrame.Add(miniClip);
        miniInfo = IndustryUi.Text("MiniInfo", "", "mini-info");
        miniInfo.pickingMode = PickingMode.Ignore;
        miniClock = IndustryUi.Text("MiniClock", "", "mini-clock");
        miniClock.pickingMode = PickingMode.Ignore;
        miniHost.Add(miniFrame);
        miniHost.Add(miniClock);
        miniHost.Add(miniInfo);
        miniClip.RegisterCallback<WheelEvent>(OnMiniWheel);
        miniClip.RegisterCallback<PointerMoveEvent>(evt => UpdateTooltip(miniImage, miniUv, evt.localPosition, evt.position, MiniFollow ? PlayerYaw() : 0f));
        miniClip.RegisterCallback<PointerLeaveEvent>(_ => IndustryUi.Show(tooltip, false));
        root.Add(miniHost);

        fullRoot = IndustryUi.Screen("FullMap");
        fullRoot.Add(IndustryUi.El("Dim", "dim"));
        var chrome = IndustryUi.El("Chrome", "panel", "map-chrome");
        var head = IndustryUi.El("Head", "header");
        head.Add(IndustryUi.Text("Title", UiLocale.T("overlay.map"), "title"));
        coordLabel = IndustryUi.Text("Coords", "", "muted");
        head.Add(coordLabel);
        head.Add(IndustryUi.Btn("✕", () => SetOpen(false), "close"));
        chrome.Add(head);

        var legend = IndustryUi.El("Legend", "map-legend");
        AddSwatch(legend, new Color(0.13f, 0.30f, 0.16f), UiLocale.T("map.forest"));
        AddSwatch(legend, new Color(0.58f, 0.74f, 0.34f), UiLocale.T("map.field"));
        AddSwatch(legend, new Color(0.28f, 0.28f, 0.30f), UiLocale.T("map.mountain"));
        AddSwatch(legend, new Color(0.11f, 0.32f, 0.52f), UiLocale.T("map.water"));
        AddLayerChip(legend, UiLocale.T("map.veins"), new Color(0.72f, 0.24f, 0.18f), () => layerVeins, v => layerVeins = v);
        AddLayerChip(legend, UiLocale.T("map.buildings"), BuildingColor, () => layerBuildings, v => layerBuildings = v);
        AddLayerChip(legend, UiLocale.T("map.belts"), BeltColor, () => layerBelts, v => layerBelts = v);
        chrome.Add(legend);

        fullWrap = IndustryUi.El("FullWrap", "map-frame", "map-full-wrap");
        fullWrap.style.position = Position.Relative;
        fullImage = MakeMapImage("FullImage");
        fullImage.AddToClassList("map-full");
        fullImage.style.flexGrow = 1;
        fullOverlay = IndustryUi.El("FullOverlay", "map-overlay");
        fullOverlay.pickingMode = PickingMode.Ignore;
        fullMarker = MakePlayerMark("FullMark");
        fullOverlay.Add(fullMarker);
        measureLayer = new MeasureLayer { name = "Measure" };
        measureLayer.AddToClassList("map-overlay");
        fullOverlay.Add(measureLayer);
        measureLabel = IndustryUi.Text("MeasureL", "", "measure-label");
        measureLabel.pickingMode = PickingMode.Ignore;
        IndustryUi.Show(measureLabel, false);
        fullOverlay.Add(measureLabel);
        worldCompass = IndustryUi.Text("Rose", "N", "map-compass");
        worldCompass.pickingMode = PickingMode.Ignore;
        fullOverlay.Add(worldCompass);
        fullWrap.Add(fullImage);
        fullWrap.Add(fullOverlay);

        var body = IndustryUi.El("Body", "map-body");
        body.Add(fullWrap);
        waypointPanel = IndustryUi.El("Wp", "map-side");
        waypointPanel.Add(IndustryUi.Text("WpT", UiLocale.T("map.waypoints"), "heading-3"));
        waypointSearch = new TextField { name = "WpSearch" };
        waypointSearch.AddToClassList("field");
        waypointSearch.RegisterValueChangedCallback(evt =>
        {
            waypointFilter = evt.newValue ?? "";
            RebuildWaypointList();
        });
        waypointPanel.Add(waypointSearch);
        waypointList = new ScrollView { name = "WpList" };
        waypointList.AddToClassList("scroll");
        waypointList.style.flexGrow = 1;
        waypointPanel.Add(waypointList);
        waypointPanel.Add(IndustryUi.Btn(UiLocale.T("map.marker_here"), () => BeginMarker(PlayerCell()), "btn-small", "btn-primary"));
        body.Add(waypointPanel);
        chrome.Add(body);

        var tools = IndustryUi.El("Tools", "map-tools");
        tools.Add(IndustryUi.Btn("−", () => ZoomAtCenter(1.22f), "btn-small"));
        zoomLabel = IndustryUi.Text("Zoom", "100%", "muted");
        tools.Add(zoomLabel);
        tools.Add(IndustryUi.Btn("+", () => ZoomAtCenter(0.82f), "btn-small"));
        tools.Add(IndustryUi.Btn(UiLocale.T("map.center"), () => CenterOnPlayer(0.22f), "btn-small", "btn-primary"));
        measureBtn = IndustryUi.Btn(UiLocale.T("map.measure"), ToggleMeasureTool, "btn-small");
        tools.Add(measureBtn);
        tools.Add(IndustryUi.Text("Hint", UiLocale.T("hint.measure_map") + "  ·  " + UiLocale.T("mouse.rmb") + " " + UiLocale.T("hint.marker_map"), "muted"));
        chrome.Add(tools);

        editPanel = IndustryUi.El("Edit", "map-edit");
        editPanel.Add(IndustryUi.Text("ET", UiLocale.T("map.marker_edit"), "heading-3"));
        editName = new TextField { name = "EditName" };
        editName.AddToClassList("field");
        editPanel.Add(editName);
        var colors = IndustryUi.El("Cols", "row");
        for (int i = 0; i < MapMarkerSystem.Palette.Length; i++)
        {
            Color c = MapMarkerSystem.Palette[i];
            var sw = IndustryUi.El("C", "map-swatch");
            sw.style.backgroundColor = c;
            sw.style.width = 18;
            sw.style.height = 18;
            sw.style.marginRight = 6;
            Color captured = c;
            sw.RegisterCallback<ClickEvent>(_ =>
            {
                if (editing != null && MapMarkerSystem.Instance != null)
                    MapMarkerSystem.Instance.SetColor(editing.id, captured);
            });
            colors.Add(sw);
        }
        editPanel.Add(colors);
        var editBtns = IndustryUi.El("EB", "row");
        teleportBtn = IndustryUi.Btn(UiLocale.T("map.teleport"), TeleportEditing, "btn-small", "btn-primary");
        editBtns.Add(teleportBtn);
        editBtns.Add(IndustryUi.Btn(UiLocale.T("map.marker_save"), SaveEdit, "btn-small", "btn-primary"));
        editBtns.Add(IndustryUi.Btn(UiLocale.T("map.marker_hide"), ToggleEditHidden, "btn-small"));
        editBtns.Add(IndustryUi.Btn(UiLocale.T("menu.delete"), () =>
        {
            if (editing != null)
                AskDelete(editing);
            IndustryUi.Show(editPanel, false);
        }, "btn-small", "btn-danger"));
        editPanel.Add(editBtns);
        IndustryUi.Show(editPanel, false);
        chrome.Add(editPanel);

        fullRoot.Add(chrome);
        fullWrap.RegisterCallback<WheelEvent>(OnFullWheel);
        fullImage.RegisterCallback<WheelEvent>(OnFullWheel);
        fullImage.RegisterCallback<PointerDownEvent>(OnFullDown);
        fullImage.RegisterCallback<PointerMoveEvent>(OnFullMove);
        fullImage.RegisterCallback<PointerUpEvent>(OnFullUp);
        fullImage.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            if (!IsOpen)
                return;
            IndustryUi.Show(tooltip, false);
        });
        IndustryUi.Show(fullRoot, false);
        root.Add(fullRoot);

        tooltip = IndustryUi.Text("Tip", "", "tooltip");
        tooltip.pickingMode = PickingMode.Ignore;
        IndustryUi.Show(tooltip, false);
        root.Add(tooltip);
    }

    static VisualElement MakePlayerMark(string name)
    {
        var mark = IndustryUi.El(name, "player-mark");
        mark.pickingMode = PickingMode.Ignore;
        mark.style.backgroundImage = new StyleBackground(MapIcons.PlayerArrow);
        mark.style.backgroundColor = Color.clear;
        return mark;
    }

    static Label MakeCompass(string name, string letter)
    {
        var label = IndustryUi.Text(name, letter, "compass-letter");
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    static void AddSwatch(VisualElement row, Color color, string name)
    {
        var item = IndustryUi.El("L", "row");
        item.style.marginRight = 14;
        var sw = IndustryUi.El("S", "map-swatch");
        sw.style.backgroundColor = color;
        item.Add(sw);
        item.Add(IndustryUi.Text("N", name, "muted"));
        row.Add(item);
    }

    void AddLayerChip(VisualElement row, string name, Color color, System.Func<bool> get, System.Action<bool> set)
    {
        Button chip = IndustryUi.Btn(name, null, "map-layer");
        var sw = IndustryUi.El("S", "map-swatch");
        sw.style.backgroundColor = color;
        chip.Insert(0, sw);
        void Sync()
        {
            IndustryUi.SetOn(chip, !get(), "is-off");
        }
        chip.clicked += () =>
        {
            set(!get());
            Sync();
            Rebuild();
        };
        Sync();
        row.Add(chip);
    }

    static MapView MakeMapImage(string name)
    {
        var view = new MapView { name = name };
        view.style.flexGrow = 1;
        view.style.overflow = Overflow.Hidden;
        return view;
    }

    sealed class MapView : VisualElement
    {
        Texture2D tex;
        Texture2D fog;
        Rect uv = new Rect(0f, 0f, 1f, 1f);
        float yaw;
        bool showGrid;
        int mapMinX;
        int mapMinZ;
        int mapW = 1;
        int mapH = 1;

        public MapView()
        {
            generateVisualContent += Paint;
            pickingMode = PickingMode.Position;
        }

        public bool ShowGrid
        {
            get => showGrid;
            set
            {
                if (showGrid == value)
                    return;
                showGrid = value;
                MarkDirtyRepaint();
            }
        }

        public void SetMapBounds(int minX, int minZ, int width, int height)
        {
            mapMinX = minX;
            mapMinZ = minZ;
            mapW = Mathf.Max(1, width);
            mapH = Mathf.Max(1, height);
        }

        public Texture2D MapTexture
        {
            get => tex;
            set
            {
                if (tex == value)
                    return;
                tex = value;
                MarkDirtyRepaint();
            }
        }

        public Texture2D FogTexture
        {
            get => fog;
            set
            {
                if (fog == value)
                    return;
                fog = value;
                MarkDirtyRepaint();
            }
        }

        public Rect ViewUv
        {
            get => uv;
            set
            {
                uv = value;
                MarkDirtyRepaint();
            }
        }

        public float ViewYaw
        {
            get => yaw;
            set
            {
                if (Mathf.Abs(yaw - value) < 0.05f)
                    return;
                yaw = value;
                MarkDirtyRepaint();
            }
        }

        void Paint(MeshGenerationContext ctx)
        {
            if (tex == null)
                return;
            Rect r = contentRect;
            if (r.width < 1f || r.height < 1f)
                return;

            float rad = -yaw * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float uc = uv.x + uv.width * 0.5f;
            float vc = uv.y + uv.height * 0.5f;
            float hw = uv.width * 0.5f;
            float hh = uv.height * 0.5f;
            DrawQuad(ctx, tex, r, uc, vc, hw, hh, cos, sin, Color.white);
            if (fog != null && MapSettings.HideUnexplored)
                DrawQuad(ctx, fog, r, uc, vc, hw, hh, cos, sin, Color.white);

            if (!showGrid)
                return;
            Painter2D p = ctx.painter2D;
            p.strokeColor = new Color(1f, 1f, 1f, 0.18f);
            p.lineWidth = 1f;
            const int step = 16;
            int x0 = mapMinX + Mathf.FloorToInt(uv.x * mapW);
            int x1 = mapMinX + Mathf.CeilToInt((uv.x + uv.width) * mapW);
            int z0 = mapMinZ + Mathf.FloorToInt(uv.y * mapH);
            int z1 = mapMinZ + Mathf.CeilToInt((uv.y + uv.height) * mapH);
            x0 -= x0 % step;
            z0 -= z0 % step;
            for (int x = x0; x <= x1; x += step)
            {
                float u = (x - mapMinX) / (float)mapW;
                Vector2 a = UvToPx(r, uv, yaw, u, uv.y);
                Vector2 b = UvToPx(r, uv, yaw, u, uv.y + uv.height);
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.Stroke();
            }
            for (int z = z0; z <= z1; z += step)
            {
                float v = (z - mapMinZ) / (float)mapH;
                Vector2 a = UvToPx(r, uv, yaw, uv.x, v);
                Vector2 b = UvToPx(r, uv, yaw, uv.x + uv.width, v);
                p.BeginPath();
                p.MoveTo(a);
                p.LineTo(b);
                p.Stroke();
            }
        }

        static Vector2 UvToPx(Rect r, Rect view, float yawDeg, float u, float v)
        {
            float cx = view.x + view.width * 0.5f;
            float cy = view.y + view.height * 0.5f;
            float ox = u - cx;
            float oy = v - cy;
            float rad = yawDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            float rx = ox * cos - oy * sin;
            float ry = ox * sin + oy * cos;
            float sx = 0.5f + rx / Mathf.Max(0.0001f, view.width);
            float sy = 0.5f - ry / Mathf.Max(0.0001f, view.height);
            return new Vector2(r.xMin + sx * r.width, r.yMin + sy * r.height);
        }

        static void DrawQuad(
            MeshGenerationContext ctx,
            Texture2D texture,
            Rect r,
            float uc,
            float vc,
            float hw,
            float hh,
            float cos,
            float sin,
            Color32 tint)
        {
            MeshWriteData mesh = ctx.Allocate(4, 6, texture);
            mesh.SetNextVertex(Vert(r.xMin, r.yMin, RotUv(uc, vc, -hw, hh, cos, sin), tint));
            mesh.SetNextVertex(Vert(r.xMax, r.yMin, RotUv(uc, vc, hw, hh, cos, sin), tint));
            mesh.SetNextVertex(Vert(r.xMax, r.yMax, RotUv(uc, vc, hw, -hh, cos, sin), tint));
            mesh.SetNextVertex(Vert(r.xMin, r.yMax, RotUv(uc, vc, -hw, -hh, cos, sin), tint));
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(1);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(2);
            mesh.SetNextIndex(3);
        }

        static Vector2 RotUv(float uc, float vc, float ox, float oy, float cos, float sin)
        {
            return new Vector2(uc + ox * cos - oy * sin, vc + ox * sin + oy * cos);
        }

        static Vertex Vert(float x, float y, Vector2 uv, Color32 tint)
        {
            return new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = tint, uv = uv };
        }
    }

    sealed class MeasureLayer : VisualElement
    {
        public Vector2 A;
        public Vector2 B;
        public bool On;

        public MeasureLayer()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext ctx)
        {
            if (!On)
                return;
            Painter2D p = ctx.painter2D;
            p.strokeColor = new Color(1f, 0.92f, 0.35f, 0.95f);
            p.lineWidth = 2.5f;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.MoveTo(A);
            p.LineTo(B);
            p.Stroke();
            p.fillColor = p.strokeColor;
            p.BeginPath();
            p.Arc(A, 4f, Angle.Degrees(0), Angle.Degrees(360));
            p.Fill();
            p.BeginPath();
            p.Arc(B, 4f, Angle.Degrees(0), Angle.Degrees(360));
            p.Fill();
        }
    }

    void CenterOnPlayer(float zoom)
    {
        Vector2 p = PlayerUv();
        zoom = Mathf.Clamp(zoom, 0.06f, 1f);
        viewUv = new Rect(p.x - zoom * 0.5f, p.y - zoom * 0.5f, zoom, zoom);
        ClampView();
        if (fullImage != null)
            fullImage.ViewUv = viewUv;
        RefreshZoomLabel();
    }

    void ZoomAtCenter(float factor)
    {
        if (fullImage == null)
            return;
        float w = Mathf.Max(1f, fullImage.resolvedStyle.width);
        float h = Mathf.Max(1f, fullImage.resolvedStyle.height);
        ZoomAt(fullImage, new Vector2(w * 0.5f, h * 0.5f), factor);
        RefreshZoomLabel();
    }

    void RefreshZoomLabel()
    {
        if (zoomLabel != null)
            zoomLabel.text = Mathf.RoundToInt(100f / Mathf.Max(0.06f, viewUv.width)) + "%";
    }

    void ApplyMiniSize()
    {
        if (miniFrame == null || miniClip == null)
            return;
        float size = MiniSize;
        miniFrame.style.width = size + 12f;
        miniFrame.style.height = size + 12f;
        miniClip.style.width = size;
        miniClip.style.height = size;
        ApplyMiniStyle();
    }

    public void ApplyMiniStyle()
    {
        if (miniFrame == null || miniClip == null)
            return;
        float size = MiniSize;
        float radius = MiniRound ? size * 0.5f : 2f;
        SetRadius(miniFrame, radius);
        SetRadius(miniClip, radius);
        miniFrame.style.overflow = Overflow.Hidden;
        miniClip.style.overflow = Overflow.Hidden;
        float a = MapSettings.MiniOpacity;
        miniFrame.style.backgroundColor = new Color(0.06f, 0.08f, 0.1f, a);
        bool show = MapSettings.MiniVisible && !IsOpen;
        IndustryUi.Show(miniHost, show);
        PlaceMiniHost();
    }

    void PlaceMiniHost()
    {
        if (miniHost == null)
            return;
        const float pad = 24f;
        miniHost.style.position = Position.Absolute;
        miniHost.style.left = StyleKeyword.Auto;
        miniHost.style.right = StyleKeyword.Auto;
        miniHost.style.top = StyleKeyword.Auto;
        miniHost.style.bottom = StyleKeyword.Auto;
        switch (MapSettings.MiniCorner)
        {
            case 0:
                miniHost.style.left = pad;
                miniHost.style.top = pad;
                break;
            case 2:
                miniHost.style.left = pad;
                miniHost.style.bottom = pad;
                break;
            case 3:
                miniHost.style.right = pad;
                miniHost.style.bottom = pad;
                break;
            default:
                miniHost.style.right = pad;
                miniHost.style.top = pad;
                break;
        }

        if (miniFrame == null)
            return;

        bool bottom = MapSettings.MiniCorner >= 2;
        if (miniClock != null)
            miniClock.RemoveFromHierarchy();
        if (miniInfo != null)
            miniInfo.RemoveFromHierarchy();
        miniFrame.RemoveFromHierarchy();
        if (bottom)
        {
            if (miniInfo != null)
                miniHost.Add(miniInfo);
            if (miniClock != null)
                miniHost.Add(miniClock);
            miniHost.Add(miniFrame);
        }
        else
        {
            miniHost.Add(miniFrame);
            if (miniClock != null)
                miniHost.Add(miniClock);
            if (miniInfo != null)
                miniHost.Add(miniInfo);
        }
    }

    static void SetRadius(VisualElement el, float radius)
    {
        el.style.borderTopLeftRadius = radius;
        el.style.borderTopRightRadius = radius;
        el.style.borderBottomLeftRadius = radius;
        el.style.borderBottomRightRadius = radius;
    }

    void UpdateMiniView()
    {
        if (miniImage == null || WorldBiomeMap.Instance == null || !WorldBiomeMap.Instance.IsReady)
            return;

        Vector2 center = PlayerUv();
        float z = Mathf.Clamp(MiniZoom, 0.06f, 1f);
        miniUv = new Rect(center.x - z * 0.5f, center.y - z * 0.5f, z, z);
        miniImage.ViewUv = miniUv;
        miniImage.ViewYaw = MiniFollow ? PlayerYaw() : 0f;
    }

    void OnMiniWheel(WheelEvent evt)
    {
        if (IsOpen)
            return;
        MiniZoom *= evt.delta.y > 0f ? 0.85f : 1.1764706f;
        evt.StopPropagation();
    }

    void OnFullWheel(WheelEvent evt)
    {
        if (!IsOpen)
            return;
        ZoomAt(fullImage, evt.localMousePosition, evt.delta.y > 0f ? 0.85f : 1.1764706f);
        RefreshZoomLabel();
        evt.StopPropagation();
    }

    void ToggleMeasureTool()
    {
        measureTool = !measureTool;
        IndustryUi.SetOn(measureBtn, measureTool, "is-selected");
        if (!measureTool)
        {
            measuring = false;
            measureOn = false;
            if (measureLayer != null)
            {
                measureLayer.On = false;
                measureLayer.MarkDirtyRepaint();
            }
            IndustryUi.Show(measureLabel, false);
        }
    }

    void OnFullDown(PointerDownEvent evt)
    {
        if (!IsOpen)
            return;

        if (evt.button == 1)
        {
            rmbHold = true;
            rmbDragged = false;
            lastMouse = (Vector2)evt.position;
            rmbHasCell = LocalToCell(fullImage, viewUv, evt.localPosition, 0f, out rmbCell);
            fullImage.CapturePointer(evt.pointerId);
            evt.StopPropagation();
            return;
        }

        if (evt.button != 0)
            return;

        bool wantMeasure = measureTool || ShiftHeld();
        if (wantMeasure && LocalToCell(fullImage, viewUv, evt.localPosition, 0f, out Vector2Int start))
        {
            measuring = true;
            measureOn = true;
            measureA = start;
            measureB = start;
            fullImage.CapturePointer(evt.pointerId);
            UpdateMeasureVisual();
            evt.StopPropagation();
            return;
        }

        dragging = true;
        lastMouse = (Vector2)evt.position;
        fullImage.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    void OnFullMove(PointerMoveEvent evt)
    {
        if (IsOpen)
            UpdateTooltip(fullImage, viewUv, evt.localPosition, evt.position, 0f);

        if (measuring)
        {
            if (LocalToCell(fullImage, viewUv, evt.localPosition, 0f, out Vector2Int cell))
                measureB = cell;
            UpdateMeasureVisual();
            evt.StopPropagation();
            return;
        }

        if (rmbHold)
        {
            Vector2 deltaR = (Vector2)evt.position - lastMouse;
            if (deltaR.sqrMagnitude > 36f)
                rmbDragged = true;
            if (rmbDragged)
            {
                lastMouse = (Vector2)evt.position;
                PanView(deltaR);
            }
            evt.StopPropagation();
            return;
        }

        if (!dragging)
            return;

        Vector2 delta = (Vector2)evt.position - lastMouse;
        lastMouse = (Vector2)evt.position;
        PanView(delta);
        evt.StopPropagation();
    }

    void PanView(Vector2 delta)
    {
        if (fullImage == null)
            return;
        float w = Mathf.Max(1f, fullImage.resolvedStyle.width);
        float h = Mathf.Max(1f, fullImage.resolvedStyle.height);
        viewUv.x -= delta.x / w * viewUv.width;
        viewUv.y += delta.y / h * viewUv.height;
        ClampView();
    }

    void OnFullUp(PointerUpEvent evt)
    {
        if (measuring)
        {
            measuring = false;
            if (fullImage.HasPointerCapture(evt.pointerId))
                fullImage.ReleasePointer(evt.pointerId);
            return;
        }

        if (rmbHold)
        {
            rmbHold = false;
            if (fullImage.HasPointerCapture(evt.pointerId))
                fullImage.ReleasePointer(evt.pointerId);
            if (!rmbDragged && rmbHasCell)
                BeginMarker(rmbCell);
            return;
        }

        if (!dragging)
            return;
        dragging = false;
        if (fullImage.HasPointerCapture(evt.pointerId))
            fullImage.ReleasePointer(evt.pointerId);
    }

    void BeginMarker(Vector2Int cell)
    {
        UiModal.Prompt(
            UiLocale.T("map.marker_title"),
            UiLocale.T("map.marker_body") + "  (" + cell.x + ", " + cell.y + ")",
            UiLocale.T("map.marker_place"),
            UiLocale.T("map.marker_default"),
            name =>
            {
                if (MapMarkerSystem.Instance != null)
                    MapMarkerSystem.Instance.Add(cell, name);
                RefreshPins();
            });
    }

    void AskDelete(MapMarkerSave marker)
    {
        if (marker == null)
            return;
        UiModal.Confirm(
            UiLocale.T("map.marker"),
            UiLocale.T("map.marker_delete", marker.label),
            UiLocale.T("menu.delete"),
            () =>
            {
                if (MapMarkerSystem.Instance != null)
                    MapMarkerSystem.Instance.Remove(marker.id);
                RefreshPins();
            });
    }

    void RefreshPins()
    {
        ClearPins(miniOverlay, miniPins);
        ClearPins(fullOverlay, fullPins);
        if (MapMarkerSystem.Instance == null)
            return;

        List<MapMarkerSave> markers = MapMarkerSystem.Instance.Markers;
        bool miniWp = MapSettings.MiniWaypoints;
        for (int i = 0; i < markers.Count; i++)
        {
            MapMarkerSave marker = markers[i];
            if (marker == null)
                continue;
            if (miniWp && marker.hidden == 0)
                miniPins.Add(MakePin(miniOverlay, marker, false));
            fullPins.Add(MakePin(fullOverlay, marker, true));
        }
        LayoutPins();
        if (IsOpen)
            RebuildWaypointList();
    }

    VisualElement MakePin(VisualElement host, MapMarkerSave marker, bool named)
    {
        var pin = IndustryUi.El("Pin", "map-pin");
        pin.style.backgroundColor = new Color(marker.r, marker.g, marker.b, 1f);
        pin.userData = marker;
        if (named)
        {
            pin.pickingMode = PickingMode.Position;
            var label = IndustryUi.Text("N", marker.label, "map-pin-name");
            label.pickingMode = PickingMode.Ignore;
            pin.Add(label);
            MapMarkerSave captured = marker;
            pin.RegisterCallback<PointerDownEvent>(evt =>
            {
                evt.StopImmediatePropagation();
                if (evt.button == 1)
                    AskDelete(captured);
                else if (evt.button == 0)
                    OpenEdit(captured);
            });
        }
        else
        {
            pin.pickingMode = PickingMode.Ignore;
            var dist = IndustryUi.Text("D", "", "map-pin-dist");
            dist.pickingMode = PickingMode.Ignore;
            pin.Add(dist);
        }
        host.Add(pin);
        return pin;
    }

    static void ClearPins(VisualElement host, List<VisualElement> pins)
    {
        for (int i = 0; i < pins.Count; i++)
        {
            if (pins[i] != null && pins[i].parent == host)
                pins[i].RemoveFromHierarchy();
        }
        pins.Clear();
    }

    void LayoutPins()
    {
        LayoutPinSet(miniPins, miniOverlay, miniUv, MiniFollow ? PlayerYaw() : 0f, true);
        if (IsOpen)
            LayoutPinSet(fullPins, fullOverlay, viewUv, 0f, false);
    }

    void LayoutPinSet(List<VisualElement> pins, VisualElement overlay, Rect uv, float yaw, bool clampEdge)
    {
        if (overlay == null)
            return;
        float w = overlay.resolvedStyle.width;
        float h = overlay.resolvedStyle.height;
        if (w < 1f || h < 1f)
            return;

        for (int i = 0; i < pins.Count; i++)
        {
            VisualElement pin = pins[i];
            MapMarkerSave marker = pin != null ? pin.userData as MapMarkerSave : null;
            if (pin == null || marker == null)
                continue;

            Vector2 uvPos = CellUv(new Vector2Int(marker.x, marker.z));
            Vector2 local = UvToLocal(overlay, uv, uvPos, yaw);
            bool inside = InView(local, w, h, clampEdge && MiniRound);
            if (clampEdge && !inside)
                local = ClampToMini(local, w, h);
            else if (!clampEdge && !inside)
            {
                IndustryUi.Show(pin, false);
                continue;
            }

            IndustryUi.Show(pin, true);
            pin.style.left = local.x - 5f;
            pin.style.top = local.y - 5f;
            Label dist = pin.Q<Label>("D");
            if (dist != null)
            {
                bool edge = clampEdge && !InView(UvToLocal(overlay, uv, uvPos, yaw), w, h, MiniRound);
                if (edge)
                {
                    Vector2Int pc = PlayerCell();
                    int cells = Mathf.RoundToInt(Vector2.Distance(new Vector2(marker.x, marker.z), new Vector2(pc.x, pc.y)));
                    dist.text = cells.ToString();
                    IndustryUi.Show(dist, true);
                }
                else
                    IndustryUi.Show(dist, false);
            }
        }
    }

    bool InView(Vector2 p, float w, float h, bool round)
    {
        if (round)
        {
            Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
            return (p - c).sqrMagnitude <= (Mathf.Min(w, h) * 0.5f - 2f) * (Mathf.Min(w, h) * 0.5f - 2f);
        }
        return p.x >= 0f && p.x <= w && p.y >= 0f && p.y <= h;
    }

    Vector2 ClampToMini(Vector2 p, float w, float h)
    {
        const float pad = 8f;
        Vector2 c = new Vector2(w * 0.5f, h * 0.5f);
        if (MiniRound)
        {
            Vector2 d = p - c;
            float max = Mathf.Min(w, h) * 0.5f - pad;
            if (d.sqrMagnitude > 0.0001f && d.magnitude > max)
                return c + d.normalized * max;
            return p;
        }
        return new Vector2(Mathf.Clamp(p.x, pad, w - pad), Mathf.Clamp(p.y, pad, h - pad));
    }

    void UpdateMeasureVisual()
    {
        if (measureLayer == null || fullOverlay == null)
            return;
        if (!measureOn)
        {
            measureLayer.On = false;
            measureLayer.MarkDirtyRepaint();
            IndustryUi.Show(measureLabel, false);
            return;
        }

        Vector2 a = UvToLocal(fullOverlay, viewUv, CellUv(measureA), 0f);
        Vector2 b = UvToLocal(fullOverlay, viewUv, CellUv(measureB), 0f);
        measureLayer.A = a;
        measureLayer.B = b;
        measureLayer.On = true;
        measureLayer.MarkDirtyRepaint();

        int dx = Mathf.Abs(measureB.x - measureA.x);
        int dz = Mathf.Abs(measureB.y - measureA.y);
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        measureLabel.text = UiLocale.T("map.cells", dist.ToString("0.#")) + "  " + UiLocale.T("map.delta", dx, dz);
        measureLabel.style.left = (a.x + b.x) * 0.5f + 8f;
        measureLabel.style.top = (a.y + b.y) * 0.5f - 10f;
        IndustryUi.Show(measureLabel, true);
    }

    void ZoomAt(VisualElement image, Vector2 local, float factor)
    {
        if (image == null)
            return;
        float w = Mathf.Max(1f, image.resolvedStyle.width);
        float h = Mathf.Max(1f, image.resolvedStyle.height);
        float nx = Mathf.Clamp01(local.x / w);
        float ny = 1f - Mathf.Clamp01(local.y / h);
        float worldU = viewUv.x + nx * viewUv.width;
        float worldV = viewUv.y + ny * viewUv.height;
        float zoom = Mathf.Clamp(viewUv.width * factor, 0.06f, 1f);
        viewUv.width = zoom;
        viewUv.height = zoom;
        viewUv.x = worldU - nx * zoom;
        viewUv.y = worldV - ny * zoom;
        ClampView();
    }

    void ClampView()
    {
        viewUv.width = Mathf.Clamp(viewUv.width, 0.06f, 1f);
        viewUv.height = viewUv.width;
        viewUv.x = Mathf.Clamp(viewUv.x, 0f, 1f - viewUv.width);
        viewUv.y = Mathf.Clamp(viewUv.y, 0f, 1f - viewUv.height);
    }

    Vector2 PlayerUv()
    {
        return CellUv(PlayerCell());
    }

    Vector2Int PlayerCell()
    {
        PlayerBuilder builder = GameManager.Instance != null
            ? GameManager.Instance.playerBuilder
            : FindFirstObjectByType<PlayerBuilder>();
        if (builder == null)
            return Vector2Int.zero;
        if (GridSystem.Instance != null)
            return GridSystem.Instance.WorldToCell(builder.transform.position);
        return new Vector2Int(Mathf.RoundToInt(builder.transform.position.x), Mathf.RoundToInt(builder.transform.position.z));
    }

    float PlayerYaw()
    {
        PlayerBuilder builder = GameManager.Instance != null
            ? GameManager.Instance.playerBuilder
            : FindFirstObjectByType<PlayerBuilder>();
        return builder != null ? builder.transform.eulerAngles.y : 0f;
    }

    static Vector2 CellUv(Vector2Int cell)
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady)
            return new Vector2(0.5f, 0.5f);
        return new Vector2(
            (cell.x - map.MapMinX + 0.5f) / map.MapWidth,
            (cell.y - map.MapMinZ + 0.5f) / map.MapHeight);
    }

    void UpdateTooltip(VisualElement image, Rect uv, Vector2 local, Vector2 panelPos, float yaw)
    {
        if (tooltip == null || image == null)
            return;
        if (image == fullImage && !IsOpen)
        {
            IndustryUi.Show(tooltip, false);
            return;
        }
        if (!LocalToCell(image, uv, local, yaw, out Vector2Int cell))
        {
            IndustryUi.Show(tooltip, false);
            return;
        }

        if (coordLabel != null)
            coordLabel.text = cell.x + ", " + cell.y;

        string label = LabelAt(cell);
        if (MapExploration.Instance != null && !MapExploration.Instance.IsExplored(cell))
            label = UiLocale.T("map.unexplored");
        if (string.IsNullOrEmpty(label))
        {
            IndustryUi.Show(tooltip, false);
            return;
        }

        tooltip.text = label;
        tooltip.style.left = panelPos.x + 18f;
        tooltip.style.top = panelPos.y - 18f;
        IndustryUi.Show(tooltip, true);
    }

    static bool LocalToCell(VisualElement image, Rect uv, Vector2 local, float yaw, out Vector2Int cell)
    {
        cell = default;
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady || image == null)
            return false;
        float w = image.resolvedStyle.width;
        float h = image.resolvedStyle.height;
        if (w < 1f || h < 1f)
            return false;

        float sx = local.x / w;
        float sy = local.y / h;
        float rx = (sx - 0.5f) * uv.width;
        float ry = (0.5f - sy) * uv.height;
        float rad = -yaw * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        float ox = rx * cos - ry * sin;
        float oy = rx * sin + ry * cos;
        float u = uv.x + uv.width * 0.5f + ox;
        float v = uv.y + uv.height * 0.5f + oy;
        int x = map.MapMinX + Mathf.FloorToInt(u * map.MapWidth);
        int z = map.MapMinZ + Mathf.FloorToInt(v * map.MapHeight);
        cell = new Vector2Int(x, z);
        return true;
    }

    static Vector2 UvToLocal(VisualElement image, Rect uv, Vector2 worldUv, float yaw)
    {
        float w = image.resolvedStyle.width;
        float h = image.resolvedStyle.height;
        float cx = uv.x + uv.width * 0.5f;
        float cy = uv.y + uv.height * 0.5f;
        float ox = worldUv.x - cx;
        float oy = worldUv.y - cy;
        float rad = yaw * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        float rx = ox * cos - oy * sin;
        float ry = ox * sin + oy * cos;
        float sx = 0.5f + rx / Mathf.Max(0.0001f, uv.width);
        float sy = 0.5f - ry / Mathf.Max(0.0001f, uv.height);
        return new Vector2(sx * w, sy * h);
    }

    static string LabelAt(Vector2Int cell)
    {
        if (MapMarkerSystem.Instance != null)
        {
            List<MapMarkerSave> markers = MapMarkerSystem.Instance.Markers;
            for (int i = 0; i < markers.Count; i++)
            {
                MapMarkerSave marker = markers[i];
                if (marker != null && marker.x == cell.x && marker.z == cell.y)
                    return marker.label;
            }
        }

        if (WorldResourceScatterer.Instance != null
            && WorldResourceScatterer.Instance.TryGetVeinLabel(cell, out string vein))
            return vein;

        GameObject obj = GridOccupancy.GetAt(cell);
        if (obj == null)
            return null;

        Conveyor belt = obj.GetComponent<Conveyor>();
        if (belt != null)
            return "Конвейер";

        BuildingBase building = obj.GetComponent<BuildingBase>();
        if (building != null && building.data != null && !string.IsNullOrEmpty(building.data.displayName))
            return building.data.displayName;
        return obj.name;
    }

    void UpdatePlayerMark(VisualElement marker, VisualElement overlay, Rect uv, float yaw, bool mini)
    {
        if (marker == null || overlay == null || WorldBiomeMap.Instance == null || !WorldBiomeMap.Instance.IsReady)
            return;

        float w = overlay.resolvedStyle.width;
        float h = overlay.resolvedStyle.height;
        if (w < 1f || h < 1f)
            return;

        Vector2 local = UvToLocal(overlay, uv, PlayerUv(), yaw);
        bool inside = InView(local, w, h, mini && MiniRound);
        if (!inside && !mini)
        {
            IndustryUi.Show(marker, false);
            return;
        }

        IndustryUi.Show(marker, true);
        if (mini)
            local = new Vector2(w * 0.5f, h * 0.5f);
        marker.style.left = local.x - 11f;
        marker.style.top = local.y - 11f;
        float face = PlayerYaw();
        marker.style.rotate = new Rotate(Angle.Degrees(mini && MiniFollow ? 0f : face));
    }

    void UpdateMiniInfo()
    {
        if (miniInfo == null)
            return;
        bool show = MapSettings.MiniCoords || MapSettings.MiniBiome;
        IndustryUi.Show(miniInfo, show && MapSettings.MiniVisible && !IsOpen);
        if (!show)
            return;

        Vector2Int cell = PlayerCell();
        string text = "";
        if (MapSettings.MiniCoords)
        {
            float y = 0f;
            PlayerBuilder builder = GameManager.Instance != null ? GameManager.Instance.playerBuilder : null;
            if (builder != null)
                y = builder.transform.position.y;
            text = cell.x + ", " + y.ToString("0") + ", " + cell.y;
        }
        if (MapSettings.MiniBiome && WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsReady)
        {
            string biome = BiomeName(WorldBiomeMap.Instance.Get(cell));
            text = string.IsNullOrEmpty(text) ? biome : text + "  ·  " + biome;
        }
        miniInfo.text = text;
    }

    void UpdateMiniClock()
    {
        if (miniClock == null)
            return;
        bool show = GameSettings.ClockVisible && MapSettings.MiniVisible && !IsOpen;
        IndustryUi.Show(miniClock, show);
        if (!show)
            return;
        miniClock.text = DayNight.FormatClock(
            DayNight.Hour,
            DayNight.Day,
            GameSettings.ClockParts,
            GameSettings.ClockShowDay);
    }

    void UpdateCompass()
    {
        if (compass == null || miniOverlay == null)
            return;
        bool show = MapSettings.MiniCompass && MapSettings.MiniVisible && !IsOpen;
        float w = miniOverlay.resolvedStyle.width;
        float h = miniOverlay.resolvedStyle.height;
        if (w < 1f || h < 1f)
        {
            for (int i = 0; i < compass.Length; i++)
                IndustryUi.Show(compass[i], false);
            return;
        }

        Vector2Int pc = PlayerCell();
        Vector2Int[] dirs =
        {
            new Vector2Int(0, 16),
            new Vector2Int(16, 0),
            new Vector2Int(0, -16),
            new Vector2Int(-16, 0)
        };
        float yaw = MiniFollow ? PlayerYaw() : 0f;
        Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
        float radius = Mathf.Min(w, h) * 0.5f - 12f;
        for (int i = 0; i < compass.Length; i++)
        {
            IndustryUi.Show(compass[i], show);
            if (!show)
                continue;
            Vector2 local = UvToLocal(miniOverlay, miniUv, CellUv(pc + dirs[i]), yaw);
            Vector2 dir = local - center;
            if (dir.sqrMagnitude < 0.001f)
                dir = Vector2.up * -1f;
            dir.Normalize();
            if (MiniRound)
                local = center + dir * radius;
            else
            {
                local = center + dir * Mathf.Min(w, h);
                local.x = Mathf.Clamp(local.x, 8f, w - 16f);
                local.y = Mathf.Clamp(local.y, 4f, h - 16f);
            }
            compass[i].style.left = local.x - 6f;
            compass[i].style.top = local.y - 8f;
        }

        if (worldCompass != null)
        {
            worldCompass.style.left = 12f;
            worldCompass.style.top = 8f;
        }
    }

    void PanWithKeys()
    {
        if (dragging || measuring || KeybindStore.BlocksGameplayInput)
            return;
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;
        Vector2 pan = Vector2.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)
            pan.y += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)
            pan.y -= 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)
            pan.x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed)
            pan.x += 1f;
        if (pan.sqrMagnitude < 0.01f)
            return;
        float speed = viewUv.width * Time.unscaledDeltaTime * 0.7f;
        viewUv.x += pan.x * speed;
        viewUv.y += pan.y * speed;
        ClampView();
    }

    void RebuildWaypointList()
    {
        if (waypointList == null || MapMarkerSystem.Instance == null)
            return;
        waypointList.Clear();
        string filter = (waypointFilter ?? "").Trim();
        List<MapMarkerSave> markers = MapMarkerSystem.Instance.Markers;
        Vector2Int pc = PlayerCell();
        for (int i = 0; i < markers.Count; i++)
        {
            MapMarkerSave marker = markers[i];
            if (marker == null)
                continue;
            if (!string.IsNullOrEmpty(filter) && (marker.label == null || marker.label.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0))
                continue;
            int dist = Mathf.RoundToInt(Vector2.Distance(new Vector2(marker.x, marker.z), new Vector2(pc.x, pc.y)));
            MapMarkerSave captured = marker;
            var row = IndustryUi.El("Wp", "map-wp-row");
            var sw = IndustryUi.El("S", "map-swatch");
            sw.style.backgroundColor = new Color(marker.r, marker.g, marker.b);
            row.Add(sw);
            var col = IndustryUi.El("C", "col", "grow");
            col.Add(IndustryUi.Text("N", marker.label + (marker.hidden != 0 ? "  ·  " + UiLocale.T("settings.off") : ""), "body-text"));
            col.Add(IndustryUi.Text("D", marker.x + ", " + marker.z + "  ·  " + UiLocale.T("map.cells", dist), "muted"));
            row.Add(col);
            if (MapSettings.AllowTeleport)
            {
                MapMarkerSave tp = captured;
                Button tpBtn = IndustryUi.Btn(UiLocale.T("map.teleport"), () => TeleportTo(tp), "btn-small");
                tpBtn.RegisterCallback<ClickEvent>(evt => evt.StopImmediatePropagation());
                row.Add(tpBtn);
            }
            row.RegisterCallback<ClickEvent>(_ =>
            {
                CenterOnCell(new Vector2Int(captured.x, captured.z));
                OpenEdit(captured);
            });
            waypointList.Add(row);
        }
    }

    void CenterOnCell(Vector2Int cell)
    {
        Vector2 p = CellUv(cell);
        float zoom = viewUv.width;
        viewUv = new Rect(p.x - zoom * 0.5f, p.y - zoom * 0.5f, zoom, zoom);
        ClampView();
        if (fullImage != null)
            fullImage.ViewUv = viewUv;
    }

    void UpdateFogOverlay()
    {
        MapExploration explore = MapExploration.Instance;
        if (explore == null)
            return;
        Texture2D fog = MapSettings.HideUnexplored ? explore.FogTexture : null;
        if (miniImage != null)
            miniImage.FogTexture = fog;
        if (fullImage != null)
            fullImage.FogTexture = fog;
        if (explore.Version == lastFogVersion)
            return;
        lastFogVersion = explore.Version;
        if (miniImage != null)
            miniImage.MarkDirtyRepaint();
        if (fullImage != null)
            fullImage.MarkDirtyRepaint();
    }

    void OpenEdit(MapMarkerSave marker)
    {
        editing = marker;
        if (editPanel == null || marker == null)
            return;
        if (editName != null)
            editName.value = marker.label;
        IndustryUi.Show(teleportBtn, MapSettings.AllowTeleport);
        if (teleportBtn != null)
            teleportBtn.SetEnabled(MapSettings.AllowTeleport);
        IndustryUi.Show(editPanel, true);
    }

    void TeleportEditing()
    {
        if (editing == null)
            return;
        TeleportTo(editing);
    }

    void TeleportTo(MapMarkerSave marker)
    {
        if (marker == null || !MapSettings.AllowTeleport)
            return;
        UiModal.Confirm(
            UiLocale.T("map.teleport"),
            UiLocale.T("map.teleport_body", marker.label, marker.x, marker.z),
            UiLocale.T("map.teleport"),
            () =>
            {
                PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
                if (player == null)
                    return;
                player.TeleportToCell(new Vector2Int(marker.x, marker.z));
                SetOpen(false);
            });
    }

    void SaveEdit()
    {
        if (editing == null || MapMarkerSystem.Instance == null)
            return;
        MapMarkerSystem.Instance.Rename(editing.id, editName != null ? editName.value : editing.label);
        IndustryUi.Show(editPanel, false);
        editing = null;
        RefreshPins();
    }

    void ToggleEditHidden()
    {
        if (editing == null || MapMarkerSystem.Instance == null)
            return;
        MapMarkerSystem.Instance.SetHidden(editing.id, editing.hidden == 0);
        RefreshPins();
    }

    static string BiomeName(WorldBiome biome)
    {
        switch (biome)
        {
            case WorldBiome.Forest:
            case WorldBiome.Woodland:
                return UiLocale.T("map.forest");
            case WorldBiome.MountainPeak:
            case WorldBiome.MountainSlope:
                return UiLocale.T("map.mountain");
            case WorldBiome.Lake:
            case WorldBiome.Ocean:
            case WorldBiome.Beach:
                return UiLocale.T("map.water");
            default:
                return UiLocale.T("map.field");
        }
    }

    static bool ShiftHeld()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
    }
}
