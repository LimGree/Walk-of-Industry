using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// «Текстовые сигналы звуков» (доступность): короткая подпись над хотбаром, со стрелкой в сторону источника.
/// Показывается только при включённой настройке Captions.
/// </summary>
public static class SoundCaptions
{
    const int LifeMs = 3000;
    const int MaxLines = 4;

    static VisualElement host;

    static readonly Dictionary<string, string> WorldKeys = new Dictionary<string, string>
    {
        { "world/world_demolish", "caption.demolish" },
        { "world/world_upgrade", "caption.upgrade" },
        { "world/world_invalid", "caption.invalid" },
    };

    public static void Build(VisualElement root)
    {
        if (root == null)
            return;
        host = IndustryUi.El("Captions", "caption-host");
        host.pickingMode = PickingMode.Ignore;
        root.Add(host);
    }

    /// <summary>Хук из GameAudio.PlayAt: подписываем только значимые звуки мира.</summary>
    public static void OnWorldSound(string key, Vector3 position)
    {
        if (!GameSettings.Captions || key == null)
            return;
        if (WorldKeys.TryGetValue(key, out string loc))
            Show(loc, position);
    }

    public static void Show(string locKey, Vector3? source)
    {
        if (!GameSettings.Captions || host == null)
            return;
        string text = UiLocale.T(locKey);
        string dir = source.HasValue ? Direction(source.Value) : "";
        var line = IndustryUi.El("Line", "caption-line");
        line.pickingMode = PickingMode.Ignore;
        if (!string.IsNullOrEmpty(dir))
            line.Add(IndustryUi.Text("Dir", dir, "caption-dir"));
        line.Add(IndustryUi.Text("T", text, "caption-text"));
        host.Add(line);
        while (host.childCount > MaxLines)
            host.RemoveAt(0);
        line.schedule.Execute(() => line.RemoveFromHierarchy()).StartingIn(LifeMs);
    }

    /// <summary>Стрелка относительно взгляда камеры; рядом с игроком — без стрелки.</summary>
    static string Direction(Vector3 world)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return "";
        Vector3 to = world - cam.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 9f)
            return "";
        Vector3 fwd = cam.transform.forward;
        fwd.y = 0f;
        float angle = Vector3.SignedAngle(fwd, to, Vector3.up);
        if (angle > -45f && angle < 45f)
            return "↑";
        if (angle >= 45f && angle <= 135f)
            return "→";
        if (angle <= -45f && angle >= -135f)
            return "←";
        return "↓";
    }
}
