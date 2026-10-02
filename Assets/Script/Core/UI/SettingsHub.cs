using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Экран настроек: вкладки слева столбиком, справа заголовок вкладки и прокручиваемые группы строк.
/// Вкладки: Экран и графика · Интерфейс · Мир · Строительство · Игра · Управление · Звук · Карта · Миникарта · Доступность.
/// </summary>
public static class SettingsHub
{
    public static string CurrentTab { get; private set; } = "display";

    /// <summary>
    /// Прячет/показывает окно, в котором сидят настройки (пауза). Нужно режиму перетаскивания миникарты.
    /// null — хоста нет (главное меню), тогда кнопка перетаскивания не показывается.
    /// </summary>
    public static Action<bool> HostVisibility;

    static readonly (string id, string title, string desc)[] Tabs =
    {
        ("display", "settings.tab_display", "settings.tab_display_desc"),
        ("interface", "settings.tab_interface", "settings.tab_interface_desc"),
        ("world", "settings.tab_world", "settings.tab_world_desc"),
        ("build", "settings.tab_build", "settings.tab_build_desc"),
        ("game", "settings.tab_game", "settings.tab_game_desc"),
        ("controls", "settings.tab_controls", "settings.tab_controls_desc"),
        ("sound", "settings.tab_sound", "settings.tab_sound_desc"),
        ("map", "settings.tab_map", "settings.tab_map_desc"),
        ("minimap", "settings.tab_minimap", "settings.tab_minimap_desc"),
        ("a11y", "settings.tab_a11y", "settings.tab_a11y_desc"),
    };

    public static void Fill(VisualElement parent, Action onBack, string tab = "display")
    {
        if (parent == null)
            return;

        parent.Clear();
        parent.style.width = StyleKeyword.Null;

        var header = IndustryUi.El("Header", "set-header");
        header.Add(IndustryUi.Text("T", UiLocale.T("settings.title"), "set-title"));
        header.Add(IndustryUi.El("Spacer", "grow"));
        header.Add(IndustryUi.Text("Live", UiLocale.T("settings.live_apply"), "set-live"));
        parent.Add(header);

        var body = IndustryUi.El("Body", "set-body");
        var nav = IndustryUi.El("Nav", "set-nav");
        var content = IndustryUi.El("Content", "set-content");
        body.Add(nav);
        body.Add(content);
        parent.Add(body);

        var pageTitle = IndustryUi.Text("PageTitle", "", "set-page-title");
        var pageDesc = IndustryUi.Text("PageDesc", "", "set-page-desc");
        content.Add(pageTitle);
        content.Add(pageDesc);

        var pages = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        var buttons = new Dictionary<string, VisualElement>(StringComparer.Ordinal);
        foreach (var t in Tabs)
        {
            VisualElement btn = NavButton(t.title, t.desc);
            nav.Add(btn);
            buttons[t.id] = btn;
            VisualElement page = Page(t.id);
            FillTab(t.id, page);
            content.Add(page);
            pages[t.id] = page;
        }

        void Show(string id)
        {
            if (!pages.ContainsKey(id))
                id = Tabs[0].id;
            CurrentTab = id;
            foreach (var pair in pages)
                IndustryUi.Show(pair.Value, pair.Key == id);
            foreach (var pair in buttons)
                IndustryUi.SetOn(pair.Value, pair.Key == id, "is-selected");
            foreach (var t in Tabs)
            {
                if (t.id != id)
                    continue;
                pageTitle.text = UiLocale.T(t.title);
                pageDesc.text = UiLocale.T(t.desc);
            }
        }

        foreach (var pair in buttons)
        {
            string id = pair.Key;
            pair.Value.AddManipulator(new Clickable(() =>
            {
                if (CurrentTab != id)
                    UiAudio.PlayToggle();
                Show(id);
            }));
        }

        Show(string.IsNullOrEmpty(tab) ? CurrentTab : tab);

        var footer = IndustryUi.El("Footer", "set-footer");
        if (onBack != null)
            footer.Add(IndustryUi.Btn(UiLocale.T("menu.back"), onBack, "btn-ghost", "set-back"));
        parent.Add(footer);
    }

