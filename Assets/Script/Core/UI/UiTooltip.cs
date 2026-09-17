using UnityEngine;
using UnityEngine.UIElements;

public static class UiTooltip
{
    const int DelayMs = 280;
    const float Offset = 16f;

    static VisualElement layer;
    static Label title;
    static Label body;
    static Label shortcut;
    static IVisualElementScheduledItem pending;
    static VisualElement owner;

    public static void Bind(VisualElement element, string heading, string description, string key = null)
    {
        if (element == null)
            return;
        element.RegisterCallback<PointerEnterEvent>(_ => Arm(element, heading, description, key));
        element.RegisterCallback<PointerLeaveEvent>(_ => HideIfOwner(element));
        element.RegisterCallback<PointerDownEvent>(_ => Hide());
    }

    public static void Show(VisualElement from, string heading, string description, Vector2 panelPos, string key = null)
    {
        Attach(from);
        if (layer == null)
            return;
        title.text = heading ?? "";
        body.text = description ?? "";
        shortcut.text = key ?? "";
        IndustryUi.Show(title, !string.IsNullOrEmpty(heading));
        IndustryUi.Show(body, !string.IsNullOrEmpty(description));
        IndustryUi.Show(shortcut, !string.IsNullOrEmpty(key));
        layer.style.display = DisplayStyle.Flex;
        layer.style.opacity = 1f;
        Place(panelPos);
    }

    public static void Hide()
    {
        pending?.Pause();
        pending = null;
        owner = null;
        if (layer != null)
            layer.style.display = DisplayStyle.None;
    }

    static void Arm(VisualElement element, string heading, string description, string key)
    {
        owner = element;
        pending?.Pause();
        pending = element.schedule.Execute(() =>
        {
            if (owner != element)
                return;
            Vector2 pos = element.worldBound.max;
            Show(element, heading, description, pos, key);
        }).StartingIn(DelayMs);
    }

    static void HideIfOwner(VisualElement element)
    {
        if (owner == element)
            Hide();
    }

    static void Place(Vector2 panelPos)
    {
        if (layer == null || layer.parent == null)
            return;
        layer.schedule.Execute(() =>
        {
            if (layer.panel == null)
                return;
            float w = Mathf.Max(1f, layer.resolvedStyle.width);
            float h = Mathf.Max(1f, layer.resolvedStyle.height);
            float maxX = Mathf.Max(1f, layer.parent.resolvedStyle.width);
            float maxY = Mathf.Max(1f, layer.parent.resolvedStyle.height);
            float x = panelPos.x + Offset;
            float y = panelPos.y + Offset;
            if (x + w > maxX - 8f)
                x = panelPos.x - w - Offset;
            if (y + h > maxY - 8f)
                y = panelPos.y - h - Offset;
            layer.style.left = Mathf.Max(8f, x);
            layer.style.top = Mathf.Max(8f, y);
        });
    }

    static void Attach(VisualElement from)
    {
        VisualElement host = from != null ? RootOf(from) : null;
        if (host == null)
            return;
        VisualElement existing = host.Q("Tooltip");
        if (existing != null)
        {
            layer = existing;
            title = existing.Q<Label>("Tt");
            body = existing.Q<Label>("Tb");
            shortcut = existing.Q<Label>("Tk");
            return;
        }

        layer = IndustryUi.El("Tooltip", "ui-tooltip");
        layer.pickingMode = PickingMode.Ignore;
        title = IndustryUi.Text("Tt", "", "ui-tooltip__title");
        body = IndustryUi.Text("Tb", "", "ui-tooltip__body");
        shortcut = IndustryUi.Text("Tk", "", "ui-tooltip__key");
        layer.Add(title);
        layer.Add(body);
        layer.Add(shortcut);
        layer.style.display = DisplayStyle.None;
        host.Add(layer);
    }

    static VisualElement RootOf(VisualElement el)
    {
        VisualElement cur = el;
        while (cur != null && cur.parent != null && cur.parent.parent != null)
            cur = cur.parent;
        return cur;
    }
}
