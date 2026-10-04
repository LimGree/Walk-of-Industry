using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Image = UnityEngine.UIElements.Image;

/// <summary>
/// Редактор таблички (E по декорации «Табличка», [[Decorations]]). Слева — живое превью (своя камера
/// снимает копию таблички в текстуру: клик выбирает элемент, перетаскивание двигает, колесо — размер,
/// Shift+колесо — поворот) и настройки доски: стойка, размер, рамка, цвета, двусторонняя, подсветка, шаблоны.
/// Справа — элементы (текст, символ, иконка, плашка) и свойства выбранного. Правки сразу видны
/// на табличке в мире и уходят в сейв вместе с ней.
/// </summary>
public class SignEditorUI : MonoBehaviour
{
    public static SignEditorUI Instance { get; private set; }
    public bool IsOpen { get; private set; }

    const int SortOrder = 89;
    const string StackId = "sign";
    const int TexW = 1024;
    const int TexH = 640;
    /// <summary>Слой превью, как у превью персонажа (AvatarPreview): камера видит только его.</summary>
    const int PreviewLayer = 29;
    static readonly Vector3 RigPosition = new Vector3(0f, -4000f, 0f);

    Decoration target;
    SignData work;
    SignData original;
    int selected = -1;
    bool dirty;

    VisualElement overlay;
    VisualElement previewBox;
    Image previewImage;
    VisualElement selectBox;
    VisualElement boardHost;
    VisualElement listHost;
    VisualElement propsHost;
    Label listTitle;
    VisualElement rowX, rowY, rowSize, rowRot, rowW, rowH;

    GameObject rig;
    Transform rigVisual;
    Camera cam;
    Light camLight;
    RenderTexture rt;
    SignView view;
    float viewW = 1f;
    float viewH = 1f;
    readonly List<Light> sunLights = new List<Light>();
    readonly List<Light> muted = new List<Light>();
    AmbientMode savedMode;
    Color savedAmbient;
    float savedIntensity;
    bool savedFog;
    bool lit;

    bool dragging;
    Vector2 dragOffset;

    struct IconChoice
    {
        public string id;
        public Sprite sprite;
        public string title;
    }

