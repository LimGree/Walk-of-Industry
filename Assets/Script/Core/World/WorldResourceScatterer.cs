using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WorldResourceScatterer : MonoBehaviour
{
    enum MountainOre
    {
        Stone,
        Coal,
        Copper,
        Iron,
        Sulfur
    }

    [Header("Prefabs (Assets/prefabs/resourses)")]
    public GameObject sandPrefab;
    public GameObject stonePrefab;
    public GameObject coalPrefab;
    public GameObject copperPrefab;
    public GameObject ironPrefab;
    public GameObject sulfurPrefab;
    public GameObject treePrefab;

    [Header("Deposits")]
    [Tooltip("Россыпь с зазором: к каждой ноде можно подвести ленту.")]
    public int treeDeposits = 12;
    public int ironDeposits = 3;
    public int copperDeposits = 3;
    public int stoneDeposits = 3;
    public int coalDeposits = 3;
    public int sulfurDeposits = 3;
    public int sandDeposits = 3;
    public int nodesPerCluster = 24;
    public int clusterRadius = 16;
    public int nodeGap = 2;
    public int clusterGap = 14;
    [Range(1, 6)] public int minOresPerType = 2;
    [Range(20, 400)] public int maxCenterAttempts = 200;

    [Header("Streaming")]
    public float visualRadius = 38f;

    public static WorldResourceScatterer Instance { get; private set; }
    public IReadOnlyList<VeinMark> Veins => veins;
    public IReadOnlyList<Vector2Int> ClusterCenters => clusterCenters;
    public IReadOnlyList<string> ClusterKindKeys => clusterKindKeys;
    public bool IsScattered { get; private set; }
    public float ScatterProgress { get; private set; }

    Transform root;
    Transform player;
    readonly HashSet<Vector2Int> used = new HashSet<Vector2Int>();
    readonly List<Vector2Int> clusterCenters = new List<Vector2Int>();
    readonly List<int> clusterKinds = new List<int>();
    readonly List<string> clusterKindKeys = new List<string>();
    readonly List<int> clusterSizes = new List<int>();
    readonly List<VeinMark> veins = new List<VeinMark>(256);
    readonly Dictionary<Vector2Int, string> veinLookup = new Dictionary<Vector2Int, string>();
    readonly List<NodeVisual> visuals = new List<NodeVisual>(256);
    System.Random rng;
    string pendingKind;

    public struct VeinMark
    {
        public Vector2Int cell;
        public Color color;
        public string label;
    }

    struct NodeVisual
    {
        public Vector3 pos;
        public GameObject prefab;
        public GameObject stub;
        public GameObject visual;
        public float yaw;
        public bool shown;
    }

    Vector3 lastCullPos = new Vector3(99999f, 0f, 0f);
    float lastCullTime = -10f;

    void Awake()
    {
        Instance = this;
    }

    void OnEnable()
    {
        GameSettings.Changed += SyncVisualRadius;
        SyncVisualRadius();
    }

    void OnDisable()
    {
        GameSettings.Changed -= SyncVisualRadius;
    }

    void SyncVisualRadius()
    {
        visualRadius = GameSettings.RenderDistance;
        lastCullTime = -10f;
    }

    Coroutine scatterRoutine;
    List<KeptVein> pendingKeep;

    struct KeptVein
    {
        public Vector2Int cell;
        public ItemData resource;
        public GameObject prefab;
        public string kind;
    }

    void Start()
    {
        if (WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsReady)
            Scatter();
    }

    void OnDestroy()
    {
        GameSettings.Changed -= SyncVisualRadius;
        if (Instance == this)
            Instance = null;
    }

    void LateUpdate()
    {
        if (!WorldView.HasPlayer)
            return;
        Vector3 p = WorldView.PlayerPos;
        if ((p - lastCullPos).sqrMagnitude >= 9f || Time.unscaledTime - lastCullTime >= 0.2f)
        {
            lastCullPos = p;
            lastCullTime = Time.unscaledTime;
            UpdateVisibility();
        }
    }

    [ContextMenu("Scatter")]
    public void Scatter()
    {
        if (!isActiveAndEnabled)
            return;
        if (scatterRoutine != null)
            StopCoroutine(scatterRoutine);
        scatterRoutine = StartCoroutine(ScatterRoutine());
    }

    [ContextMenu("Scatter Keep Extractor Veins")]
    public void ScatterPreservingExtractorVeins()
    {
        pendingKeep = CollectExtractorVeins();
        Debug.Log("[Scatter] Keep " + pendingKeep.Count + " vein cells under extractors.");
        Scatter();
    }

    IEnumerator ScatterRoutine()
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady)
            yield break;

        IsScattered = false;
        ScatterProgress = 0f;
        ResolvePrefabs();
        ClearSpawned();
        used.Clear();
        clusterCenters.Clear();
        clusterKinds.Clear();
        clusterKindKeys.Clear();
        clusterSizes.Clear();
        veins.Clear();
        veinLookup.Clear();
        visuals.Clear();
        rng = new System.Random(map.seed * 31 + 9);
        root = new GameObject("WorldResources").transform;
        if (pendingKeep != null)
        {
            for (int i = 0; i < pendingKeep.Count; i++)
            {
                used.Add(pendingKeep[i].cell);
                clusterCenters.Add(pendingKeep[i].cell);
                clusterKinds.Add(-1);
            }
        }
        yield return null;

        yield return PlaceAllPatchesRoutine(map);
        RestoreKeptVeins();
        ValidateVeinInvariants(map);
        LogClusterStats();
        RebindExtractors();
        HideAllVisuals();
        ScatterProgress = 1f;
        IsScattered = true;
        scatterRoutine = null;
        pendingKeep = null;
        if (WorldMapUI.Instance != null)
            WorldMapUI.Instance.Rebuild();
    }

    void ClearSpawned()
    {
        if (root != null)
            Destroy(root.gameObject);
        GameObject existing = GameObject.Find("WorldResources");
        if (existing != null)
            Destroy(existing);
    }

    List<KeptVein> CollectExtractorVeins()
    {
        ResolvePrefabs();
        var kept = new Dictionary<Vector2Int, KeptVein>();
        Extractor[] extractors = Object.FindObjectsByType<Extractor>(FindObjectsSortMode.None);
        var seen = new HashSet<Vector2Int>();
        var queue = new Queue<Vector2Int>();

        for (int e = 0; e < extractors.Length; e++)
        {
            Extractor extractor = extractors[e];
            if (extractor == null)
                continue;
            Vector2Int start = BuildingLinker.WorldToCell(extractor.transform.position);
            ResourceNode startNode = ResourceNode.GetAt(start);
            if (startNode == null || startNode.resource == null)
                continue;

            ItemData res = startNode.resource;
            queue.Clear();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                if (!seen.Add(cell))
                    continue;
                ResourceNode node = ResourceNode.GetAt(cell);
                if (node == null || node.resource != res)
                    continue;

                string kind = KindOfItem(res);
                kept[cell] = new KeptVein
                {
                    cell = cell,
                    resource = res,
                    prefab = PrefabOfItem(res),
                    kind = kind
                };

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x == 0 && y == 0)
                            continue;
                        queue.Enqueue(cell + new Vector2Int(x, y));
                    }
                }
            }
        }

        return new List<KeptVein>(kept.Values);
    }

    void RestoreKeptVeins()
    {
        if (pendingKeep == null)
            return;
        for (int i = 0; i < pendingKeep.Count; i++)
        {
            KeptVein vein = pendingKeep[i];
            if (ResourceNode.HasNode(vein.cell))
                continue;
            pendingKind = vein.kind;
            if (vein.prefab != null)
                SpawnNode(vein.prefab, vein.cell);
        }
    }

    static void RebindExtractors()
    {
        Extractor[] extractors = Object.FindObjectsByType<Extractor>(FindObjectsSortMode.None);
        for (int i = 0; i < extractors.Length; i++)
        {
            if (extractors[i] != null)
                extractors[i].BindToNearbyNode();
        }
    }

    GameObject PrefabOfItem(ItemData item)
    {
        if (item == null)
            return stonePrefab;
        GameObject[] all =
        {
            sandPrefab, stonePrefab, coalPrefab, copperPrefab, ironPrefab, sulfurPrefab, treePrefab
        };
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && ResourceOf(all[i]) == item)
                return all[i];
        }

        string id = item.id != null ? item.id.ToLowerInvariant() : "";
        if (id.Contains("sand"))
            return sandPrefab;
        if (id.Contains("coal"))
            return coalPrefab;
        if (id.Contains("cooper") || id.Contains("copper"))
            return copperPrefab;
        if (id.Contains("iron"))
            return ironPrefab;
        if (id.Contains("sulfur"))
            return sulfurPrefab;
        if (id.Contains("wood") || id.Contains("tree") || id.Contains("log"))
            return treePrefab;
        return stonePrefab;
    }

    static string KindOfItem(ItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.id))
            return "stone";
        string id = item.id.ToLowerInvariant();
        if (id.Contains("sand"))
            return "sand";
        if (id.Contains("coal"))
            return "coal";
        if (id.Contains("cooper") || id.Contains("copper"))
            return "copper";
        if (id.Contains("iron"))
            return "iron";
        if (id.Contains("sulfur"))
            return "sulfur";
        if (id.Contains("wood") || id.Contains("tree") || id.Contains("log"))
            return "tree";
        return "stone";
    }

    enum PatchKind
    {
        Iron,
        Copper,
        Stone,
        Coal,
        Sulfur,
        Sand,
        Tree
    }

    struct PatchRequest
    {
        public PatchKind kind;
        public int minPick;
        public int kindId;
    }

    IEnumerator PlaceAllPatchesRoutine(WorldBiomeMap map)
    {
        var mountainPool = new List<Vector2Int>(256);
        var sandPool = new List<Vector2Int>(128);
        var treePool = new List<Vector2Int>(256);
        CollectSuitable(map, mountainPool, sandPool, treePool);

        var requests = BuildPatchRequests(sandPool.Count, treePool.Count);
        int done = 0;
        for (int i = 0; i < requests.Count; i++)
        {
            PatchRequest req = requests[i];
            List<Vector2Int> pool = PoolOf(req.kind, mountainPool, sandPool, treePool);
            TryFulfillRequest(req, pool);
            done++;
            ScatterProgress = Mathf.Lerp(0.02f, 0.88f, done / (float)Mathf.Max(1, requests.Count));
            if (i % 2 == 0)
                yield return null;
        }

        yield return CoverEmptyMountainsRoutine(map, mountainPool);
        ScatterProgress = 0.95f;
    }

    List<PatchRequest> BuildPatchRequests(int sandCells, int treeCells)
    {
        var list = new List<PatchRequest>(48);
        int ores = Mathf.Max(2, minOresPerType);
        MountainOre[] cycle =
        {
            MountainOre.Iron,
            MountainOre.Copper,
            MountainOre.Stone,
            MountainOre.Coal,
            MountainOre.Sulfur
        };
        for (int t = 0; t < cycle.Length; t++)
        {
            for (int n = 0; n < ores; n++)
                list.Add(MakeRequest(KindOfOre(cycle[t]), 10, (int)cycle[t]));
        }

        int sandWant = sandCells >= 8 ? Mathf.Clamp(sandDeposits, 2, 3) : 0;
        for (int i = 0; i < sandWant; i++)
            list.Add(MakeRequest(PatchKind.Sand, 8, -1));

        int treeWant = 0;
        if (treeCells > 0)
            treeWant = Mathf.Clamp(treeCells / 250, Mathf.Max(8, treeDeposits), 28);
        for (int i = 0; i < treeWant; i++)
            list.Add(MakeRequest(PatchKind.Tree, 10, -1));

        return list;
    }

    static PatchRequest MakeRequest(PatchKind kind, int minPick, int kindId)
    {
        return new PatchRequest { kind = kind, minPick = minPick, kindId = kindId };
    }

    static PatchKind KindOfOre(MountainOre ore)
    {
        switch (ore)
        {
            case MountainOre.Coal: return PatchKind.Coal;
            case MountainOre.Copper: return PatchKind.Copper;
            case MountainOre.Iron: return PatchKind.Iron;
            case MountainOre.Sulfur: return PatchKind.Sulfur;
            default: return PatchKind.Stone;
        }
    }

    static List<Vector2Int> PoolOf(
        PatchKind kind,
        List<Vector2Int> mountain,
        List<Vector2Int> sand,
        List<Vector2Int> tree)
    {
        if (kind == PatchKind.Sand)
            return sand;
        if (kind == PatchKind.Tree)
            return tree;
        return mountain;
    }

    void CollectSuitable(
        WorldBiomeMap map,
        List<Vector2Int> mountain,
        List<Vector2Int> sand,
        List<Vector2Int> tree)
    {
        int x0 = map.MapMinX;
        int z0 = map.MapMinZ;
        int w = map.MapWidth;
        int h = map.MapHeight;
        for (int z = 0; z < h; z++)
        {
            for (int x = 0; x < w; x++)
            {
                Vector2Int cell = new Vector2Int(x0 + x, z0 + z);
                WorldBiome biome = map.Get(cell);
                if (WorldBiomeMap.IsMountain(biome))
                    mountain.Add(cell);
                else if (biome == WorldBiome.Beach)
                    sand.Add(cell);
                else if (biome == WorldBiome.Forest || biome == WorldBiome.Woodland)
                    tree.Add(cell);
            }
        }
    }

    bool TryFulfillRequest(PatchRequest req, List<Vector2Int> pool)
    {
        GameObject prefab = PrefabOfKind(req.kind);
        if (prefab == null || pool == null || pool.Count == 0)
            return false;

        pendingKind = KindKey(req.kind);
        int gap = Mathf.Max(2, clusterGap);
        int minPick = Mathf.Max(1, req.minPick);
        int want = rng.Next(Mathf.Max(minPick, 14), Mathf.Max(minPick, nodesPerCluster) + 1);
        int attempts = Mathf.Max(20, maxCenterAttempts);

        for (int round = 0; round < 4; round++)
        {
            int tries = Mathf.Min(attempts, pool.Count);
            for (int t = 0; t < tries; t++)
            {
                Vector2Int center = pool[rng.Next(pool.Count)];
                if (!CanStartCluster(center, gap))
                    continue;
                if (CountSuitableNearby(center, req.kind, clusterRadius) < minPick * 2)
                    continue;
                if (CommitPatch(center, req.kind, prefab, want, minPick, req.kindId))
                    return true;
            }

            gap = Mathf.Max(4, gap / 2);
            if (round >= 1)
                minPick = Mathf.Max(8, minPick - 2);
        }

        return false;
    }

    bool CommitPatch(Vector2Int center, PatchKind kind, GameObject prefab, int want, int minPick, int kindId)
    {
        List<Vector2Int> pick = GrowPatch(center, kind, want, minPick);
        if (pick.Count < Mathf.Max(1, minPick))
            return false;

        for (int i = 0; i < pick.Count; i++)
            SpawnNode(prefab, pick[i]);

        clusterCenters.Add(center);
        clusterKinds.Add(kindId);
        clusterKindKeys.Add(KindKey(kind));
        clusterSizes.Add(pick.Count);
        return true;
    }

    List<Vector2Int> GrowPatch(Vector2Int center, PatchKind kind, int want, int minPick)
    {
        int radius = Mathf.Max(8, clusterRadius) + rng.Next(0, 6);
        int r2 = radius * radius;
        var candidates = new List<Vector2Int>(64);

        for (int dz = -radius; dz <= radius; dz++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dz * dz > r2)
                    continue;
                Vector2Int cell = new Vector2Int(center.x + dx, center.y + dz);
                if (!CellSuitable(cell, kind))
                    continue;
                if (used.Contains(cell) || ResourceNode.HasNode(cell))
                    continue;
                if (!FarFromUsed(cell, nodeGap))
                    continue;
                candidates.Add(cell);
            }
        }

        if (candidates.Count < Mathf.Max(1, minPick))
            return new List<Vector2Int>();

        var score = new float[candidates.Count];
        var order = new List<int>(candidates.Count);
        float jitter = radius * 0.28f;
        for (int i = 0; i < candidates.Count; i++)
        {
            int dx = candidates[i].x - center.x;
            int dz = candidates[i].y - center.y;
            score[i] = Mathf.Sqrt(dx * dx + dz * dz) + (float)rng.NextDouble() * jitter;
            order.Add(i);
        }
        order.Sort((a, b) => score[a].CompareTo(score[b]));

        int holeChance = kind == PatchKind.Tree ? 18 : 28;
        var pick = new List<Vector2Int>(want);
        var reserved = new HashSet<Vector2Int>();
        for (int n = 0; n < order.Count && pick.Count < want; n++)
        {
            Vector2Int cell = candidates[order[n]];
            if (pick.Count >= 4 && rng.Next(100) < holeChance)
                continue;
            if (reserved.Contains(cell) || !FarFromSet(cell, reserved, nodeGap))
                continue;
            if (!FarEuclidean(cell, pick, 2.05f))
                continue;
            pick.Add(cell);
            reserved.Add(cell);
        }

        if (pick.Count < Mathf.Max(1, minPick))
            return new List<Vector2Int>();
        return pick;
    }

    static bool FarEuclidean(Vector2Int cell, List<Vector2Int> picked, float minDist)
    {
        float need = minDist * minDist;
        for (int i = 0; i < picked.Count; i++)
        {
            int dx = cell.x - picked[i].x;
            int dz = cell.y - picked[i].y;
            if (dx * dx + dz * dz < need)
                return false;
        }
        return true;
    }

    static bool CellSuitable(Vector2Int cell, PatchKind kind)
    {
        if (WorldBiomeMap.Instance == null)
            return false;
        WorldBiome biome = WorldBiomeMap.Instance.Get(cell);
        switch (kind)
        {
            case PatchKind.Sand:
                return biome == WorldBiome.Beach;
            case PatchKind.Tree:
                return biome == WorldBiome.Forest || biome == WorldBiome.Woodland;
            default:
                return WorldBiomeMap.IsMountain(biome);
        }
    }

    IEnumerator CoverEmptyMountainsRoutine(WorldBiomeMap map, List<Vector2Int> mountainPool)
    {
        if (map == null || mountainPool == null || mountainPool.Count == 0)
            yield break;

        MountainOre[] cycle =
        {
            MountainOre.Iron,
            MountainOre.Copper,
            MountainOre.Stone,
            MountainOre.Coal,
            MountainOre.Sulfur
        };

        var mountainCells = new List<Vector2Int>(64);
        for (int id = 0; id < map.MountainCount; id++)
        {
            mountainCells.Clear();
            map.CollectMountainCells(id, mountainCells);
            if (mountainCells.Count < 8)
                continue;
            if (MountainHasNode(mountainCells))
                continue;

            MountainOre ore = cycle[id % cycle.Length];
            var req = MakeRequest(KindOfOre(ore), 8, (int)ore);
            TryFulfillRequest(req, mountainCells);
            yield return null;
        }
    }

    bool MountainHasNode(List<Vector2Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (used.Contains(cells[i]) || ResourceNode.HasNode(cells[i]) || veinLookup.ContainsKey(cells[i]))
                return true;
        }
        return false;
    }

    void ValidateVeinInvariants(WorldBiomeMap map)
    {
        int need = Mathf.Max(1, minOresPerType);
        WarnIfShort("iron", CountClusters((int)MountainOre.Iron), need);
        WarnIfShort("copper", CountClusters((int)MountainOre.Copper), need);
        WarnIfShort("stone", CountClusters((int)MountainOre.Stone), need);
        WarnIfShort("coal", CountClusters((int)MountainOre.Coal), need);
        WarnIfShort("sulfur", CountClusters((int)MountainOre.Sulfur), need);

        if (map == null)
            return;
        int empty = 0;
        var cells = new List<Vector2Int>(64);
        for (int id = 0; id < map.MountainCount; id++)
        {
            cells.Clear();
            map.CollectMountainCells(id, cells);
            if (cells.Count < 8)
                continue;
            if (!MountainHasNode(cells))
                empty++;
        }
        if (empty > 0)
            Debug.LogWarning("[Scatter] " + empty + " mountain(s) have no resource nodes.");
    }

    int CountClusters(int kindId)
    {
        int n = 0;
        for (int i = 0; i < clusterKinds.Count; i++)
        {
            if (clusterKinds[i] == kindId)
                n++;
        }
        return n;
    }

    static void WarnIfShort(string name, int have, int need)
    {
        if (have < need)
            Debug.LogWarning("[Scatter] " + name + " clusters " + have + " < guaranteed " + need + ".");
    }

    int CountSuitableNearby(Vector2Int center, PatchKind kind, int radius)
    {
        int r = Mathf.Max(4, radius);
        int r2 = r * r;
        int n = 0;
        for (int dz = -r; dz <= r; dz++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                if (dx * dx + dz * dz > r2)
                    continue;
                if (CellSuitable(new Vector2Int(center.x + dx, center.y + dz), kind))
                    n++;
            }
        }
        return n;
    }

    void LogClusterStats()
    {
        string[] keys = { "iron", "copper", "stone", "coal", "sulfur", "sand", "tree" };
        var sb = new System.Text.StringBuilder(256);
        sb.Append("[Scatter] clusters after world gen");
        int totalClusters = 0;
        int totalNodes = 0;
        for (int k = 0; k < keys.Length; k++)
        {
            int clusters = 0;
            int nodes = 0;
            for (int i = 0; i < clusterKindKeys.Count; i++)
            {
                if (clusterKindKeys[i] != keys[k])
                    continue;
                clusters++;
                nodes += clusterSizes[i];
            }
            totalClusters += clusters;
            totalNodes += nodes;
            float avg = clusters > 0 ? nodes / (float)clusters : 0f;
            sb.Append('\n');
            sb.Append("  ");
            sb.Append(LabelOfKind(keys[k]));
            sb.Append(": ");
            sb.Append(clusters);
            sb.Append(" скопл., жил в среднем ");
            sb.Append(avg.ToString("0.0"));
            sb.Append(" (всего ");
            sb.Append(nodes);
            sb.Append(')');
        }
        sb.Append('\n');
        sb.Append("  итого: ");
        sb.Append(totalClusters);
        sb.Append(" скоплений, ");
        sb.Append(totalNodes);
        sb.Append(" жил");
        Debug.Log(sb.ToString());
    }

    static string KindKey(PatchKind kind)
    {
        switch (kind)
        {
            case PatchKind.Sand: return "sand";
            case PatchKind.Tree: return "tree";
            case PatchKind.Coal: return "coal";
            case PatchKind.Copper: return "copper";
            case PatchKind.Iron: return "iron";
            case PatchKind.Sulfur: return "sulfur";
            default: return "stone";
        }
    }

    GameObject PrefabOfKind(PatchKind kind)
    {
        switch (kind)
        {
            case PatchKind.Sand: return sandPrefab;
            case PatchKind.Tree: return treePrefab;
            case PatchKind.Coal: return coalPrefab;
            case PatchKind.Copper: return copperPrefab;
            case PatchKind.Iron: return ironPrefab;
            case PatchKind.Sulfur: return sulfurPrefab;
            default: return stonePrefab;
        }
    }

    bool CanStartCluster(Vector2Int cell, int gap)
    {
        if (used.Contains(cell) || ResourceNode.HasNode(cell))
            return false;
        for (int i = 0; i < clusterCenters.Count; i++)
        {
            if (Chebyshev(clusterCenters[i], cell) < gap)
                return false;
        }
        return true;
    }

    static bool FarFromSet(Vector2Int cell, HashSet<Vector2Int> set, int gap)
    {
        int g = Mathf.Max(1, gap);
        for (int z = -g + 1; z < g; z++)
        {
            for (int x = -g + 1; x < g; x++)
            {
                if (x == 0 && z == 0)
                    continue;
                if (set.Contains(cell + new Vector2Int(x, z)))
                    return false;
            }
        }
        return true;
    }

    bool FarFromUsed(Vector2Int cell, int gap)
    {
        int g = Mathf.Max(1, gap);
        for (int z = -g + 1; z < g; z++)
        {
            for (int x = -g + 1; x < g; x++)
            {
                if (x == 0 && z == 0)
                    continue;
                if (used.Contains(cell + new Vector2Int(x, z)))
                    return false;
            }
        }
        return true;
    }

    public bool DevSpawnVein(string kind, Vector2Int cell)
    {
        if (used.Contains(cell) || ResourceNode.HasNode(cell))
            return false;
        if (rng == null)
            rng = new System.Random(1);
        if (root == null)
        {
            GameObject go = new GameObject("ResourceNodes");
            root = go.transform;
        }
        pendingKind = kind;
        GameObject prefab = PrefabOfKindName(kind);
        return SpawnNode(prefab, cell);
    }

    GameObject PrefabOfKindName(string kind)
    {
        switch ((kind ?? "").ToLowerInvariant())
        {
            case "sand": return sandPrefab;
            case "tree":
            case "log": return treePrefab;
            case "coal": return coalPrefab;
            case "copper":
            case "cooper": return copperPrefab;
            case "iron": return ironPrefab;
            case "sulfur": return sulfurPrefab;
            default: return stonePrefab;
        }
    }

    bool SpawnNode(GameObject prefab, Vector2Int cell)
    {
        if (prefab == null)
            return false;

        Vector3 pos = WorldBiomeMap.Instance.CellWorld(cell);
        float yaw = rng.Next(0, 4) * 90f;
        GameObject stub = new GameObject(prefab.name + "_" + cell.x + "_" + cell.y);
        stub.transform.SetParent(root, false);
        stub.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        ResourceNode node = stub.AddComponent<ResourceNode>();
        node.resource = ResourceOf(prefab);
        node.RegisterCell();

        visuals.Add(new NodeVisual
        {
            pos = pos,
            prefab = prefab,
            stub = stub,
            visual = null,
            yaw = yaw,
            shown = false
        });
        veins.Add(new VeinMark
        {
            cell = cell,
            color = ColorOfKind(pendingKind),
            label = LabelOfKind(pendingKind)
        });
        veinLookup[cell] = LabelOfKind(pendingKind);

        used.Add(cell);
        return true;
    }

    static readonly Dictionary<int, ItemData> prefabResource = new Dictionary<int, ItemData>();

    static ItemData ResourceOf(GameObject prefab)
    {
        if (prefab == null)
            return null;
        int id = prefab.GetInstanceID();
        if (prefabResource.TryGetValue(id, out ItemData cached))
            return cached;
        ResourceNode node = prefab.GetComponent<ResourceNode>();
        if (node == null)
            node = prefab.GetComponentInChildren<ResourceNode>(true);
        ItemData resource = node != null ? node.resource : null;
        prefabResource[id] = resource;
        return resource;
    }

    public bool TryGetVeinLabel(Vector2Int cell, out string label)
    {
        return veinLookup.TryGetValue(cell, out label);
    }

    static string LabelOfKind(string kind)
    {
        switch (kind)
        {
            case "sand": return "Песок";
            case "tree": return "Дерево";
            case "coal": return "Уголь";
            case "copper": return "Медь";
            case "iron": return "Железо";
            case "sulfur": return "Сера";
            default: return "Камень";
        }
    }

    static Color ColorOfKind(string kind)
    {
        switch (kind)
        {
            case "sand": return new Color(0.95f, 0.86f, 0.32f);
            case "tree": return new Color(0.07f, 0.22f, 0.08f);
            case "coal": return new Color(0.07f, 0.07f, 0.08f);
            case "copper": return new Color(0.86f, 0.46f, 0.16f);
            case "iron": return new Color(0.72f, 0.24f, 0.18f);
            case "sulfur": return new Color(0.92f, 0.82f, 0.18f);
            default: return new Color(0.78f, 0.78f, 0.80f);
        }
    }

    void HideAllVisuals()
    {
        for (int i = 0; i < visuals.Count; i++)
            SetShown(i, false);
    }

    void UpdateVisibility()
    {
        if (visuals.Count == 0)
            return;

        Vector3 p = WorldView.PlayerPos;
        float r2 = visualRadius * visualRadius;
        int budget = 6;
        for (int i = 0; i < visuals.Count; i++)
        {
            Vector3 d = visuals[i].pos - p;
            d.y = 0f;
            bool want = d.sqrMagnitude <= r2;
            if (want && visuals[i].visual == null)
            {
                if (budget <= 0)
                    continue;
                budget--;
            }
            SetShown(i, want);
        }
    }

    void SetShown(int index, bool show)
    {
        NodeVisual vis = visuals[index];
        if (show)
        {
            if (vis.visual == null)
                AttachVisual(ref vis);
            vis.shown = vis.visual != null;
        }
        else
        {
            if (vis.visual != null)
            {
                Destroy(vis.visual);
                vis.visual = null;
            }
            vis.shown = false;
        }
        visuals[index] = vis;
    }

    static void AttachVisual(ref NodeVisual vis)
    {
        if (vis.stub == null || vis.prefab == null)
            return;
        GameObject visual = Object.Instantiate(vis.prefab, vis.stub.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        ResourceNode extra = visual.GetComponent<ResourceNode>();
        if (extra == null)
            extra = visual.GetComponentInChildren<ResourceNode>(true);
        if (extra != null)
            Object.Destroy(extra);
        ResourceNode stubNode = vis.stub.GetComponent<ResourceNode>();
        if (stubNode != null)
            stubNode.RegisterCell();
        vis.visual = visual;
    }

    static int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    void Shuffle(List<Vector2Int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2Int t = list[i];
            list[i] = list[j];
            list[j] = t;
        }
    }

    void ResolvePrefabs()
    {
        if (sandPrefab == null) sandPrefab = LoadPrefab("sand");
        if (stonePrefab == null) stonePrefab = LoadPrefab("stone");
        if (coalPrefab == null) coalPrefab = LoadPrefab("stone_coal_ore");
        if (copperPrefab == null) copperPrefab = LoadPrefab("stone_cooper_ore");
        if (ironPrefab == null) ironPrefab = LoadPrefab("stone_iron_ore");
        if (sulfurPrefab == null) sulfurPrefab = LoadPrefab("sulfur");
        if (treePrefab == null) treePrefab = LoadPrefab("tree");

        if (sandPrefab == null && stonePrefab == null && treePrefab == null)
            Debug.LogError("WorldResourceScatterer: no resource prefabs in the player. Add them to Resources/ResourceNodePrefabs.");
    }

    static GameObject LoadPrefab(string name)
    {
        ResourceNodePrefabs catalog = Resources.Load<ResourceNodePrefabs>("ResourceNodePrefabs");
        if (catalog != null)
        {
            GameObject fromCatalog = catalog.Get(name);
            if (fromCatalog != null)
                return fromCatalog;
        }

        GameObject fromResources = Resources.Load<GameObject>("resourses/" + name);
        if (fromResources != null)
            return fromResources;

#if UNITY_EDITOR
        string[] guids = AssetDatabase.FindAssets(name + " t:Prefab", new[] { "Assets/prefabs/resourses" });
        for (int i = 0; i < (guids != null ? guids.Length : 0); i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(path))
                continue;
            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            if (file == name)
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        if (guids != null && guids.Length > 0)
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
#endif
        Debug.LogWarning("WorldResourceScatterer: missing prefab " + name);
        return null;
    }
}

