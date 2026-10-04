using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>
/// Два огороженных цеха тестового двора ([[TestYard]]) восточнее основной площадки.
/// «Цех рецептов» — по стенду на каждый рецепт: склад/бак сырья → лента/труба → станок → склад результата,
/// перед стендом табличка (входы → выход, станок, время цикла, штук в минуту). Станки с улучшением
/// (сборщик) — отдельным рядом второго уровня. «Цех цепочек» — производство от сырья до готового:
/// шестерня, кабель, мотор, микросхема, батарея (побочные ветки подходят к станку сбоку).
/// Сырьё бесконечное: склады подачи пополняются, склады результата чистятся (<see cref="TestYardFeeder"/>).
/// Станки цехов не ломаются ночью. Клетки складов и границы цехов пишутся в сейв.
/// </summary>
public static partial class TestYard
{
    const string KeyFeeds = "testyard_feeds";
    const string KeySinks = "testyard_sinks";
    const string KeyHalls = "testyard_halls";
    /// <summary>Ширина цехов восточнее площадки (для поиска ровного места).</summary>
    const int HallsW = 100;
    const int MaxRowW = 34;

    static readonly Vector2Int N = new Vector2Int(0, 1);
    static readonly Vector2Int S = new Vector2Int(0, -1);
    static readonly Vector2Int E = new Vector2Int(1, 0);
    static readonly Vector2Int W = new Vector2Int(-1, 0);

    struct Feed
    {
        public Vector2Int cell;
        public string item;
    }

    static readonly List<Feed> feeds = new List<Feed>();
    static readonly List<Vector2Int> sinks = new List<Vector2Int>();
    static readonly List<RectInt> halls = new List<RectInt>();
    static TestYardFeeder feeder;

    // габариты того, что ставится сейчас (цех или цепочка)
    static bool tracking;
    static int bMinX, bMinZ, bMaxX, bMaxZ;

    // ---------- Вход ----------

    static void ClearHallState()
    {
        feeds.Clear();
        sinks.Clear();
        halls.Clear();
    }

    static void BuildDemoHalls(Vector2Int o)
    {
        ClearHallState();
        Vector2Int start = o + new Vector2Int(PadW + 6, 2);
        RectInt recipes = BuildRecipeHall(start);
        BuildChainHall(new Vector2Int(recipes.xMax + 6, start.y));
        EnsureFeeder();
    }

    /// <summary>Станки цехов не участвуют в ночных поломках ([[BreakdownSystem]]).</summary>
    public static bool InDemoHall(BuildingBase b)
    {
        if (b == null || halls.Count == 0 || WorldCatalog.Active == null || !WorldCatalog.Active.sandbox)
            return false;
        Vector2Int cell = BuildingLinker.WorldToCell(b.transform.position);
        for (int i = 0; i < halls.Count; i++)
        {
            if (halls[i].Contains(cell))
                return true;
        }

        return false;
    }

    // ---------- Цех рецептов ----------

    struct StandPlan
    {
        public RecipeData recipe;
        public BuildingData building;
        public int level;
        public int x;
        public int westPad;
        public bool caption;
        public string captionText;
        public string board;
        public string frame;
    }

    static RectInt BuildRecipeHall(Vector2Int start)
    {
        var groups = RecipeGroups();
        var rows = new List<List<StandPlan>>();
        var row = new List<StandPlan>();
        int x = 0;
        int total = 0;
        for (int g = 0; g < groups.Count; g++)
        {
            (BuildingData building, int level, List<RecipeData> list) = groups[g];
            int width = 3;
            for (int i = 0; i < list.Count; i++)
                width += SlotWidth(building, list[i]);
            if (row.Count > 0 && x + width > MaxRowW)
            {
                rows.Add(row);
                row = new List<StandPlan>();
                x = 0;
            }

            string board, frame;
            GroupColors(building, level, out board, out frame);
            string count = L(list.Count + " " + Plural(list.Count, "рецепт", "рецепта", "рецептов"), list.Count + " recipes");
            row.Add(new StandPlan
            {
                caption = true, x = x, board = board, frame = frame,
                captionText = building.Title.ToUpperInvariant() + (level >= 2 ? L("\nУР. 2 · ", "\nLV 2 · ") : "\n") + count
            });
            x += 3;
            for (int i = 0; i < list.Count; i++)
            {
                int pad = WestPad(list[i]);
                row.Add(new StandPlan { recipe = list[i], building = building, level = level, x = x, westPad = pad, board = board, frame = frame });
                x += SlotWidth(building, list[i]);
                total++;
            }
        }

        if (row.Count > 0)
            rows.Add(row);

        BeginTrack();
        int z = start.y + 4;
        for (int r = 0; r < rows.Count; r++)
        {
            int depth = 0;
            for (int i = 0; i < rows[r].Count; i++)
            {
                StandPlan p = rows[r][i];
                int sx = start.x + 2 + p.x;
                if (p.caption)
                {
                    PutSignAt(new Vector2Int(sx, z), RowCaption(p.captionText, p.board, p.frame));
                    continue;
                }

                depth = Mathf.Max(depth, 8 + p.building.size.y);
                PlaceStand(p, new Vector2Int(sx, z));
            }

            z += depth + 2;
        }

        RectInt hall = EndTrack(2);
        int gateX = hall.xMin + hall.width / 2 - 1;
        Fence(hall, gateX, 3);
        halls.Add(hall);

        var sign = new SignData { mount = (int)SignMount.Posts, size = 2, frame = 2, board = "2A2D31", frameColor = "F08A24", postColor = "0E0F11" };
        sign.items.Add(SignElement.Text(L("ЦЕХ РЕЦЕПТОВ", "RECIPE HALL"), 0f, 0.26f, 0.2f, "F08A24", bold: true, shadow: true));
        sign.items.Add(SignElement.Text(L("все " + total + " " + Plural(total, "рецепт", "рецепта", "рецептов") + " игры в работе", "all " + total + " recipes running"), 0f, 0.0f, 0.12f, "FFFFFF"));
        sign.items.Add(SignElement.Text(L("сырьё бесконечное · время — без генератора", "endless input · times without a generator"), 0f, -0.2f, 0.09f, "C8CCD0", italic: true));
        sign.items.Add(SignElement.Text(L("сборщик — 1 и 2 уровень", "assembler — level 1 and 2"), 0f, -0.36f, 0.09f, "C8CCD0", italic: true));
        PutSignAt(new Vector2Int(gateX + 5, hall.yMin - 3), sign);
        return hall;
    }

