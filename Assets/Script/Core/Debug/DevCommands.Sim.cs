using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>Время и симуляция, сейвы, поломки и мини-игры, отладочные оверлеи, язык, баг-репорт.</summary>
public static partial class DevCommands
{
    // ---------- Время ----------

    [DevCommand("timescale", "[<x>]", "скорость всей игры 0.1–20 поверх настройки")]
    static string Timescale(DevArgs a)
    {
        if (a.Has(0))
        {
            if (!a.TryFloat(0, out float x) || x <= 0f)
                return UsageOf(a);
            GameSettings.DevTimeScale = Mathf.Clamp(x, 0.1f, 20f);
            if (!PhotoMode.IsActive)
                GameSettings.ApplyTimeScale();
        }

        return Ok("timescale ×" + F(GameSettings.DevTimeScale) + " (итого Time.timeScale " + F(Time.timeScale) + ")"
            + (GameSettings.DevFrozen ? " · симуляция на паузе: /pause off" : ""));
    }

    [DevCommand("pause", "[on|off]", "остановить симуляцию без меню паузы (камера и консоль работают)")]
    static string Pause(DevArgs a)
    {
        if (a.Has(0) && !a.Is(0, "on", "off"))
            return UsageOf(a);
        GameSettings.DevFrozen = a.Has(0) ? a[0] == "on" : !GameSettings.DevFrozen;
        GameSettings.ApplyTimeScale();
        return Ok(GameSettings.DevFrozen ? "симуляция стоит · " + Btn("шаг", "/step") + " " + Btn("10 кадров", "/step 10") + " " + Btn("дальше", "/pause off") : "симуляция идёт");
    }

    [DevCommand("step", "[<frames>]", "прогнать N кадров (по умолчанию 1) и снова встать на паузу")]
    static string Step(DevArgs a)
    {
        int frames = 1;
        if (a.Has(0) && (!a.TryInt(0, out frames) || frames <= 0))
            return UsageOf(a);
        frames = Mathf.Min(frames, 10000);
        DevRuntime.Step(frames);
        return Ok("шаг: " + frames + " кадр(ов) с кадра " + Time.frameCount + "  " + Btn("ещё", "/step " + frames));
    }

    // ---------- Сейвы ----------

    [DevCommand("save", "", "сохранить мир")]
    [DevCommand("save", "as <name>", "именованный сейв для тестовых сценариев (папка dev_saves мира)")]
    static string Save(DevArgs a)
    {
        SaveSystem sys = SaveSystem.Instance;
        if (sys == null || !WorldCatalog.HasActive)
            return Err("нет мира");
        if (!a.Has(0))
        {
            sys.SaveGame();
            return Ok("saved");
        }

        if (a[0] != "as" || !a.Has(1))
            return UsageOf(a);
        string name = SafeName(a.From(1));
        if (name.Length == 0)
            return Err("плохое имя");
        string path = DevSavePath(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, sys.CaptureJson());
        return Ok("сохранено: " + name + "  " + Btn("загрузить", "/load " + name));
    }

    [DevCommand("load", "", "загрузить мир из основного сейва")]
    [DevCommand("load", "<save>", "загрузить именованный сейв")]
    static string Load(DevArgs a)
    {
        SaveSystem sys = SaveSystem.Instance;
        if (sys == null || !WorldCatalog.HasActive)
            return Err("нет мира");
        if (!a.Has(0))
        {
            sys.LoadGame();
            return Ok("loading");
        }

        string name = SafeName(a.From(0));
        string path = DevSavePath(name);
        if (!File.Exists(path))
            return Err("нет сейва " + name + " — /saves list");
        return sys.LoadFromJson(File.ReadAllText(path)) ? Ok("загружено: " + name) : Err("не прочитать " + name);
    }

    [DevCommand("saves", "list", "именованные сейвы этого мира")]
    static string Saves(DevArgs a)
    {
        if (a[0] != "list")
            return UsageOf(a);
        if (!WorldCatalog.HasActive)
            return Err("нет мира");
        string dir = DevSaveDir();
        if (!Directory.Exists(dir))
            return Warn("сейвов нет — /save as <имя>");
        string[] files = Directory.GetFiles(dir, "*.json");
        if (files.Length == 0)
            return Warn("сейвов нет — /save as <имя>");
        System.Array.Sort(files, (x, y) => File.GetLastWriteTime(y).CompareTo(File.GetLastWriteTime(x)));
        var sb = new StringBuilder(256);
        sb.Append(dir);
        for (int i = 0; i < files.Length; i++)
        {
            string name = Path.GetFileNameWithoutExtension(files[i]);
            sb.Append('\n').Append(name).Append("  ").Append(File.GetLastWriteTime(files[i]).ToString("dd.MM HH:mm"))
                .Append("  ").Append(Btn("загрузить", "/load " + name));
        }

        return sb.ToString();
    }

