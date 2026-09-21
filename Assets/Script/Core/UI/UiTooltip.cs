using System;
using UnityEngine;
using UnityEngine.UIElements;

public static class UiTooltip
{
    const int DelayMs = 180;
    const float Offset = 18f;

    static VisualElement layer;
    static Label title;
    static Label body;
    static Label shortcut;
    static IVisualElementScheduledItem pending;
    static VisualElement owner;

    public static void Bind(VisualElement element, string heading, string description, string key = null)
    {
        Bind(element, () => heading, () => description, () => key);
    }

    public static void Bind(VisualElement element, Func<string> heading, Func<string> description, Func<string> key = null)
    {
        if (element == null)
            return;
        element.RegisterCallback<PointerEnterEvent>(evt =>
            Arm(element, heading != null ? heading() : null, description != null ? description() : null,
                key != null ? key() : null, evt.position));
        element.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (owner == element && layer != null && layer.style.display == DisplayStyle.Flex)
                Place(evt.position);
        });
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
        layer.BringToFront();
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

    static void Arm(VisualElement element, string heading, string description, string key, Vector2 panelPos)
    {
        owner = element;
        pending?.Pause();
        pending = element.schedule.Execute(() =>
        {
            if (owner != element)
                return;
            Show(element, heading, description, panelPos, key);
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
        layer.style.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.96f);
        layer.style.borderTopWidth = 1;
        layer.style.borderBottomWidth = 1;
        layer.style.borderLeftWidth = 1;
        layer.style.borderRightWidth = 1;
        layer.style.borderTopColor = new Color(0.38f, 0.42f, 0.46f, 1f);
        layer.style.borderBottomColor = new Color(0.38f, 0.42f, 0.46f, 1f);
        layer.style.borderLeftColor = new Color(0.38f, 0.42f, 0.46f, 1f);
        layer.style.borderRightColor = new Color(0.38f, 0.42f, 0.46f, 1f);
        layer.style.paddingLeft = 12;
        layer.style.paddingRight = 12;
        layer.style.paddingTop = 8;
        layer.style.paddingBottom = 8;
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
        if (el != null && el.panel != null && el.panel.visualTree != null)
            return el.panel.visualTree;
        VisualElement cur = el;
        while (cur != null && cur.parent != null)
            cur = cur.parent;
        return cur;
    }
}
