using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Песочница ([[TestYard]]): все здания, все формы лент и труб, подземки, рука, жилы всех видов
/// с экстракторами, все декорации ([[Decorations]]) и напольная плитка под зданием.
/// Создаётся из главного меню: Shift+Ctrl+Alt + «Продолжить».
/// Версия и угол двора пишутся в сейв: устаревший двор пересобирается при загрузке,
/// а жилы двора (их нет в генерации мира) ставятся заново при каждой загрузке.
/// </summary>
public static class TestYard
{
    /// <summary>Поднять, когда меняется состав двора: старые тестовые миры пересоберутся.</summary>
    public const int Version = 2;

    const int PadW = 60;
    const int PadH = 72;
    const string KeyVersion = "testyard_v";
    const string KeyX = "testyard_x";
    const string KeyZ = "testyard_z";

    static readonly string[] VeinKinds = { "iron", "cooper", "coal", "stone", "sand", "sulfur", "tree" };
    const int VeinRowZ = 17;
    const int VeinStep = 7;

    static bool hasOrigin;
    static Vector2Int padOrigin;

    // ---------- Вход из SaveSystem ----------

    /// <summary>Песочница загружена: пустой или устаревший двор — пересобрать, иначе вернуть жилы.</summary>
    public static void OnSandboxLoaded(SaveData data, int buildingCount)
    {
        hasOrigin = TryReadOrigin(data, out padOrigin);
        int version = ReadInt(data, KeyVersion, 0);
        if (buildingCount == 0 || version < Version || !hasOrigin)
        {
            if (buildingCount > 0)
                SaveSystem.ClearWorldBuildings();
            Populate();
            return;
        }

        RestoreVeins(padOrigin);
    }

    public static void CaptureSave(SaveData data)
    {
        if (data == null || !hasOrigin || WorldCatalog.Active == null || !WorldCatalog.Active.sandbox)
            return;
        if (data.extras == null)
            data.extras = new List<SaveKeyValue>();
        var inv = CultureInfo.InvariantCulture;
        data.extras.Add(new SaveKeyValue { key = KeyVersion, value = Version.ToString(inv) });
        data.extras.Add(new SaveKeyValue { key = KeyX, value = padOrigin.x.ToString(inv) });
        data.extras.Add(new SaveKeyValue { key = KeyZ, value = padOrigin.y.ToString(inv) });
    }

    public static void Populate()
    {
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.CompleteAllResearch(false);
        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.AddCoins(500000, MoneySource.Cheat);
            PlayerWallet.Instance.AddRubies(999, MoneySource.Cheat);
        }

        Vector2Int o = FindPad();
        padOrigin = o;
        hasOrigin = true;

        BuildingData belt = GameDatabase.FindBuilding("conveyor");
        BuildingData pipe = GameDatabase.FindBuilding("pipe");
        BuildingData ug = GameDatabase.FindBuilding("underground_conveyor");
        BuildingData splitter = GameDatabase.FindBuilding("splitter");
        BuildingData pipeSplitter = GameDatabase.FindBuilding("pipe_splitter");
        BuildingData extractor = GameDatabase.FindBuilding("extractor");
        BuildingData storage = GameDatabase.FindBuilding("storage_container");
        BuildingData tank = GameDatabase.FindBuilding("fluid_storage_tank");
        BuildingData water = GameDatabase.FindBuilding("water_extractor");
        BuildingData arm = GameDatabase.FindBuilding("robotic_arm");

        BuildingLinker.SuppressRelink = true;
        try
        {
            PlaceShapes(o, 1, belt, splitter);                  // ленты: z 1..5
            PlaceShapes(o, 8, pipe, pipeSplitter);              // трубы: z 8..12
            PlaceTunnelsAndArm(o, belt, ug, arm, storage, tank, pipe);  // z 14
            PlaceVeinLines(o, extractor, belt, storage);         // z 16..18
            int z = PlaceBuildingGrid(o, VeinRowZ + 5, belt);
            z = PlaceFloorDemo(o, z + 2, storage, belt);
            PlaceDecorGrid(o, z + 2);
            PlaceWater(o, water);
        }
        finally
        {
            BuildingLinker.SuppressRelink = false;
        }