    /// <summary>Рецепты по станкам: плавильня, сборщик (и его 2-й уровень), конструктор, НПЗ, химзавод, прочие.</summary>
    static List<(BuildingData, int, List<RecipeData>)> RecipeGroups()
    {
        var byBuilding = new Dictionary<string, List<RecipeData>>();
        var order = new List<string>();
        RecipeData[] all = GameDatabase.AllRecipes();
        for (int i = 0; i < all.Length; i++)
        {
            RecipeData r = all[i];
            if (r == null || r.inputs == null || r.outputs == null || r.outputs.Count == 0 || r.outputs[0].item == null)
                continue;
            BuildingData b = BuildingFor(r);
            if (b == null || b.prefab == null)
                continue;
            if (!byBuilding.TryGetValue(b.id, out List<RecipeData> list))
            {
                list = new List<RecipeData>();
                byBuilding[b.id] = list;
                order.Add(b.id);
            }

            if (!list.Contains(r))
                list.Add(r);
        }

        string[] preferred = { "smelter", "assembler", "constructor", "refinery", "chemical_plant" };
        order.Sort((a, b) =>
        {
            int ia = System.Array.IndexOf(preferred, a);
            int ib = System.Array.IndexOf(preferred, b);
            if (ia < 0) ia = 100;
            if (ib < 0) ib = 100;
            return ia != ib ? ia.CompareTo(ib) : string.CompareOrdinal(a, b);
        });

        var tiers = new Dictionary<ItemData, int>();
        var result = new List<(BuildingData, int, List<RecipeData>)>();
        for (int i = 0; i < order.Count; i++)
        {
            List<RecipeData> list = byBuilding[order[i]];
            list.Sort((a, b) =>
            {
                int ta = Tier(a.outputs[0].item, tiers, 0);
                int tb = Tier(b.outputs[0].item, tiers, 0);
                return ta != tb ? ta.CompareTo(tb) : string.CompareOrdinal(a.Title, b.Title);
            });
            BuildingData data = GameDatabase.FindBuilding(order[i]);
            result.Add((data, 1, list));
            CrafterBuilding proto = data.prefab.GetComponent<CrafterBuilding>();
            if (proto != null && proto.CanUpgradeBuilding)
                result.Add((data, 2, list));
        }

        return result;
    }

    /// <summary>Глубина предмета в дереве рецептов: сырьё — 0.</summary>
    static int Tier(ItemData item, Dictionary<ItemData, int> memo, int depth)
    {
        if (item == null || depth > 12)
            return 0;
        if (memo.TryGetValue(item, out int t))
            return t;
        memo[item] = 0;
        RecipeData r = RecipeProducing(item);
        int best = 0;
        if (r != null && r.inputs != null)
        {
            for (int i = 0; i < r.inputs.Count; i++)
                best = Mathf.Max(best, Tier(r.inputs[i].item, memo, depth + 1) + 1);
        }

        memo[item] = best;
        return best;
    }

    static RecipeData RecipeProducing(ItemData item)
    {
        RecipeData[] all = GameDatabase.AllRecipes();
        for (int i = 0; i < all.Length; i++)
        {
            RecipeData r = all[i];
            if (r != null && r.outputs != null && r.outputs.Count > 0 && r.outputs[0].item == item)
                return r;
        }

        return null;
    }

    static RecipeData RecipeProducing(string itemId)
    {
        return RecipeProducing(GameDatabase.FindItem(itemId));
    }

