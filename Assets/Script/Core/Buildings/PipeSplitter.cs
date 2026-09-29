using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Трубный сплиттер 1×1: жидкость входит сзади (−Z), выходит по кругу вперёд, вправо и влево
/// в трубы или в здания, которые берут жидкость (бак, НПЗ…). Занят выход — пропускает его.
/// </summary>
public class PipeSplitter : BuildingBase
{
    const int Capacity = 6;
    const float PerSecond = 24f;

    readonly Queue<ItemData> buffer = new Queue<ItemData>();
    int next;
    float carry;

    public int Buffered => buffer.Count;

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (!IsPlaced || item == null || !item.isFluid || buffer.Count >= Capacity)
            return false;
        buffer.Enqueue(item);
        return true;
    }

    void Update()
    {
        if (!IsPlaced || buffer.Count == 0 || outputSockets == null || outputSockets.Length == 0)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        carry = Mathf.Min(carry + Time.deltaTime * PerSecond, Capacity);
        int n = outputSockets.Length;
        while (carry >= 1f && buffer.Count > 0)
        {
            ItemData item = buffer.Peek();
            bool sent = false;
            for (int k = 0; k < n; k++)
            {
                int idx = (next + k) % n;
                if (!TryGive(outputSockets[idx], item))
                    continue;
                buffer.Dequeue();
                next = (idx + 1) % n;
                carry -= 1f;
                sent = true;
                break;
            }

            if (!sent)
                break;
        }
    }

    bool TryGive(BuildingSocket socket, ItemData item)
    {
        if (socket == null)
            return false;
        BuildingBase dest = BuildingLinker.GetBuildingAt(BuildingLinker.GetSocketFrontCell(socket));
        if (dest == null || dest == this)
            return false;

        Conveyor belt = dest as Conveyor;
        if (belt != null)
        {
            if (!(belt is Pipe))
                return false;
            // Не толкаем обратно в трубу, которая сама нас кормит.
            if (BuildingLinker.FeedsInto(belt, BuildingLinker.WorldToCell(transform.position)))
                return false;
            return belt.TryAcceptTransfer(item, null, this);
        }

        if (dest is PipeSplitter other)
            return other.CanAcceptFrom(this) && other.TryReceiveItem(item, null);
        if (!dest.CanAcceptFrom(this))
            return false;
        return dest.TryReceiveItem(item, socket);
    }

    public override void WriteSave(BuildingSaveData save)
    {
        base.WriteSave(save);
        if (save == null)
            return;
        var counts = new Dictionary<ItemData, int>();
        foreach (ItemData item in buffer)
        {
            counts.TryGetValue(item, out int have);
            counts[item] = have + 1;
        }

        save.storage = SaveItems.FromCounts(counts);
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        buffer.Clear();
        if (save == null || save.storage == null)
            return;
        for (int i = 0; i < save.storage.Count; i++)
        {
            ItemData item = GameDatabase.FindItem(save.storage[i].itemId);
            if (item == null)
                continue;
            for (int k = 0; k < save.storage[i].amount && buffer.Count < Capacity; k++)
                buffer.Enqueue(item);
        }
    }
}
