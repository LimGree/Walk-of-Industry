using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class UiTheme
{
    public static readonly Color Primary = Rgb(228, 232, 234);
    public static readonly Color Secondary = Rgb(168, 178, 184);
    public static readonly Color Accent = Rgb(214, 148, 62);
    public static readonly Color Highlight = Rgb(232, 168, 78);
    public static readonly Color Muted = Rgb(118, 130, 138);

    public static readonly Color Overlay = new Color(0.04f, 0.05f, 0.07f, 0.78f);
    public static readonly Color Panel = new Color(0.07f, 0.09f, 0.11f, 0.94f);
    public static readonly Color PanelEdge = Rgb(48, 58, 68);
    public static readonly Color Card = Rgb(18, 23, 29);
    public static readonly Color CardAlt = Rgb(24, 30, 38);
    public static readonly Color Chip = Rgb(14, 18, 23);
    public static readonly Color AccentDim = new Color(Accent.r, Accent.g, Accent.b, 0.16f);
    public static readonly Color Text = Primary;
    public static readonly Color TextDim = Secondary;
    public static readonly Color Ok = Rgb(92, 176, 118);
    public static readonly Color Warn = Rgb(214, 164, 64);
    public static readonly Color Locked = Rgb(82, 92, 98);
    public static readonly Color Danger = Rgb(186, 72, 68);

    static Sprite roundSprite;

    public static Sprite Round
    {
        get
        {
            if (roundSprite == null)
                roundSprite = CreateRoundSprite(64, 16);
            return roundSprite;
        }
    }

    public static void StyleImage(Image image, Color color, bool sliced = true)
    {
        if (image == null)
            return;
        image.sprite = Round;
        image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        image.pixelsPerUnitMultiplier = 2.2f;
    }

    public static void StylePanel(GameObject go, Vector2 size)
    {
        if (go == null)
            return;

        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;
        }

        Image image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();
        StyleImage(image, Panel);
        image.raycastTarget = true;
    }

    public static void StyleText(TextMeshProUGUI text, float size, Color color, FontStyles style = FontStyles.Normal)
    {
        if (text == null)
            return;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.richText = true;
    }

    public static void StyleSlider(Slider slider)
    {
        if (slider == null)
            return;

        Image bg = slider.GetComponent<Image>();
        if (bg != null)
            StyleImage(bg, Chip);

        if (slider.fillRect != null)
        {
            Image fill = slider.fillRect.GetComponent<Image>();
            if (fill != null)
                StyleImage(fill, Accent);
        }

        if (slider.handleRect != null)
            slider.handleRect.gameObject.SetActive(false);
    }

    public static ScrollRect EnsureVerticalScroll(RectTransform content)
    {
        if (content == null)
            return null;

        ScrollRect existing = content.GetComponentInParent<ScrollRect>();
        if (existing != null && existing.content == content)
            return existing;

        RectTransform originalParent = content.parent as RectTransform;
        if (originalParent == null)
            return null;

        GameObject view = new GameObject("ScrollView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        view.transform.SetParent(originalParent, false);
        view.transform.SetSiblingIndex(content.GetSiblingIndex());

        RectTransform viewRt = view.GetComponent<RectTransform>();
        viewRt.anchorMin = content.anchorMin;
        viewRt.anchorMax = content.anchorMax;
        viewRt.pivot = content.pivot;
        viewRt.anchoredPosition = content.anchoredPosition;
        viewRt.sizeDelta = content.sizeDelta;
        viewRt.offsetMin = content.offsetMin;
        viewRt.offsetMax = content.offsetMax;

        Image viewImage = view.GetComponent<Image>();
        viewImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewImage.raycastTarget = true;

        content.SetParent(viewRt, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 200f);
        content.offsetMin = new Vector2(0f, content.offsetMin.y);
        content.offsetMax = new Vector2(0f, 0f);

        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject bar = new GameObject("Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
        bar.transform.SetParent(viewRt, false);
        RectTransform barRt = bar.GetComponent<RectTransform>();
        barRt.anchorMin = new Vector2(1f, 0f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(1f, 1f);
        barRt.sizeDelta = new Vector2(12f, 0f);
        barRt.anchoredPosition = Vector2.zero;
        StyleImage(bar.GetComponent<Image>(), Card);

        GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handle.transform.SetParent(bar.transform, false);
        RectTransform handleRt = handle.GetComponent<RectTransform>();
        handleRt.anchorMin = Vector2.zero;
        handleRt.anchorMax = Vector2.one;
        handleRt.offsetMin = new Vector2(2f, 2f);
        handleRt.offsetMax = new Vector2(-2f, -2f);
        StyleImage(handle.GetComponent<Image>(), Accent);

        Scrollbar scrollbar = bar.GetComponent<Scrollbar>();
        scrollbar.handleRect = handleRt;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.targetGraphic = handle.GetComponent<Image>();

        ScrollRect scroll = view.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewRt;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 48f;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.inertia = true;

        return scroll;
    }

    public static void EnsureGrid(Transform parent, Vector2 cell, Vector2 spacing, int columns)
    {
        if (parent == null)
            return;

        GridLayoutGroup grid = parent.GetComponent<GridLayoutGroup>();
        if (grid == null)
            grid = parent.gameObject.AddComponent<GridLayoutGroup>();

        grid.cellSize = cell;
        grid.spacing = spacing;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.padding = new RectOffset(12, 12, 12, 12);
    }

    public static Image AddImage(Transform parent, string name, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        StyleImage(image, color);
        return image;
    }

    public static TextMeshProUGUI AddText(Transform parent, string name, string value, float size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        StyleText(text, size, color);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        return text;
    }

    static Color Rgb(int r, int g, int b)
    {
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }

    static Sprite CreateRoundSprite(int size, int radius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color32[] pixels = new Color32[size * size];
        float r = radius;
        float max = size - 1;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Min(x, max - x);
                float dy = Mathf.Min(y, max - y);
                float a = 1f;
                if (dx < r && dy < r)
                {
                    float dist = Vector2.Distance(new Vector2(dx, dy), new Vector2(r, r));
                    a = Mathf.Clamp01(r - dist + 0.5f);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
