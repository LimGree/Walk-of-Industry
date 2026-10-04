using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UIElements.Image;

public static class IndustryUi
{
    static readonly Dictionary<int, PanelSettings> Panels = new Dictionary<int, PanelSettings>();
    static StyleSheet sheet;
    static Font font;
    static ThemeStyleSheet runtimeTheme;

    public static VisualElement Mount(MonoBehaviour host, int sortingOrder)
    {
        if (host == null)
            return null;

        string childName = "UITK_" + host.GetType().Name;
        Transform existing = host.transform.Find(childName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(childName);
        go.transform.SetParent(host.transform, false);
        UIDocument doc = go.GetComponent<UIDocument>();
        if (doc == null)
            doc = go.AddComponent<UIDocument>();
        doc.panelSettings = Settings(sortingOrder);
        VisualElement root = doc.rootVisualElement;
        root.Clear();
        root.AddToClassList("root");
        root.pickingMode = PickingMode.Ignore;
        ApplyFont(root);
        StyleSheet tokens = Tokens();
        if (tokens != null && !root.styleSheets.Contains(tokens))
            root.styleSheets.Add(tokens);
        StyleSheet theme = Theme();
        if (theme != null && !root.styleSheets.Contains(theme))
            root.styleSheets.Add(theme);
        UiRuntime.Ensure();
        UiLook.Register(root);
        return root;
    }

    public static PanelSettings Settings(int sortingOrder)
    {
        if (Panels.TryGetValue(sortingOrder, out PanelSettings existing) && existing != null)
            return existing;

        PanelSettings settings = LoadPanelTemplate();
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            ThemeStyleSheet tss = RuntimeTheme();
            if (tss != null)
                settings.themeStyleSheet = tss;
        }
        settings.name = "IndustryPanel_" + sortingOrder;
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        settings.match = 0.5f;
        settings.scale = GameSettings.UiScale;
        settings.sortingOrder = sortingOrder;
        if (settings.themeStyleSheet == null)
            settings.themeStyleSheet = RuntimeTheme();
        Panels[sortingOrder] = settings;
        return settings;
    }

    /// <summary>Применяет GameSettings.UiScale ко всем панелям.</summary>
    public static void ApplyUiScale()
    {
        float scale = GameSettings.UiScale;
        foreach (var pair in Panels)
        {
            if (pair.Value != null)
                pair.Value.scale = scale;
        }
    }

    static PanelSettings LoadPanelTemplate()
    {
        PanelSettings src = Resources.Load<PanelSettings>("UI/IndustryPanel");
        if (src == null)
            return null;
        PanelSettings clone = UnityEngine.Object.Instantiate(src);
        clone.hideFlags = HideFlags.HideAndDontSave;
        return clone;
    }