    static VisualElement NavButton(string titleKey, string descKey)
    {
        var btn = IndustryUi.El("Nav_" + titleKey, "set-nav-btn");
        btn.Add(IndustryUi.El("Bar", "set-nav-bar"));
        var col = IndustryUi.El("Col", "set-nav-text");
        col.Add(IndustryUi.Text("T", UiLocale.T(titleKey), "set-nav-title"));
        col.Add(IndustryUi.Text("D", UiLocale.T(descKey), "set-nav-desc"));
        btn.Add(col);
        return btn;
    }

    static VisualElement Page(string name)
    {
        var scroll = new ScrollView { name = "Page_" + name };
        scroll.AddToClassList("set-page");
        scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        return scroll;
    }

    static void FillTab(string id, VisualElement page)
    {
        switch (id)
        {
            case "display":
                FillDisplay(page);
                break;
            case "interface":
                FillInterface(page);
                break;
            case "world":
                FillWorld(page);
                break;
            case "build":
                FillBuild(page);
                break;
            case "game":
                FillGame(page);
                break;
            case "controls":
                FillControls(page);
                break;
            case "sound":
                GameAudio.AddMixerSliders(page);
                break;
            case "map":
                MapSettingsUI.FillWorld(page);
                break;
            case "minimap":
                MapSettingsUI.FillMinimap(page);
                break;
            case "a11y":
                FillAccessibility(page);
                break;
        }
    }

    static string M(float v) => Mathf.RoundToInt(v) + " " + UiLocale.T("settings.unit_m");
    static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

