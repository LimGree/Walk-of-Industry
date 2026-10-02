using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Станция загрузки дронов 5×5. Ленты заходят сзади (−Z, вся сторона), цех пакует
/// предметы одного типа (фильтр или «первый пришедший») в ящик на грузоподъёмность дрона.
/// Готовые ящики (до 4) забирают дроны с площадок и везут на привязанную [[DroneUnloadStation]].
/// Первый дрон в комплекте, остальные — за рубины, до числа слотов из исследований.
/// </summary>
public class DroneLoadStation : BuildingBase, IInteractable
{
    [Header("Drones")]
    public ItemData filter;

    readonly List<Drone> drones = new List<Drone>();
    readonly List<ItemData> readyItems = new List<ItemData>();
    readonly List<int> readyCounts = new List<int>();

    ItemData crateItem;
    int crateCount;
    float lastFeed;
    int ownedDrones = 1;
    bool hasTargetCell;
    Vector2Int targetCell;
    DroneUnloadStation target;
    float nextDispatch;

    public int OwnedDrones => ownedDrones;
    public IReadOnlyList<Drone> Drones => drones;
    public ItemData CrateItem => crateItem;
    public int CrateCount => crateCount;
    public int ReadyCount => readyItems.Count;
    public DroneUnloadStation Target => ResolveTarget();

    void Awake()
    {
        DroneModels.Attach(transform, DroneModels.LoadStation, 5f);
    }

    public override void OnPlaced()
    {
        base.OnPlaced();
        EnsureDrones();
    }

    public override void OnRemoved()
    {
        KillDrones();
        base.OnRemoved();
    }

    protected override void OnDestroy()
    {
        KillDrones();
        base.OnDestroy();
    }

    public Vector3 PadWorld(int pad)
    {
        Vector3 local = DroneModels.LoadPads[Mathf.Clamp(pad, 0, DroneModels.LoadPads.Length - 1)];
        return transform.TransformPoint(local);
    }

    // ---------- Дроны ----------

    void EnsureDrones()
    {
        if (!IsPlaced)
            return;
        drones.RemoveAll(d => d == null);
        while (drones.Count < ownedDrones)
            drones.Add(Drone.Spawn(this, FreePad()));
    }

    int FreePad()
    {
        for (int p = 0; p < DroneNetwork.MaxDrones; p++)
        {
            bool used = false;
            for (int i = 0; i < drones.Count; i++)
            {
                if (drones[i] != null && drones[i].HomePad == p)
                    used = true;
            }

            if (!used)
                return p;
        }

        return 0;
    }

    void KillDrones()
    {
        for (int i = 0; i < drones.Count; i++)
        {
            if (drones[i] != null)
                Destroy(drones[i].gameObject);
        }

        drones.Clear();
    }

    public bool CanBuyDrone => ownedDrones < DroneNetwork.Slots() && ownedDrones < DroneNetwork.MaxDrones;
    public int NextDronePrice => DroneNetwork.PriceForNext(ownedDrones);

    public bool TryBuyDrone()
    {
        if (!CanBuyDrone || PlayerWallet.Instance == null)
            return false;
        if (!PlayerWallet.Instance.TrySpendRubies(NextDronePrice, MoneySource.Drones))
            return false;
        ownedDrones++;
        EnsureDrones();
        return true;
    }

    // ---------- Привязка ----------

    public void SetTarget(DroneUnloadStation station)
    {
        target = station;
        hasTargetCell = station != null;
        targetCell = station != null ? BuildingLinker.WorldToCell(station.transform.position) : default;
    }

    DroneUnloadStation ResolveTarget()
    {
        if (target != null && target.IsPlaced)
            return target;
        target = null;
        if (!hasTargetCell)
            return null;
        target = BuildingLinker.GetBuildingAt(targetCell) as DroneUnloadStation;
        return target;
    }

    public void SetFilter(ItemData item)
    {
        filter = item;
    }

    // ---------- Упаковка ----------

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (!IsPlaced || item == null || item.isFluid)
            return false;
        if (filter != null && item != filter)
            return false;
        if (crateItem != null && item != crateItem)
        {
            // Пришёл другой тип — дозакрыть текущий ящик и начать новый.
            if (!Seal())
                return false;
        }

