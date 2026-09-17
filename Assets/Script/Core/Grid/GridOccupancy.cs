using System.Collections.Generic;
using UnityEngine;

/// <summary>Занятость клеток сетки.</summary>
public static class GridOccupancy
{
    private static readonly Dictionary<Vector2Int, GameObject> occupiedCells =
        new Dictionary<Vector2Int, GameObject>();

    private static readonly Dictionary<GameObject, List<Vector2Int>> objectCells =
        new Dictionary<GameObject, List<Vector2Int>>();

    private static readonly Dictionary<Vector2Int, object> reservedCells =
        new Dictionary<Vector2Int, object>();

    private static readonly Dictionary<object, List<Vector2Int>> reservationCells =
        new Dictionary<object, List<Vector2Int>>();

    public static void CollectOccupants(List<GameObject> results)
    {
        if (results == null)
            return;
        results.Clear();
        foreach (var pair in objectCells)
        {
            if (pair.Key != null)
                results.Add(pair.Key);
        }
    }

    public static bool IsCellFree(Vector2Int cell)
    {
        return IsCellFree(cell, null, null);
    }

    public static bool IsCellFree(
        Vector2Int cell,
        object ignoreReservation,
        HashSet<GameObject> ignoreOccupants)
    {
        if (occupiedCells.TryGetValue(cell, out GameObject obj))
        {
            if (obj == null)
                occupiedCells.Remove(cell);
            else if (ignoreOccupants == null || !ignoreOccupants.Contains(obj))
                return false;
        }

        if (reservedCells.TryGetValue(cell, out object token)
            && token != null
            && token != ignoreReservation)
            return false;

        return true;
    }

    public static bool IsAreaFree(Vector2Int origin, Vector2Int size)
    {
        return IsAreaFree(origin, size, null, null);
    }

    public static bool IsAreaFree(
        Vector2Int origin,
        Vector2Int size,
        object ignoreReservation,
        HashSet<GameObject> ignoreOccupants)
    {
        size = NormalizeSize(size);

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                if (!IsCellFree(origin + new Vector2Int(x, y), ignoreReservation, ignoreOccupants))
                    return false;
            }
        }

        return true;
    }

    public static void Reserve(object token, List<Vector2Int> cells)
    {
        if (token == null)
            return;

        Release(token);
        if (cells == null || cells.Count == 0)
            return;

        var owned = new List<Vector2Int>(cells.Count);
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            reservedCells[cell] = token;
            owned.Add(cell);
        }
        reservationCells[token] = owned;
    }

    public static void Release(object token)
    {
        if (token == null)
            return;
        if (!reservationCells.TryGetValue(token, out List<Vector2Int> cells))
            return;

        for (int i = 0; i < cells.Count; i++)
        {
            if (reservedCells.TryGetValue(cells[i], out object owner) && owner == token)
                reservedCells.Remove(cells[i]);
        }
        reservationCells.Remove(token);
    }

    public static bool TryGetCells(GameObject obj, List<Vector2Int> results)
    {
        if (results == null)
            return false;
        results.Clear();
        if (obj == null || !objectCells.TryGetValue(obj, out List<Vector2Int> cells))
            return false;

        results.AddRange(cells);
        return cells.Count > 0;
    }

    public static void Register(GameObject obj, Vector2Int cell)
    {
        Register(obj, cell, Vector2Int.one);
    }

    public static void Register(GameObject obj, Vector2Int origin, Vector2Int size)
    {
        if (obj == null)
            return;

        Unregister(obj);

        size = NormalizeSize(size);
        var cells = new List<Vector2Int>(size.x * size.y);

        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                Vector2Int cell = origin + new Vector2Int(x, y);

                if (occupiedCells.TryGetValue(cell, out GameObject existing)
                    && existing != null && existing != obj)
                {
                    Debug.LogWarning(
                        $"[GridOccupancy] Cell {cell} already occupied by {existing.name}, " +
                        $"overwriting with {obj.name}");
                }

                occupiedCells[cell] = obj;
                cells.Add(cell);
            }
        }

        objectCells[obj] = cells;
    }

    public static void Unregister(GameObject obj)
    {
        if (obj == null)
            return;

        if (!objectCells.TryGetValue(obj, out List<Vector2Int> cells))
            return;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int cell = cells[i];
            if (occupiedCells.TryGetValue(cell, out GameObject owner) && owner == obj)
                occupiedCells.Remove(cell);
        }

        objectCells.Remove(obj);
    }

    public static void Unregister(Vector2Int cell)
    {
        if (!occupiedCells.TryGetValue(cell, out GameObject obj))
            return;

        if (obj != null)
            Unregister(obj);
        else
            occupiedCells.Remove(cell);
    }

    public static GameObject GetAt(Vector2Int cell)
    {
        if (!occupiedCells.TryGetValue(cell, out GameObject obj))
            return null;

        if (obj == null)
        {
            occupiedCells.Remove(cell);
            return null;
        }

        return obj;
    }

    public static void CollectAllOccupiedObjects(List<GameObject> results, HashSet<int> seen)
    {
        if (results == null)
            return;

        if (seen == null)
            seen = new HashSet<int>();

        var keys = new List<GameObject>(objectCells.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            GameObject obj = keys[i];
            if (obj == null) continue;
            if (!objectCells.ContainsKey(obj)) continue;
            int id = obj.GetInstanceID();
            if (!seen.Add(id)) continue;
            results.Add(obj);
        }
    }

    public static Vector2Int GetRotatedSize(Vector2Int size, float rotationY)
    {
        size = NormalizeSize(size);
        int quarter = Mathf.RoundToInt(rotationY / 90f) % 4;
        if (quarter < 0) quarter += 4;

        if (quarter == 1 || quarter == 3)
            return new Vector2Int(size.y, size.x);

        return size;
    }

    public static Vector2Int NormalizeSize(Vector2Int size)
    {
        return new Vector2Int(Mathf.Max(1, size.x), Mathf.Max(1, size.y));
    }

    public static void ClearAll()
    {
        occupiedCells.Clear();
        objectCells.Clear();
        reservedCells.Clear();
        reservationCells.Clear();
    }
}