    [DevCommand("godsave", "", "сохранить и скопировать сейв в .god")]
    static string GodSave(DevArgs a)
    {
        if (SaveSystem.Instance == null || !WorldCatalog.HasActive)
            return Err("no world");
        SaveSystem.Instance.SaveGame();
        string src = WorldCatalog.ActiveSavePath;
        if (string.IsNullOrEmpty(src) || !File.Exists(src))
            return Err("no file");
        string dst = src + ".god";
        File.Copy(src, dst, true);
        return Ok("godsave " + Path.GetFileName(dst));
    }

    static string snapshotJson;
    static string snapshotAt;

    [DevCommand("snapshot", "", "снимок мира в памяти (до выхода из игры)")]
    static string Snapshot(DevArgs a)
    {
        SaveSystem sys = SaveSystem.Instance;
        if (sys == null || !WorldCatalog.HasActive)
            return Err("нет мира");
        snapshotJson = sys.CaptureJson();
        snapshotAt = System.DateTime.Now.ToString("HH:mm:ss");
        return Ok("снимок " + snapshotAt + " (" + (snapshotJson.Length / 1024) + " КБ)  " + Btn("откатить", "/restore"));
    }

    [DevCommand("restore", "", "откатить мир к снимку /snapshot")]
    static string Restore(DevArgs a)
    {
        SaveSystem sys = SaveSystem.Instance;
        if (sys == null || !WorldCatalog.HasActive)
            return Err("нет мира");
        if (string.IsNullOrEmpty(snapshotJson))
            return Err("снимка нет — /snapshot");
        return sys.LoadFromJson(snapshotJson) ? Ok("откат к снимку " + snapshotAt) : Err("снимок не прочитать");
    }

    static string DevSaveDir()
    {
        return Path.Combine(Path.GetDirectoryName(WorldCatalog.ActiveSavePath) ?? Application.persistentDataPath, "dev_saves");
    }

    static string DevSavePath(string name)
    {
        return Path.Combine(DevSaveDir(), name + ".json");
    }

    static string SafeName(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        foreach (char ch in raw.Trim())
            sb.Append(char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_');
        return sb.ToString();
    }

    static List<string> DevSaveNames()
    {
        var list = new List<string>();
        if (!WorldCatalog.HasActive)
            return list;
        string dir = DevSaveDir();
        if (!Directory.Exists(dir))
            return list;
        string[] files = Directory.GetFiles(dir, "*.json");
        for (int i = 0; i < files.Length; i++)
            list.Add(Path.GetFileNameWithoutExtension(files[i]));
        return list;
    }

    [DevCommand("reset", "progress", "сбросить исследования, открытия и обучение (мир остаётся, с подтверждением)")]
    static string ResetProgress(DevArgs a)
    {
        if (a[0] != "progress")
            return UsageOf(a);
        return AskConfirm("сбросить исследования, открытые здания/рецепты и обучение?", () =>
        {
            ResearchSystem rs = ResearchSystem.Instance;
            if (rs != null)
            {
                rs.ApplySave(null);
                rs.RaiseUnlocksChanged();
            }

            if (TutorialSystem.Instance != null)
                TutorialSystem.Instance.Restart();
            return Ok("прогресс сброшен: исследования, открытия, обучение");
        });
    }

    // ---------- Поломки и мини-игры ----------

    [DevCommand("breakdown", "info|now", "сводка / одна ночная поломка сейчас (с тостом)")]
    [DevCommand("breakdown", "off|on", "выключить или включить авто-поломки")]
    [DevCommand("breakdown", "chance [<x>]", "множитель доли поломок за ночь")]
    static string Breakdown(DevArgs a)
    {
        BreakdownSystem sys = BreakdownSystem.Instance;
        if (sys == null)
            return Err("no breakdown system");
        switch (a[0])
        {
            case "now":
                int before = BreakdownSystem.BrokenCount;
                sys.ForceOne();
                return BreakdownSystem.BrokenCount > before ? Ok("одна поломка") : Warn("некого ломать (или лимит 25%)");
            case "off":
            case "on":
                BreakdownSystem.DevOff = a[0] == "off";
                return Ok("авто-поломки " + (BreakdownSystem.DevOff ? "выключены" : "включены"));
            case "chance":
                if (a.Has(1))
                {
                    if (!a.TryFloat(1, out float mul) || mul < 0f)
                        return UsageOf(a);
                    BreakdownSystem.DevRateMul = mul;
                }

                return Ok("поломки ×" + F(BreakdownSystem.DevRateMul) + " · за ночь ломается " + F(BreakdownSystem.DailyRate() * 100f, "0.#") + "% станков");
            case "info":
            case "":
                return sys.Describe() + (BreakdownSystem.DevOff ? "\nавто-поломки выключены консолью" : "")
                    + (!Mathf.Approximately(BreakdownSystem.DevRateMul, 1f) ? "\nконсоль: шанс ×" + F(BreakdownSystem.DevRateMul) : "");
            default:
                return UsageOf(a);
        }
    }

    [DevCommand("minigame", "<minigame>", "мини-игра ремонта: здание под прицелом (или ближайшее) ломается и открывается ремонт")]
    [DevCommand("minigame", "win|fail", "засчитать или провалить открытую мини-игру")]
    static string Minigame(DevArgs a)
    {
        RepairUI ui = RepairUI.Instance;
        if (ui == null)
            return Err("no repair ui");
        if (a[0] == "win")
            return ui.DevWin() ? Ok("мини-игра пройдена") : Err("мини-игра не открыта");
        if (a[0] == "fail")
            return ui.DevFail() ? Ok("мини-игра провалена") : Err("мини-игра не открыта");

        int game = System.Array.IndexOf(MinigameNames, a[0]);
        if (game < 0)
            return UsageOf(a);
        BuildingBase b = AimedBuilding();
        if (b == null || !BreakdownSystem.CanBreak(b))
            b = NearestBreakable();
        if (b == null)
            return Err("нет станка или добычи, которую можно сломать");
        if (!b.IsBroken)
        {
            if (BreakdownSystem.Instance == null)
                return Err("no breakdown system");
            BreakdownSystem.Instance.BreakNow(b, false);
        }

        ui.DevOpen(b, game);
        DevConsole.CloseSoon();
        return Ok("ремонт " + Name(b) + " · " + MinigameNames[game] + " · /minigame win|fail");
    }

    static BuildingBase NearestBreakable()
    {
        PlayerMovement move = Player();
        Vector3 from = move != null ? move.transform.position : Vector3.zero;
        BuildingBase best = null;
        float bestD = float.MaxValue;
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            BuildingBase b = all[i];
            if (b == null || !b.IsPlaced || !BreakdownSystem.CanBreak(b))
                continue;
            float d = (b.transform.position - from).sqrMagnitude;
            if (d < bestD)
            {
                bestD = d;
                best = b;
            }
        }

        return best;
    }

