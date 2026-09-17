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
        return ReadExtraInt(save, "exit") != 0;
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
        isExit = ReadExtraInt(save, "exit") != 0;
        if (pairId >= nextPairId)
            nextPairId = pairId + 1;

        if (pairId <= 0)
            return;

        if (PendingPairs.TryGetValue(pairId, out UndergroundConveyor other) && other != null)
        {
            PendingPairs.Remove(pairId);
            paired = other;
            other.paired = this;
        }
        else
            PendingPairs[pairId] = this;
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
