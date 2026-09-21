using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Подземный конвейер: вход и выход — пара. Между ними до 5 пустых клеток, только прямая.
/// Снос одного сносит второй.
/// </summary>
public class UndergroundConveyor : BuildingBase
{
    public bool isExit;

    UndergroundConveyor paired;
    int pairId;
    bool removingPair;

    static int nextPairId = 1;
    static readonly Dictionary<int, UndergroundConveyor> PendingPairs = new Dictionary<int, UndergroundConveyor>(32);

    public UndergroundConveyor Paired => paired;
    public int PairId => pairId;
    public Vector2Int Cell => BuildingLinker.WorldToCell(transform.position);
    public Vector2Int ForwardCell => BuildingLinker.ToCardinal(transform.forward);
    public static bool SuppressPairDestroy;

    public static void BeginLoad()
    {
        PendingPairs.Clear();
    }

    public static bool IsExitSave(BuildingSaveData save)
    {
        if (save == null)
            return false;
        if (ReadExtraInt(save, "exit") != 0)
            return true;
        return save.stateFloat >= 0.5f;
    }

    public static GameObject PrefabFor(BuildingData data, bool isExit)
    {
        if (data == null)
            return null;
        if (isExit && data.pairExitPrefab != null)
            return data.pairExitPrefab;
        return data.prefab;
    }

    public void SetPairMeta(bool exit, int id)
    {
        isExit = exit;
        pairId = Mathf.Max(0, id);
    }

    public static void BindPair(UndergroundConveyor entrance, UndergroundConveyor exit)
    {
        if (entrance == null || exit == null)
            return;

        int id = nextPairId++;
        entrance.isExit = false;
        exit.isExit = true;
        entrance.pairId = id;
        exit.pairId = id;
        entrance.paired = exit;
        exit.paired = entrance;
    }

    public override bool CanAcceptFrom(BuildingBase source)
    {
        if (isExit || paired == null || source == null || source == this)
            return false;

        Conveyor belt = source as Conveyor;
        if (belt != null)
            return belt.Cell + belt.ExitDir == Cell;

        Splitter splitter = source as Splitter;
        if (splitter != null)
            return BuildingLinker.FeedsInto(splitter, Cell);

        return BuildingLinker.FeedsInto(source, Cell) || BuildingLinker.HasInputFrom(this, source);
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (isExit || paired == null || item == null || item.isFluid)
            return false;
        if (!HasOutputSpace())
            return false;
        return TryReturnToOutput(item);
    }

    protected override bool TryPushToConnections(ItemData item)
    {
        if (!isExit || item == null)
            return false;

        BuildingBase dest = BuildingLinker.GetBuildingAt(Cell + ForwardCell);
        if (TryGiveFromExit(dest, item))
            return true;
        return base.TryPushToConnections(item);
    }

    bool TryGiveFromExit(BuildingBase dest, ItemData item)
    {
        if (dest == null || dest == this || dest == paired)
            return false;

        Conveyor belt = dest as Conveyor;
        if (belt != null)
        {
            if (item.isFluid)
                return false;
            return belt.TryAcceptTransfer(item, null, this);
        }

        Splitter splitter = dest as Splitter;
        if (splitter != null)
        {
            if (!splitter.CanAcceptFrom(this))
                return false;
            return splitter.TryAcceptTransfer(item, null);
        }

        if (!dest.CanAcceptFrom(this))
            return false;
        return dest.TryReceiveItem(item, null);
    }

    public override void OnRemoved()
    {
        UndergroundConveyor other = paired;
        paired = null;
        if (other != null)
            other.paired = null;

        base.OnRemoved();

        if (other != null && !removingPair && !SuppressPairDestroy)
        {
            removingPair = true;
            other.removingPair = true;
            other.OnRemoved();
            Destroy(other.gameObject);
        }
    }

    public override void OnRotated()
    {
        if (paired != null)
            return;
        base.OnRotated();
    }

    public override void WriteSave(BuildingSaveData save)
    {
        base.WriteSave(save);
        if (save == null)
            return;
        save.stateInt = pairId;
        save.stateFloat = isExit ? 1f : 0f;
        if (save.extras == null)
            save.extras = new List<SaveKeyValue>();
        save.extras.Add(new SaveKeyValue { key = "pair", value = pairId.ToString() });
        save.extras.Add(new SaveKeyValue { key = "exit", value = isExit ? "1" : "0" });
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        if (save == null)
            return;

        pairId = ReadExtraInt(save, "pair");
        if (pairId <= 0)
            pairId = save.stateInt;
        int exitFlag = ReadExtraInt(save, "exit");
        isExit = exitFlag != 0 || save.stateFloat >= 0.5f;
        if (pairId >= nextPairId)
            nextPairId = pairId + 1;

        if (OutputBufferCount > 0)
            WorldSim.MarkFlush(this);

        if (pairId <= 0)
            return;

        if (PendingPairs.TryGetValue(pairId, out UndergroundConveyor other) && other != null)
        {
            PendingPairs.Remove(pairId);
            BindLoadedPair(this, other);
        }
        else
            PendingPairs[pairId] = this;
    }

