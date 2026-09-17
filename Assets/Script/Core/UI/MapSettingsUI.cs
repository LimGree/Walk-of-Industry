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
        parent.Add(IndustryUi.Text("A", UiLocale.T("settings.map_appearance"), "settings-group"));
        parent.Add(SettingsControls.SliderRow("settings.minimap_zoom", 0.06f, 1f, () => MapSettings.MiniZoom, v => MapSettings.MiniZoom = v, v => Mathf.RoundToInt(v * 100f)));
        parent.Add(SettingsControls.SliderRow("settings.minimap_size", 140f, 400f, () => MapSettings.MiniSize, v => MapSettings.MiniSize = v, v => Mathf.RoundToInt(v)));
        parent.Add(SettingsControls.SliderRow("settings.mini_opacity", 0.25f, 1f, () => MapSettings.MiniOpacity, v => MapSettings.MiniOpacity = v, v => Mathf.RoundToInt(v * 100f)));
        parent.Add(SettingsControls.ChipRow("settings.mini_shape",
            (UiLocale.T("settings.mini_square"), () => !MapSettings.MiniRound, () => MapSettings.MiniRound = false),
            (UiLocale.T("settings.mini_round"), () => MapSettings.MiniRound, () => MapSettings.MiniRound = true)));
        parent.Add(SettingsControls.ChipRow("settings.mini_corner",
            (UiLocale.T("settings.corner_tl"), () => MapSettings.MiniCorner == 0, () => MapSettings.MiniCorner = 0),
            (UiLocale.T("settings.corner_tr"), () => MapSettings.MiniCorner == 1, () => MapSettings.MiniCorner = 1),
            (UiLocale.T("settings.corner_bl"), () => MapSettings.MiniCorner == 2, () => MapSettings.MiniCorner = 2),
            (UiLocale.T("settings.corner_br"), () => MapSettings.MiniCorner == 3, () => MapSettings.MiniCorner = 3)));
        parent.Add(SettingsControls.Toggle("settings.mini_visible", () => MapSettings.MiniVisible, v => MapSettings.MiniVisible = v));

        parent.Add(IndustryUi.Text("R", UiLocale.T("settings.map_rotation"), "settings-group"));
        parent.Add(SettingsControls.ChipRow("settings.mini_orient",
            (UiLocale.T("settings.mini_north"), () => !MapSettings.MiniFollow, () => MapSettings.MiniFollow = false),
            (UiLocale.T("settings.mini_follow"), () => MapSettings.MiniFollow, () => MapSettings.MiniFollow = true)));
        parent.Add(SettingsControls.Toggle("settings.mini_compass", () => MapSettings.MiniCompass, v => MapSettings.MiniCompass = v));

        parent.Add(IndustryUi.Text("I", UiLocale.T("settings.map_info"), "settings-group"));
        parent.Add(SettingsControls.Toggle("settings.mini_coords", () => MapSettings.MiniCoords, v => MapSettings.MiniCoords = v));
        parent.Add(SettingsControls.Toggle("settings.mini_biome", () => MapSettings.MiniBiome, v => MapSettings.MiniBiome = v));
        parent.Add(SettingsControls.Toggle("settings.clock_visible", () => GameSettings.ClockVisible, v => GameSettings.ClockVisible = v));

        parent.Add(IndustryUi.Text("W", UiLocale.T("settings.map_waypoints"), "settings-group"));
        parent.Add(SettingsControls.Toggle("settings.mini_waypoints", () => MapSettings.MiniWaypoints, v => MapSettings.MiniWaypoints = v));
        parent.Add(SettingsControls.Toggle("settings.map_holograms", () => MapSettings.Holograms, v => MapSettings.Holograms = v));

        parent.Add(IndustryUi.Text("G", UiLocale.T("settings.map_grid"), "settings-group"));
        parent.Add(SettingsControls.Toggle("settings.mini_grid", () => MapSettings.MiniGrid, v => MapSettings.MiniGrid = v));
    }

    public static void FillWorld(VisualElement parent)
    {
        if (parent == null)
            return;
        parent.Add(IndustryUi.Text("B", UiLocale.T("settings.map_behaviour"), "settings-group"));
        parent.Add(SettingsControls.Toggle("settings.world_pause", () => MapSettings.WorldPause, v => MapSettings.WorldPause = v));
        parent.Add(SettingsControls.Toggle("settings.world_compass", () => MapSettings.WorldCompass, v => MapSettings.WorldCompass = v));
        parent.Add(SettingsControls.Toggle("settings.world_grid", () => MapSettings.WorldGrid, v => MapSettings.WorldGrid = v));
        parent.Add(SettingsControls.Toggle("settings.map_holograms", () => MapSettings.Holograms, v => MapSettings.Holograms = v));
        parent.Add(SettingsControls.Toggle("settings.map_hide_unexplored", () => MapSettings.HideUnexplored, v => MapSettings.HideUnexplored = v));
        parent.Add(SettingsControls.SliderRow("settings.map_reveal", 8f, 72f, () => MapSettings.RevealRadius, v => MapSettings.RevealRadius = Mathf.RoundToInt(v), v => Mathf.RoundToInt(v)));
        parent.Add(SettingsControls.Toggle("settings.map_allow_teleport", () => MapSettings.AllowTeleport, v => MapSettings.AllowTeleport = v));
        parent.Add(IndustryUi.Text("Hint", UiLocale.T("settings.map_world_hint"), "muted"));
    }
}
