using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Живёт всю игру (DontDestroyOnLoad). Применяет то, что зависит от кадра, камеры или фокуса окна:
/// постобработка и масштаб рендера на Camera.main, фоновый FPS, пауза и звук при сворачивании,
/// выбор монитора, счётчик FPS, внешний вид интерфейса (UiLook).
/// </summary>
public class SettingsRuntime : MonoBehaviour
{
    public static SettingsRuntime Instance { get; private set; }

    /// <summary>Окно не в фокусе и «Звук в фоне» выключен.</summary>
    public static bool AudioMuted { get; private set; }

    Camera appliedCam;
    bool dirty = true;
    bool focused = true;
    int appliedMonitor = -1;

    VisualElement root;
    Label fpsLabel;
    float fpsTimer;
    int fpsFrames;
    float fpsWorst;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null)
            return;
        var go = new GameObject("SettingsRuntime");
        DontDestroyOnLoad(go);
        go.AddComponent<SettingsRuntime>();
    }

    void Awake()
    {
        Instance = this;
        GameSettings.Changed += MarkDirty;
        GameSettings.ApplyMono();
        // Окна прошлой сцены уничтожены — стек начинается заново.
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => UiStack.Clear();
    }

    void OnDestroy()
    {
        GameSettings.Changed -= MarkDirty;
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        root = IndustryUi.Mount(this, 990);
        fpsLabel = IndustryUi.Text("Fps", "", "fps-counter");
        fpsLabel.pickingMode = PickingMode.Ignore;
        root.Add(fpsLabel);
        SoundCaptions.Build(root);
        appliedMonitor = GameSettings.Monitor;
        MarkDirty();
    }

    void MarkDirty()
    {
        dirty = true;
    }

    void Update()
    {
        Camera cam = Camera.main;
        if (cam != appliedCam || dirty)
        {
            appliedCam = cam;
            dirty = false;
            PostFx.Apply(cam);
            RenderScaler.Apply(cam);
            UiLook.ApplyAll();
            SocketArrow.RefreshFromSettings();
            ApplyFocusRules();
            ApplyMonitor();
        }
        TickFps();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        focused = hasFocus;
        ApplyFocusRules();
        if (!hasFocus && GameSettings.PauseOnUnfocus && GameManager.Instance != null
            && !GameManager.Instance.IsPaused && WorldCatalog.HasActive)
            GameManager.Instance.SetPaused(true);
    }

    void ApplyFocusRules()
    {
        Application.runInBackground = true;
        int bg = GameSettings.BackgroundFps;
        if (!focused && bg > 0)
            Application.targetFrameRate = bg;
        else
            Application.targetFrameRate = GameSettings.FpsCap <= 0 ? -1 : GameSettings.FpsCap;
        AudioMuted = !focused && !GameSettings.AudioInBackground;
        AudioListener.volume = AudioMuted ? 0f : GameAudio.GetBus("master");
    }

    void ApplyMonitor()
    {
        int want = GameSettings.Monitor;
        if (want == appliedMonitor)
            return;
        appliedMonitor = want;
        var displays = new List<DisplayInfo>();
        Screen.GetDisplayLayout(displays);
        if (want < 0 || want >= displays.Count)
            return;
        DisplayInfo info = displays[want];
        Screen.MoveMainWindowTo(info, Vector2Int.zero);
    }

    /// <summary>Имена мониторов для чипов настройки.</summary>
    public static List<string> MonitorNames()
    {
        var displays = new List<DisplayInfo>();
        Screen.GetDisplayLayout(displays);
        var names = new List<string>(displays.Count);
        for (int i = 0; i < displays.Count; i++)
            names.Add((i + 1) + ". " + (string.IsNullOrEmpty(displays[i].name) ? displays[i].width + "×" + displays[i].height : displays[i].name));
        return names;
    }

    void TickFps()
    {
        if (fpsLabel == null)
            return;
        int mode = GameSettings.FpsCounter;
        IndustryUi.Show(fpsLabel, mode > 0);
        if (mode == 0)
            return;
        float dt = Time.unscaledDeltaTime;
        fpsFrames++;
        fpsTimer += dt;
        fpsWorst = Mathf.Max(fpsWorst, dt);
        if (fpsTimer < 0.5f)
            return;
        float fps = fpsFrames / fpsTimer;
        fpsLabel.text = mode == 1
            ? Mathf.RoundToInt(fps) + " FPS"
            : Mathf.RoundToInt(fps) + " FPS · " + (fpsTimer / fpsFrames * 1000f).ToString("0.0") + " " + UiLocale.T("fps.ms")
                + " · " + UiLocale.T("fps.max") + " " + (fpsWorst * 1000f).ToString("0") + " " + UiLocale.T("fps.ms");
        fpsFrames = 0;
        fpsTimer = 0f;
        fpsWorst = 0f;
    }
}
