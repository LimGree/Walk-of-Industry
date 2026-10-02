using UnityEngine;

/// <summary>
/// Масштаб рендера для встроенного конвейера: основная камера рисует в уменьшенную текстуру,
/// вспомогательная камера растягивает её на экран. Интерфейс (UI Toolkit) рисуется поверх в полном разрешении.
/// При 100 % всё выключено и камера рисует прямо на экран.
/// </summary>
public class RenderScaler : MonoBehaviour
{
    Camera source;
    Camera blitCam;
    RenderTexture target;

    public static void Apply(Camera cam)
    {
        if (cam == null)
            return;
        RenderScaler scaler = cam.GetComponent<RenderScaler>();
        if (GameSettings.RenderScale >= 0.995f)
        {
            if (scaler != null)
                scaler.enabled = false;
            return;
        }
        if (scaler == null)
            scaler = cam.gameObject.AddComponent<RenderScaler>();
        scaler.enabled = true;
        scaler.Refresh();
    }

    void OnEnable()
    {
        source = GetComponent<Camera>();
        EnsureBlitCam();
        Refresh();
    }

    void OnDisable()
    {
        if (source != null && source.targetTexture == target)
            source.targetTexture = null;
        if (blitCam != null)
            blitCam.enabled = false;
        ReleaseTarget();
    }

    void OnDestroy()
    {
        if (blitCam != null)
            Destroy(blitCam.gameObject);
    }

    void LateUpdate()
    {
        Refresh();
    }

    void Refresh()
    {
        if (source == null)
            return;
        float scale = GameSettings.RenderScale;
        int w = Mathf.Max(64, Mathf.RoundToInt(Screen.width * scale));
        int h = Mathf.Max(64, Mathf.RoundToInt(Screen.height * scale));
        if (target == null || target.width != w || target.height != h)
        {
            if (source.targetTexture == target)
                source.targetTexture = null;
            ReleaseTarget();
            target = new RenderTexture(w, h, 24, RenderTextureFormat.DefaultHDR)
            {
                name = "RenderScale",
                filterMode = FilterMode.Bilinear,
                antiAliasing = Mathf.Max(1, QualitySettings.antiAliasing)
            };
            target.Create();
        }
        if (source.targetTexture != target)
            source.targetTexture = target;
        EnsureBlitCam();
        blitCam.enabled = true;
        blitCam.depth = source.depth + 0.5f;
    }

    void EnsureBlitCam()
    {
        if (blitCam != null)
            return;
        var go = new GameObject("RenderScaleBlit");
        go.transform.SetParent(transform, false);
        blitCam = go.AddComponent<Camera>();
        blitCam.cullingMask = 0;
        blitCam.clearFlags = CameraClearFlags.Nothing;
        blitCam.orthographic = true;
        blitCam.useOcclusionCulling = false;
        blitCam.allowHDR = false;
        blitCam.allowMSAA = false;
        go.AddComponent<RenderScaleBlit>().owner = this;
    }

    public RenderTexture Target => target;

    void ReleaseTarget()
    {
        if (target == null)
            return;
        target.Release();
        Destroy(target);
        target = null;
    }
}
