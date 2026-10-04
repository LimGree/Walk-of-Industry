using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Песочница ([[TestYard]]): все здания, все формы лент и труб, подземки, рука, жилы всех видов
/// с экстракторами, все декорации ([[Decorations]]), напольная плитка под зданием и выставка табличек
/// (шаблоны, стойки, размеры, подсветка и приёмы оформления — [[SignEditorUI]]).
/// Восточнее — два огороженных цеха: все рецепты по стендам и цепочки от сырья до готового (TestYard.Halls.cs).
/// Создаётся из главного меню: Shift+Ctrl+Alt + «Продолжить».
/// Версия и угол двора пишутся в сейв: устаревший двор пересобирается при загрузке,
/// а жилы двора (их нет в генерации мира) ставятся заново при каждой загрузке.
/// </summary>
public static partial class TestYard
{
    /// <summary>Поднять, когда меняется состав двора: старые тестовые миры пересоберутся.</summary>
    public const int Version = 4;

    const int PadW = 60;
    const int PadH = 86;
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
        RestoreHalls(data);
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
        CaptureHalls(data);
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
            z = PlaceDecorGrid(o, z + 2);
            PlaceSignGallery(o, z + 3);
            BuildDemoHalls(o);
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
        FaceOut(Put(storage, o, ax + 5, z, 0f), E);
        Put(belt, o, ax + 6, z, east);
        Put(belt, o, ax + 7, z, east);
        FaceIn(Put(storage, o, ax + 8, z, 0f), W);

