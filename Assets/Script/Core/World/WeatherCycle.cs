using UnityEngine;

/// <summary>
/// Цикл ясно / дождь / гроза: частицы у камеры, молнии, гром.
/// </summary>
public class WeatherCycle : MonoBehaviour
{
    public static WeatherCycle Instance { get; private set; }

    [Header("Preview")]
    [Tooltip("Ясно / дождь / гроза. В Play можно переключать сразу.")]
    public WeatherKind previewWeather = WeatherKind.Clear;

    ParticleSystem rain;
    float nextFront;
    float nextBolt;
    float thunderAt = -1f;
    bool skipValidate;

    void Awake()
    {
        Instance = this;
        Weather.Kind = previewWeather;
        EnsureRain();
        ArmFront();
        ArmBolt();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (rain != null)
            Destroy(rain.gameObject);
    }

    void OnValidate()
    {
        if (skipValidate)
            return;
        Weather.Kind = previewWeather;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        float dt = Time.deltaTime;
        if (GameSettings.WeatherAuto)
        {
            nextFront -= dt;
            if (nextFront <= 0f)
            {
                Weather.Kind = PickNext();
                ArmFront();
            }
        }

        Weather.Tick(dt);
        skipValidate = true;
        previewWeather = Weather.Kind;
        skipValidate = false;

        DriveRain();
        TickStorm(dt);
        GameAudio.SetWeather(Weather.Rain);
    }

    void LateUpdate()
    {
        FollowCamera();
    }

    public void CaptureSave(SaveData data)
    {
        if (data != null)
            data.worldWeather = (int)Weather.Kind;
    }

    public void ApplySave(SaveData data)
    {
        if (data == null)
            ResetToNewWorld();
        else
            Weather.Kind = ClampKind(data.worldWeather);
        previewWeather = Weather.Kind;
        Weather.Tick(10f);
    }

    public void ResetToNewWorld()
    {
        Weather.ResetToNewWorld();
        previewWeather = WeatherKind.Clear;
        ArmFront();
    }

    static WeatherKind ClampKind(int value)
    {
        if (value < 0 || value > 2)
            return WeatherKind.Clear;
        return (WeatherKind)value;
    }

    void ArmFront()
    {
        nextFront = Random.Range(3f, 7f) * 60f;
    }

    static WeatherKind PickNext()
    {
        return (WeatherKind)Random.Range(0, 3);
    }

    void TickStorm(float dt)
    {
        if (Weather.Kind != WeatherKind.Storm || Weather.Rain < 0.35f)
        {
            thunderAt = -1f;
            return;
        }

        nextBolt -= dt;
        if (nextBolt <= 0f)
        {
            Weather.Flash = Random.Range(0.65f, 1f);
            thunderAt = Time.time + Random.Range(0.28f, 2.4f);
            ArmBolt();
        }

        if (thunderAt > 0f && Time.time >= thunderAt)
        {
            thunderAt = -1f;
            GameAudio.Thunder();
        }
    }

    void ArmBolt()
    {
        nextBolt = Random.Range(3.2f, 11.5f);
    }

    void DriveRain()
    {
        EnsureRain();
        if (rain == null)
            return;

        var emission = rain.emission;
        float rate = Weather.Rain * (Weather.Kind == WeatherKind.Storm ? 1100f : 620f);
        emission.rateOverTime = rate;
        if (Weather.Rain > 0.04f)
        {
            if (!rain.isPlaying)
                rain.Play();
        }
        else if (rain.isPlaying && Weather.Rain < 0.01f)
            rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    void FollowCamera()
    {
        if (rain == null)
            return;

        Camera cam = WorldView.Cam;
        Vector3 origin = WorldView.HasPlayer ? WorldView.PlayerPos : transform.position;
        if (cam != null)
        {
            origin = cam.transform.position;
            Vector3 ahead = cam.transform.forward;
            ahead.y = 0f;
            if (ahead.sqrMagnitude > 0.001f)
                origin += ahead.normalized * 2.5f;
        }

        rain.transform.SetPositionAndRotation(origin + Vector3.up * 5.5f, Quaternion.identity);
    }

    void EnsureRain()
    {
        if (rain != null)
            return;

        var go = new GameObject("WeatherRain");
        go.transform.SetParent(null);
        rain = go.AddComponent<ParticleSystem>();
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = rain.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.15f);
        main.startSpeed = 0f;
        main.startSize3D = true;
        main.startSizeX = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
        main.startSizeY = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSizeZ = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
        main.startColor = new Color(0.86f, 0.90f, 0.96f, 0.85f);
        main.maxParticles = 4000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 1.1f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = rain.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;

        var shape = rain.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(18f, 0.6f, 18f);

        var vel = rain.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);
        vel.y = new ParticleSystem.MinMaxCurve(-14f, -20f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.6f, 0.6f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Material mat = RuntimeMaterials.Create(BuildStreak(), new Color(0.88f, 0.92f, 0.98f, 0.9f));
        if (mat.HasProperty("_WalkLightTint"))
            mat.SetColor("_WalkLightTint", Color.white);
        if (mat.HasProperty("_LightTint"))
            mat.SetColor("_LightTint", Color.white);
        renderer.sharedMaterial = mat;
    }

    static Texture2D BuildStreak()
    {
        var tex = new Texture2D(4, 16, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        for (int y = 0; y < 16; y++)
        {
            float a = 1f - Mathf.Abs(y / 15f - 0.5f) * 1.6f;
            a = Mathf.Clamp01(a);
            for (int x = 0; x < 4; x++)
            {
                float edge = 1f - Mathf.Abs(x / 3f - 0.5f) * 1.8f;
                Color c = Color.white;
                c.a = Mathf.Clamp01(a * edge);
                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply(false, true);
        return tex;
    }
}
