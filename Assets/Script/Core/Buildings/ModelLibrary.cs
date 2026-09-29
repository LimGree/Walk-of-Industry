using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Low-poly модели проекта: OBJ из `Resources/Models` (источник — generated/wi_models.py, правятся в Blender).
/// Материалы подменяются на `Resources/Models/Materials/<имя из wi.mtl>` (Standard, палитра wi_*).
/// </summary>
public static class ModelLibrary
{
    public const string Folder = "Models/";

    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
    static readonly Dictionary<string, GameObject> sources = new Dictionary<string, GameObject>();

    public static bool Has(string model)
    {
        return Source(model) != null;
    }

    static GameObject Source(string model)
    {
        if (string.IsNullOrEmpty(model))
            return null;
        if (sources.TryGetValue(model, out GameObject src))
            return src;
        src = Resources.Load<GameObject>(Folder + model);
        sources[model] = src;
        return src;
    }

    /// <summary>
    /// Новый экземпляр модели без родителя. width — ожидаемая ширина по X в метрах (0 — не проверять):
    /// если импорт дал другой масштаб (OBJ в см и т.п.), модель подгоняется.
    /// </summary>
    public static GameObject Spawn(string model, float width)
    {
        GameObject src = Source(model);
        if (src == null)
            return null;
        GameObject go = Object.Instantiate(src);
        go.name = model;
        ApplyMaterials(go);
        if (width > 0f)
            Fit(go.transform, width);
        CenterProps(go.transform);
        Collider[] cols = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
            Object.Destroy(cols[i]);
        return go;
    }

    /// <summary>Повесить модель под holder, но в осях и масштабе align (корня здания).</summary>
    public static Transform AttachAligned(string model, Transform holder, Transform align, float width, string name)
    {
        GameObject go = Spawn(model, width);
        if (go == null)
            return null;
        go.name = name;
        Transform t = go.transform;
        Vector3 fit = t.localScale;
        t.SetParent(holder, false);
        t.position = align.position;
        t.rotation = align.rotation;
        Vector3 hs = holder.lossyScale;
        Vector3 al = align.lossyScale;
        t.localScale = new Vector3(
            fit.x * Mathf.Abs(al.x) / Mathf.Max(0.0001f, Mathf.Abs(hs.x)),
            fit.y * Mathf.Abs(al.y) / Mathf.Max(0.0001f, Mathf.Abs(hs.y)),
            fit.z * Mathf.Abs(al.z) / Mathf.Max(0.0001f, Mathf.Abs(hs.z)));
        int layer = holder.gameObject.layer;
        foreach (Transform c in go.GetComponentsInChildren<Transform>(true))
            c.gameObject.layer = layer;
        return t;
    }

    static void ApplyMaterials(GameObject go)
    {
        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < rends.Length; r++)
        {
            Material[] shared = rends[r].sharedMaterials;
            bool changed = false;
            for (int m = 0; m < shared.Length; m++)
            {
                if (shared[m] == null)
                    continue;
                Material mine = FindMaterial(shared[m].name);
                if (mine != null)
                {
                    shared[m] = mine;
                    changed = true;
                }
            }

            if (changed)
                rends[r].sharedMaterials = shared;
        }
    }

    public static Material FindMaterial(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        name = name.Replace(" (Instance)", "");
        if (mats.TryGetValue(name, out Material m))
            return m;
        m = Resources.Load<Material>(Folder + "Materials/" + name);
        mats[name] = m;
        return m;
    }

    static void Fit(Transform root, float width)
    {
        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0)
            return;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);
        float got = Mathf.Max(b.size.x, b.size.z);
        if (got < 0.001f)
            return;
        float k = width / got;
        // Только явный перекос единиц импорта (1 юнит = 1 см и т.п.), не мелкие отличия формы.
        if (k < 0.5f || k > 2f)
            root.localScale *= k;
    }

    static void CenterProps(Transform root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("Prop"))
                continue;
            MeshFilter mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                continue;
            Mesh mesh = Object.Instantiate(mf.sharedMesh);
            Vector3 c = mesh.bounds.center;
            Vector3[] v = mesh.vertices;
            for (int i = 0; i < v.Length; i++)
                v[i] -= c;
            mesh.vertices = v;
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;
            t.localPosition += Vector3.Scale(t.localRotation * c, t.localScale);
        }
    }

    public static Transform FindChild(Transform root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }

        return null;
    }
}
