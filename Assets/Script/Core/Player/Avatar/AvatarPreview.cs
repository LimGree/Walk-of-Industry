using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Живое превью персонажа для экрана внешности: подиум высоко над миром на своём слое,
/// своя камера рисует в RenderTexture, свой свет. Персонаж дышит и крутится мышью.
/// Работает и в главном меню, и на паузе (unscaled-время).
/// </summary>
public class AvatarPreview : MonoBehaviour
{
    const int PreviewLayer = 29;
    static readonly Vector3 StagePos = new Vector3(0f, 3000f, 0f);

    Camera cam;
    AvatarRig rig;
    AvatarAnimator animator;
    Transform turntable;
    float yaw = 200f;
    float targetYaw = 200f;
    float autoSpin;
    float zoom = 1f;

    public RenderTexture Texture { get; private set; }

    public static AvatarPreview Create(AvatarLook look, int width, int height)
    {
        var go = new GameObject("AvatarPreview") { hideFlags = HideFlags.DontSave };
        go.transform.position = StagePos;
        AvatarPreview preview = go.AddComponent<AvatarPreview>();
        preview.Setup(look, width, height);
        return preview;
    }

    void Setup(AvatarLook look, int width, int height)
    {
        Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "AvatarPreviewRT", antiAliasing = 4 };
        Texture.Create();

        turntable = new GameObject("Turntable") { layer = PreviewLayer }.transform;
        turntable.SetParent(transform, false);

        var camGo = new GameObject("PreviewCam") { layer = PreviewLayer };
        camGo.transform.SetParent(transform, false);
        cam = camGo.AddComponent<Camera>();
        cam.cullingMask = 1 << PreviewLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.fieldOfView = 24f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 30f;
        cam.targetTexture = Texture;
        cam.allowHDR = false;
        cam.allowMSAA = true;
        cam.useOcclusionCulling = false;
        PlaceCamera();

        // Камера смотрит вдоль +Z: ключевой и заполняющий светят с её стороны, контровой — из-за спины.
        AddLight("Key", new Vector3(32f, 30f, 0f), new Color(1f, 0.95f, 0.86f), 1.15f);
        AddLight("Fill", new Vector3(10f, -40f, 0f), new Color(0.62f, 0.72f, 0.9f), 0.45f);
        AddLight("Rim", new Vector3(18f, 200f, 0f), new Color(1f, 0.78f, 0.5f), 0.7f);

        SetLook(look);
    }

    void AddLight(string name, Vector3 euler, Color color, float intensity)
    {
        var go = new GameObject(name) { layer = PreviewLayer };
        go.transform.SetParent(transform, false);
        go.transform.rotation = Quaternion.Euler(euler);
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = color;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << PreviewLayer;
        light.renderMode = LightRenderMode.ForcePixel;
    }

    void PlaceCamera()
    {
        // Весь рост в кадре, взгляд чуть сверху.
        float dist = Mathf.Lerp(5.2f, 2.6f, Mathf.InverseLerp(0.6f, 1.6f, zoom));
        Vector3 target = new Vector3(0f, Mathf.Lerp(0.92f, 1.35f, Mathf.InverseLerp(0.6f, 1.6f, zoom)), 0f);
        cam.transform.localPosition = target + Quaternion.Euler(6f, 0f, 0f) * new Vector3(0f, 0f, -dist);
        cam.transform.localRotation = Quaternion.LookRotation(target - cam.transform.localPosition, Vector3.up);
    }

    public void SetLook(AvatarLook look)
    {
        if (rig != null)
            Destroy(rig.gameObject);
        rig = AvatarRig.Build(turntable, look, PreviewLayer);
        rig.SetShadowMode(ShadowCastingMode.Off);
        rig.SetReceiveShadows(false);
        animator = new AvatarAnimator(rig);
        animator.Tick(0.016f, new AvatarAnimator.Motion { grounded = true });
    }

    /// <summary>Повернуть мышью (градусы).</summary>
    public void Spin(float degrees)
    {
        targetYaw += degrees;
        autoSpin = 0f;
    }

    /// <summary>Колесо: ближе к лицу / весь рост.</summary>
    public void Zoom(float delta)
    {
        zoom = Mathf.Clamp(zoom + delta, 0.6f, 1.6f);
        PlaceCamera();
    }

    public void Wave()
    {
        animator?.TriggerAction();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        autoSpin = Mathf.Min(1f, autoSpin + dt * 0.25f);
        targetYaw += dt * 10f * autoSpin * autoSpin;
        yaw = Mathf.Lerp(yaw, targetYaw, 1f - Mathf.Exp(-12f * dt));
        turntable.localRotation = Quaternion.Euler(0f, yaw, 0f);
        animator?.Tick(dt, new AvatarAnimator.Motion { grounded = true });
    }

    void OnDestroy()
    {
        if (cam != null)
            cam.targetTexture = null;
        if (Texture != null)
        {
            Texture.Release();
            Destroy(Texture);
        }
    }
}
