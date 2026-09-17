using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Полупрозрачный гост: альбедо исходного материала остаётся, альфа и тинт
/// берутся из ghostValid / ghostInvalid. Все слои renderer.sharedMaterials.
/// </summary>
public class GhostTint : MonoBehaviour
{
    static readonly Dictionary<long, Material> cache = new Dictionary<long, Material>();
    static Shader ghostShader;

    static readonly Color FallbackValid = new Color(0.62f, 0.91f, 0.52f, 0.45f);
    static readonly Color FallbackInvalid = new Color(0.91f, 0.55f, 0.52f, 0.45f);

    readonly Dictionary<int, Material[]> originals = new Dictionary<int, Material[]>();
    readonly Dictionary<int, Material[]> dstValid = new Dictionary<int, Material[]>();
    readonly Dictionary<int, Material[]> dstInvalid = new Dictionary<int, Material[]>();

    public static void Apply(GameObject root, bool valid, Material validMat, Material invalidMat)
    {
        if (root == null)
            return;
        GhostTint tint = root.GetComponent<GhostTint>();
        if (tint == null)
            tint = root.AddComponent<GhostTint>();
        Color ok = validMat != null ? validMat.color : FallbackValid;
        Color bad = invalidMat != null ? invalidMat.color : FallbackInvalid;
        if (ok.a < 0.12f)
            ok.a = 0.45f;
        if (bad.a < 0.12f)
            bad.a = 0.45f;
        tint.Apply(valid, ok, bad);
    }

    public void Apply(bool valid, Color validTint, Color invalidTint)
    {
        Color tint = valid ? validTint : invalidTint;
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Renderer rend = all[i];
            if (Skip(rend))
                continue;

            int id = rend.GetInstanceID();
            if (!originals.TryGetValue(id, out Material[] src) || src == null)
            {
                src = rend.sharedMaterials;
                originals[id] = src;
            }

            if (src == null || src.Length == 0)
                continue;

            Dictionary<int, Material[]> bag = valid ? dstValid : dstInvalid;
            if (!bag.TryGetValue(id, out Material[] dst) || dst == null || dst.Length != src.Length)
            {
                dst = new Material[src.Length];
                for (int m = 0; m < src.Length; m++)
                    dst[m] = GhostOf(src[m], tint);
                bag[id] = dst;
            }
            rend.sharedMaterials = dst;
        }
    }

    static bool Skip(Renderer rend)
    {
        if (rend == null)
            return true;
        if (rend is ParticleSystemRenderer || rend is LineRenderer || rend is TrailRenderer)
            return true;
        return rend.GetComponentInParent<SocketArrow>() != null;
    }

    static Material GhostOf(Material source, Color tint)
    {
        int srcId = source != null ? source.GetInstanceID() : 0;
        int tintKey = (Mathf.RoundToInt(tint.r * 255f) << 24)
            ^ (Mathf.RoundToInt(tint.g * 255f) << 16)
            ^ (Mathf.RoundToInt(tint.b * 255f) << 8)
            ^ Mathf.RoundToInt(tint.a * 255f);
        long key = ((long)srcId << 32) ^ (uint)tintKey;
        if (cache.TryGetValue(key, out Material existing) && existing != null)
            return existing;

        Shader shader = GhostShader();
        var mat = new Material(shader);
        Texture tex = ReadTex(source);
        Color albedo = ReadAlbedo(source);
        mat.SetTexture("_MainTex", tex != null ? tex : Texture2D.whiteTexture);
        if (source != null && source.HasProperty("_MainTex"))
        {
            mat.SetTextureScale("_MainTex", source.GetTextureScale("_MainTex"));
            mat.SetTextureOffset("_MainTex", source.GetTextureOffset("_MainTex"));
        }

        Color c = new Color(albedo.r * tint.r, albedo.g * tint.g, albedo.b * tint.b, tint.a);
        mat.SetColor("_Color", c);
        mat.color = c;
        mat.renderQueue = 3000;
        cache[key] = mat;
        return mat;
    }

    static Shader GhostShader()
    {
        if (ghostShader != null)
            return ghostShader;
        ghostShader = Resources.Load<Shader>("WalkToBiomeGhost");
        if (ghostShader == null)
            ghostShader = Shader.Find("Hidden/WalkToBiome/Ghost");
        if (ghostShader == null)
            ghostShader = RuntimeMaterials.Unlit;
        return ghostShader;
    }

    static Texture ReadTex(Material source)
    {
        if (source == null)
            return null;
        Texture tex = source.mainTexture;
        if (tex == null && source.HasProperty("_MainTex"))
            tex = source.GetTexture("_MainTex");
        if (tex == null && source.HasProperty("_BaseMap"))
            tex = source.GetTexture("_BaseMap");
        return tex;
    }

    static Color ReadAlbedo(Material source)
    {
        if (source == null)
            return Color.white;
        if (source.HasProperty("_Color"))
            return source.color;
        if (source.HasProperty("_BaseColor"))
            return source.GetColor("_BaseColor");
        return Color.white;
    }
}