    static void FillDisplay(VisualElement parent)
    {
        parent.Add(SettingsControls.Group("settings.display"));
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

        List<string> monitors = SettingsRuntime.MonitorNames();
        if (monitors.Count > 1)
        {
            var chips = new (string, Func<bool>, Action)[monitors.Count];
            for (int i = 0; i < monitors.Count; i++)
            {
                int idx = i;
                chips[i] = (monitors[i], () => GameSettings.Monitor == idx, () => GameSettings.Monitor = idx);
            }
            parent.Add(SettingsControls.ChipRow("settings.monitor", chips));
        }

        parent.Add(SettingsControls.Toggle("settings.vsync", () => GameSettings.VSync, v => GameSettings.VSync = v));
        parent.Add(SettingsControls.ChipRow("settings.fps_cap",
            (UiLocale.T("settings.fps_unlimited"), () => GameSettings.FpsCap == 0, () => GameSettings.FpsCap = 0),
            ("30", () => GameSettings.FpsCap == 30, () => GameSettings.FpsCap = 30),
            ("60", () => GameSettings.FpsCap == 60, () => GameSettings.FpsCap = 60),
            ("120", () => GameSettings.FpsCap == 120, () => GameSettings.FpsCap = 120),
            ("144", () => GameSettings.FpsCap == 144, () => GameSettings.FpsCap = 144)));
        parent.Add(SettingsControls.Describe(SettingsControls.ChipRow("settings.bg_fps",
            (UiLocale.T("settings.bg_fps_same"), () => GameSettings.BackgroundFps == 0, () => GameSettings.BackgroundFps = 0),
            ("15", () => GameSettings.BackgroundFps == 15, () => GameSettings.BackgroundFps = 15),
            ("30", () => GameSettings.BackgroundFps == 30, () => GameSettings.BackgroundFps = 30)),
            "settings.bg_fps_desc"));
        parent.Add(SettingsControls.ChipRow("settings.fps_counter",
            (UiLocale.T("settings.off"), () => GameSettings.FpsCounter == 0, () => GameSettings.FpsCounter = 0),
            ("FPS", () => GameSettings.FpsCounter == 1, () => GameSettings.FpsCounter = 1),
            (UiLocale.T("settings.fps_detailed"), () => GameSettings.FpsCounter == 2, () => GameSettings.FpsCounter = 2)));

        parent.Add(SettingsControls.Group("settings.gfx"));
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
        parent.Add(SettingsControls.ChipRow("settings.shadows",
            (UiLocale.T("settings.off"), () => GameSettings.Shadows == 0, () => GameSettings.Shadows = 0),
            (UiLocale.T("settings.low"), () => GameSettings.Shadows == 1, () => GameSettings.Shadows = 1),
            (UiLocale.T("settings.high"), () => GameSettings.Shadows == 2, () => GameSettings.Shadows = 2)));
        parent.Add(SettingsControls.ChipRow("settings.aa",
            (UiLocale.T("settings.off"), () => GameSettings.AntiAliasing == 0, () => GameSettings.AntiAliasing = 0),
            ("FXAA", () => GameSettings.AntiAliasing == 1, () => GameSettings.AntiAliasing = 1),
            ("SMAA", () => GameSettings.AntiAliasing == 2, () => GameSettings.AntiAliasing = 2),
            ("MSAA ×2", () => GameSettings.AntiAliasing == 3, () => GameSettings.AntiAliasing = 3),
            ("MSAA ×4", () => GameSettings.AntiAliasing == 4, () => GameSettings.AntiAliasing = 4)));
        parent.Add(SettingsControls.Describe(SettingsControls.SliderRow("settings.render_scale", 0.5f, 1f,
            () => GameSettings.RenderScale, v => GameSettings.RenderScale = Mathf.Round(v * 20f) / 20f, v => Pct(v)),
            "settings.render_scale_desc"));
        parent.Add(SettingsControls.Describe(SettingsControls.SliderRow("settings.render_distance", GameSettings.ObjectDistMin, GameSettings.ObjectDistMax,
            () => GameSettings.RenderDistance, v => GameSettings.RenderDistance = v, v => M(v)),
            "settings.render_distance_desc"));
        parent.Add(SettingsControls.Describe(SettingsControls.SliderRow("settings.belt_items", GameSettings.ObjectDistMin, GameSettings.ObjectDistMax,
            () => GameSettings.BeltItemDistance, v => GameSettings.BeltItemDistance = v, v => M(v)),
            "settings.belt_items_desc"));
        parent.Add(SettingsControls.SliderRow("settings.fov", GameSettings.FovMin, GameSettings.FovMax,
            () => GameSettings.FieldOfView, v => GameSettings.FieldOfView = Mathf.Round(v), v => Mathf.RoundToInt(v) + "°"));
        parent.Add(SettingsControls.ChipRow("settings.building_fx",
            (UiLocale.T("settings.fx_off"), () => GameSettings.BuildingFxQuality == 0, () => GameSettings.BuildingFxQuality = 0),
            (UiLocale.T("settings.fx_low"), () => GameSettings.BuildingFxQuality == 1, () => GameSettings.BuildingFxQuality = 1),
            (UiLocale.T("settings.fx_full"), () => GameSettings.BuildingFxQuality == 2, () => GameSettings.BuildingFxQuality = 2)));

        parent.Add(SettingsControls.Group("settings.light_fog"));
        parent.Add(SettingsControls.Describe(SettingsControls.SliderRow("settings.world_light", 0.15f, 2.5f,
            () => GameSettings.WorldLight, v => GameSettings.WorldLight = v, v => v.ToString("0.00")),
            "settings.world_light_desc"));
        parent.Add(SettingsControls.Describe(SettingsControls.SliderRow("settings.brightness", 0.35f, 2f,
            () => GameSettings.Brightness, v => GameSettings.Brightness = v, v => v.ToString("0.00")),
            "settings.brightness_desc"));
        parent.Add(SettingsControls.Describe(SettingsControls.SliderRow("settings.gamma", 0.6f, 1.6f,
            () => GameSettings.Gamma, v => GameSettings.Gamma = Mathf.Round(v * 20f) / 20f, v => v.ToString("0.00")),
            "settings.gamma_desc"));
        parent.Add(SettingsControls.Toggle("settings.fog", () => GameSettings.FogEnabled, v => GameSettings.FogEnabled = v));
        parent.Add(SettingsControls.SliderRow("settings.fog_start", 1f, 2000f,
            () => GameSettings.FogStart, v => GameSettings.FogStart = v, v => M(v)));
        parent.Add(SettingsControls.SliderRow("settings.fog_end", 1f, 4000f,
            () => GameSettings.FogEnd, v => GameSettings.FogEnd = v, v => M(v)));

        parent.Add(SettingsControls.Group("settings.effects"));
        parent.Add(SettingsControls.Toggle("settings.bloom", () => GameSettings.Bloom, v => GameSettings.Bloom = v));
        parent.Add(SettingsControls.Toggle("settings.vignette", () => GameSettings.Vignette, v => GameSettings.Vignette = v));
        parent.Add(SettingsControls.SliderRow("settings.motion_blur", 0f, 1f,
            () => GameSettings.MotionBlur, v => GameSettings.MotionBlur = Mathf.Round(v * 20f) / 20f, v => v <= 0.001f ? UiLocale.T("settings.off") : Pct(v)));
        parent.Add(SettingsControls.SliderRow("settings.head_bob", 0f, 1f,
            () => GameSettings.HeadBob, v => GameSettings.HeadBob = Mathf.Round(v * 20f) / 20f, v => v <= 0.001f ? UiLocale.T("settings.off") : Pct(v)));
        parent.Add(SettingsControls.SliderRow("settings.camera_shake", 0f, 1f,
            () => GameSettings.CameraShake, v => GameSettings.CameraShake = Mathf.Round(v * 20f) / 20f, v => v <= 0.001f ? UiLocale.T("settings.off") : Pct(v)));
    }

