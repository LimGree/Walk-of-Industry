using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Модель предмета для ленты, склеенная в один меш (один рендерер вместо десятка частей).
/// Части с простым непрозрачным Standard-материалом без текстур уходят в первый сабмеш
/// с общим материалом: цвет — в цвет вершин, металл/гладкость — в uv4, так одинаковые
/// предметы рисуются инстансингом. Остальные части остаются отдельными сабмешами со своим материалом.
/// Меш уже отмасштабирован под размер груза и отцентрован. Кеш на предмет и масштаб.
/// </summary>
public static class BeltItemBake
{
    public sealed class Baked
    {
        public Mesh mesh;
        public Material[] materials;
    }

    static readonly Dictionary<long, Baked> cache = new Dictionary<long, Baked>(32);
    static Material sharedMaterial;
    static bool sharedMaterialTried;

    sealed class Part
    {
        public Mesh mesh;
        public int subMesh;
        public Material material;
        public Matrix4x4 matrix;
    }

    sealed class Group
    {
        public Material material;
        public readonly List<int> indices = new List<int>(256);
    }

    /// <summary>Склеенная модель или null — тогда предмет рисуется исходным префабом.</summary>
    public static Baked Get(ItemData item, float itemScale)
    {
        if (item == null || item.worldPrefab == null)
            return null;
        long key = ((long)item.GetInstanceID() << 32) ^ (uint)System.BitConverter.SingleToInt32Bits(itemScale);
        if (cache.TryGetValue(key, out Baked baked))
            return baked;
        baked = Build(item, itemScale);
        cache[key] = baked;
        return baked;
    }

    static Material SharedMaterial
    {
        get
        {
            if (sharedMaterialTried)
                return sharedMaterial;
            sharedMaterialTried = true;
            Shader shader = Resources.Load<Shader>("WalkToBiomeBeltItem");
            if (shader == null)
                shader = Shader.Find("Hidden/WalkToBiome/BeltItem");
            if (shader != null && shader.isSupported)
            {
                sharedMaterial = new Material(shader) { name = "BeltItemShared", enableInstancing = true };
                sharedMaterial.hideFlags = HideFlags.DontSave;
            }
            return sharedMaterial;
        }
    }

