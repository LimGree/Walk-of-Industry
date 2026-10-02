using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Внешний вид интерфейса из настроек. Каждый корень IndustryUi.Mount получает классы темы:
/// цвет акцента (accent-*), анимации (anim-reduced / anim-off), контраст (hi-contrast),
/// палитру для дальтоников (cb-*). Переопределения токенов — в Industry.uss, секция UI LOOK.
/// Плюс прозрачность HUD для зарегистрированных элементов.
/// </summary>
public static class UiLook
{
    static readonly List<VisualElement> Roots = new List<VisualElement>();
    static readonly List<VisualElement> Huds = new List<VisualElement>();

    static readonly string[] AccentClasses = { "", "accent-blue", "accent-green", "accent-red" };
    static readonly string[] AnimClasses = { "", "anim-reduced", "anim-off" };
    static readonly string[] BlindClasses = { "", "cb-protan", "cb-deutan", "cb-tritan" };

    public static void Register(VisualElement root)
    {
        if (root == null)
            return;
        Roots.RemoveAll(r => r == null);
        if (!Roots.Contains(root))
            Roots.Add(root);
        ApplyTo(root);
    }

    /// <summary>Элемент HUD, чья прозрачность берётся из «Прозрачность HUD».</summary>
    public static void RegisterHud(VisualElement el)
    {
        if (el == null || Huds.Contains(el))
            return;
        Huds.Add(el);
        el.style.opacity = GameSettings.HudOpacity;
    }

    public static void ApplyAll()
    {
        for (int i = Roots.Count - 1; i >= 0; i--)
        {
            if (Roots[i] == null)
                Roots.RemoveAt(i);
            else
                ApplyTo(Roots[i]);
        }
        float hud = GameSettings.HudOpacity;
        for (int i = Huds.Count - 1; i >= 0; i--)
        {
            if (Huds[i] == null)
                Huds.RemoveAt(i);
            else
                Huds[i].style.opacity = hud;
        }
    }

    static void ApplyTo(VisualElement root)
    {
        Pick(root, AccentClasses, GameSettings.AccentColor);
        Pick(root, AnimClasses, GameSettings.UiAnimations);
        Pick(root, BlindClasses, GameSettings.ColorblindMode);
        root.EnableInClassList("hi-contrast", GameSettings.HighContrast);
    }

    static void Pick(VisualElement root, string[] classes, int index)
    {
        for (int i = 1; i < classes.Length; i++)
            root.EnableInClassList(classes[i], i == index);
    }

    /// <summary>Цвета статусов для кода (карта, иконки), с учётом режима для дальтоников.</summary>
    public static Color Good => GameSettings.ColorblindMode switch
    {
        1 or 2 => new Color(0.25f, 0.55f, 1f),
        3 => new Color(0.2f, 0.75f, 0.75f),
        _ => new Color(0.36f, 0.69f, 0.46f)
    };

    public static Color Bad => GameSettings.ColorblindMode switch
    {
        1 or 2 => new Color(1f, 0.6f, 0.1f),
        3 => new Color(0.95f, 0.3f, 0.45f),
        _ => new Color(0.73f, 0.28f, 0.27f)
    };

    public static Color Warn => GameSettings.ColorblindMode switch
    {
        1 or 2 => new Color(0.95f, 0.9f, 0.3f),
        3 => new Color(1f, 0.55f, 0.6f),
        _ => new Color(0.84f, 0.64f, 0.25f)
    };
}
