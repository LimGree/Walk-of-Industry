using UnityEngine;

/// <summary>
/// Ворует предмет у соседа спереди и кладёт назад.
/// К лентам и зданиям не коннектится.
/// </summary>
public class RoboticArm : BuildingBase, IInteractable
{
    [Header("Arm")]
    public float transferInterval = 0.45f;
    public float itemHeight = 0.7f;
    public float itemScale = 0.28f;
    public ItemData filter;

    [Header("Debug")]
    public bool showDebug;

    ItemData heldItem;
    Transform heldVisual;
    float cooldown;
    bool isLive;

    public Vector2Int Cell => BuildingLinker.WorldToCell(transform.position);
    public ItemData Filter => filter;
    public ItemData HeldItem => heldItem;

    void Awake()
    {
        EnsureSetup();
    }

    public override void OnPlaced()
    {
        isLive = true;
        cooldown = 0f;
        EnsureSetup();
        base.OnPlaced();
    }

    public override void OnRemoved()
    {
        isLive = false;
        ClearHeld(true);
        base.OnRemoved();
    }

    public override bool CanAcceptFrom(BuildingBase source)
    {
        return false;
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        return false;
    }

    public void SetFilter(ItemData item)
    {
        filter = item;
    }

    public override void WriteSave(BuildingSaveData save)
    {
        base.WriteSave(save);
        if (save == null)
            return;
        save.filterItemId = filter != null ? filter.id : "";
        save.heldItemId = heldItem != null ? heldItem.id : "";
        save.stateFloat = cooldown;
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        ClearHeld(true);
        if (save == null)
            return;

        filter = GameDatabase.FindItem(save.filterItemId);
        cooldown = Mathf.Max(0f, save.stateFloat);
        ItemData held = GameDatabase.FindItem(save.heldItemId);
        if (held != null)
            Hold(held, null);
    }

    void Update()
    {
        if (!isLive)
            return;

        if (WorldView.InRange(transform.position))
            UpdateHeldVisual();
        else if (heldVisual != null)
            heldVisual.gameObject.SetActive(false);

        cooldown -= Time.deltaTime;
        if (cooldown > 0f)
            return;

        if (heldItem == null)
        {
            if (TryStealFromFront())
                cooldown = transferInterval;
            return;
        }

        if (TryDropToBack())
            cooldown = transferInterval;
    }

    bool TryStealFromFront()
    {
        BuildingBase source = Neighbor(FrontDir());
        BuildingBase dest = Neighbor(BackDir());
        if (source == null || dest == null || source == this || dest == this || source == dest)
            return false;

        ItemData stolen;
        Transform visual;
        if (!TrySteal(source, filter, out stolen, out visual))
            return false;

        if (TryDeliver(dest, stolen, visual))
        {
            if (showDebug)
                Debug.Log($"[Arm] {stolen.displayName}: {source.name} → {dest.name}");
            return true;
        }

        if (CanEventuallyTake(dest, stolen))
        {
            Hold(stolen, visual);
            return true;
        }

        TryReturn(source, stolen, visual);
        return false;
    }

    bool CanEventuallyTake(BuildingBase dest, ItemData item)
    {
        if (dest == null || item == null)
            return false;

        if (dest is Conveyor || dest is Splitter)
            return true;

        StorageContainer storage = dest as StorageContainer;
        if (storage != null)
        {
            if (!storage.AcceptsCargo(item))
                return false;
            ItemData locked = storage.StoredType;
            return locked == null || locked == item;
        }

        return dest.CanAcceptFrom(this);
    }

    bool TryDropToBack()
    {
        BuildingBase dest = Neighbor(BackDir());
        if (dest == null || dest == this)
            return false;

        ItemData dropping = heldItem;
        if (!TryDeliver(dest, heldItem, heldVisual))
            return false;

        if (showDebug)
            Debug.Log($"[Arm] drop {dropping.displayName} to {dest.name}");
        return true;
    }

    bool TrySteal(BuildingBase source, ItemData wanted, out ItemData item, out Transform visual)
    {
        item = null;
        visual = null;

        Conveyor belt = source as Conveyor;
        if (belt != null)
            return belt.TryStealMatching(wanted, out item, out visual);

        Splitter splitter = source as Splitter;
        if (splitter != null)
            return splitter.TryStealMatching(wanted, out item, out visual);

        StorageContainer storage = source as StorageContainer;
        if (storage != null)
            return storage.TrySteal(wanted, out item);

        return source.TryStealFromOutput(wanted, out item);
    }