    static BuildingData BuildingFor(RecipeData r)
    {
        if (r.allowedBuildingIds != null)
        {
            for (int i = 0; i < r.allowedBuildingIds.Count; i++)
            {
                string id = r.allowedBuildingIds[i];
                if (string.IsNullOrEmpty(id))
                    continue;
                BuildingData b = GameDatabase.FindBuilding(GameDatabase.Normalize(id));
                if (b != null)
                    return b;
            }
        }

        return r.requiredBuilding;
    }

    static bool HasFluidInput(RecipeData r, bool fluid)
    {
        for (int i = 0; i < r.inputs.Count; i++)
        {
            if (r.inputs[i].item != null && r.inputs[i].item.isFluid == fluid)
                return true;
        }

        return false;
    }

    /// <summary>Жидкость + твёрдое (химзавод): жидкость подходит слева, под неё 3 клетки.</summary>
    static int WestPad(RecipeData r)
    {
        return HasFluidInput(r, true) && HasFluidInput(r, false) ? 3 : 0;
    }

    static int SlotWidth(BuildingData b, RecipeData r)
    {
        return Mathf.Max(b.size.x + WestPad(r), 2) + 1;
    }

    static void GroupColors(BuildingData b, int level, out string board, out string frame)
    {
        switch (b.id)
        {
            case "smelter": board = "6E1F2A"; frame = "F08A24"; break;
            case "assembler":
                if (level >= 2) { board = "1C2E5A"; frame = "F2C230"; }
                else { board = "1F8A8A"; frame = "E3D3A8"; }
                break;
            case "constructor": board = "2B3A2E"; frame = "8CC63F"; break;
            case "refinery": board = "2A2D31"; frame = "3EC1E0"; break;
            case "chemical_plant": board = "2A2D31"; frame = "E0559A"; break;
            default: board = "2A2D31"; frame = "C8CCD0"; break;
        }
    }

    /// <summary>
    /// Стенд (снизу вверх): табличка, пусто, склад сырья, две ленты, станок, две ленты, склад результата.
    /// Жидкость к химзаводу — слева трубой из бака.
    /// </summary>
    static void PlaceStand(StandPlan p, Vector2Int slot)
    {
        RecipeData r = p.recipe;
        Vector2Int min = new Vector2Int(slot.x + p.westPad, slot.y + 5);
        CrafterBuilding c = Machine(p.building, min, r, p.level);
        if (c == null)
            return;

        var used = new HashSet<BuildingSocket>();
        for (int i = 0; i < r.inputs.Count; i++)
        {
            ItemData item = r.inputs[i].item;
            BuildingSocket s = SocketFor(c, item, used);
            if (s == null)
            {
                Debug.LogWarning("[TestYard] Нет входа под " + (item != null ? item.id : "?") + " у " + p.building.id);
                continue;
            }

            used.Add(s);
            Vector2Int f = BuildingLinker.GetSocketFrontCell(s);
            Vector2Int d = BuildingLinker.SocketWorldCardinal(s);
            Line(new List<Vector2Int> { f + d, f }, -d, item.isFluid);
            Source(item, f + d * 2, -d);
        }

        Vector2Int o = OutFront(c, out Vector2Int od);
        bool fluidOut = r.outputs[0].item.isFluid;
        Line(new List<Vector2Int> { o, o + od }, od, fluidOut);
        Sink(o + od * 2, od, fluidOut);

        int signX = min.x + (p.building.size.x - 1) / 2;
        PutSignAt(new Vector2Int(signX, slot.y), StandSign(r, c, p.level, p.board, p.frame));
    }

    static SignData StandSign(RecipeData r, CrafterBuilding c, int level, string board, string frame)
    {
        var d = new SignData { mount = (int)SignMount.Plaque, size = 1, board = board, frameColor = frame, postColor = "2A2D31" };
        ItemData outItem = r.outputs[0].item;
        d.items.Add(SignElement.Text(outItem.Title, 0f, 0.34f, 0.15f, "FFFFFF", bold: true, shadow: true));

        int slots = r.inputs.Count + 1 + r.outputs.Count;
        float step = Mathf.Min(0.28f, 0.92f / slots);
        float icon = Mathf.Min(0.32f, step * 1.1f);
        float x = -(slots - 1) * step * 0.5f;
        for (int i = 0; i < r.inputs.Count; i++, x += step)
            IconWithAmount(d, r.inputs[i], x, icon);
        d.items.Add(SignElement.Symbol("→", x, 0.02f, icon * 0.75f, frame));
        x += step;
        for (int i = 0; i < r.outputs.Count; i++, x += step)
            IconWithAmount(d, r.outputs[i], x, icon);

        float time = c != null ? c.GetEffectiveCraftTime() : Economy.CraftNeed(r);
        float perMin = time > 0.001f ? 60f / time * r.outputs[0].amount : 0f;
        string title = c != null && c.data != null ? c.data.Title : "";
        if (level >= 2)
            title += L(" · ур. 2", " · lv 2");
        string line = title + "  ·  " + Num(time) + L(" с", " s") + "  ·  " + Num(perMin) + L("/мин", "/min");
        d.items.Add(SignElement.Text(line, 0f, -0.36f, 0.095f, "E3D3A8"));
        return d;
    }

