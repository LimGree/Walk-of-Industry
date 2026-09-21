using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Клетка ленты. Выход = transform.forward. Форма — таблица [[BeltRules]] по маске входов.
/// Сокетов нет: соседство + ExitDir.
/// </summary>
public class Conveyor : BuildingBase, IInteractable
{
    [Header("Belt")]
    public float speed = 2.5f;
    public int maxItems = 3;
    public float itemHeight = 0.42f;
    public float itemScale = 0.38f;

    [Header("Debug")]
    public bool showDebug;

    public static readonly Dictionary<Vector2Int, Vector2Int> PreviewExits =
        new Dictionary<Vector2Int, Vector2Int>(32);

    static readonly List<Conveyor> WorldVisualOverride = new List<Conveyor>(16);

    public Vector2Int Cell => BuildingLinker.WorldToCell(transform.position);
    public Vector2Int ExitDir { get; private set; } = new Vector2Int(0, 1);
    public BeltInMask InMask { get; private set; }
    public BeltShape Shape { get; private set; } = BeltShape.Straight;
    public bool IsPreview => isPreview;
    public bool FromBack => BeltRules.Has(InMask, BeltInMask.Back);
    public bool FromLeft => BeltRules.Has(InMask, BeltInMask.Left);
    public bool FromRight => BeltRules.Has(InMask, BeltInMask.Right);
    public ItemData Filter => filter;

    ItemData filter;
    SpriteRenderer filterChip;
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly MaterialPropertyBlock TintBlock = new MaterialPropertyBlock();

    public struct Incoming
    {
        public BeltInMask mask;
        public BeltShape shape;
    }

    bool isLive;
    bool isPreview;
    BeltInMask lastServed = BeltInMask.None;
    float simCarry;
    SocketArrow[] arrowCache;
    GameObject straightVisual;
    GameObject cornerVisual;
    GameObject teeVisual;
    GameObject sidesVisual;
    GameObject tripleVisual;
    Renderer[] teeDecalRenderers;
    Material[][] teeDecalOriginals;
    readonly List<BeltCargo> cargo = new List<BeltCargo>(4);

    class BeltCargo
    {
        public ItemData item;
        public float progress;
        public Transform visual;
        public Vector2Int entryDir;
    }

    void Awake()
    {
        EnsureSetup();
        RefreshExitFromTransform();
        ClearBeltSockets();
    }

    public void Interact(GameObject interactor)
    {
        BeltRide.TryInteract(this, interactor);
    }

    public void SetFilter(ItemData item)
    {
        filter = item;
        ApplyFilterLook();
    }

    public override void OnPlaced()
    {
        isLive = true;
        EnsureSetup();
        RefreshExitFromTransform();
        ClearBeltSockets();
        WorldSim.RegisterBelt(this);
        base.OnPlaced();
    }

    public override void OnRemoved()
    {
        isLive = false;
        ClearCargo();
        WorldSim.UnregisterBelt(this);
        base.OnRemoved();
    }

    public override void OnRotated()
    {
        RefreshExitFromTransform();
        base.OnRotated();
    }

    public void PreparePreview(BuildingData previewData)
    {
        isPreview = true;
        isLive = false;
        if (previewData != null)
            data = previewData;
        DestroyCreatedVisuals();
        EnsureSetup();
        ClearBeltSockets();
    }

    public void Preview(Vector3 worldPos, Quaternion rotation)
    {
        isLive = false;
        transform.SetPositionAndRotation(worldPos, rotation);
        ApplyIncoming(ComputeIncoming());
    }

    /// <summary>
    /// Только картинка. Симуляция (маска, груз) не меняется.
    /// Нужно, чтобы гост показал, какой формы станет уже стоящая лента.
    /// </summary>
    public void ShowIncomingVisual(Incoming incoming)
    {
        BeltShape simShape = Shape;
        BeltInMask simMask = InMask;
        Shape = incoming.shape;
        InMask = incoming.mask;
        ApplyVisual();
        Shape = simShape;
        InMask = simMask;
    }

    public void RestoreVisual()
    {
        ApplyVisual();
    }

    public static void RegisterPreviewExit(Vector3 worldPos, float yaw)
    {
        PreviewExits[BuildingLinker.WorldToCell(worldPos)] = BuildingLinker.ExitDirFromYaw(yaw);
    }

