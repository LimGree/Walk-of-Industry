using System.Collections.Generic;
using UnityEngine;

public enum WorldBiome
{
    Field,
    Woodland,
    Forest,
    Beach,
    Lake,
    Ocean,
    MountainPeak,
    MountainSlope
}

/// <summary>
/// Клеточная карта биомов + покраска terrain. Вода (озеро/океан) — нельзя строить.
/// </summary>
public class WorldBiomeMap : MonoBehaviour
{
    public static WorldBiomeMap Instance { get; private set; }

    [Header("Source")]
    public Terrain terrain;
    public int seed = 17;

    [Header("Layout")]
    [Range(2, 40)] public int oceanWidth = 6;
    [Range(1, 8)] public int beachWidth = 2;
    [Range(0.55f, 0.9f)] public float lakeThreshold = 0.74f;
    [Range(0.4f, 0.8f)] public float forestThreshold = 0.58f;
    [Range(0.15f, 0.55f)] public float woodlandThreshold = 0.34f;
    [Range(0.55f, 0.9f)] public float mountainPeakThreshold = 0.60f;
    [Range(1, 10)] public int mountainSlopeWidth = 4;

    [Header("Land growth")]
    [Range(4, 60)] public int mountainMinSpacing = 42;
    [Range(0.35f, 0.75f)] public float mountainGrowThreshold = 0.56f;
    [Range(8, 120)] public int mountainMinCells = 24;
    [Range(20, 600)] public int mountainMaxCellsPerSeed = 460;
    [Range(3, 24)] public int mountainGrowRadius = 16;
    [Range(1, 120)] public int maxMountainSeeds = 90;
    [Range(6, 40)] public int lakeMinSpacing = 16;
    [Range(0.4f, 0.8f)] public float lakeGrowThreshold = 0.68f;
    [Range(8, 80)] public int lakeMinCells = 20;
    [Range(1, 20)] public int maxLakeSeeds = 8;
    [Range(0f, 0.2f)] public float lakeWinMargin = 0.08f;
    [Range(4, 24)] public int lakeMountainSeparation = 4;
    [Range(40, 8000)] public int minMountainCapableArea = 4000;
    [Range(1000, 80000)] public int maxMountainCells = 80000;
    [Range(1, 12)] public int landRetryLimit = 8;

    [Header("Colors")]
    public Color forest = new Color(0.13f, 0.30f, 0.16f);
    public Color woodland = new Color(0.30f, 0.50f, 0.24f);
    public Color field = new Color(0.58f, 0.74f, 0.34f);
    public Color beach = new Color(0.86f, 0.74f, 0.42f);
    public Color lake = new Color(0.30f, 0.62f, 0.76f);
    public Color ocean = new Color(0.11f, 0.32f, 0.52f);
    public Color mountainPeak = new Color(0.28f, 0.28f, 0.30f);
    public Color mountainSlope = new Color(0.58f, 0.58f, 0.60f);

    WorldBiome[] cells;
    int[] mountainIds;
    int mountainCount;
    int minX;
    int minZ;
    int width;
    int height;
    bool ready;

    Transform overlayRoot;
    Material overlayMat;
    Texture2D biomeTexture;
    Texture2D overlayTexture;

    const float HorizonPadMeters = 280f;

    public bool IsReady => ready;
    public int MapMinX => minX;
    public int MapMinZ => minZ;
    public int MapWidth => width;
    public int MapHeight => height;
    public Texture2D BiomeTexture => biomeTexture;

    public Vector3 PlayableCenterWorld
    {
        get
        {
            if (terrain == null || terrain.terrainData == null)
                return Vector3.zero;
            Vector3 pos = terrain.GetPosition();
            Vector3 size = terrain.terrainData.size;
            return new Vector3(pos.x + size.x * 0.5f, 0f, pos.z + size.z * 0.5f);
        }
    }

    void Awake()
    {
        Instance = this;
        if (terrain == null)
            terrain = FindFirstObjectByType<Terrain>();
        if (WorldCatalog.HasActive)
            seed = WorldCatalog.Active.seed;
        Generate();
    }

    void OnEnable()
    {
        GameSettings.Changed += RefreshOverlayGfx;
        RefreshOverlayGfx();
    }

    void OnDisable()
    {
        GameSettings.Changed -= RefreshOverlayGfx;
    }

    void OnDestroy()
    {
        GameSettings.Changed -= RefreshOverlayGfx;
        if (biomeTexture != null)
            Destroy(biomeTexture);
        if (overlayTexture != null)
            Destroy(overlayTexture);
        if (Instance == this)
            Instance = null;
    }

    [ContextMenu("Regenerate")]
    public void Generate()
    {
        if (terrain == null)
            terrain = FindFirstObjectByType<Terrain>();
        if (terrain == null || terrain.terrainData == null)
        {
            ready = false;
            return;
        }

        ResolveBounds();
        BuildMap();
        ready = true;

        try
        {
            PaintOverlay();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Biome overlay skipped: " + e.Message);
        }
    }