    static void IconWithAmount(SignData d, ItemStack stack, float x, float size)
    {
        if (stack.item == null)
            return;
        d.items.Add(SignElement.Icon("item:" + stack.item.id, x, 0.04f, size));
        d.items.Add(SignElement.Text("×" + stack.amount, x + size * 0.28f, 0.04f - size * 0.42f, 0.1f, "FFFFFF", bold: true, shadow: true));
    }

    static SignData RowCaption(string text, string board, string frame)
    {
        var d = new SignData { mount = (int)SignMount.Stand, size = 0, frame = 2, board = board, frameColor = frame, postColor = "0E0F11" };
        d.items.Add(SignElement.Text(text, 0f, 0f, 0.15f, "FFFFFF", bold: true, shadow: true));
        return d;
    }

    // ---------- Цех цепочек ----------

    sealed class Stage
    {
        public string output;
        public int level = 1;
        public Chain side;
    }

    sealed class Chain
    {
        public string title;
        public string raw;
        public Stage[] stages;
    }

    static Stage St(string output, Chain side = null, int level = 1)
    {
        return new Stage { output = output, side = side, level = level };
    }

    static Chain[] Chains()
    {
        Chain wire = new Chain { raw = "cooper_ore", stages = new[] { St("cooper_Ingot"), St("wire") } };
        return new[]
        {
            // батарея первой: её кислота подходит слева, ветка уходит на запад
            new Chain { title = L("БАТАРЕЯ", "BATTERY"), raw = "iron_ore", stages = new[] { St("iron_ingot"), St("steel_ingot"),
                St("battery", new Chain { raw = "sulfur", stages = new[] { St("sulfuric_acid") } }) } },
            new Chain { title = L("ШЕСТЕРНЯ", "GEAR"), raw = "iron_ore", stages = new[] { St("iron_ingot"), St("iron_rod"), St("gear", level: 2) } },
            new Chain { title = L("КАБЕЛЬ", "CABLE"), raw = "cooper_ore", stages = new[] { St("cooper_Ingot"), St("wire"),
                St("cable", new Chain { raw = "crude_oil", stages = new[] { St("rubber") } }) } },
            new Chain { title = L("МОТОР", "MOTOR"), raw = "iron_ore", stages = new[] { St("iron_ingot"), St("steel_ingot"), St("steel_rod"),
                St("motor", wire) } },
            new Chain { title = L("МИКРОСХЕМА", "COMPUTER CHIP"), raw = "sand", stages = new[] { St("silicon"),
                St("circuit_board", new Chain { raw = "cooper_ore", stages = new[] { St("cooper_Ingot"), St("wire") } }),
                St("computer_chip", new Chain { raw = "crude_oil", stages = new[] { St("plastic") } }) } },
        };
    }

    static void BuildChainHall(Vector2Int start)
    {
        Chain[] chains = Chains();
        int hallMinX = int.MaxValue, hallMinZ = int.MaxValue, hallMaxX = int.MinValue, hallMaxZ = int.MinValue;
        int x = start.x + 8;
        int baseZ = start.y + 8;
        for (int i = 0; i < chains.Length; i++)
        {
            BeginTrack();
            BuildLine(chains[i], new Vector2Int(x, baseZ), true, out _, out _);
            RectInt box = EndTrack(0);
            PutSignAt(new Vector2Int(x - 1, Mathf.Min(box.yMin, baseZ) - 3), ChainSign(chains[i]));
            hallMinX = Mathf.Min(hallMinX, box.xMin);
            hallMinZ = Mathf.Min(hallMinZ, box.yMin - 3);
            hallMaxX = Mathf.Max(hallMaxX, box.xMax);
            hallMaxZ = Mathf.Max(hallMaxZ, box.yMax);
            x = box.xMax + 4;
        }

        if (hallMinX == int.MaxValue)
            return;
        var hall = new RectInt(hallMinX - 2, hallMinZ - 2, hallMaxX - hallMinX + 4, hallMaxZ - hallMinZ + 4);
        int gateX = hall.xMin + hall.width / 2 - 1;
        Fence(hall, gateX, 3);
        halls.Add(hall);

        var sign = new SignData { mount = (int)SignMount.Posts, size = 2, frame = 2, board = "1C2E5A", frameColor = "8CC63F", postColor = "0E0F11" };
        sign.items.Add(SignElement.Text(L("ЦЕХ ЦЕПОЧЕК", "CHAIN HALL"), 0f, 0.24f, 0.2f, "8CC63F", bold: true, shadow: true));
        sign.items.Add(SignElement.Text(L("от сырья до готового:\nкаждый станок кормит следующий", "raw to finished:\neach machine feeds the next"), 0f, -0.08f, 0.11f, "FFFFFF"));
        sign.items.Add(SignElement.Text(L("побочные ветки подходят сбоку", "side branches join from the side"), 0f, -0.36f, 0.09f, "C8CCD0", italic: true));
        PutSignAt(new Vector2Int(gateX + 5, hall.yMin - 3), sign);
    }