        BuildingLinker.RelinkAll();
        RebindExtractors();
        TeleportPlayer(o);
        Debug.Log("[TestYard] v" + Version + " pad " + o.x + "," + o.y);
    }

    // ---------- Ленты и трубы: все формы ----------

    /// <summary>
    /// Все формы линии для ленты или трубы (одинаковая логика форм): прямая, оба угла, обе T,
    /// два бока, три входа и сплиттер с тремя выходами. Ряд высотой 5 клеток от z0.
    /// </summary>
    static void PlaceShapes(Vector2Int o, int z0, BuildingData line, BuildingData splitter)
    {
        if (line == null)
            return;
        const float north = 0f, east = 90f, south = 180f, west = 270f;
        int z = z0 + 1;

        // прямая
        for (int x = 2; x <= 5; x++)
            Put(line, o, x, z, east);
        // угол: снизу вверх и направо
        Put(line, o, 8, z - 1, north);
        Put(line, o, 8, z, east);
        Put(line, o, 9, z, east);
        // угол: сверху вниз и направо
        Put(line, o, 12, z + 1, south);
        Put(line, o, 12, z, east);
        Put(line, o, 13, z, east);
        // T: вход сзади + справа
        Put(line, o, 16, z - 1, north);
        Put(line, o, 17, z, west);
        Put(line, o, 16, z, north);
        Put(line, o, 16, z + 1, north);
        // T: вход сзади + слева
        Put(line, o, 21, z - 1, north);
        Put(line, o, 20, z, east);
        Put(line, o, 21, z, north);
        Put(line, o, 21, z + 1, north);
        // два бока, сзади пусто
        Put(line, o, 25, z, east);
        Put(line, o, 27, z, west);
        Put(line, o, 26, z, north);
        Put(line, o, 26, z + 1, north);
        // три входа
        Put(line, o, 32, z - 1, north);
        Put(line, o, 31, z, east);
        Put(line, o, 33, z, west);
        Put(line, o, 32, z, north);
        Put(line, o, 32, z + 1, north);
        // сплиттер: вход снизу, три выхода
        if (splitter != null)
        {
            Put(line, o, 38, z - 1, north);
            Put(splitter, o, 38, z, north);
            Put(line, o, 38, z + 1, north);
            Put(line, o, 39, z, east);
            Put(line, o, 37, z, west);
        }

        // длинная змейка: углы в обе стороны подряд
        Put(line, o, 43, z - 1, east);
        Put(line, o, 44, z - 1, north);
        Put(line, o, 44, z, north);
        Put(line, o, 44, z + 1, east);
        Put(line, o, 45, z + 1, south);
        Put(line, o, 45, z, south);
        Put(line, o, 45, z - 1, east);
        Put(line, o, 46, z - 1, east);
    }

    static void PlaceTunnelsAndArm(Vector2Int o, BuildingData belt, BuildingData ug, BuildingData arm,
        BuildingData storage, BuildingData tank, BuildingData pipe)
    {
        const float east = 90f;
        const int z = 14;
        // подземка вплотную (зазор 0) и на максимум (зазор pairMaxGap) с лентами на входе и выходе
        int maxGap = ug != null ? Mathf.Max(1, ug.pairMaxGap) : 5;
        Put(belt, o, 1, z, east);
        PutPair(ug, o, 2, z, 3, z, east);
        Put(belt, o, 4, z, east);

        Put(belt, o, 7, z, east);
        PutPair(ug, o, 8, z, 9 + maxGap, z, east);
        Put(belt, o, 10 + maxGap, z, east);

        // склад → рука → склад (рука берёт сзади, кладёт вперёд)
        int ax = 20 + maxGap;
        Put(storage, o, ax, z, 0f);
        Put(arm, o, ax + 1, z, east);
        Put(storage, o, ax + 2, z, 0f);

        // склад → лента → лента в склад
        Put(storage, o, ax + 5, z, 0f);
        Put(belt, o, ax + 6, z, east);
        Put(belt, o, ax + 7, z, east);
        Put(storage, o, ax + 8, z, 0f);

        // бак → труба → бак
        Put(tank, o, ax + 11, z, east);
        Put(pipe, o, ax + 12, z, east);
        Put(pipe, o, ax + 13, z, east);
        Put(tank, o, ax + 14, z, east);
    }

    // ---------- Жилы ----------

    static void PlaceVeinLines(Vector2Int o, BuildingData extractor, BuildingData belt, BuildingData storage)
    {
        SpawnYardVeins(o);
        for (int i = 0; i < VeinKinds.Length; i++)
        {
            int x = 2 + i * VeinStep;
            Put(extractor, o, x, VeinRowZ, 90f);
            Put(belt, o, x + 1, VeinRowZ, 90f);
            Put(belt, o, x + 2, VeinRowZ, 90f);
            Put(storage, o, x + 3, VeinRowZ, 0f);
        }
    }

    /// <summary>Жила под экстрактором и две рядом (видно, что это скопление). Повтор безопасен.</summary>
    static void SpawnYardVeins(Vector2Int o)
    {
        WorldResourceScatterer scatter = WorldResourceScatterer.Instance;
        if (scatter == null)
        {
            Debug.LogWarning("[TestYard] Нет WorldResourceScatterer — жилы не поставлены");
            return;
        }

        for (int i = 0; i < VeinKinds.Length; i++)
        {
            int x = 2 + i * VeinStep;
            Vector2Int under = o + new Vector2Int(x, VeinRowZ);
            scatter.DevSpawnVein(VeinKinds[i], under);
            scatter.DevSpawnVein(VeinKinds[i], under + new Vector2Int(0, -1));
            scatter.DevSpawnVein(VeinKinds[i], under + new Vector2Int(0, 1));
            if (!ResourceNode.HasNode(under))
                Debug.LogWarning("[TestYard] Жила " + VeinKinds[i] + " не встала в " + under);
        }
    }

    static void RestoreVeins(Vector2Int o)
    {
        SpawnYardVeins(o);
        RebindExtractors();
    }

    static void RebindExtractors()
    {
        Extractor[] all = Object.FindObjectsByType<Extractor>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null)
                all[i].BindToNearbyNode();
        }
    }

    // ---------- Все здания ----------

    /// <summary>Сетка всех зданий (кроме лент и добычи с жилы/воды), у каждого лента во вход. Возвращает z за последним рядом.</summary>
    static int PlaceBuildingGrid(Vector2Int o, int z0, BuildingData belt)
    {
        BuildingData[] all = GameDatabase.AllBuildings();
        int x = 2;
        int z = z0;
        int rowH = 0;
        for (int i = 0; i < all.Length; i++)
        {
            BuildingData data = all[i];
            if (data == null || data.prefab == null)
                continue;
            if (data.IsConveyor || data.IsPairedStraight)
                continue;
            if (PlayerBuilder.NeedsResourceNode(data) || data.requiresWater)
                continue;

            Vector2Int size = GridFootprint.GetRotatedSize(data.size, 0f);
            if (x + size.x >= PadW - 1)
            {
                x = 2;
                z += rowH + 2;
                rowH = 0;
            }

            Put(data, o, x, z, 0f);
            if (belt != null)
                Put(belt, o, x, z - 1, 0f);
            rowH = Mathf.Max(rowH, size.y);
            x += size.x + 2;
        }

        return z + rowH;
    }

    // ---------- Декорации ----------

    /// <summary>Площадка из плитки со складом и лентой поверх, асфальтовая дорога вдоль. Возвращает z за ней.</summary>
    static int PlaceFloorDemo(Vector2Int o, int z0, BuildingData storage, BuildingData belt)
    {
        BuildingData paving = GameDatabase.FindBuilding("decor_paving");
        BuildingData asphalt = GameDatabase.FindBuilding("decor_asphalt");
        for (int x = 2; x < PadW - 2; x++)
            Put(asphalt, o, x, z0, 90f);
        for (int x = 2; x < 10; x++)
        {
            for (int z = z0 + 1; z < z0 + 5; z++)
                Put(paving, o, x, z, 0f);
        }

        // здания и декорации стоят на плитке — она в своей сетке
        Put(storage, o, 3, z0 + 2, 0f);
        Put(belt, o, 4, z0 + 2, 90f);
        Put(belt, o, 5, z0 + 2, 90f);
        Put(storage, o, 6, z0 + 2, 0f);
        Put(GameDatabase.FindBuilding("decor_bench"), o, 3, z0 + 4, 180f);
        Put(GameDatabase.FindBuilding("decor_street_lamp"), o, 8, z0 + 4, 0f);
        Put(GameDatabase.FindBuilding("decor_vending"), o, 9, z0 + 1, 270f);
        return z0 + 5;
    }

    /// <summary>Все декорации по категориям, лицом к дороге (−Z). Напольные — уже в демо плитки.</summary>
    static void PlaceDecorGrid(Vector2Int o, int z0)
    {
        IReadOnlyList<DecorCatalog.Def> all = DecorCatalog.All;
        int x = 2;
        int z = z0;
        int rowH = 0;
        DecorCatalog.Cat? cat = null;
        for (int i = 0; i < all.Count; i++)
        {
            DecorCatalog.Def def = all[i];
            if (def.data == null || def.IsFloor)
                continue;
            Vector2Int size = def.size;
            bool newCat = cat.HasValue && cat.Value != def.cat;
            if (x + size.x >= PadW - 1 || newCat)
            {
                x = 2;
                z += rowH + 2;
                rowH = 0;
            }

            cat = def.cat;
            Put(def.data, o, x, z, 180f);
            rowH = Mathf.Max(rowH, size.y);
            x += size.x + 1;
        }

        // линия забора и сетки — проверка поворота по линии
        BuildingData fence = GameDatabase.FindBuilding("decor_fence");
        BuildingData wire = GameDatabase.FindBuilding("decor_wire_fence");
        int fz = z + rowH + 2;
        for (int k = 0; k < 6; k++)
        {
            Put(fence, o, 2 + k, fz, 90f);
            Put(wire, o, 10 + k, fz, 90f);
        }
    }

    static void PlaceWater(Vector2Int o, BuildingData water)
    {
        if (water == null || WorldBiomeMap.Instance == null)
            return;
        Vector2Int lake = FindWaterNear(o);
        if (lake.x == int.MinValue)
            return;
        PutAt(water, lake, 0f);
    }

    // ---------- Установка ----------

    static void Put(BuildingData data, Vector2Int origin, int x, int z, float yaw)
    {
        PutAt(data, origin + new Vector2Int(x, z), yaw);
    }

    static void PutAt(BuildingData data, Vector2Int min, float yaw)
    {
        if (data == null || data.prefab == null)
            return;
        Vector2Int size = GridFootprint.GetRotatedSize(data.size, yaw);
        bool free = DecorSystem.IsAreaFreeFor(data, min, size, null, null);
        if (!free && !data.allowOnWater)
            return;
        Vector3 pos = GridFootprint.MinCellToCenter(min, size, GroundY(min, size));
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
        Vector3 inPos = GridFootprint.MinCellToCenter(a, Vector2Int.one, GroundY(a, Vector2Int.one));
        Vector3 outPos = GridFootprint.MinCellToCenter(b, Vector2Int.one, GroundY(b, Vector2Int.one));
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

    /// <summary>Высота земли под центром footprint (раньше всё ставилось на высоту начала сетки и висело/тонуло на рельефе).</summary>
    static float GroundY(Vector2Int min, Vector2Int size)
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady)
            return GridSystem.Instance != null ? GridSystem.Instance.origin.y : 0f;
        return map.CellWorld(min + new Vector2Int(size.x / 2, size.y / 2)).y;
    }

    // ---------- Площадка ----------

    /// <summary>Ровная суша без воды и чужих жил; если такой нет — с наименьшим числом плохих клеток.</summary>
    static Vector2Int FindPad()
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        Vector2Int center = Vector2Int.zero;
        if (GridSystem.Instance != null)
            center = GridSystem.Instance.WorldToCell(map != null ? map.PlayableCenterWorld : Vector3.zero);
        center -= new Vector2Int(PadW / 2, PadH / 2);

        Vector2Int best = center;
        float bestScore = float.MaxValue;
        for (int r = 0; r <= 120; r += 8)
        {
            for (int dz = -r; dz <= r; dz += 8)
            {
                for (int dx = -r; dx <= r; dx += 8)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r)
                        continue;
                    Vector2Int o = center + new Vector2Int(dx, dz);
                    float score = PadScore(o, bestScore);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = o;
                        if (score <= 0.5f)
                            return best;
                    }
                }
            }
        }

        return best;
    }

    static float PadScore(Vector2Int o, float stopAbove)
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady)
            return 0f;
        float bad = 0f;
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int z = 0; z < PadH; z += 2)
        {
            for (int x = 0; x < PadW; x += 2)
            {
                Vector2Int c = o + new Vector2Int(x, z);
                WorldBiome b = map.Get(c);
                if (b == WorldBiome.Lake || b == WorldBiome.Ocean)
                    bad += 10f;
                else if (b == WorldBiome.MountainPeak || b == WorldBiome.MountainSlope)
                    bad += 3f;
                if (ResourceNode.HasNode(c))
                    bad += 1f;
                if ((x % 8) == 0 && (z % 8) == 0)
                {
                    float y = map.CellWorld(c).y;
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
                if (bad > stopAbove)
                    return bad;
            }
        }

        // перепад высот тоже плох: ленты на склоне выглядят криво
        return bad + Mathf.Max(0f, maxY - minY - 1f) * 2f;
    }

    static Vector2Int FindWaterNear(Vector2Int o)
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null)
            return new Vector2Int(int.MinValue, 0);
        for (int r = 1; r < 90; r++)
        {
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r)
                        continue;
                    Vector2Int c = o + new Vector2Int(dx, dz);
                    if (map.Get(c) == WorldBiome.Lake && map.Get(c + Vector2Int.one) == WorldBiome.Lake)
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
        Vector2Int stand = origin + new Vector2Int(PadW / 2, -1);
        player.TeleportToCell(stand);
        player.ApplySavedPose(player.transform.position, 0f, 12f);
    }

    // ---------- Сейв ----------

    static bool TryReadOrigin(SaveData data, out Vector2Int origin)
    {
        origin = default;
        int x = ReadInt(data, KeyX, int.MinValue);
        int z = ReadInt(data, KeyZ, int.MinValue);
        if (x == int.MinValue || z == int.MinValue)
            return false;
        origin = new Vector2Int(x, z);
        return true;
    }

    static int ReadInt(SaveData data, string key, int fallback)
    {
        if (data == null || data.extras == null)
            return fallback;
        for (int i = 0; i < data.extras.Count; i++)
        {
            SaveKeyValue row = data.extras[i];
            if (row != null && row.key == key
                && int.TryParse(row.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
                return v;
        }

        return fallback;
    }
}
