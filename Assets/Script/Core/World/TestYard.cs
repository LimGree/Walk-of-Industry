using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Песочница: все здания, все формы лент, жилы с экстракторами.
/// Создаётся из главного меню: Shift+Ctrl+Alt + «Продолжить».
/// </summary>
public static class TestYard
{
    const int PadW = 48;
    const int PadH = 28;

    static float Y
    {
        get
        {
            if (GridSystem.Instance != null)
                return GridSystem.Instance.origin.y;
            return 0f;
        }
    }

    public static void Populate()
    {
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.CompleteAllResearch(false);
        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.AddCoins(500000);
            PlayerWallet.Instance.AddRubies(999);
        }

        Vector2Int origin = FindPad();
        BuildingData belt = GameDatabase.FindBuilding("conveyor");
        BuildingData pipe = GameDatabase.FindBuilding("pipe");
        BuildingData ug = GameDatabase.FindBuilding("underground_conveyor");
        BuildingData splitter = GameDatabase.FindBuilding("splitter");
        BuildingData extractor = GameDatabase.FindBuilding("extractor");
        BuildingData storage = GameDatabase.FindBuilding("storage_container");
        BuildingData tank = GameDatabase.FindBuilding("fluid_storage_tank");
        BuildingData water = GameDatabase.FindBuilding("water_extractor");
        BuildingData oil = GameDatabase.FindBuilding("oil_extractor");

        PlaceBeltMuseum(origin, belt, pipe, ug, splitter, tank);
        PlaceVeinLines(origin, extractor, belt, storage);
        PlaceBuildingGrid(origin, belt);
        PlaceWaterAndOil(origin, water, oil);