    static ThemeStyleSheet RuntimeTheme()
    {
        if (runtimeTheme != null)
            return runtimeTheme;
        runtimeTheme = Resources.Load<ThemeStyleSheet>("UI/IndustryTheme");
        if (runtimeTheme != null)
            return runtimeTheme;
        runtimeTheme = Resources.Load<ThemeStyleSheet>("UI/UnityDefaultRuntimeTheme");
        if (runtimeTheme != null)
            return runtimeTheme;
#if UNITY_EDITOR
        runtimeTheme = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
            "Packages/com.unity.ui/PackageResources/StyleSheets/Generated/Default.tss");
#endif
        return runtimeTheme;
    }

    static Font UiFont()
    {
        if (font != null)
            return font;
        font = Resources.Load<Font>("UI/LiberationSans");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        if (font == null)
            font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Arial", "Tahoma" }, 16);
        return font;
    }

    static void ApplyFont(VisualElement el)
    {
        Font face = UiFont();
        if (el == null || face == null)
            return;
        el.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromFont(face));
        el.style.unityFont = face;
    }

    public static VisualTreeAsset LoadTree(string typeName)
    {
        VisualTreeAsset tree = Resources.Load<VisualTreeAsset>("UI/" + typeName);
        if (tree != null)
            return tree;
        if (typeName.EndsWith("UI"))
            tree = Resources.Load<VisualTreeAsset>("UI/" + typeName.Substring(0, typeName.Length - 2));
        if (tree != null)
            return tree;
        if (typeName == "WalletHud")
            return Resources.Load<VisualTreeAsset>("UI/Wallet");
        if (typeName == "SelectionActionsUI")
            return Resources.Load<VisualTreeAsset>("UI/Selection");
        if (typeName == "LoadingScreen")
            return Resources.Load<VisualTreeAsset>("UI/Loading");
        return null;
    }

    public static StyleSheet Theme()
    {
        if (sheet == null)
            sheet = Resources.Load<StyleSheet>("UI/Industry");
        return sheet;
    }

    public static StyleSheet Tokens()
    {
        return Resources.Load<StyleSheet>("UI/IndustryTokens");
    }

    public static VisualElement Screen(string name, params string[] classes)
    {
        var el = new VisualElement { name = name };
        el.AddToClassList("screen");
        AddClasses(el, classes);
        return el;
    }

    public static VisualElement El(string name, params string[] classes)
    {
        var el = new VisualElement { name = name };
        AddClasses(el, classes);
        return el;
    }

    public static Label Text(string name, string value, params string[] classes)
    {
        var label = new Label(value) { name = name };
        AddClasses(label, classes);
        ApplyFont(label);
        return label;
    }

    public static Button Btn(string label, Action onClick, params string[] classes)
    {
        var button = new Button(() =>
        {
            UiAudio.PlayClick();
            onClick?.Invoke();
        }) { text = "" };
        AddClasses(button, "btn");
        AddClasses(button, classes);
        ApplyFont(button);
        var text = new Label(label);
        text.AddToClassList("btn-label");
        ApplyFont(text);
        button.Add(text);
        button.RegisterCallback<PointerEnterEvent>(_ => UiAudio.PlayHover());
        return button;
    }

    public static VisualElement Icon(Sprite sprite, params string[] classes)
    {
        var image = new Image();
        AddClasses(image, classes);
        if (sprite != null)
        {
            image.sprite = sprite;
            image.scaleMode = ScaleMode.ScaleToFit;
        }
        image.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
        return image;
    }

    public static void SetIcon(Image image, Sprite sprite)
    {
        if (image == null)
            return;
        image.sprite = sprite;
        image.scaleMode = ScaleMode.ScaleToFit;
        image.style.display = sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
    }

    /// <summary>
    /// Каркас игрового окна в стиле настроек: затемнение, панель с янтарной кромкой,
    /// шапка (иконка, заголовок, подзаголовок, «Esc — закрыть»), тело и подвал с подсказками (WindowHints).
    /// Имена Overlay / Dim / Panel / Header / Title / Close / Body сохранены для старых окон.
    /// </summary>
    public static VisualElement OverlayPanel(string title, Sprite icon, Action onClose)
    {
        VisualElement screen = Screen("Overlay");
        screen.pickingMode = PickingMode.Position;
        var dim = El("Dim", "dim");
        dim.pickingMode = PickingMode.Position;
        screen.Add(dim);

        var panel = El("Panel", "panel", "panel-wide", "win");
        var header = El("Header", "header", "win-header");
        header.Add(Icon(icon, "header-icon", "win-icon"));
        var titles = El("Titles", "win-titles");
        titles.Add(Text("Title", title ?? "", "title", "win-title"));
        var sub = Text("Sub", "", "win-sub");
        Show(sub, false);
        titles.Add(sub);
        header.Add(titles);
        header.Add(El("Spacer", "grow"));

        var close = new Button(() => onClose?.Invoke()) { name = "Close", text = "" };
        AddClasses(close, "win-close");
        ApplyFont(close);
        close.Add(Keycap("Esc"));
        close.Add(Text("L", UiLocale.T("win.close"), "hint-label"));
        header.Add(close);
        panel.Add(header);

        panel.Add(El("Body", "col", "grow", "panel-body", "win-body"));
        screen.Add(panel);
        return screen;
    }

    /// <summary>Серый подзаголовок под названием окна (счётчик, состояние, подсказка).</summary>
    public static void WindowSubtitle(VisualElement overlay, string text)
    {
        Label sub = overlay != null ? overlay.Q<Label>("Sub") : null;
        if (sub == null)
            return;
        sub.text = text ?? "";
        Show(sub, !string.IsNullOrEmpty(text));
    }

    /// <summary>Подвал окна со строкой подсказок «клавиша — действие». Всегда последним в панели.</summary>
    public static void WindowHints(VisualElement overlay, params (string key, string label)[] hints)
    {
        VisualElement panel = PanelOf(overlay);
        if (panel == null)
            return;
        VisualElement footer = panel.Q("WinFooter");
        if (footer == null)
            footer = El("WinFooter", "win-footer");
        footer.Clear();
        for (int i = 0; i < hints.Length; i++)
        {
            var hint = El("Hint", "win-hint");
            hint.Add(Keycap(hints[i].key));
            hint.Add(Text("L", hints[i].label, "hint-label"));
            footer.Add(hint);
        }
        panel.Add(footer);
        Show(footer, hints.Length > 0);
    }

    /// <summary>
    /// Боковая колонка вкладок как в настройках. Возвращает колонку; onSelect зовётся с id вкладки.
    /// SetNav(nav, id) подсвечивает выбранную.
    /// </summary>
    public static VisualElement SideNav(IList<(string id, string title, string desc)> tabs, Action<string> onSelect)
    {
        var nav = El("Nav", "set-nav", "win-nav");
        for (int i = 0; i < tabs.Count; i++)
        {
            var t = tabs[i];
            var btn = El("Nav_" + t.id, "set-nav-btn");
            btn.userData = t.id;
            btn.Add(El("Bar", "set-nav-bar"));
            var col = El("Col", "set-nav-text");
            col.Add(Text("T", t.title, "set-nav-title"));
            if (!string.IsNullOrEmpty(t.desc))
                col.Add(Text("D", t.desc, "set-nav-desc"));
            btn.Add(col);
            string id = t.id;
            btn.AddManipulator(new Clickable(() =>
            {
                UiAudio.PlayToggle();
                onSelect?.Invoke(id);
            }));
            nav.Add(btn);
        }
        return nav;
    }

    public static void SetNav(VisualElement nav, string id)
    {
        if (nav == null)
            return;
        foreach (VisualElement child in nav.Children())
            SetOn(child, (child.userData as string) == id, "is-selected");
    }

    /// <summary>Заголовок секции внутри окна (как группы в настройках).</summary>
    public static VisualElement Section(string title)
    {
        var head = El("Section", "set-group");
        head.Add(Text("T", title, "set-group-title"));
        head.Add(El("Line", "set-group-line"));
        return head;
    }

    /// <summary>Пустое состояние: крупная серая строка + пояснение.</summary>
    public static VisualElement Empty(string title, string body = null)
    {
        var box = El("Empty", "win-empty");
        box.Add(Text("T", title, "win-empty-title"));
        if (!string.IsNullOrEmpty(body))
            box.Add(Text("B", body, "win-empty-body"));
        return box;
    }

    public static VisualElement PanelOf(VisualElement overlay)
    {
        return overlay != null ? overlay.Q("Panel") : null;
    }

    public static void Show(VisualElement el, bool on)
    {
        if (el != null)
            el.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
    }

    public static void ShowPanel(VisualElement el, bool on)
    {
        if (el == null)
            return;
        if (on)
            UiMotion.ShowPanel(el);
        else
            UiMotion.HidePanel(el);
    }

    public static string Money(int value)
    {
        return value.ToString("N0");
    }

    public static VisualElement Keycap(string key)
    {
        var cap = El("Key", "keycap");
        cap.Add(Text("K", key ?? "", "hint-key"));
        return cap;
    }

    public static VisualElement StatusLine(string name, UiStatus status, string extra = null)
    {
        var row = El(name, "status-row");
        UiStatusUtil.Apply(row, status);
        row.Add(El("Dot", "status-dot"));
        string label = UiStatusUtil.Label(status);
        if (!string.IsNullOrEmpty(extra))
            label = label + "   " + extra;
        row.Add(Text("L", label, "body-small", "grow"));
        var badge = Text("B", UiStatusUtil.Label(status), "badge", UiStatusUtil.BadgeClass(status));
        row.Add(badge);
        return row;
    }

    public static void SetState(VisualElement element, string state, bool enabled = true)
    {
        if (element == null)
            return;
        element.EnableInClassList("is-selected", state == "selected");
        element.EnableInClassList("is-active", state == "active");
        element.EnableInClassList("is-locked", state == "locked");
        element.EnableInClassList("is-error", state == "error");
        element.EnableInClassList("is-complete", state == "complete");
        element.EnableInClassList("is-empty", state == "empty");
        element.SetEnabled(enabled);
    }

    public static string BuildingCategory(BuildingData data)
    {
        if (data == null)
            return "Special";
        if (DecorCatalog.IsDecor(data))
            return "Decor";
        if (data.IsConveyor)
            return "Logistics";
        if (data.requiresResourceNode || data.requiresWater || data.allowOnWater)
            return "Extraction";
        string hay = ((data.id ?? "") + " " + (data.Title ?? "")).ToLowerInvariant();
        if (hay.IndexOf("storage", StringComparison.Ordinal) >= 0
            || hay.IndexOf("tank", StringComparison.Ordinal) >= 0
            || hay.IndexOf("container", StringComparison.Ordinal) >= 0)
            return "Storage";
        if (hay.IndexOf("lab", StringComparison.Ordinal) >= 0
            || hay.IndexOf("research", StringComparison.Ordinal) >= 0)
            return "Research";
        if (hay.IndexOf("drone", StringComparison.Ordinal) >= 0
            || hay.IndexOf("splitter", StringComparison.Ordinal) >= 0
            || hay.IndexOf("arm", StringComparison.Ordinal) >= 0)
            return "Logistics";
        return "Production";
    }

    public static void HideLegacy(Component host, params GameObject[] roots)
    {
        if (host != null)
        {
            Transform keep = host.transform;
            for (int i = 0; i < keep.childCount; i++)
            {
                Transform child = keep.GetChild(i);
                if (child == null || child.name.StartsWith("UITK_"))
                    continue;
                child.gameObject.SetActive(false);
            }
        }

        if (roots == null)
            return;
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject go = roots[i];
            if (go == null || (host != null && go == host.gameObject))
                continue;
            go.SetActive(false);
        }
    }

    public static void DisableHudCanvas(Component host)
    {
        if (host == null)
            return;
        Canvas canvas = host.GetComponent<Canvas>();
        if (canvas == null)
            canvas = host.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            return;
        canvas.enabled = false;
        var ray = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
        if (ray != null)
            ray.enabled = false;
    }

    public static void SetHeader(VisualElement overlay, string title, Sprite icon)
    {
        Label label = overlay != null ? overlay.Q<Label>("Title") : null;
        if (label != null)
            label.text = title ?? "";
        Image image = overlay != null ? overlay.Q<Image>(className: "header-icon") : null;
        SetIcon(image, icon);
    }

    public static void SetButtonLabel(Button button, string label)
    {
        Label text = button != null ? button.Q<Label>(className: "btn-label") : null;
        if (text != null)
            text.text = label ?? "";
    }

    public static void SetOn(VisualElement el, bool on, string className)
    {
        if (el == null || string.IsNullOrEmpty(className))
            return;
        if (on)
            el.AddToClassList(className);
        else
            el.RemoveFromClassList(className);
    }

    public static ScrollView Scroll(string name = "Scroll")
    {
        var scroll = new ScrollView { name = name };
        scroll.AddToClassList("scroll");
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.style.flexGrow = 1;
        scroll.style.flexShrink = 1;
        scroll.style.minHeight = 0;
        return scroll;
    }

    public static VisualElement ProgressBar(string name)
    {
        var track = El(name, "progress-track");
        track.Add(El("Fill", "progress-fill"));
        return track;
    }

    public static void SetProgress(VisualElement track, float t)
    {
        VisualElement fill = track != null ? track.Q("Fill") : null;
        if (fill != null)
            fill.style.width = Length.Percent(Mathf.Clamp01(t) * 100f);
    }

    public static Button TabBtn(string label, Action onClick)
    {
        Button button = Btn(label, onClick, "tab");
        button.RemoveFromClassList("btn");
        return button;
    }

    public static Button CardButton(Action onClick, params string[] classes)
    {
        var button = new Button(() => onClick?.Invoke()) { text = "" };
        AddClasses(button, "card");
        AddClasses(button, classes);
        ApplyFont(button);
        return button;
    }

    public static ItemData FirstItem(IList<ItemStack> stacks)
    {
        if (stacks == null)
            return null;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i] != null && stacks[i].item != null)
                return stacks[i].item;
        }
        return null;
    }

    public static VisualElement StackChip(Sprite sprite, int amount)
    {
        return StackChip(null, sprite, amount, -1);
    }

    public static VisualElement StackChip(ItemData item, int need, int have = -1)
    {
        return StackChip(item, item != null ? item.icon : null, need, have);
    }

    public static VisualElement StackChip(ItemData item, Sprite sprite, int need, int have)
    {
        var chip = El("Chip", "stack-chip");
        chip.Add(Icon(sprite, "stack-icon"));
        string count = have >= 0 ? have + "/" + need : (need > 0 ? need.ToString() : "");
        if (!string.IsNullOrEmpty(count))
            chip.Add(Text("N", count, "stack-count"));
        if (item != null)
        {
            string qty = have >= 0 ? have + " / " + need : need.ToString();
            UiTooltip.Bind(chip, item.Title, string.IsNullOrEmpty(item.Info) ? qty : item.Info + "\n" + qty);
        }

        return chip;
    }

    public static void AddStacks(VisualElement row, IList<ItemStack> stacks)
    {
        if (row == null)
            return;
        int added = 0;
        if (stacks != null)
        {
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack stack = stacks[i];
                if (stack == null || stack.item == null)
                    continue;
                row.Add(StackChip(stack.item, stack.amount));
                added++;
            }
        }

        if (added == 0)
            row.Add(Text("None", "—", "muted"));
    }

    public static VisualElement RecipeCard(RecipeData recipe, bool selected, Action onClick)
    {
        return RecipeCard(
            recipe != null ? recipe.Title : "Recipe",
            recipe != null ? recipe.inputs : null,
            recipe != null ? recipe.outputs : null,
            selected,
            onClick);
    }

    public static VisualElement RecipeCard(
        string title,
        IList<ItemStack> inputs,
        IList<ItemStack> outputs,
        bool selected,
        Action onClick)
    {
        Button card = CardButton(onClick, selected ? "card-on" : null);
        card.AddToClassList("card-row");
        ItemData first = FirstItem(outputs);
        card.Add(Well(first != null ? first.icon : null));
        var col = El("Col", "col", "grow");
        col.Add(Text("T", title ?? UiLocale.T("machine.recipe"), "body-text"));
        var io = El("IO", "row", "io-row");
        AddStacks(io, inputs);
        io.Add(Text("Arr", " → ", "gold"));
        AddStacks(io, outputs);
        col.Add(io);
        card.Add(col);
        return card;
    }

    /// <summary>Иконка в круглой подложке — как в плитках сумки.</summary>
    public static VisualElement Well(Sprite icon, string extraClass = null)
    {
        var well = El("Well", "card-well");
        if (!string.IsNullOrEmpty(extraClass))
            well.AddToClassList(extraClass);
        var img = Icon(icon, "card-well-icon");
        img.pickingMode = PickingMode.Ignore;
        well.pickingMode = PickingMode.Ignore;
        well.Add(img);
        return well;
    }

    public static VisualElement ActionCard(string title, string subtitle, bool enabled, Action onClick)
    {
        Button card = CardButton(enabled ? onClick : null);
        if (!enabled)
        {
            card.AddToClassList("card-locked");
            card.SetEnabled(false);
        }
        var col = El("Col", "col", "grow");
        col.Add(Text("T", title ?? "", enabled ? "gold" : "muted"));
        col.Add(Text("S", subtitle ?? "", "muted"));
        card.Add(col);
        return card;
    }

    public static VisualElement FilterCard(Sprite icon, string title, string subtitle, bool selected, Action onClick)
    {
        Button card = CardButton(onClick, selected ? "card-on" : null);
        card.AddToClassList("card-row");
        card.Add(Well(icon));
        var col = El("Col", "col", "grow");
        col.Add(Text("T", title ?? "", selected ? "gold" : "body-text"));
        col.Add(Text("S", subtitle ?? "", "muted"));
        card.Add(col);
        return card;
    }

    public static VisualElement BuildingCard(BuildingData data, bool unlocked, string subtitle, bool marked, Action onClick)
    {
        return BuildingCard(data, unlocked, subtitle, marked, onClick, compact: false);
    }

    public static VisualElement BuildingCard(
        BuildingData data,
        bool unlocked,
        string subtitle,
        bool marked,
        Action onClick,
        bool compact)
    {
        VisualElement card = onClick != null && unlocked
            ? CardButton(onClick, "card-building")
            : El("BuildingCard", "card", "card-building");
        if (data != null && !string.IsNullOrEmpty(data.id))
            card.name = "Bag_" + data.id;
        if (compact)
            card.AddToClassList("card-building-compact");
        if (marked)
            card.AddToClassList("is-selected");
        if (!unlocked)
        {
            card.AddToClassList("is-locked");
            card.AddToClassList("card-locked");
            card.SetEnabled(true);
        }

        card.Add(Icon(data != null ? data.icon : null, "card-icon"));
        var col = El("Meta", "col", "grow");
        string cat = BuildingCategory(data);
        col.Add(Text("Cat", UiLocale.T("cat." + cat.ToLowerInvariant()), "card-cat"));
        col.Add(Text("T", data != null ? data.Title : UiLocale.T("b.building"), unlocked ? "heading-3" : "muted"));
        int cost = Economy.BuildCost(data);
        if (cost > 0)
            col.Add(Text("Cost", "◈  " + Money(cost), "card-cost"));
        if (!unlocked)
            col.Add(Text("Lock", UiLocale.T("build.locked"), "badge", "badge-locked"));
        else if (!string.IsNullOrEmpty(subtitle))
            col.Add(Text("Sub", subtitle, marked ? "gold" : "caption"));
        else if (!compact && data != null && !string.IsNullOrEmpty(data.Info))
            col.Add(Text("D", data.Info, "caption"));
        card.Add(col);

        string tipTitle = data != null ? data.Title : UiLocale.T("b.building");
        string tipBody = unlocked
            ? (string.IsNullOrEmpty(data != null ? data.Info : null) ? cat : data.Info)
            : UiLocale.T("build.needs_research");
        UiTooltip.Bind(card, tipTitle, tipBody, cost > 0 ? UiLocale.T("wallet.coins_n", Money(cost)) : null);
        return card;
    }

    public static VisualElement ResearchCard(ResearchNodeData node, string status, bool canStart, Action onClick)
    {
        Button card = CardButton(onClick, "research-node");
        if (node != null && !string.IsNullOrEmpty(node.id))
            card.name = "Res_" + node.id;
        if (status == "ACTIVE")
            card.AddToClassList("is-active");
        if (status == "DONE")
            card.AddToClassList("is-complete");
        if (status == "LOCKED")
            card.AddToClassList("is-locked");
        card.Add(Icon(node != null ? node.icon : null, "icon-32"));
        var col = El("Col", "col", "grow");
        var head = El("H", "row");
        head.Add(Text("T", node != null ? node.Title : UiLocale.T("machine.tab_research"), "body-text", "grow"));
        string badgeClass = status == "DONE" ? "badge-done"
            : status == "ACTIVE" ? "badge-running"
            : status == "LOCKED" ? "badge-locked"
            : "badge-ready";
        string shown = status == "DONE" ? UiLocale.T("research.done")
            : status == "ACTIVE" ? UiLocale.T("research.active")
            : status == "LOCKED" ? UiLocale.T("research.locked")
            : UiLocale.T("research.ready");
        head.Add(Text("S", shown, "badge", badgeClass));
        col.Add(head);
        var need = El("Need", "row", "io-row");
        AddStacks(need, node != null ? node.requiredItems : null);
        col.Add(need);
        card.Add(col);
        string req = node != null ? node.Info : "";
        UiTooltip.Bind(card, node != null ? node.Title : UiLocale.T("machine.tab_research"), req, status);
        return card;
    }

    public static VisualElement StatRow(Sprite icon, string name, string detail)
    {
        var row = El("Stat", "stat-row");
        row.Add(Icon(icon, "icon-24"));
        row.Add(Text("N", name ?? "", "stat-name"));
        row.Add(Text("D", detail ?? "", "stat-value"));
        return row;
    }

    public static VisualElement RateBar(Sprite icon, string name, float plus, float minus, float max)
    {
        var col = El("Rate", "col", "rate-block");
        col.Add(StatRow(icon, name, "+" + plus.ToString("0.#") + UiLocale.T("unit.per_min") + "   −" + minus.ToString("0.#") + UiLocale.T("unit.per_min")));
        var track = El("Track", "rate-track");
        var up = El("Up", "rate-fill", "rate-fill-up");
        var down = El("Dn", "rate-fill", "rate-fill-down");
        float m = Mathf.Max(0.01f, max);
        up.style.width = Length.Percent(Mathf.Clamp01(plus / m) * 100f);
        down.style.width = Length.Percent(Mathf.Clamp01(minus / m) * 100f);
        track.Add(up);
        track.Add(down);
        col.Add(track);
        return col;
    }

    public static VisualElement BuildingChip(BuildingData building)
    {
        var chip = El("Bld", "codex-building");
        chip.Add(Icon(building != null ? building.icon : null, "icon-32"));
        chip.Add(Text("N", building != null ? building.Title : "—", "caption"));
        if (building != null)
            UiTooltip.Bind(chip, building.Title, building.Info);
        return chip;
    }

    public static VisualElement CodexCard(RecipeCodex.Entry entry)
    {
        ItemData item = entry.item;
        var card = El("Codex", "card", "codex-card");
        if (item != null && !string.IsNullOrEmpty(item.id))
            card.name = "Codex_" + item.id.Trim();

        var head = El("Head", "codex-head");
        head.Add(Well(item != null ? item.icon : null, "card-well-lg"));
        var meta = El("Meta", "col", "grow");
        var titleRow = El("TitleRow", "row");
        titleRow.Add(Text("T", item != null ? item.Title : "—", "heading-3", "grow"));
        if (item != null && item.isFluid)
            titleRow.Add(Text("Fluid", UiLocale.T("codex.fluid"), "badge", "badge-ready"));
        meta.Add(titleRow);
        if (item != null && !string.IsNullOrEmpty(item.Info))
            meta.Add(Text("D", item.Info, "muted"));
        head.Add(meta);
        if (item != null)
            head.Add(Btn(UiLocale.T("codex.open_map"), () => ProductionMapUI.Instance?.Open(item), "btn-small", "btn-ghost"));
        card.Add(head);

        bool any = false;
        if (entry.extractBuildings != null && entry.extractBuildings.Count > 0)
        {
            any = true;
            card.Add(Text("EX", UiLocale.T("codex.extract"), "label-caps"));
            var row = El("Extract", "codex-source");
            for (int i = 0; i < entry.extractBuildings.Count; i++)
                row.Add(BuildingChip(entry.extractBuildings[i]));
            row.Add(Text("Vein", UiLocale.T("codex.extract_node"), "caption"));
            card.Add(row);
        }

        if (entry.recipes != null && entry.recipes.Count > 0)
        {
            any = true;
            card.Add(Text("CR", UiLocale.T("codex.craft"), "label-caps"));
            for (int i = 0; i < entry.recipes.Count; i++)
            {
                RecipeData recipe = entry.recipes[i];
                if (recipe == null)
                    continue;
                var block = El("Recipe", "codex-recipe", "col");
                var io = El("IO", "row", "io-row");
                AddStacks(io, recipe.inputs);
                io.Add(Text("Arr", " → ", "gold"));
                AddStacks(io, recipe.outputs);
                block.Add(io);

                var where = El("Where", "codex-source");
                List<BuildingData> buildings = RecipeCodex.BuildingsOf(recipe);
                for (int b = 0; b < buildings.Count; b++)
                    where.Add(BuildingChip(buildings[b]));
                if (recipe.craftTime > 0f)
                    where.Add(Text("Time", UiLocale.T("codex.time", recipe.craftTime.ToString("0.##")), "caption"));
                block.Add(where);
                card.Add(block);
            }
        }

        if (!any)
            card.Add(Text("None", UiLocale.T("codex.none"), "muted"));

        UiTooltip.Bind(card, item != null ? item.Title : "—", item != null ? item.Info : "");
        return card;
    }

    public static VisualElement StorageCell()
    {
        var slot = El("Slot", "storage-slot");
        slot.Add(Icon(null, "card-icon"));
        slot.Add(Text("Count", "", "stack-count"));
        return slot;
    }

    public static void BindStorage(VisualElement slot, ItemStack stack)
    {
        if (slot == null)
            return;
        bool has = stack != null && !stack.IsEmpty && stack.item != null;
        SetIcon(slot.Q<Image>(), has ? stack.item.icon : null);
        Label count = slot.Q<Label>("Count");
        if (count != null)
            count.text = has ? stack.amount.ToString() : "";
    }

    static void AddClasses(VisualElement el, params string[] classes)
    {
        if (classes == null)
            return;
        for (int i = 0; i < classes.Length; i++)
        {
            if (!string.IsNullOrEmpty(classes[i]))
                el.AddToClassList(classes[i]);
        }
    }
}
