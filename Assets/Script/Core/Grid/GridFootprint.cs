using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Footprint на сетке: pivot = ЦЕНТР footprint.
/// size.x = клетки по X, size.y = клетки по Z.
/// </summary>
public static class GridFootprint
{
    public static float CellSize
    {
        get
        {
            if (GridSystem.Instance != null)
                return Mathf.Max(0.0001f, GridSystem.Instance.cellSize);
            return 1f;
        }
    }

    public static Vector3 GridOrigin
    {
        get
        {
            if (GridSystem.Instance != null)
                return GridSystem.Instance.origin;
            return Vector3.zero;
        }
    }

    public static Vector2Int NormalizeSize(Vector2Int size)
    {
        return GridOccupancy.NormalizeSize(size);
    }

    public static Vector2Int GetRotatedSize(Vector2Int size, float rotationY)
    {
        return GridOccupancy.GetRotatedSize(size, rotationY);
    }

    public static Vector3 SnapCenter(Vector3 worldPos, Vector2Int size)
    {
        size = NormalizeSize(size);
        float cell = CellSize;
        Vector3 o = GridOrigin;

        float cx = (worldPos.x - o.x) / cell;
        float cz = (worldPos.z - o.z) / cell;

        Vector2Int min = MinCellFromCenterCellSpace(cx, cz, size);
        float newCx = min.x + (size.x - 1) * 0.5f;
        float newCz = min.y + (size.y - 1) * 0.5f;

        return new Vector3(newCx * cell + o.x, worldPos.y, newCz * cell + o.z);
    }

    public static Vector2Int GetMinCell(Vector3 worldCenter, Vector2Int size)
    {
        size = NormalizeSize(size);
        float cell = CellSize;
        Vector3 o = GridOrigin;

        float cx = (worldCenter.x - o.x) / cell;
        float cz = (worldCenter.z - o.z) / cell;
        return MinCellFromCenterCellSpace(cx, cz, size);
    }

    static Vector2Int MinCellFromCenterCellSpace(float cx, float cz, Vector2Int size)
    {
        float minXf = cx - (size.x - 1) * 0.5f;
        float minZf = cz - (size.y - 1) * 0.5f;
        return new Vector2Int(Mathf.RoundToInt(minXf), Mathf.RoundToInt(minZf));
    }

    public static Vector3 MinCellToCenter(Vector2Int minCell, Vector2Int size, float y)
    {
        size = NormalizeSize(size);
        float cell = CellSize;
        Vector3 o = GridOrigin;
        float cx = minCell.x + (size.x - 1) * 0.5f;
        float cz = minCell.y + (size.y - 1) * 0.5f;
        return new Vector3(cx * cell + o.x, y, cz * cell + o.z);
    }

    public static Vector3 GetHalfExtents(Vector2Int size)
    {
        size = NormalizeSize(size);
        float cell = CellSize;
        return new Vector3(size.x * cell * 0.5f, 0f, size.y * cell * 0.5f);
    }

    public static Vector3 GetWorldSize(Vector2Int size)
    {
        size = NormalizeSize(size);
        float cell = CellSize;
        return new Vector3(size.x * cell, 0f, size.y * cell);
    }

    public static void CollectCells(Vector3 worldCenter, Vector2Int size, List<Vector2Int> results)
    {
        if (results == null) return;
        results.Clear();
        size = NormalizeSize(size);
        Vector2Int min = GetMinCell(worldCenter, size);
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
                results.Add(min + new Vector2Int(x, y));
        }
    }

    public static List<Vector2Int> GetOccupiedCells(GameObject obj)
    {
        var cells = new List<Vector2Int>();
        if (obj == null)
            return cells;

        BuildingBase building = obj.GetComponent<BuildingBase>();
        if (building != null)
        {
            CollectCells(obj.transform.position, building.FootprintSize, cells);
            return cells;
        }

        if (GridSystem.Instance != null)
        {
            Vector2Int cell = GridSystem.Instance.WorldToCell(obj.transform.position);
            cells.Add(cell);
        }

        return cells;
    }

    public static void Register(GameObject obj, Vector3 worldCenter, Vector2Int size)
    {
        if (obj == null) return;
        size = NormalizeSize(size);
        Vector2Int min = GetMinCell(worldCenter, size);
        GridOccupancy.Register(obj, min, size);
    }

    public static void DrawFootprintGizmo(Vector3 worldCenter, Vector2Int size, Color color, float yOffset = 0.05f)
    {
        size = NormalizeSize(size);
        Vector3 half = GetHalfExtents(size);
        float y = worldCenter.y + yOffset;

        Vector3 c = new Vector3(worldCenter.x, y, worldCenter.z);
        Vector3 a = c + new Vector3(-half.x, 0f, -half.z);
        Vector3 b = c + new Vector3( half.x, 0f, -half.z);
        Vector3 d = c + new Vector3( half.x, 0f,  half.z);
        Vector3 e = c + new Vector3(-half.x, 0f,  half.z);

        Gizmos.color = color;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, d);
        Gizmos.DrawLine(d, e);
        Gizmos.DrawLine(e, a);

        float m = Mathf.Min(half.x, half.z) * 0.25f;
        if (m < 0.05f) m = 0.05f;
        Gizmos.DrawLine(c + Vector3.left * m, c + Vector3.right * m);
        Gizmos.DrawLine(c + Vector3.back * m, c + Vector3.forward * m);
    }

    public static void DrawCellQuadsGizmo(Vector3 worldCenter, Vector2Int size, Color color, float yOffset = 0.02f)
    {
        size = NormalizeSize(size);
        float cell = CellSize;
        Vector3 o = GridOrigin;
        Vector2Int min = GetMinCell(worldCenter, size);
        float y = worldCenter.y + yOffset;
        float h = cell * 0.48f;

        Gizmos.color = color;
        for (int x = 0; x < size.x; x++)
        {
            for (int z = 0; z < size.y; z++)
            {
                float cx = (min.x + x) * cell + o.x;
                float cz = (min.y + z) * cell + o.z;
                Vector3 p = new Vector3(cx, y, cz);
                Vector3 a = p + new Vector3(-h, 0f, -h);
                Vector3 b = p + new Vector3( h, 0f, -h);
                Vector3 d = p + new Vector3( h, 0f,  h);
                Vector3 e = p + new Vector3(-h, 0f,  h);
                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(b, d);
                Gizmos.DrawLine(d, e);
                Gizmos.DrawLine(e, a);
            }
        }
    }
}
