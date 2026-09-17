using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Один тик логистики и кулла зданий вместо Update/LateUpdate на каждом объекте.
/// </summary>
public class WorldSim : MonoBehaviour
{
    public const float CullCell = 32f;

    static WorldSim instance;
    readonly List<BuildingBase> buildings = new List<BuildingBase>(256);
    readonly List<Conveyor> belts = new List<Conveyor>(256);
    readonly List<Splitter> splitters = new List<Splitter>(32);
    readonly Dictionary<Vector2Int, List<BuildingBase>> buckets = new Dictionary<Vector2Int, List<BuildingBase>>(64);
    readonly HashSet<Vector2Int> lastVisible = new HashSet<Vector2Int>();
    readonly List<Vector2Int> visibleScratch = new List<Vector2Int>(32);
    readonly Dictionary<BuildingBase, Vector2Int> buildingBucket = new Dictionary<BuildingBase, Vector2Int>(256);
    readonly List<BuildingBase> flushQueue = new List<BuildingBase>(64);
    bool cullPrimed;

    public static WorldSim Ensure()
    {
        if (instance != null)
            return instance;
        GridSystem grid = GridSystem.Instance;
        if (grid != null)
        {
            instance = grid.GetComponent<WorldSim>();
            if (instance == null)
                instance = grid.gameObject.AddComponent<WorldSim>();
            return instance;
        }

        var go = new GameObject("WorldSim");
        instance = go.AddComponent<WorldSim>();
        return instance;
    }

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public static void RegisterBuilding(BuildingBase building)
    {
        if (building == null)
            return;
        WorldSim sim = Ensure();
        if (sim.buildings.Contains(building))
        {
            sim.MoveBucket(building);
            return;
        }
        sim.buildings.Add(building);
        sim.AddToBucket(building);
    }

    public static void UnregisterBuilding(BuildingBase building)
    {
        if (instance == null || building == null)
            return;
        instance.buildings.Remove(building);
        instance.RemoveFromBucket(building);
        instance.buildingBucket.Remove(building);
    }

    public static void RegisterBelt(Conveyor belt)
    {
        if (belt == null)
            return;
        WorldSim sim = Ensure();
        if (!sim.belts.Contains(belt))
            sim.belts.Add(belt);
        RegisterBuilding(belt);
    }

    public static void UnregisterBelt(Conveyor belt)
    {
        if (instance == null || belt == null)
            return;
        instance.belts.Remove(belt);
        UnregisterBuilding(belt);
    }

    public static void RegisterSplitter(Splitter splitter)
    {
        if (splitter == null)
            return;
        WorldSim sim = Ensure();
        if (!sim.splitters.Contains(splitter))
            sim.splitters.Add(splitter);
        RegisterBuilding(splitter);
    }

    public static void UnregisterSplitter(Splitter splitter)
    {
        if (instance == null || splitter == null)
            return;
        instance.splitters.Remove(splitter);
        UnregisterBuilding(splitter);
    }

    public static void MarkFlush(BuildingBase building)
    {
        if (building == null)
            return;
        WorldSim sim = Ensure();
        if (!sim.flushQueue.Contains(building))
            sim.flushQueue.Add(building);
    }

    void Update()
    {
        BeltItemView.BeginFrame();
        float dt = Time.deltaTime;

        for (int i = belts.Count - 1; i >= 0; i--)
        {
            if (belts[i] == null)
            {
                belts.RemoveAt(i);
                continue;
            }
            belts[i].SimStep(dt);
            belts[i].SimDraw();
        }
        for (int i = splitters.Count - 1; i >= 0; i--)
        {
            if (splitters[i] == null)
            {
                splitters.RemoveAt(i);
                continue;
            }
            splitters[i].SimTick();
        }

        BeltItemView.Flush();
    }

    void LateUpdate()
    {
        for (int i = flushQueue.Count - 1; i >= 0; i--)
        {
            BuildingBase b = flushQueue[i];
            if (b == null)
            {
                flushQueue.RemoveAt(i);
                continue;
            }
            b.SimFlush();
            if (b.OutputBufferCount <= 0)
                flushQueue.RemoveAt(i);
        }
        CullVisible();
    }

    void CullVisible()
    {
        if (!WorldView.HasPlayer)
            return;

        Vector3 p = WorldView.PlayerPos;
        float radius = WorldView.Radius + CullCell;
        int r = Mathf.Max(1, Mathf.CeilToInt(radius / CullCell));
        Vector2Int origin = BucketOf(p);
        visibleScratch.Clear();
        for (int z = -r; z <= r; z++)
        {
            for (int x = -r; x <= r; x++)
                visibleScratch.Add(new Vector2Int(origin.x + x, origin.y + z));
        }

        if (!cullPrimed)
        {
            for (int i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] != null)
                    buildings[i].CullTick(false);
            }
            cullPrimed = true;
        }

        for (int i = 0; i < visibleScratch.Count; i++)
        {
            Vector2Int cell = visibleScratch[i];
            List<BuildingBase> list;
            if (!buckets.TryGetValue(cell, out list))
                continue;
            for (int b = 0; b < list.Count; b++)
            {
                if (list[b] != null)
                    list[b].CullTick(true);
            }
        }

        foreach (Vector2Int cell in lastVisible)
        {
            bool stay = false;
            for (int i = 0; i < visibleScratch.Count; i++)
            {
                if (visibleScratch[i] == cell)
                {
                    stay = true;
                    break;
                }
            }
            if (stay)
                continue;
            List<BuildingBase> list;
            if (!buckets.TryGetValue(cell, out list))
                continue;
            for (int b = 0; b < list.Count; b++)
            {
                if (list[b] != null)
                    list[b].CullTick(false);
            }
        }

        lastVisible.Clear();
        for (int i = 0; i < visibleScratch.Count; i++)
            lastVisible.Add(visibleScratch[i]);
    }

    void MoveBucket(BuildingBase building)
    {
        RemoveFromBucket(building);
        AddToBucket(building);
    }

    void AddToBucket(BuildingBase building)
    {
        Vector2Int cell = BucketOf(building.transform.position);
        buildingBucket[building] = cell;
        List<BuildingBase> list;
        if (!buckets.TryGetValue(cell, out list))
        {
            list = new List<BuildingBase>(8);
            buckets[cell] = list;
        }
        if (!list.Contains(building))
            list.Add(building);
    }

    void RemoveFromBucket(BuildingBase building)
    {
        Vector2Int cell;
        if (buildingBucket.TryGetValue(building, out cell))
        {
            List<BuildingBase> list;
            if (buckets.TryGetValue(cell, out list))
                list.Remove(building);
            buildingBucket.Remove(building);
            return;
        }

        foreach (KeyValuePair<Vector2Int, List<BuildingBase>> pair in buckets)
            pair.Value.Remove(building);
    }

    static Vector2Int BucketOf(Vector3 world)
    {
        return new Vector2Int(
            Mathf.FloorToInt(world.x / CullCell),
            Mathf.FloorToInt(world.z / CullCell));
    }
}
