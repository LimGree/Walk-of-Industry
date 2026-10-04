using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Стройка: поставить, снести, повернуть, сетка и её проверка.</summary>
public static partial class DevCommands
{
    [DevCommand("build", "<building> [<rot>]", "поставить здание под прицелом бесплатно и без проверок")]
    static string Build(DevArgs a)
    {
        BuildingData data = GameDatabase.FindBuilding(a.Raw(0));
        if (data == null || data.prefab == null)
            return Err("нет здания " + a.Raw(0));
        if (data.IsPairedStraight)
            return Err(data.id + " ставится парой (вход + выход) — только руками");
        float yaw = 0f;
        if (a.Has(1) && !a.TryFloat(1, out yaw))
            return UsageOf(a);
        yaw = Mathf.Repeat(Mathf.Round(yaw / 90f) * 90f, 360f);
        if (!AimHit(out RaycastHit hit))
            return Err("под прицелом нет земли");

        float y = hit.point.y;
        BuildingBase under = hit.collider != null ? hit.collider.GetComponentInParent<BuildingBase>() : null;
        if (under != null)
            y = under.transform.position.y;
        Vector2Int size = GridFootprint.GetRotatedSize(data.size, yaw);
        Vector3 pos = GridFootprint.SnapCenter(new Vector3(hit.point.x, y, hit.point.z), size);

        GameObject go = Object.Instantiate(data.prefab, pos, Quaternion.Euler(0f, yaw, 0f));
        BuildingBase b = go.GetComponent<BuildingBase>();
        if (b == null)
        {
            GridFootprint.Register(go, pos, size);
            return Ok("поставлен объект " + data.id);
        }

        b.data = data;
        b.OnPlaced();
        BuildUndo.Begin();
        BuildUndo.NotePlaced(b);
        BuildUndo.End();
        return Ok("поставлен " + data.id + " в " + CellText(CellOf(b)) + " (" + F(yaw, "0") + "°)");
    }

    [DevCommand("freebuild", "[on|off]", "бесплатная стройка (и без возврата при сносе)")]
    static string FreeBuild(DevArgs a)
    {
        if (a.Has(0) && !a.Is(0, "on", "off"))
            return UsageOf(a);
        Economy.DevFreeBuild = a.Has(0) ? a[0] == "on" : !Economy.DevFreeBuild;
        return Ok("freebuild " + (Economy.DevFreeBuild ? "on" : "off"));
    }

    [DevCommand("destroy", "here", "снести здание под прицелом (без возврата денег)")]
    [DevCommand("destroy", "radius <r>", "снести всё в радиусе r клеток от игрока")]
    static string Destroy(DevArgs a)
    {
        if (a[0] == "here")
        {
            BuildingBase b = AimedBuilding();
            if (b == null)
                return Err("под прицелом нет здания");
            string name = Name(b);
            Vector2Int c = CellOf(b);
            Remove(b);
            return Ok("снесён " + name + " в " + CellText(c));
        }

        if (a[0] == "radius")
        {
            if (!a.TryFloat(1, out float r) || r <= 0f)
                return UsageOf(a);
            var list = new List<BuildingBase>(64);
            CollectNear(r, list);
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null || !list[i].IsPlaced)
                    continue;
                Remove(list[i]);
                n++;
            }

