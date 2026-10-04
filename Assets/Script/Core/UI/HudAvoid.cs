using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Карточки HUD (цель, подсказка, обучение) не перекрывают миникарту: она двигается и меняет размер,
/// поэтому место считаем каждый кадр. Пробуем обычное место, потом под миникартой, потом над ней.
/// </summary>
public static class HudAvoid
{
    const float Gap = 12f;

    /// <param name="right">прижать к правому краю (иначе к левому)</param>
    /// <param name="fromTop">отступ сверху (иначе снизу)</param>
    /// <param name="offset">обычный отступ от верха/низа, px панели</param>
    public static void Place(VisualElement card, bool right, bool fromTop, float offset, float inset = 24f)
    {
        if (card == null || card.panel == null || card.resolvedStyle.display == DisplayStyle.None)
            return;
        Rect screen = card.panel.visualTree.worldBound;
        float w = card.resolvedStyle.width;
        float h = card.resolvedStyle.height;
        if (screen.width < 1f || screen.height < 1f || float.IsNaN(w) || float.IsNaN(h))
            return;

        float x = right ? screen.width - inset - w : inset;
        float top = fromTop ? offset : screen.height - offset - h;

        if (WorldMapUI.Instance != null && WorldMapUI.Instance.TryGetMiniScreenRect(out Rect f))
        {
            var mini = new Rect(f.x * screen.width, f.y * screen.height, f.width * screen.width, f.height * screen.height);
            if (Overlaps(x, top, w, h, mini))
            {
                float below = mini.yMax + Gap;
                float above = mini.yMin - Gap - h;
                bool fitsBelow = below + h <= screen.height - inset;
                bool fitsAbove = above >= inset;
                // Ближайший к обычному месту вариант, который влезает в экран.
                if (fitsBelow && (!fitsAbove || Mathf.Abs(below - top) <= Mathf.Abs(above - top)))
                    top = below;
                else if (fitsAbove)
                    top = above;
                else
                    x = right ? mini.xMin - Gap - w : mini.xMax + Gap; // не влезло ни сверху, ни снизу — рядом сбоку
            }
        }

        card.style.position = Position.Absolute;
        card.style.top = top;
        card.style.bottom = StyleKeyword.Auto;
        card.style.left = x;
        card.style.right = StyleKeyword.Auto;
    }

    static bool Overlaps(float x, float y, float w, float h, Rect r)
    {
        return x < r.xMax && x + w > r.xMin && y < r.yMax && y + h > r.yMin;
    }
}
