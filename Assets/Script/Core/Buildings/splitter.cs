using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Перекрёсток ленты: 1 вход, 3 выхода. Предметы едут по клетке, как на конвейере.
/// </summary>
public class Splitter : BuildingBase
{
    [Header("Belt")]
    public float speed = 2.5f;
    public int maxItems = 3;
    public float itemHeight = 0.35f;
    public float itemScale = 0.28f;

    [Header("Debug")]
    public bool showDebug;

    public Vector2Int Cell => BuildingLinker.WorldToCell(transform.position);

    public BuildingSocket InputSocket =>
        inputSockets != null && inputSockets.Length > 0 ? inputSockets[0] : null;

    bool isLive;
    int nextOutput;
    float resolvedHeight;
    readonly List<Cargo> cargo = new List<Cargo>(4);

    class Cargo
    {
        public ItemData item;
        public float progress;
        public Transform visual;
        public Vector2Int entryDir;
        public Vector2Int exitDir;
    }

    void Awake()
    {
        EnsureSetup();
    }

    public override void OnPlaced()
    {
        isLive = true;
        EnsureSetup();
        nextOutput = 0;
        WorldSim.RegisterSplitter(this);
        base.OnPlaced();
    }

    public override void OnRemoved()
    {
        isLive = false;
        ClearCargo();
        WorldSim.UnregisterSplitter(this);
        base.OnRemoved();
    }

    public bool IsFedBy(BuildingBase source)
    {
        if (source == null)
            return false;

        return BuildingLinker.OccupiesCell(source, Cell + InputOutward());
    }

    public override bool CanAcceptFrom(BuildingBase source)
    {
        return IsFedBy(source);
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (!isLive || item == null || item.isFluid || !CanAccept())
            return false;

        SpawnCargo(item, null, InferEntryDir(fromSocket), Vector2Int.zero);
        return true;
    }

    public bool TryAcceptTransfer(ItemData item, Transform visual)
    {
        if (!isLive || item == null || item.isFluid || !CanAccept())
            return false;

        Vector3 from = visual != null ? visual.position : transform.position;
        SpawnCargo(item, null, InferEntryFromWorld(from), Vector2Int.zero);
        return true;
    }

    public bool HasRoomForItem()
    {
        return isLive && CanAccept();
    }

    public bool TryStealMatching(ItemData filter, out ItemData item, out Transform visual)
    {
        item = null;
        visual = null;
        if (!isLive)
            return false;

        int best = -1;
        float bestProgress = -1f;
        for (int i = 0; i < cargo.Count; i++)
        {
            Cargo entry = cargo[i];
            if (entry == null || entry.item == null)
                continue;
            if (filter != null && entry.item != filter)
                continue;
            if (entry.progress > bestProgress)
            {
                best = i;
                bestProgress = entry.progress;
            }
        }

        if (best < 0)
            return false;

        Cargo stolen = cargo[best];
        item = stolen.item;
        visual = stolen.visual;
        cargo.RemoveAt(best);
        if (visual != null)
            visual.SetParent(null, true);
        return true;
    }

    float ItemGap => 1f / Mathf.Max(1, maxItems);

    bool CanAccept()
    {
        if (cargo.Count >= Mathf.Max(1, maxItems))
            return false;

        float nearest = 1f;
        for (int i = 0; i < cargo.Count; i++)
        {
            if (cargo[i].progress < nearest)
                nearest = cargo[i].progress;
        }

        return nearest >= ItemGap;
    }

    public void SimDraw()
    {
        SubmitCargoDraws();
    }

    public void SimTick()
    {
        if (!isLive)
            return;

        float cell = GridFootprint.CellSize;
        float move = speed * Time.deltaTime / Mathf.Max(0.05f, cell);
        float gap = ItemGap;

        for (int i = 0; i < cargo.Count; i++)
        {
            float limit = 1f;
            float progress = cargo[i].progress;
            for (int j = 0; j < cargo.Count; j++)
            {
                if (j == i)
                    continue;
                float other = cargo[j].progress;
                if (other > progress)
                    limit = Mathf.Min(limit, other - gap);
            }
            if (limit < 0f)
                limit = 0f;
            if (progress < limit)
                cargo[i].progress = Mathf.Min(limit, progress + move);
        }

        int front = -1;
        float best = 0.999f;
        for (int i = 0; i < cargo.Count; i++)
        {
            if (cargo[i].progress >= best)
            {
                best = cargo[i].progress;
                front = i;
            }
        }
        if (front >= 0)
        {
            if (TryHandOff(cargo[front]))
            {
                ReleaseCargoVisual(cargo[front]);
                cargo.RemoveAt(front);
            }
            else
                cargo[front].progress = 1f;
        }

        SimDraw();
    }

