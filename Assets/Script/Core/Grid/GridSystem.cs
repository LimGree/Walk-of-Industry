using UnityEngine;

public class GridSystem : MonoBehaviour
{
    public static GridSystem Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("Размер одной клетки в world units. 1 = 1 метр.")]
    public float cellSize = 1f;
    public Vector3 origin = Vector3.zero;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        GridOccupancy.ClearAll();
        EnsureComponent<BuildGridVisualizer>();
        EnsureComponent<WorldBiomeMap>();
        EnsureComponent<WorldResourceScatterer>();
        EnsureComponent<WorldSim>();
    }

    void EnsureComponent<T>() where T : Component
    {
        if (GetComponent<T>() == null)
            gameObject.AddComponent<T>();
    }

    /// <summary>Snap точки к ближайшему узлу сетки (центр 1×1 клетки).</summary>
    public Vector3 SnapToGrid(Vector3 worldPosition)
    {
        float x = Mathf.Round((worldPosition.x - origin.x) / cellSize) * cellSize + origin.x;
        float z = Mathf.Round((worldPosition.z - origin.z) / cellSize) * cellSize + origin.z;
        return new Vector3(x, worldPosition.y, z);
    }

    /// <summary>
    /// Snap центра footprint здания. size — клетки по X/Z (уже с учётом поворота).
    /// </summary>
    public Vector3 SnapFootprintCenter(Vector3 worldPosition, Vector2Int size)
    {
        return GridFootprint.SnapCenter(worldPosition, size);
    }

    public Vector2Int WorldToCell(Vector3 worldPosition)
    {
        int x = Mathf.RoundToInt((worldPosition.x - origin.x) / cellSize);
        int z = Mathf.RoundToInt((worldPosition.z - origin.z) / cellSize);
        return new Vector2Int(x, z);
    }

    /// <summary>Мир: центр клетки (узел сетки).</summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(
            cell.x * cellSize + origin.x,
            0f,
            cell.y * cellSize + origin.z
        );
    }

    public Vector3 GetCellCenter(Vector2Int cell, float y)
    {
        Vector3 world = CellToWorld(cell);
        world.y = y;
        return world;
    }

    public Vector3 SnapToCellCenter(Vector3 worldPosition)
    {
        Vector2Int cell = WorldToCell(worldPosition);
        return GetCellCenter(cell, worldPosition.y);
    }
}