    public static void ApplyWorldVisualOverrides()
    {
        ClearWorldVisualOverrides();
        if (PreviewExits.Count == 0)
            return;

        var seen = new HashSet<int>();
        foreach (KeyValuePair<Vector2Int, Vector2Int> kv in PreviewExits)
        {
            Vector2Int front = kv.Key + kv.Value;
            if (PreviewExits.ContainsKey(front))
                continue;

            Conveyor belt = BuildingLinker.GetBuildingAt(front) as Conveyor;
            if (belt == null || belt.isPreview)
                continue;
            if (!seen.Add(belt.GetInstanceID()))
                continue;

            belt.ShowIncomingVisual(belt.ComputeIncoming());
            WorldVisualOverride.Add(belt);
        }
    }

    public static void ClearWorldVisualOverrides()
    {
        for (int i = 0; i < WorldVisualOverride.Count; i++)
        {
            if (WorldVisualOverride[i] != null)
                WorldVisualOverride[i].RestoreVisual();
        }
        WorldVisualOverride.Clear();
    }

    public Incoming ComputeIncoming()
    {
        EnsureSetup();
        RefreshExitFromTransform();
        Vector2Int cell = Cell;
        Vector2Int exit = ExitDir;
        Incoming incoming;
        incoming.mask = BeltRules.MaskFromFlags(
            NeighborFeeds(cell + BeltRules.BackNeighbor(exit), cell),
            NeighborFeeds(cell + BeltRules.LeftNeighbor(exit), cell),
            NeighborFeeds(cell + BeltRules.RightNeighbor(exit), cell));
        incoming.shape = BeltRules.Shape(incoming.mask);
        return incoming;
    }

    public void ApplyIncoming(Incoming incoming)
    {
        InMask = incoming.mask;
        Shape = incoming.shape;
        ApplyVisual();
        RemapCargoToValidEntries();
    }

    static bool NeighborFeeds(Vector2Int neighborCell, Vector2Int selfCell)
    {
        if (PreviewExits.TryGetValue(neighborCell, out Vector2Int previewExit))
            return neighborCell + previewExit == selfCell;

        BuildingBase other = BuildingLinker.GetBuildingAt(neighborCell);
        if (other == null)
            return false;
        return BuildingLinker.FeedsInto(other, selfCell);
    }

    public bool IsFedBy(BuildingBase source)
    {
        if (source == null)
            return false;
        return BuildingLinker.FeedsInto(source, Cell) && SideFromSource(source) != BeltInMask.None;
    }

