using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    const string Root = "Audio/";
    const string PrefPrefix = "AudioVol_";
    const float HoverGap = 0.08f;
    const float Ui2dVolume = 0.55f;
    const float WorldVolume = 0.8f;
    const float LoopVolume = 0.12f;
    const float MusicVolume = 0.28f;
    const float AmbientVolume = 0.22f;
    const float LoopRange = 16f;
    const float LoopHold = 0.45f;

    public static readonly string[] Buses =
    {
        "master", "ui", "world", "buildings", "music", "ambient", "player"
    };

    readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    readonly Dictionary<int, AudioSource> loops = new Dictionary<int, AudioSource>();
    readonly Dictionary<int, float> loopHoldUntil = new Dictionary<int, float>();
    readonly List<int> deadLoops = new List<int>(8);
    readonly Dictionary<string, float> mix = new Dictionary<string, float>();

    AudioSource ui;
    AudioSource music;
    AudioSource ambA;
    AudioSource ambB;
    AudioSource world2d;
    AudioSource weather;
    float nextHover;
    string musicKey;
    string ambKeyA;
    string ambKeyB;
    float ambW0;
    float ambW1;
    float ambForest;
    float ambField;
    float ambMountain;
    float ambWater;

    public static GameAudio Ensure()
    {
        if (Instance != null)
            return Instance;
        var go = new GameObject("GameAudio");
        DontDestroyOnLoad(go);
        return go.AddComponent<GameAudio>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        if (ui == null)
            Build();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MainMenu.MenuSceneName || scene.name == MainMenu.LoadingSceneName)
            PlayMusic("music/mus_menu");
        else
            StopMusic();
    }

    void Build()
    {
        ui = MakeSource("Ui", true, 0f);
        world2d = MakeSource("World2d", true, 0f);
        music = MakeSource("Music", true, 0f);
        music.loop = true;
        music.ignoreListenerPause = true;
        ambA = MakeSource("AmbientA", true, 0f);
        ambA.loop = true;
        ambB = MakeSource("AmbientB", true, 0f);
        ambB.loop = true;
        weather = MakeSource("Weather", true, 0f);
        weather.loop = true;
        LoadFolder("ui");
        LoadFolder("world");
        LoadFolder("buildings");
        LoadFolder("music");
        LoadFolder("ambient");
        LoadFolder("player");
        LoadMix();
        ApplyMix();
    }

    void LoadMix()
    {
        mix.Clear();
        for (int i = 0; i < Buses.Length; i++)
            mix[Buses[i]] = PlayerPrefs.GetFloat(PrefPrefix + Buses[i], 1f);
    }

    public static float GetBus(string bus)
    {
        GameAudio audio = Ensure();
        if (audio.mix.Count == 0)
            audio.LoadMix();
        return audio.mix.TryGetValue(bus, out float v) ? Mathf.Clamp01(v) : 1f;
    }

    public static void SetBus(string bus, float value)
    {
        GameAudio audio = Ensure();
        audio.mix[bus] = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefPrefix + bus, audio.mix[bus]);
        PlayerPrefs.Save();
        audio.ApplyMix();
    }

    void ApplyMix()
    {
        AudioListener.volume = GetBus("master");
        if (music != null)
            music.volume = MusicVolume * GetBus("music");
        float ambBus = AmbientVolume * GetBus("ambient");
        if (ambA != null)
            ambA.volume = ambBus * ambW0;
        if (ambB != null)
            ambB.volume = ambBus * ambW1;
        if (weather != null && weather.isPlaying)
            weather.volume = Mathf.Max(weather.volume, 0f);
        foreach (var pair in loops)
        {
            if (pair.Value != null)
                pair.Value.volume = LoopVolume * GetBus("buildings");
        }
    }

    public static void AddMixerSliders(VisualElement parent)
    {
        if (parent == null)
            return;
        parent.Add(IndustryUi.Text("Audio", UiLocale.T("settings.audio"), "settings-group"));
        AddSlider(parent, "master", "settings.vol_master");
        AddSlider(parent, "ui", "settings.vol_ui");
        AddSlider(parent, "world", "settings.vol_world");
        AddSlider(parent, "buildings", "settings.vol_buildings");
        AddSlider(parent, "player", "settings.vol_player");
        AddSlider(parent, "music", "settings.vol_music");
        AddSlider(parent, "ambient", "settings.vol_ambient");
    }

    static void AddSlider(VisualElement parent, string bus, string locKey)
    {
        var box = IndustryUi.El("Vol_" + bus, "volume-row", "col");
        var label = IndustryUi.Text("L", "", "caption");
        var slider = new Slider(0f, 1f) { value = GetBus(bus) };
        void Refresh()
        {
            label.text = UiLocale.T(locKey) + "  " + Mathf.RoundToInt(GetBus(bus) * 100f) + "%";
        }
        slider.RegisterValueChangedCallback(evt =>
        {
            SetBus(bus, evt.newValue);
            Refresh();
        });
        Refresh();
        box.Add(label);
        box.Add(slider);
        parent.Add(box);
    }

    AudioSource MakeSource(string name, bool spatializeOff, float minDist)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        AudioSource src = child.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = spatializeOff ? 0f : 1f;
        src.minDistance = minDist;
        src.dopplerLevel = 0f;
        return src;
    }

    void LoadFolder(string folder)
    {
        AudioClip[] found = Resources.LoadAll<AudioClip>(Root + folder);
        if (found == null)
            return;
        for (int i = 0; i < found.Length; i++)
        {
            AudioClip clip = found[i];
            if (clip == null)
                continue;
            clips[folder + "/" + clip.name] = clip;
        }
    }

    public static void Ui(string key)
    {
        Ensure().Play2d("ui/" + key, Ui2dVolume, false);
    }

    public static void UiHover()
    {
        GameAudio audio = Ensure();
        if (Time.unscaledTime < audio.nextHover)
            return;
        audio.nextHover = Time.unscaledTime + HoverGap;
        audio.Play2d("ui/ui_hover", Ui2dVolume * 0.45f, false);
    }

    public static void World(string key, Vector3 position)
    {
        Ensure().PlayAt("world/" + key, position, WorldVolume);
    }

    public static void Player(string key)
    {
        Ensure().Play2d("player/" + key, WorldVolume * 0.7f, false);
    }

    public static void Thunder()
    {
        GameAudio audio = Ensure();
        if (audio.world2d == null)
            return;
        audio.world2d.pitch = Random.Range(0.82f, 1.12f);
        AudioClip clip = audio.Clip("world/world_thunder");
        if (clip != null)
            audio.world2d.PlayOneShot(clip, 0.92f * GetBus("world") * GetBus("ambient"));
        audio.world2d.pitch = 1f;
    }

    public static void SetWeather(float rain)
    {
        Ensure().DriveWeather(rain);
    }

    public static void Loop(Component host, string key, bool on)
    {
        if (host == null)
            return;
        if (on && !WorldView.InRange(host.transform.position, LoopRange))
            on = false;
        Ensure().SetLoop(host, "buildings/" + key, on);
    }

    public static void PlayMusic(string key)
    {
        Ensure().SetMusic(key);
    }

    public static void StopMusic()
    {
        Ensure().SetMusic(null);
    }

    public static void SetPaused(bool paused)
    {
        GameAudio audio = Ensure();
        if (paused)
            audio.SetMusic("music/mus_pause");
        else if (SceneManager.GetActiveScene().name == MainMenu.MenuSceneName
            || SceneManager.GetActiveScene().name == MainMenu.LoadingSceneName)
            audio.SetMusic("music/mus_menu");
        else
            audio.SetMusic(null);
    }

    void LateUpdate()
    {
        TickAmbient();
        PruneLoops();
    }

    float nextAmbientScan;

    void TickAmbient()
    {
        if (Time.unscaledTime >= nextAmbientScan)
        {
            nextAmbientScan = Time.unscaledTime + 0.25f;
            ambForest = 0f;
            ambField = 0f;
            ambMountain = 0f;
            ambWater = 0f;
            if (WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsReady)
            {
                Vector2Int cell = BuildingLinker.WorldToCell(WorldView.PlayerPos);
                const int r = 6;
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        string key = AmbientKey(WorldBiomeMap.Instance.Get(new Vector2Int(cell.x + dx, cell.y + dz)));
                        if (key == "ambient/amb_forest") ambForest++;
                        else if (key == "ambient/amb_mountain") ambMountain++;
                        else if (key == "ambient/amb_water") ambWater++;
                        else ambField++;
                    }
                }
            }
        }

        string k0 = null, k1 = null;
        float t0 = 0f, t1 = 0f;
        PickTop(ambForest, "ambient/amb_forest", ref k0, ref t0, ref k1, ref t1);
        PickTop(ambField, "ambient/amb_field", ref k0, ref t0, ref k1, ref t1);
        PickTop(ambMountain, "ambient/amb_mountain", ref k0, ref t0, ref k1, ref t1);
        PickTop(ambWater, "ambient/amb_water", ref k0, ref t0, ref k1, ref t1);
        float sum = t0 + t1;
        if (sum > 0f)
        {
            t0 /= sum;
            t1 /= sum;
        }

        float speed = Time.unscaledDeltaTime * 0.65f;
        ambW0 = Mathf.MoveTowards(ambW0, t0, speed);
        ambW1 = Mathf.MoveTowards(ambW1, t1, speed);
        DriveAmb(ref ambA, ref ambKeyA, k0, ambW0);
        DriveAmb(ref ambB, ref ambKeyB, k1, ambW1);
    }

    void DriveWeather(float rain)
    {
        if (weather == null)
            return;
        float target = AmbientVolume * 1.55f * GetBus("ambient") * Mathf.Clamp01(rain);
        float fade = Time.unscaledDeltaTime * 0.45f;
        if (target < 0.012f)
        {
            weather.volume = Mathf.MoveTowards(weather.volume, 0f, fade);
            if (weather.volume < 0.01f && weather.isPlaying)
                weather.Stop();
            return;
        }

        if (weather.clip == null)
        {
            AudioClip clip = Clip("ambient/amb_rain");
            if (clip == null)
                return;
            weather.clip = clip;
        }

        if (!weather.isPlaying)
            weather.Play();
        weather.volume = Mathf.MoveTowards(weather.volume, target, fade);
    }

    static void PickTop(float w, string key, ref string k0, ref float t0, ref string k1, ref float t1)
    {
        if (w <= 0f)
            return;
        if (w > t0)
        {
            k1 = k0;
            t1 = t0;
            k0 = key;
            t0 = w;
        }
        else if (w > t1)
        {
            k1 = key;
            t1 = w;
        }
    }

    void DriveAmb(ref AudioSource src, ref string held, string key, float weight)
    {
        if (src == null)
            return;
        float fade = Time.unscaledDeltaTime * 0.55f;
        float target = AmbientVolume * GetBus("ambient") * Mathf.Clamp01(weight);
        bool want = !string.IsNullOrEmpty(key) && target >= 0.01f;

        if (held != null && (!want || held != key))
        {
            src.volume = Mathf.MoveTowards(src.volume, 0f, fade);
            if (src.volume > 0.012f)
                return;
            src.Stop();
            src.clip = null;
            held = null;
        }

        if (!want)
            return;

        if (held != key)
        {
            AudioClip clip = Clip(key);
            if (clip == null)
                return;
            src.clip = clip;
            held = key;
            src.volume = 0f;
            src.Play();
        }

        src.volume = Mathf.MoveTowards(src.volume, target, fade);
        if (!src.isPlaying)
            src.Play();
    }

    static string AmbientKey(WorldBiome biome)
    {
        switch (biome)
        {
            case WorldBiome.Forest:
            case WorldBiome.Woodland:
                return "ambient/amb_forest";
            case WorldBiome.MountainPeak:
            case WorldBiome.MountainSlope:
                return "ambient/amb_mountain";
            case WorldBiome.Lake:
            case WorldBiome.Ocean:
            case WorldBiome.Beach:
                return "ambient/amb_water";
            default:
                return "ambient/amb_field";
        }
    }

    void SetMusic(string key)
    {
        if (musicKey == key && music.isPlaying)
            return;
        musicKey = key;
        AudioClip clip = Clip(key);
        if (clip == null)
        {
            music.Stop();
            music.clip = null;
            return;
        }
        if (music.clip != clip)
            music.clip = clip;
        music.volume = MusicVolume * GetBus("music");
        music.ignoreListenerPause = true;
        if (!music.isPlaying)
            music.Play();
    }

    void Play2d(string key, float volume, bool ignorePause)
    {
        AudioClip clip = Clip(key);
        if (clip == null)
            return;
        ui.ignoreListenerPause = ignorePause || key.StartsWith("ui/");
        string bus = key.StartsWith("ui/") ? "ui" : key.StartsWith("player/") ? "player" : "world";
        ui.PlayOneShot(clip, volume * GetBus(bus));
    }

    void PlayAt(string key, Vector3 position, float volume)
    {
        AudioClip clip = Clip(key);
        if (clip == null)
            return;
        AudioSource.PlayClipAtPoint(clip, position, volume * GetBus("world"));
    }

    void SetLoop(Component host, string key, bool on)
    {
        int id = host.GetInstanceID();
        if (on)
            loopHoldUntil[id] = Time.time + LoopHold;
        else
        {
            float hold = 0f;
            loopHoldUntil.TryGetValue(id, out hold);
            if (Time.time < hold)
                on = true;
        }

        if (!on)
        {
            if (loops.TryGetValue(id, out AudioSource existing) && existing != null)
                existing.Stop();
            return;
        }

        AudioClip clip = Clip(key);
        if (clip == null)
            return;

        if (!loops.TryGetValue(id, out AudioSource src) || src == null)
        {
            src = host.GetComponent<AudioSource>();
            if (src == null)
                src = host.gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.dopplerLevel = 0f;
            src.minDistance = 2.5f;
            src.maxDistance = LoopRange;
            src.rolloffMode = AudioRolloffMode.Linear;
            loops[id] = src;
        }

        src.loop = true;
        if (src.clip != clip)
            src.clip = clip;
        src.volume = LoopVolume * GetBus("buildings");
        if (!src.isPlaying)
            src.Play();
    }

    void PruneLoops()
    {
        deadLoops.Clear();
        foreach (var pair in loops)
        {
            if (pair.Value == null)
                deadLoops.Add(pair.Key);
        }
        for (int i = 0; i < deadLoops.Count; i++)
            loops.Remove(deadLoops[i]);
    }

    AudioClip Clip(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;
        clips.TryGetValue(key, out AudioClip clip);
        return clip;
    }
}
