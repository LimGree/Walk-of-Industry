using UnityEngine;

/// <summary>
/// Расширенные настройки (ревизия настроек, осень 2026): графика, HUD, стройка, правила мира,
/// управление, звук, доступность. Всё в PlayerPrefs, общее для всех миров.
/// Каждый сеттер зовёт Changed — подписчики сами перечитывают нужное.
/// </summary>
public static partial class GameSettings
{
    // ===== Графика =====

    /// <summary>0 — выкл, 1 — низкие, 2 — высокие.</summary>
    public static int Shadows { get => GetI("GfxShadows", 2, 0, 2); set => SetI("GfxShadows", value, 0, 2, true); }

    /// <summary>0 — выкл, 1 — FXAA, 2 — SMAA, 3 — MSAA ×2, 4 — MSAA ×4.</summary>
    public static int AntiAliasing { get => GetI("GfxAa", 0, 0, 4); set => SetI("GfxAa", value, 0, 4, true); }

    /// <summary>Доля разрешения, в котором рисуется 3D (0.5–1). Интерфейс всегда в полном разрешении.</summary>
    public static float RenderScale { get => GetF("GfxRenderScale", 1f, 0.5f, 1f); set => SetF("GfxRenderScale", value, 0.5f, 1f); }

    /// <summary>Дальность предметов на лентах, не больше дальности объектов.</summary>
    public static float BeltItemDistance { get => GetF("GfxBeltItems", ObjectDistMax, ObjectDistMin, ObjectDistMax); set => SetF("GfxBeltItems", value, ObjectDistMin, ObjectDistMax); }

    public static bool Bloom { get => GetB("GfxBloom", false); set => SetB("GfxBloom", value); }
    public static bool Vignette { get => GetB("GfxVignette", false); set => SetB("GfxVignette", value); }

    /// <summary>Сила размытия в движении, 0 — выкл.</summary>
    public static float MotionBlur { get => GetF("GfxMotionBlur", 0f, 0f, 1f); set => SetF("GfxMotionBlur", value, 0f, 1f); }

    /// <summary>Гамма 0.6–1.6, 1 — как задумано.</summary>
    public static float Gamma { get => GetF("GfxGamma", 1f, 0.6f, 1.6f); set => SetF("GfxGamma", value, 0.6f, 1.6f); }

    public static float HeadBob { get => GetF("GfxHeadBob", 0.3f, 0f, 1f); set => SetF("GfxHeadBob", value, 0f, 1f); }
    public static float CameraShake { get => GetF("GfxShake", 1f, 0f, 1f); set => SetF("GfxShake", value, 0f, 1f); }

    /// <summary>Сдвиг камеры от 3-го лица вбок, м: минус — влево, плюс — вправо (за плечом).</summary>
    public static float ThirdPersonSide { get => GetF("CamThirdSide", 0.75f, -1.5f, 1.5f); set => SetF("CamThirdSide", value, -1.5f, 1.5f); }

    /// <summary>Номер монитора из Screen.GetDisplayLayout.</summary>
    public static int Monitor { get => GetI("GfxMonitor", 0, 0, 8); set => SetI("GfxMonitor", value, 0, 8, false); }

    /// <summary>FPS, когда окно не в фокусе: 0 — как обычно.</summary>
    public static int BackgroundFps { get => GetI("GfxBgFps", 30, 0, 60); set => SetI("GfxBgFps", value, 0, 60, false); }

    /// <summary>0 — выкл, 1 — FPS, 2 — FPS + мс кадра.</summary>
    public static int FpsCounter { get => GetI("GfxFpsCounter", 0, 0, 2); set => SetI("GfxFpsCounter", value, 0, 2, false); }

    // ===== Интерфейс =====

    /// <summary>0 — инверт-квадрат, 1 — точка, 2 — крест, 3 — круг.</summary>
    public static int CrosshairStyle { get => GetI("UiCrossStyle", 0, 0, 3); set => SetI("UiCrossStyle", value, 0, 3, false); }
    public static float CrosshairSize { get => GetF("UiCrossSize", 1f, 0.5f, 2f); set => SetF("UiCrossSize", value, 0.5f, 2f); }

    /// <summary>Индекс в CrosshairColors.</summary>
    public static int CrosshairColor { get => GetI("UiCrossColor", 0, 0, CrosshairColors.Length - 1); set => SetI("UiCrossColor", value, 0, CrosshairColors.Length - 1, false); }

    public static readonly Color[] CrosshairColors =
    {
        new Color(1f, 1f, 1f, 0.95f),
        new Color(0.84f, 0.58f, 0.24f, 1f),
        new Color(0.45f, 0.9f, 0.55f, 1f),
        new Color(0.35f, 0.85f, 1f, 1f),
        new Color(1f, 0.35f, 0.32f, 1f),
    };