    static void BindLoadedPair(UndergroundConveyor a, UndergroundConveyor b)
    {
        if (a == null || b == null)
            return;

        UndergroundConveyor entrance = a.isExit ? b : a;
        UndergroundConveyor exit = a.isExit ? a : b;
        if (entrance.isExit && exit.isExit)
        {
            entrance = a;
            exit = b;
            entrance.isExit = false;
            exit.isExit = true;
        }
        else if (!entrance.isExit && !exit.isExit)
            exit.isExit = true;

        int id = Mathf.Max(a.pairId, b.pairId, 1);
        entrance.pairId = id;
        exit.pairId = id;
        entrance.paired = exit;
        exit.paired = entrance;
        WorldSim.MarkFlush(entrance);
        WorldSim.MarkFlush(exit);
    }

    public static void FinishLoad()
    {
        UndergroundConveyor[] all = Object.FindObjectsByType<UndergroundConveyor>(FindObjectsSortMode.None);
        var byPair = new Dictionary<int, UndergroundConveyor>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            UndergroundConveyor tunnel = all[i];
            if (tunnel == null)
                continue;
            if (tunnel.paired != null || tunnel.pairId <= 0)
                continue;
            if (byPair.TryGetValue(tunnel.pairId, out UndergroundConveyor other) && other != null)
            {
                byPair.Remove(tunnel.pairId);
                BindLoadedPair(tunnel, other);
            }
            else
                byPair[tunnel.pairId] = tunnel;
        }

        for (int i = 0; i < all.Length; i++)
        {
            UndergroundConveyor entrance = all[i];
            if (entrance == null || entrance.paired != null)
                continue;
            UndergroundConveyor exit = FindFacingPartner(entrance);
            if (exit != null)
                BindLoadedPair(entrance, exit);
        }

        PendingPairs.Clear();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].OutputBufferCount > 0)
                WorldSim.MarkFlush(all[i]);
        }
    }

    static UndergroundConveyor FindFacingPartner(UndergroundConveyor self)
    {
        if (self == null)
            return null;

        Vector2Int forward = self.ForwardCell;
        if (forward.x == 0 && forward.y == 0)
            return null;

        UndergroundConveyor ahead = WalkUnpaired(self, forward);
        if (ahead != null && ahead.ForwardCell == forward)
        {
            self.isExit = false;
            ahead.isExit = true;
            AssignPairId(self, ahead);
            return ahead;
        }

        UndergroundConveyor behind = WalkUnpaired(self, new Vector2Int(-forward.x, -forward.y));
        if (behind != null && behind.ForwardCell == forward)
        {
            behind.isExit = false;
            self.isExit = true;
            AssignPairId(behind, self);
            return behind;
        }

        return null;
    }

    static UndergroundConveyor WalkUnpaired(UndergroundConveyor from, Vector2Int step)
    {
        Vector2Int cell = from.Cell;
        int maxGap = from.data != null ? Mathf.Max(1, from.data.pairMaxGap) : 5;
        for (int n = 1; n <= maxGap + 1; n++)
        {
            BuildingBase found = BuildingLinker.GetBuildingAt(cell + step * n);
            UndergroundConveyor other = found as UndergroundConveyor;
            if (other == null || other == from || other.paired != null)
                continue;
            return other;
        }

        return null;
    }

    static void AssignPairId(UndergroundConveyor a, UndergroundConveyor b)
    {
        int id = Mathf.Max(a.pairId, b.pairId);
        if (id <= 0)
            id = nextPairId++;
        a.pairId = id;
        b.pairId = id;
    }

    static int ReadExtraInt(BuildingSaveData save, string key)
    {
        if (save.extras == null)
            return 0;
        for (int i = 0; i < save.extras.Count; i++)
        {
            SaveKeyValue row = save.extras[i];
            if (row == null || row.key != key)
                continue;
            int value;
            if (int.TryParse(row.value, out value))
                return value;
        }

        return 0;
    }

    public bool TryAcceptFromPair(ItemData item)
    {
        if (item == null)
            return false;
        return TryOutputToAny(item);
    }

    public override void SimFlush()
    {
        if (!isExit)
            FlushToPair();
        else
            base.SimFlush();
    }

    void FlushToPair()
    {
        if (paired == null)
            return;

        while (OutputBufferCount > 0)
        {
            ItemData item;
            if (!TryStealFromOutput(null, out item))
                break;
            if (paired.TryAcceptFromPair(item))
                continue;
            if (!TryReturnToOutput(item))
                break;
            break;
        }
    }
}
