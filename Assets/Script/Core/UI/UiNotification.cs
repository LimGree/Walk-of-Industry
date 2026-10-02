using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Категория тоста — для фильтра уведомлений в настройках.</summary>
public enum NotifyKind
{
    General,
    Breakdown,
    Research,
    Achievement,
    Resources
}

public static class UiNotification
{
    const int LifeMs = 3200;

    static VisualElement host;
    static VisualElement boundHost;
    static VisualElement freeHost;
    static float lastResourcesAt = -99f;

    public static bool Allowed(NotifyKind kind)
    {
        switch (kind)
        {
            case NotifyKind.Breakdown:
                return GameSettings.NotifyBreakdowns;
            case NotifyKind.Research:
                return GameSettings.NotifyResearch;
            case NotifyKind.Achievement:
                return GameSettings.NotifyAchievements;
            case NotifyKind.Resources:
                return GameSettings.NotifyResources;
            default:
                return true;
        }
    }

    public static void Push(NotifyKind kind, string heading, string detail, UiStatus status = UiStatus.Neutral)
    {
        if (!Allowed(kind))
            return;
        if (kind == NotifyKind.Resources)
        {
            // Не спамить при протяжке ленты без денег.
            if (Time.unscaledTime - lastResourcesAt < 2.5f)
                return;
            lastResourcesAt = Time.unscaledTime;
        }
        Push(heading, detail, status);
    }

    public static void BindHost(VisualElement el)
    {
        boundHost = el;
        host = el;
    }

    public static void UnbindHost(VisualElement el)
    {
        if (boundHost != el)
            return;
        boundHost = null;
        host = null;
    }

    public static void Push(string heading, string detail, UiStatus status = UiStatus.Neutral)
    {
        Ensure();
        if (host == null)
            return;
        var toast = IndustryUi.El("Toast", "toast");
        if (status == UiStatus.Completed || status == UiStatus.Ready)
            toast.AddToClassList("toast-ok");
        else if (status == UiStatus.Warning)
            toast.AddToClassList("toast-warn");
        else if (status == UiStatus.Error)
            toast.AddToClassList("toast-error");
        toast.Add(IndustryUi.Text("T", heading ?? "", "toast-title"));
        if (!string.IsNullOrEmpty(detail))
            toast.Add(IndustryUi.Text("B", detail, "toast-body"));
        host.Insert(0, toast);
        toast.schedule.Execute(() =>
        {
            toast.RemoveFromHierarchy();
        }).StartingIn(LifeMs);
        while (host.childCount > 4)
            host.RemoveAt(host.childCount - 1);
    }

    static void Ensure()
    {
        int pos = GameSettings.NotifyPosition;
        if (pos != 0)
        {
            VisualElement hostRoot = UiRuntime.HostRoot;
            if (freeHost == null || freeHost.panel == null)
            {
                if (hostRoot == null)
                    return;
                freeHost = IndustryUi.El("ToastsFree", "toast-host");
                freeHost.pickingMode = PickingMode.Ignore;
                hostRoot.Add(freeHost);
            }
            freeHost.EnableInClassList("toast-pos-br", pos == 1);
            freeHost.EnableInClassList("toast-pos-tc", pos == 2);
            host = freeHost;
            return;
        }
        if (boundHost != null && boundHost.panel != null)
        {
            host = boundHost;
            return;
        }
        if (host != null && host.panel != null)
            return;
        VisualElement root = UiRuntime.HostRoot;
        if (root == null)
            return;
        host = IndustryUi.El("Toasts", "toast-host");
        host.pickingMode = PickingMode.Ignore;
        root.Add(host);
    }
}