    // ---------- Отладка ----------

    [DevCommand("gizmos", "", "какие оверлеи включены")]
    [DevCommand("gizmos", "<gizmo> [on|off]", "оверлеи в мире: порты, радиусы генераторов, занятость клеток, чанки, дроны; off — всё выключить")]
    static string Gizmos(DevArgs a)
    {
        if (a.Has(0))
        {
            if (a[0] == "off")
            {
                DevGizmos.Clear();
                return Ok("оверлеи выключены");
            }

            DevGizmos.Layer layer;
            switch (a[0])
            {
                case "ports": layer = DevGizmos.Layer.Ports; break;
                case "power": layer = DevGizmos.Layer.Power; break;
                case "occupancy": layer = DevGizmos.Layer.Occupancy; break;
                case "chunks": layer = DevGizmos.Layer.Chunks; break;
                case "drones": layer = DevGizmos.Layer.Drones; break;
                default: return UsageOf(a);
            }

            if (a.Has(1) && !a.Is(1, "on", "off"))
                return UsageOf(a);
            bool on = a.Has(1) ? a[1] == "on" : !DevGizmos.IsOn(layer);
            DevGizmos.Set(layer, on);
        }

        var sb = new StringBuilder("оверлеи:");
        string[] names = { "ports", "power", "occupancy", "chunks", "drones" };
        for (int i = 0; i < names.Length; i++)
            sb.Append(' ').Append(Btn((DevGizmos.IsOn((DevGizmos.Layer)i) ? "■ " : "□ ") + names[i], "/gizmos " + names[i]));
        sb.Append("\nпорты: зелёный — вход, оранжевый — выход, жёлтый — направление ленты");
        return sb.ToString();
    }

    [DevCommand("lang", "ru|en|key", "язык интерфейса на лету; key — ключи строк вместо текста")]
    static string Lang(DevArgs a)
    {
        switch (a[0])
        {
            case "ru":
            case "en":
                UiLocale.SetShowKeys(false);
                UiLocale.Set(a[0]);
                return Ok("язык: " + a[0]);
            case "key":
                UiLocale.SetShowKeys(!UiLocale.ShowKeys);
                return Ok(UiLocale.ShowKeys ? "ключи вместо текста — что осталось текстом, то не через локализацию; выключить: /lang key" : "ключи выключены");
            default:
                return UsageOf(a);
        }
    }

