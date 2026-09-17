using UnityEngine;
using UnityEngine.UIElements;

public static class UiMotion
{
    const int FadeMs = 120;

    public static void FadeIn(VisualElement element)
    {
        if (element == null)
            return;
        element.style.opacity = 1f;
        element.RemoveFromClassList("is-hidden");
    }

    public static void FadeOut(VisualElement element)
    {
        if (element == null)
            return;
        element.style.opacity = 0f;
        element.AddToClassList("is-hidden");
    }

    public static void ShowPanel(VisualElement element)
    {
        if (element == null)
            return;
        element.style.display = DisplayStyle.Flex;
        element.style.opacity = 0f;
        element.schedule.Execute(() =>
        {
            if (element.panel != null)
                element.style.opacity = 1f;
        }).StartingIn(16);
        UiAudio.PlayOpen();
    }

    public static void HidePanel(VisualElement element)
    {
        if (element == null)
            return;
        element.style.opacity = 0f;
        element.schedule.Execute(() =>
        {
            if (element.panel != null)
                element.style.display = DisplayStyle.None;
        }).StartingIn(FadeMs);
        UiAudio.PlayClose();
    }
}