    static void FillInterface(VisualElement parent)
    {
        parent.Add(SettingsControls.Group("settings.interface"));
        parent.Add(SettingsControls.ChipRow("settings.language",
            ("Русский", () => UiLocale.IsRu, () => UiLocale.Set(UiLocale.Ru)),
            ("English", () => !UiLocale.IsRu, () => UiLocale.Set(UiLocale.En))));
        parent.Add(SettingsControls.Describe(
            SettingsControls.Toggle("settings.hints", () => InputHintUI.HintsEnabled, v => InputHintUI.HintsEnabled = v),
            "settings.hints_desc"));
        parent.Add(SettingsControls.SliderRow("settings.ui_scale", 0.8f, 1.3f,
            () => GameSettings.UiScale, v => GameSettings.UiScale = Mathf.Round(v * 20f) / 20f, v => Pct(v)));
        parent.Add(SettingsControls.ChipRow("settings.ui_anim",
            (UiLocale.T("settings.anim_full"), () => GameSettings.UiAnimations == 0, () => GameSettings.UiAnimations = 0),
            (UiLocale.T("settings.anim_reduced"), () => GameSettings.UiAnimations == 1, () => GameSettings.UiAnimations = 1),
            (UiLocale.T("settings.off"), () => GameSettings.UiAnimations == 2, () => GameSettings.UiAnimations = 2)));
        parent.Add(SettingsControls.ChipRow("settings.accent",
            (UiLocale.T("settings.accent_amber"), () => GameSettings.AccentColor == 0, () => GameSettings.AccentColor = 0),
            (UiLocale.T("settings.accent_blue"), () => GameSettings.AccentColor == 1, () => GameSettings.AccentColor = 1),
            (UiLocale.T("settings.accent_green"), () => GameSettings.AccentColor == 2, () => GameSettings.AccentColor = 2),
            (UiLocale.T("settings.accent_red"), () => GameSettings.AccentColor == 3, () => GameSettings.AccentColor = 3)));

        parent.Add(SettingsControls.Group("settings.hud"));
        parent.Add(SettingsControls.SliderRow("settings.hud_opacity", 0.4f, 1f,
            () => GameSettings.HudOpacity, v => GameSettings.HudOpacity = Mathf.Round(v * 20f) / 20f, v => Pct(v)));
        parent.Add(SettingsControls.Describe(SettingsControls.ChipRow("settings.hide_hud",
            (UiLocale.T("settings.never"), () => GameSettings.HideHudSeconds == 0, () => GameSettings.HideHudSeconds = 0),
            (UiLocale.T("settings.sec_n", 5), () => GameSettings.HideHudSeconds == 5, () => GameSettings.HideHudSeconds = 5),
            (UiLocale.T("settings.sec_n", 10), () => GameSettings.HideHudSeconds == 10, () => GameSettings.HideHudSeconds = 10),
            (UiLocale.T("settings.sec_n", 20), () => GameSettings.HideHudSeconds == 20, () => GameSettings.HideHudSeconds = 20)),
            "settings.hide_hud_desc"));
        parent.Add(SettingsControls.Toggle("settings.hotbar_label", () => GameSettings.HotbarLabel, v => GameSettings.HotbarLabel = v));

        parent.Add(SettingsControls.Group("settings.crosshair"));
        parent.Add(SettingsControls.ChipRow("settings.cross_style",
            (UiLocale.T("settings.cross_invert"), () => GameSettings.CrosshairStyle == 0, () => GameSettings.CrosshairStyle = 0),
            (UiLocale.T("settings.cross_dot"), () => GameSettings.CrosshairStyle == 1, () => GameSettings.CrosshairStyle = 1),
            (UiLocale.T("settings.cross_cross"), () => GameSettings.CrosshairStyle == 2, () => GameSettings.CrosshairStyle = 2),
            (UiLocale.T("settings.cross_circle"), () => GameSettings.CrosshairStyle == 3, () => GameSettings.CrosshairStyle = 3)));
        parent.Add(SettingsControls.SliderRow("settings.cross_size", 0.5f, 2f,
            () => GameSettings.CrosshairSize, v => GameSettings.CrosshairSize = Mathf.Round(v * 20f) / 20f, v => Pct(v)));
        parent.Add(SettingsControls.Describe(SettingsControls.ChipRow("settings.cross_color",
            (UiLocale.T("settings.color_white"), () => GameSettings.CrosshairColor == 0, () => GameSettings.CrosshairColor = 0),
            (UiLocale.T("settings.accent_amber"), () => GameSettings.CrosshairColor == 1, () => GameSettings.CrosshairColor = 1),
            (UiLocale.T("settings.accent_green"), () => GameSettings.CrosshairColor == 2, () => GameSettings.CrosshairColor = 2),
            (UiLocale.T("settings.color_cyan"), () => GameSettings.CrosshairColor == 3, () => GameSettings.CrosshairColor = 3),
            (UiLocale.T("settings.accent_red"), () => GameSettings.CrosshairColor == 4, () => GameSettings.CrosshairColor = 4)),
            "settings.cross_color_desc"));

        parent.Add(SettingsControls.Group("settings.notifications"));
        parent.Add(SettingsControls.ChipRow("settings.notify_pos",
            (UiLocale.T("settings.notify_under_wallet"), () => GameSettings.NotifyPosition == 0, () => GameSettings.NotifyPosition = 0),
            (UiLocale.T("settings.corner_br"), () => GameSettings.NotifyPosition == 1, () => GameSettings.NotifyPosition = 1),
            (UiLocale.T("settings.notify_top_center"), () => GameSettings.NotifyPosition == 2, () => GameSettings.NotifyPosition = 2)));
        parent.Add(SettingsControls.Toggle("settings.notify_breakdowns", () => GameSettings.NotifyBreakdowns, v => GameSettings.NotifyBreakdowns = v));
        parent.Add(SettingsControls.Toggle("settings.notify_research", () => GameSettings.NotifyResearch, v => GameSettings.NotifyResearch = v));
        parent.Add(SettingsControls.Toggle("settings.notify_achievements", () => GameSettings.NotifyAchievements, v => GameSettings.NotifyAchievements = v));
        parent.Add(SettingsControls.Toggle("settings.notify_resources", () => GameSettings.NotifyResources, v => GameSettings.NotifyResources = v));
    }