        BuildingLinker.RelinkAll();
        TeleportPlayer(origin);
        Debug.Log("[TestYard] pad " + origin.x + "," + origin.y);
    }

    static void PlaceBeltMuseum(
        Vector2Int o,
        BuildingData belt,
        BuildingData pipe,
        BuildingData ug,
        BuildingData splitter,
        BuildingData tank)
    {
        float east = 90f;
        float north = 0f;
        float south = 180f;
        float west = 270f;

        Put(belt, o, 2, 2, east);
        Put(belt, o, 3, 2, east);
        Put(belt, o, 4, 2, east);
        Put(belt, o, 5, 2, east);

        Put(belt, o, 8, 1, north);
        Put(belt, o, 8, 2, east);

        Put(belt, o, 12, 3, south);
        Put(belt, o, 12, 2, east);

        Put(belt, o, 16, 2, north);
        Put(belt, o, 17, 3, west);
        Put(belt, o, 16, 3, north);
        Put(belt, o, 16, 4, north);

        Put(belt, o, 21, 2, north);
        Put(belt, o, 20, 3, east);
        Put(belt, o, 21, 3, north);
        Put(belt, o, 21, 4, north);

        Put(belt, o, 25, 3, east);
        Put(belt, o, 27, 3, west);
        Put(belt, o, 26, 3, north);
        Put(belt, o, 26, 4, north);

        Put(belt, o, 32, 2, north);
        Put(belt, o, 31, 3, east);
        Put(belt, o, 33, 3, west);
        Put(belt, o, 32, 3, north);
        Put(belt, o, 32, 4, north);

        Put(belt, o, 38, 2, north);
        Put(splitter, o, 38, 3, north);
        Put(belt, o, 38, 4, north);
        Put(belt, o, 39, 3, east);
        Put(belt, o, 37, 3, west);

        PutPair(ug, o, 2, 6, 7, 6, east);

        Put(pipe, o, 10, 6, east);
        Put(pipe, o, 11, 6, east);
        Put(pipe, o, 12, 6, east);
        Put(tank, o, 14, 6, north);
    }

    static void PlaceVeinLines(Vector2Int o, BuildingData extractor, BuildingData belt, BuildingData storage)
    {
        string[] kinds = { "iron", "cooper", "coal", "stone", "sand", "sulfur", "tree" };
        int x = 2;
        int z = 10;
        for (int i = 0; i < kinds.Length; i++)
        {
            Vector2Int cell = o + new Vector2Int(x, z);
            if (WorldResourceScatterer.Instance != null)
                WorldResourceScatterer.Instance.DevSpawnVein(kinds[i], cell);
            Put(extractor, o, x, z, 90f);
            Put(belt, o, x + 1, z, 90f);
            Put(belt, o, x + 2, z, 90f);
            Put(storage, o, x + 3, z, 0f);
            x += 6;
        }
    }

    static void PlaceBuildingGrid(Vector2Int o, BuildingData belt)
    {
        BuildingData[] all = GameDatabase.AllBuildings();
        int x = 2;
        int z = 16;
        int rowH = 4;
        for (int i = 0; i < all.Length; i++)
        {
            BuildingData data = all[i];
            if (data == null || data.prefab == null)
                continue;
            if (data.IsConveyor || data.IsPairedStraight)
                continue;
            if (data.requiresResourceNode || data.requiresWater)
                continue;

            Vector2Int size = GridFootprint.GetRotatedSize(data.size, 0f);
            if (x + size.x >= PadW - 1)
            {
                x = 2;
                z += rowH;
            }

            Put(data, o, x, z, 0f);
            if (belt != null && !data.IsConveyor)
                Put(belt, o, x, z - 1, 0f);
            x += size.x + 2;
        }
    }

    static void PlaceWaterAndOil(Vector2Int o, BuildingData water, BuildingData oil)
    {
        if (oil != null)
            Put(oil, o, 44, 6, 0f);

        if (water == null || WorldBiomeMap.Instance == null)
            return;
        Vector2Int lake = FindWaterNear(o);
        if (lake.x == int.MinValue)
            return;
        PutAt(water, lake, 0f);
    }

    static void Put(BuildingData data, Vector2Int origin, int x, int z, float yaw)
    {
        PutAt(data, origin + new Vector2Int(x, z), yaw);
    }

    static void PutAt(BuildingData data, Vector2Int min, float yaw)
    {
        if (data == null || data.prefab == null)
            return;
        Vector2Int size = GridFootprint.GetRotatedSize(data.size, yaw);
        if (!GridOccupancy.IsAreaFree(min, size) && !data.requiresResourceNode && !data.allowOnWater)
            return;
        Vector3 pos = GridFootprint.MinCellToCenter(min, size, Y);
        GameObject go = Object.Instantiate(data.prefab, pos, Quaternion.Euler(0f, yaw, 0f));
        BuildingBase building = go.GetComponent<BuildingBase>();
        if (building == null)
            return;
        building.data = data;
        building.OnPlaced();
    }

    static void PutPair(BuildingData data, Vector2Int origin, int x0, int z0, int x1, int z1, float yaw)
    {
        if (data == null || data.prefab == null || data.pairExitPrefab == null)
            return;
        Vector2Int a = origin + new Vector2Int(x0, z0);
        Vector2Int b = origin + new Vector2Int(x1, z1);
        Vector3 inPos = GridFootprint.MinCellToCenter(a, Vector2Int.one, Y);
        Vector3 outPos = GridFootprint.MinCellToCenter(b, Vector2Int.one, Y);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        GameObject inGo = Object.Instantiate(data.prefab, inPos, rot);
        GameObject outGo = Object.Instantiate(data.pairExitPrefab, outPos, rot);
        UndergroundConveyor entrance = inGo.GetComponent<UndergroundConveyor>();
        UndergroundConveyor exit = outGo.GetComponent<UndergroundConveyor>();
        if (entrance == null)
            entrance = inGo.AddComponent<UndergroundConveyor>();
        if (exit == null)
            exit = outGo.AddComponent<UndergroundConveyor>();
        entrance.data = data;
        exit.data = data;
        UndergroundConveyor.BindPair(entrance, exit);
        entrance.OnPlaced();
        exit.OnPlaced();
    }

    static Vector2Int FindPad()
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        Vector2Int center = Vector2Int.zero;
        if (GridSystem.Instance != null)
            center = GridSystem.Instance.WorldToCell(
                map != null ? map.PlayableCenterWorld : Vector3.zero);

        for (int r = 0; r <= 80; r += 4)
        {
            for (int dz = -r; dz <= r; dz += 4)
            {
                for (int dx = -r; dx <= r; dx += 4)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r)
                        continue;
                    Vector2Int o = center + new Vector2Int(dx, dz);
                    if (RectLand(o))
                        return o;
                }
            }
        }

        return center;
    }

    static bool RectLand(Vector2Int o)
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        for (int z = 0; z < PadH; z++)
        {
            for (int x = 0; x < PadW; x++)
            {
                Vector2Int c = o + new Vector2Int(x, z);
                if (map != null && map.IsWater(c))
                    return false;
            }
        }

        return true;
    }

    static Vector2Int FindWaterNear(Vector2Int o)
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null)
            return new Vector2Int(int.MinValue, 0);
        for (int r = 1; r < 80; r++)
        {
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r)
                        continue;
                    Vector2Int c = o + new Vector2Int(dx, dz);
                    if (map.Get(c) == WorldBiome.Lake)
                        return c;
                }
            }
        }

        return new Vector2Int(int.MinValue, 0);
    }

    static void TeleportPlayer(Vector2Int origin)
    {
        PlayerMovement player = Object.FindFirstObjectByType<PlayerMovement>();
        if (player == null)
            return;
        Vector2Int stand = origin + new Vector2Int(PadW / 2, 0);
        player.TeleportToCell(stand);
        player.ApplySavedPose(player.transform.position, 0f, 12f);
    }
}