    bool TryDeliver(BuildingBase dest, ItemData item, Transform visual)
    {
        if (dest == null || item == null)
            return false;

        Conveyor belt = dest as Conveyor;
        if (belt != null)
        {
            if (!belt.TryAcceptTransfer(item, visual, this))
                return false;
            TakeFromHand(visual, false);
            BeltItemView.Release(visual, item);
            return true;
        }

        Splitter splitter = dest as Splitter;
        if (splitter != null)
        {
            if (!splitter.TryAcceptTransfer(item, visual))
                return false;
            TakeFromHand(visual, false);
            BeltItemView.Release(visual, item);
            return true;
        }

        if (!dest.CanAcceptFrom(this))
            return false;

        BuildingSocket destInput = dest.inputSockets != null && dest.inputSockets.Length > 0
            ? dest.inputSockets[0]
            : null;
        if (!dest.TryReceiveItem(item, destInput))
            return false;

        TakeFromHand(visual, true);
        return true;
    }

    void TryReturn(BuildingBase source, ItemData item, Transform visual)
    {
        if (source is Conveyor belt && belt.TryAcceptTransfer(item, visual, this))
            return;
        if (source is Splitter splitter && splitter.TryAcceptTransfer(item, visual))
            return;
        if (source is StorageContainer storage && storage.TryAddOne(item))
        {
            BeltItemView.Destroy(visual);
            return;
        }

        if (source.TryReturnToOutput(item))
        {
            BeltItemView.Destroy(visual);
            return;
        }

        Hold(item, visual);
    }

    void Hold(ItemData item, Transform visual)
    {
        heldItem = item;
        if (visual == null)
            visual = BeltItemView.Create(item, itemScale);
        else
            BeltItemView.Prepare(visual);

        heldVisual = visual;
        if (heldVisual != null)
            heldVisual.SetParent(transform, true);
        UpdateHeldVisual();
    }

    void TakeFromHand(Transform visual, bool destroyVisual)
    {
        if (heldVisual == visual)
        {
            heldItem = null;
            heldVisual = null;
        }

        if (destroyVisual)
            BeltItemView.Destroy(visual);
    }

    void UpdateHeldVisual()
    {
        if (heldVisual == null)
            return;

        if (!heldVisual.gameObject.activeSelf)
            heldVisual.gameObject.SetActive(true);

        Vector3 pos = transform.position + Vector3.up * itemHeight;
        Vector3 look = BuildingLinker.CardinalToWorld(BackDir());
        BeltItemView.Update(heldVisual, pos, look);
    }

    void ClearHeld(bool destroy)
    {
        if (destroy)
            BeltItemView.Destroy(heldVisual);
        heldVisual = null;
        heldItem = null;
    }

    BuildingBase Neighbor(Vector2Int dir)
    {
        if (dir.x == 0 && dir.y == 0)
            return null;
        return BuildingLinker.GetBuildingAt(Cell + dir);
    }

    Vector2Int FrontDir()
    {
        Transform marker = transform.Find("front");
        if (marker != null)
            return BuildingLinker.ToCardinal(marker.position - transform.position);
        return BuildingLinker.ToCardinal(transform.forward);
    }

    Vector2Int BackDir()
    {
        Transform marker = transform.Find("back");
        if (marker != null)
            return BuildingLinker.ToCardinal(marker.position - transform.position);
        return new Vector2Int(-FrontDir().x, -FrontDir().y);
    }

    void EnsureSetup()
    {
        inputSockets = System.Array.Empty<BuildingSocket>();
        outputSockets = System.Array.Empty<BuildingSocket>();
        DisableChildColliders();
        EnsureCollider();
    }

    void DisableChildColliders()
    {
        Collider[] cols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null && cols[i].gameObject != gameObject)
                cols[i].enabled = false;
        }
    }

    void EnsureCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null)
            box = gameObject.AddComponent<BoxCollider>();

        float cell = GridFootprint.CellSize * 0.72f;
        Vector3 lossy = transform.lossyScale;
        box.size = new Vector3(
            cell / Mathf.Max(0.01f, lossy.x),
            0.7f / Mathf.Max(0.01f, lossy.y),
            cell / Mathf.Max(0.01f, lossy.z)
        );
        box.center = new Vector3(0f, 0.35f, 0f);
        box.enabled = true;
    }

    public void Interact(GameObject interactor)
    {
        if (MachineUI.Instance != null)
            MachineUI.Instance.Open(this);
    }

    protected override void OnDestroy()
    {
        ClearHeld(true);
        base.OnDestroy();
    }
}
