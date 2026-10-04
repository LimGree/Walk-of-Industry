using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Подсказка маршрута ленты: путь по свободным клеткам от выхода одного здания до входа другого (A*, 4 стороны).
/// Только для показа — ничего не строит. Клетки с жилой дороже (лента там встаёт, но лучше обойти),
/// вода — запрещена.
/// </summary>
public static class BeltRouteHint
{
    const int MaxNodes = 6000;
    const int Margin = 12;

    static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1)
    };

    /// <summary>Маршрут от любого выхода <paramref name="from"/> до любого входа <paramref name="to"/>.</summary>
    public static bool Between(BuildingBase from, BuildingBase to, List<Vector2Int> path)
    {
        path.Clear();
        if (from == null || to == null || from.outputSockets == null || to.inputSockets == null)
            return false;

        var starts = new List<Vector2Int>(4);
        for (int i = 0; i < from.outputSockets.Length; i++)
        {
            if (from.outputSockets[i] != null)
                starts.Add(BuildingLinker.GetSocketFrontCell(from.outputSockets[i]));
        }

        var goals = new HashSet<Vector2Int>();
        for (int i = 0; i < to.inputSockets.Length; i++)
        {
            if (to.inputSockets[i] != null)
                goals.Add(BuildingLinker.GetSocketFrontCell(to.inputSockets[i]));
        }

        if (starts.Count == 0 || goals.Count == 0)
            return false;

        List<Vector2Int> best = null;
        var tmp = new List<Vector2Int>(64);
        for (int i = 0; i < starts.Count; i++)
        {
            if (!Find(starts[i], goals, tmp))
                continue;
            if (best == null || tmp.Count < best.Count)
                best = new List<Vector2Int>(tmp);
        }

        if (best == null)
            return false;
        path.AddRange(best);
        return true;
    }

    public static bool Find(Vector2Int start, HashSet<Vector2Int> goals, List<Vector2Int> path)
    {
        path.Clear();
        if (goals.Contains(start))
        {
            path.Add(start);
            return true;
        }

        // Рамка поиска: старт + цели + запас.
        int minX = start.x, maxX = start.x, minY = start.y, maxY = start.y;
        foreach (Vector2Int g in goals)
        {
            minX = Mathf.Min(minX, g.x);
            maxX = Mathf.Max(maxX, g.x);
            minY = Mathf.Min(minY, g.y);
            maxY = Mathf.Max(maxY, g.y);
        }

        minX -= Margin;
        maxX += Margin;
        minY -= Margin;
        maxY += Margin;

        var open = new List<(int f, Vector2Int c)>(256);
        var g0 = new Dictionary<Vector2Int, int> { [start] = 0 };
        var came = new Dictionary<Vector2Int, Vector2Int>();
        open.Add((Heuristic(start, goals), start));
        int expanded = 0;

        while (open.Count > 0 && expanded++ < MaxNodes)
        {
            int bi = 0;
            for (int i = 1; i < open.Count; i++)
            {
                if (open[i].f < open[bi].f)
                    bi = i;
            }

            Vector2Int cur = open[bi].c;
            open.RemoveAt(bi);
            if (goals.Contains(cur))
            {
                Rebuild(came, cur, start, path);
                return true;
            }

            int gc = g0[cur];
            for (int d = 0; d < Dirs.Length; d++)
            {
                Vector2Int next = cur + Dirs[d];
                if (next.x < minX || next.x > maxX || next.y < minY || next.y > maxY)
                    continue;
                int step = StepCost(next, goals);
                if (step < 0)
                    continue;
                int ng = gc + step;
                if (g0.TryGetValue(next, out int old) && old <= ng)
                    continue;
                g0[next] = ng;
                came[next] = cur;
                open.Add((ng + Heuristic(next, goals), next));
            }
        }

        return false;
    }

    /// <summary>-1 — нельзя, иначе цена шага.</summary>
    static int StepCost(Vector2Int cell, HashSet<Vector2Int> goals)
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map != null && (map.IsOcean(cell) || map.IsWater(cell)))
            return -1;
        if (!GridOccupancy.IsCellFree(cell))
        {
            // Уже стоящая лента — можно идти по ней (игрок продолжит линию).
            return BuildingLinker.GetBuildingAt(cell) is Conveyor ? 1 : -1;
        }

        return ResourceNode.HasNode(cell) && !goals.Contains(cell) ? 5 : 1;
    }

    static int Heuristic(Vector2Int c, HashSet<Vector2Int> goals)
    {
        int best = int.MaxValue;
        foreach (Vector2Int g in goals)
            best = Mathf.Min(best, Mathf.Abs(g.x - c.x) + Mathf.Abs(g.y - c.y));
        return best;
    }

    static void Rebuild(Dictionary<Vector2Int, Vector2Int> came, Vector2Int end, Vector2Int start, List<Vector2Int> path)
    {
        Vector2Int c = end;
        path.Add(c);
        int guard = 0;
        while (c != start && came.TryGetValue(c, out Vector2Int prev) && guard++ < MaxNodes)
        {
            c = prev;
            path.Add(c);
        }

        path.Reverse();
    }
}
