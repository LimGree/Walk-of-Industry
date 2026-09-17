using UnityEngine.UIElements;

public enum UiStatus
{
    Neutral,
    Running,
    Ready,
    Warning,
    Error,
    Locked,
    Completed
}

public static class UiStatusUtil
{
    static readonly string[] Classes =
    {
        "status-running",
        "status-ready",
        "status-warning",
        "status-error",
        "status-locked",
        "status-completed"
    };

    public static string Label(UiStatus status)
    {
        switch (status)
        {
            case UiStatus.Running: return "RUNNING";
            case UiStatus.Ready: return "READY";
            case UiStatus.Warning: return "WARNING";
            case UiStatus.Error: return "ERROR";
            case UiStatus.Locked: return "LOCKED";
            case UiStatus.Completed: return "DONE";
            default: return "";
        }
    }

    public static string BadgeClass(UiStatus status)
    {
        switch (status)
        {
            case UiStatus.Running: return "badge-running";
            case UiStatus.Ready: return "badge-ready";
            case UiStatus.Warning: return "badge-warn";
            case UiStatus.Error: return "badge-error";
            case UiStatus.Locked: return "badge-locked";
            case UiStatus.Completed: return "badge-done";
            default: return "badge";
        }
    }

    public static void Apply(VisualElement element, UiStatus status)
    {
        if (element == null)
            return;
        for (int i = 0; i < Classes.Length; i++)
            element.RemoveFromClassList(Classes[i]);
        string cls = StatusClass(status);
        if (!string.IsNullOrEmpty(cls))
            element.AddToClassList(cls);
    }

    public static string StatusClass(UiStatus status)
    {
        switch (status)
        {
            case UiStatus.Running: return "status-running";
            case UiStatus.Ready: return "status-ready";
            case UiStatus.Warning: return "status-warning";
            case UiStatus.Error: return "status-error";
            case UiStatus.Locked: return "status-locked";
            case UiStatus.Completed: return "status-completed";
            default: return "";
        }
    }
}
