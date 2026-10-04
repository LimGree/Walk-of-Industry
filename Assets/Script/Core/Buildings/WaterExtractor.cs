using UnityEngine;

public class WaterExtractor : BuildingBase, IInteractable
{
    [Header("Water")]
    public ItemData resource;
    public float extractInterval = 1.2f;
    public int itemsPerCycle = 1;

    [Header("Debug")]
    public bool showDebug;

    float timer;

    public float CurrentInterval => Mathf.Max(0.05f,
        extractInterval * Economy.ExtractTimeMul / PowerGenerator.GetNearbySpeedMultiplier(transform.position));
    public int CurrentItemsPerCycle => Mathf.Max(1, itemsPerCycle);

    void Awake()
    {
        EnsureSetup();
        ResolveResource();
    }

    public override void OnPlaced()
    {
        EnsureSetup();
        ResolveResource();
        timer = 0f;
        base.OnPlaced();
    }

    void ResolveResource()
    {
        if (resource != null)
            return;
        resource = GameDatabase.FindItem("water");
    }

    void Update()
    {
        ResolveResource();
        if (resource == null)
        {
            GameAudio.Loop(this, "bld_water_loop", false);
            return;
        }

        float breakMul = BreakWorkMul;
        GameAudio.Loop(this, "bld_water_loop", breakMul > 0f && HasOutputSpace(1));

        timer += Time.deltaTime * breakMul * DevWorkMul;
        if (timer < CurrentInterval)
            return;
        timer = Mathf.Min(timer - CurrentInterval, CurrentInterval);

        int count = CurrentItemsPerCycle;
        if (!HasOutputSpace(count) && !CanPushAnyNow())
            return;

        for (int i = 0; i < count; i++)
        {
            if (!CanPushAnyNow() && !HasOutputSpace(1))
                break;
            if (!TryOutputToAny(resource))
                break;
            ProductionStats.Instance?.RecordProduced(resource, 1);
        }
    }

    bool CanPushAnyNow()
    {
        if (outputSockets == null)
            return false;
        for (int i = 0; i < outputSockets.Length; i++)
        {
            BuildingSocket socket = outputSockets[i];
            if (socket == null)
                continue;
            if (socket.connectedSocket != null)
                return true;
            BuildingBase front = BuildingLinker.GetBuildingAt(BuildingLinker.GetSocketFrontCell(socket));
            if (front != null && front != this)
                return true;
        }
        return HasPushNeighbor();
    }

    /// <summary>Доля текущего цикла добычи (0..1).</summary>
    public float CycleProgress => Mathf.Clamp01(timer / Mathf.Max(0.05f, CurrentInterval));

    /// <summary>Консоль (/machine finish): цикл добычи завершится на следующем кадре.</summary>
    public void DevFinishCycle()
    {
        timer = CurrentInterval;
    }

    public void Interact(GameObject interactor)
    {
        if (RepairUI.TryOpen(this))
            return;
        if (MachineUI.Instance != null)
            MachineUI.Instance.Open(this);
    }

    void EnsureSetup()
    {
        DisableChildColliders();
        EnsureCollider();
        EnsureSockets();
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

        Vector3 world = GridFootprint.GetWorldSize(FootprintSize);
        Vector3 lossy = transform.lossyScale;
        box.size = new Vector3(
            world.x * 0.88f / Mathf.Max(0.01f, lossy.x),
            1.1f / Mathf.Max(0.01f, lossy.y),
            world.z * 0.88f / Mathf.Max(0.01f, lossy.z)
        );
        box.center = new Vector3(0f, 0.55f, 0f);
        box.enabled = true;
    }

    void EnsureSockets()
    {
        if (outputSockets != null && outputSockets.Length > 0 && outputSockets[0] != null)
            return;

        Transform existing = transform.Find("OutputSocket");
        GameObject go = existing != null ? existing.gameObject : new GameObject("OutputSocket");
        go.transform.SetParent(transform, false);
        BuildingPrefabLayout.PlaceSocket(go.transform, new Vector3(0f, 0.3f, 0.5f), BuildingPrefabLayout.OutputRotation);

        BuildingSocket socket = go.GetComponent<BuildingSocket>();
        if (socket == null)
            socket = go.AddComponent<BuildingSocket>();
        socket.socketType = SocketType.Output;
        outputSockets = new[] { socket };
        inputSockets = System.Array.Empty<BuildingSocket>();
    }
}
