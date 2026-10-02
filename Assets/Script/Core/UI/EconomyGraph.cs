using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// График экономики за последние минуты (<see cref="EconomyLedger"/>): столбики дохода вверх и расхода вниз
/// от средней линии, поверх — линия баланса монет со своим масштабом.
/// </summary>
public class EconomyGraph : VisualElement
{
    public const int Window = 30;

    static readonly Color Income = new Color(0.36f, 0.69f, 0.46f, 0.9f);
    static readonly Color Expense = new Color(0.73f, 0.28f, 0.27f, 0.9f);
    static readonly Color Balance = new Color(0.84f, 0.58f, 0.24f, 1f);
    static readonly Color Grid = new Color(1f, 1f, 1f, 0.06f);

    public EconomyGraph()
    {
        AddToClassList("econ-graph");
        generateVisualContent += Draw;
    }

    public void Refresh()
    {
        MarkDirtyRepaint();
    }

    void Draw(MeshGenerationContext ctx)
    {
        Rect r = contentRect;
        if (r.width < 4f || r.height < 4f)
            return;
        Painter2D p = ctx.painter2D;
        IReadOnlyList<EconomyLedger.Minute> all = EconomyLedger.Minutes;
        int count = Mathf.Min(Window, all.Count);
        int first = all.Count - count;

        float maxFlow = 1f;
        int minBal = int.MaxValue, maxBal = int.MinValue;
        for (int i = first; i < all.Count; i++)
        {
            maxFlow = Mathf.Max(maxFlow, all[i].CoinIncome, all[i].CoinExpense);
            minBal = Mathf.Min(minBal, all[i].coinsEnd);
            maxBal = Mathf.Max(maxBal, all[i].coinsEnd);
        }

        float mid = r.yMin + r.height * 0.5f;
        float half = r.height * 0.5f - 2f;

        // сетка
        p.strokeColor = Grid;
        p.lineWidth = 1f;
        for (int g = 1; g < 4; g++)
        {
            float y = r.yMin + r.height * g / 4f;
            p.BeginPath();
            p.MoveTo(new Vector2(r.xMin, y));
            p.LineTo(new Vector2(r.xMax, y));
            p.Stroke();
        }
        if (count == 0)
            return;

        float slot = r.width / Window;
        float bar = Mathf.Max(1f, slot * 0.7f);
        // Свежие минуты — справа.
        for (int i = 0; i < count; i++)
        {
            EconomyLedger.Minute m = all[first + i];
            float x = r.xMax - (count - i) * slot + (slot - bar) * 0.5f;
            float up = half * m.CoinIncome / maxFlow;
            float down = half * Mathf.Max(0, m.CoinExpense) / maxFlow;
            if (up > 0.5f)
                Rect(p, new Rect(x, mid - up, bar, up), Income);
            if (down > 0.5f)
                Rect(p, new Rect(x, mid, bar, down), Expense);
        }

        // баланс
        float span = Mathf.Max(1, maxBal - minBal);
        p.strokeColor = Balance;
        p.lineWidth = 2f;
        p.lineJoin = LineJoin.Round;
        p.BeginPath();
        for (int i = 0; i < count; i++)
        {
            EconomyLedger.Minute m = all[first + i];
            float x = r.xMax - (count - i) * slot + slot * 0.5f;
            float y = r.yMax - 3f - (r.height - 6f) * (m.coinsEnd - minBal) / span;
            if (i == 0)
                p.MoveTo(new Vector2(x, y));
            else
                p.LineTo(new Vector2(x, y));
        }
        p.Stroke();
    }

    static void Rect(Painter2D p, Rect rect, Color color)
    {
        p.fillColor = color;
        p.BeginPath();
        p.MoveTo(new Vector2(rect.xMin, rect.yMin));
        p.LineTo(new Vector2(rect.xMax, rect.yMin));
        p.LineTo(new Vector2(rect.xMax, rect.yMax));
        p.LineTo(new Vector2(rect.xMin, rect.yMax));
        p.ClosePath();
        p.Fill();
    }
}