    public bool AcceptsFromCell(Vector2Int fromCell)
    {
        Vector2Int delta = fromCell - Cell;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) != 1)
            return false;
        return delta != ExitDir;
    }

    public override bool CanAcceptFrom(BuildingBase source)
    {
        return IsFedBy(source);
    }

    protected bool MatchesFilter(ItemData item)
    {
        return filter == null || item == filter;
    }

    protected virtual bool AcceptsItem(ItemData item)
    {
        return item != null && !item.isFluid && MatchesFilter(item);
    }

    protected virtual bool ShowCargoVisual => true;

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        BuildingBase source = SourceFromSocket(fromSocket);
        Vector2Int entry = InferEntryDir(fromSocket, source);
        return TryAccept(item, null, entry);
    }

    public bool TryAcceptTransfer(ItemData item, Transform visual, BuildingBase source = null)
    {
        Vector2Int entry = InferEntryDir(null, source, visual);
        return TryAccept(item, visual, entry);
    }

    bool TryAccept(ItemData item, Transform visual, Vector2Int entry)
    {
        if (!isLive || !AcceptsItem(item))
            return false;
        if (!IsValidEntry(entry))
            entry = ExitDir;
        if (!IsValidEntry(entry) || !CanAccept(entry))
            return false;
        SpawnCargo(item, 0f, visual, entry);
        return true;
    }

    public bool HasRoomForItem()
    {
        return isLive && cargo.Count < Mathf.Max(1, maxItems);
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
            BeltCargo entry = cargo[i];
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

        BeltCargo stolen = cargo[best];
        item = stolen.item;
        visual = stolen.visual;
        cargo.RemoveAt(best);
        if (visual != null)
            visual.SetParent(null, true);
        return true;
    }

    float ItemGap => 1f / Mathf.Max(1, maxItems);

    bool CanAccept(Vector2Int entryDir)
    {
        if (cargo.Count >= Mathf.Max(1, maxItems))
            return false;

        float nearestToEntry = 1f;
        bool any = false;
        for (int i = 0; i < cargo.Count; i++)
        {
            if (cargo[i].entryDir != entryDir)
                continue;
            any = true;
            if (cargo[i].progress < nearestToEntry)
                nearestToEntry = cargo[i].progress;
        }

        if (any && nearestToEntry < ItemGap)
            return false;

        if (!IsMergeTile())
            return true;

        BeltInMask side = BeltRules.SideFromTravel(ExitDir, entryDir);
        BeltInMask served = NextServedSide();
        return served == BeltInMask.None || side == served;
    }

    bool IsMergeTile()
    {
        int n = 0;
        if (FromBack) n++;
        if (FromLeft) n++;
        if (FromRight) n++;
        return n >= 2;
    }

    public bool HasItemReadyToExit()
    {
        if (!isLive)
            return false;
        for (int i = 0; i < cargo.Count; i++)
        {
            if (cargo[i] != null && cargo[i].progress >= 0.999f)
                return true;
        }

        return false;
    }

    bool IsValidEntry(Vector2Int entry)
    {
        if (entry.x == 0 && entry.y == 0)
            return false;
        return BeltRules.SideFromTravel(ExitDir, entry) != BeltInMask.None;
    }

    public void SimDraw()
    {
        if (!ShowCargoVisual)
            return;
        SubmitCargoDraws();
    }

    public void SimStep(float dt)
    {
        if (!isLive)
            return;

        float cell = GridFootprint.CellSize;
        float boost = BeltSpeedSystem.Instance != null ? BeltSpeedSystem.Instance.Multiplier : 1f;
        float move = speed * boost * dt / Mathf.Max(0.05f, cell);
        float gap = ItemGap;
        BeltInMask served = NextServedSide();

        for (int i = 0; i < cargo.Count; i++)
        {
            float progress = cargo[i].progress;
            float limit = progress >= BeltRules.MergeT ? 1f : BeltRules.MergeT;
            for (int j = 0; j < cargo.Count; j++)
            {
                if (j == i)
                    continue;
                float other = cargo[j].progress;
                if (progress < BeltRules.MergeT && other >= BeltRules.MergeT)
                    continue;
                if (progress < BeltRules.MergeT
                    && cargo[j].entryDir != cargo[i].entryDir
                    && other < BeltRules.MergeT)
                    continue;
                if (other > progress)
                    limit = Mathf.Min(limit, other - gap);
            }

            if (progress < BeltRules.MergeT)
            {
                BeltInMask side = BeltRules.SideFromTravel(ExitDir, cargo[i].entryDir);
                if (served != BeltInMask.None
                    && side != BeltInMask.None
                    && side != served
                    && progress + move >= BeltRules.MergeT)
                    continue;
            }

            if (limit < 0f)
                limit = 0f;
            if (progress < limit)
                cargo[i].progress = Mathf.Min(limit, progress + move);
            if (progress < BeltRules.MergeT && cargo[i].progress >= BeltRules.MergeT && served != BeltInMask.None)
                lastServed = served;
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
                // Если visual был передан следующему объекту,
                // источник больше не владеет им.
                if (cargo[front].visual != null)
                    ReleaseCargoVisual(cargo[front]);

                cargo.RemoveAt(front);
            }
            else
                cargo[front].progress = 1f;
        }
    }

    void SubmitCargoDraws()
    {
        bool show = ShowCargoVisual && WorldView.InRange(transform.position);
        for (int i = 0; i < cargo.Count; i++)
        {
            BeltCargo item = cargo[i];
            if (show)
            {
                if (item.visual == null)
                    item.visual = BeltItemView.Rent(item.item, itemScale);
                BeltInMask side = BeltRules.SideFromTravel(ExitDir, item.entryDir);
                Vector3 pos = BeltRules.PathWorld(transform, side, item.progress, itemHeight);
                Vector3 look = BeltRules.PathWorld(transform, side, Mathf.Min(1f, item.progress + 0.05f), itemHeight) - pos;
                BeltItemView.Update(item.visual, pos, look);
            }
            else
                ReleaseCargoVisual(item);
        }
    }

    bool TryHandOff(BeltCargo item)
    {
        Vector2Int nextCell = Cell + ExitDir;
        BuildingBase dest = BuildingLinker.GetBuildingAt(nextCell);
        if (dest == null)
            return false;

        Conveyor nextBelt = dest as Conveyor;
        if (nextBelt != null)
        {
            bool accepted = nextBelt.TryAcceptTransfer(item.item, item.visual, this);

            if (accepted)
                item.visual = null;

            return accepted;
        }

        Splitter nextSplit = dest as Splitter;
        if (nextSplit != null)
        {
            if (!nextSplit.CanAcceptFrom(this))
                return false;

            bool accepted = nextSplit.TryAcceptTransfer(item.item, item.visual);

            if (accepted)
                item.visual = null;

            return accepted;
        }

        if (!dest.CanAcceptFrom(this))
            return false;

        BuildingSocket destInput = dest.inputSockets != null && dest.inputSockets.Length > 0
            ? dest.inputSockets[0]
            : null;
        return dest.TryReceiveItem(item.item, destInput);
    }

    static void ReleaseCargoVisual(BeltCargo item)
    {
        if (item == null || item.visual == null)
            return;
        BeltItemView.Release(item.visual, item.item);
        item.visual = null;
    }

    void SpawnCargo(ItemData item, float progress, Transform existingVisual, Vector2Int entryDir)
    {
        if (!IsValidEntry(entryDir))
            entryDir = ExitDir;

        BeltCargo cargoItem = new BeltCargo
        {
            item = item,
            progress = progress,
            visual = existingVisual,
            entryDir = entryDir
        };

        bool show = ShowCargoVisual && WorldView.InRange(transform.position);
        if (show)
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

    public int CargoCount => cargo.Count;

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

    BeltInMask NextServedSide()
    {
        BeltInMask waiting = BeltInMask.None;
        for (int i = 0; i < cargo.Count; i++)
        {
            if (cargo[i].progress >= BeltRules.MergeT)
                continue;
            waiting |= BeltRules.SideFromTravel(ExitDir, cargo[i].entryDir);
        }

        if (IsMergeTile())
        {
            if (FromBack && !BeltRules.Has(waiting, BeltInMask.Back) && FeederReady(BeltInMask.Back))
                waiting |= BeltInMask.Back;
            if (FromLeft && !BeltRules.Has(waiting, BeltInMask.Left) && FeederReady(BeltInMask.Left))
                waiting |= BeltInMask.Left;
            if (FromRight && !BeltRules.Has(waiting, BeltInMask.Right) && FeederReady(BeltInMask.Right))
                waiting |= BeltInMask.Right;
        }

        BeltInMask[] order = { BeltInMask.Back, BeltInMask.Left, BeltInMask.Right };
        int start = 0;
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == lastServed)
            {
                start = (i + 1) % order.Length;
                break;
            }
        }

        for (int n = 0; n < order.Length; n++)
        {
            BeltInMask bit = order[(start + n) % order.Length];
            if (BeltRules.Has(waiting, bit))
                return bit;
        }

        return BeltInMask.None;
    }

    bool FeederReady(BeltInMask side)
    {
        Vector2Int neighbor = Cell + BeltRules.NeighborDelta(ExitDir, side);
        BuildingBase b = BuildingLinker.GetBuildingAt(neighbor);
        if (b == null || b == this || !BuildingLinker.FeedsInto(b, Cell))
            return false;

        Conveyor belt = b as Conveyor;
        if (belt != null)
            return belt.HasItemReadyToExit();

        Splitter splitter = b as Splitter;
        if (splitter != null)
            return splitter.HasItemReadyToward(Cell);

        UndergroundConveyor tunnel = b as UndergroundConveyor;
        if (tunnel != null)
            return tunnel.isExit && tunnel.OutputBufferCount > 0;

        return b.OutputBufferCount > 0;
    }

    void RemapCargoToValidEntries()
    {
        Vector2Int fallback = ExitDir;
        if (BeltRules.Has(InMask, BeltInMask.Left))
            fallback = BeltRules.Travel(ExitDir, BeltInMask.Left);
        else if (BeltRules.Has(InMask, BeltInMask.Right))
            fallback = BeltRules.Travel(ExitDir, BeltInMask.Right);

        for (int i = 0; i < cargo.Count; i++)
        {
            if (!IsValidEntry(cargo[i].entryDir))
                cargo[i].entryDir = fallback;
        }
    }

    void RefreshExitFromTransform()
    {
        ExitDir = BuildingLinker.ToCardinal(transform.forward);
    }

    static BuildingBase SourceFromSocket(BuildingSocket fromSocket)
    {
        if (fromSocket == null)
            return null;
        BuildingBase owner = fromSocket.Owner;
        if (owner != null)
            return owner;
        if (fromSocket.connectedSocket != null)
            return fromSocket.connectedSocket.Owner;
        return null;
    }

    Vector2Int InferEntryDir(BuildingSocket fromSocket, BuildingBase source, Transform visual = null)
    {
        BuildingBase src = source;
        if (src == null)
            src = SourceFromSocket(fromSocket);
        if (src == this)
            src = fromSocket != null && fromSocket.connectedSocket != null
                ? fromSocket.connectedSocket.Owner
                : null;

        BeltInMask fromSource = SideFromSource(src);
        if (fromSource != BeltInMask.None)
            return BeltRules.Travel(ExitDir, fromSource);

        if (visual != null)
        {
            Vector2Int fromWorld = EntryFromWorld(visual.position);
            if (IsValidEntry(fromWorld))
                return fromWorld;
        }

        return Vector2Int.zero;
    }

    BeltInMask SideFromSource(BuildingBase source)
    {
        if (source == null || source == this)
            return BeltInMask.None;

        Vector2Int exit = ExitDir;
        if (BuildingLinker.OccupiesCell(source, Cell + BeltRules.BackNeighbor(exit)))
            return BeltInMask.Back;
        if (BuildingLinker.OccupiesCell(source, Cell + BeltRules.LeftNeighbor(exit)))
            return BeltInMask.Left;
        if (BuildingLinker.OccupiesCell(source, Cell + BeltRules.RightNeighbor(exit)))
            return BeltInMask.Right;
        return BeltInMask.None;
    }

    Vector2Int EntryFromWorld(Vector3 world)
    {
        Vector2Int fromCell = BuildingLinker.WorldToCell(world);
        Vector2Int delta = Cell - fromCell;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
            return IsValidEntry(delta) ? delta : Vector2Int.zero;

        Vector3 local = world - transform.position;
        local.y = 0f;
        if (local.sqrMagnitude < 0.0001f)
            return Vector2Int.zero;
        Vector2Int inward = BuildingLinker.ToCardinal(local);
        Vector2Int travel = new Vector2Int(-inward.x, -inward.y);
        return IsValidEntry(travel) ? travel : Vector2Int.zero;
    }

    void ApplyVisual()
    {
        EnsureCollider();
        if (straightVisual == null)
            straightVisual = FindStraightVisual();

        BeltShape shown = Shape;
        BeltRules.GetVisual(shown, InMask, out float extraYaw, out bool mirrorX);
        if (straightVisual != null)
        {
            straightVisual.SetActive(shown == BeltShape.Straight);
            if (shown == BeltShape.Straight)
                EnableRenderers(straightVisual);
        }

        GameObject form = EnsureForm(shown);
        if (shown != BeltShape.Straight)
            PresentVisual(form, true, extraYaw, mirrorX);
        if (shown == BeltShape.Tee)
            ApplyTeeArrowMaterial(form, UseTeeMirrorTexture(InMask));
        ApplyFilterLook();
        InvalidateArrows();
        RefreshArrows();
    }

    GameObject EnsureForm(BeltShape shown)
    {
        if (shown == BeltShape.Straight)
        {
            DestroyVisualRoot(ref cornerVisual);
            ClearTeeDecalCache();
            DestroyVisualRoot(ref teeVisual);
            DestroyVisualRoot(ref sidesVisual);
            DestroyVisualRoot(ref tripleVisual);
            return straightVisual;
        }

        if (shown != BeltShape.Corner)
            DestroyVisualRoot(ref cornerVisual);
        if (shown != BeltShape.Tee)
        {
            ClearTeeDecalCache();
            DestroyVisualRoot(ref teeVisual);
        }
        if (shown != BeltShape.Sides)
            DestroyVisualRoot(ref sidesVisual);
        if (shown != BeltShape.Triple)
            DestroyVisualRoot(ref tripleVisual);

        switch (shown)
        {
            case BeltShape.Corner:
                if (cornerVisual == null)
                    cornerVisual = CreateChildVisual("CornerVisual", data != null ? data.cornerPrefab : null, data != null ? data.cornerGhostPrefab : null);
                return cornerVisual;
            case BeltShape.Tee:
                if (teeVisual == null)
                    teeVisual = CreateChildVisual("TeeVisual", data != null ? data.teePrefab : null, data != null ? data.teeGhostPrefab : null);
                return teeVisual;
            case BeltShape.Sides:
                if (sidesVisual == null)
                    sidesVisual = CreateChildVisual("SidesVisual", data != null ? data.sidesPrefab : null, data != null ? data.sidesGhostPrefab : null);
                return sidesVisual;
            case BeltShape.Triple:
                if (tripleVisual == null)
                    tripleVisual = CreateChildVisual("TripleVisual", data != null ? data.triplePrefab : null, data != null ? data.tripleGhostPrefab : null);
                return tripleVisual;
            default:
                return straightVisual;
        }
    }

    public bool ShouldShowIoArrow(SocketArrow arrow)
    {
        if (arrow == null || !arrow.gameObject.activeInHierarchy)
            return false;

        RefreshExitFromTransform();
        BeltShape shown = ResolveShownShape();
        if (!ArrowBelongsToShownVisual(arrow, shown))
            return false;

        if (shown == BeltShape.Tee || shown == BeltShape.Sides || shown == BeltShape.Triple)
            return true;

        Vector2Int delta = ArrowNeighborDelta(arrow);
        if (delta.x == 0 && delta.y == 0)
            return false;
        return !HasLogisticsAt(Cell + delta);
    }

    void InvalidateArrows()
    {
        arrowCache = null;
    }

    void RefreshArrows()
    {
        if (arrowCache == null)
            arrowCache = GetComponentsInChildren<SocketArrow>(true);
        for (int i = 0; i < arrowCache.Length; i++)
        {
            if (arrowCache[i] != null)
                arrowCache[i].Apply();
        }
    }

    bool ArrowBelongsToShownVisual(SocketArrow arrow, BeltShape shown)
    {
        Transform t = arrow.transform;
        if (UnderVisual(t, tripleVisual))
            return shown == BeltShape.Triple;
        if (UnderVisual(t, sidesVisual))
            return shown == BeltShape.Sides;
        if (UnderVisual(t, teeVisual))
            return shown == BeltShape.Tee;
        if (UnderVisual(t, cornerVisual))
            return shown == BeltShape.Corner;
        if (UnderVisual(t, straightVisual))
            return shown == BeltShape.Straight;
        return shown == BeltShape.Straight;
    }

    static bool UnderVisual(Transform t, GameObject visual)
    {
        return visual != null && t.IsChildOf(visual.transform);
    }

    Vector2Int ArrowNeighborDelta(SocketArrow arrow)
    {
        Vector3 local = transform.InverseTransformPoint(arrow.transform.position);
        local.y = 0f;
        if (local.sqrMagnitude < 0.04f)
            return Vector2Int.zero;
        if (Mathf.Abs(local.z) >= Mathf.Abs(local.x))
            return local.z >= 0f ? ExitDir : BeltRules.BackNeighbor(ExitDir);
        return local.x >= 0f ? BeltRules.RightNeighbor(ExitDir) : BeltRules.LeftNeighbor(ExitDir);
    }

    bool HasLogisticsAt(Vector2Int cell)
    {
        if (cell == Cell)
            return false;
        if (PreviewExits.ContainsKey(cell))
            return true;

        Conveyor other = BuildingLinker.GetBuildingAt(cell) as Conveyor;
        if (other == null || other == this || other.IsPreview)
            return false;
        return (this is Pipe) == (other is Pipe);
    }

    BeltShape ResolveShownShape()
    {
        return Shape;
    }

    static bool UseTeeMirrorTexture(BeltInMask mask)
    {
        return BeltRules.Has(mask, BeltInMask.Left) && !BeltRules.Has(mask, BeltInMask.Right);
    }

    void ApplyTeeArrowMaterial(GameObject visual, bool useMirror)
    {
        Material mirror = data != null ? data.teeMirrorMaterial : null;
        if (visual == null || mirror == null)
            return;

        if (teeDecalRenderers == null || teeDecalRenderers.Length == 0)
            CacheTeeDecals(visual);

        for (int i = 0; i < teeDecalRenderers.Length; i++)
        {
            Renderer renderer = teeDecalRenderers[i];
            Material[] original = i < teeDecalOriginals.Length ? teeDecalOriginals[i] : null;
            if (renderer == null || original == null)
                continue;

            var mats = new Material[original.Length];
            for (int m = 0; m < original.Length; m++)
            {
                Material mat = original[m];
                mats[m] = useMirror && IsTeeArrowMaterial(mat) ? mirror : mat;
            }

            renderer.sharedMaterials = mats;
        }
    }

    void CacheTeeDecals(GameObject visual)
    {
        Renderer[] found = visual.GetComponentsInChildren<Renderer>(true);
        teeDecalRenderers = found;
        teeDecalOriginals = new Material[found.Length][];
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] == null)
                continue;
            Material[] mats = found[i].sharedMaterials;
            teeDecalOriginals[i] = new Material[mats.Length];
            for (int m = 0; m < mats.Length; m++)
                teeDecalOriginals[i][m] = mats[m];
        }
    }

    void ClearTeeDecalCache()
    {
        teeDecalRenderers = null;
        teeDecalOriginals = null;
    }

    static bool IsTeeArrowMaterial(Material mat)
    {
        if (mat == null)
            return false;
        string n = mat.name;
        if (n.IndexOf("Mirror", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return false;
        if (n.IndexOf("4_5", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (n.IndexOf("005", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return n.IndexOf("Рисунок", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void PresentVisual(GameObject visual, bool on, float extraYaw, bool mirrorX)
    {
        if (visual == null)
            return;
        visual.SetActive(on);
        if (!on)
            return;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0f, extraYaw, 0f);
        Vector3 scale = visual.transform.localScale;
        float ax = Mathf.Abs(scale.x);
        if (ax < 0.0001f)
            ax = 1f;
        visual.transform.localScale = new Vector3(mirrorX ? -ax : ax, scale.y, scale.z);
    }

    void EnsureSetup()
    {
        EnsureCollider();
        if (straightVisual == null)
            straightVisual = FindStraightVisual();
        if (straightVisual != null)
            StripBeltExtras(straightVisual);
        ClearBeltSockets();
    }

    void ClearBeltSockets()
    {
        inputSockets = System.Array.Empty<BuildingSocket>();
        outputSockets = System.Array.Empty<BuildingSocket>();
    }

    void EnsureCollider()
    {
        Collider[] childCols = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < childCols.Length; i++)
        {
            if (childCols[i] != null && childCols[i].gameObject != gameObject)
                childCols[i].enabled = false;
        }

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

    void DestroyCreatedVisuals()
    {
        DestroyVisualRoot(ref cornerVisual);
        ClearTeeDecalCache();
        DestroyVisualRoot(ref teeVisual);
        DestroyVisualRoot(ref sidesVisual);
        DestroyVisualRoot(ref tripleVisual);
    }

    static void DestroyVisualRoot(ref GameObject visual)
    {
        if (visual == null)
            return;
        visual.SetActive(false);
        Destroy(visual);
        visual = null;
    }

    GameObject FindStraightVisual()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null)
                continue;

            string n = child.name;
            if (n.IndexOf("Corner", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Tee", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Sides", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Triple", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Socket", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("Point", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("BeltItem", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (child.GetComponentInChildren<Renderer>() == null)
                continue;

            return child.gameObject;
        }

        return null;
    }

    GameObject CreateChildVisual(string visualName, GameObject livePrefab, GameObject ghostPrefab)
    {
        GameObject src = isPreview && ghostPrefab != null ? ghostPrefab : livePrefab;
        if (src == null)
            src = livePrefab;
        if (src == null)
            return null;

        GameObject visual = Instantiate(src, transform);
        visual.name = visualName;
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        MatchLayer(visual, gameObject.layer);
        StripRuntimeComponents(visual);
        StripBeltExtras(visual);
        visual.SetActive(false);
        return visual;
    }

    static void MatchLayer(GameObject go, int layer)
    {
        if (go == null)
            return;
        go.layer = layer;
        Transform t = go.transform;
        for (int i = 0; i < t.childCount; i++)
            MatchLayer(t.GetChild(i).gameObject, layer);
    }

    static void StripRuntimeComponents(GameObject root)
    {
        if (root == null)
            return;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                behaviours[i].enabled = false;
        }
    }

    static void EnableRenderers(GameObject root)
    {
        if (root == null)
            return;
        MeshRenderer[] rends = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null)
                rends[i].enabled = true;
        }
    }

    static void StripBeltExtras(GameObject root)
    {
        if (root == null)
            return;
        var kill = new List<GameObject>(8);
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.gameObject == root)
                continue;
            string n = t.name;
            if (n.StartsWith("IoArrow", System.StringComparison.OrdinalIgnoreCase))
                kill.Add(t.gameObject);
        }
        for (int i = 0; i < kill.Count; i++)
        {
            if (kill[i] != null)
                Object.Destroy(kill[i]);
        }
    }

    void ApplyFilterLook()
    {
        Color tint = FilterTint(filter);
        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        int painted = 0;
        for (int i = 0; i < rends.Length; i++)
        {
            Renderer r = rends[i];
            if (r == null || r is SpriteRenderer)
                continue;
            if (!IsArrowRenderer(r))
                continue;
            PaintRenderer(r, tint);
            painted++;
        }

        if (painted == 0)
        {
            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null || r is SpriteRenderer)
                    continue;
                PaintRenderer(r, tint);
            }
        }

        RefreshFilterChip();
    }

    static void PaintRenderer(Renderer r, Color tint)
    {
        r.GetPropertyBlock(TintBlock);
        TintBlock.SetColor(ColorId, tint);
        TintBlock.SetColor(BaseColorId, tint);
        r.SetPropertyBlock(TintBlock);
    }

    static bool IsArrowRenderer(Renderer r)
    {
        Material mat = r.sharedMaterial;
        if (mat == null)
            return false;
        return IsTeeArrowMaterial(mat);
    }

    static Color FilterTint(ItemData item)
    {
        if (item == null)
            return Color.white;
        string id = item.id != null ? item.id.ToLowerInvariant() : "";
        if (id.IndexOf("iron") >= 0) return new Color(0.95f, 0.55f, 0.22f, 1f);
        if (id.IndexOf("copper") >= 0 || id.IndexOf("cooper") >= 0) return new Color(0.95f, 0.38f, 0.18f, 1f);
        if (id.IndexOf("coal") >= 0) return new Color(0.35f, 0.38f, 0.42f, 1f);
        if (id.IndexOf("gear") >= 0) return new Color(1f, 0.85f, 0.2f, 1f);
        if (id.IndexOf("water") >= 0) return new Color(0.25f, 0.65f, 1f, 1f);
        if (id.IndexOf("oil") >= 0) return new Color(0.25f, 0.18f, 0.28f, 1f);
        if (id.IndexOf("sulfur") >= 0) return new Color(0.95f, 0.9f, 0.25f, 1f);
        if (id.IndexOf("stone") >= 0 || id.IndexOf("brick") >= 0) return new Color(0.7f, 0.62f, 0.5f, 1f);
        if (id.IndexOf("sand") >= 0) return new Color(0.9f, 0.8f, 0.45f, 1f);
        if (id.IndexOf("log") >= 0 || id.IndexOf("wood") >= 0 || id.IndexOf("plank") >= 0)
            return new Color(0.65f, 0.42f, 0.22f, 1f);
        int h = id.GetHashCode();
        float hue = Mathf.Abs(h % 1000) / 1000f;
        return Color.HSVToRGB(hue, 0.65f, 0.95f);
    }

    void RefreshFilterChip()
    {
        if (filter == null || filter.icon == null)
        {
            if (filterChip != null)
                filterChip.enabled = false;
            return;
        }

        if (filterChip == null)
        {
            var go = new GameObject("FilterChip");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, itemHeight + 0.22f, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = Vector3.one * 0.35f;
            filterChip = go.AddComponent<SpriteRenderer>();
            filterChip.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            filterChip.receiveShadows = false;
        }

        filterChip.enabled = true;
        filterChip.sprite = filter.icon;
    }

    public override void WriteSave(BuildingSaveData save)
    {
        base.WriteSave(save);
        if (save == null)
            return;
        save.filterItemId = filter != null ? filter.id : "";
        save.cargo = new List<BeltItemSave>(cargo.Count);
        for (int i = 0; i < cargo.Count; i++)
        {
            BeltCargo entry = cargo[i];
            if (entry == null || entry.item == null || string.IsNullOrEmpty(entry.item.id))
                continue;
            save.cargo.Add(new BeltItemSave
            {
                itemId = entry.item.id,
                progress = entry.progress,
                entryX = entry.entryDir.x,
                entryY = entry.entryDir.y,
                exitX = ExitDir.x,
                exitY = ExitDir.y
            });
        }
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        ClearCargo();
        filter = save != null ? GameDatabase.FindItem(save.filterItemId) : null;
        ApplyFilterLook();
        if (save == null || save.cargo == null)
            return;

        for (int i = 0; i < save.cargo.Count; i++)
        {
            BeltItemSave entry = save.cargo[i];
            ItemData item = GameDatabase.FindItem(entry.itemId);
            if (item == null)
                continue;
            Vector2Int entryDir = new Vector2Int(entry.entryX, entry.entryY);
            SpawnCargo(item, Mathf.Clamp01(entry.progress), null, entryDir);
        }
    }

    protected override void OnDestroy()
    {
        ClearCargo();
        WorldSim.UnregisterBelt(this);
        base.OnDestroy();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showDebug)
            return;

        RefreshExitFromTransform();
        float half = GridFootprint.CellSize * 0.5f;
        BeltInMask[] sides = { BeltInMask.Back, BeltInMask.Left, BeltInMask.Right };
        for (int s = 0; s < sides.Length; s++)
        {
            if (InMask != BeltInMask.None && !BeltRules.Has(InMask, sides[s]) && sides[s] != BeltInMask.Back)
                continue;
            if (InMask != BeltInMask.None && sides[s] == BeltInMask.Back && !FromBack && (FromLeft || FromRight))
                continue;

            Gizmos.color = sides[s] == BeltInMask.Back ? Color.cyan : Color.yellow;
            Vector3 prev = transform.TransformPoint(BeltRules.PathLocal(sides[s], 0f, half, itemHeight));
            for (int i = 1; i <= 8; i++)
            {
                Vector3 next = transform.TransformPoint(BeltRules.PathLocal(sides[s], i / 8f, half, itemHeight));
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.2f, BuildingLinker.CardinalToWorld(ExitDir) * 0.6f);
    }
#endif
}