    /// <summary>
    /// Линия снизу вверх: сырьё в <paramref name="baseCell"/>, дальше этапы через две ленты.
    /// <paramref name="sinkAtEnd"/> — в конце склад результата; иначе возвращает клетку, куда выдаёт последний станок.
    /// </summary>
    static bool BuildLine(Chain chain, Vector2Int baseCell, bool sinkAtEnd, out Vector2Int outCell, out ItemData outItem)
    {
        outItem = GameDatabase.FindItem(chain.raw);
        outCell = baseCell + N;
        if (outItem == null)
        {
            Debug.LogWarning("[TestYard] Нет сырья " + chain.raw);
            return false;
        }

        Source(outItem, baseCell, N);
        Vector2Int cur = baseCell + N;
        for (int i = 0; i < chain.stages.Length; i++)
        {
            Stage st = chain.stages[i];
            RecipeData r = RecipeProducing(st.output);
            BuildingData b = r != null ? BuildingFor(r) : null;
            if (b == null)
            {
                Debug.LogWarning("[TestYard] Нет рецепта/станка для " + st.output);
                return false;
            }

            Vector2Int f = cur + N;
            Line(new List<Vector2Int> { cur, f }, N, outItem.isFluid);
            CrafterBuilding c = PlaceAligned(b, r, st.level, outItem, f, out BuildingSocket mainSocket);
            if (c == null)
                return false;

            if (st.side != null)
                BuildSide(c, st.side, mainSocket);

            cur = OutFront(c, out _);
            outItem = r.outputs[0].item;
        }

        outCell = cur;
        if (sinkAtEnd)
        {
            Line(new List<Vector2Int> { cur, cur + N }, N, outItem.isFluid);
            Sink(cur + N * 2, N, outItem.isFluid);
        }

        return true;
    }

    /// <summary>Побочная ветка: своя колонка восточнее (или западнее) всего поставленного, выход — Г-образно в свободный вход.</summary>
    static void BuildSide(CrafterBuilding c, Chain side, BuildingSocket mainSocket)
    {
        ItemData sideItem = SideOutput(side);
        var used = new HashSet<BuildingSocket> { mainSocket };
        BuildingSocket s = SocketFor(c, sideItem, used);
        if (s == null)
        {
            Debug.LogWarning("[TestYard] Нет второго входа у " + c.data.id);
            return;
        }

        Vector2Int f = BuildingLinker.GetSocketFrontCell(s);
        Vector2Int d = BuildingLinker.SocketWorldCardinal(s);
        int left = 0, right = 0, height = 0;
        for (int i = 0; i < side.stages.Length; i++)
        {
            RecipeData r = RecipeProducing(side.stages[i].output);
            BuildingData b = r != null ? BuildingFor(r) : null;
            Vector2Int size = b != null ? b.size : Vector2Int.one;
            left = Mathf.Max(left, (size.x - 1) / 2);
            right = Mathf.Max(right, size.x - 1 - (size.x - 1) / 2);
            height += size.y + 2;
        }

        bool west = d == W;
        int colX = west ? bMinX - 2 - right : bMaxX + 2 + left;
        // выход ветки — на ряд ниже клетки входа, оттуда вверх и вбок
        int baseZ = f.y - 2 - height;
        if (!BuildLine(side, new Vector2Int(colX, baseZ), false, out Vector2Int start, out ItemData item))
            return;
        Line(RouteL(start, f), -d, item.isFluid);
    }

    static ItemData SideOutput(Chain side)
    {
        if (side.stages.Length == 0)
            return GameDatabase.FindItem(side.raw);
        RecipeData r = RecipeProducing(side.stages[side.stages.Length - 1].output);
        return r != null ? r.outputs[0].item : null;
    }

    /// <summary>Путь от клетки до цели: сначала вверх до её ряда, потом вбок.</summary>
    static List<Vector2Int> RouteL(Vector2Int from, Vector2Int to)
    {
        var cells = new List<Vector2Int> { from };
        Vector2Int cur = from;
        while (cur.y < to.y)
        {
            cur += N;
            cells.Add(cur);
        }

        Vector2Int step = to.x > cur.x ? E : W;
        while (cur.x != to.x)
        {
            cur += step;
            cells.Add(cur);
        }

        return cells;
    }

