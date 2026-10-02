using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Свет фонарей, огня печей и фонарика игрока на землю. Земля — большие тайлы карты биомов
/// без освещения (<see cref="WorldBiomeMap"/>); в прямом рендере на такой тайл приходилось лишь несколько
/// попиксельных огней. Здесь до 32 ближайших к камере точечных/прожекторных источников раз в кадр
/// уходят глобальными массивами в шейдер Hidden/WalkToBiome/GroundLit.
/// </summary>
public class GroundLights : MonoBehaviour
{
    public const int MaxLights = 32;
    const float RescanEvery = 1f;
    const float MaxDistance = 80f;

    static readonly int PosId = Shader.PropertyToID("_WalkPointPos");
    static readonly int ColorId = Shader.PropertyToID("_WalkPointColor");
    static readonly int DirId = Shader.PropertyToID("_WalkPointDir");
    static readonly int CountId = Shader.PropertyToID("_WalkPointCount");

    static Shader groundShader;
    static bool shaderTried;

    readonly List<Light> known = new List<Light>(256);
    readonly Light[] picked = new Light[MaxLights];
    readonly float[] pickedDist = new float[MaxLights];
    readonly Vector4[] pos = new Vector4[MaxLights];
    readonly Vector4[] col = new Vector4[MaxLights];
    readonly Vector4[] dir = new Vector4[MaxLights];
    float nextScan;

    /// <summary>Освещаемый шейдер земли (или null — тогда остаётся неосвещаемый).</summary>
    public static Shader GroundShader
    {
        get
        {
            if (shaderTried)
                return groundShader;
            shaderTried = true;
            groundShader = Resources.Load<Shader>("WalkToBiomeGroundLit");
            if (groundShader == null)
                groundShader = Shader.Find("Hidden/WalkToBiome/GroundLit");
            if (groundShader != null && !groundShader.isSupported)
                groundShader = null;
            return groundShader;
        }
    }

    void OnEnable()
    {
        nextScan = 0f;
        // Массивы шейдера получают размер при первой установке — сразу полный.
        Shader.SetGlobalVectorArray(PosId, pos);
        Shader.SetGlobalVectorArray(ColorId, col);
        Shader.SetGlobalVectorArray(DirId, dir);
        Shader.SetGlobalFloat(CountId, 0f);
    }

    void OnDisable()
    {
        Shader.SetGlobalFloat(CountId, 0f);
    }

    void LateUpdate()
    {
        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + RescanEvery;
            known.Clear();
            Light[] all = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].type == LightType.Point || all[i].type == LightType.Spot)
                    known.Add(all[i]);
            }
        }

        Camera cam = WorldView.Cam;
        Vector3 eye = cam != null ? cam.transform.position : transform.position;
        int count = 0;
        for (int i = 0; i < known.Count; i++)
        {
            Light l = known[i];
            if (l == null || !l.isActiveAndEnabled || l.intensity <= 0.01f || l.range <= 0.05f)
                continue;
            float d = (l.transform.position - eye).sqrMagnitude;
            float reach = MaxDistance + l.range;
            if (d > reach * reach)
                continue;
            // Держим MaxLights ближайших: вставка в отсортированный хвост.
            if (count < MaxLights)
            {
                picked[count] = l;
                pickedDist[count] = d;
                count++;
            }
            else if (d < pickedDist[MaxLights - 1])
            {
                picked[MaxLights - 1] = l;
                pickedDist[MaxLights - 1] = d;
            }
            else
                continue;
            for (int k = count - 1; k > 0 && pickedDist[k] < pickedDist[k - 1]; k--)
            {
                (pickedDist[k], pickedDist[k - 1]) = (pickedDist[k - 1], pickedDist[k]);
                (picked[k], picked[k - 1]) = (picked[k - 1], picked[k]);
            }
        }

        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        for (int i = 0; i < count; i++)
        {
            Light l = picked[i];
            Vector3 p = l.transform.position;
            pos[i] = new Vector4(p.x, p.y, p.z, 1f / (l.range * l.range));
            Color c = linear ? l.color.linear : l.color;
            c *= l.intensity;
            bool spot = l.type == LightType.Spot;
            col[i] = new Vector4(c.r, c.g, c.b, spot ? 1f : 0f);
            Vector3 f = l.transform.forward;
            dir[i] = new Vector4(f.x, f.y, f.z, spot ? Mathf.Cos(l.spotAngle * 0.5f * Mathf.Deg2Rad) : -1f);
        }
        for (int i = count; i < MaxLights; i++)
            picked[i] = null;

        Shader.SetGlobalVectorArray(PosId, pos);
        Shader.SetGlobalVectorArray(ColorId, col);
        Shader.SetGlobalVectorArray(DirId, dir);
        Shader.SetGlobalFloat(CountId, count);
    }
}
