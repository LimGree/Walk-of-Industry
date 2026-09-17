using UnityEngine;

public class OilExtractor : BuildingBase, IInteractable
{
    public const int RichnessCount = 4;

    static readonly float[] Intervals = { 2.2f, 1.45f, 0.9f, 0.55f };
    static readonly string[] RichnessNames = { "Скудная", "Обычная", "Богатая", "Очень богатая" };

    [Header("Oil")]
    public ItemData resource;
    public int richness = -1;
    public float extractInterval = 1.45f;
    public int itemsPerCycle = 1;

    [Header("Debug")]
    public bool showDebug;

    float timer;

    public string RichnessName
    {
        get
        {
            int i = Mathf.Clamp(richness, 0, RichnessCount - 1);
            return RichnessNames[i];
        }
    }

    public float CurrentInterval => Mathf.Max(0.05f, extractInterval * Economy.ExtractTimeMul);
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
        if (richness < 0)
            RollRichness();
        timer = 0f;
        base.OnPlaced();
    }

    public void RollRichness()
    {
        richness = Random.Range(0, RichnessCount);
        extractInterval = Intervals[richness];
        timer = 0f;
    }

    void ResolveResource()
    {
        if (resource != null)
            return;
        resource = GameDatabase.FindItem("crude_oil");
    }

    void Update()
    {
        ResolveResource();
        if (resource == null)
        {
            GameAudio.Loop(this, "bld_oil_loop", false);
            return;
        }

        GameAudio.Loop(this, "bld_oil_loop", HasOutputSpace(1));

        timer += Time.deltaTime;
        if (timer < CurrentInterval)
            return;
        timer -= CurrentInterval;

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

    public override void WriteSave(BuildingSaveData save)
    {
        base.WriteSave(save);
        if (save == null)
            return;
        save.stateInt = richness;
        save.stateFloat = extractInterval;
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        if (save == null)
            return;
        richness = save.stateInt;
        extractInterval = save.stateFloat > 0.05f ? save.stateFloat : extractInterval;
        if (richness < 0 || richness >= RichnessCount)
            RollRichness();
        else
            extractInterval = Intervals[Mathf.Clamp(richness, 0, RichnessCount - 1)];
        ResolveResource();
    }

    public void Interact(GameObject interactor)
    {
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
