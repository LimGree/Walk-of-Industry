using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Постобработка (пакет Post Processing v2) из настроек: сглаживание FXAA/SMAA, свечение, виньетка,
/// размытие в движении, гамма. Слой вешается на Camera.main из кода, общий глобальный volume живёт между сценами.
/// Если всё выключено — слой отключается, кадр не тратится.
/// </summary>
public static class PostFx
{
    static PostProcessResources resources;
    static PostProcessVolume volume;
    static Bloom bloom;
    static Vignette vignette;
    static MotionBlur motionBlur;
    static ColorGrading grading;
    static bool resourcesLoaded;

    public static void Apply(Camera cam)
    {
        if (cam == null)
            return;

        bool aaPost = GameSettings.AntiAliasing == 1 || GameSettings.AntiAliasing == 2;
        bool effects = GameSettings.Bloom || GameSettings.Vignette || GameSettings.MotionBlur > 0.01f
            || Mathf.Abs(GameSettings.Gamma - 1f) > 0.01f;
        PostProcessLayer layer = cam.GetComponent<PostProcessLayer>();

        if (!aaPost && !effects)
        {
            if (layer != null)
                layer.enabled = false;
            return;
        }

        PostProcessResources res = Resources();
        if (res == null)
            return;

        EnsureVolume();
        if (layer == null)
        {
            layer = cam.gameObject.AddComponent<PostProcessLayer>();
            layer.Init(res);
        }

        layer.volumeTrigger = cam.transform;
        layer.volumeLayer = 1 << volume.gameObject.layer;
        layer.antialiasingMode = GameSettings.AntiAliasing == 1 ? PostProcessLayer.Antialiasing.FastApproximateAntialiasing
            : GameSettings.AntiAliasing == 2 ? PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing
            : PostProcessLayer.Antialiasing.None;
        layer.fastApproximateAntialiasing.fastMode = true;
        layer.subpixelMorphologicalAntialiasing.quality = SubpixelMorphologicalAntialiasing.Quality.Medium;
        layer.enabled = true;

        bloom.enabled.Override(GameSettings.Bloom);
        bloom.intensity.Override(1.4f);
        bloom.threshold.Override(0.95f);
        bloom.softKnee.Override(0.6f);
        bloom.fastMode.Override(true);

        vignette.enabled.Override(GameSettings.Vignette);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.45f);

        float blur = GameSettings.MotionBlur;
        motionBlur.enabled.Override(blur > 0.01f);
        motionBlur.shutterAngle.Override(Mathf.Lerp(60f, 300f, blur));
        motionBlur.sampleCount.Override(8);

        float gamma = GameSettings.Gamma;
        grading.enabled.Override(Mathf.Abs(gamma - 1f) > 0.01f);
        grading.gradingMode.Override(GradingMode.LowDefinitionRange);
        grading.gamma.Override(new Vector4(1f, 1f, 1f, Mathf.Clamp(gamma - 1f, -0.6f, 0.6f)));
    }

    static PostProcessResources Resources()
    {
        if (resourcesLoaded)
            return resources;
        resourcesLoaded = true;
        PostFxResources refs = UnityEngine.Resources.Load<PostFxResources>("PostFxResources");
        resources = refs != null ? refs.resources : null;
#if UNITY_EDITOR
        if (resources == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:PostProcessResources");
            if (guids.Length > 0)
                resources = UnityEditor.AssetDatabase.LoadAssetAtPath<PostProcessResources>(
                    UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]));
        }
#endif
        if (resources == null)
            Debug.LogWarning("[PostFx] PostProcessResources не найдены — постобработка выключена.");
        return resources;
    }

    static void EnsureVolume()
    {
        if (volume != null)
            return;
        var go = new GameObject("PostFxVolume");
        Object.DontDestroyOnLoad(go);
        go.layer = 0;
        volume = go.AddComponent<PostProcessVolume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        var profile = ScriptableObject.CreateInstance<PostProcessProfile>();
        profile.name = "PostFxRuntime";
        bloom = profile.AddSettings<Bloom>();
        vignette = profile.AddSettings<Vignette>();
        motionBlur = profile.AddSettings<MotionBlur>();
        grading = profile.AddSettings<ColorGrading>();
        volume.profile = profile;
    }
}
