using System.Collections.Generic;
using UnityEngine;

/// <summary>Игрок и камера: телепорт, позиция, noclip, скорость, вид камеры, HUD.</summary>
public static partial class DevCommands
{
    [DevCommand("tp", "<x> <z>", "телепорт на клетку")]
    [DevCommand("tp", "home", "к лаборатории (база)")]
    [DevCommand("tp", "biome <biome>", "к ближайшему биому")]
    [DevCommand("tp", "cluster <cluster>", "к ближайшему кластеру")]
    [DevCommand("tp", "veins <vein>", "к ближайшей жиле")]
    static string Tp(DevArgs a)
    {
        PlayerMovement move = Player();
        if (move == null)
            return Err("no player");
        Vector2Int cell;
        if (a.TryFloat(0, out float x) && a.TryFloat(1, out float z))
            cell = new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(z));
        else if (a[0] == "home")
        {
            if (!HomeCell(out cell))
                return Err("лаборатории нет — некуда");
        }
        else if (a[0] == "biome")
        {
            if (!TryParseBiome(a.Raw(1), out WorldBiome biome))
                return UsageOf(a);
            if (!NearestBiome(biome, out cell))
                return Err("no cell");
        }
        else if (a[0] == "cluster")
        {
            if (!NearestCluster(a.Raw(1), out cell))
                return Err("no cluster " + a.Raw(1));
        }
        else if (a[0] == "veins" || a[0] == "vein")
        {
            if (!NearestVein(a.Raw(1), out cell))
                return Err("no vein " + a.Raw(1));
        }
        else
            return UsageOf(a);

        if (DevRuntime.Cam == DevRuntime.CamMode.Free)
            DevRuntime.SetCam(DevRuntime.CamMode.Player);
        move.TeleportToCell(cell);
        return Ok("tp " + CellText(cell));
    }

    static bool HomeCell(out Vector2Int cell)
    {
        cell = default;
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] is ResearchLab lab && lab != null && lab.IsPlaced)
            {
                cell = NearFreeCell(lab);
                return true;
            }
        }

        return false;
    }

    [DevCommand("pos", "", "координаты, клетка, биом; /tp на это место — в буфер обмена")]
    static string Pos(DevArgs a)
    {
        PlayerMovement move = Player();
        if (move == null)
            return Err("no player");
        Vector3 p = move.transform.position;
        Vector2Int cell = BuildingLinker.WorldToCell(p);
        WorldBiome biome = WorldBiomeMap.Instance != null ? WorldBiomeMap.Instance.Get(cell) : WorldBiome.Field;
        string tp = "/tp " + cell.x + " " + cell.y;
        GUIUtility.systemCopyBuffer = tp;
        return "позиция " + F(p.x, "0.0") + ", " + F(p.y, "0.0") + ", " + F(p.z, "0.0")
            + " · клетка " + CellText(cell) + " · биом " + biome + " · взгляд " + F(Mathf.Repeat(move.transform.eulerAngles.y, 360f), "0") + "°"
            + "\nв буфере: " + tp;
    }

    [DevCommand("noclip", "[on|off]", "полёт сквозь всё: WASD, Пробел — вверх, Ctrl/C — вниз, бег — быстрее")]
    static string Noclip(DevArgs a)
    {
        if (a.Has(0) && !a.Is(0, "on", "off"))
            return UsageOf(a);
        PlayerMovement.DevNoclip = a.Has(0) ? a[0] == "on" : !PlayerMovement.DevNoclip;
        return Ok("noclip " + (PlayerMovement.DevNoclip ? "on" : "off"));
    }

    [DevCommand("speed", "[<x>]", "множитель скорости игрока (и noclip, и свободной камеры)")]
    static string Speed(DevArgs a)
    {
        if (a.Has(0))
        {
            if (!a.TryFloat(0, out float mul) || mul <= 0f)
                return UsageOf(a);
            PlayerMovement.DevSpeedMul = Mathf.Clamp(mul, 0.1f, 50f);
        }

        return Ok("speed ×" + F(PlayerMovement.DevSpeedMul));
    }

    [DevCommand("cam", "first|third|front", "камера игрока: от первого лица, из-за плеча, спереди")]
    [DevCommand("cam", "free", "свободная камера: WASD, E/Q — вверх/вниз, Shift — быстрее; игрок стоит")]
    [DevCommand("cam", "top [<h>]", "вид сверху на высоте h (по умолчанию 24)")]
    static string Cam(DevArgs a)
    {
        PlayerMovement move = Player();
        switch (a[0])
        {
            case "first":
            case "third":
            case "front":
                DevRuntime.SetCam(DevRuntime.CamMode.Player);
                if (move != null)
                {
                    move.SetView(a[0] == "first" ? PlayerMovement.CameraView.First
                        : a[0] == "third" ? PlayerMovement.CameraView.ThirdBack
                        : PlayerMovement.CameraView.ThirdFront);
                }

                return Ok("камера: " + a[0]);
            case "free":
                return DevRuntime.SetCam(DevRuntime.CamMode.Free) ? Ok("свободная камера — закрой консоль и лети; назад: /cam first") : Err("нет камеры");
            case "top":
                float h = -1f;
                if (a.Has(1) && !a.TryFloat(1, out h))
                    return UsageOf(a);
                return DevRuntime.SetCam(DevRuntime.CamMode.Top, h) ? Ok("вид сверху, высота " + F(DevRuntime.TopHeight, "0")) : Err("нет камеры");
            default:
                return UsageOf(a);
        }
    }

    [DevCommand("hud", "off|on", "спрятать весь интерфейс (кроме консоли) для скриншотов")]
    static string Hud(DevArgs a)
    {
        if (!a.Is(0, "on", "off"))
            return UsageOf(a);
        int n = DevRuntime.SetHud(a[0] == "on");
        return Ok(a[0] == "on" ? "HUD показан" : "HUD скрыт (" + n + " панелей) · вернуть: /hud on");
    }
}
