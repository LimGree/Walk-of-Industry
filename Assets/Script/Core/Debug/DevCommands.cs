using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public static class DevCommands
{
    static readonly string[] Catalog =
    {
        "/help",
        "/help research",
        "/money add ",
        "/money remove ",
        "/ruby add ",
        "/ruby remove ",
        "/research list",
        "/research skip ",
        "/research skipall",
        "/tutorial restart",
        "/tutorial skip",
        "/unlock recipe ",
        "/unlock recipe all",
        "/unlock build ",
        "/unlock build all",
        "/tp biome ",
        "/tp cluster ",
        "/tp veins ",
        "/time set morning",
        "/time set day",
        "/time set evening",
        "/time set night",
        "/time set midnight",
        "/time set ",
        "/timeskip ",
        "/timeskip 0:01:00",
        "/timeskip 1:00:00",
        "/stat cluster",
        "/stat veins",
        "/stat biome",
        "/locate biome",
        "/locate cluster",
        "/locate veins",
        "/regenWorldMap",
        "/belts",
        "/conveer speed ",
        "/conveyor speed ",
        "/belt speed ",
        "/clearcargo",
        "/killitems",
        "/dump cell",
        "/save",
        "/load",
        "/godsave",
        "/fps",
        "/log on belts",
        "/log off belts",
        "/spawn vein ",
        "/spawn builder "
    };

    public static void Suggest(string raw, List<string> into)
    {
        into.Clear();
        string q = Normalize(raw);
        if (q.Length == 0)
            q = "/";
        for (int i = 0; i < Catalog.Length; i++)
        {
            if (Catalog[i].StartsWith(q, true, CultureInfo.InvariantCulture))
                AddUnique(into, Catalog[i]);
        }

        AddPrefixed(into, q, "/spawn builder ", BuildingIds());
        AddPrefixed(into, q, "/unlock build ", BuildingIds());
        AddPrefixed(into, q, "/unlock recipe ", RecipeIds());
        AddPrefixed(into, q, "/research skip ", ResearchIds());
        AddPrefixed(into, q, "/spawn vein ", VeinIds());
        AddPrefixed(into, q, "/tp veins ", VeinIds());
        AddPrefixed(into, q, "/locate veins ", VeinIds());
        AddPrefixed(into, q, "/tp biome ", BiomeIds());
        AddPrefixed(into, q, "/locate biome ", BiomeIds());
        AddPrefixed(into, q, "/tp cluster ", ClusterIds());
        AddPrefixed(into, q, "/locate cluster ", ClusterIds());
    }

    static void AddPrefixed(List<string> into, string query, string prefix, IList<string> ids)
    {
        if (ids == null || string.IsNullOrEmpty(query) || string.IsNullOrEmpty(prefix))
            return;
        string stem = prefix.TrimEnd();
        if (!query.StartsWith(stem, true, CultureInfo.InvariantCulture))
            return;

        for (int i = 0; i < ids.Count; i++)
        {
            if (string.IsNullOrEmpty(ids[i]))
                continue;
            string line = prefix + ids[i];
            if (line.StartsWith(query, true, CultureInfo.InvariantCulture))
                AddUnique(into, line);
        }
    }

    static void AddUnique(List<string> into, string line)
    {
        if (string.IsNullOrEmpty(line) || into.Contains(line))
            return;
        into.Add(line);
    }

    static List<string> BuildingIds()
    {
        var list = new List<string>();
        BuildingData[] all = GameDatabase.AllBuildings();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !string.IsNullOrEmpty(all[i].id))
                list.Add(all[i].id);
        }

        return list;
    }

    static List<string> RecipeIds()
    {
        var list = new List<string>();
        RecipeData[] all = GameDatabase.AllRecipes();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !string.IsNullOrEmpty(all[i].id))
                list.Add(all[i].id);
        }

        return list;
    }

    static List<string> ResearchIds()
    {
        var list = new List<string>();
        ResearchNodeData[] all = GameDatabase.AllResearches();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !string.IsNullOrEmpty(all[i].id))
                list.Add(all[i].id);
        }

        return list;
    }

    static readonly string[] VeinNames =
    {
        "iron", "copper", "cooper", "coal", "stone", "sand", "sulfur", "tree", "log"
    };

    static readonly string[] BiomeNames =
    {
        "field", "woodland", "forest", "beach", "lake", "ocean", "peak", "slope", "mountain"
    };

    static IList<string> VeinIds()
    {
        return VeinNames;
    }

    static IList<string> BiomeIds()
    {
        return BiomeNames;
    }

    static IList<string> ClusterIds()
    {
        WorldResourceScatterer s = WorldResourceScatterer.Instance;
        if (s == null || s.ClusterKindKeys == null || s.ClusterKindKeys.Count == 0)
            return VeinNames;
        var list = new List<string>();
        for (int i = 0; i < s.ClusterKindKeys.Count; i++)
            AddUnique(list, s.ClusterKindKeys[i]);
        return list.Count > 0 ? list : VeinNames;
    }

    public static string Run(string raw)
    {
        string line = Normalize(raw);
        if (line.StartsWith("/"))
            line = line.Substring(1).Trim();
        if (line.Length == 0)
            return "";

        string[] p = Split(line);
        string a0 = p[0].ToLowerInvariant();
        string a1 = p.Length > 1 ? p[1].ToLowerInvariant() : "";
        string a2 = p.Length > 2 ? p[2] : "";

        try
        {
            if (a0 == "help")
                return a1 == "research" ? HelpResearch() : Help();
            if (a0 == "money")
                return Money(a1, a2);
            if (a0 == "ruby")
                return Ruby(a1, a2);
            if (a0 == "research")
                return Research(a1, a2);
            if (a0 == "tutorial")
                return Tutorial(a1);
            if (a0 == "unlock")
                return Unlock(a1, a2);
            if (a0 == "tp")
                return Tp(a1, a2);
            if (a0 == "time")
                return TimeSet(a1, a2);
            if (a0 == "timeskip")
                return Timeskip(p);
            if (a0 == "stat")
                return Stat(a1);
            if (a0 == "locate")
                return Locate(a1, a2);
            if (a0 == "regenworldmap")
                return Regen();
            if (a0 == "belts")
                return Belts();
            if (a0 == "conveer" || a0 == "conveyor" || a0 == "belt")
                return BeltSpeed(a1, a2);
            if (a0 == "clearcargo")
                return ClearCargo();
            if (a0 == "killitems")
                return KillItems();
            if (a0 == "dump")
                return Dump(a1);
            if (a0 == "save")
                return Save();
            if (a0 == "load")
                return Load();
            if (a0 == "godsave")
                return GodSave();
            if (a0 == "fps")
                return Fps();
            if (a0 == "log")
                return Log(a1, a2);
            if (a0 == "spawn")
                return Spawn(a1, a2);
            return "unknown: /" + line;
        }
        catch (System.Exception e)
        {
            return e.GetType().Name + ": " + e.Message;
        }
    }

    static string Help()
    {
        var sb = new StringBuilder();
        sb.AppendLine("money add|remove N");
        sb.AppendLine("ruby add|remove N");
        sb.AppendLine("research list|skip <id>|skipall");
        sb.AppendLine("tutorial restart|skip");
        sb.AppendLine("unlock recipe <id|all>");
        sb.AppendLine("unlock build <id|all>");
        sb.AppendLine("tp biome <id> | cluster <type> | veins <type>");
        sb.AppendLine("time set morning|day|evening|night|midnight|HH[:MM[:SS]]");
        sb.AppendLine("timeskip HH:MM:SS");
        sb.AppendLine("stat cluster|veins|biome");
        sb.AppendLine("locate biome|cluster|veins [id]");
        sb.AppendLine("regenWorldMap | belts | conveer speed X | clearcargo | killitems | dump cell");
        sb.AppendLine("save | load | godsave | fps");
        sb.AppendLine("log on|off belts");
        sb.AppendLine("spawn vein <type> | spawn builder <id>");
        sb.Append("help research");
        return sb.ToString();
    }

    static string HelpResearch()
    {
        return Research("list", "");
    }

    static string Money(string op, string n)
    {
        if (PlayerWallet.Instance == null)
            return "no wallet";
        int v = ParseInt(n);
        if (op == "add")
            PlayerWallet.Instance.AddCoins(v);
        else if (op == "remove")
            PlayerWallet.Instance.AddCoins(-v);
        else
            return "money add|remove N";
        return "coins " + PlayerWallet.Instance.Coins;
    }

    static string Ruby(string op, string n)
    {
        if (PlayerWallet.Instance == null)
            return "no wallet";
        int v = ParseInt(n);
        if (op == "add")
            PlayerWallet.Instance.AddRubies(v);
        else if (op == "remove")
            PlayerWallet.Instance.AddRubies(-v);
        else
            return "ruby add|remove N";
        return "rubies " + PlayerWallet.Instance.Rubies;
    }

    static string Research(string op, string id)
    {
        ResearchSystem rs = ResearchSystem.Instance;
        if (rs == null)
            return "no research";
        if (op == "list")
        {
            var sb = new StringBuilder();
            ResearchNodeData[] all = GameDatabase.AllResearches();
            if (all.Length == 0 && rs.allResearchNodes != null)
                all = rs.allResearchNodes.ToArray();
            for (int i = 0; i < all.Length; i++)
            {
                ResearchNodeData n = all[i];
                if (n == null)
                    continue;
                sb.Append(n.displayName);
                sb.Append(" - ");
                sb.Append(n.id);
                if (rs.IsResearchUnlocked(n))
                    sb.Append(" [done]");
                if (i < all.Length - 1)
                    sb.Append('\n');
            }
            return sb.Length == 0 ? "empty" : sb.ToString();
        }
        if (op == "skipall")
            return "skipped " + rs.CompleteAllResearch(false);
        if (op == "skip")
        {
            ResearchNodeData node = GameDatabase.FindResearch(id);
            if (node == null && rs.allResearchNodes != null)
            {
                for (int i = 0; i < rs.allResearchNodes.Count; i++)
                {
                    ResearchNodeData n = rs.allResearchNodes[i];
                    if (n != null && string.Equals(n.id, id, System.StringComparison.OrdinalIgnoreCase))
                        node = n;
                }
            }
            if (node == null)
                return "no research " + id;
            rs.CompleteResearch(node, false);
            return "done " + node.id;
        }
        return "research list|skip <id>|skipall";
    }

    static string Tutorial(string op)
    {
        TutorialSystem t = TutorialSystem.Instance;
        if (t == null)
            return "no tutorial";
        if (op == "skip")
        {
            t.Skip();
            return "tutorial skipped";
        }
        if (op == "restart")
        {
            t.Restart();
            return "tutorial restart";
        }
        return "tutorial restart|skip";
    }

    static string Unlock(string kind, string id)
    {
        ResearchSystem rs = ResearchSystem.Instance;
        if (rs == null)
            return "no research";
        if (kind == "recipe")
        {
            if (id == "all")
            {
                RecipeData[] all = GameDatabase.AllRecipes();
                int n = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (rs.UnlockRecipe(all[i]))
                        n++;
                }
                return "recipes +" + n;
            }
            RecipeData recipe = GameDatabase.FindRecipe(id);
            if (recipe == null)
                return "no recipe " + id;
            rs.UnlockRecipe(recipe);
            return "recipe " + recipe.id;
        }
        if (kind == "build")
        {
            if (id == "all")
            {
                BuildingData[] all = GameDatabase.AllBuildings();
                int n = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (rs.UnlockBuilding(all[i]))
                        n++;
                }
                return "buildings +" + n;
            }
            BuildingData b = GameDatabase.FindBuilding(id);
            if (b == null)
                return "no building " + id;
            rs.UnlockBuilding(b);
            return "building " + b.id;
        }
        return "unlock recipe|build <id|all>";
    }

    static string Tp(string kind, string id)
    {
        PlayerMovement move = Object.FindFirstObjectByType<PlayerMovement>();
        if (move == null)
            return "no player";
        Vector2Int cell;
        if (kind == "biome")
        {
            WorldBiome biome;
            if (!TryParseBiome(id, out biome))
                return "biome: field woodland forest beach lake ocean peak slope mountain";
            if (!NearestBiome(biome, out cell))
                return "no cell";
        }
        else if (kind == "cluster")
        {
            if (!NearestCluster(id, out cell))
                return "no cluster " + id;
        }
        else if (kind == "veins" || kind == "vein")
        {
            if (!NearestVein(id, out cell))
                return "no vein " + id;
        }
        else
            return "tp biome|cluster|veins <id>";
        move.TeleportToCell(cell);
        return "tp " + cell.x + "," + cell.y;
    }

    static string TimeSet(string op, string value)
    {
        if (op != "set")
            return "time set ...";
        float hour;
        if (!TryParseHour(value, out hour))
            return "time set morning|day|evening|night|midnight|HH[:MM[:SS]]";
        AchievementSystem.NotifyTimeCheat();
        DayNight.Hour = DayNight.WrapHour(hour);
        GameSettings.ApplyAtmosphere();
        return "time " + DayNight.FormatHour(DayNight.Hour);
    }

    public static string Describe(string raw)
    {
        string q = Normalize(raw);
        if (q.Length == 0)
            q = "/";
        string best = "";
        for (int i = 0; i < Catalog.Length; i++)
        {
            if (!Catalog[i].StartsWith(q, true, CultureInfo.InvariantCulture))
                continue;
            best = Catalog[i];
            break;
        }

        if (best.StartsWith("/timeskip", true, CultureInfo.InvariantCulture) || q.StartsWith("/timeskip", true, CultureInfo.InvariantCulture))
            return "симуляция сдачи в лабу по текущим скоростям (монеты и жилы, рубины не трогать). синтаксис: /timeskip ЧЧ:ММ:СС";
        if (best.StartsWith("/time", true, CultureInfo.InvariantCulture) || q.StartsWith("/time", true, CultureInfo.InvariantCulture))
            return "поставить час мира. синтаксис: /time set morning|day|evening|night|midnight|ЧЧ[:ММ[:СС]]";
        if (best.StartsWith("/money", true, CultureInfo.InvariantCulture))
            return "монеты. синтаксис: /money add|remove N";
        if (best.StartsWith("/ruby", true, CultureInfo.InvariantCulture))
            return "рубины. синтаксис: /ruby add|remove N";
        if (best.StartsWith("/research", true, CultureInfo.InvariantCulture))
            return "исследования. синтаксис: /research list|skip <id>|skipall";
        if (best.StartsWith("/unlock", true, CultureInfo.InvariantCulture))
            return "открыть рецепт или здание. синтаксис: /unlock recipe|build <id|all>";
        if (best.StartsWith("/tp", true, CultureInfo.InvariantCulture))
            return "телепорт. синтаксис: /tp biome <id> | cluster <type> | veins <type>";
        if (best.StartsWith("/spawn", true, CultureInfo.InvariantCulture))
            return "заспавнить. синтаксис: /spawn vein <type> | spawn builder <id>";
        if (best.StartsWith("/conveer", true, CultureInfo.InvariantCulture)
            || best.StartsWith("/conveyor", true, CultureInfo.InvariantCulture)
            || q.StartsWith("/conveer", true, CultureInfo.InvariantCulture)
            || q.StartsWith("/conveyor", true, CultureInfo.InvariantCulture)
            || q.StartsWith("/belt speed", true, CultureInfo.InvariantCulture))
            return "множитель скорости лент. синтаксис: /conveer speed X";
        if (best.StartsWith("/help", true, CultureInfo.InvariantCulture))
            return "список команд. синтаксис: /help [research]";
        if (string.IsNullOrEmpty(best))
            return "";
        return "Tab — подставить. синтаксис: " + best;
    }

    static string Timeskip(string[] p)
    {
        string spec = p.Length >= 2 ? p[1] : "";
        if (p.Length >= 4)
            spec = p[1] + ":" + p[2] + ":" + p[3];
        float seconds;
        if (!TryParseDuration(spec, out seconds) || seconds <= 0f)
            return "timeskip HH:MM:SS";
        AchievementSystem.NotifyTimeCheat();

        float minutes = seconds / 60f;
        ProductionStats stats = ProductionStats.Instance;
        ResearchSystem rs = ResearchSystem.Instance;
        var sb = new StringBuilder();
        int coinGain = 0;
        if (stats != null && PlayerWallet.Instance != null)
        {
            coinGain = Mathf.Max(0, Mathf.RoundToInt(stats.CoinsPerMinute() * minutes));
            if (coinGain > 0)
                PlayerWallet.Instance.AddCoins(coinGain);
        }

        int kinds = 0;
        int submitted = 0;
        ItemData[] items = GameDatabase.AllItems();
        for (int i = 0; i < items.Length; i++)
        {
            ItemData item = items[i];
            if (item == null || IsRubyItem(item) || stats == null)
                continue;
            float perMin = stats.ProducedPerMinute(item.id);
            int amount = Mathf.Max(0, Mathf.RoundToInt(perMin * minutes));
            if (amount <= 0)
                continue;
            kinds++;
            if (rs != null)
                submitted += rs.SubmitBulk(item, amount);
        }

        float dayMin = Mathf.Max(1f, GameSettings.DayLengthMinutes);
        float gameHours = (seconds / 3600f) * (24f * 60f / dayMin);
        DayNight.Advance(gameHours);
        GameSettings.ApplyAtmosphere();

        sb.Append("skip ").Append(spec);
        sb.Append("  +").Append(coinGain).Append("c");
        sb.Append("  items ").Append(submitted).Append(" / ").Append(kinds).Append(" kinds");
        sb.Append("  clock ").Append(DayNight.FormatHour(DayNight.Hour));
        return sb.ToString();
    }

    static bool IsRubyItem(ItemData item)
    {
        if (item == null || string.IsNullOrEmpty(item.id))
            return true;
        return item.id.IndexOf("ruby", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool TryParseDuration(string spec, out float seconds)
    {
        seconds = 0f;
        if (string.IsNullOrEmpty(spec))
            return false;
        string[] parts = spec.Split(':');
        if (parts.Length == 1)
        {
            float v;
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return false;
            seconds = v;
            return seconds > 0f;
        }

        if (parts.Length == 2 || parts.Length == 3)
        {
            float h = 0f;
            float m;
            float s = 0f;
            int i = 0;
            if (parts.Length == 3)
            {
                if (!float.TryParse(parts[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out h))
                    return false;
            }

            if (!float.TryParse(parts[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out m))
                return false;
            if (i < parts.Length
                && !float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out s))
                return false;
            seconds = h * 3600f + m * 60f + s;
            return seconds > 0f;
        }

        return false;
    }

    static string Stat(string kind)
    {
        if (kind == "biome")
            return StatBiome();
        if (kind == "veins")
            return StatVeins();
        if (kind == "cluster")
            return StatClusters();
        return "stat cluster|veins|biome";
    }

    static string Locate(string kind, string id)
    {
        var sb = new StringBuilder();
        int shown = 0;
        if (kind == "biome")
        {
            WorldBiome biome;
            if (!TryParseBiome(id, out biome) && !string.IsNullOrEmpty(id))
                return "bad biome";
            WorldBiomeMap map = WorldBiomeMap.Instance;
            if (map == null || !map.IsReady)
                return "no map";
            var cells = new List<Vector2Int>(64);
            if (!TryParseBiome(id, out biome))
                return "locate biome field|woodland|forest|beach|lake|ocean|peak|slope|mountain";
            map.CollectBiomeCells(biome, cells);
            for (int i = 0; i < cells.Count && shown < 20; i += Mathf.Max(1, cells.Count / 20))
            {
                sb.Append(cells[i].x);
                sb.Append(',');
                sb.Append(cells[i].y);
                sb.Append(' ');
                shown++;
            }
            sb.Append("(");
            sb.Append(cells.Count);
            sb.Append(")");
            return sb.ToString();
        }
        if (kind == "cluster")
        {
            WorldResourceScatterer s = WorldResourceScatterer.Instance;
            if (s == null)
                return "no scatter";
            for (int i = 0; i < s.ClusterCenters.Count && shown < 30; i++)
            {
                string k = i < s.ClusterKindKeys.Count ? s.ClusterKindKeys[i] : "";
                if (!string.IsNullOrEmpty(id) && !KindMatch(k, id))
                    continue;
                sb.Append(k);
                sb.Append(' ');
                sb.Append(s.ClusterCenters[i].x);
                sb.Append(',');
                sb.Append(s.ClusterCenters[i].y);
                sb.Append('\n');
                shown++;
            }
            return shown == 0 ? "none" : sb.ToString();
        }
        if (kind == "veins" || kind == "vein")
        {
            WorldResourceScatterer s = WorldResourceScatterer.Instance;
            if (s == null)
                return "no scatter";
            for (int i = 0; i < s.Veins.Count && shown < 30; i++)
            {
                string label = s.Veins[i].label ?? "";
                if (!string.IsNullOrEmpty(id) && !KindMatch(label, id))
                    continue;
                sb.Append(label);
                sb.Append(' ');
                sb.Append(s.Veins[i].cell.x);
                sb.Append(',');
                sb.Append(s.Veins[i].cell.y);
                sb.Append('\n');
                shown++;
            }
            return shown == 0 ? "none" : sb.ToString();
        }
        return "locate biome|cluster|veins [id]";
    }

    static string Regen()
    {
        if (WorldBiomeMap.Instance != null)
            WorldBiomeMap.Instance.Generate();
        if (WorldResourceScatterer.Instance != null)
            WorldResourceScatterer.Instance.ScatterPreservingExtractorVeins();
        return "regen";
    }

    static string BeltSpeed(string op, string value)
    {
        if (op != "speed")
            return "conveer speed X";
        BeltSpeedSystem sys = BeltSpeedSystem.Instance;
        if (sys == null)
            return "no belts";
        if (string.IsNullOrEmpty(value))
            return "speed x" + sys.CheatMul.ToString("0.##") + "  total x" + sys.Multiplier.ToString("0.##");
        float mul;
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out mul))
            return "conveer speed X";
        sys.SetCheatMul(mul);
        return "speed x" + sys.CheatMul.ToString("0.##") + "  total x" + sys.Multiplier.ToString("0.##");
    }

    static string Belts()
    {
        Conveyor[] belts = Object.FindObjectsByType<Conveyor>(FindObjectsSortMode.None);
        int cargo = 0;
        int forms = 0;
        int onScreen = 0;
        for (int i = 0; i < belts.Length; i++)
        {
            if (belts[i] == null)
                continue;
            cargo += belts[i].CargoCount;
            if (belts[i].transform.childCount > 1)
                forms++;
            if (WorldView.InRange(belts[i].transform.position))
                onScreen++;
        }
        Splitter[] splits = Object.FindObjectsByType<Splitter>(FindObjectsSortMode.None);
        return "belts " + belts.Length + " cargo " + cargo + " extraForms " + forms + " onScreen " + onScreen + " splitters " + splits.Length;
    }

    static string ClearCargo()
    {
        Conveyor[] belts = Object.FindObjectsByType<Conveyor>(FindObjectsSortMode.None);
        for (int i = 0; i < belts.Length; i++)
        {
            if (belts[i] != null)
                belts[i].DevClearCargo();
        }
        Splitter[] splits = Object.FindObjectsByType<Splitter>(FindObjectsSortMode.None);
        for (int i = 0; i < splits.Length; i++)
        {
            if (splits[i] != null)
                splits[i].DevClearCargo();
        }
        return "cargo cleared";
    }

    static string KillItems()
    {
        BeltItemView.ClearPool();
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
                continue;
            if (!all[i].name.StartsWith("BeltItem_"))
                continue;
            Object.Destroy(all[i].gameObject);
            n++;
        }
        return "killed " + n;
    }

    static string Dump(string what)
    {
        if (what != "cell")
            return "dump cell";
        PlayerMovement move = Object.FindFirstObjectByType<PlayerMovement>();
        if (move == null)
            return "no player";
        Vector2Int cell = BuildingLinker.WorldToCell(move.transform.position);
        WorldBiome biome = WorldBiomeMap.Instance != null ? WorldBiomeMap.Instance.Get(cell) : WorldBiome.Field;
        GameObject occ = GridOccupancy.GetAt(cell);
        BuildingBase b = BuildingLinker.GetBuildingAt(cell);
        ResourceNode node = ResourceNode.GetAt(cell);
        string vein = "";
        if (WorldResourceScatterer.Instance != null)
            WorldResourceScatterer.Instance.TryGetVeinLabel(cell, out vein);
        return "cell " + cell.x + "," + cell.y
            + " biome " + biome
            + " occ " + (occ != null ? occ.name : "-")
            + " bld " + (b != null && b.data != null ? b.data.id : "-")
            + " vein " + (node != null && node.resource != null ? node.resource.id : (vein ?? "-"));
    }

    static string Save()
    {
        if (SaveSystem.Instance == null)
            return "no save";
        SaveSystem.Instance.SaveGame();
        return "saved";
    }

    static string Load()
    {
        if (SaveSystem.Instance == null)
            return "no save";
        SaveSystem.Instance.LoadGame();
        return "loading";
    }

    static string GodSave()
    {
        if (SaveSystem.Instance == null || !WorldCatalog.HasActive)
            return "no world";
        SaveSystem.Instance.SaveGame();
        string src = WorldCatalog.ActiveSavePath;
        if (string.IsNullOrEmpty(src) || !File.Exists(src))
            return "no file";
        string dst = src + ".god";
        File.Copy(src, dst, true);
        return "godsave " + Path.GetFileName(dst);
    }

    static string Fps()
    {
        float dt = Time.unscaledDeltaTime;
        float fps = dt > 0.0001f ? 1f / dt : 0f;
        Conveyor[] belts = Object.FindObjectsByType<Conveyor>(FindObjectsSortMode.None);
        int items = 0;
        Transform[] tr = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < tr.Length; i++)
        {
            if (tr[i] != null && tr[i].name.StartsWith("BeltItem_"))
                items++;
        }
        MeshRenderer[] mesh = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        return "fps " + fps.ToString("0.0")
            + " belts " + belts.Length
            + " beltItems " + items
            + " pool " + BeltItemView.PooledCount
            + " meshR " + mesh.Length;
    }

    static string Log(string onOff, string target)
    {
        bool on = onOff == "on";
        if (target != "belts")
            return "log on|off belts";
        Conveyor[] belts = Object.FindObjectsByType<Conveyor>(FindObjectsSortMode.None);
        for (int i = 0; i < belts.Length; i++)
        {
            if (belts[i] != null)
                belts[i].showDebug = on;
        }
        Extractor[] ex = Object.FindObjectsByType<Extractor>(FindObjectsSortMode.None);
        for (int i = 0; i < ex.Length; i++)
        {
            if (ex[i] != null)
                ex[i].showDebug = on;
        }
        return "debug belts " + on;
    }

    static string Spawn(string kind, string id)
    {
        PlayerMovement move = Object.FindFirstObjectByType<PlayerMovement>();
        if (move == null)
            return "no player";
        Vector2Int cell = BuildingLinker.WorldToCell(move.transform.position);
        if (kind == "vein")
        {
            if (WorldResourceScatterer.Instance == null)
                return "no scatter";
            if (!WorldResourceScatterer.Instance.DevSpawnVein(id, cell))
                return "spawn vein failed";
            return "vein " + id + " " + cell.x + "," + cell.y;
        }
        if (kind == "builder" || kind == "building")
        {
            BuildingData data = GameDatabase.FindBuilding(id);
            if (data == null || data.prefab == null)
                return "no building " + id;
            Vector3 pos = GridSystem.Instance != null
                ? GridSystem.Instance.GetCellCenter(cell, move.transform.position.y)
                : move.transform.position;
            GameObject go = Object.Instantiate(data.prefab, pos, Quaternion.identity);
            BuildingBase b = go.GetComponent<BuildingBase>();
            if (b != null)
            {
                b.data = data;
                b.OnPlaced();
            }
            return "spawn " + data.id;
        }
        return "spawn vein <type> | spawn builder <id>";
    }

    static string StatBiome()
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady)
            return "no map";
        var counts = new Dictionary<WorldBiome, int>();
        var tmp = new List<Vector2Int>(256);
        for (int i = 0; i <= (int)WorldBiome.MountainSlope; i++)
        {
            tmp.Clear();
            var biome = (WorldBiome)i;
            map.CollectBiomeCells(biome, tmp);
            counts[biome] = tmp.Count;
        }
        var sb = new StringBuilder();
        foreach (KeyValuePair<WorldBiome, int> pair in counts)
        {
            sb.Append(pair.Key);
            sb.Append(' ');
            sb.Append(pair.Value);
            sb.Append('\n');
        }
        return sb.ToString();
    }

    static string StatVeins()
    {
        WorldResourceScatterer s = WorldResourceScatterer.Instance;
        if (s == null)
            return "no scatter";
        var map = new Dictionary<string, int>();
        for (int i = 0; i < s.Veins.Count; i++)
        {
            string k = s.Veins[i].label ?? "?";
            int n;
            map.TryGetValue(k, out n);
            map[k] = n + 1;
        }
        var sb = new StringBuilder();
        foreach (KeyValuePair<string, int> pair in map)
        {
            sb.Append(pair.Key);
            sb.Append(' ');
            sb.Append(pair.Value);
            sb.Append('\n');
        }
        sb.Append("total ");
        sb.Append(s.Veins.Count);
        return sb.ToString();
    }

    static string StatClusters()
    {
        WorldResourceScatterer s = WorldResourceScatterer.Instance;
        if (s == null)
            return "no scatter";
        var map = new Dictionary<string, int>();
        for (int i = 0; i < s.ClusterKindKeys.Count; i++)
        {
            string k = s.ClusterKindKeys[i] ?? "?";
            int n;
            map.TryGetValue(k, out n);
            map[k] = n + 1;
        }
        var sb = new StringBuilder();
        foreach (KeyValuePair<string, int> pair in map)
        {
            sb.Append(pair.Key);
            sb.Append(' ');
            sb.Append(pair.Value);
            sb.Append('\n');
        }
        sb.Append("total ");
        sb.Append(s.ClusterCenters.Count);
        return sb.ToString();
    }

    static bool NearestBiome(WorldBiome biome, out Vector2Int cell)
    {
        cell = Vector2Int.zero;
        WorldBiomeMap map = WorldBiomeMap.Instance;
        PlayerMovement move = Object.FindFirstObjectByType<PlayerMovement>();
        if (map == null || move == null)
            return false;
        Vector2Int origin = BuildingLinker.WorldToCell(move.transform.position);
        var cells = new List<Vector2Int>(256);
        if (biome == WorldBiome.MountainPeak)
        {
            map.CollectBiomeCells(WorldBiome.MountainPeak, cells);
            map.CollectBiomeCells(WorldBiome.MountainSlope, cells);
        }
        else
            map.CollectBiomeCells(biome, cells);
        if (cells.Count == 0)
            return false;
        int best = 0;
        int bestD = int.MaxValue;
        for (int i = 0; i < cells.Count; i++)
        {
            int d = Mathf.Abs(cells[i].x - origin.x) + Mathf.Abs(cells[i].y - origin.y);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        cell = cells[best];
        return true;
    }

    static bool NearestCluster(string type, out Vector2Int cell)
    {
        cell = Vector2Int.zero;
        WorldResourceScatterer s = WorldResourceScatterer.Instance;
        PlayerMovement move = Object.FindFirstObjectByType<PlayerMovement>();
        if (s == null || move == null)
            return false;
        Vector2Int origin = BuildingLinker.WorldToCell(move.transform.position);
        int best = -1;
        int bestD = int.MaxValue;
        for (int i = 0; i < s.ClusterCenters.Count; i++)
        {
            string k = i < s.ClusterKindKeys.Count ? s.ClusterKindKeys[i] : "";
            if (!KindMatch(k, type))
                continue;
            int d = Mathf.Abs(s.ClusterCenters[i].x - origin.x) + Mathf.Abs(s.ClusterCenters[i].y - origin.y);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        if (best < 0)
            return false;
        cell = s.ClusterCenters[best];
        return true;
    }

    static bool NearestVein(string type, out Vector2Int cell)
    {
        cell = Vector2Int.zero;
        WorldResourceScatterer s = WorldResourceScatterer.Instance;
        PlayerMovement move = Object.FindFirstObjectByType<PlayerMovement>();
        if (s == null || move == null)
            return false;
        Vector2Int origin = BuildingLinker.WorldToCell(move.transform.position);
        int best = -1;
        int bestD = int.MaxValue;
        for (int i = 0; i < s.Veins.Count; i++)
        {
            if (!KindMatch(s.Veins[i].label, type))
                continue;
            int d = Mathf.Abs(s.Veins[i].cell.x - origin.x) + Mathf.Abs(s.Veins[i].cell.y - origin.y);
            if (d < bestD)
            {
                bestD = d;
                best = i;
            }
        }
        if (best < 0)
            return false;
        cell = s.Veins[best].cell;
        return true;
    }

    static bool TryParseBiome(string id, out WorldBiome biome)
    {
        biome = WorldBiome.Field;
        if (string.IsNullOrEmpty(id))
            return false;
        switch (id.ToLowerInvariant())
        {
            case "field": biome = WorldBiome.Field; return true;
            case "woodland": biome = WorldBiome.Woodland; return true;
            case "forest": biome = WorldBiome.Forest; return true;
            case "beach": biome = WorldBiome.Beach; return true;
            case "lake": biome = WorldBiome.Lake; return true;
            case "ocean": biome = WorldBiome.Ocean; return true;
            case "peak":
            case "mountainpeak": biome = WorldBiome.MountainPeak; return true;
            case "slope":
            case "mountainslope": biome = WorldBiome.MountainSlope; return true;
            case "mountain": biome = WorldBiome.MountainPeak; return true;
            default: return System.Enum.TryParse(id, true, out biome);
        }
    }

    static bool KindMatch(string have, string want)
    {
        if (string.IsNullOrEmpty(want))
            return true;
        if (string.IsNullOrEmpty(have))
            return false;
        have = have.ToLowerInvariant();
        want = want.ToLowerInvariant();
        if (have == want)
            return true;
        if (want == "iron" && (have.Contains("желез") || have == "iron"))
            return true;
        if (want == "copper" || want == "cooper")
            return have.Contains("мед") || have.Contains("cooper") || have.Contains("copper");
        if (want == "stone" && (have.Contains("кам") || have == "stone"))
            return true;
        if (want == "coal" && (have.Contains("угол") || have == "coal"))
            return true;
        if (want == "sulfur" && (have.Contains("сер") || have == "sulfur"))
            return true;
        if (want == "sand" && (have.Contains("пес") || have == "sand"))
            return true;
        if ((want == "tree" || want == "log") && (have.Contains("дерев") || have == "tree" || have == "log"))
            return true;
        return have.Contains(want);
    }

    static bool TryParseHour(string value, out float hour)
    {
        hour = 0f;
        if (string.IsNullOrEmpty(value))
            return false;
        switch (value.ToLowerInvariant())
        {
            case "morning": hour = 7.5f; return true;
            case "day": hour = 12f; return true;
            case "evening": hour = 18.5f; return true;
            case "night": hour = 21f; return true;
            case "midnight": hour = 0f; return true;
        }
        string[] parts = value.Split(':');
        if (parts.Length == 1)
        {
            int h;
            if (!int.TryParse(parts[0], out h))
                return false;
            hour = h;
            return true;
        }
        if (parts.Length >= 2)
        {
            int h, m, s = 0;
            if (!int.TryParse(parts[0], out h) || !int.TryParse(parts[1], out m))
                return false;
            if (parts.Length >= 3 && !int.TryParse(parts[2], out s))
                return false;
            hour = h + m / 60f + s / 3600f;
            return true;
        }
        return false;
    }

    static int ParseInt(string s)
    {
        int v;
        int.TryParse(s, out v);
        return Mathf.Abs(v);
    }

    static string Normalize(string raw)
    {
        return raw == null ? "" : raw.Trim();
    }

    static string[] Split(string line)
    {
        return line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
    }
}
