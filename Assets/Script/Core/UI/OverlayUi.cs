using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class OverlayUi
{
    public const float PanelWidth = 1120f;
    public const float PanelHeight = 760f;

    public static GameObject CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        GameObject canvasGo = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(parent, false);
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvasGo;
    }

    public static Image CreateDim(Transform parent)
    {
        Image dim = UiTheme.AddImage(parent, "Dim", Vector2.zero, new Color(0.02f, 0.06f, 0.04f, 0.72f));
        dim.raycastTarget = true;
        RectTransform rt = dim.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return dim;
    }

    public static GameObject CreatePanel(Transform parent)
    {
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panel.transform.SetParent(parent, false);
        UiTheme.StylePanel(panel, new Vector2(PanelWidth, PanelHeight));
        return panel;
    }

    public static TextMeshProUGUI CreateHeader(Transform panel, Sprite icon, string title, System.Action onClose)
    {
        Image bar = UiTheme.AddImage(panel, "Header", Vector2.zero, UiTheme.Chip);
        RectTransform barRt = bar.rectTransform;
        barRt.anchorMin = new Vector2(0f, 1f);
        barRt.anchorMax = new Vector2(1f, 1f);
        barRt.pivot = new Vector2(0.5f, 1f);
        barRt.anchoredPosition = Vector2.zero;
        barRt.sizeDelta = new Vector2(0f, 84f);

        if (icon != null)
        {
            Image image = UiTheme.AddImage(bar.transform, "Icon", new Vector2(52f, 52f), Color.white);
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.sprite = icon;
            RectTransform iconRt = image.rectTransform;
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(48f, 0f);
            iconRt.sizeDelta = new Vector2(52f, 52f);
        }

        TextMeshProUGUI titleText = UiTheme.AddText(bar.transform, "Title", title, 32f, UiTheme.Accent);
        titleText.fontStyle = FontStyles.Bold;
        RectTransform titleRt = titleText.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 0f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = new Vector2(icon != null ? 88f : 28f, 0f);
        titleRt.offsetMax = new Vector2(-90f, 0f);

        Button close = CreateIconButton(bar.transform, "Close", "✕", new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(52f, 52f));
        UiTheme.StyleImage(close.GetComponent<Image>(), UiTheme.Danger);
        close.onClick.AddListener(() => onClose?.Invoke());
        return titleText;
    }

    public static Transform CreateBody(Transform panel, float top = 96f, float bottom = 24f)
    {
        GameObject body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(panel, false);
        RectTransform rt = body.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(24f, bottom);
        rt.offsetMax = new Vector2(-24f, -top);
        return body.transform;
    }

    public static Transform CreateScrollColumn(Transform parent, string name)
    {
        GameObject content = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(parent, false);
        RectTransform rt = content.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(8, 20, 8, 8);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        UiTheme.EnsureVerticalScroll(rt);
        return content.transform;
    }

    public static Image CreateSprite(Transform parent, string name, Sprite sprite, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;
        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = sprite != null;
        return image;
    }

    public static Button CreateIconButton(Transform parent, string name, string label, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        UiTheme.StyleImage(go.GetComponent<Image>(), UiTheme.Card);
        TextMeshProUGUI text = UiTheme.AddText(go.transform, "Label", label, 24f, Color.white);
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        RectTransform textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        return go.GetComponent<Button>();
    }

    public static void LayoutHeight(Component component, float height)
    {
        if (component == null)
            return;
        LayoutElement layout = component.GetComponent<LayoutElement>();
        if (layout == null)
            layout = component.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
    }
}
