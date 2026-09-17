using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

public static class SettingsHub
{
    public static string CurrentTab { get; private set; } = "general";

    public static void Fill(VisualElement parent, Action onBack, string tab = "general")
    {
        if (parent == null)
            return;

        parent.Clear();
        parent.style.width = 720;
        parent.Add(IndustryUi.Text("T", UiLocale.T("settings.title"), "title-hero"));

        var tabs = IndustryUi.El("Tabs", "settings-tabs");
        var pages = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);

        AddTab(tabs, buttons, "general", UiLocale.T("settings.tab_general"));
        AddTab(tabs, buttons, "controls", UiLocale.T("settings.tab_controls"));
        AddTab(tabs, buttons, "sound", UiLocale.T("settings.tab_sound"));
        AddTab(tabs, buttons, "map", UiLocale.T("settings.tab_map"));
        AddTab(tabs, buttons, "minimap", UiLocale.T("settings.tab_minimap"));
        parent.Add(tabs);

        var general = Page("General");
        FillGeneral(general);
        parent.Add(general);
        pages["general"] = general;

        var controls = Page("Controls");
        KeybindSettingsUI.Fill(controls, null, true);
        parent.Add(controls);
        pages["controls"] = controls;

        var sound = Page("Sound");
        GameAudio.AddMixerSliders(sound);
        parent.Add(sound);
        pages["sound"] = sound;

        var map = Page("Map");
        MapSettingsUI.FillWorld(map);
        parent.Add(map);
        pages["map"] = map;

        var mini = Page("Mini");
        MapSettingsUI.FillMinimap(mini);
        parent.Add(mini);
        pages["minimap"] = mini;

        void Show(string id)
        {
            if (!pages.ContainsKey(id))
                id = "general";
            CurrentTab = id;
            foreach (var pair in pages)
                IndustryUi.Show(pair.Value, pair.Key == id);
            foreach (var pair in buttons)
                IndustryUi.SetOn(pair.Value, pair.Key == id, "is-selected");
        }

        foreach (var pair in buttons)
        {
            string id = pair.Key;
            pair.Value.clicked += () => Show(id);
        }

