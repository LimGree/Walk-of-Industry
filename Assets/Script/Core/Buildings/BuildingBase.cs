using System.Collections.Generic;
using UnityEngine;

public abstract class BuildingBase : MonoBehaviour
{
    [Header("Data")]
    public BuildingData data;

    [Header("Sockets")]
    public BuildingSocket[] inputSockets;
    public BuildingSocket[] outputSockets;

    [Header("Output buffer")]
    [Tooltip("Буфер выхода, если приёмник занят.")]
    public int maxOutputBuffer = 8;

    [Header("Footprint gizmo")]
    public bool drawFootprintGizmo = true;

    static readonly Vector2Int[] PushCardinals =
    {
        new Vector2Int(0, 1),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(-1, 0)
    };
    static readonly List<Vector2Int> PushCells = new List<Vector2Int>(16);

    private readonly Queue<ItemData> outputBuffer = new Queue<ItemData>();
    Renderer[] cullRenderers;
    Collider[] cullColliders;
    bool[] cullColliderOn;
    bool worldPlaced;
    bool worldShown = true;

    public int OutputBufferCount => outputBuffer.Count;
    public int OutputBufferFree => Mathf.Max(0, maxOutputBuffer - outputBuffer.Count);
    public bool IsPlaced => worldPlaced;

    public bool OutputContains(ItemData item)
    {
        if (item == null)
            return false;
        foreach (ItemData have in outputBuffer)
        {
            if (have == item)
                return true;
        }
        return false;
    }

    public Vector2Int FootprintSize
    {
        get
        {
            Vector2Int size = data != null ? data.size : Vector2Int.one;
            return GridFootprint.GetRotatedSize(size, transform.eulerAngles.y);
        }
    }

    public virtual void OnPlaced()
    {
        worldPlaced = true;
        RegisterOnGrid();
        WorldSim.RegisterBuilding(this);
        BuildingVisuals.ApplyPlaced(this, ReadLevel());
        if (!BuildingLinker.SuppressRelink)
            BuildingLinker.RelinkAround(this);
        AchievementSystem.NotifyPlaced(this);
    }

    public virtual void OnRemoved()
    {
        worldPlaced = false;
        WorldSim.UnregisterBuilding(this);
        var around = new List<Vector2Int>(8);
        BuildingLinker.CollectFootprintCells(this, around);
        DisconnectAllSockets();
        GridOccupancy.Unregister(gameObject);
        if (!BuildingLinker.SuppressRelink)
            BuildingLinker.RefreshRemoved(around);
    }

    public virtual void OnRotated()
    {
        BuildingLinker.RelinkAround(this);
    }

    protected virtual void OnDestroy()
    {
        WorldSim.UnregisterBuilding(this);
        GridOccupancy.Unregister(gameObject);
    }

    public void ReRegisterOnGrid()
    {
        GridOccupancy.Unregister(gameObject);
        RegisterOnGrid();
        WorldSim.RegisterBuilding(this);
    }

    protected void RegisterOnGrid()
    {
        if (GridSystem.Instance == null)
            return;

        GridFootprint.Register(gameObject, transform.position, FootprintSize);
    }

    void DisconnectAllSockets()
    {
        if (inputSockets != null)
        {
            for (int i = 0; i < inputSockets.Length; i++)
            {
                if (inputSockets[i] != null)
                    inputSockets[i].DisconnectAll();
            }
        }

        if (outputSockets != null)
        {
            for (int i = 0; i < outputSockets.Length; i++)
            {
                if (outputSockets[i] != null)
                    outputSockets[i].DisconnectAll();
            }
        }
    }