    static void FillWorld(VisualElement parent)
    {
        parent.Add(SettingsControls.Group("settings.daynight"));
        parent.Add(SettingsControls.Describe(
            SettingsControls.Toggle("settings.daynight_on", () => GameSettings.DayNightEnabled, v => GameSettings.DayNightEnabled = v),
            "settings.daynight_desc"));

        parent.Add(SettingsControls.Group("settings.weather"));
        parent.Add(SettingsControls.Describe(
            SettingsControls.Toggle("settings.weather_auto", () => GameSettings.WeatherAuto, v => GameSettings.WeatherAuto = v),
            "settings.weather_auto_desc"));
    }

    static void FillBuild(VisualElement parent)
    {
        parent.Add(SettingsControls.Group("settings.build_helpers"));
        parent.Add(SettingsControls.ChipRow("settings.build_grid",
            (UiLocale.T("settings.grid_build"), () => GameSettings.BuildGridMode == 0, () => GameSettings.BuildGridMode = 0),
            (UiLocale.T("settings.grid_always"), () => GameSettings.BuildGridMode == 1, () => GameSettings.BuildGridMode = 1),
            (UiLocale.T("settings.off"), () => GameSettings.BuildGridMode == 2, () => GameSettings.BuildGridMode = 2)));
        parent.Add(SettingsControls.Toggle("settings.io_arrows", () => GameSettings.IoArrows, v => GameSettings.IoArrows = v));
        parent.Add(SettingsControls.SliderRow("settings.ghost_opacity", 0.3f, 0.9f,
            () => GameSettings.GhostOpacity, v => GameSettings.GhostOpacity = Mathf.Round(v * 20f) / 20f, v => Pct(v)));

        parent.Add(SettingsControls.Group("settings.build_status"));
        parent.Add(SettingsControls.ChipRow("settings.idle_icons",
            (UiLocale.T("settings.always"), () => GameSettings.IdleIcons == 0, () => GameSettings.IdleIcons = 0),
            (UiLocale.T("settings.grid_build"), () => GameSettings.IdleIcons == 1, () => GameSettings.IdleIcons = 1),
            (UiLocale.T("settings.off"), () => GameSettings.IdleIcons == 2, () => GameSettings.IdleIcons = 2)));
        parent.Add(SettingsControls.Toggle("settings.break_xray", () => GameSettings.BreakdownMarkersXray, v => GameSettings.BreakdownMarkersXray = v));

        parent.Add(SettingsControls.Group("settings.build_safety"));
        parent.Add(SettingsControls.Describe(SettingsControls.ChipRow("settings.confirm_mass",
            (UiLocale.T("settings.never"), () => GameSettings.ConfirmMassDelete == 0, () => GameSettings.ConfirmMassDelete = 0),
            (UiLocale.T("settings.from_n", 5), () => GameSettings.ConfirmMassDelete == 5, () => GameSettings.ConfirmMassDelete = 5),
            (UiLocale.T("settings.from_n", 10), () => GameSettings.ConfirmMassDelete == 10, () => GameSettings.ConfirmMassDelete = 10),
            (UiLocale.T("settings.from_n", 25), () => GameSettings.ConfirmMassDelete == 25, () => GameSettings.ConfirmMassDelete = 25)),
            "settings.confirm_mass_desc"));
    }

