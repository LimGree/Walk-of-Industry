using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Логистика: ленты, трубы, дроны.</summary>
public static partial class DevCommands
{
    enum BeltScope { Here, All, Radius }

    [DevCommand("belt", "speed [<x>]", "множитель скорости лент (без числа — текущий)", Alias = "conveer,conveyor")]
    [DevCommand("belt", "info [here]", "лента под прицелом: груз, скорость, затор")]
    [DevCommand("belt", "jam list", "заторы на лентах: головы заторов и их длина")]
    [DevCommand("belt", "flush here|all", "убрать груз с ленты под прицелом или со всех")]
    [DevCommand("belt", "flush radius <r>", "убрать груз с лент в радиусе r клеток")]
    static string Belt(DevArgs a)
    {
        switch (a[0])
        {
            case "speed":
                return BeltSpeed(a);
            case "info":
                if (a.Has(1) && a[1] != "here")
                    return UsageOf(a);
                Conveyor belt = AimedBuilding() as Conveyor;
                if (belt == null || belt is Pipe)
                    return Err("под прицелом нет ленты");
                return BeltInfo(belt);
            case "jam":
                return a[1] == "list" ? JamList() : UsageOf(a);
            case "flush":
                return Flush(a, false);
            default:
                return UsageOf(a);
        }
    }

    static string BeltSpeed(DevArgs a)
    {
        BeltSpeedSystem sys = BeltSpeedSystem.Instance;
        if (sys == null)
            return Err("no belts");
        if (a.Has(1))
        {
            if (!a.TryFloat(1, out float mul))
                return UsageOf(a);
            sys.SetCheatMul(mul);
        }

        return Ok("speed x" + F(sys.CheatMul) + "  total x" + F(sys.Multiplier));
    }

    static string Flush(DevArgs a, bool pipes)
    {
        if (a[1] == "here")
            return BeltFlush(BeltScope.Here, 0f, pipes);
        if (a[1] == "all")
            return BeltFlush(BeltScope.All, 0f, pipes);
        if (a[1] == "radius" && a.TryFloat(2, out float r) && r > 0f)
            return BeltFlush(BeltScope.Radius, r, pipes);
        return UsageOf(a);
    }

