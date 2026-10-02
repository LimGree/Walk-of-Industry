using System;
using UnityEngine;
using UnityEngine.UIElements;

public static class MapSettingsUI
{
    public static void Fill(VisualElement parent)
    {
        FillMinimap(parent);
        FillWorld(parent);
    }

    public static void FillMinimap(VisualElement parent)
    {
        if (parent == null)
            return;
        parent.Add(SettingsControls.Group("settings.map_appearance"));
        parent.Add(SettingsControls.Toggle("settings.mini_visible", () => MapSettings.MiniVisible, v => MapSettings.MiniVisible = v));
        parent.Add(SettingsControls.SliderRow("settings.minimap_zoom", 0.06f, 1f, () => MapSettings.MiniZoom, v => MapSettings.MiniZoom = v, v => Mathf.RoundToInt(v * 100f)));
        parent.Add(SettingsControls.SliderRow("settings.minimap_size", 140f, 400f, () => MapSettings.MiniSize, v => MapSettings.MiniSize = v, v => Mathf.RoundToInt(v)));
        parent.Add(SettingsControls.SliderRow("settings.mini_opacity", 0.25f, 1f, () => MapSettings.MiniOpacity, v => MapSettings.MiniOpacity = v, v => Mathf.RoundToInt(v * 100f)));
        parent.Add(SettingsControls.ChipRow("settings.mini_shape",
            (UiLocale.T("settings.mini_square"), () => !MapSettings.MiniRound, () => MapSettings.MiniRound = false),
            (UiLocale.T("settings.mini_round"), () => MapSettings.MiniRound, () => MapSettings.MiniRound = true)));

        parent.Add(SettingsControls.Group("settings.mini_position"));
        VisualElement corner = SettingsControls.ChipRow("settings.mini_corner",
            (UiLocale.T("settings.corner_tl"), () => IsCorner(0), () => MapSettings.MiniCorner = 0),
            (UiLocale.T("settings.corner_tr"), () => IsCorner(1), () => MapSettings.MiniCorner = 1),
            (UiLocale.T("settings.corner_bl"), () => IsCorner(2), () => MapSettings.MiniCorner = 2),
            (UiLocale.T("settings.corner_br"), () => IsCorner(3), () => MapSettings.MiniCorner = 3));
        parent.Add(corner);
        if (SettingsHub.HostVisibility != null && WorldMapUI.Instance != null)
        {
            VisualElement drag = SettingsControls.ActionRow("settings.mini_drag", "settings.mini_drag_btn", () =>
            {
                WorldMapUI map = WorldMapUI.Instance;
                if (map == null)
                    return;
                SettingsHub.HostVisibility?.Invoke(false);
                map.BeginMiniDrag(() => SettingsHub.HostVisibility?.Invoke(true));
            });
            parent.Add(SettingsControls.Describe(drag, MapSettings.MiniCustom ? "settings.mini_drag_custom" : "settings.mini_drag_desc"));
        }

        parent.Add(SettingsControls.Group("settings.map_rotation"));
        parent.Add(SettingsControls.ChipRow("settings.mini_orient",
            (UiLocale.T("settings.mini_north"), () => !MapSettings.MiniFollow, () => MapSettings.MiniFollow = false),
            (UiLocale.T("settings.mini_follow"), () => MapSettings.MiniFollow, () => MapSettings.MiniFollow = true)));
        parent.Add(SettingsControls.Toggle("settings.mini_compass", () => MapSettings.MiniCompass, v => MapSettings.MiniCompass = v));

        parent.Add(SettingsControls.Group("settings.map_info"));
        parent.Add(SettingsControls.Toggle("settings.mini_coords", () => MapSettings.MiniCoords, v => MapSettings.MiniCoords = v));
        parent.Add(SettingsControls.Toggle("settings.mini_biome", () => MapSettings.MiniBiome, v => MapSettings.MiniBiome = v));
        parent.Add(SettingsControls.Toggle("settings.mini_waypoints", () => MapSettings.MiniWaypoints, v => MapSettings.MiniWaypoints = v));
        parent.Add(SettingsControls.Toggle("settings.mini_grid", () => MapSettings.MiniGrid, v => MapSettings.MiniGrid = v));

        parent.Add(SettingsControls.Group("settings.clock"));
        parent.Add(SettingsControls.Toggle("settings.clock_visible", () => GameSettings.ClockVisible, v => GameSettings.ClockVisible = v));
        parent.Add(SettingsControls.ChipRow("settings.clock_format",
            (UiLocale.T("settings.clock_h"), () => GameSettings.ClockFormat == 0, () => GameSettings.ClockFormat = 0),
            (UiLocale.T("settings.clock_hm"), () => GameSettings.ClockFormat == 1, () => GameSettings.ClockFormat = 1),
            (UiLocale.T("settings.clock_hms"), () => GameSettings.ClockFormat == 2, () => GameSettings.ClockFormat = 2)));
        parent.Add(SettingsControls.Toggle("settings.clock_day", () => GameSettings.ClockShowDay, v => GameSettings.ClockShowDay = v));
    }