    static void FillGame(VisualElement parent)
    {
        parent.Add(SettingsControls.Group("settings.saves"));
        parent.Add(SettingsControls.ChipRow("settings.autosave",
            (UiLocale.T("settings.off"), () => GameSettings.AutosaveMinutes == 0, () => GameSettings.AutosaveMinutes = 0),
            (UiLocale.T("settings.min_n", 1), () => GameSettings.AutosaveMinutes == 1, () => GameSettings.AutosaveMinutes = 1),
            (UiLocale.T("settings.min_n", 2), () => GameSettings.AutosaveMinutes == 2, () => GameSettings.AutosaveMinutes = 2),
            (UiLocale.T("settings.min_n", 5), () => GameSettings.AutosaveMinutes == 5, () => GameSettings.AutosaveMinutes = 5),
            (UiLocale.T("settings.min_n", 10), () => GameSettings.AutosaveMinutes == 10, () => GameSettings.AutosaveMinutes = 10)));
        parent.Add(SettingsControls.Toggle("settings.autosave_notice", () => GameSettings.AutosaveNotice, v => GameSettings.AutosaveNotice = v));
        parent.Add(SettingsControls.Describe(
            SettingsControls.Toggle("settings.save_on_quit", () => GameSettings.SaveOnQuit, v => GameSettings.SaveOnQuit = v),
            "settings.save_on_quit_desc"));
        parent.Add(SettingsControls.Toggle("settings.pause_unfocus", () => GameSettings.PauseOnUnfocus, v => GameSettings.PauseOnUnfocus = v));
        parent.Add(SettingsControls.ActionRow("settings.worlds_folder", "settings.open_worlds", OpenWorldsFolder));

        parent.Add(SettingsControls.Group("settings.tutorial"));
        parent.Add(SettingsControls.Toggle("settings.tutorial_skip", () => GameSettings.TutorialSkip, v => GameSettings.TutorialSkip = v));
        if (TutorialSystem.Instance != null && WorldCatalog.HasActive)
        {
            parent.Add(SettingsControls.ActionRow("settings.tutorial_restart", "settings.tutorial_restart_btn", () =>
            {
                UiModal.Confirm(
                    UiLocale.T("settings.tutorial_restart"),
                    UiLocale.T("settings.tutorial_restart_body"),
                    UiLocale.T("settings.tutorial_restart_btn"),
                    () =>
                    {
                        if (TutorialSystem.Instance != null)
                            TutorialSystem.Instance.Restart();
                        if (GameManager.Instance != null)
                            GameManager.Instance.SetPaused(false);
                    },
                    danger: false);
            }));
        }

        parent.Add(SettingsControls.Group("settings.mechanics"));
        parent.Add(SettingsControls.Describe(
            SettingsControls.Toggle("settings.breakdowns_on", () => GameSettings.BreakdownsEnabled, v => GameSettings.BreakdownsEnabled = v),
            "settings.breakdowns_desc"));
        parent.Add(SettingsControls.ChipRow("settings.breakdown_rate",
            (UiLocale.T("settings.rate_rare"), () => GameSettings.BreakdownRate == 0, () => GameSettings.BreakdownRate = 0),
            (UiLocale.T("settings.rate_normal"), () => GameSettings.BreakdownRate == 1, () => GameSettings.BreakdownRate = 1),
            (UiLocale.T("settings.rate_often"), () => GameSettings.BreakdownRate == 2, () => GameSettings.BreakdownRate = 2)));
        parent.Add(SettingsControls.Describe(
            SettingsControls.Toggle("settings.storm_damage", () => GameSettings.StormDamage, v => GameSettings.StormDamage = v),
            "settings.storm_damage_desc"));
        parent.Add(SettingsControls.Describe(SettingsControls.ChipRow("settings.research_speed",
            ("×0.5", () => GameSettings.ResearchSpeed < 0.75f, () => GameSettings.ResearchSpeed = 0.5f),
            ("×1", () => GameSettings.ResearchSpeed >= 0.75f && GameSettings.ResearchSpeed < 1.5f, () => GameSettings.ResearchSpeed = 1f),
            ("×2", () => GameSettings.ResearchSpeed >= 1.5f, () => GameSettings.ResearchSpeed = 2f)),
            "settings.research_speed_desc"));
        parent.Add(SettingsControls.ChipRow("settings.cost_mult",
            ("×0.5", () => GameSettings.CostMultiplier < 0.75f, () => GameSettings.CostMultiplier = 0.5f),
            ("×1", () => GameSettings.CostMultiplier >= 0.75f && GameSettings.CostMultiplier < 1.5f, () => GameSettings.CostMultiplier = 1f),
            ("×2", () => GameSettings.CostMultiplier >= 1.5f, () => GameSettings.CostMultiplier = 2f)));

        parent.Add(SettingsControls.Group("settings.cheats"));
        parent.Add(SettingsControls.CheatToggle("settings.sandbox", "settings.sandbox_cheat_body",
            () => GameSettings.Sandbox, v => GameSettings.Sandbox = v, "settings.sandbox_desc"));
        VisualElement speed = null;
        void SetSpeed(int value)
        {
            if (value == 1 || GameSettings.GameSpeed > 1)
            {
                GameSettings.GameSpeed = value;
                return;
            }
            SettingsControls.ConfirmCheat("settings.speed_cheat_body", () =>
            {
                GameSettings.GameSpeed = value;
                SettingsControls.Sync(speed);
            });
        }
        speed = SettingsControls.ChipRow("settings.game_speed",
            ("×1", () => GameSettings.GameSpeed == 1, () => SetSpeed(1)),
            ("×2", () => GameSettings.GameSpeed == 2, () => SetSpeed(2)),
            ("×4", () => GameSettings.GameSpeed == 4, () => SetSpeed(4)));
        speed.AddToClassList("is-cheat");
        parent.Add(SettingsControls.Describe(speed, "settings.game_speed_desc"));
    }