        if (crateCount >= DroneNetwork.Capacity() && !Seal())
            return false;
        crateItem = item;
        crateCount++;
        lastFeed = Time.time;
        if (crateCount >= DroneNetwork.Capacity())
            Seal();
        return true;
    }

    bool Seal()
    {
        if (crateItem == null || crateCount <= 0)
            return true;
        if (readyItems.Count >= DroneNetwork.ReadyCrates)
            return false;
        readyItems.Add(crateItem);
        readyCounts.Add(crateCount);
        crateItem = null;
        crateCount = 0;
        return true;
    }

    public void AcceptReturnedCargo(Drone drone)
    {
        drone.TakeCargoBack(out ItemData item, out int count);
        if (item == null || count <= 0)
            return;
        readyItems.Insert(0, item);
        readyCounts.Insert(0, count);
    }

    void Update()
    {
        if (!IsPlaced)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        if (drones.Count < ownedDrones)
            EnsureDrones();

        // Поток редкий — не ждём полный ящик вечно.
        if (crateCount > 0 && Time.time - lastFeed > DroneNetwork.SealIdleSeconds)
            Seal();

        if (BreakMode == 1 || readyItems.Count == 0 || Time.time < nextDispatch)
            return;
        DroneUnloadStation to = ResolveTarget();
        if (to == null)
            return;

        for (int i = 0; i < drones.Count; i++)
        {
            Drone d = drones[i];
            if (d == null || d.IsBusy)
                continue;
            if (d.Launch(readyItems[0], readyCounts[0], to))
            {
                readyItems.RemoveAt(0);
                readyCounts.RemoveAt(0);
                nextDispatch = Time.time + 0.6f;
            }
            break;
        }
    }

    public int FlyingCount()
    {
        int n = 0;
        for (int i = 0; i < drones.Count; i++)
        {
            if (drones[i] != null && drones[i].IsBusy)
                n++;
        }

        return n;
    }

    public int ReadyItemTotal()
    {
        int n = 0;
        for (int i = 0; i < readyCounts.Count; i++)
            n += readyCounts[i];
        return n;
    }

    public ItemData ReadyItemAt(int i) => i >= 0 && i < readyItems.Count ? readyItems[i] : null;

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
        save.filterItemId = filter != null ? filter.id : "";
        save.stateInt = ownedDrones;
        if (save.extras == null)
            save.extras = new List<SaveKeyValue>();
        var inv = CultureInfo.InvariantCulture;
        if (hasTargetCell)
            save.extras.Add(new SaveKeyValue { key = "droneTarget", value = targetCell.x.ToString(inv) + "," + targetCell.y.ToString(inv) });

        // Готовые ящики + текущий + груз в полёте (при загрузке — снова на площадку).
        var crates = new List<ItemAmountSave>();
        for (int i = 0; i < readyItems.Count; i++)
            crates.Add(new ItemAmountSave { itemId = readyItems[i].id, amount = readyCounts[i] });
        for (int i = 0; i < drones.Count; i++)
        {
            Drone d = drones[i];
            if (d != null && d.CargoItem != null && d.CargoCount > 0)
                crates.Add(new ItemAmountSave { itemId = d.CargoItem.id, amount = d.CargoCount });
        }

        save.storage = crates;
        if (crateItem != null && crateCount > 0)
            save.inputBuffer = new List<ItemAmountSave> { new ItemAmountSave { itemId = crateItem.id, amount = crateCount } };
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        if (save == null)
            return;
        filter = GameDatabase.FindItem(save.filterItemId);
        ownedDrones = Mathf.Clamp(save.stateInt, 1, DroneNetwork.MaxDrones);
        readyItems.Clear();
        readyCounts.Clear();
        if (save.storage != null)
        {
            for (int i = 0; i < save.storage.Count; i++)
            {
                ItemData item = GameDatabase.FindItem(save.storage[i].itemId);
                if (item == null || save.storage[i].amount <= 0)
                    continue;
                readyItems.Add(item);
                readyCounts.Add(save.storage[i].amount);
            }
        }

        crateItem = null;
        crateCount = 0;
        if (save.inputBuffer != null && save.inputBuffer.Count > 0)
        {
            crateItem = GameDatabase.FindItem(save.inputBuffer[0].itemId);
            crateCount = crateItem != null ? Mathf.Max(0, save.inputBuffer[0].amount) : 0;
            lastFeed = Time.time;
        }

        hasTargetCell = false;
        if (save.extras != null)
        {
            for (int i = 0; i < save.extras.Count; i++)
            {
                SaveKeyValue row = save.extras[i];
                if (row == null || row.key != "droneTarget" || string.IsNullOrEmpty(row.value))
                    continue;
                string[] p = row.value.Split(',');
                if (p.Length == 2
                    && int.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                    && int.TryParse(p[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z))
                {
                    hasTargetCell = true;
                    targetCell = new Vector2Int(x, z);
                }
            }
        }

        target = null;
        KillDrones();
        EnsureDrones();
    }
}