            return Ok("снесено " + n + " в радиусе " + F(r));
        }

        return UsageOf(a);
    }

    static void Remove(BuildingBase b)
    {
        if (b == null || !b.IsPlaced)
            return;
        b.OnRemoved();
        Object.Destroy(b.gameObject);
    }

    [DevCommand("wipe", "buildings", "снести все постройки, мир остаётся (с подтверждением)")]
    static string Wipe(DevArgs a)
    {
        if (a[0] != "buildings")
            return UsageOf(a);
        int count = 0;
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null && all[i].IsPlaced)
                count++;
        }

        return AskConfirm("снести все постройки (" + count + ")?", () =>
        {
            SaveSystem.ClearWorldBuildings();
            BuildUndo.Clear();
            return Ok("снесено построек: " + count);
        });
    }

    [DevCommand("rotate", "here [<deg>]", "повернуть здание под прицелом (по умолчанию на 90°)")]
    static string Rotate(DevArgs a)
    {
        if (a[0] != "here")
            return UsageOf(a);
        float delta = 90f;
        if (a.Has(1) && !a.TryFloat(1, out delta))
            return UsageOf(a);
        delta = Mathf.Round(delta / 90f) * 90f;
        if (Mathf.Approximately(Mathf.Repeat(delta, 360f), 0f))
            return Warn("поворот на 0°");
        BuildingBase b = AimedBuilding();
        if (b == null)
            return Err("под прицелом нет здания");
        if (b is UndergroundConveyor)
            return Err("подземку не повернуть — она парная");

        float prevYaw = b.transform.eulerAngles.y;
        Vector3 prevPos = b.transform.position;
        float newYaw = prevYaw + delta;
        b.transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
        if (b.data != null)
        {
            Vector2Int size = GridFootprint.GetRotatedSize(b.data.size, newYaw);
            Vector2Int min = GridFootprint.GetMinCell(b.transform.position, size);
            GridOccupancy.Unregister(b.gameObject);
            if (!GridOccupancy.IsAreaFree(min, size))
            {
                b.transform.rotation = Quaternion.Euler(0f, prevYaw, 0f);
                b.ReRegisterOnGrid();
                return Err("не повернуть: новые клетки заняты");
            }

            GridOccupancy.Register(b.gameObject, min, size);
        }
        else
            b.ReRegisterOnGrid();

        b.OnRotated();
        BuildUndo.NoteEdit(b, prevPos, prevYaw);
        return Ok(Name(b) + " → " + F(Mathf.Repeat(newYaw, 360f), "0") + "°");
    }

    [DevCommand("grid", "show|hide", "сетка и занятые клетки вне режима стройки")]
    static string Grid(DevArgs a)
    {
        if (!a.Is(0, "show", "hide"))
            return UsageOf(a);
        bool on = a[0] == "show";
        BuildGridVisualizer.DevForceShow = on;
        DevGizmos.Set(DevGizmos.Layer.Occupancy, on);
        return Ok(on ? "сетка: показана (голубое — здание, красное — «призрачная» клетка)" : "сетка: как в настройках");
    }

    [DevCommand("validate", "grid [fix]", "найти рассинхрон занятости клеток со зданиями; fix — починить")]
    static string Validate(DevArgs a)
    {
        if (a[0] != "grid")
            return UsageOf(a);
        bool fix = a[1] == "fix";
        var sb = new StringBuilder(512);
        int issues = 0;
        int fixedCount = 0;
        var expected = new List<Vector2Int>(16);
        var have = new List<Vector2Int>(16);
        var expectedSet = new HashSet<Vector2Int>();

        BuildingBase[] buildings = Object.FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingBase b = buildings[i];
            // Плитка и асфальт живут в своей сетке декора ([[DecorSystem]]).
            if (b == null || !b.IsPlaced || b.data == null || (b is Decoration d && d.IsFloor))
                continue;
            GridFootprint.CollectCells(b.transform.position, b.FootprintSize, expected);
            GridOccupancy.TryGetCells(b.gameObject, have);
            expectedSet.Clear();
            for (int k = 0; k < expected.Count; k++)
                expectedSet.Add(expected[k]);

            string problem = null;
            for (int k = 0; k < expected.Count && problem == null; k++)
            {
                GameObject owner = GridOccupancy.GetAt(expected[k]);
                if (owner != b.gameObject)
                    problem = "клетка " + CellText(expected[k]) + (owner == null ? " свободна" : " записана за " + owner.name);
            }

            for (int k = 0; k < have.Count && problem == null; k++)
            {
                if (!expectedSet.Contains(have[k]))
                    problem = "лишняя клетка " + CellText(have[k]);
            }

            if (problem == null)
                continue;
            issues++;
            if (issues <= 25)
                sb.Append('\n').Append(Name(b)).Append(' ').Append(CellText(CellOf(b))).Append(": ").Append(problem).Append("  ").Append(TpBtn(b));
            if (fix)
            {
                b.ReRegisterOnGrid();
                fixedCount++;
            }
        }

        var map = new List<KeyValuePair<Vector2Int, GameObject>>(1024);
        GridOccupancy.CollectCellMap(map);
        var ghostObjects = new HashSet<GameObject>();
        for (int i = 0; i < map.Count; i++)
        {
            Vector2Int cell = map[i].Key;
            GameObject obj = map[i].Value;
            string problem = null;
            if (obj == null)
                problem = "призрак: объект уничтожен";
            else
            {
                BuildingBase b = obj.GetComponent<BuildingBase>();
                if (b != null && !b.IsPlaced)
                    problem = "призрак: " + obj.name + " не поставлен";
            }

            if (problem == null)
                continue;
            issues++;
            if (issues <= 25)
                sb.Append('\n').Append("клетка ").Append(CellText(cell)).Append(": ").Append(problem).Append("  ").Append(Btn("tp", "/tp " + cell.x + " " + cell.y));
            if (!fix)
                continue;
            if (obj == null)
                GridOccupancy.Unregister(cell);
            else
                ghostObjects.Add(obj);
            fixedCount++;
        }

        foreach (GameObject obj in ghostObjects)
            GridOccupancy.Unregister(obj);

        if (issues == 0)
            return Ok("сетка в порядке: " + map.Count + " занятых клеток");
        if (issues > 25)
            sb.Append("\n… и ещё ").Append(issues - 25);
        string head = "проблем: " + issues + (fix ? ", исправлено " + fixedCount : "  " + Btn("исправить", "/validate grid fix"));
        return Warn(head + sb);
    }

    [DevCommand("count", "buildings [<type>]", "сколько построек по типам; с типом — где они")]
    static string Count(DevArgs a)
    {
        if (a[0] != "buildings")
            return UsageOf(a);
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        if (a.Has(1))
        {
            BuildingData data = GameDatabase.FindBuilding(a.Raw(1));
            if (data == null)
                return Err("нет здания " + a.Raw(1));
            var sb = new StringBuilder(256);
            int n = 0;
            for (int i = 0; i < all.Count; i++)
            {
                BuildingBase b = all[i];
                if (b == null || !b.IsPlaced || b.data != data)
                    continue;
                n++;
                if (n <= 20)
                    sb.Append('\n').Append(CellText(CellOf(b))).Append("  ").Append(TpBtn(b));
            }

            if (n > 20)
                sb.Append("\n… и ещё ").Append(n - 20);
            return data.id + ": " + n + sb;
        }

        var counts = new Dictionary<string, int>();
        int total = 0;
        for (int i = 0; i < all.Count; i++)
        {
            BuildingBase b = all[i];
            if (b == null || !b.IsPlaced)
                continue;
            string id = Name(b);
            counts.TryGetValue(id, out int n);
            counts[id] = n + 1;
            total++;
        }

        var rows = new List<KeyValuePair<string, int>>(counts);
        rows.Sort((x, y) => y.Value != x.Value ? y.Value.CompareTo(x.Value) : string.CompareOrdinal(x.Key, y.Key));
        var text = new StringBuilder(512);
        text.Append("всего ").Append(total);
        for (int i = 0; i < rows.Count; i++)
            text.Append('\n').Append(rows[i].Value.ToString().PadLeft(5)).Append("  ").Append(rows[i].Key);
        return text.ToString();
    }
}