    static void FillControls(VisualElement parent)
    {
        parent.Add(SettingsControls.Group("settings.mouse"));
        parent.Add(SettingsControls.SliderRow("settings.mouse_sens", 0.1f, 3f,
            () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = Mathf.Round(v * 20f) / 20f, v => v.ToString("0.00") + "×"));
        parent.Add(SettingsControls.Describe(SettingsControls.SliderRow("settings.zoom_sens", 0.2f, 1.5f,
            () => GameSettings.ZoomSensitivity, v => GameSettings.ZoomSensitivity = Mathf.Round(v * 20f) / 20f, v => v.ToString("0.00") + "×"),
            "settings.zoom_sens_desc"));
        parent.Add(SettingsControls.Toggle("settings.mouse_smooth", () => GameSettings.MouseSmoothing, v => GameSettings.MouseSmoothing = v));
        parent.Add(SettingsControls.Toggle("settings.invert_x", () => GameSettings.InvertX, v => GameSettings.InvertX = v));
        parent.Add(SettingsControls.Toggle("settings.invert_y", () => GameSettings.InvertY, v => GameSettings.InvertY = v));

        parent.Add(SettingsControls.Group("settings.movement"));
        parent.Add(SettingsControls.ChipRow("settings.sprint_mode",
            (UiLocale.T("settings.mode_hold"), () => !GameSettings.SprintToggle, () => GameSettings.SprintToggle = false),
            (UiLocale.T("settings.mode_toggle"), () => GameSettings.SprintToggle, () => GameSettings.SprintToggle = true)));
        parent.Add(SettingsControls.ChipRow("settings.zoom_mode",
            (UiLocale.T("settings.mode_hold"), () => !GameSettings.ZoomToggle, () => GameSettings.ZoomToggle = false),
            (UiLocale.T("settings.mode_toggle"), () => GameSettings.ZoomToggle, () => GameSettings.ZoomToggle = true)));

        parent.Add(SettingsControls.Group("settings.wheel"));
        parent.Add(SettingsControls.Toggle("settings.invert_wheel", () => GameSettings.InvertHotbarWheel, v => GameSettings.InvertHotbarWheel = v));
        parent.Add(SettingsControls.Toggle("settings.shift_wheel", () => GameSettings.ShiftWheelRotate, v => GameSettings.ShiftWheelRotate = v));

        parent.Add(SettingsControls.Group("settings.keybinds"));
        var keys = IndustryUi.El("Keys", "set-keys");
        KeybindSettingsUI.Fill(keys, null, true);
        parent.Add(keys);
    }