    /// <summary>Фильтр жил по типу: чип на каждый тип руды, найденный в мире (только в игре).</summary>
    static void AddVeinFilter(VisualElement parent)
    {
        WorldResourceScatterer scatter = WorldResourceScatterer.Instance;
        if (scatter == null)
            return;
        var kinds = new System.Collections.Generic.List<string>();
        var veins = scatter.Veins;
        for (int i = 0; i < veins.Count; i++)
        {
            string label = veins[i].label;
            if (!string.IsNullOrEmpty(label) && !kinds.Contains(label))
                kinds.Add(label);
        }
        if (kinds.Count == 0)
            return;
        kinds.Sort(StringComparer.CurrentCulture);
        var chips = new (string, Func<bool>, Action)[kinds.Count];
        for (int i = 0; i < kinds.Count; i++)
        {
            string kind = kinds[i];
            chips[i] = (kind, () => !MapSettings.IsVeinHidden(kind), () => MapSettings.SetVeinHidden(kind, !MapSettings.IsVeinHidden(kind)));
        }
        parent.Add(SettingsControls.Describe(SettingsControls.ChipRow("settings.map_vein_filter", chips), "settings.map_vein_filter_desc"));
    }

    static bool IsCorner(int corner)
    {
        return !MapSettings.MiniCustom && MapSettings.MiniCorner == corner;
    }

    public static void FillWorld(VisualElement parent)
    {
        if (parent == null)
            return;
        parent.Add(SettingsControls.Group("settings.map_behaviour"));
        parent.Add(SettingsControls.Toggle("settings.world_pause", () => MapSettings.WorldPause, v => MapSettings.WorldPause = v));
        parent.Add(SettingsControls.Toggle("settings.world_compass", () => MapSettings.WorldCompass, v => MapSettings.WorldCompass = v));
        parent.Add(SettingsControls.Toggle("settings.world_grid", () => MapSettings.WorldGrid, v => MapSettings.WorldGrid = v));
        parent.Add(SettingsControls.Toggle("settings.map_hide_unexplored", () => MapSettings.HideUnexplored, v => MapSettings.HideUnexplored = v));
        parent.Add(SettingsControls.ChipRow("settings.map_open_zoom",
            (UiLocale.T("settings.zoom_last"), () => MapSettings.FullOpenZoom == 0, () => MapSettings.FullOpenZoom = 0),
            (UiLocale.T("settings.zoom_player"), () => MapSettings.FullOpenZoom == 1, () => MapSettings.FullOpenZoom = 1),
            (UiLocale.T("settings.zoom_all"), () => MapSettings.FullOpenZoom == 2, () => MapSettings.FullOpenZoom = 2)));

        parent.Add(SettingsControls.Group("settings.map_layers"));
        parent.Add(SettingsControls.Describe(
            SettingsControls.Toggle("settings.map_buildings", () => MapSettings.ShowBuildings, v => MapSettings.ShowBuildings = v),
            "settings.map_buildings_desc"));
        parent.Add(SettingsControls.Toggle("settings.map_drones", () => MapSettings.ShowDrones, v => MapSettings.ShowDrones = v));
        parent.Add(SettingsControls.Toggle("settings.map_veins", () => MapSettings.ShowVeins, v => MapSettings.ShowVeins = v));
        AddVeinFilter(parent);

        parent.Add(SettingsControls.Group("settings.map_waypoints"));
        parent.Add(SettingsControls.Toggle("settings.map_holograms", () => MapSettings.Holograms, v => MapSettings.Holograms = v));
        parent.Add(SettingsControls.ChipRow("settings.waypoint_color",
            (UiLocale.T("settings.color_cycle"), () => MapSettings.WaypointColor < 0, () => MapSettings.WaypointColor = -1),
            (UiLocale.T("settings.accent_red"), () => MapSettings.WaypointColor == 0, () => MapSettings.WaypointColor = 0),
            (UiLocale.T("settings.color_cyan"), () => MapSettings.WaypointColor == 1, () => MapSettings.WaypointColor = 1),
            (UiLocale.T("settings.accent_green"), () => MapSettings.WaypointColor == 2, () => MapSettings.WaypointColor = 2),
            (UiLocale.T("settings.color_yellow"), () => MapSettings.WaypointColor == 3, () => MapSettings.WaypointColor = 3),
            (UiLocale.T("settings.color_violet"), () => MapSettings.WaypointColor == 4, () => MapSettings.WaypointColor = 4),
            (UiLocale.T("settings.color_orange"), () => MapSettings.WaypointColor == 5, () => MapSettings.WaypointColor = 5)));

        parent.Add(SettingsControls.CheatToggle("settings.map_allow_teleport", "settings.teleport_cheat_body",
            () => MapSettings.AllowTeleport, v => MapSettings.AllowTeleport = v, "settings.teleport_desc"));
    }
}