    static SignData ChainSign(Chain chain)
    {
        var d = new SignData { mount = (int)SignMount.Plaque, size = 2, frame = 2, board = "2B3A2E", frameColor = "8CC63F", postColor = "2A2D31" };
        d.items.Add(SignElement.Text(L("ЦЕПОЧКА · ", "CHAIN · ") + chain.title, 0f, 0.36f, 0.12f, "8CC63F", bold: true, shadow: true));
        var path = new List<string> { chain.raw };
        for (int i = 0; i < chain.stages.Length; i++)
            path.Add(chain.stages[i].output);
        int slots = path.Count * 2 - 1;
        float step = Mathf.Min(0.16f, 0.94f / slots);
        float x = -(slots - 1) * step * 0.5f;
        for (int i = 0; i < path.Count; i++)
        {
            d.items.Add(SignElement.Icon("item:" + path[i], x, 0.08f, Mathf.Min(0.26f, step * 1.7f)));
            x += step;
            if (i < path.Count - 1)
            {
                d.items.Add(SignElement.Symbol("→", x, 0.08f, 0.12f, "C8CCD0"));
                x += step;
            }
        }

        var sides = new StringBuilder();
        for (int i = 0; i < chain.stages.Length; i++)
        {
            Chain side = chain.stages[i].side;
            if (side == null)
                continue;
            if (sides.Length > 0)
                sides.Append('\n');
            sides.Append("+ ").Append(ItemTitle(SideOutput(side))).Append(": ").Append(ItemTitle(GameDatabase.FindItem(side.raw)));
            for (int k = 0; k < side.stages.Length; k++)
                sides.Append(" → ").Append(ItemTitle(GameDatabase.FindItem(side.stages[k].output)));
        }

        if (sides.Length > 0)
            d.items.Add(SignElement.Text(sides.ToString(), 0f, -0.28f, 0.085f, "E3D3A8"));
        for (int i = 0; i < chain.stages.Length; i++)
        {
            if (chain.stages[i].level >= 2)
            {
                d.items.Add(SignElement.Text(L("сборщик ур. 2", "assembler lv 2"), 0.32f, -0.4f, 0.075f, "F2C230", italic: true));
                break;
            }
        }

        return d;
    }

    static string ItemTitle(ItemData item)
    {
        return item != null ? item.Title : "?";
    }

    // ---------- Установка ----------

    static float Yaw(Vector2Int d)
    {
        if (d == E) return 90f;
        if (d == S) return 180f;
        if (d == W) return 270f;
        return 0f;
    }

    static BuildingBase HallPut(BuildingData data, Vector2Int min, float yaw)
    {
        if (data == null)
            return null;
        BuildingBase b = PutAt(data, min, yaw);
        if (b == null)
        {
            Debug.LogWarning("[TestYard] Занято: " + data.id + " в " + min);
            return null;
        }

        if (tracking)
        {
            Vector2Int size = GridFootprint.GetRotatedSize(data.size, yaw);
            bMinX = Mathf.Min(bMinX, min.x);
            bMinZ = Mathf.Min(bMinZ, min.y);
            bMaxX = Mathf.Max(bMaxX, min.x + size.x - 1);
            bMaxZ = Mathf.Max(bMaxZ, min.y + size.y - 1);
        }

        return b;
    }

    static void BeginTrack()
    {
        tracking = true;
        bMinX = bMinZ = int.MaxValue;
        bMaxX = bMaxZ = int.MinValue;
    }

    /// <summary>Габарит поставленного (клетки включительно) с отступом.</summary>
    static RectInt EndTrack(int margin)
    {
        tracking = false;
        if (bMinX == int.MaxValue)
            return new RectInt(0, 0, 0, 0);
        return new RectInt(bMinX - margin, bMinZ - margin, bMaxX - bMinX + 1 + margin * 2, bMaxZ - bMinZ + 1 + margin * 2);
    }

    static CrafterBuilding Machine(BuildingData data, Vector2Int min, RecipeData recipe, int level)
    {
        CrafterBuilding c = HallPut(data, min, 0f) as CrafterBuilding;
        if (c == null)
            return null;
        if (level >= 2)
            c.ApplyLevel(2);
        c.SetRecipe(recipe);
        // НПЗ с жидким выходом меняет вход и выход местами — разворачиваем, чтобы вход смотрел на юг
        if (c.inputSockets != null && c.inputSockets.Length > 0 && c.inputSockets[0] != null
            && BuildingLinker.SocketWorldCardinal(c.inputSockets[0]) == N)
        {
            c.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            c.ReRegisterOnGrid();
            c.OnRotated();
        }

        return c;
    }

    /// <summary>Станок так, чтобы клетка перед входом под <paramref name="mainItem"/> совпала с <paramref name="front"/>.</summary>
    static CrafterBuilding PlaceAligned(BuildingData data, RecipeData recipe, int level, ItemData mainItem, Vector2Int front, out BuildingSocket socket)
    {
        socket = null;
        Vector2Int guess = new Vector2Int(front.x - (data.size.x - 1) / 2, front.y + 1);
        CrafterBuilding c = Machine(data, guess, recipe, level);
        if (c == null)
            return null;
        socket = SocketFor(c, mainItem, new HashSet<BuildingSocket>());
        if (socket == null)
            return c;
        Vector2Int got = BuildingLinker.GetSocketFrontCell(socket);
        if (got != front)
        {
            Vector2Int delta = front - got;
            c.transform.position += new Vector3(delta.x, 0f, delta.y) * GridFootprint.CellSize;
            c.ReRegisterOnGrid();
            if (tracking)
            {
                bMinX = Mathf.Min(bMinX, guess.x + delta.x);
                bMaxX = Mathf.Max(bMaxX, guess.x + delta.x + data.size.x - 1);
                bMaxZ = Mathf.Max(bMaxZ, guess.y + delta.y + data.size.y - 1);
            }
        }

        return c;
    }