    static void FillAccessibility(VisualElement parent)
    {
        parent.Add(SettingsControls.Group("settings.a11y_vision"));
        parent.Add(SettingsControls.Describe(SettingsControls.ChipRow("settings.colorblind",
            (UiLocale.T("settings.off"), () => GameSettings.ColorblindMode == 0, () => GameSettings.ColorblindMode = 0),
            (UiLocale.T("settings.cb_protan"), () => GameSettings.ColorblindMode == 1, () => GameSettings.ColorblindMode = 1),
            (UiLocale.T("settings.cb_deutan"), () => GameSettings.ColorblindMode == 2, () => GameSettings.ColorblindMode = 2),
            (UiLocale.T("settings.cb_tritan"), () => GameSettings.ColorblindMode == 3, () => GameSettings.ColorblindMode = 3)),
            "settings.colorblind_desc"));
        parent.Add(SettingsControls.Describe(
            SettingsControls.Toggle("settings.hi_contrast", () => GameSettings.HighContrast, v => GameSettings.HighContrast = v),
            "settings.hi_contrast_desc"));
        parent.Add(SettingsControls.Describe(
            SettingsControls.Toggle("settings.no_flash", () => GameSettings.NoFlashes, v => GameSettings.NoFlashes = v),
            "settings.no_flash_desc"));

        parent.Add(SettingsControls.Group("settings.a11y_sound"));
        parent.Add(SettingsControls.Describe(
            SettingsControls.Toggle("settings.captions", () => GameSettings.Captions, v => GameSettings.Captions = v),
            "settings.captions_desc"));

        parent.Add(SettingsControls.Group("settings.a11y_input"));
        parent.Add(SettingsControls.Describe(SettingsControls.SliderRow("settings.hold_time", 0.4f, 2.5f,
            () => GameSettings.HoldTime, v => GameSettings.HoldTime = Mathf.Round(v * 10f) / 10f, v => v.ToString("0.0") + " " + UiLocale.T("settings.unit_s")),
            "settings.hold_time_desc"));
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