    void SubmitCargoDraws()
    {
        bool show = WorldView.InRange(transform.position);
        for (int i = 0; i < cargo.Count; i++)
        {
            Cargo item = cargo[i];
            if (show)
            {
                if (item.visual == null)
                    item.visual = BeltItemView.Rent(item.item, itemScale);
                Vector3 pos = EvaluatePath(item, item.progress);
                Vector3 look = EvaluatePath(item, Mathf.Min(1f, item.progress + 0.05f)) - pos;
                BeltItemView.Update(item.visual, pos, look);
            }
            else
                ReleaseCargoVisual(item);
        }
    }

    bool TryHandOff(Cargo item)
    {
        EnsureSetup();
        int count = outputSockets != null ? outputSockets.Length : 0;
        if (count <= 0)
            return TryGive(item, item.exitDir);

        Vector2Int entry = item.entryDir;
        int start = nextOutput;
        for (int n = 0; n < count; n++)
        {
            int index = (start + n) % count;
            BuildingSocket socket = outputSockets[index];
            if (socket == null)
                continue;

            Vector2Int dir = BuildingLinker.ToCardinal(socket.GetOutward());
            if (dir == Opposite(entry))
                continue;
            if (dir.x == 0 && dir.y == 0)
                continue;

            if (!TryGive(item, dir))
                continue;

            item.exitDir = dir;
            nextOutput = (index + 1) % count;
            return true;
        }

        return false;
    }

    bool TryGive(Cargo item, Vector2Int dir)
    {
        if (dir.x == 0 && dir.y == 0)
            return false;

        BuildingBase dest = BuildingLinker.GetBuildingAt(Cell + dir);
        if (dest == null || dest == this)
            return false;

        Conveyor nextBelt = dest as Conveyor;
        if (nextBelt != null)
        {
            if (nextBelt is Pipe)
                return false;
            return nextBelt.TryAcceptTransfer(item.item, item.visual, this);
        }

        Splitter nextSplit = dest as Splitter;
        if (nextSplit != null)
        {
            if (!nextSplit.CanAcceptFrom(this))
                return false;
            return nextSplit.TryAcceptTransfer(item.item, item.visual);
        }

        if (!dest.CanAcceptFrom(this))
            return false;

        BuildingSocket destInput = dest.inputSockets != null && dest.inputSockets.Length > 0
            ? dest.inputSockets[0]
            : null;
        return dest.TryReceiveItem(item.item, destInput);
    }

    static void ReleaseCargoVisual(Cargo item)
    {
        if (item == null || item.visual == null)
            return;
        BeltItemView.Release(item.visual, item.item);
        item.visual = null;
    }

    void SpawnCargo(ItemData item, Transform visual, Vector2Int entryDir, Vector2Int exitDir)
    {
        if (entryDir.x == 0 && entryDir.y == 0)
            entryDir = Opposite(InputOutward());
        if (exitDir.x == 0 && exitDir.y == 0)
            exitDir = ChooseExitDir(entryDir);

        Cargo cargoItem = new Cargo
        {
            item = item,
            progress = 0f,
            visual = visual,
            entryDir = entryDir,
            exitDir = exitDir
        };
        if (WorldView.InRange(transform.position))
        {
            if (cargoItem.visual == null)
                cargoItem.visual = BeltItemView.Rent(item, itemScale);
            else
                BeltItemView.Prepare(cargoItem.visual);
        }
        else if (cargoItem.visual != null)
        {
            BeltItemView.Release(cargoItem.visual, item);
            cargoItem.visual = null;
        }
        cargo.Add(cargoItem);
    }

