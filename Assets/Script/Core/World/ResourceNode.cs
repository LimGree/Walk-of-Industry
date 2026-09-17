using System.Collections.Generic;
using UnityEngine;

public class ResourceNode : MonoBehaviour
{
    static readonly Dictionary<Vector2Int, ResourceNode> nodesByCell =
        new Dictionary<Vector2Int, ResourceNode>();

    [Header("What can be mined here")]
    public ItemData resource;

    [Header("Optional")]
    public bool infinite = true;
    public int remainingAmount = 9999;

    Vector2Int registeredCell;
    bool registered;

    public Vector2Int Cell => BuildingLinker.WorldToCell(transform.position);

    public static ResourceNode GetAt(Vector2Int cell)
    {
        if (!nodesByCell.TryGetValue(cell, out ResourceNode node))
            return null;
        if (node == null)
        {
            nodesByCell.Remove(cell);
            return null;
        }
        return node;
    }

    public static bool HasNode(Vector2Int cell)
    {
        return GetAt(cell) != null;
    }

    public static bool HasNodeInArea(Vector2Int origin, Vector2Int size)
    {
        size = GridOccupancy.NormalizeSize(size);
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                if (!HasNode(origin + new Vector2Int(x, y)))
                    return false;
            }
        }
        return true;
    }

    void OnEnable()
    {
        RegisterCell();
    }

    void OnDisable()
    {
        UnregisterCell();
    }

    public void RegisterCell()
    {
        UnregisterCell();
        registeredCell = Cell;
        if (nodesByCell.TryGetValue(registeredCell, out ResourceNode existing)
            && existing != null && existing != this)
        {
            Debug.LogWarning(
                $"[ResourceNode] Cell {registeredCell} already has {existing.name}, overwritten by {name}");
        }
        nodesByCell[registeredCell] = this;
        registered = true;
    }

    void UnregisterCell()
    {
        if (!registered)
            return;
        if (nodesByCell.TryGetValue(registeredCell, out ResourceNode owner) && owner == this)
            nodesByCell.Remove(registeredCell);
        registered = false;
    }

    public bool TryConsume(int amount = 1)
    {
        if (resource == null) return false;
        if (infinite) return true;

        if (remainingAmount < amount) return false;
        remainingAmount -= amount;
        return true;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, 0.6f);
    }
}