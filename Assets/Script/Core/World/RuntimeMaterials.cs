using UnityEngine;

/// <summary>
/// Materials that work in a player build. Shader.Find for built-in
/// Unlit/Sprites shaders returns null after Unity strips unused shaders.
/// </summary>
public static class RuntimeMaterials
{
    const string ResourceName = "WalkToBiomeUnlit";
    static Shader cached;

    public static Shader Unlit
    {
        get
        {
            if (cached != null)
                return cached;

            cached = Resources.Load<Shader>(ResourceName);
            if (cached == null)
                cached = Shader.Find("Hidden/WalkToBiome/Unlit");
            if (cached == null)
                cached = Shader.Find("Sprites/Default");
            if (cached == null)
                cached = Shader.Find("Unlit/Color");
            if (cached == null)
                cached = Shader.Find("Hidden/InternalErrorShader");
            return cached;
        }
    }

    public static Material Create(Color color)
    {
        return Create(Texture2D.whiteTexture, color);
    }

    public static Material Create(Texture texture, Color color)
    {
        Shader shader = Unlit;
        if (shader == null)
        {
            Debug.LogError("RuntimeMaterials: no unlit shader in the build.");
            return new Material(Shader.Find("Hidden/InternalErrorShader"));
        }

        var mat = new Material(shader);
        if (mat.HasProperty("_MainTex"))
            mat.SetTexture("_MainTex", texture != null ? texture : Texture2D.whiteTexture);
        if (mat.HasProperty("_BaseMap"))
            mat.SetTexture("_BaseMap", texture != null ? texture : Texture2D.whiteTexture);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_LightTint"))
            mat.SetColor("_LightTint", Color.white);
        if (mat.HasProperty("_WalkUvFog"))
            mat.SetFloat("_WalkUvFog", 0f);
        mat.color = color;
        ApplyWorldGfx(mat);
        return mat;
    }

    public static void ApplyWorldGfx(Material mat)
    {
        if (mat == null)
            return;

        Color light = Shader.GetGlobalColor("_WalkLightTint");
        if (light.a < 0.01f && light.r + light.g + light.b < 0.01f)
            light = Color.white * Mathf.Clamp(GameSettings.WorldLight * GameSettings.Brightness, 0.12f, 2.5f);
        if (mat.HasProperty("_WalkLightTint"))
            mat.SetColor("_WalkLightTint", light);
    }
}
