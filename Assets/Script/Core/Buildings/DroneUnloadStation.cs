using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Станция выгрузки дронов 5×5. Площадки сзади (−Z), склад на 1000 предметов любых типов,
/// выдача на ленты спереди (+Z, вся сторона). К одной станции можно привязать сколько угодно
/// [[DroneLoadStation]]. Дрон садится, только если есть свободная площадка и место под ящик.
/// </summary>
public class DroneUnloadStation : BuildingBase, IInteractable
{
    const float OutputPerSecond = 20f;

    readonly Dictionary<ItemData, int> storage = new Dictionary<ItemData, int>();
    readonly List<ItemData> order = new List<ItemData>();
    readonly Drone[] padUsers = new Drone[4];
    readonly int[] padReserved = new int[4];
    int total;
    int reserved;
    int nextOut;
    float outCarry;

    public int Total => total;
    public IReadOnlyDictionary<ItemData, int> Storage => storage;

    void Awake()
    {
        DroneModels.Attach(transform, DroneModels.UnloadStation, 5f);
    }

    public Vector3 PadWorld(int pad)
    {
        Vector3 local = DroneModels.UnloadPads[Mathf.Clamp(pad, 0, DroneModels.UnloadPads.Length - 1)];
        return transform.TransformPoint(local);
    }

    // ---------- Площадки ----------

    public bool CanLand(int count)
    {
        return IsPlaced && BreakMode != 1 && total + reserved + count <= DroneNetwork.UnloadCapacity;
    }

    public int ReservePad(Drone drone)
    {
        for (int i = 0; i < padUsers.Length; i++)
        {
            if (padUsers[i] == drone)
                return i;
        }

        for (int i = 0; i < padUsers.Length; i++)
        {
            if (padUsers[i] == null)
            {
                padUsers[i] = drone;
                padReserved[i] = drone != null ? drone.CargoCount : 0;
                reserved += padReserved[i];
                return i;
            }
        }

        return -1;
    }

    public void ReleasePad(int pad, Drone drone)
    {
        if (pad < 0 || pad >= padUsers.Length || padUsers[pad] != drone)
            return;
        padUsers[pad] = null;
        reserved = Mathf.Max(0, reserved - padReserved[pad]);
        padReserved[pad] = 0;
    }

    public bool Deliver(ItemData item, int count)
    {
        if (!IsPlaced || item == null || count <= 0)
            return false;
        // Резерв снимается при освобождении площадки; здесь проверяем только реальную ёмкость.
        if (total + count > DroneNetwork.UnloadCapacity)
            return false;
        storage.TryGetValue(item, out int have);
        if (have == 0 && !order.Contains(item))
            order.Add(item);
        storage[item] = have + count;
        total += count;
        return true;
    }

    // ---------- Выдача на ленту ----------

    void Update()
    {
        if (!IsPlaced || total <= 0)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        outCarry = Mathf.Min(outCarry + Time.deltaTime * OutputPerSecond * BreakWorkMul, 8f);
        int guard = order.Count + 1;
        while (outCarry >= 1f && total > 0 && HasOutputSpace(1) && guard > 0)
        {
            if (order.Count == 0)
                break;
            nextOut %= order.Count;
            ItemData item = order[nextOut];
            if (!storage.TryGetValue(item, out int have) || have <= 0)
            {
                order.RemoveAt(nextOut);
                guard--;
                continue;
            }

            if (!TryOutputToAny(item))
                break;
            outCarry -= 1f;
            have--;
            total--;
            if (have <= 0)
            {
                storage.Remove(item);
                order.RemoveAt(nextOut);
            }
            else
            {
                storage[item] = have;
                nextOut++;
            }
        }
    }

    public int IncomingDrones()
    {
        int n = 0;
        for (int i = 0; i < DroneNetwork.AllDrones.Count; i++)
        {
            Drone d = DroneNetwork.AllDrones[i];
            if (d != null && d.IsBusy && d.Target == this && d.CargoCount > 0)
                n++;
        }

        return n;
    }

    public int LinkedStations()
    {
        int n = 0;
        DroneLoadStation[] all = FindObjectsByType<DroneLoadStation>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].IsPlaced && all[i].Target == this)
                n++;
        }

        return n;
    }

    public void Interact(GameObject interactor)
    {
        if (RepairUI.TryOpen(this))
            return;
        DroneStationUI.OpenFor(this);
    }

    // ---------- Сейв ----------

    public override void WriteSave(BuildingSaveData save)
    {
        base.WriteSave(save);
        if (save == null)
            return;
        var list = new List<ItemAmountSave>();
        for (int i = 0; i < order.Count; i++)
        {
            if (storage.TryGetValue(order[i], out int have) && have > 0)
                list.Add(new ItemAmountSave { itemId = order[i].id, amount = have });
        }

        save.storage = list;
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        storage.Clear();
        order.Clear();
        total = 0;
        if (save == null || save.storage == null)
            return;
        for (int i = 0; i < save.storage.Count; i++)
        {
            ItemData item = GameDatabase.FindItem(save.storage[i].itemId);
            int n = Mathf.Max(0, save.storage[i].amount);
            if (item == null || n == 0)
                continue;
            n = Mathf.Min(n, DroneNetwork.UnloadCapacity - total);
            if (n <= 0)
                break;
            storage.TryGetValue(item, out int have);
            if (have == 0)
                order.Add(item);
            storage[item] = have + n;
            total += n;
        }
    }
}
