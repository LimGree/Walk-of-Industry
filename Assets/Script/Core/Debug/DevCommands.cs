using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Команды консоли ([[DevConsole]]). Команда — метод с [[DevCommandAttribute]], реестр — [[DevRegistry]].
/// Файлы DevCommands.*.cs — группы команд по темам.
/// </summary>
public static partial class DevCommands
{
    /// <summary>Метка вида строки в начале результата: e — ошибка, w — предупреждение, o — успех, s — служебное.</summary>
    public const char Mark = '\u0001';

    static string Err(string text) => Mark + "e" + text;
    static string Warn(string text) => Mark + "w" + text;
    static string Ok(string text) => Mark + "o" + text;

    /// <summary>Кнопка в строке вывода: клик выполняет команду.</summary>
    static string Btn(string label, string command) => "[[" + label + "|" + command + "]]";

    static string UsageOf(DevArgs a)
    {
        return Warn(DevRegistry.Usage(DevRegistry.Find(a.Command)));
    }

    // ---------- Подтверждение ----------

    static System.Func<string> pendingAction;
    static string pendingWhat;

    /// <summary>Ждёт ли консоль «yes / да» на опасную команду.</summary>
    public static bool AwaitingConfirm => pendingAction != null;

    static string AskConfirm(string what, System.Func<string> action)
    {
        pendingAction = action;
        pendingWhat = what;
        return Warn(what + "\nвведи yes или да, чтобы подтвердить; другое — отмена");
    }

    // ---------- Запуск ----------

    /// <summary>Одна команда (без «;»). Возвращает текст результата, возможно с меткой вида.</summary>
    public static string Run(string raw)
    {
        string line = (raw ?? "").Trim();
        if (line.StartsWith("/"))
            line = line.Substring(1).Trim();

        if (pendingAction != null)
        {
            System.Func<string> action = pendingAction;
            string what = pendingWhat;
            pendingAction = null;
            pendingWhat = null;
            string low = line.ToLowerInvariant();
            if (low == "yes" || low == "y" || low == "да" || low == "д")
            {
                try
                {
                    return action();
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                    return Err(e.GetType().Name + ": " + e.Message);
                }
            }

            return Warn("отменено: " + what);
        }

        if (line.Length == 0)
            return "";

        string[] p = line.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        DevRegistry.Entry entry = DevRegistry.Find(p[0]);
        if (entry == null)
            return Err("нет команды /" + p[0] + " — /help");

        string[] args = new string[p.Length - 1];
        System.Array.Copy(p, 1, args, 0, args.Length);
        try
        {
            return entry.run(new DevArgs(entry.name, args));
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            return Err(e.GetType().Name + ": " + e.Message);
        }
    }

    // ---------- Значения для подсказок ----------

    static readonly string[] VeinNames =
    {
        "iron", "copper", "cooper", "coal", "stone", "sand", "sulfur", "tree", "log"
    };

    static readonly string[] BiomeNames =
    {
        "field", "woodland", "forest", "beach", "lake", "ocean", "peak", "slope", "mountain"
    };

    static readonly string[] MinigameNames = { "wires", "signal", "engine", "math", "captcha" };
    static readonly string[] GizmoNames = { "ports", "power", "occupancy", "chunks", "drones", "off" };
    static readonly string[] RotNames = { "0", "90", "180", "270" };

    /// <summary>Значения для &lt;hole&gt; в синтаксисе; null — свободный ввод.</summary>
    public static IList<string> HoleValues(string hole)
    {
        switch (hole)
        {
            case "item": return ItemIds(false);
            case "fluid": return ItemIds(true);
            case "building":
            case "type": return BuildingIds();
            case "recipe": return RecipeIds();
            case "research": return ResearchIds();
            case "vein": return VeinNames;
            case "biome": return BiomeNames;
            case "cluster": return ClusterIds();
            case "decor": return DecorIds();
            case "minigame": return MinigameNames;
            case "gizmo": return GizmoNames;
            case "rot": return RotNames;
            case "save": return DevSaveNames();
            case "command": return CommandNames();
            default: return null;
        }
    }