        // бак → труба → бак
        FaceOut(Put(tank, o, ax + 11, z, east), E);
        Put(pipe, o, ax + 12, z, east);
        Put(pipe, o, ax + 13, z, east);
        FaceIn(Put(tank, o, ax + 14, z, east), W);
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
            FaceIn(Put(storage, o, x + 3, VeinRowZ, 0f), W);
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
        FaceOut(Put(storage, o, 3, z0 + 2, 0f), E);
        Put(belt, o, 4, z0 + 2, 90f);
        Put(belt, o, 5, z0 + 2, 90f);
        FaceIn(Put(storage, o, 6, z0 + 2, 0f), W);
        Put(GameDatabase.FindBuilding("decor_bench"), o, 3, z0 + 4, 180f);
        Put(GameDatabase.FindBuilding("decor_street_lamp"), o, 8, z0 + 4, 0f);
        Put(GameDatabase.FindBuilding("decor_vending"), o, 9, z0 + 1, 270f);
        return z0 + 5;
    }

    /// <summary>Все декорации по категориям, лицом к дороге (−Z). Напольные — уже в демо плитки. Возвращает z линии заборов.</summary>
    static int PlaceDecorGrid(Vector2Int o, int z0)
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

        return fz;
    }

    // ---------- Таблички ----------

    /// <summary>
    /// Выставка табличек лицом к −Z, три ряда: все шаблоны; стойки, размеры и подсветка;
    /// приёмы (поворот, слои, иконки, символы, цвета, выравнивание, стили, односторонняя).
    /// </summary>
    static void PlaceSignGallery(Vector2Int o, int z0)
    {
        BuildingData data = GameDatabase.FindBuilding(DecorCatalog.SignId);
        if (data == null)
            return;

        int x = 2;
        PutSign(data, o, ref x, z0, Caption(L("ШАБЛОНЫ", "TEMPLATES")));
        for (int i = 0; i < SignPresets.All.Length; i++)
            PutSign(data, o, ref x, z0, SignPresets.All[i].make());

        int z1 = z0 + 4;
        x = 2;
        PutSign(data, o, ref x, z1, Caption(L("СТОЙКИ\nРАЗМЕРЫ", "STANDS\nSIZES")));
        string[] mounts = { L("Два столба", "Two posts"), L("Столб", "Pole"), L("Кронштейн", "Bracket"), L("Наклонная", "Tilted"), L("Постамент", "Plinth") };
        for (int m = 0; m < mounts.Length; m++)
        {
            var d = new SignData { mount = m, board = "1F8A8A", frameColor = "E3D3A8", postColor = "2A2D31" };
            d.items.Add(SignElement.Text(mounts[m], 0f, 0f, 0.22f, "FFFFFF", bold: true, shadow: true));
            PutSign(data, o, ref x, z1, d);
        }

        string[] sizes = { "S", "M", "L" };
        for (int k = 0; k < sizes.Length; k++)
        {
            var d = new SignData { size = k, board = "F2C230", frameColor = "0E0F11", postColor = "2A2D31" };
            d.items.Add(SignElement.Text(sizes[k], 0f, 0.02f, 0.6f, "0E0F11", bold: true));
            PutSign(data, o, ref x, z1, d);
        }

        var dim = new SignData { board = "1C2E5A", frameColor = "C8CCD0", postColor = "2A2D31" };
        dim.items.Add(SignElement.Text(L("БЕЗ\nПОДСВЕТКИ", "NO\nLIGHT"), 0f, 0f, 0.2f, "FFFFFF", bold: true));
        PutSign(data, o, ref x, z1, dim);
        var glow = new SignData { board = "1C2E5A", frameColor = "F2C230", postColor = "2A2D31", glow = true };
        glow.items.Add(SignElement.Text(L("С ПОДСВЕТКОЙ", "NIGHT\nLIGHT"), 0f, 0.05f, 0.2f, "F2C230", bold: true));
        glow.items.Add(SignElement.Symbol("☼", 0f, -0.3f, 0.22f, "F2C230"));
        PutSign(data, o, ref x, z1, glow);

        int z2 = z1 + 4;
        x = 2;
        PutSign(data, o, ref x, z2, Caption(L("ПРИЁМЫ", "TRICKS")));
        PutSign(data, o, ref x, z2, DemoRotation());
        PutSign(data, o, ref x, z2, DemoLayers());
        PutSign(data, o, ref x, z2, DemoIcons());
        PutSign(data, o, ref x, z2, DemoSymbols());
        PutSign(data, o, ref x, z2, DemoColors());
        PutSign(data, o, ref x, z2, DemoAlign());
        PutSign(data, o, ref x, z2, DemoStyles());
        PutSign(data, o, ref x, z2, DemoOneSided());
    }

    static void PutSign(BuildingData data, Vector2Int o, ref int x, int z, SignData sign)
    {
        if (Put(data, o, x, z, 180f) is Decoration d)
            d.SetSign(sign);
        x += 3;
    }

    static string L(string ru, string en)
    {
        return UiLocale.IsRu ? ru : en;
    }

    static SignData Caption(string text)
    {
        var d = new SignData { mount = (int)SignMount.Stand, size = 0, frame = 2, board = "2A2D31", frameColor = "F08A24", postColor = "0E0F11" };
        d.items.Add(SignElement.Text(text, 0f, 0f, 0.2f, "F08A24", bold: true));
        return d;
    }

    static SignData DemoRotation()
    {
        var d = new SignData { board = "E3D3A8", frameColor = "2A2D31", postColor = "2A2D31" };
        d.items.Add(SignElement.Text("0°", -0.32f, 0.22f, 0.2f, "0E0F11", bold: true));
        d.items.Add(SignElement.Text("45°", 0.02f, 0.16f, 0.2f, "D23A2E", bold: true, rot: 45f));
        d.items.Add(SignElement.Text("90°", 0.36f, 0f, 0.2f, "2F6FD6", bold: true, rot: 90f));
        d.items.Add(SignElement.Text("−30°", -0.22f, -0.24f, 0.2f, "2E9B4E", bold: true, rot: -30f));
        d.items.Add(SignElement.Text("180°", 0.12f, -0.28f, 0.17f, "7B4BC4", bold: true, rot: 180f));
        return d;
    }

    static SignData DemoLayers()
    {
        var d = new SignData { board = "FFFFFF", frameColor = "2A2D31", postColor = "2A2D31" };
        d.items.Add(SignElement.Plate(-0.17f, 0.08f, 0.4f, 0.55f, "D23A2E"));
        d.items.Add(SignElement.Plate(0f, 0f, 0.4f, 0.55f, "F2C230"));
        d.items.Add(SignElement.Plate(0.17f, -0.08f, 0.4f, 0.55f, "2F6FD6"));
        d.items.Add(SignElement.Text(L("СЛОИ", "LAYERS"), 0f, 0f, 0.22f, "FFFFFF", bold: true, shadow: true));
        return d;
    }

    static SignData DemoIcons()
    {
        var d = new SignData { board = "2A2D31", frameColor = "8CC63F", postColor = "2A2D31" };
        string[] ids = { "item:iron_ore", "item:cooper_ore", "item:coal_ore", "item:iron_plate", "item:circuit_board", "bld:smelter" };
        for (int i = 0; i < ids.Length; i++)
            d.items.Add(SignElement.Icon(ids[i], -0.3f + (i % 3) * 0.3f, 0.06f - (i / 3) * 0.36f, 0.32f));
        d.items.Add(SignElement.Text(L("ИКОНКИ", "ICONS"), 0f, 0.38f, 0.13f, "8CC63F", bold: true));
        return d;
    }

    static SignData DemoSymbols()
    {
        var d = new SignData { size = 2, board = "0E0F11", frameColor = "3EC1E0", postColor = "2A2D31" };
        string[] colors = { "F2C230", "3EC1E0", "E0559A", "8CC63F", "F08A24" };
        for (int i = 0; i < 20 && i < SignGlyphs.All.Length; i++)
        {
            int c = i % 5;
            int r = i / 5;
            d.items.Add(SignElement.Symbol(SignGlyphs.All[i], -0.4f + c * 0.2f, 0.33f - r * 0.22f, 0.2f, colors[(c + r) % colors.Length]));
        }

        return d;
    }

    static SignData DemoColors()
    {
        var d = new SignData { board = "2A2D31", frameColor = "FFFFFF", postColor = "2A2D31" };
        string word = L("РАДУГА", "COLORS");
        string[] colors = { "D23A2E", "F08A24", "F2C230", "8CC63F", "3EC1E0", "7B4BC4" };
        for (int i = 0; i < word.Length && i < colors.Length; i++)
            d.items.Add(SignElement.Text(word[i].ToString(), -0.36f + i * 0.145f, 0.1f, 0.3f, colors[i], bold: true));
        d.items.Add(SignElement.Text(L("любой цвет: #RRGGBB", "any color: #RRGGBB"), 0f, -0.26f, 0.12f, "C8CCD0", italic: true));
        return d;
    }

    static SignData DemoAlign()
    {
        var d = new SignData { size = 2, board = "E3D3A8", frameColor = "6B4A2B", postColor = "4A3424" };
        d.items.Add(SignElement.Text(L("влево\nстроки\nкраем", "left\naligned\nlines"), -0.44f, 0f, 0.13f, "0E0F11", align: 1));
        d.items.Add(SignElement.Text(L("по\nцентру\nстроки", "center\naligned\nlines"), 0f, 0f, 0.13f, "6E1F2A", align: 0));
        d.items.Add(SignElement.Text(L("вправо\nстроки\nкраем", "right\naligned\nlines"), 0.44f, 0f, 0.13f, "1C2E5A", align: 2));
        return d;
    }

    static SignData DemoStyles()
    {
        var d = new SignData { board = "FFFFFF", frameColor = "7A8088", postColor = "2A2D31" };
        d.items.Add(SignElement.Text(L("Обычный", "Regular"), 0f, 0.3f, 0.14f, "0E0F11"));
        d.items.Add(SignElement.Text(L("Жирный", "Bold"), 0f, 0.1f, 0.14f, "0E0F11", bold: true));
        d.items.Add(SignElement.Text(L("Курсив", "Italic"), 0f, -0.1f, 0.14f, "0E0F11", italic: true));
        d.items.Add(SignElement.Text(L("С тенью", "Shadow"), 0f, -0.3f, 0.14f, "F2C230", bold: true, shadow: true));
        return d;
    }

    static SignData DemoOneSided()
    {
        var d = new SignData { board = "6E1F2A", frameColor = "C9A24A", postColor = "2A2D31", twoSided = false };
        d.items.Add(SignElement.Text(L("ТОЛЬКО\nСПЕРЕДИ", "FRONT\nONLY"), 0f, 0.05f, 0.2f, "F2C230", bold: true));
        d.items.Add(SignElement.Text(L("сзади пусто", "back is blank"), 0f, -0.32f, 0.1f, "E3D3A8", italic: true));
        return d;
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

    static BuildingBase Put(BuildingData data, Vector2Int origin, int x, int z, float yaw)
    {
        return PutAt(data, origin + new Vector2Int(x, z), yaw);
    }

    static BuildingBase PutAt(BuildingData data, Vector2Int min, float yaw)
    {
        if (data == null || data.prefab == null)
            return null;
        Vector2Int size = GridFootprint.GetRotatedSize(data.size, yaw);
        bool free = DecorSystem.IsAreaFreeFor(data, min, size, null, null);
        if (!free && !data.allowOnWater)
            return null;
        Vector3 pos = GridFootprint.MinCellToCenter(min, size, GroundY(min, size));
        GameObject go = Object.Instantiate(data.prefab, pos, Quaternion.Euler(0f, yaw, 0f));
        BuildingBase building = go.GetComponent<BuildingBase>();
        if (building == null)
            return null;
        building.data = data;
        building.OnPlaced();
        return building;
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
        center -= new Vector2Int((PadW + HallsW) / 2, PadH / 2);

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
            for (int x = 0; x < PadW + HallsW; x += 2)
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
