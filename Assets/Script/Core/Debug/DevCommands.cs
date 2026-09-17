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
        "/stat cluster",
        "/stat veins",
        "/stat biome",
        "/locate biome",
        "/locate cluster",
        "/locate veins",
        "/regenWorldMap",
        "/belts",
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
                into.Add(Catalog[i]);
        }
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
            if (a0 == "stat")
                return Stat(a1);
            if (a0 == "locate")
                return Locate(a1, a2);
            if (a0 == "regenworldmap")
                return Regen();
            if (a0 == "belts")
                return Belts();
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
        sb.AppendLine("stat cluster|veins|biome");
        sb.AppendLine("locate biome|cluster|veins [id]");
        sb.AppendLine("regenWorldMap | belts | clearcargo | killitems | dump cell");
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
        {
            ResearchNodeData[] all = GameDatabase.AllResearches();
            if (all.Length == 0 && rs.allResearchNodes != null)
                all = rs.allResearchNodes.ToArray();
            int n = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || rs.IsResearchUnlocked(all[i]))
                    continue;
                rs.CompleteResearch(all[i], false);
                n++;
            }
            return "skipped " + n;
        }
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
        DayNight.Hour = DayNight.WrapHour(hour);
        GameSettings.ApplyAtmosphere();
        return "time " + DayNight.FormatHour(DayNight.Hour);
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