    void ResolveBounds()
    {
        Vector3 pos = terrain.GetPosition();
        Vector3 size = terrain.terrainData.size;
        Vector2Int a = WorldToCell(pos + new Vector3(0.51f, 0f, 0.51f));
        Vector2Int b = WorldToCell(pos + new Vector3(size.x - 0.51f, 0f, size.z - 0.51f));
        minX = Mathf.Min(a.x, b.x);
        minZ = Mathf.Min(a.y, b.y);
        width = Mathf.Abs(b.x - a.x) + 1;
        height = Mathf.Abs(b.y - a.y) + 1;
        width = Mathf.Max(8, width);
        height = Mathf.Max(8, height);
    }

    static Vector2Int WorldToCell(Vector3 world)
    {
        if (GridSystem.Instance != null)
            return GridSystem.Instance.WorldToCell(world);
        return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
    }

    void BuildMap()
    {
        cells = new WorldBiome[width * height];
        float oceanOff = seed * 17.13f;
        PaintOcean(oceanOff);

        int salt = 0;
        int retries = Mathf.Max(1, landRetryLimit);
        WorldBiome[] frozenCore = null;
        for (int attempt = 0; attempt < retries; attempt++)
        {
            float landOff = oceanOff + salt * 97.13f;
            PaintLand(landOff);
            frozenCore = SnapshotCore();
            ApplySlopes();
            if (TrimExcessMountainArea())
                ApplySlopes();
            if (CountMountainCells() >= Mathf.Max(1, minMountainCapableArea))
                break;
            salt++;
        }

        ApplyBeaches();
        ApplyVegetation(oceanOff);
        BuildMountainIds();
        ValidateLandInvariants(frozenCore);
        int mountainCells = CountMountainCells();
        float avg = mountainCount > 0 ? mountainCells / (float)mountainCount : 0f;
        Debug.Log("[WorldBiomeMap] mountains: " + mountainCount + " massifs, "
            + mountainCells + " cells (peak+slope), avg " + avg.ToString("0") + " per massif.");
    }