        Show(tab);
        if (onBack != null)
            parent.Add(IndustryUi.Btn(UiLocale.T("menu.back"), onBack, "btn-ghost"));
    }

    static void AddTab(VisualElement row, Dictionary<string, Button> buttons, string id, string label)
    {
        Button b = SettingsControls.Chip(label, null);
        row.Add(b);
        buttons[id] = b;
    }

    static VisualElement Page(string name)
    {
        var scroll = new ScrollView { name = name };
        scroll.AddToClassList("scroll");
        scroll.AddToClassList("settings-page");
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scroll.style.maxHeight = 520;
        return scroll;
    }

    static void FillGeneral(VisualElement parent)
    {
        parent.Add(IndustryUi.Text("G0", UiLocale.T("settings.general"), "settings-group"));
        parent.Add(UiLocale.LanguageRow());
        Button hints = null;
        hints = IndustryUi.Btn("", () =>
        {
            InputHintUI.HintsEnabled = !InputHintUI.HintsEnabled;
            IndustryUi.SetButtonLabel(hints, InputHintUI.HintsEnabled
                ? UiLocale.T("settings.hints_on")
                : UiLocale.T("settings.hints_off"));
        });
        IndustryUi.SetButtonLabel(hints, InputHintUI.HintsEnabled
            ? UiLocale.T("settings.hints_on")
            : UiLocale.T("settings.hints_off"));
        parent.Add(hints);

        parent.Add(IndustryUi.Text("G1", UiLocale.T("settings.gfx"), "settings-group"));
        parent.Add(SettingsControls.SliderRow("settings.world_light", 0.15f, 2.5f,
            () => GameSettings.WorldLight, v => GameSettings.WorldLight = v, v => v.ToString("0.00")));
        parent.Add(SettingsControls.SliderRow("settings.render_distance", GameSettings.ObjectDistMin, GameSettings.ObjectDistMax,
            () => GameSettings.RenderDistance, v => GameSettings.RenderDistance = v, v => Mathf.RoundToInt(v)));
        parent.Add(SettingsControls.Toggle("settings.fog", () => GameSettings.FogEnabled, v => GameSettings.FogEnabled = v));
        parent.Add(SettingsControls.SliderRow("settings.fog_start", 1f, 2000f,
            () => GameSettings.FogStart, v => GameSettings.FogStart = v, v => Mathf.RoundToInt(v)));
        parent.Add(SettingsControls.SliderRow("settings.fog_end", 1f, 4000f,
            () => GameSettings.FogEnd, v => GameSettings.FogEnd = v, v => Mathf.RoundToInt(v)));
        parent.Add(SettingsControls.SliderRow("settings.brightness", 0.35f, 2f,
            () => GameSettings.Brightness, v => GameSettings.Brightness = v, v => v.ToString("0.00")));

        parent.Add(IndustryUi.Text("G1b", UiLocale.T("settings.daynight"), "settings-group"));
        parent.Add(SettingsControls.Toggle("settings.daynight_on", () => GameSettings.DayNightEnabled, v => GameSettings.DayNightEnabled = v));
        parent.Add(SettingsControls.SliderRow("settings.day_length", 6f, 48f,
            () => GameSettings.DayLengthMinutes, v => GameSettings.DayLengthMinutes = v, v => Mathf.RoundToInt(v)));
        parent.Add(SettingsControls.SliderRow("settings.world_hour", 0f, 24f,
            () => GameSettings.WorldHour, v => GameSettings.WorldHour = v, v => DayNight.FormatHour(v)));
        parent.Add(SettingsControls.Toggle("settings.clock_visible", () => GameSettings.ClockVisible, v => GameSettings.ClockVisible = v));
        parent.Add(SettingsControls.ChipRow("settings.clock_format",
            (UiLocale.T("settings.clock_h"), () => GameSettings.ClockFormat == 0, () => GameSettings.ClockFormat = 0),
            (UiLocale.T("settings.clock_hm"), () => GameSettings.ClockFormat == 1, () => GameSettings.ClockFormat = 1),
            (UiLocale.T("settings.clock_hms"), () => GameSettings.ClockFormat == 2, () => GameSettings.ClockFormat = 2)));
        parent.Add(SettingsControls.Toggle("settings.clock_day", () => GameSettings.ClockShowDay, v => GameSettings.ClockShowDay = v));

        parent.Add(IndustryUi.Text("G1c", UiLocale.T("settings.weather"), "settings-group"));
        parent.Add(SettingsControls.Toggle("settings.weather_auto", () => GameSettings.WeatherAuto, v => GameSettings.WeatherAuto = v));
        parent.Add(SettingsControls.ChipRow("settings.weather_kind",
            (UiLocale.T("settings.weather_clear"), () => GameSettings.WeatherKindIndex == 0, () => GameSettings.WeatherKindIndex = 0),
            (UiLocale.T("settings.weather_rain"), () => GameSettings.WeatherKindIndex == 1, () => GameSettings.WeatherKindIndex = 1),
            (UiLocale.T("settings.weather_storm"), () => GameSettings.WeatherKindIndex == 2, () => GameSettings.WeatherKindIndex = 2)));

        string[] qualityNames = QualitySettings.names;
        if (qualityNames != null && qualityNames.Length > 0)
        {
            var chips = new (string, Func<bool>, Action)[qualityNames.Length];
            for (int i = 0; i < qualityNames.Length; i++)
            {
                int idx = i;
                chips[i] = (qualityNames[i], () => GameSettings.Quality == idx, () => GameSettings.Quality = idx);
            }
            parent.Add(SettingsControls.ChipRow("settings.quality", chips));
        }

        parent.Add(IndustryUi.Text("G2", UiLocale.T("settings.display"), "settings-group"));
        parent.Add(SettingsControls.ChipRow("settings.display_mode",
            (UiLocale.T("settings.fullscreen"), () => GameSettings.DisplayMode == 0, () => GameSettings.DisplayMode = 0),
            (UiLocale.T("settings.borderless"), () => GameSettings.DisplayMode == 1, () => GameSettings.DisplayMode = 1),
            (UiLocale.T("settings.windowed"), () => GameSettings.DisplayMode == 2, () => GameSettings.DisplayMode = 2)));

        List<string> resolutions = GameSettings.ResolutionChoices(out int selected);
        parent.Add(SettingsControls.Dropdown("settings.resolution", resolutions, selected, label =>
        {
            if (GameSettings.TryParseResolution(label, out int w, out int h))
                GameSettings.SetResolution(w, h);
        }));

        parent.Add(SettingsControls.Toggle("settings.vsync", () => GameSettings.VSync, v => GameSettings.VSync = v));
        parent.Add(SettingsControls.ChipRow("settings.fps_cap",
            (UiLocale.T("settings.fps_unlimited"), () => GameSettings.FpsCap == 0, () => GameSettings.FpsCap = 0),
            ("30", () => GameSettings.FpsCap == 30, () => GameSettings.FpsCap = 30),
            ("60", () => GameSettings.FpsCap == 60, () => GameSettings.FpsCap = 60),
            ("120", () => GameSettings.FpsCap == 120, () => GameSettings.FpsCap = 120),
            ("144", () => GameSettings.FpsCap == 144, () => GameSettings.FpsCap = 144)));

        parent.Add(IndustryUi.Text("G3", UiLocale.T("settings.files"), "settings-group"));
        parent.Add(IndustryUi.Btn(UiLocale.T("settings.open_worlds"), OpenWorldsFolder, "btn-primary"));
    }

    static void OpenWorldsFolder()
    {
        string path = WorldCatalog.WorldsFolder;
        try
        {
            System.IO.Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[Settings] Не удалось открыть папку миров: " + e.Message);
        }
    }
}