    public static float HudOpacity { get => GetF("UiHudOpacity", 1f, 0.4f, 1f); set => SetF("UiHudOpacity", value, 0.4f, 1f); }

    /// <summary>Через сколько секунд без действий прятать хотбар; 0 — не прятать.</summary>
    public static int HideHudSeconds { get => GetI("UiHideHud", 0, 0, 60); set => SetI("UiHideHud", value, 0, 60, false); }

    public static bool HotbarLabel { get => GetB("UiHotbarLabel", true); set => SetB("UiHotbarLabel", value); }

    /// <summary>0 — справа сверху (под балансом), 1 — справа снизу, 2 — по центру сверху.</summary>
    public static int NotifyPosition { get => GetI("UiNotifyPos", 0, 0, 2); set => SetI("UiNotifyPos", value, 0, 2, false); }

    public static bool NotifyBreakdowns { get => GetB("UiNotifyBreak", true); set => SetB("UiNotifyBreak", value); }
    public static bool NotifyResearch { get => GetB("UiNotifyResearch", true); set => SetB("UiNotifyResearch", value); }
    public static bool NotifyAchievements { get => GetB("UiNotifyAch", true); set => SetB("UiNotifyAch", value); }
    public static bool NotifyResources { get => GetB("UiNotifyRes", true); set => SetB("UiNotifyRes", value); }

    /// <summary>0 — полные, 1 — уменьшенные, 2 — выкл.</summary>
    public static int UiAnimations { get => GetI("UiAnim", 0, 0, 2); set => SetI("UiAnim", value, 0, 2, false); }

    /// <summary>0 — янтарный, 1 — синий, 2 — зелёный, 3 — красный.</summary>
    public static int AccentColor { get => GetI("UiAccent", 0, 0, 3); set => SetI("UiAccent", value, 0, 3, false); }

    // ===== Строительство =====

    /// <summary>0 — только в режиме стройки, 1 — всегда, 2 — выкл.</summary>
    public static int BuildGridMode { get => GetI("BuildGrid", 0, 0, 2); set => SetI("BuildGrid", value, 0, 2, false); }
    public static bool IoArrows { get => GetB("BuildIoArrows", true); set => SetB("BuildIoArrows", value); }
    public static float GhostOpacity { get => GetF("BuildGhostAlpha", 0.55f, 0.3f, 0.9f); set => SetF("BuildGhostAlpha", value, 0.3f, 0.9f); }

    /// <summary>С какого числа зданий удаление выделенного требует удержания; 0 — никогда.</summary>
    public static int ConfirmMassDelete { get => GetI("BuildConfirmMass", 10, 0, 100); set => SetI("BuildConfirmMass", value, 0, 100, false); }

    /// <summary>0 — всегда, 1 — только в режиме стройки, 2 — выкл.</summary>
    public static int IdleIcons { get => GetI("BuildIdleIcons", 0, 0, 2); set => SetI("BuildIdleIcons", value, 0, 2, false); }
    public static bool BreakdownMarkersXray { get => GetB("BuildBreakXray", true); set => SetB("BuildBreakXray", value); }

    // ===== Игра =====

    public static bool SaveOnQuit { get => GetB("GameSaveOnQuit", true); set => SetB("GameSaveOnQuit", value); }
    public static bool AutosaveNotice { get => GetB("GameSaveNotice", true); set => SetB("GameSaveNotice", value); }
    public static bool PauseOnUnfocus { get => GetB("GamePauseUnfocus", false); set => SetB("GamePauseUnfocus", value); }
    public static bool TutorialSkip { get => GetB("GameTutorialSkip", false); set => SetB("GameTutorialSkip", value); }

    /// <summary>0 — редко, 1 — норм, 2 — часто.</summary>
    public static int BreakdownRate { get => GetI("GameBreakRate", 1, 0, 2); set => SetI("GameBreakRate", value, 0, 2, false); }
    public static float BreakdownRateMultiplier => BreakdownRate == 0 ? 0.5f : BreakdownRate == 2 ? 2f : 1f;

    /// <summary>Множитель скорости исследований: 0.5 / 1 / 2.</summary>
    public static float ResearchSpeed { get => GetF("GameResearchSpeed", 1f, 0.5f, 2f); set => SetF("GameResearchSpeed", value, 0.5f, 2f); }

    /// <summary>Множитель цен на постройку: 0.5 / 1 / 2.</summary>
    public static float CostMultiplier { get => GetF("GameCostMul", 1f, 0.5f, 2f); set => SetF("GameCostMul", value, 0.5f, 2f); }

    /// <summary>Чит: стройка бесплатная.</summary>
    public static bool Sandbox { get => GetB("GameSandbox", false); set => SetB("GameSandbox", value); }