    static List<IconChoice> icons;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (overlay != null)
            return;
        Build();
        IndustryUi.Show(overlay, false);
    }

    void OnEnable()
    {
        Camera.onPreCull += OnCamPreCull;
        Camera.onPostRender += OnCamPostRender;
    }

    void OnDisable()
    {
        Camera.onPreCull -= OnCamPreCull;
        Camera.onPostRender -= OnCamPostRender;
        RestoreLighting();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
        }

        if (rig != null)
            Destroy(rig);
    }

    // ---------- Открыть / закрыть ----------

    public static void OpenFor(Decoration sign)
    {
        if (Instance == null || sign == null || !sign.IsSign)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        Instance.Open(sign);
    }

    void Open(Decoration sign)
    {
        if (overlay == null)
            Build();
        target = sign;
        original = sign.Sign;
        work = original.Clone();
        selected = work.items.Count > 0 ? 0 : -1;
        IndustryUi.SetHeader(overlay, sign.data != null ? sign.data.Title : UiLocale.T("sign.title"), sign.data != null ? sign.data.icon : null);
        IsOpen = true;
        UiStack.Opened(StackId, Close, SortOrder);
        IndustryUi.Show(overlay, true);
        UiAudio.PlayOpen();
        EnsurePreview();
        RebuildAll();
        dirty = true;
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        dragging = false;
        if (dirty && target != null && work != null)
            target.SetSign(work);
        dirty = false;
        overlay.panel?.focusController?.focusedElement?.Blur();
        UiStack.Closed(StackId);
        IndustryUi.Show(overlay, false);
        if (cam != null)
            cam.enabled = false;
        if (rig != null)
            rig.SetActive(false);
        target = null;
        KeybindStore.SuppressGameplay();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    void LateUpdate()
    {
        if (!IsOpen)
            return;
        if (target == null || !target.IsPlaced)
        {
            Close();
            return;
        }

        HandleKeys();
        if (dirty)
        {
            dirty = false;
            target.SetSign(work);
            RebuildPreview();
        }

        UpdateSelectBox();
    }

    void MarkDirty()
    {
        dirty = true;
    }

    // ---------- Каркас окна ----------

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, SortOrder);
        overlay = IndustryUi.OverlayPanel(UiLocale.T("sign.title"), null, Close);
        VisualElement panel = IndustryUi.PanelOf(overlay);
        panel?.AddToClassList("sign-win");
        IndustryUi.WindowSubtitle(overlay, UiLocale.T("sign.sub"));
        IndustryUi.WindowHints(overlay,
            (UiLocale.T("bag.lmb"), UiLocale.T("sign.hint_drag")),
            (UiLocale.T("sign.key_wheel"), UiLocale.T("sign.hint_wheel")),
            ("Shift+" + UiLocale.T("sign.key_wheel"), UiLocale.T("sign.hint_rot")),
            ("Del", UiLocale.T("sign.hint_del")),
            ("Esc", UiLocale.T("win.close")));

        VisualElement host = overlay.Q("Body") ?? panel;
        var body = IndustryUi.El("SignBody", "sign-body");

        var left = IndustryUi.El("Left", "sign-left");
        previewBox = IndustryUi.El("Preview", "sign-preview");
        previewImage = new Image { name = "PreviewImage", scaleMode = ScaleMode.ScaleToFit };
        previewImage.AddToClassList("sign-preview-img");
        previewImage.RegisterCallback<PointerDownEvent>(OnPreviewDown);
        previewImage.RegisterCallback<PointerMoveEvent>(OnPreviewMove);
        previewImage.RegisterCallback<PointerUpEvent>(OnPreviewUp);
        previewImage.RegisterCallback<PointerCaptureOutEvent>(_ => dragging = false);
        previewImage.RegisterCallback<WheelEvent>(OnPreviewWheel);
        previewBox.Add(previewImage);
        selectBox = IndustryUi.El("Select", "sign-select");
        selectBox.pickingMode = PickingMode.Ignore;
        previewBox.Add(selectBox);
        left.Add(previewBox);
        left.Add(IndustryUi.Text("PreviewHint", UiLocale.T("sign.preview_hint"), "sign-preview-hint"));
        ScrollView leftScroll = IndustryUi.Scroll("BoardScroll");
        boardHost = IndustryUi.El("Board", "col");
        leftScroll.Add(boardHost);
        left.Add(leftScroll);
        body.Add(left);

        var right = IndustryUi.El("Right", "sign-right");
        var head = IndustryUi.El("ListHead", "sign-list-head");
        listTitle = IndustryUi.Text("T", "", "set-group-title");
        head.Add(listTitle);
        head.Add(IndustryUi.El("Line", "set-group-line"));
        right.Add(head);
        var add = IndustryUi.El("Add", "sign-add");
        add.Add(IndustryUi.Btn(UiLocale.T("sign.add_text"), () => AddElement(SignKind.Text), "btn-small"));
        add.Add(IndustryUi.Btn(UiLocale.T("sign.add_symbol"), () => AddElement(SignKind.Symbol), "btn-small"));
        add.Add(IndustryUi.Btn(UiLocale.T("sign.add_icon"), () => AddElement(SignKind.Icon), "btn-small"));
        add.Add(IndustryUi.Btn(UiLocale.T("sign.add_plate"), () => AddElement(SignKind.Plate), "btn-small"));
        right.Add(add);
        ScrollView listScroll = IndustryUi.Scroll("ListScroll");
        listScroll.AddToClassList("sign-list");
        listHost = IndustryUi.El("List", "col");
        listScroll.Add(listHost);
        right.Add(listScroll);
        right.Add(IndustryUi.Section(UiLocale.T("sign.sec_props")));
        ScrollView propsScroll = IndustryUi.Scroll("PropsScroll");
        propsHost = IndustryUi.El("Props", "col");
        propsScroll.Add(propsHost);
        right.Add(propsScroll);
        body.Add(right);

        host.Add(body);
        overlay.pickingMode = PickingMode.Position;
        root.Add(overlay);
    }

    void RebuildAll()
    {
        RebuildBoardPanel();
        RebuildList();
        RebuildProps();
    }

    // ---------- Доска ----------

    void RebuildBoardPanel()
    {
        boardHost.Clear();
        boardHost.Add(IndustryUi.Section(UiLocale.T("sign.sec_board")));
        boardHost.Add(SettingsControls.ChipRow("sign.mount",
            Chip("sign.mount_posts", () => work.mount == 0, () => SetMount(0)),
            Chip("sign.mount_pole", () => work.mount == 1, () => SetMount(1)),
            Chip("sign.mount_hang", () => work.mount == 2, () => SetMount(2)),
            Chip("sign.mount_plaque", () => work.mount == 3, () => SetMount(3)),
            Chip("sign.mount_stand", () => work.mount == 4, () => SetMount(4))));
        boardHost.Add(SettingsControls.ChipRow("sign.size",
            ("S", () => work.size == 0, () => { work.size = 0; MarkDirty(); }),
            ("M", () => work.size == 1, () => { work.size = 1; MarkDirty(); }),
            ("L", () => work.size == 2, () => { work.size = 2; MarkDirty(); })));
        boardHost.Add(SettingsControls.ChipRow("sign.frame",
            Chip("sign.frame_none", () => work.frame == 0, () => { work.frame = 0; MarkDirty(); }),
            Chip("sign.frame_thin", () => work.frame == 1, () => { work.frame = 1; MarkDirty(); }),
            Chip("sign.frame_wide", () => work.frame == 2, () => { work.frame = 2; MarkDirty(); })));
        boardHost.Add(SwatchRow("sign.color_board", () => work.board, v => work.board = v));
        boardHost.Add(SwatchRow("sign.color_frame", () => work.frameColor, v => work.frameColor = v));
        boardHost.Add(SwatchRow("sign.color_post", () => work.postColor, v => work.postColor = v));
        boardHost.Add(SettingsControls.Describe(
            SettingsControls.Toggle("sign.two_sided", () => work.twoSided, v => { work.twoSided = v; MarkDirty(); }), "sign.two_sided_desc"));
        boardHost.Add(SettingsControls.Describe(
            SettingsControls.Toggle("sign.glow", () => work.glow, v => { work.glow = v; MarkDirty(); }), "sign.glow_desc"));

        boardHost.Add(IndustryUi.Section(UiLocale.T("sign.sec_presets")));
        var presets = IndustryUi.El("Presets", "sign-presets");
        for (int i = 0; i < SignPresets.All.Length; i++)
        {
            SignPresets.Preset p = SignPresets.All[i];
            presets.Add(IndustryUi.Btn(UiLocale.T(p.titleKey), () => ApplyPreset(p), "btn-small"));
        }

        boardHost.Add(presets);
        var actions = IndustryUi.El("BoardActions", "sign-actions");
        actions.Add(IndustryUi.Btn(UiLocale.T("sign.revert"), Revert, "btn-small"));
        actions.Add(IndustryUi.Btn(UiLocale.T("sign.clear"), () =>
        {
            work.items.Clear();
            selected = -1;
            MarkDirty();
            RebuildList();
            RebuildProps();
        }, "btn-small"));
        boardHost.Add(actions);
    }

    static (string, Func<bool>, Action) Chip(string key, Func<bool> on, Action click)
    {
        return (UiLocale.T(key), on, click);
    }

    void SetMount(int mount)
    {
        work.mount = mount;
        MarkDirty();
    }

    void ApplyPreset(SignPresets.Preset p)
    {
        work = p.make();
        work.Sanitize();
        selected = work.items.Count > 0 ? work.items.Count - 1 : -1;
        UiAudio.PlayConfirm();
        MarkDirty();
        RebuildAll();
    }

    void Revert()
    {
        work = original.Clone();
        selected = work.items.Count > 0 ? 0 : -1;
        MarkDirty();
        RebuildAll();
    }

    /// <summary>Строка выбора цвета: палитра таблички + поле «#RRGGBB» для точного цвета.</summary>
    VisualElement SwatchRow(string key, Func<string> get, Action<string> set)
    {
        var row = IndustryUi.El("Row", "set-row", "set-row-wide", "sign-swatch-row");
        var text = IndustryUi.El("Text", "set-text");
        text.Add(IndustryUi.Text("L", UiLocale.T(key), "set-label"));
        row.Add(text);
        var ctl = IndustryUi.El("Ctl", "set-ctl", "sign-swatches");
        var cells = new List<VisualElement>();
        var field = new TextField { name = "Hex", maxLength = 7 };
        field.AddToClassList("field");
        field.AddToClassList("sign-hex");

        void Refresh()
        {
            string cur = get();
            for (int i = 0; i < cells.Count; i++)
                IndustryUi.SetOn(cells[i], (cells[i].userData as string) == cur, "is-selected");
            field.SetValueWithoutNotify("#" + cur);
        }

        for (int i = 0; i < SignColors.Palette.Length; i++)
        {
            string hex = SignColors.Palette[i];
            var cell = IndustryUi.El("Swatch", "sign-swatch");
            cell.style.backgroundColor = SignColors.Parse(hex);
            cell.userData = hex;
            cell.AddManipulator(new Clickable(() =>
            {
                set(hex);
                UiAudio.PlayClick();
                Refresh();
                MarkDirty();
            }));
            cells.Add(cell);
            ctl.Add(cell);
        }

        field.RegisterValueChangedCallback(evt =>
        {
            string hex = SignColors.Normalize(evt.newValue);
            if (hex == null || hex == get())
                return;
            set(hex);
            for (int i = 0; i < cells.Count; i++)
                IndustryUi.SetOn(cells[i], (cells[i].userData as string) == hex, "is-selected");
            MarkDirty();
        });
        ctl.Add(field);
        row.Add(ctl);
        row.userData = (Action)Refresh;
        Refresh();
        return row;
    }

    // ---------- Элементы ----------

    SignElement Selected => work != null && selected >= 0 && selected < work.items.Count ? work.items[selected] : null;

    string Contrast()
    {
        Color b = SignColors.Parse(work.board);
        float lum = b.r * 0.3f + b.g * 0.59f + b.b * 0.11f;
        return lum > 0.55f ? "0E0F11" : "FFFFFF";
    }

    void AddElement(SignKind kind)
    {
        if (work.items.Count >= SignData.MaxElements)
        {
            UiAudio.PlayError();
            UiNotification.Push(UiLocale.T("sign.full"), UiLocale.T("sign.full_sub", SignData.MaxElements), UiStatus.Warning);
            return;
        }

        SignElement el;
        switch (kind)
        {
            case SignKind.Symbol: el = SignElement.Symbol("→", 0f, 0f, 0.3f, Contrast()); break;
            case SignKind.Icon: el = SignElement.Icon("item:gear", 0f, 0f, 0.42f); break;
            case SignKind.Plate: el = SignElement.Plate(0f, 0f, 0.4f, 0.16f, "F08A24"); break;
            default: el = SignElement.Text(UiLocale.T("sign.new_text"), 0f, 0f, 0.2f, Contrast()); break;
        }

        // новый элемент чуть смещён, чтобы не лечь ровно поверх прошлого
        float shift = (work.items.Count % 5) * 0.04f;
        el.x = shift - 0.08f;
        el.y = 0.08f - shift;
        work.items.Add(el);
        selected = work.items.Count - 1;
        UiAudio.PlayConfirm();
        MarkDirty();
        RebuildList();
        RebuildProps();
    }

    void Select(int index)
    {
        if (index == selected)
            return;
        selected = index;
        RebuildList();
        RebuildProps();
    }

    void RemoveSelected()
    {
        if (Selected == null)
            return;
        work.items.RemoveAt(selected);
        selected = Mathf.Min(selected, work.items.Count - 1);
        UiAudio.PlayToggle();
        MarkDirty();
        RebuildList();
        RebuildProps();
    }

    void MoveLayer(int dir)
    {
        int to = selected + dir;
        if (Selected == null || to < 0 || to >= work.items.Count)
            return;
        SignElement el = work.items[selected];
        work.items.RemoveAt(selected);
        work.items.Insert(to, el);
        selected = to;
        MarkDirty();
        RebuildList();
    }

    void Duplicate()
    {
        if (Selected == null)
            return;
        if (work.items.Count >= SignData.MaxElements)
        {
            UiAudio.PlayError();
            return;
        }

        SignElement copy = Selected.Copy();
        copy.x = Mathf.Clamp(copy.x + 0.05f, -0.6f, 0.6f);
        copy.y = Mathf.Clamp(copy.y - 0.05f, -0.6f, 0.6f);
        work.items.Insert(selected + 1, copy);
        selected++;
        MarkDirty();
        RebuildList();
        RebuildProps();
    }

    void RebuildList()
    {
        listHost.Clear();
        listTitle.text = UiLocale.T("sign.sec_items", work.items.Count, SignData.MaxElements);
        if (work.items.Count == 0)
        {
            listHost.Add(IndustryUi.Text("Empty", UiLocale.T("sign.list_empty"), "muted"));
            return;
        }

        // сверху списка — верхний слой (рисуется поверх остальных)
        for (int i = work.items.Count - 1; i >= 0; i--)
        {
            int index = i;
            SignElement el = work.items[i];
            var row = IndustryUi.El("El", "sign-el");
            IndustryUi.SetOn(row, i == selected, "is-selected");
            row.Add(Badge(el));
            row.Add(IndustryUi.Text("T", ElementTitle(el), "sign-el-title"));
            row.Add(IndustryUi.Text("K", KindTitle(el.Kind), "sign-el-kind"));
            row.AddManipulator(new Clickable(() =>
            {
                UiAudio.PlayClick();
                Select(index);
            }));
            listHost.Add(row);
        }
    }

    VisualElement Badge(SignElement el)
    {
        var badge = IndustryUi.El("Badge", "sign-el-badge");
        switch (el.Kind)
        {
            case SignKind.Icon:
                badge.Add(IndustryUi.Icon(SignView.IconSprite(el.text), "sign-el-icon"));
                break;
            case SignKind.Plate:
                badge.style.backgroundColor = SignColors.Parse(el.color);
                break;
            default:
                var l = IndustryUi.Text("G", el.Kind == SignKind.Symbol ? el.text : "T", "sign-el-badge-label");
                l.style.color = SignColors.Parse(el.color);
                badge.Add(l);
                break;
        }

        return badge;
    }

    static string ElementTitle(SignElement el)
    {
        switch (el.Kind)
        {
            case SignKind.Icon:
                return IconTitle(el.text);
            case SignKind.Plate:
                return "#" + el.color;
            case SignKind.Symbol:
                return el.text;
            default:
                string t = (el.text ?? "").Replace('\n', ' ').Trim();
                if (t.Length == 0)
                    return UiLocale.T("sign.empty_text");
                return t.Length > 28 ? t.Substring(0, 27) + "…" : t;
        }
    }

    static string KindTitle(SignKind kind)
    {
        switch (kind)
        {
            case SignKind.Symbol: return UiLocale.T("sign.kind_symbol");
            case SignKind.Icon: return UiLocale.T("sign.kind_icon");
            case SignKind.Plate: return UiLocale.T("sign.kind_plate");
            default: return UiLocale.T("sign.kind_text");
        }
    }

    // ---------- Свойства ----------

    void RebuildProps()
    {
        propsHost.Clear();
        rowX = rowY = rowSize = rowRot = rowW = rowH = null;
        SignElement el = Selected;
        if (el == null)
        {
            propsHost.Add(IndustryUi.Empty(UiLocale.T("sign.no_sel"), UiLocale.T("sign.no_sel_body")));
            return;
        }

        switch (el.Kind)
        {
            case SignKind.Text:
            {
                var field = new TextField { name = "SignText", multiline = true, maxLength = SignData.MaxText, value = el.text };
                field.AddToClassList("field");
                field.AddToClassList("sign-textfield");
                field.RegisterValueChangedCallback(evt =>
                {
                    el.text = evt.newValue;
                    MarkDirty();
                    RefreshSelectedRow();
                });
                propsHost.Add(field);
                propsHost.Add(IndustryUi.Text("TextHint", UiLocale.T("sign.text_hint"), "sign-preview-hint"));
                break;
            }
            case SignKind.Symbol:
                propsHost.Add(GlyphGrid(el));
                break;
            case SignKind.Icon:
                propsHost.Add(IconPicker(el));
                break;
        }

        if (el.Kind == SignKind.Plate)
        {
            rowW = SettingsControls.SliderRow("sign.p_width", 0.02f, 1.2f, () => el.w, v => { el.w = v; MarkDirty(); }, Percent);
            rowH = SettingsControls.SliderRow("sign.p_height", 0.02f, 1.2f, () => el.h, v => { el.h = v; MarkDirty(); }, Percent);
            propsHost.Add(rowW);
            propsHost.Add(rowH);
        }
        else
        {
            rowSize = SettingsControls.SliderRow("sign.p_size", 0.04f, 1.2f, () => el.size, v => { el.size = v; MarkDirty(); }, Percent);
            propsHost.Add(rowSize);
        }

        rowRot = SettingsControls.SliderRow("sign.p_rot", -180f, 180f, () => el.rot, v => { el.rot = Mathf.Round(v); MarkDirty(); },
            v => Mathf.RoundToInt(v) + "°");
        rowX = SettingsControls.SliderRow("sign.p_x", -0.6f, 0.6f, () => el.x, v => { el.x = v; MarkDirty(); }, Percent);
        rowY = SettingsControls.SliderRow("sign.p_y", -0.6f, 0.6f, () => el.y, v => { el.y = v; MarkDirty(); }, Percent);
        propsHost.Add(rowRot);
        propsHost.Add(rowX);
        propsHost.Add(rowY);
        propsHost.Add(SettingsControls.ChipRow("sign.p_quick",
            ("0°", () => false, () => SetRot(el, 0f)),
            ("−90°", () => false, () => SetRot(el, -90f)),
            ("90°", () => false, () => SetRot(el, 90f)),
            (UiLocale.T("sign.center"), () => false, () => { el.x = 0f; el.y = 0f; SyncRows(); MarkDirty(); })));

        propsHost.Add(SwatchRow(el.Kind == SignKind.Icon ? "sign.p_tint" : "sign.p_color", () => el.color, v =>
        {
            el.color = v;
            RefreshSelectedRow();
        }));

        if (el.Kind == SignKind.Text)
        {
            propsHost.Add(SettingsControls.ChipRow("sign.p_style",
                (UiLocale.T("sign.bold"), () => el.bold, () => { el.bold = !el.bold; MarkDirty(); }),
                (UiLocale.T("sign.italic"), () => el.italic, () => { el.italic = !el.italic; MarkDirty(); }),
                (UiLocale.T("sign.shadow"), () => el.shadow, () => { el.shadow = !el.shadow; MarkDirty(); })));
            propsHost.Add(SettingsControls.ChipRow("sign.p_align",
                (UiLocale.T("sign.align_left"), () => el.align == 1, () => { el.align = 1; MarkDirty(); }),
                (UiLocale.T("sign.align_center"), () => el.align == 0, () => { el.align = 0; MarkDirty(); }),
                (UiLocale.T("sign.align_right"), () => el.align == 2, () => { el.align = 2; MarkDirty(); })));
        }
        else if (el.Kind == SignKind.Symbol)
        {
            propsHost.Add(SettingsControls.ChipRow("sign.p_style",
                (UiLocale.T("sign.shadow"), () => el.shadow, () => { el.shadow = !el.shadow; MarkDirty(); })));
        }

        var actions = IndustryUi.El("Actions", "sign-actions");
        actions.Add(IndustryUi.Btn(UiLocale.T("sign.layer_up"), () => MoveLayer(1), "btn-small"));
        actions.Add(IndustryUi.Btn(UiLocale.T("sign.layer_down"), () => MoveLayer(-1), "btn-small"));
        actions.Add(IndustryUi.Btn(UiLocale.T("sign.duplicate"), Duplicate, "btn-small"));
        actions.Add(IndustryUi.Btn(UiLocale.T("sign.delete"), RemoveSelected, "btn-small", "btn-danger"));
        propsHost.Add(actions);
    }

    static object Percent(float v)
    {
        return Mathf.RoundToInt(v * 100f) + "%";
    }

    void SetRot(SignElement el, float deg)
    {
        el.rot = deg;
        SyncRows();
        MarkDirty();
    }

    void SyncRows()
    {
        SettingsControls.Sync(rowX);
        SettingsControls.Sync(rowY);
        SettingsControls.Sync(rowSize);
        SettingsControls.Sync(rowRot);
        SettingsControls.Sync(rowW);
        SettingsControls.Sync(rowH);
    }

    /// <summary>Подпись выбранной строки списка без пересборки (чтобы поле ввода не теряло фокус).</summary>
    void RefreshSelectedRow()
    {
        SignElement el = Selected;
        if (el == null)
            return;
        int row = work.items.Count - 1 - selected;
        if (row < 0 || row >= listHost.childCount)
            return;
        VisualElement r = listHost[row];
        Label title = r.Q<Label>("T");
        if (title != null)
            title.text = ElementTitle(el);
        VisualElement oldBadge = r.Q("Badge");
        if (oldBadge != null)
        {
            int at = r.IndexOf(oldBadge);
            r.Remove(oldBadge);
            r.Insert(at, Badge(el));
        }

        MarkDirty();
    }

    VisualElement GlyphGrid(SignElement el)
    {
        var grid = IndustryUi.El("Glyphs", "sign-glyphs");
        var buttons = new List<VisualElement>();
        for (int i = 0; i < SignGlyphs.All.Length; i++)
        {
            string g = SignGlyphs.All[i];
            var b = IndustryUi.El("Glyph", "sign-glyph");
            b.userData = g;
            b.Add(IndustryUi.Text("G", g, "sign-glyph-label"));
            IndustryUi.SetOn(b, el.text == g, "is-selected");
            b.AddManipulator(new Clickable(() =>
            {
                el.text = g;
                UiAudio.PlayClick();
                for (int k = 0; k < buttons.Count; k++)
                    IndustryUi.SetOn(buttons[k], (buttons[k].userData as string) == g, "is-selected");
                RefreshSelectedRow();
            }));
            buttons.Add(b);
            grid.Add(b);
        }

        return grid;
    }

    VisualElement IconPicker(SignElement el)
    {
        EnsureIcons();
        var col = IndustryUi.El("IconPicker", "col");
        var search = new TextField { name = "IconSearch" };
        search.AddToClassList("field");
        search.AddToClassList("search-field");
        if (search.textEdition != null)
            search.textEdition.placeholder = UiLocale.T("sign.icon_search");
        col.Add(search);
        var grid = IndustryUi.El("Icons", "sign-icons");
        col.Add(grid);

        void Fill()
        {
            grid.Clear();
            string q = (search.value ?? "").Trim().ToLowerInvariant();
            int shown = 0;
            for (int i = 0; i < icons.Count && shown < 160; i++)
            {
                IconChoice c = icons[i];
                if (q.Length > 0 && (c.title ?? "").ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0
                    && c.id.ToLowerInvariant().IndexOf(q, StringComparison.Ordinal) < 0)
                    continue;
                shown++;
                var b = IndustryUi.El("Icon", "sign-glyph", "sign-icon-btn");
                b.Add(IndustryUi.Icon(c.sprite, "sign-icon-img"));
                IndustryUi.SetOn(b, el.text == c.id, "is-selected");
                UiTooltip.Bind(b, c.title, null);
                string id = c.id;
                b.AddManipulator(new Clickable(() =>
                {
                    el.text = id;
                    UiAudio.PlayClick();
                    foreach (VisualElement other in grid.Children())
                        IndustryUi.SetOn(other, false, "is-selected");
                    IndustryUi.SetOn(b, true, "is-selected");
                    RefreshSelectedRow();
                }));
                grid.Add(b);
            }

            if (shown == 0)
                grid.Add(IndustryUi.Text("None", UiLocale.T("sign.icon_none"), "muted"));
        }

        search.RegisterValueChangedCallback(_ => Fill());
        Fill();
        return col;
    }

    static void EnsureIcons()
    {
        if (icons != null && icons.Count > 0)
            return;
        icons = new List<IconChoice>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        ItemData[] items = GameDatabase.AllItems();
        for (int i = 0; i < items.Length; i++)
        {
            ItemData it = items[i];
            if (it == null || it.icon == null || string.IsNullOrEmpty(it.id) || !seen.Add("item:" + it.id))
                continue;
            icons.Add(new IconChoice { id = "item:" + it.id, sprite = it.icon, title = it.Title });
        }

        BuildingData[] buildings = GameDatabase.AllBuildings();
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingData b = buildings[i];
            if (b == null || b.icon == null || string.IsNullOrEmpty(b.id) || !seen.Add("bld:" + b.id))
                continue;
            icons.Add(new IconChoice { id = "bld:" + b.id, sprite = b.icon, title = b.Title });
        }
    }

    static string IconTitle(string id)
    {
        EnsureIcons();
        for (int i = 0; i < icons.Count; i++)
        {
            if (icons[i].id == id)
                return icons[i].title;
        }

        return id;
    }

    // ---------- Клавиши ----------

    void HandleKeys()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null || KeybindStore.IsTyping || UiModal.IsOpen)
            return;
        SignElement el = Selected;
        if (el == null)
            return;
        if (kb.deleteKey.wasPressedThisFrame)
        {
            RemoveSelected();
            return;
        }

        bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
        if (ctrl && kb.dKey.wasPressedThisFrame)
        {
            Duplicate();
            return;
        }

        float step = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ? 0.05f : 0.01f;
        Vector2 move = Vector2.zero;
        if (kb.leftArrowKey.wasPressedThisFrame) move.x -= step;
        if (kb.rightArrowKey.wasPressedThisFrame) move.x += step;
        if (kb.upArrowKey.wasPressedThisFrame) move.y += step;
        if (kb.downArrowKey.wasPressedThisFrame) move.y -= step;
        if (move == Vector2.zero)
            return;
        el.x = Mathf.Clamp(el.x + move.x, -0.6f, 0.6f);
        el.y = Mathf.Clamp(el.y + move.y, -0.6f, 0.6f);
        SyncRows();
        MarkDirty();
    }

    // ---------- Превью: мышь ----------

    Vector2 PointerToBoard(Vector2 panelPos)
    {
        Vector2 local = previewImage.WorldToLocal(panelPos);
        Rect r = previewImage.contentRect;
        if (view == null || r.width < 1f || r.height < 1f)
            return Vector2.zero;
        float u = local.x / r.width - 0.5f;
        float v = 0.5f - local.y / r.height;
        return new Vector2(u * viewW / view.BoardSize.x, v * viewH / view.BoardSize.y);
    }

    Vector2 BoardToPixel(Vector2 b)
    {
        Rect r = previewImage.contentRect;
        float u = b.x * view.BoardSize.x / viewW + 0.5f;
        float v = 0.5f - b.y * view.BoardSize.y / viewH;
        return new Vector2(u * r.width, v * r.height);
    }

    void OnPreviewDown(PointerDownEvent evt)
    {
        if (evt.button != 0 || view == null)
            return;
        Vector2 b = PointerToBoard(evt.position);
        int hit = view.HitTest(b);
        // уже выбранный элемент под курсором тащится, даже если сверху лежит другой
        if (Selected != null && view.TryGetRect(selected, out Rect cur) && cur.Contains(b))
            hit = selected;
        Select(hit);
        if (hit >= 0)
        {
            SignElement el = work.items[hit];
            dragOffset = new Vector2(el.x, el.y) - b;
            dragging = true;
            previewImage.CapturePointer(evt.pointerId);
            UiAudio.PlayClick();
        }

        evt.StopPropagation();
    }

    void OnPreviewMove(PointerMoveEvent evt)
    {
        if (!dragging || !previewImage.HasPointerCapture(evt.pointerId))
            return;
        SignElement el = Selected;
        if (el == null)
            return;
        Vector2 p = PointerToBoard(evt.position) + dragOffset;
        if (evt.shiftKey)
        {
            p.x = Mathf.Round(p.x * 20f) / 20f;
            p.y = Mathf.Round(p.y * 20f) / 20f;
        }
        else
        {
            // мягкая привязка к центральным осям
            if (Mathf.Abs(p.x) < 0.015f) p.x = 0f;
            if (Mathf.Abs(p.y) < 0.015f) p.y = 0f;
        }

        el.x = Mathf.Clamp(p.x, -0.6f, 0.6f);
        el.y = Mathf.Clamp(p.y, -0.6f, 0.6f);
        SyncRows();
        MarkDirty();
        evt.StopPropagation();
    }

    void OnPreviewUp(PointerUpEvent evt)
    {
        if (previewImage.HasPointerCapture(evt.pointerId))
            previewImage.ReleasePointer(evt.pointerId);
        dragging = false;
    }

    void OnPreviewWheel(WheelEvent evt)
    {
        SignElement el = Selected;
        if (el == null)
            return;
        float dir = evt.delta.y < 0f ? 1f : -1f;
        if (evt.shiftKey)
            el.rot = Mathf.Repeat(el.rot + dir * (evt.ctrlKey ? 15f : 5f) + 180f, 360f) - 180f;
        else if (el.Kind == SignKind.Plate)
        {
            float k = dir > 0f ? 1.08f : 1f / 1.08f;
            el.w = Mathf.Clamp(el.w * k, 0.02f, 1.2f);
            el.h = Mathf.Clamp(el.h * k, 0.02f, 1.2f);
        }
        else
            el.size = Mathf.Clamp(el.size * (dir > 0f ? 1.08f : 1f / 1.08f), 0.04f, 1.2f);
        SyncRows();
        MarkDirty();
        evt.StopPropagation();
    }

    void UpdateSelectBox()
    {
        if (view == null || Selected == null || !view.TryGetRect(selected, out Rect r))
        {
            IndustryUi.Show(selectBox, false);
            return;
        }

        Vector2 a = BoardToPixel(new Vector2(r.xMin, r.yMax));
        Vector2 b = BoardToPixel(new Vector2(r.xMax, r.yMin));
        const float pad = 4f;
        selectBox.style.left = a.x - pad;
        selectBox.style.top = a.y - pad;
        selectBox.style.width = Mathf.Max(6f, b.x - a.x + pad * 2f);
        selectBox.style.height = Mathf.Max(6f, b.y - a.y + pad * 2f);
        IndustryUi.Show(selectBox, true);
    }

    // ---------- Превью: камера ----------

    void EnsurePreview()
    {
        if (rig == null)
        {
            rig = new GameObject("SignPreviewRig") { hideFlags = HideFlags.DontSave };
            rig.transform.position = RigPosition;
            rigVisual = new GameObject("Visual").transform;
            rigVisual.SetParent(rig.transform, false);

            var camGo = new GameObject("SignPreviewCamera");
            camGo.transform.SetParent(rig.transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.075f, 0.09f, 0.11f, 1f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 12f;
            cam.allowHDR = false;
            cam.useOcclusionCulling = false;
            cam.depth = -50f;
            cam.cullingMask = 1 << PreviewLayer;
            cam.aspect = (float)TexW / TexH;

            var lightGo = new GameObject("SignPreviewLight");
            lightGo.transform.SetParent(rig.transform, false);
            camLight = lightGo.AddComponent<Light>();
            camLight.type = LightType.Directional;
            camLight.intensity = 1.05f;
            camLight.color = new Color(1f, 0.97f, 0.92f);
            camLight.shadows = LightShadows.None;
            camLight.cullingMask = 1 << PreviewLayer;
            camLight.enabled = false;
        }

        if (rt == null)
        {
            rt = new RenderTexture(TexW, TexH, 24, RenderTextureFormat.ARGB32) { name = "SignPreview", antiAliasing = 4 };
            rt.Create();
        }

        cam.targetTexture = rt;
        previewImage.image = rt;
        rig.SetActive(true);
        cam.enabled = true;

        sunLights.Clear();
        foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l != null && l != camLight && l.type == LightType.Directional)
                sunLights.Add(l);
        }
    }

    void RebuildPreview()
    {
        if (rig == null)
            EnsurePreview();
        view = SignView.Build(rigVisual, work, PreviewLayer, false);
        Transform face = view.FaceFront;
        Vector2 bs = view.BoardSize;
        float aspect = (float)TexW / TexH;
        const float margin = 1.18f;
        float half = Mathf.Max(bs.y * margin * 0.5f, bs.x * margin * 0.5f / aspect);
        cam.orthographicSize = half;
        viewH = half * 2f;
        viewW = viewH * aspect;
        Quaternion look = Quaternion.LookRotation(face.forward, face.up);
        cam.transform.SetPositionAndRotation(face.position - face.forward * 4f, look);
        camLight.transform.rotation = look * Quaternion.Euler(28f, -32f, 0f);
    }

    // Только для камеры превью: своё освещение без солнца, тумана и ночи мира.
    void OnCamPreCull(Camera c)
    {
        if (c == null || c != cam)
            return;
        muted.Clear();
        for (int i = 0; i < sunLights.Count; i++)
        {
            Light l = sunLights[i];
            if (l != null && l.enabled)
            {
                l.enabled = false;
                muted.Add(l);
            }
        }

        savedMode = RenderSettings.ambientMode;
        savedAmbient = RenderSettings.ambientLight;
        savedIntensity = RenderSettings.ambientIntensity;
        savedFog = RenderSettings.fog;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.52f, 0.54f, 0.58f);
        RenderSettings.ambientIntensity = 1f;
        RenderSettings.fog = false;
        camLight.enabled = true;
        lit = true;
    }

    void OnCamPostRender(Camera c)
    {
        if (c == null || c != cam)
            return;
        RestoreLighting();
    }

    void RestoreLighting()
    {
        if (!lit)
            return;
        lit = false;
        for (int i = 0; i < muted.Count; i++)
        {
            if (muted[i] != null)
                muted[i].enabled = true;
        }

        muted.Clear();
        RenderSettings.ambientMode = savedMode;
        RenderSettings.ambientLight = savedAmbient;
        RenderSettings.ambientIntensity = savedIntensity;
        RenderSettings.fog = savedFog;
        if (camLight != null)
            camLight.enabled = false;
    }
}