    static BuildingSocket SocketFor(CrafterBuilding c, ItemData item, HashSet<BuildingSocket> used)
    {
        if (c.inputSockets == null || item == null)
            return null;
        BuildingSocket fallback = null;
        for (int i = 0; i < c.inputSockets.Length; i++)
        {
            BuildingSocket s = c.inputSockets[i];
            if (s == null || used.Contains(s))
                continue;
            if (IsFluidSocket(c, s) == item.isFluid)
                return s;
            if (fallback == null)
                fallback = s;
        }

        return fallback;
    }

    static bool IsFluidSocket(CrafterBuilding c, BuildingSocket s)
    {
        if (c is Refinery refinery)
            return refinery.IsPipeSocket(s);
        return s.name.IndexOf("Fluid", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static Vector2Int OutFront(CrafterBuilding c, out Vector2Int dir)
    {
        BuildingSocket s = c.outputSockets != null && c.outputSockets.Length > 0 ? c.outputSockets[0] : null;
        if (s == null)
        {
            dir = N;
            return BuildingLinker.WorldToCell(c.transform.position) + N;
        }

        dir = BuildingLinker.SocketWorldCardinal(s);
        return BuildingLinker.GetSocketFrontCell(s);
    }

    /// <summary>Лента или труба по клеткам; каждая смотрит на следующую, последняя — в <paramref name="lastDir"/>.</summary>
    static void Line(List<Vector2Int> cells, Vector2Int lastDir, bool fluid)
    {
        BuildingData data = GameDatabase.FindBuilding(fluid ? "pipe" : "conveyor");
        for (int i = 0; i < cells.Count; i++)
        {
            Vector2Int dir = i + 1 < cells.Count ? cells[i + 1] - cells[i] : lastDir;
            HallPut(data, cells[i], Yaw(dir));
        }
    }

    static void Source(ItemData item, Vector2Int cell, Vector2Int push)
    {
        BuildingData data = GameDatabase.FindBuilding(item.isFluid ? "fluid_storage_tank" : "storage_container");
        if (HallPut(data, cell, Yaw(push)) is StorageContainer st)
        {
            FaceOut(st, push);
            st.DevFill(item);
            feeds.Add(new Feed { cell = cell, item = item.id });
        }
    }

    static void Sink(Vector2Int cell, Vector2Int flow, bool fluid)
    {
        BuildingData data = GameDatabase.FindBuilding(fluid ? "fluid_storage_tank" : "storage_container");
        BuildingBase b = HallPut(data, cell, Yaw(flow));
        if (b != null)
        {
            FaceIn(b, -flow);
            sinks.Add(cell);
        }
    }

    /// <summary>Выход склада/бака смотрит в <paramref name="dir"/>. У склада в префабе вход спереди, у бака — своя раскладка, поэтому по факту сокета.</summary>
    static void FaceOut(BuildingBase b, Vector2Int dir)
    {
        if (b != null)
            TurnSocket(b, b.outputSockets, dir);
    }

    /// <summary>Вход склада/бака смотрит в <paramref name="dir"/> (туда, откуда приходит лента).</summary>
    static void FaceIn(BuildingBase b, Vector2Int dir)
    {
        if (b != null)
            TurnSocket(b, b.inputSockets, dir);
    }

    static void TurnSocket(BuildingBase b, BuildingSocket[] sockets, Vector2Int dir)
    {
        if (sockets == null || sockets.Length == 0 || sockets[0] == null)
            return;
        Vector2Int got = BuildingLinker.SocketWorldCardinal(sockets[0]);
        if (got == dir || got == Vector2Int.zero)
            return;
        b.transform.rotation = Quaternion.Euler(0f, b.transform.eulerAngles.y + Yaw(dir) - Yaw(got), 0f);
        b.ReRegisterOnGrid();
        b.OnRotated();
    }

    static void PutSignAt(Vector2Int min, SignData sign)
    {
        BuildingData data = GameDatabase.FindBuilding(DecorCatalog.SignId);
        if (HallPut(data, min, 180f) is Decoration d)
            d.SetSign(sign);
    }

    /// <summary>Забор по периметру с проходом в южной стороне и фонарями у входа.</summary>
    static void Fence(RectInt r, int gateX, int gateW)
    {
        BuildingData fence = GameDatabase.FindBuilding("decor_fence");
        BuildingData lamp = GameDatabase.FindBuilding("decor_street_lamp");
        int x0 = r.xMin, x1 = r.xMax - 1, z0 = r.yMin, z1 = r.yMax - 1;
        for (int x = x0; x <= x1; x++)
        {
            if (x < gateX || x >= gateX + gateW)
                PutAt(fence, new Vector2Int(x, z0), 90f);
            PutAt(fence, new Vector2Int(x, z1), 90f);
        }

        for (int z = z0 + 1; z < z1; z++)
        {
            PutAt(fence, new Vector2Int(x0, z), 0f);
            PutAt(fence, new Vector2Int(x1, z), 0f);
        }

        PutAt(lamp, new Vector2Int(gateX - 1, z0 - 1), 180f);
        PutAt(lamp, new Vector2Int(gateX + gateW, z0 - 1), 180f);
        PutAt(lamp, new Vector2Int(x0 + 1, z1 - 1), 0f);
        PutAt(lamp, new Vector2Int(x1 - 1, z1 - 1), 0f);
    }

    static string Num(float v)
    {
        return v >= 10f ? Mathf.RoundToInt(v).ToString(CultureInfo.InvariantCulture) : v.ToString("0.##", CultureInfo.InvariantCulture);
    }

    static string Plural(int n, string one, string few, string many)
    {
        int m10 = n % 10, m100 = n % 100;
        if (m10 == 1 && m100 != 11)
            return one;
        if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20))
            return few;
        return many;
    }