    void PaintOcean(float seedOff)
    {
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int edge = Mathf.Min(x, z, width - 1 - x, height - 1 - z);
                float wobble = (Mathf.PerlinNoise(x * 0.08f + seedOff, z * 0.08f) - 0.5f) * 4f;
                Set(x, z, edge + wobble < oceanWidth ? WorldBiome.Ocean : WorldBiome.Field);
            }
        }
    }

    void PaintLand(float seedOff)
    {
        int n = width * height;
        var mRaw = new float[n];
        var lRaw = new float[n];
        var mScore = new float[n];
        var lScore = new float[n];

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = z * width + x;
                if (GetLocal(x, z) == WorldBiome.Ocean)
                    continue;
                Set(x, z, WorldBiome.Field);
                mRaw[i] = MountainNoise(x, z, seedOff);
                lRaw[i] = LakeNoise(x, z, seedOff);
                mScore[i] = mRaw[i] - mountainPeakThreshold;
                lScore[i] = lRaw[i] - lakeThreshold;
            }
        }

        List<Vector2Int> mountainSeeds = PickSeeds(
            mScore, true, mScore, lScore, mountainMinSpacing, maxMountainSeeds);
        List<Vector2Int> lakeSeeds = PickSeeds(
            lScore, false, mScore, lScore, lakeMinSpacing, maxLakeSeeds);
        SeparateLakeSeeds(mountainSeeds, lakeSeeds);
        GrowPeaksAndLakes(mountainSeeds, lakeSeeds, mRaw, lRaw, mScore, lScore);
        DropTinyRegions(WorldBiome.MountainPeak, mountainMinCells);
        DropTinyRegions(WorldBiome.Lake, lakeMinCells);
    }

    bool MountainOwns(int i, float[] mScore, float[] lScore)
    {
        return mScore[i] > 0f && mScore[i] >= lScore[i];
    }

    bool LakeOwns(int i, float[] mScore, float[] lScore)
    {
        return lScore[i] > 0f && lScore[i] > mScore[i] + lakeWinMargin;
    }

    bool MountainCanGrow(int i, float[] mRaw, float[] mScore, float[] lScore)
    {
        return mRaw[i] >= mountainGrowThreshold && mScore[i] >= lScore[i];
    }

    bool LakeCanGrow(int i, float[] lRaw, float[] mScore, float[] lScore)
    {
        return lRaw[i] >= lakeGrowThreshold && lScore[i] > mScore[i] + lakeWinMargin;
    }

    List<Vector2Int> PickSeeds(
        float[] scores,
        bool mountains,
        float[] mScore,
        float[] lScore,
        int spacing,
        int maxSeeds)
    {
        var ranked = new List<SeedRank>(256);
        var fill = mountains ? new List<SeedRank>(512) : null;
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (GetLocal(x, z) == WorldBiome.Ocean)
                    continue;
                int i = z * width + x;
                if (mountains)
                {
                    if (mScore[i] < lScore[i])
                        continue;
                    if (scores[i] + mountainPeakThreshold < mountainGrowThreshold)
                        continue;
                }
                else
                {
                    if (scores[i] <= 0f || !LakeOwns(i, mScore, lScore))
                        continue;
                }

                var rank = new SeedRank { cell = new Vector2Int(x, z), score = scores[i] };
                if (IsLocalMax(scores, x, z))
                    ranked.Add(rank);
                else if (fill != null)
                    fill.Add(rank);
            }
        }

        ranked.Sort((a, b) => b.score.CompareTo(a.score));
        if (fill != null)
            fill.Sort((a, b) => b.score.CompareTo(a.score));

        int gap = Mathf.Max(1, spacing);
        int cap = Mathf.Max(1, maxSeeds);
        var picked = new List<Vector2Int>(cap);
        AppendSeeds(ranked, picked, gap, cap);
        if (fill != null && picked.Count < cap)
            AppendSeeds(fill, picked, gap, cap);
        return picked;
    }

    static void AppendSeeds(List<SeedRank> ranked, List<Vector2Int> picked, int gap, int cap)
    {
        for (int i = 0; i < ranked.Count && picked.Count < cap; i++)
        {
            Vector2Int cell = ranked[i].cell;
            bool far = true;
            for (int p = 0; p < picked.Count; p++)
            {
                if (Chebyshev(picked[p], cell) < gap)
                {
                    far = false;
                    break;
                }
            }
            if (!far)
                continue;
            picked.Add(cell);
        }
    }

    void SeparateLakeSeeds(List<Vector2Int> mountainSeeds, List<Vector2Int> lakeSeeds)
    {
        int sep = Mathf.Max(1, lakeMountainSeparation);
        for (int i = lakeSeeds.Count - 1; i >= 0; i--)
        {
            Vector2Int lake = lakeSeeds[i];
            for (int m = 0; m < mountainSeeds.Count; m++)
            {
                if (Chebyshev(lake, mountainSeeds[m]) < sep)
                {
                    lakeSeeds.RemoveAt(i);
                    break;
                }
            }
        }
    }

    bool IsLocalMax(float[] scores, int x, int z)
    {
        float s = scores[z * width + x];
        for (int dz = -1; dz <= 1; dz++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dz == 0)
                    continue;
                int nx = x + dx;
                int nz = z + dz;
                if (nx < 0 || nz < 0 || nx >= width || nz >= height)
                    continue;
                if (GetLocal(nx, nz) == WorldBiome.Ocean)
                    continue;
                if (scores[nz * width + nx] > s)
                    return false;
            }
        }
        return true;
    }

    struct PeakFront
    {
        public Vector2Int cell;
        public int seed;
    }

    void GrowPeaksAndLakes(
        List<Vector2Int> mountainSeeds,
        List<Vector2Int> lakeSeeds,
        float[] mRaw,
        float[] lRaw,
        float[] mScore,
        float[] lScore)
    {
        var mountainFrontier = new Queue<PeakFront>();
        int[] seedSize = new int[Mathf.Max(1, mountainSeeds.Count)];
        int maxPer = Mathf.Max(mountainMinCells, mountainMaxCellsPerSeed);
        int maxR = Mathf.Max(2, mountainGrowRadius);

        for (int i = 0; i < mountainSeeds.Count; i++)
        {
            Vector2Int s = mountainSeeds[i];
            Set(s.x, s.y, WorldBiome.MountainPeak);
            seedSize[i] = 1;
            mountainFrontier.Enqueue(new PeakFront { cell = s, seed = i });
        }

        var lakeFrontier = new HashSet<Vector2Int>();
        for (int i = 0; i < lakeSeeds.Count; i++)
        {
            Vector2Int s = lakeSeeds[i];
            if (GetLocal(s.x, s.y) != WorldBiome.Field)
                continue;
            Set(s.x, s.y, WorldBiome.Lake);
            lakeFrontier.Add(s);
        }

        var proposedL = new HashSet<Vector2Int>();
        var nextL = new HashSet<Vector2Int>();

        while (mountainFrontier.Count > 0 || lakeFrontier.Count > 0)
        {
            int wave = mountainFrontier.Count;
            for (int n = 0; n < wave; n++)
            {
                PeakFront front = mountainFrontier.Dequeue();
                if (seedSize[front.seed] >= maxPer)
                    continue;
                Vector2Int origin = mountainSeeds[front.seed];
                for (int dir = 0; dir < 8; dir++)
                {
                    TryExpandPeak(
                        front.cell.x + DirX[dir],
                        front.cell.y + DirZ[dir],
                        front.seed,
                        origin,
                        maxR,
                        maxPer,
                        seedSize,
                        mRaw,
                        mScore,
                        lScore,
                        mountainFrontier);
                }
            }

            proposedL.Clear();
            foreach (Vector2Int cell in lakeFrontier)
            {
                TryProposeLake(cell.x + 1, cell.y, lRaw, mScore, lScore, proposedL);
                TryProposeLake(cell.x - 1, cell.y, lRaw, mScore, lScore, proposedL);
                TryProposeLake(cell.x, cell.y + 1, lRaw, mScore, lScore, proposedL);
                TryProposeLake(cell.x, cell.y - 1, lRaw, mScore, lScore, proposedL);
            }

            nextL.Clear();
            foreach (Vector2Int cell in proposedL)
            {
                if (GetLocal(cell.x, cell.y) != WorldBiome.Field)
                    continue;
                Set(cell.x, cell.y, WorldBiome.Lake);
                nextL.Add(cell);
            }
            lakeFrontier = new HashSet<Vector2Int>(nextL);
        }
    }

    static readonly int[] DirX = { 1, -1, 0, 0, 1, 1, -1, -1 };
    static readonly int[] DirZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

    bool InsidePeakShape(Vector2Int origin, Vector2Int cell, int seedId, int maxR)
    {
        float dx = cell.x - origin.x;
        float dz = cell.y - origin.y;
        float off = seed * 17.13f + seedId * 3.17f;
        float wx = dx + (Mathf.PerlinNoise(cell.x * 0.07f + off, cell.y * 0.07f) - 0.5f) * 9f;
        float wz = dz + (Mathf.PerlinNoise(cell.x * 0.07f + off + 8.4f, cell.y * 0.07f) - 0.5f) * 9f;
        float dist = Mathf.Sqrt(wx * wx + wz * wz);
        float ang = Mathf.Atan2(wz, wx);
        float n1 = Mathf.PerlinNoise(Mathf.Cos(ang) * 0.85f + off, Mathf.Sin(ang) * 0.85f);
        float n2 = Mathf.PerlinNoise(Mathf.Cos(ang * 2.2f) * 1.6f + off + 5.1f, Mathf.Sin(ang * 2.2f) * 1.6f);
        float radius = maxR * (0.58f + 0.32f * n1 + 0.28f * n2);
        return dist <= radius;
    }

    void TryExpandPeak(
        int x,
        int z,
        int seedId,
        Vector2Int origin,
        int maxR,
        int maxPer,
        int[] seedSize,
        float[] mRaw,
        float[] mScore,
        float[] lScore,
        Queue<PeakFront> frontier)
    {
        if (seedSize[seedId] >= maxPer)
            return;
        if (x < 0 || z < 0 || x >= width || z >= height)
            return;
        Vector2Int cell = new Vector2Int(x, z);
        if (!InsidePeakShape(origin, cell, seedId, maxR))
            return;
        if (GetLocal(x, z) != WorldBiome.Field)
            return;
        int i = z * width + x;
        if (!MountainCanGrow(i, mRaw, mScore, lScore))
            return;
        Set(x, z, WorldBiome.MountainPeak);
        seedSize[seedId]++;
        frontier.Enqueue(new PeakFront { cell = cell, seed = seedId });
    }

    void TryProposeLake(
        int x,
        int z,
        float[] lRaw,
        float[] mScore,
        float[] lScore,
        HashSet<Vector2Int> proposed)
    {
        if (x < 0 || z < 0 || x >= width || z >= height)
            return;
        if (GetLocal(x, z) != WorldBiome.Field)
            return;
        int i = z * width + x;
        if (!LakeCanGrow(i, lRaw, mScore, lScore))
            return;
        proposed.Add(new Vector2Int(x, z));
    }

    void DropTinyRegions(WorldBiome biome, int minCells)
    {
        int need = Mathf.Max(1, minCells);
        var seen = new bool[cells.Length];
        var stack = new Stack<int>();
        var region = new List<int>(64);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int start = z * width + x;
                if (seen[start] || GetLocal(x, z) != biome)
                    continue;

                region.Clear();
                stack.Push(start);
                seen[start] = true;
                while (stack.Count > 0)
                {
                    int cur = stack.Pop();
                    region.Add(cur);
                    int cx = cur % width;
                    int cz = cur / width;
                    TryPushRegion(stack, seen, biome, cx + 1, cz);
                    TryPushRegion(stack, seen, biome, cx - 1, cz);
                    TryPushRegion(stack, seen, biome, cx, cz + 1);
                    TryPushRegion(stack, seen, biome, cx, cz - 1);
                }

                if (region.Count >= need)
                    continue;
                for (int i = 0; i < region.Count; i++)
                    cells[region[i]] = WorldBiome.Field;
            }
        }
    }

    void TryPushRegion(Stack<int> stack, bool[] seen, WorldBiome biome, int x, int z)
    {
        if (x < 0 || z < 0 || x >= width || z >= height)
            return;
        int i = z * width + x;
        if (seen[i] || GetLocal(x, z) != biome)
            return;
        seen[i] = true;
        stack.Push(i);
    }

    int CountMountainCells()
    {
        int n = 0;
        for (int i = 0; i < cells.Length; i++)
        {
            if (IsMountain(cells[i]))
                n++;
        }
        return n;
    }

    bool TrimExcessMountainArea()
    {
        int cap = Mathf.Max(minMountainCapableArea, maxMountainCells);
        if (CountMountainCells() <= cap)
            return false;

        var regions = new List<List<int>>();
        var seen = new bool[cells.Length];
        var stack = new Stack<int>();
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int start = z * width + x;
                if (seen[start] || cells[start] != WorldBiome.MountainPeak)
                    continue;
                var region = new List<int>(64);
                stack.Push(start);
                seen[start] = true;
                while (stack.Count > 0)
                {
                    int cur = stack.Pop();
                    region.Add(cur);
                    int cx = cur % width;
                    int cz = cur / width;
                    TryPushRegion(stack, seen, WorldBiome.MountainPeak, cx + 1, cz);
                    TryPushRegion(stack, seen, WorldBiome.MountainPeak, cx - 1, cz);
                    TryPushRegion(stack, seen, WorldBiome.MountainPeak, cx, cz + 1);
                    TryPushRegion(stack, seen, WorldBiome.MountainPeak, cx, cz - 1);
                }
                regions.Add(region);
            }
        }

        regions.Sort((a, b) => b.Count.CompareTo(a.Count));
        int kept = 0;
        for (int r = 0; r < regions.Count; r++)
        {
            if (kept + regions[r].Count <= cap)
            {
                kept += regions[r].Count;
                continue;
            }
            for (int i = 0; i < regions[r].Count; i++)
                cells[regions[r][i]] = WorldBiome.Field;
        }

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == WorldBiome.MountainSlope)
                cells[i] = WorldBiome.Field;
        }
        return true;
    }

    void ApplyVegetation(float seedOff)
    {
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (GetLocal(x, z) != WorldBiome.Field)
                    continue;
                float veg = VegNoise(x, z, seedOff);
                if (veg > forestThreshold)
                    Set(x, z, WorldBiome.Forest);
                else if (veg > woodlandThreshold)
                    Set(x, z, WorldBiome.Woodland);
            }
        }
    }

    WorldBiome[] SnapshotCore()
    {
        var copy = new WorldBiome[cells.Length];
        System.Array.Copy(cells, copy, cells.Length);
        return copy;
    }

    static bool IsFrozenCore(WorldBiome biome)
    {
        return biome == WorldBiome.Ocean
            || biome == WorldBiome.Lake
            || biome == WorldBiome.MountainPeak;
    }

    void ValidateLandInvariants(WorldBiome[] frozenCore)
    {
        if (frozenCore != null && frozenCore.Length == cells.Length)
        {
            int rewritten = 0;
            for (int i = 0; i < cells.Length; i++)
            {
                if (IsFrozenCore(frozenCore[i]) && cells[i] != frozenCore[i])
                    rewritten++;
            }
            if (rewritten > 0)
                Debug.LogWarning("[WorldBiomeMap] " + rewritten + " frozen ocean/lake/peak cell(s) were rewritten.");
        }

        if (CountMountainCells() < minMountainCapableArea)
            Debug.LogWarning("[WorldBiomeMap] Mountain area below minMountainCapableArea after retries.");
    }

    struct SeedRank
    {
        public Vector2Int cell;
        public float score;
    }

    static int Chebyshev(Vector2Int a, Vector2Int b)
    {
        return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
    }

    void ApplyBeaches()
    {
        int ring = Mathf.Max(1, beachWidth);
        var next = new WorldBiome[cells.Length];
        System.Array.Copy(cells, next, cells.Length);

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                WorldBiome current = GetLocal(x, z);
                if (current == WorldBiome.Lake
                    || current == WorldBiome.Ocean
                    || current == WorldBiome.MountainPeak
                    || current == WorldBiome.MountainSlope)
                    continue;
                if (NearLake(x, z, ring))
                    next[z * width + x] = WorldBiome.Beach;
            }
        }

        cells = next;
    }

    void ApplySlopes()
    {
        int ring = Mathf.Max(1, mountainSlopeWidth);
        var next = new WorldBiome[cells.Length];
        System.Array.Copy(cells, next, cells.Length);

        var queue = new Queue<int>();
        var dist = new int[cells.Length];
        for (int i = 0; i < dist.Length; i++)
            dist[i] = -1;

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (GetLocal(x, z) != WorldBiome.MountainPeak)
                    continue;
                int i = z * width + x;
                dist[i] = 0;
                queue.Enqueue(i);
            }
        }

        float off = seed * 17.13f + 2.7f;
        while (queue.Count > 0)
        {
            int cur = queue.Dequeue();
            int cx = cur % width;
            int cz = cur / width;
            int cd = dist[cur];
            for (int dir = 0; dir < 8; dir++)
            {
                int x = cx + DirX[dir];
                int z = cz + DirZ[dir];
                if (x < 0 || z < 0 || x >= width || z >= height)
                    continue;
                int i = z * width + x;
                if (dist[i] >= 0)
                    continue;
                WorldBiome biome = GetLocal(x, z);
                if (biome == WorldBiome.Ocean || biome == WorldBiome.Lake || biome == WorldBiome.MountainPeak)
                    continue;

                int nd = cd + 1;
                float n1 = Mathf.PerlinNoise(x * 0.11f + off, z * 0.11f);
                float n2 = Mathf.PerlinNoise(x * 0.04f + off + 6.2f, z * 0.04f);
                float allow = ring * (0.45f + 0.7f * n1 + 0.5f * n2);
                if (nd > allow + 0.51f)
                    continue;

                dist[i] = nd;
                next[i] = WorldBiome.MountainSlope;
                queue.Enqueue(i);
            }
        }

        cells = next;
    }

    void BuildMountainIds()
    {
        mountainIds = new int[cells.Length];
        for (int i = 0; i < mountainIds.Length; i++)
            mountainIds[i] = -1;

        mountainCount = 0;
        var stack = new System.Collections.Generic.Stack<int>();

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                int i = z * width + x;
                if (mountainIds[i] >= 0 || !IsMountain(GetLocal(x, z)))
                    continue;

                int id = mountainCount++;
                stack.Push(i);
                mountainIds[i] = id;
                while (stack.Count > 0)
                {
                    int cur = stack.Pop();
                    int cx = cur % width;
                    int cz = cur / width;
                    TryPushMountain(stack, cx + 1, cz, id);
                    TryPushMountain(stack, cx - 1, cz, id);
                    TryPushMountain(stack, cx, cz + 1, id);
                    TryPushMountain(stack, cx, cz - 1, id);
                }
            }
        }
    }

    void TryPushMountain(System.Collections.Generic.Stack<int> stack, int x, int z, int id)
    {
        if (x < 0 || z < 0 || x >= width || z >= height)
            return;
        int i = z * width + x;
        if (mountainIds[i] >= 0 || !IsMountain(GetLocal(x, z)))
            return;
        mountainIds[i] = id;
        stack.Push(i);
    }

    public static bool IsMountain(WorldBiome biome)
    {
        return biome == WorldBiome.MountainPeak || biome == WorldBiome.MountainSlope;
    }

    public int MountainCount => mountainCount;

    public int GetMountainId(Vector2Int cell)
    {
        if (!ready || mountainIds == null)
            return -1;
        int x = cell.x - minX;
        int z = cell.y - minZ;
        if (x < 0 || z < 0 || x >= width || z >= height)
            return -1;
        return mountainIds[z * width + x];
    }

    public Vector3 CellWorld(Vector2Int cell)
    {
        float y = 0f;
        if (terrain != null)
        {
            Vector3 probe = new Vector3(cell.x + 0f, 0f, cell.y + 0f);
            if (GridSystem.Instance != null)
                probe = GridSystem.Instance.GetCellCenter(cell, 0f);
            y = terrain.SampleHeight(probe) + terrain.GetPosition().y;
        }
        if (GridSystem.Instance != null)
            return GridSystem.Instance.GetCellCenter(cell, y);
        return new Vector3(cell.x, y, cell.y);
    }

    public void CollectBiomeCells(WorldBiome biome, System.Collections.Generic.List<Vector2Int> results)
    {
        if (results == null || !ready)
            return;
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (GetLocal(x, z) == biome)
                    results.Add(new Vector2Int(minX + x, minZ + z));
            }
        }
    }

    public void CollectMountainCells(int mountainId, System.Collections.Generic.List<Vector2Int> results)
    {
        if (results == null || !ready || mountainIds == null)
            return;
        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < width; x++)
            {
                if (mountainIds[z * width + x] == mountainId)
                    results.Add(new Vector2Int(minX + x, minZ + z));
            }
        }
    }

    bool NearBiome(int x, int z, int radius, WorldBiome biome)
    {
        int x0 = Mathf.Max(0, x - radius);
        int x1 = Mathf.Min(width - 1, x + radius);
        int z0 = Mathf.Max(0, z - radius);
        int z1 = Mathf.Min(height - 1, z + radius);
        for (int zz = z0; zz <= z1; zz++)
        {
            for (int xx = x0; xx <= x1; xx++)
            {
                if (GetLocal(xx, zz) == biome)
                    return true;
            }
        }
        return false;
    }

    bool NearLake(int x, int z, int radius)
    {
        int x0 = Mathf.Max(0, x - radius);
        int x1 = Mathf.Min(width - 1, x + radius);
        int z0 = Mathf.Max(0, z - radius);
        int z1 = Mathf.Min(height - 1, z + radius);
        for (int zz = z0; zz <= z1; zz++)
        {
            for (int xx = x0; xx <= x1; xx++)
            {
                if (GetLocal(xx, zz) == WorldBiome.Lake)
                    return true;
            }
        }
        return false;
    }

    float LakeNoise(int x, int z, float seedOff)
    {
        float wx = x + (Mathf.PerlinNoise(x * 0.02f + seedOff, z * 0.02f) - 0.5f) * 18f;
        float wz = z + (Mathf.PerlinNoise(x * 0.02f + 40f + seedOff, z * 0.02f) - 0.5f) * 18f;
        float a = Mathf.PerlinNoise(wx * 0.028f + seedOff, wz * 0.028f);
        float b = Mathf.PerlinNoise(wx * 0.07f + 90f + seedOff, wz * 0.07f);
        return a * 0.72f + b * 0.28f;
    }

    float MountainNoise(int x, int z, float seedOff)
    {
        float wx = x + (Mathf.PerlinNoise(x * 0.015f + seedOff + 3f, z * 0.015f) - 0.5f) * 22f;
        float wz = z + (Mathf.PerlinNoise(x * 0.015f + seedOff + 8f, z * 0.015f) - 0.5f) * 22f;
        float a = Mathf.PerlinNoise(wx * 0.018f + seedOff + 21f, wz * 0.018f);
        float b = Mathf.PerlinNoise(wx * 0.05f + seedOff + 55f, wz * 0.05f);
        return a * 0.75f + b * 0.25f;
    }

    float VegNoise(int x, int z, float seedOff)
    {
        float a = Mathf.PerlinNoise(x * 0.04f + seedOff + 11f, z * 0.04f);
        float b = Mathf.PerlinNoise(x * 0.11f + seedOff + 70f, z * 0.11f);
        return a * 0.65f + b * 0.35f;
    }

    void Set(int x, int z, WorldBiome biome)
    {
        cells[z * width + x] = biome;
    }

    WorldBiome GetLocal(int x, int z)
    {
        if (x < 0 || z < 0 || x >= width || z >= height)
            return WorldBiome.Ocean;
        return cells[z * width + x];
    }

    public WorldBiome Get(Vector2Int cell)
    {
        if (!ready)
            return WorldBiome.Field;
        return GetLocal(cell.x - minX, cell.y - minZ);
    }

    public bool IsWater(Vector2Int cell)
    {
        WorldBiome biome = Get(cell);
        return biome == WorldBiome.Lake || biome == WorldBiome.Ocean;
    }

    public bool IsOcean(Vector2Int cell)
    {
        return Get(cell) == WorldBiome.Ocean;
    }

    public bool IsLake(Vector2Int cell)
    {
        return Get(cell) == WorldBiome.Lake;
    }

    public static bool BlocksPlayer(Vector3 world)
    {
        if (Instance == null || !Instance.ready)
            return false;
        return Instance.IsOcean(WorldToCell(world));
    }

    public static bool BlocksPlayer(Vector3 world, float radius)
    {
        if (BlocksPlayer(world))
            return true;
        if (radius < 0.05f)
            return false;
        float r = radius * 0.8f;
        return BlocksPlayer(world + new Vector3(r, 0f, 0f))
            || BlocksPlayer(world + new Vector3(-r, 0f, 0f))
            || BlocksPlayer(world + new Vector3(0f, 0f, r))
            || BlocksPlayer(world + new Vector3(0f, 0f, -r));
    }

    public Vector2Int NearestWalkable(Vector2Int cell, int maxRadius = 48)
    {
        if (!IsOcean(cell))
            return cell;

        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                Vector2Int a = new Vector2Int(cell.x + dx, cell.y - r);
                if (!IsOcean(a))
                    return a;
                Vector2Int b = new Vector2Int(cell.x + dx, cell.y + r);
                if (!IsOcean(b))
                    return b;
            }

            for (int dz = -r + 1; dz <= r - 1; dz++)
            {
                Vector2Int a = new Vector2Int(cell.x - r, cell.y + dz);
                if (!IsOcean(a))
                    return a;
                Vector2Int b = new Vector2Int(cell.x + r, cell.y + dz);
                if (!IsOcean(b))
                    return b;
            }
        }

        return cell;
    }

    public static bool CanBuild(Vector2Int origin, Vector2Int size, bool allowWater = false, bool requireWater = false)
    {
        if (Instance == null || !Instance.ready)
            return true;

        size = GridOccupancy.NormalizeSize(size);
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                bool water = Instance.IsWater(origin + new Vector2Int(x, y));
                if (requireWater)
                {
                    if (!water)
                        return false;
                }
                else if (water && !allowWater)
                {
                    return false;
                }
            }
        }
        return true;
    }

    public Color ColorOf(WorldBiome biome)
    {
        switch (biome)
        {
            case WorldBiome.Forest: return forest;
            case WorldBiome.Woodland: return woodland;
            case WorldBiome.Field: return field;
            case WorldBiome.Beach: return beach;
            case WorldBiome.Lake: return lake;
            case WorldBiome.MountainPeak: return mountainPeak;
            case WorldBiome.MountainSlope: return mountainSlope;
            default: return ocean;
        }
    }

    void PaintOverlay()
    {
        if (biomeTexture != null)
            Destroy(biomeTexture);
        if (overlayTexture != null)
            Destroy(overlayTexture);

        biomeTexture = BuildBiomeTexture(0, 0);
        Vector3 size = terrain.terrainData.size;
        int padX = Mathf.Max(1, Mathf.CeilToInt(HorizonPadMeters * width / Mathf.Max(1, size.x)));
        int padZ = Mathf.Max(1, Mathf.CeilToInt(HorizonPadMeters * height / Mathf.Max(1, size.z)));
        overlayTexture = BuildBiomeTexture(padX, padZ);

        if (overlayRoot == null)
        {
            GameObject root = new GameObject("BiomeOverlay");
            root.transform.SetParent(terrain.transform, false);
            overlayRoot = root.transform;
        }
        for (int i = overlayRoot.childCount - 1; i >= 0; i--)
            Destroy(overlayRoot.GetChild(i).gameObject);

        overlayMat = RuntimeMaterials.Create(overlayTexture, Color.white);
        if (overlayMat.HasProperty("_WalkUvFog"))
            overlayMat.SetFloat("_WalkUvFog", 0f);

        Vector3 origin = terrain.GetPosition();
        float worldW = size.x + padX * 2f * (size.x / Mathf.Max(1, width));
        float worldH = size.z + padZ * 2f * (size.z / Mathf.Max(1, height));
        float tile = 64f;
        int nx = Mathf.Max(1, Mathf.CeilToInt(worldW / tile));
        int nz = Mathf.Max(1, Mathf.CeilToInt(worldH / tile));
        float x0 = origin.x + size.x * 0.5f - worldW * 0.5f;
        float z0 = origin.z + size.z * 0.5f - worldH * 0.5f;

        for (int iz = 0; iz < nz; iz++)
        {
            for (int ix = 0; ix < nx; ix++)
            {
                float x = x0 + (ix + 0.5f) * tile;
                float z = z0 + (iz + 0.5f) * tile;
                float w = Mathf.Min(tile, x0 + worldW - (x0 + ix * tile));
                float h = Mathf.Min(tile, z0 + worldH - (z0 + iz * tile));
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "Tile";
                go.transform.SetParent(overlayRoot, false);
                Collider col = go.GetComponent<Collider>();
                if (col != null)
                    Destroy(col);
                go.transform.position = new Vector3(x, 0.01f, z);
                go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                go.transform.localScale = new Vector3(w, h, 1f);
                MeshRenderer rend = go.GetComponent<MeshRenderer>();
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                Material mat = new Material(overlayMat);
                float u0 = (ix * tile) / worldW;
                float v0 = (iz * tile) / worldH;
                mat.mainTextureScale = new Vector2(w / worldW, h / worldH);
                mat.mainTextureOffset = new Vector2(u0, v0);
                if (mat.HasProperty("_WalkUvFog"))
                    mat.SetFloat("_WalkUvFog", 0f);
                rend.sharedMaterial = mat;
            }
        }

        RefreshOverlayGfx();
    }

    Texture2D BuildBiomeTexture(int padX, int padZ)
    {
        int tw = width + padX * 2;
        int th = height + padZ * 2;
        Texture2D tex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[tw * th];
        for (int z = 0; z < th; z++)
        {
            for (int x = 0; x < tw; x++)
            {
                Color c = ColorOf(GetLocal(x - padX, z - padZ));
                float n = (Mathf.PerlinNoise(x * 0.35f, z * 0.35f) - 0.5f) * 0.06f;
                c.r = Mathf.Clamp01(c.r + n);
                c.g = Mathf.Clamp01(c.g + n);
                c.b = Mathf.Clamp01(c.b + n);
                pixels[z * tw + x] = c;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    public void RefreshOverlayGfx()
    {
        if (overlayRoot == null)
            return;
        MeshRenderer[] rends = overlayRoot.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            Material mat = rends[i] != null ? rends[i].sharedMaterial : null;
            RuntimeMaterials.ApplyWorldGfx(mat);
            if (mat != null && mat.HasProperty("_WalkUvFog"))
                mat.SetFloat("_WalkUvFog", 0f);
        }
    }
}