    Vector3 EvaluatePath(Cargo item, float t)
    {
        float cell = GridFootprint.CellSize;
        Vector3 up = Vector3.up * resolvedHeight;
        Vector3 start = transform.position - BuildingLinker.CardinalToWorld(item.entryDir) * (cell * 0.5f) + up;
        Vector3 mid = transform.position + up;
        Vector3 end = transform.position + BuildingLinker.CardinalToWorld(item.exitDir) * (cell * 0.5f) + up;

        t = Mathf.Clamp01(t);
        if (item.entryDir == item.exitDir)
            return Vector3.Lerp(start, end, t);

        if (t < 0.5f)
            return Vector3.Lerp(start, mid, t * 2f);
        return Vector3.Lerp(mid, end, (t - 0.5f) * 2f);
    }

    Vector2Int InferEntryDir(BuildingSocket fromSocket)
    {
        if (fromSocket != null)
        {
            BuildingBase src = fromSocket.Owner;
            if (src != null && src != this)
                return InferEntryFromWorld(src.transform.position);
        }

        return Opposite(InputOutward());
    }

    Vector2Int InferEntryFromWorld(Vector3 world)
    {
        Vector2Int fromCell = BuildingLinker.WorldToCell(world);
        Vector2Int delta = Cell - fromCell;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
            return delta;

        Vector3 local = world - transform.position;
        local.y = 0f;
        if (local.sqrMagnitude > 0.0001f)
            return Opposite(BuildingLinker.ToCardinal(local));

        return Opposite(InputOutward());
    }

    Vector2Int ChooseExitDir(Vector2Int entryDir)
    {
        EnsureSetup();
        if (outputSockets == null || outputSockets.Length == 0)
            return entryDir.x == 0 && entryDir.y == 0
                ? BuildingLinker.ToCardinal(transform.forward)
                : entryDir;

        Vector2Int fallback = Vector2Int.zero;
        int count = outputSockets.Length;
        for (int n = 0; n < count; n++)
        {
            int index = (nextOutput + n) % count;
            BuildingSocket socket = outputSockets[index];
            if (socket == null)
                continue;

            Vector2Int dir = BuildingLinker.ToCardinal(socket.GetOutward());
            if (dir == Opposite(entryDir))
                continue;

            if (fallback.x == 0 && fallback.y == 0)
                fallback = dir;

            BuildingBase dest = BuildingLinker.GetBuildingAt(Cell + dir);
            if (dest == null || dest == this)
                continue;

            nextOutput = (index + 1) % count;
            return dir;
        }

        if (fallback.x != 0 || fallback.y != 0)
        {
            nextOutput = (nextOutput + 1) % count;
            return fallback;
        }

        return entryDir;
    }

    Vector2Int InputOutward()
    {
        if (InputSocket != null)
            return BuildingLinker.ToCardinal(InputSocket.GetOutward());

        return new Vector2Int(1, 0);
    }

    static Vector2Int Opposite(Vector2Int dir)
    {
        return new Vector2Int(-dir.x, -dir.y);
    }

    public override void WriteSave(BuildingSaveData save)
    {
        base.WriteSave(save);
        if (save == null)
            return;
        save.stateInt = nextOutput;
        save.cargo = new List<BeltItemSave>(cargo.Count);
        for (int i = 0; i < cargo.Count; i++)
        {
            Cargo entry = cargo[i];
            if (entry == null || entry.item == null || string.IsNullOrEmpty(entry.item.id))
                continue;
            save.cargo.Add(new BeltItemSave
            {
                itemId = entry.item.id,
                progress = entry.progress,
                entryX = entry.entryDir.x,
                entryY = entry.entryDir.y,
                exitX = entry.exitDir.x,
                exitY = entry.exitDir.y
            });
        }
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        ClearCargo();
        if (save == null)
            return;

        nextOutput = Mathf.Max(0, save.stateInt);
        if (save.cargo == null)
            return;

        for (int i = 0; i < save.cargo.Count; i++)
        {
            BeltItemSave entry = save.cargo[i];
            ItemData item = GameDatabase.FindItem(entry.itemId);
            if (item == null)
                continue;
            Vector2Int entryDir = new Vector2Int(entry.entryX, entry.entryY);
            Vector2Int exitDir = new Vector2Int(entry.exitX, entry.exitY);
            SpawnCargo(item, null, entryDir, exitDir);
            if (cargo.Count > 0)
                cargo[cargo.Count - 1].progress = Mathf.Clamp01(entry.progress);
        }
    }