    public virtual bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        return false;
    }

    /// <summary>
    /// Можно ли принять предмет от этого соседа: только со стороны входного сокета.
    /// </summary>
    public virtual bool CanAcceptFrom(BuildingBase source)
    {
        return BuildingLinker.HasInputFrom(this, source);
    }

    public bool HasOutputSpace(int count = 1)
    {
        return count <= 0 || outputBuffer.Count + count <= maxOutputBuffer;
    }

    public bool TryStealFromOutput(ItemData filter, out ItemData item)
    {
        item = null;
        if (outputBuffer.Count == 0)
            return false;

        ItemData front = outputBuffer.Peek();
        if (filter != null && front != filter)
            return false;

        item = outputBuffer.Dequeue();
        return true;
    }

    public bool TryReturnToOutput(ItemData item)
    {
        if (item == null || outputBuffer.Count >= maxOutputBuffer)
            return false;
        outputBuffer.Enqueue(item);
        WorldSim.MarkFlush(this);
        return true;
    }

    protected bool TryOutputToAny(ItemData item)
    {
        if (item == null) return false;

        if (TryPushToConnections(item))
            return true;

        if (outputBuffer.Count < maxOutputBuffer)
        {
            outputBuffer.Enqueue(item);
            WorldSim.MarkFlush(this);
            return true;
        }

        return false;
    }

    protected virtual bool TryPushToConnections(ItemData item)
    {
        if (item == null)
            return false;

        if (outputSockets != null)
        {
            for (int i = 0; i < outputSockets.Length; i++)
            {
                BuildingSocket socket = outputSockets[i];
                if (socket == null)
                    continue;

                if (socket.connectedSocket != null)
                {
                    BuildingBase linked = socket.connectedSocket.GetComponentInParent<BuildingBase>();
                    if (linked != null
                        && linked.CanAcceptFrom(this)
                        && linked.TryReceiveItem(item, socket.connectedSocket))
                        return true;
                }

                if (TryGiveTo(BuildingLinker.GetBuildingAt(BuildingLinker.GetSocketFrontCell(socket)), item, socket))
                    return true;

                Vector2Int dir = BuildingLinker.SocketWorldCardinal(socket);
                if (dir.x == 0 && dir.y == 0)
                    continue;

                GridFootprint.CollectCells(transform.position, FootprintSize, PushCells);
                for (int c = 0; c < PushCells.Count; c++)
                {
                    if (TryGiveTo(BuildingLinker.GetBuildingAt(PushCells[c] + dir), item, socket))
                        return true;
                }
            }
        }

        return TryPushToAdjacentBelts(item);
    }

    protected bool HasPushNeighbor()
    {
        GridFootprint.CollectCells(transform.position, FootprintSize, PushCells);
        for (int i = 0; i < PushCells.Count; i++)
        {
            for (int d = 0; d < PushCardinals.Length; d++)
            {
                BuildingBase other = BuildingLinker.GetBuildingAt(PushCells[i] + PushCardinals[d]);
                if (other != null && other != this)
                    return true;
            }
        }

        return false;
    }

    protected bool TryPushToAdjacentBelts(ItemData item)
    {
        if (item == null)
            return false;

        GridFootprint.CollectCells(transform.position, FootprintSize, PushCells);
        for (int i = 0; i < PushCells.Count; i++)
        {
            for (int d = 0; d < PushCardinals.Length; d++)
            {
                BuildingBase other = BuildingLinker.GetBuildingAt(PushCells[i] + PushCardinals[d]);
                if (TryGiveTo(other, item, null))
                    return true;
            }
        }

        return false;
    }

    bool TryGiveTo(BuildingBase dest, ItemData item, BuildingSocket fromSocket)
    {
        if (dest == null || dest == this || item == null)
            return false;

        Conveyor belt = dest as Conveyor;
        if (belt != null)
        {
            bool pipe = belt is Pipe;
            if (item.isFluid != pipe)
                return false;
            if (fromSocket != null)
            {
                if (BuildingLinker.GetSocketFrontCell(fromSocket) != belt.Cell)
                    return false;
            }
            else if (!BuildingLinker.FeedsInto(this, belt.Cell))
                return false;
            return belt.TryAcceptTransfer(item, null, this);
        }

        if (item.isFluid)
            return false;

        Splitter splitter = dest as Splitter;
        if (splitter != null)
        {
            if (!splitter.CanAcceptFrom(this))
                return false;
            return splitter.TryAcceptTransfer(item, null);
        }

        if (!dest.CanAcceptFrom(this))
            return false;
        return dest.TryReceiveItem(item, fromSocket);
    }

    protected void FlushOutputBuffer()
    {
        while (outputBuffer.Count > 0)
        {
            ItemData item = outputBuffer.Peek();
            if (!TryPushToConnections(item))
                break;
            outputBuffer.Dequeue();
        }
    }

    public virtual int ReadLevel()
    {
        return 1;
    }

    public virtual bool CanUpgradeBuilding => false;

    public virtual bool TryUpgradeBuilding()
    {
        return false;
    }

    public virtual void ApplyLevel(int level)
    {
    }

    public virtual void WriteSave(BuildingSaveData save)
    {
        if (save == null)
            return;
        save.outputBuffer = SaveItems.FromQueue(outputBuffer);
    }

    public virtual void ReadSave(BuildingSaveData save)
    {
        if (save == null)
            return;
        SaveItems.ToQueue(save.outputBuffer, outputBuffer);
    }

    public virtual void SimFlush()
    {
        if (outputBuffer.Count > 0)
            FlushOutputBuffer();
    }

    public void CullTick(bool show)
    {
        ApplyWorldCull(show);
    }

    void ApplyWorldCull(bool show)
    {
        if (!worldPlaced)
            return;

        if (show == worldShown && cullRenderers != null)
            return;

        if (cullRenderers == null)
            CaptureCull();

        worldShown = show;
        if (cullRenderers != null)
        {
            for (int i = 0; i < cullRenderers.Length; i++)
            {
                Renderer rend = cullRenderers[i];
                if (rend == null || rend.GetComponentInParent<SocketArrow>() != null)
                    continue;
                rend.enabled = show;
            }
        }

        SocketArrow.RefreshOn(transform);

        if (cullColliders != null)
        {
            for (int i = 0; i < cullColliders.Length; i++)
            {
                if (cullColliders[i] != null)
                    cullColliders[i].enabled = show && cullColliderOn[i];
            }
        }
    }

    void CaptureCull()
    {
        cullRenderers = GetComponentsInChildren<Renderer>(true);
        cullColliders = GetComponentsInChildren<Collider>(true);
        cullColliderOn = new bool[cullColliders.Length];
        for (int i = 0; i < cullColliders.Length; i++)
            cullColliderOn[i] = cullColliders[i] != null && cullColliders[i].enabled;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (!drawFootprintGizmo)
            return;

        Vector2Int size = data != null ? data.size : Vector2Int.one;
        size = GridFootprint.GetRotatedSize(size, transform.eulerAngles.y);

        GridFootprint.DrawFootprintGizmo(transform.position, size, new Color(0.2f, 0.9f, 1f, 0.95f));
        GridFootprint.DrawCellQuadsGizmo(transform.position, size, new Color(0.2f, 1f, 0.4f, 0.55f));

        Vector3 worldSize = GridFootprint.GetWorldSize(size);
        float cell = GridFootprint.CellSize;
        Vector3 cubeSize = new Vector3(worldSize.x * 0.98f, cell * 0.5f, worldSize.z * 0.98f);
        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.35f);
        Gizmos.DrawWireCube(transform.position + Vector3.up * (cubeSize.y * 0.5f), cubeSize);
    }
#endif
}