    static string BeltFlush(BeltScope scope, float radius, bool pipes)
    {
        string what = pipes ? "труб" : "лент";
        if (scope == BeltScope.Here)
        {
            BuildingBase b = AimedBuilding();
            if (b is Conveyor belt && (belt is Pipe) == pipes)
            {
                int n = belt.CargoCount;
                belt.DevClearCargo();
                return Ok("убрано " + n);
            }

            if (!pipes && b is Splitter split)
            {
                int n = split.CargoCount;
                split.DevClearCargo();
                return Ok("убрано " + n);
            }

            if (pipes && b is PipeSplitter ps)
            {
                int n = ps.Buffered;
                ps.DevClear();
                return Ok("убрано " + n);
            }

            return Err(pipes ? "под прицелом нет трубы" : "под прицелом нет ленты или сплиттера");
        }

        var list = new List<BuildingBase>(256);
        if (scope == BeltScope.Radius)
            CollectNear(radius, list);
        else
        {
            IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].IsPlaced)
                    list.Add(all[i]);
            }
        }

        int cells = 0;
        int items = 0;
        for (int i = 0; i < list.Count; i++)
        {
            BuildingBase b = list[i];
            if (b is Conveyor belt && (belt is Pipe) == pipes)
            {
                items += belt.CargoCount;
                belt.DevClearCargo();
                cells++;
            }
            else if (!pipes && b is Splitter split)
            {
                items += split.CargoCount;
                split.DevClearCargo();
                cells++;
            }
            else if (pipes && b is PipeSplitter ps)
            {
                items += ps.Buffered;
                ps.DevClear();
                cells++;
            }
        }

        return Ok("очищено " + what + ": " + cells + ", убрано " + items);
    }

    static string BeltInfo(Conveyor belt)
    {
        bool pipe = belt is Pipe;
        var sb = new StringBuilder(256);
        Vector2Int cell = belt.Cell;
        sb.Append(pipe ? "труба " : "лента ").Append(CellText(cell)).Append(" · ").Append(belt.Shape)
            .Append(" · выход ").Append(CellText(belt.ExitDir))
            .Append(" · входы ").Append(belt.FromBack ? "сзади " : "").Append(belt.FromLeft ? "слева " : "").Append(belt.FromRight ? "справа" : "");
        if (pipe)
        {
            float fill = belt.CargoCount / (float)Mathf.Max(1, belt.maxItems);
            sb.Append("\nжидкость: ").Append(belt.CargoCount > 0 && belt.CargoItemAt(0) != null ? belt.CargoItemAt(0).id : "—")
                .Append(" · объём ").Append(belt.CargoCount).Append('/').Append(belt.maxItems)
                .Append(" · давление ").Append(F(fill * 100f, "0")).Append('%');
        }
        else
        {
            sb.Append("\nгруз ").Append(belt.CargoCount).Append('/').Append(belt.maxItems).Append(':');
            for (int i = 0; i < belt.CargoCount; i++)
            {
                ItemData item = belt.CargoItemAt(i);
                sb.Append(' ').Append(item != null ? item.id : "?").Append('@').Append(F(belt.CargoProgressAt(i), "0.00"));
            }
        }

        sb.Append("\nскорость ").Append(F(belt.CellsPerSecond)).Append(" кл/с");
        if (belt.Filter != null)
            sb.Append(" · фильтр ").Append(belt.Filter.id);
        if (belt.BlockedSeconds > 0.25f)
            sb.Append("\nЗАТОР ").Append(F(belt.BlockedSeconds, "0.#")).Append("с: ").Append(JamReason(belt));
        else
            sb.Append("\nидёт свободно");
        return sb.ToString();
    }

    const float JamAfter = 2f;

    static bool IsJammed(Conveyor belt)
    {
        return belt != null && belt.IsPlaced && belt.BlockedSeconds >= JamAfter;
    }

    static string JamReason(Conveyor belt)
    {
        BuildingBase dest = BuildingLinker.GetBuildingAt(belt.Cell + belt.ExitDir);
        ItemData front = null;
        float best = -1f;
        for (int i = 0; i < belt.CargoCount; i++)
        {
            if (belt.CargoProgressAt(i) > best)
            {
                best = belt.CargoProgressAt(i);
                front = belt.CargoItemAt(i);
            }
        }

        string item = front != null ? front.id : "груз";
        if (dest == null)
            return "конец линии — некуда отдать " + item;
        if (dest is Conveyor next)
            return IsJammed(next) ? "следующая клетка тоже стоит" : "следующая клетка не принимает " + item + " (полна или вход не с этой стороны)";
        if (dest is CrafterBuilding c && c.IdleText() != null)
            return Name(dest) + ": " + c.IdleText();
        if (dest.IsBroken)
            return Name(dest) + " сломан";
        return Name(dest) + " не принимает " + item;
    }

    static string JamList()
    {
        var jammed = new Dictionary<Vector2Int, Conveyor>(64);
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] is Conveyor belt && !(belt is Pipe) && IsJammed(belt))
                jammed[belt.Cell] = belt;
        }

        if (jammed.Count == 0)
            return Ok("заторов нет");

        var heads = new List<(Conveyor head, int length)>(16);
        var queue = new Queue<Conveyor>();
        var seen = new HashSet<Conveyor>();
        foreach (Conveyor belt in jammed.Values)
        {
            if (jammed.ContainsKey(belt.Cell + belt.ExitDir) && jammed[belt.Cell + belt.ExitDir] != belt)
                continue;
            int length = 0;
            seen.Clear();
            queue.Clear();
            queue.Enqueue(belt);
            seen.Add(belt);
            while (queue.Count > 0)
            {
                Conveyor cur = queue.Dequeue();
                length++;
                for (int d = 0; d < 4; d++)
                {
                    Vector2Int dir = Dirs[d];
                    if (!jammed.TryGetValue(cur.Cell - dir, out Conveyor prev) || seen.Contains(prev))
                        continue;
                    if (prev.ExitDir != dir)
                        continue;
                    seen.Add(prev);
                    queue.Enqueue(prev);
                }
            }

            heads.Add((belt, length));
        }

        heads.Sort((x, y) => y.length.CompareTo(x.length));
        var sb = new StringBuilder(1024);
        sb.Append("стоят лент: ").Append(jammed.Count).Append(", заторов: ").Append(heads.Count);
        int shown = Mathf.Min(25, heads.Count);
        for (int i = 0; i < shown; i++)
        {
            Conveyor h = heads[i].head;
            sb.Append('\n').Append(CellText(h.Cell)).Append(" · ").Append(heads[i].length).Append(" кл. · ")
                .Append(JamReason(h)).Append("  ").Append(Btn("tp", "/tp " + (h.Cell.x - h.ExitDir.x * 2) + " " + (h.Cell.y - h.ExitDir.y * 2)));
        }

        if (heads.Count > shown)
            sb.Append("\n… и ещё ").Append(heads.Count - shown);
        return Warn(sb.ToString());
    }

    static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)
    };

    [DevCommand("pipe", "info [here]", "труба под прицелом: жидкость, давление, объём")]
    [DevCommand("pipe", "flush here|all", "вылить трубу под прицелом или все трубы")]
    [DevCommand("pipe", "flush radius <r>", "вылить трубы в радиусе r клеток")]
    static string PipeCmd(DevArgs a)
    {
        if (a[0] == "info")
        {
            if (a.Has(1) && a[1] != "here")
                return UsageOf(a);
            BuildingBase b = AimedBuilding();
            if (b is Pipe pipe)
                return BeltInfo(pipe);
            if (b is PipeSplitter ps)
                return "трубный сплиттер " + CellText(CellOf(ps)) + " · в буфере " + ps.Buffered + "/6";
            return Err("под прицелом нет трубы");
        }

        if (a[0] == "flush")
            return Flush(a, true);
        return UsageOf(a);
    }

    // ---------- Дроны ----------

    [DevCommand("drones", "list", "станции и дроны: задача, груз, куда летят", Alias = "drone")]
    [DevCommand("drones", "speed [<x>]", "множитель скорости дронов")]
    [DevCommand("drones", "recall", "вернуть все дроны на станции (груз возвращается)")]
    [DevCommand("drones", "path show|hide", "показать маршруты дронов линиями")]
    static string Drones(DevArgs a)
    {
        switch (a[0])
        {
            case "list":
                return DroneList();
            case "speed":
                if (a.Has(1))
                {
                    if (!a.TryFloat(1, out float mul) || mul <= 0f)
                        return UsageOf(a);
                    DroneNetwork.DevSpeedMul = Mathf.Clamp(mul, 0.05f, 50f);
                }

                return Ok("drones speed ×" + F(DroneNetwork.DevSpeedMul) + " (" + F(DroneNetwork.Speed() * DroneNetwork.DevSpeedMul, "0.#") + " м/с)");
            case "recall":
                int n = 0;
                for (int i = 0; i < DroneNetwork.AllDrones.Count; i++)
                {
                    Drone d = DroneNetwork.AllDrones[i];
                    if (d != null && d.DevRecall())
                        n++;
                }

                return Ok("развернул домой: " + n);
            case "path":
                if (!a.Is(1, "show", "hide"))
                    return UsageOf(a);
                DevGizmos.Set(DevGizmos.Layer.Drones, a[1] == "show");
                return Ok("маршруты дронов: " + (a[1] == "show" ? "показаны" : "скрыты"));
            default:
                return UsageOf(a);
        }
    }

    static string DroneList()
    {
        DroneLoadStation[] stations = Object.FindObjectsByType<DroneLoadStation>(FindObjectsSortMode.None);
        var sb = new StringBuilder(1024);
        int placed = 0;
        for (int s = 0; s < stations.Length; s++)
        {
            DroneLoadStation st = stations[s];
            if (st == null || !st.IsPlaced)
                continue;
            placed++;
            sb.Append(placed > 1 ? "\n" : "").Append("станция ").Append(DroneNetwork.CellText(st))
                .Append(" → ").Append(st.Target != null ? DroneNetwork.CellText(st.Target) : "нет цели")
                .Append(" · ящиков ").Append(st.ReadyCount).Append(" · фильтр ").Append(st.filter != null ? st.filter.id : "—")
                .Append("  ").Append(TpBtn(st));
            IReadOnlyList<Drone> drones = st.Drones;
            for (int i = 0; i < drones.Count; i++)
            {
                Drone d = drones[i];
                if (d == null)
                    continue;
                sb.Append("\n  дрон ").Append(i + 1).Append(": ").Append(d.State)
                    .Append(d.CargoItem != null ? " · " + d.CargoItem.id + " ×" + d.CargoCount : " · пустой");
                if (d.IsBusy)
                    sb.Append(" · до цели ").Append(F(Vector3.Distance(d.transform.position, d.DevGoal()), "0")).Append(" м");
            }
        }

        if (placed == 0)
            return Warn("станций загрузки нет");
        sb.Append("\nвсего дронов ").Append(DroneNetwork.AllDrones.Count).Append(" · скорость ×").Append(F(DroneNetwork.DevSpeedMul));
        return sb.ToString();
    }
}