    static Baked Build(ItemData item, float itemScale)
    {
        GameObject prefab = item.worldPrefab;
        // Анимация, частицы, скиннинг и т.п. склейкой не передать — такие предметы остаются как есть.
        if (prefab.GetComponentInChildren<Animator>(true) != null
            || prefab.GetComponentInChildren<Animation>(true) != null
            || prefab.GetComponentInChildren<ParticleSystem>(true) != null
            || prefab.GetComponentInChildren<SkinnedMeshRenderer>(true) != null
            || prefab.GetComponentInChildren<LODGroup>(true) != null)
            return null;

        Transform root = prefab.transform;
        // Как при Instantiate под визуалом: позиция и поворот корня обнуляются, масштаб остаётся.
        Matrix4x4 rootSpace = Matrix4x4.Scale(root.localScale) * root.worldToLocalMatrix;

        var parts = new List<Part>(16);
        MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            MeshRenderer mr = renderers[r];
            if (!mr.enabled || !ActiveInPrefab(mr.transform, root))
                continue;
            if (!mr.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null)
                continue;
            Mesh mesh = mf.sharedMesh;
            // В билде без Read/Write вершины не прочитать (в редакторе можно всегда).
            if (!mesh.isReadable && !Application.isEditor)
                return null;
            Material[] mats = mr.sharedMaterials;
            Matrix4x4 m = rootSpace * mr.transform.localToWorldMatrix;
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                Material mat = mats.Length > 0 ? mats[Mathf.Min(s, mats.Length - 1)] : null;
                if (mat == null)
                    continue;
                parts.Add(new Part { mesh = mesh, subMesh = s, material = mat, matrix = m });
            }
        }

        if (parts.Count == 0)
            return null;

        Material shared = SharedMaterial;
        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

        var positions = new List<Vector3>(1024);
        var normals = new List<Vector3>(1024);
        var uv0 = new List<Vector2>(1024);
        var colors = new List<Color>(1024);
        var metalGloss = new List<Vector2>(1024);
        var groups = new List<Group>(4);
        var bakedGroup = new Group();
        bool needTangents = false;

        var srcPos = new List<Vector3>(512);
        var srcNrm = new List<Vector3>(512);
        var srcUv = new List<Vector2>(512);
        var srcIdx = new List<int>(1024);
        var remap = new Dictionary<int, int>(512);

        Mesh lastMesh = null;
        for (int p = 0; p < parts.Count; p++)
        {
            Part part = parts[p];
            if (part.mesh != lastMesh)
            {
                lastMesh = part.mesh;
                part.mesh.GetVertices(srcPos);
                part.mesh.GetNormals(srcNrm);
                part.mesh.GetUVs(0, srcUv);
            }

            bool simple = shared != null && IsSimple(part.material);
            Group group = simple ? bakedGroup : GroupFor(groups, part.material);
            if (!simple && part.material.IsKeywordEnabled("_NORMALMAP"))
                needTangents = true;

            Color tint = Color.white;
            Vector2 mg = Vector2.zero;
            if (simple)
            {
                tint = part.material.HasProperty("_Color") ? part.material.color : Color.white;
                if (linear)
                    tint = tint.linear;
                mg = new Vector2(
                    part.material.HasProperty("_Metallic") ? part.material.GetFloat("_Metallic") : 0f,
                    part.material.HasProperty("_Glossiness") ? part.material.GetFloat("_Glossiness") : 0.5f);
            }

            Matrix4x4 m = part.matrix;
            Matrix4x4 nm = m.inverse.transpose;
            bool flip = m.determinant < 0f;

            part.mesh.GetIndices(srcIdx, part.subMesh);
            MeshTopology topology = part.mesh.GetTopology(part.subMesh);
            if (topology != MeshTopology.Triangles)
                continue;

            remap.Clear();
            for (int i = 0; i < srcIdx.Count; i += 3)
            {
                int a = Vertex(srcIdx[i]);
                int b = Vertex(srcIdx[i + 1]);
                int c = Vertex(srcIdx[i + 2]);
                group.indices.Add(a);
                if (flip)
                {
                    group.indices.Add(c);
                    group.indices.Add(b);
                }
                else
                {
                    group.indices.Add(b);
                    group.indices.Add(c);
                }
            }

            int Vertex(int src)
            {
                if (remap.TryGetValue(src, out int dst))
                    return dst;
                dst = positions.Count;
                remap[src] = dst;
                positions.Add(m.MultiplyPoint3x4(srcPos[src]));
                normals.Add(src < srcNrm.Count ? nm.MultiplyVector(srcNrm[src]).normalized : Vector3.up);
                uv0.Add(src < srcUv.Count ? srcUv[src] : Vector2.zero);
                colors.Add(tint);
                metalGloss.Add(mg);
                return dst;
            }
        }

        if (positions.Count == 0)
            return null;

        // Подгонка как раньше в FitChildToSize: самая длинная сторона = размер груза, центр в нуле.
        Bounds bounds = new Bounds(positions[0], Vector3.zero);
        for (int i = 1; i < positions.Count; i++)
            bounds.Encapsulate(positions[i]);
        float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDim < 0.0001f)
            return null;
        float scale = itemScale / maxDim;
        Vector3 center = bounds.center;
        for (int i = 0; i < positions.Count; i++)
            positions[i] = (positions[i] - center) * scale;

        if (bakedGroup.indices.Count > 0)
        {
            bakedGroup.material = shared;
            groups.Insert(0, bakedGroup);
        }

        var combined = new Mesh { name = "BeltItem_" + item.id };
        combined.hideFlags = HideFlags.DontSave;
        if (positions.Count > 65535)
            combined.indexFormat = IndexFormat.UInt32;
        combined.SetVertices(positions);
        combined.SetNormals(normals);
        combined.SetUVs(0, uv0);
        combined.SetUVs(3, metalGloss);
        combined.SetColors(colors);
        combined.subMeshCount = groups.Count;
        var materials = new Material[groups.Count];
        for (int g = 0; g < groups.Count; g++)
        {
            combined.SetTriangles(groups[g].indices, g, false);
            materials[g] = groups[g].material;
        }
        combined.RecalculateBounds();
        if (needTangents)
            combined.RecalculateTangents();
        combined.UploadMeshData(true);

        return new Baked { mesh = combined, materials = materials };
    }

    static Group GroupFor(List<Group> groups, Material material)
    {
        for (int i = 0; i < groups.Count; i++)
        {
            if (groups[i].material == material)
                return groups[i];
        }
        var group = new Group { material = material };
        groups.Add(group);
        return group;
    }

    /// <summary>Непрозрачный Standard без текстур и свечения — его целиком описывают цвет, металл и гладкость.</summary>
    static bool IsSimple(Material mat)
    {
        if (mat == null || mat.shader == null || mat.shader.name != "Standard")
            return false;
        if (mat.renderQueue > 2450)
            return false;
        if (mat.HasProperty("_Mode") && mat.GetFloat("_Mode") > 0.5f)
            return false;
        if (mat.IsKeywordEnabled("_EMISSION"))
            return false;
        string[] texProps = { "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap", "_ParallaxMap", "_DetailAlbedoMap" };
        for (int i = 0; i < texProps.Length; i++)
        {
            if (mat.HasProperty(texProps[i]) && mat.GetTexture(texProps[i]) != null)
                return false;
        }
        return true;
    }

    static bool ActiveInPrefab(Transform t, Transform root)
    {
        for (Transform cur = t; cur != null; cur = cur.parent)
        {
            if (!cur.gameObject.activeSelf)
                return false;
            if (cur == root)
                break;
        }
        return true;
    }

    /// <summary>Повесить склеенную модель на визуал. Тени груз не отбрасывает.</summary>
    public static bool TryAttach(GameObject root, ItemData item, float itemScale)
    {
        Baked baked = Get(item, itemScale);
        if (baked == null)
            return false;

        var model = new GameObject("Model");
        model.transform.SetParent(root.transform, false);
        model.AddComponent<MeshFilter>().sharedMesh = baked.mesh;
        MeshRenderer mr = model.AddComponent<MeshRenderer>();
        mr.sharedMaterials = baked.materials;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        return true;
    }
}