    public void DevClearCargo()
    {
        ClearCargo();
    }

    void ClearCargo()
    {
        for (int i = 0; i < cargo.Count; i++)
            ReleaseCargoVisual(cargo[i]);
        cargo.Clear();
    }

    void EnsureSetup()
    {
        DisableChildColliders();
        EnsureCollider();
        BindPrefabSockets();
        resolvedHeight = ResolveItemHeight();
    }

    void DisableChildColliders()
    {
        Collider[] childCols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childCols.Length; i++)
        {
            if (childCols[i] != null && childCols[i].gameObject != gameObject)
                childCols[i].enabled = false;
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
            0.35f / Mathf.Max(0.01f, lossy.y),
            cell / Mathf.Max(0.01f, lossy.z)
        );
        box.center = new Vector3(0f, 0.18f, 0f);
        box.enabled = true;
    }

    void BindPrefabSockets()
    {
        if (NeedSocket(inputSockets))
        {
            inputSockets = new[]
            {
                FindOrCreateSocket("InputSocket", SocketType.Input, new Vector3(0f, 0.3f, -0.5f), BuildingPrefabLayout.InputRotation)
            };
        }
        else
        {
            inputSockets[0].socketType = SocketType.Input;
        }

        if (NeedSocket(outputSockets) || CountValid(outputSockets) < 3)
        {
            outputSockets = new[]
            {
                FindOrCreateSocket("OutputSocket", SocketType.Output, new Vector3(0f, 0.3f, 0.5f), BuildingPrefabLayout.OutputRotation),
                FindOrCreateSocket("OutputSocket (1)", SocketType.Output, new Vector3(0.5f, 0.3f, 0f), Quaternion.Euler(0f, 90f, 0f)),
                FindOrCreateSocket("OutputSocket (2)", SocketType.Output, new Vector3(-0.5f, 0.3f, 0f), Quaternion.Euler(0f, -90f, 0f))
            };
        }
        else
        {
            for (int i = 0; i < outputSockets.Length; i++)
            {
                if (outputSockets[i] != null)
                    outputSockets[i].socketType = SocketType.Output;
            }
        }
    }

    static int CountValid(BuildingSocket[] sockets)
    {
        if (sockets == null)
            return 0;

        int count = 0;
        for (int i = 0; i < sockets.Length; i++)
        {
            if (sockets[i] != null)
                count++;
        }

        return count;
    }

    static bool NeedSocket(BuildingSocket[] sockets)
    {
        return sockets == null || sockets.Length == 0 || sockets[0] == null;
    }

    BuildingSocket FindOrCreateSocket(string socketName, SocketType type, Vector3 localPos, Quaternion localRot)
    {
        Transform existing = transform.Find(socketName);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
            BuildingPrefabLayout.PlaceSocket(existing, localPos, localRot);
        }
        else
        {
            go = new GameObject(socketName);
            go.transform.SetParent(transform, false);
            BuildingPrefabLayout.PlaceSocket(go.transform, localPos, localRot);
        }

        BuildingSocket socket = go.GetComponent<BuildingSocket>();
        if (socket == null)
            socket = go.AddComponent<BuildingSocket>();
        socket.socketType = type;
        return socket;
    }

    float ResolveItemHeight()
    {
        float height = Mathf.Max(0.12f, itemHeight);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;
            if (renderer.transform.name.IndexOf("BeltItem", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (renderer.transform.GetComponentInParent<BuildingSocket>() != null)
                continue;

            float surface = renderer.bounds.max.y - transform.position.y;
            if (surface > 0.05f && surface < 1.25f)
                height = Mathf.Max(height, surface + 0.06f);
        }

        return height;
    }

    protected override void OnDestroy()
    {
        ClearCargo();
        WorldSim.UnregisterSplitter(this);
        base.OnDestroy();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebug)
            return;

        Vector3 pos = transform.position + Vector3.up * 0.25f;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(pos, BuildingLinker.CardinalToWorld(InputOutward()) * 0.6f);
        if (outputSockets == null)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < outputSockets.Length; i++)
        {
            if (outputSockets[i] != null)
                Gizmos.DrawRay(pos, outputSockets[i].GetOutward() * 0.8f);
        }
    }
#endif
}