    /// <summary>Проверять ли значение по списку (иначе — любое, подсказки только для удобства).</summary>
    public static bool HoleIsStrict(string hole)
    {
        switch (hole)
        {
            case "item":
            case "fluid":
            case "building":
            case "type":
            case "recipe":
            case "research":
            case "decor":
            case "minigame":
            case "gizmo":
            case "command":
                return true;
            default:
                return false;
        }
    }

    static List<string> CommandNames()
    {
        var list = new List<string>(96);
        IReadOnlyList<DevRegistry.Entry> all = DevRegistry.Entries;
        for (int i = 0; i < all.Count; i++)
            list.Add(all[i].name);
        return list;
    }

    static List<string> ItemIds(bool fluidOnly)
    {
        var list = new List<string>();
        ItemData[] all = GameDatabase.AllItems();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && !string.IsNullOrEmpty(all[i].id) && (!fluidOnly || all[i].isFluid))
                list.Add(all[i].id);
        }

        return list;
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

    static List<string> DecorIds()
    {
        var list = new List<string>();
        IReadOnlyList<DecorCatalog.Def> all = DecorCatalog.All;
        for (int i = 0; i < all.Count; i++)
            list.Add(all[i].id);
        return list;
    }

    static IList<string> ClusterIds()
    {
        WorldResourceScatterer s = WorldResourceScatterer.Instance;
        if (s == null || s.ClusterKindKeys == null || s.ClusterKindKeys.Count == 0)
            return VeinNames;
        var list = new List<string>();
        for (int i = 0; i < s.ClusterKindKeys.Count; i++)
        {
            if (!string.IsNullOrEmpty(s.ClusterKindKeys[i]) && !list.Contains(s.ClusterKindKeys[i]))
                list.Add(s.ClusterKindKeys[i]);
        }

        return list.Count > 0 ? list : (IList<string>)VeinNames;
    }

    // ---------- Справка ----------

    [DevCommand("help", "[<command>]", "список команд или подробно про одну")]
    [DevCommand("help", "research", "список исследований (как /research list)")]
    static string Help(DevArgs a)
    {
        if (a[0] == "research")
            return ResearchList();
        if (a.Has(0))
        {
            DevRegistry.Entry one = DevRegistry.Find(a[0]);
            return one != null ? DevRegistry.Describe(one) : Err("нет команды " + a.Raw(0));
        }

        var sb = new StringBuilder(2048);
        IReadOnlyList<DevRegistry.Entry> all = DevRegistry.Entries;
        for (int i = 0; i < all.Count; i++)
        {
            DevRegistry.Entry e = all[i];
            sb.Append(DevRegistry.Usage(e));
            string help = e.Help;
            if (!string.IsNullOrEmpty(help))
                sb.Append("  — ").Append(help);
            if (i < all.Count - 1)
                sb.Append('\n');
        }

        sb.Append("\n/help <команда> — подробно; несколько команд в строке — через ;");
        return sb.ToString();
    }

    // ---------- Деньги ----------

    [DevCommand("money", "add|remove|set <n>", "монеты")]
    static string Money(DevArgs a)
    {
        if (PlayerWallet.Instance == null)
            return Err("no wallet");
        if (!a.TryInt(1, out int v))
            return UsageOf(a);
        v = Mathf.Abs(v);
        PlayerWallet w = PlayerWallet.Instance;
        if (a[0] == "add")
            w.AddCoins(v, MoneySource.Cheat);
        else if (a[0] == "remove")
            w.AddCoins(-v, MoneySource.Cheat);
        else if (a[0] == "set")
            w.AddCoins(v - w.Coins, MoneySource.Cheat);
        else
            return UsageOf(a);
        return Ok("coins " + w.Coins);
    }

    [DevCommand("ruby", "add|remove|set <n>", "рубины")]
    static string Ruby(DevArgs a)
    {
        if (PlayerWallet.Instance == null)
            return Err("no wallet");
        if (!a.TryInt(1, out int v))
            return UsageOf(a);
        v = Mathf.Abs(v);
        PlayerWallet w = PlayerWallet.Instance;
        if (a[0] == "add")
            w.AddRubies(v, MoneySource.Cheat);
        else if (a[0] == "remove")
            w.AddRubies(-v, MoneySource.Cheat);
        else if (a[0] == "set")
            w.AddRubies(v - w.Rubies, MoneySource.Cheat);
        else
            return UsageOf(a);
        return Ok("rubies " + w.Rubies);
    }

    [DevCommand("economy", "[<min>]", "доходы/расходы по источникам", Alias = "eco")]
    static string EconomyCmd(DevArgs a)
    {
        int minutes = 0;
        if (a.Has(0) && !a.TryInt(0, out minutes))
            return UsageOf(a);
        return EconomyLedger.Summary(minutes);
    }

    // ---------- Исследования и обучение ----------

    [DevCommand("research", "list", "все исследования, [done] — открыто")]
    [DevCommand("research", "skip <research>", "завершить исследование")]
    [DevCommand("research", "skipall", "завершить все")]
    static string Research(DevArgs a)
    {
        ResearchSystem rs = ResearchSystem.Instance;
        if (rs == null)
            return Err("no research");
        if (a[0] == "list")
            return ResearchList();
        if (a[0] == "skipall")
            return Ok("skipped " + rs.CompleteAllResearch(false));
        if (a[0] == "skip")
        {
            string id = a.Raw(1);
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
                return Err("no research " + id);
            rs.CompleteResearch(node, false);
            return Ok("done " + node.id);
        }

        return UsageOf(a);
    }

    static string ResearchList()
    {
        ResearchSystem rs = ResearchSystem.Instance;
        if (rs == null)
            return Err("no research");
        var sb = new StringBuilder();
        ResearchNodeData[] all = GameDatabase.AllResearches();
        if (all.Length == 0 && rs.allResearchNodes != null)
            all = rs.allResearchNodes.ToArray();
        for (int i = 0; i < all.Length; i++)
        {
            ResearchNodeData n = all[i];
            if (n == null)
                continue;
            sb.Append(n.Title).Append(" - ").Append(n.id);
            if (rs.IsResearchUnlocked(n))
                sb.Append(" [done]");
            else
                sb.Append("  ").Append(Btn("skip", "/research skip " + n.id));
            if (i < all.Length - 1)
                sb.Append('\n');
        }

        return sb.Length == 0 ? "empty" : sb.ToString();
    }

    [DevCommand("tutorial", "restart|skip", "обучение заново или пропустить")]
    static string Tutorial(DevArgs a)
    {
        TutorialSystem t = TutorialSystem.Instance;
        if (t == null)
            return Err("no tutorial");
        if (a[0] == "skip")
        {
            t.Skip();
            return Ok("tutorial skipped");
        }

        if (a[0] == "restart")
        {
            t.Restart();
            return Ok("tutorial restart");
        }

        return UsageOf(a);
    }

    [DevCommand("unlock", "recipe all|<recipe>", "открыть рецепт")]
    [DevCommand("unlock", "build all|<building>", "открыть здание")]
    static string Unlock(DevArgs a)
    {
        ResearchSystem rs = ResearchSystem.Instance;
        if (rs == null)
            return Err("no research");
        string id = a.Raw(1);
        if (a[0] == "recipe")
        {
            if (a[1] == "all")
            {
                RecipeData[] all = GameDatabase.AllRecipes();
                int n = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (rs.UnlockRecipe(all[i]))
                        n++;
                }

                return Ok("recipes +" + n);
            }

            RecipeData recipe = GameDatabase.FindRecipe(id);
            if (recipe == null)
                return Err("no recipe " + id);
            rs.UnlockRecipe(recipe);
            return Ok("recipe " + recipe.id);
        }

        if (a[0] == "build")
        {
            if (a[1] == "all")
            {
                BuildingData[] all = GameDatabase.AllBuildings();
                int n = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (rs.UnlockBuilding(all[i]))
                        n++;
                }

                return Ok("buildings +" + n);
            }

            BuildingData b = GameDatabase.FindBuilding(id);
            if (b == null)
                return Err("no building " + id);
            rs.UnlockBuilding(b);
            return Ok("building " + b.id);
        }

        return UsageOf(a);
    }

    // ---------- Время ----------

    [DevCommand("time", "set morning|day|evening|night|midnight|<time>", "час мира: слово или ЧЧ[:ММ[:СС]]")]
    static string TimeSet(DevArgs a)
    {
        if (a[0] != "set" || !TryParseHour(a.Raw(1), out float hour))
            return UsageOf(a);
        AchievementSystem.NotifyTimeCheat();
        DayNight.Hour = DayNight.WrapHour(hour);
        GameSettings.ApplyAtmosphere();
        return Ok("time " + DayNight.FormatHour(DayNight.Hour));
    }

    [DevCommand("timeskip", "<duration> ...", "симуляция сдачи в лабу по текущим скоростям (монеты и жилы, без рубинов): ЧЧ:ММ:СС")]
    static string Timeskip(DevArgs a)
    {
        string spec = a.Raw(0);
        if (a.Count >= 3)
            spec = a.Raw(0) + ":" + a.Raw(1) + ":" + a.Raw(2);
        if (!TryParseDuration(spec, out float seconds) || seconds <= 0f)
            return UsageOf(a);
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
                PlayerWallet.Instance.AddCoins(coinGain, MoneySource.Cheat);
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
        return Ok(sb.ToString());
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
            if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                return false;
            seconds = v;
            return seconds > 0f;
        }

        if (parts.Length == 2 || parts.Length == 3)
        {
            float h = 0f;
            float s = 0f;
            int i = 0;
            if (parts.Length == 3 && !float.TryParse(parts[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out h))
                return false;
            if (!float.TryParse(parts[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out float m))
                return false;
            if (i < parts.Length
                && !float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out s))
                return false;
            seconds = h * 3600f + m * 60f + s;
            return seconds > 0f;
        }

        return false;
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
            if (!int.TryParse(parts[0], out int h))
                return false;
            hour = h;
            return true;
        }

        if (parts.Length >= 2)
        {
            int s = 0;
            if (!int.TryParse(parts[0], out int h) || !int.TryParse(parts[1], out int m))
                return false;
            if (parts.Length >= 3 && !int.TryParse(parts[2], out s))
                return false;
            hour = h + m / 60f + s / 3600f;
            return true;
        }

        return false;
    }

    // ---------- Мир: биомы, жилы, кластеры ----------

    [DevCommand("stat", "cluster|veins|biome", "сколько кластеров / жил / клеток биомов")]
    static string Stat(DevArgs a)
    {
        if (a[0] == "biome")
            return StatBiome();
        if (a[0] == "veins")
            return StatVeins();
        if (a[0] == "cluster")
            return StatClusters();
        return UsageOf(a);
    }

    [DevCommand("locate", "biome <biome>", "клетки биома")]
    [DevCommand("locate", "cluster [<cluster>]", "центры кластеров")]
    [DevCommand("locate", "veins [<vein>]", "жилы")]
    static string Locate(DevArgs a)
    {
        string kind = a[0];
        string id = a.Raw(1);
        var sb = new StringBuilder();
        int shown = 0;
        if (kind == "biome")
        {
            WorldBiomeMap map = WorldBiomeMap.Instance;
            if (map == null || !map.IsReady)
                return Err("no map");
            if (!TryParseBiome(id, out WorldBiome biome))
                return UsageOf(a);
            var cells = new List<Vector2Int>(64);
            map.CollectBiomeCells(biome, cells);
            for (int i = 0; i < cells.Count && shown < 20; i += Mathf.Max(1, cells.Count / 20))
            {
                sb.Append(Btn(cells[i].x + "," + cells[i].y, "/tp " + cells[i].x + " " + cells[i].y)).Append(' ');
                shown++;
            }

            sb.Append('(').Append(cells.Count).Append(')');
            return sb.ToString();
        }

        if (kind == "cluster")
        {
            WorldResourceScatterer s = WorldResourceScatterer.Instance;
            if (s == null)
                return Err("no scatter");
            for (int i = 0; i < s.ClusterCenters.Count && shown < 30; i++)
            {
                string k = i < s.ClusterKindKeys.Count ? s.ClusterKindKeys[i] : "";
                if (!string.IsNullOrEmpty(id) && !KindMatch(k, id))
                    continue;
                Vector2Int c = s.ClusterCenters[i];
                if (shown > 0)
                    sb.Append('\n');
                sb.Append(k).Append(' ').Append(c.x).Append(',').Append(c.y).Append("  ").Append(Btn("tp", "/tp " + c.x + " " + c.y));
                shown++;
            }

            return shown == 0 ? Warn("none") : sb.ToString();
        }

        if (kind == "veins" || kind == "vein")
        {
            WorldResourceScatterer s = WorldResourceScatterer.Instance;
            if (s == null)
                return Err("no scatter");
            for (int i = 0; i < s.Veins.Count && shown < 30; i++)
            {
                string label = s.Veins[i].label ?? "";
                if (!string.IsNullOrEmpty(id) && !KindMatch(label, id))
                    continue;
                Vector2Int c = s.Veins[i].cell;
                if (shown > 0)
                    sb.Append('\n');
                sb.Append(label).Append(' ').Append(c.x).Append(',').Append(c.y).Append("  ").Append(Btn("tp", "/tp " + c.x + " " + c.y));
                shown++;
            }

            return shown == 0 ? Warn("none") : sb.ToString();
        }

        return UsageOf(a);
    }

    [DevCommand("regenWorldMap", "", "пересоздать карту биомов и жилы (жилы под экстракторами остаются)")]
    static string Regen(DevArgs a)
    {
        if (WorldBiomeMap.Instance != null)
            WorldBiomeMap.Instance.Generate();
        if (WorldResourceScatterer.Instance != null)
            WorldResourceScatterer.Instance.ScatterPreservingExtractorVeins();
        return Ok("regen");
    }

    [DevCommand("spawn", "vein <vein>", "жила под игроком")]
    [DevCommand("spawn", "builder <building>", "здание под игроком (старый способ, лучше /build)")]
    static string Spawn(DevArgs a)
    {
        PlayerMovement move = Player();
        if (move == null)
            return Err("no player");
        Vector2Int cell = BuildingLinker.WorldToCell(move.transform.position);
        string id = a.Raw(1);
        if (a[0] == "vein")
        {
            if (WorldResourceScatterer.Instance == null)
                return Err("no scatter");
            if (!WorldResourceScatterer.Instance.DevSpawnVein(id, cell))
                return Err("spawn vein failed");
            return Ok("vein " + id + " " + cell.x + "," + cell.y);
        }

        if (a[0] == "builder" || a[0] == "building")
        {
            BuildingData data = GameDatabase.FindBuilding(id);
            if (data == null || data.prefab == null)
                return Err("no building " + id);
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

            return Ok("spawn " + data.id);
        }

        return UsageOf(a);
    }

    [DevCommand("dump", "cell", "что в клетке под игроком")]
    static string Dump(DevArgs a)
    {
        if (a[0] != "cell")
            return UsageOf(a);
        PlayerMovement move = Player();
        if (move == null)
            return Err("no player");
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

    static string StatBiome()
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady)
            return Err("no map");
        var sb = new StringBuilder();
        var tmp = new List<Vector2Int>(256);
        for (int i = 0; i <= (int)WorldBiome.MountainSlope; i++)
        {
            tmp.Clear();
            var biome = (WorldBiome)i;
            map.CollectBiomeCells(biome, tmp);
            if (sb.Length > 0)
                sb.Append('\n');
            sb.Append(biome).Append(' ').Append(tmp.Count);
        }

        return sb.ToString();
    }

    static string StatVeins()
    {
        WorldResourceScatterer s = WorldResourceScatterer.Instance;
        if (s == null)
            return Err("no scatter");
        var map = new Dictionary<string, int>();
        for (int i = 0; i < s.Veins.Count; i++)
        {
            string k = s.Veins[i].label ?? "?";
            map.TryGetValue(k, out int n);
            map[k] = n + 1;
        }

        var sb = new StringBuilder();
        foreach (KeyValuePair<string, int> pair in map)
            sb.Append(pair.Key).Append(' ').Append(pair.Value).Append('\n');
        sb.Append("total ").Append(s.Veins.Count);
        return sb.ToString();
    }

    static string StatClusters()
    {
        WorldResourceScatterer s = WorldResourceScatterer.Instance;
        if (s == null)
            return Err("no scatter");
        var map = new Dictionary<string, int>();
        for (int i = 0; i < s.ClusterKindKeys.Count; i++)
        {
            string k = s.ClusterKindKeys[i] ?? "?";
            map.TryGetValue(k, out int n);
            map[k] = n + 1;
        }

        var sb = new StringBuilder();
        foreach (KeyValuePair<string, int> pair in map)
            sb.Append(pair.Key).Append(' ').Append(pair.Value).Append('\n');
        sb.Append("total ").Append(s.ClusterCenters.Count);
        return sb.ToString();
    }

    static bool NearestBiome(WorldBiome biome, out Vector2Int cell)
    {
        cell = Vector2Int.zero;
        WorldBiomeMap map = WorldBiomeMap.Instance;
        PlayerMovement move = Player();
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
        PlayerMovement move = Player();
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
        PlayerMovement move = Player();
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

    // ---------- Ленты (старое) ----------

    [DevCommand("belts", "", "сводка по лентам и грузу")]
    static string Belts(DevArgs a)
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

    [DevCommand("clearcargo", "", "убрать груз со всех лент и сплиттеров (как /belt flush all)")]
    static string ClearCargo(DevArgs a)
    {
        return BeltFlush(BeltScope.All, 0f, false);
    }

    [DevCommand("killitems", "", "удалить все визуалы груза (BeltItem_*) и пул")]
    static string KillItems(DevArgs a)
    {
        BeltItemView.ClearPool();
        Transform[] all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        int n = 0;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || !all[i].name.StartsWith("BeltItem_"))
                continue;
            Object.Destroy(all[i].gameObject);
            n++;
        }

        return Ok("killed " + n);
    }

    [DevCommand("log", "on|off belts", "отладочный лог лент и экстракторов в Unity-консоль")]
    static string Log(DevArgs a)
    {
        bool on = a[0] == "on";
        if (a[1] != "belts" || !a.Is(0, "on", "off"))
            return UsageOf(a);
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

        return Ok("debug belts " + on);
    }

    // ---------- Производительность ----------

    [DevCommand("fps", "", "fps, объекты, память", Alias = "perf")]
    static string Fps(DevArgs a)
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
        int shown = 0, shadow = 0;
        for (int i = 0; i < mesh.Length; i++)
        {
            if (mesh[i] == null || !mesh[i].enabled || !mesh[i].isVisible)
                continue;
            shown++;
            if (mesh[i].shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                shadow++;
        }

        int lights = 0;
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l != null && l.enabled)
                lights++;
        }

        int audio = 0;
        foreach (AudioSource s in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (s != null && s.isPlaying)
                audio++;
        }

        BuildingBase[] buildings = Object.FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        long managed = System.GC.GetTotalMemory(false) / (1024 * 1024);
        long unity = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
        // Сравнивай в начале игры и через 40 минут: что растёт, то и тормозит.
        return "fps " + fps.ToString("0.0") + " (≈" + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0") + ")"
            + "\nздания " + buildings.Length + " · ленты " + belts.Length + " · декор " + DecorSystem.All.Count
            + "\nгруз на лентах " + items + " · пул " + BeltItemView.PooledCount
            + "\nобъектов " + tr.Length + " · мешей " + mesh.Length + " · видно " + shown + " · с тенью " + shadow
            + "\nсвет " + lights + " · звуки " + audio
            + "\nпамять C# " + managed + " МБ · Unity " + unity + " МБ";
    }

    // ---------- Поломки (старое) ----------

    [DevCommand("break", "<n>|here", "сломать N% станков и добычи или здание под прицелом")]
    static string Break(DevArgs a)
    {
        BreakdownSystem sys = BreakdownSystem.Instance;
        if (sys == null)
            return Err("no breakdown system");
        if (a[0] == "here")
        {
            BuildingBase b = AimedBuilding();
            if (b == null || !BreakdownSystem.CanBreak(b))
                return Err("не станок/добыча под прицелом");
            if (b.IsBroken)
                return Warn("уже сломан");
            sys.BreakNow(b, true);
            return Ok("сломан: " + Name(b) + (b.BreakMode == 2 ? " (1/4 силы)" : ""));
        }

        if (!TryPercent(a.Raw(0), out float pct))
            return UsageOf(a);
        int n = sys.BreakPercent(pct);
        return Ok("сломано " + n + " (всего сломано " + BreakdownSystem.BrokenCount + " / " + BreakdownSystem.EligibleCount() + ")");
    }

    [DevCommand("repair", "<n>|all|here", "починить N% сломанных, все или под прицелом (без мини-игры)")]
    static string Repair(DevArgs a)
    {
        BreakdownSystem sys = BreakdownSystem.Instance;
        if (sys == null)
            return Err("no breakdown system");
        if (a[0] == "here")
        {
            BuildingBase b = AimedBuilding();
            if (b == null || !b.IsBroken)
                return Err("под прицелом нет сломанного");
            sys.Repair(b, false);
            return Ok("починен: " + Name(b));
        }

        float pct = 100f;
        if (a[0] != "all" && !TryPercent(a.Raw(0), out pct))
            return UsageOf(a);
        int n = sys.RepairPercent(pct);
        return Ok("починено " + n + ", осталось " + BreakdownSystem.BrokenCount);
    }

    static bool TryPercent(string arg, out float pct)
    {
        return DevRegistry.TryNumber(arg, out pct) && pct > 0f;
    }

    // ---------- Декор ----------

    [DevCommand("decor", "all|reset|info", "открыть все / сбросить покупки / сводка")]
    [DevCommand("decor", "give <decor>", "открыть одну декорацию")]
    static string Decor(DevArgs a)
    {
        DecorSystem sys = DecorSystem.Instance;
        if (sys == null)
            return Err("no decor system");
        if (a[0] == "all")
        {
            sys.GrantAll(true);
            return Ok("декорации открыты: " + sys.OwnedCount() + " / " + DecorCatalog.All.Count);
        }

        if (a[0] == "reset")
        {
            sys.GrantAll(false);
            return Ok("покупки декораций сброшены");
        }

        if (a[0] == "give")
        {
            DecorCatalog.Def def = DecorCatalog.Find(a.Raw(1));
            if (def == null)
                return Err("нет декорации " + a.Raw(1));
            return sys.Grant(def) ? Ok("открыта: " + def.id) : Warn("уже есть или не продаётся: " + def.id);
        }

        DecorCatalog.Def deal = DecorSystem.Deal();
        return "куплено " + sys.OwnedCount() + " / " + DecorCatalog.All.Count
            + " · красота " + DecorSystem.Beauty.ToString("0.#", CultureInfo.InvariantCulture)
            + " · поставлено " + DecorSystem.All.Count
            + " · скидка дня: " + (deal != null ? deal.id : "-")
            + " · ночь: " + (DecorSystem.IsNight ? "да" : "нет");
    }

    // ---------- Общие помощники ----------

    static PlayerMovement cachedPlayer;

    static PlayerMovement Player()
    {
        if (cachedPlayer == null)
            cachedPlayer = Object.FindFirstObjectByType<PlayerMovement>();
        return cachedPlayer;
    }

    static string Name(BuildingBase b)
    {
        if (b == null)
            return "-";
        return b.data != null && !string.IsNullOrEmpty(b.data.id) ? b.data.id : b.name;
    }

    static Vector2Int CellOf(BuildingBase b)
    {
        return BuildingLinker.WorldToCell(b.transform.position);
    }

    static string CellText(Vector2Int c)
    {
        return c.x + "," + c.y;
    }

    /// <summary>Кнопка телепорта к зданию: на свободную клетку рядом, а не на крышу.</summary>
    static string TpBtn(BuildingBase b)
    {
        Vector2Int c = NearFreeCell(b);
        return Btn("tp", "/tp " + c.x + " " + c.y);
    }

    static Vector2Int NearFreeCell(BuildingBase b)
    {
        Vector2Int center = CellOf(b);
        Vector2Int size = b.FootprintSize;
        int reach = Mathf.Max(size.x, size.y) / 2 + 1;
        for (int r = reach; r <= reach + 4; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r)
                        continue;
                    Vector2Int c = center + new Vector2Int(dx, dy);
                    if (GridOccupancy.GetAt(c) == null)
                        return c;
                }
            }
        }

        return center;
    }

    static Camera AimCamera()
    {
        Camera cam = Camera.main;
        return cam;
    }

    /// <summary>Точка под прицелом (центр экрана). В 3-м лице луч проходит сквозь игрока.</summary>
    static bool AimHit(out RaycastHit hit, float distance = 60f)
    {
        hit = default;
        Camera cam = AimCamera();
        if (cam == null)
            return false;
        var ray = new Ray(cam.transform.position, cam.transform.forward);
        return PlayerMovement.AimRaycast(ray, out hit, distance, ~0, QueryTriggerInteraction.Ignore);
    }

    /// <summary>Поставленное здание под прицелом: по коллайдеру или по клетке в точке попадания.</summary>
    static BuildingBase AimedBuilding()
    {
        if (!AimHit(out RaycastHit hit))
            return null;
        BuildingBase b = hit.collider != null ? hit.collider.GetComponentInParent<BuildingBase>() : null;
        if (b != null && b.IsPlaced)
            return b;
        GameObject occ = GridOccupancy.GetAt(BuildingLinker.WorldToCell(hit.point));
        b = occ != null ? occ.GetComponent<BuildingBase>() : null;
        return b != null && b.IsPlaced ? b : null;
    }

    static bool AimCell(out Vector2Int cell, out Vector3 point)
    {
        cell = default;
        point = default;
        if (!AimHit(out RaycastHit hit))
            return false;
        point = hit.point;
        cell = BuildingLinker.WorldToCell(hit.point);
        return true;
    }

    /// <summary>Поставленные здания в радиусе r клеток от игрока.</summary>
    static void CollectNear(float r, List<BuildingBase> into)
    {
        into.Clear();
        PlayerMovement move = Player();
        if (move == null)
            return;
        Vector3 p = move.transform.position;
        float r2 = r * GridFootprint.CellSize * r * GridFootprint.CellSize;
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            BuildingBase b = all[i];
            if (b == null || !b.IsPlaced)
                continue;
            Vector3 d = b.transform.position - p;
            d.y = 0f;
            if (d.sqrMagnitude <= r2)
                into.Add(b);
        }
    }

    static string F(float v, string format = "0.##")
    {
        return v.ToString(format, CultureInfo.InvariantCulture);
    }
}