    /// <summary>Чит: скорость игры 1 / 2 / 4.</summary>
    public static int GameSpeed { get => GetI("GameSpeed", 1, 1, 4); set { SetI("GameSpeed", value, 1, 4, false); ApplyTimeScale(); } }

    public static bool StormDamage { get => GetB("GameStormDamage", false); set => SetB("GameStormDamage", value); }

    /// <summary>Time.timeScale для «идёт игра» с учётом паузы и скорости.</summary>
    public static void ApplyTimeScale()
    {
        bool paused = GameManager.Instance != null && GameManager.Instance.IsPaused;
        Time.timeScale = paused ? 0f : GameSpeed;
    }

    // ===== Управление =====

    public static float ZoomSensitivity { get => GetF("CtlZoomSens", 0.6f, 0.2f, 1.5f); set => SetF("CtlZoomSens", value, 0.2f, 1.5f); }
    public static bool MouseSmoothing { get => GetB("CtlMouseSmooth", false); set => SetB("CtlMouseSmooth", value); }
    public static bool InvertX { get => GetB("CtlInvertX", false); set => SetB("CtlInvertX", value); }
    public static bool SprintToggle { get => GetB("CtlSprintToggle", false); set => SetB("CtlSprintToggle", value); }
    public static bool ZoomToggle { get => GetB("CtlZoomToggle", false); set => SetB("CtlZoomToggle", value); }
    public static bool InvertHotbarWheel { get => GetB("CtlInvertWheel", false); set => SetB("CtlInvertWheel", value); }
    public static bool ShiftWheelRotate { get => GetB("CtlShiftWheelRot", true); set => SetB("CtlShiftWheelRot", value); }

    // ===== Звук =====

    public static bool AudioInBackground { get => GetB("SndBackground", true); set => SetB("SndBackground", value); }
    public static bool PauseMutesWorld { get => GetB("SndPauseMute", true); set => SetB("SndPauseMute", value); }

    /// <summary>0 — без пауз, 1 — короткие, 2 — длинные.</summary>
    public static int MusicGap { get => GetI("SndMusicGap", 1, 0, 2); set => SetI("SndMusicGap", value, 0, 2, false); }
    public static bool MonoAudio { get => GetB("SndMono", false); set { SetB("SndMono", value); ApplyMono(); } }

    static bool speakerCaptured;
    static AudioSpeakerMode speakerOriginal;

    public static void ApplyMono()
    {
        AudioConfiguration cfg = AudioSettings.GetConfiguration();
        if (!speakerCaptured)
        {
            // Запоминаем режим проекта, чтобы при выключении моно вернуть именно его.
            speakerOriginal = cfg.speakerMode == AudioSpeakerMode.Mono ? AudioSpeakerMode.Stereo : cfg.speakerMode;
            speakerCaptured = true;
        }
        AudioSpeakerMode want = MonoAudio ? AudioSpeakerMode.Mono : speakerOriginal;
        if (cfg.speakerMode == want)
            return;
        cfg.speakerMode = want;
        AudioSettings.Reset(cfg);
    }

    // ===== Доступность =====

    /// <summary>0 — выкл, 1 — протанопия, 2 — дейтеранопия, 3 — тританопия.</summary>
    public static int ColorblindMode { get => GetI("A11yColorblind", 0, 0, 3); set => SetI("A11yColorblind", value, 0, 3, false); }
    public static bool NoFlashes { get => GetB("A11yNoFlash", false); set => SetB("A11yNoFlash", value); }
    public static bool HighContrast { get => GetB("A11yContrast", false); set => SetB("A11yContrast", value); }
    public static bool Captions { get => GetB("A11yCaptions", false); set => SetB("A11yCaptions", value); }

    /// <summary>Сколько секунд держать клавишу для подтверждения опасного действия.</summary>
    public static float HoldTime { get => GetF("A11yHoldTime", 1f, 0.4f, 2.5f); set => SetF("A11yHoldTime", value, 0.4f, 2.5f); }

    // ===== helpers =====

    static int GetI(string key, int fallback, int min, int max) => Mathf.Clamp(PlayerPrefs.GetInt(key, fallback), min, max);
    static float GetF(string key, float fallback, float min, float max) => Mathf.Clamp(PlayerPrefs.GetFloat(key, fallback), min, max);
    static bool GetB(string key, bool fallback) => PlayerPrefs.GetInt(key, fallback ? 1 : 0) != 0;

    static void SetI(string key, int value, int min, int max, bool apply)
    {
        SetInt(key, Mathf.Clamp(value, min, max));
        if (apply)
            Apply();
        else
            Changed?.Invoke();
    }

    static void SetF(string key, float value, float min, float max)
    {
        SetFloat(key, Mathf.Clamp(value, min, max));
        Changed?.Invoke();
    }

    static void SetB(string key, bool value)
    {
        SetInt(key, value ? 1 : 0);
        Changed?.Invoke();
    }
}