    // ---------- Бесконечное сырьё ----------

    static void EnsureFeeder()
    {
        if (feeder != null)
            return;
        feeder = new GameObject("TestYardFeeder").AddComponent<TestYardFeeder>();
    }

    /// <summary>Раз в пару секунд: склады подачи — снова полные, склады результата — пустые.</summary>
    public static void TickFeeds()
    {
        if (WorldCatalog.Active == null || !WorldCatalog.Active.sandbox)
            return;
        for (int i = 0; i < feeds.Count; i++)
        {
            if (!(BuildingLinker.GetBuildingAt(feeds[i].cell) is StorageContainer st))
                continue;
            ItemData item = GameDatabase.FindItem(feeds[i].item);
            if (item == null)
                continue;
            if (Amount(st) < Mathf.Max(10, item.maxStack))
                st.DevFill(item);
        }

        for (int i = 0; i < sinks.Count; i++)
        {
            if (BuildingLinker.GetBuildingAt(sinks[i]) is StorageContainer st && Amount(st) >= 20)
                st.DevClear();
        }
    }

    static int Amount(StorageContainer st)
    {
        int n = 0;
        IReadOnlyList<ItemStack> slots = st.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
                n += slots[i].amount;
        }

        return n;
    }

    // ---------- Сейв ----------

    static void CaptureHalls(SaveData data)
    {
        var inv = CultureInfo.InvariantCulture;
        var f = new StringBuilder();
        for (int i = 0; i < feeds.Count; i++)
            f.Append(feeds[i].cell.x.ToString(inv)).Append(',').Append(feeds[i].cell.y.ToString(inv)).Append(',').Append(feeds[i].item).Append(';');
        var s = new StringBuilder();
        for (int i = 0; i < sinks.Count; i++)
            s.Append(sinks[i].x.ToString(inv)).Append(',').Append(sinks[i].y.ToString(inv)).Append(';');
        var h = new StringBuilder();
        for (int i = 0; i < halls.Count; i++)
            h.Append(halls[i].x.ToString(inv)).Append(',').Append(halls[i].y.ToString(inv)).Append(',')
                .Append(halls[i].width.ToString(inv)).Append(',').Append(halls[i].height.ToString(inv)).Append(';');
        data.extras.Add(new SaveKeyValue { key = KeyFeeds, value = f.ToString() });
        data.extras.Add(new SaveKeyValue { key = KeySinks, value = s.ToString() });
        data.extras.Add(new SaveKeyValue { key = KeyHalls, value = h.ToString() });
    }

    static void RestoreHalls(SaveData data)
    {
        ClearHallState();
        foreach (string[] p in Rows(ReadString(data, KeyFeeds)))
        {
            if (p.Length >= 3 && TryInt(p[0], out int x) && TryInt(p[1], out int z))
                feeds.Add(new Feed { cell = new Vector2Int(x, z), item = p[2] });
        }

        foreach (string[] p in Rows(ReadString(data, KeySinks)))
        {
            if (p.Length >= 2 && TryInt(p[0], out int x) && TryInt(p[1], out int z))
                sinks.Add(new Vector2Int(x, z));
        }

        foreach (string[] p in Rows(ReadString(data, KeyHalls)))
        {
            if (p.Length >= 4 && TryInt(p[0], out int x) && TryInt(p[1], out int z) && TryInt(p[2], out int w) && TryInt(p[3], out int h))
                halls.Add(new RectInt(x, z, w, h));
        }

        if (feeds.Count > 0 || sinks.Count > 0)
            EnsureFeeder();
    }

    static IEnumerable<string[]> Rows(string value)
    {
        if (string.IsNullOrEmpty(value))
            yield break;
        string[] rows = value.Split(';');
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i].Length > 0)
                yield return rows[i].Split(',');
        }
    }

    static bool TryInt(string s, out int v)
    {
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
    }

    static string ReadString(SaveData data, string key)
    {
        if (data == null || data.extras == null)
            return null;
        for (int i = 0; i < data.extras.Count; i++)
        {
            SaveKeyValue row = data.extras[i];
            if (row != null && row.key == key)
                return row.value;
        }

        return null;
    }
}

/// <summary>Тикает бесконечное сырьё цехов тестового двора (<see cref="TestYard.TickFeeds"/>).</summary>
public class TestYardFeeder : MonoBehaviour
{
    float next;

    void Update()
    {
        if (Time.time < next)
            return;
        next = Time.time + 1.5f;
        TestYard.TickFeeds();
    }
}
