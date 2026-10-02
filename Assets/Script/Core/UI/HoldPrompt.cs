using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Плашка «Удерживай …» с полосой прогресса по центру экрана. Для опасных действий,
/// которые подтверждаются удержанием клавиши (время — настройка «Время удержания»).
/// </summary>
public static class HoldPrompt
{
    static VisualElement box;
    static Label text;
    static VisualElement fill;

    public static void Show(string message, float progress)
    {
        if (!Ensure())
            return;
        text.text = message;
        fill.style.width = Length.Percent(Mathf.Clamp01(progress) * 100f);
        IndustryUi.Show(box, true);
    }

    public static void Hide()
    {
        if (box != null)
            IndustryUi.Show(box, false);
    }

    static bool Ensure()
    {
        if (box != null && box.panel != null)
            return true;
        VisualElement root = UiRuntime.HostRoot;
        if (root == null)
            return false;
        box = IndustryUi.El("HoldPrompt", "panel", "hold-prompt");
        box.pickingMode = PickingMode.Ignore;
        text = IndustryUi.Text("T", "", "hold-text");
        var track = IndustryUi.El("Track", "hold-track");
        fill = IndustryUi.El("Fill", "hold-fill");
        track.Add(fill);
        box.Add(text);
        box.Add(track);
        root.Add(box);
        return true;
    }
}