    [DevCommand("bugreport", "[...]", "лог консоли, ошибки, скриншот, позиция, версия и сейв в zip; текст после — заметка")]
    static string BugReport(DevArgs a)
    {
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string root = Path.Combine(Application.persistentDataPath, "bugreports");
        string dir = Path.Combine(root, stamp);
        Directory.CreateDirectory(dir);

        var info = new StringBuilder(1024);
        info.Append("заметка: ").Append(a.Count > 0 ? a.From(0) : "—");
        info.Append("\nвремя: ").Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        info.Append("\nверсия: ").Append(Application.version).Append(" · Unity ").Append(Application.unityVersion)
            .Append(" · ").Append(Application.isEditor ? "editor" : "build");
        info.Append("\nОС: ").Append(SystemInfo.operatingSystem);
        info.Append("\nCPU: ").Append(SystemInfo.processorType).Append(" · RAM ").Append(SystemInfo.systemMemorySize).Append(" МБ");
        info.Append("\nGPU: ").Append(SystemInfo.graphicsDeviceName).Append(" · ").Append(SystemInfo.graphicsMemorySize).Append(" МБ");
        info.Append("\nэкран: ").Append(Screen.width).Append('×').Append(Screen.height)
            .Append(" · fps ").Append(F(1f / Mathf.Max(0.0001f, Time.smoothDeltaTime), "0"));
        if (WorldCatalog.HasActive)
            info.Append("\nмир: ").Append(WorldCatalog.Active.name).Append(" · seed ").Append(WorldCatalog.Active.seed)
                .Append(WorldCatalog.Active.sandbox ? " · песочница" : "");
        PlayerMovement move = Player();
        if (move != null)
        {
            Vector3 p = move.transform.position;
            Vector2Int cell = BuildingLinker.WorldToCell(p);
            info.Append("\nигрок: ").Append(F(p.x, "0.0")).Append(", ").Append(F(p.y, "0.0")).Append(", ").Append(F(p.z, "0.0"))
                .Append(" · клетка ").Append(CellText(cell)).Append(" · /tp ").Append(cell.x).Append(' ').Append(cell.y);
        }

        info.Append("\nвремя мира: день ").Append(DayNight.Day).Append(' ').Append(DayNight.FormatHour(DayNight.Hour));
        info.Append("\nзданий: ").Append(WorldSim.Buildings.Count);
        if (PlayerWallet.Instance != null)
            info.Append(" · монеты ").Append(PlayerWallet.Instance.Coins).Append(" · рубины ").Append(PlayerWallet.Instance.Rubies);
        info.Append("\nконсоль: freebuild ").Append(Economy.DevFreeBuild).Append(" · craft ×").Append(F(BuildingBase.DevGlobalWork))
            .Append(" · timescale ×").Append(F(GameSettings.DevTimeScale)).Append(" · noclip ").Append(PlayerMovement.DevNoclip)
            .Append(" · поломки ").Append(BreakdownSystem.DevOff ? "off" : "×" + F(BreakdownSystem.DevRateMul));
        File.WriteAllText(Path.Combine(dir, "info.txt"), info.ToString());

        File.WriteAllText(Path.Combine(dir, "console.log"), DevConsole.LogText());

        var errors = new StringBuilder(2048);
        IReadOnlyList<DevRuntime.LogRow> rows = DevRuntime.Errors;
        for (int i = 0; i < rows.Count; i++)
        {
            errors.Append('[').Append(F(rows[i].time, "0.0")).Append("s ").Append(rows[i].type).Append("] ").Append(rows[i].text).Append('\n');
            if (!string.IsNullOrEmpty(rows[i].stack))
                errors.Append(rows[i].stack).Append('\n');
        }

        File.WriteAllText(Path.Combine(dir, "errors.log"), errors.Length > 0 ? errors.ToString() : "ошибок и предупреждений нет\n");

        byte[] png = ScreenPreview.CapturePng(1920, 1080);
        if (png != null && png.Length > 0)
            File.WriteAllBytes(Path.Combine(dir, "screenshot.png"), png);

        if (SaveSystem.Instance != null && WorldCatalog.HasActive)
            File.WriteAllText(Path.Combine(dir, "save.json"), SaveSystem.Instance.CaptureJson());

        string zip = Path.Combine(root, "bugreport_" + stamp + ".zip");
        try
        {
            if (File.Exists(zip))
                File.Delete(zip);
            System.IO.Compression.ZipFile.CreateFromDirectory(dir, zip);
        }
        catch (System.Exception e)
        {
            GUIUtility.systemCopyBuffer = dir;
            return Warn("zip не собрался (" + e.Message + "), файлы в папке (путь в буфере):\n" + dir);
        }

        GUIUtility.systemCopyBuffer = zip;
        return Ok("баг-репорт: " + zip + "\nпуть в буфере обмена · ошибок в логе: " + rows.Count);
    }
}
