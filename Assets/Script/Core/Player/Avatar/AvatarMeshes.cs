using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Детали персонажа — мягкие скруглённые бруски в стиле зданий. Цвет запечён в вершины,
/// металл/гладкость — в uv4, поэтому весь персонаж рисуется одним материалом
/// (шейдер груза на лентах, Resources/WalkToBiomeBeltItem). Меши кешируются по форме и цвету.
/// </summary>
public static class AvatarMeshes
{
    const int Segments = 4;
    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>(64);
    static Material material;

    public static Material Material
    {
        get
        {
            if (material != null)
                return material;
            Shader shader = Resources.Load<Shader>("WalkToBiomeBeltItem");
            if (shader == null)
                shader = Shader.Find("Hidden/WalkToBiome/BeltItem");
            if (shader == null || !shader.isSupported)
                shader = Shader.Find("Standard");
            material = new Material(shader) { name = "AvatarShared", enableInstancing = true, hideFlags = HideFlags.DontSave };
            return material;
        }
    }

    /// <summary>
    /// Скруглённый брусок с центром в нуле. radius — радиус скругления (обрезается до половины меньшей стороны),
    /// taper — во сколько раз уже по X нижний край (1 — прямой).
    /// </summary>
    public static Mesh RoundedBox(Vector3 size, float radius, Color32 color, float gloss, float metal = 0f, float taper = 1f)
    {
        float r = Mathf.Min(radius, Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.5f);
        string key = size.x.ToString("F3") + "|" + size.y.ToString("F3") + "|" + size.z.ToString("F3") + "|" + r.ToString("F3")
            + "|" + color.r + "," + color.g + "," + color.b + "|" + gloss.ToString("F2") + "|" + metal.ToString("F2") + "|" + taper.ToString("F2");
        if (cache.TryGetValue(key, out Mesh cached) && cached != null)
            return cached;

        var verts = new List<Vector3>(6 * (Segments + 1) * (Segments + 1));
        var normals = new List<Vector3>(verts.Capacity);
        var tris = new List<int>(6 * Segments * Segments * 6);
        Vector3 half = size * 0.5f;
        Vector3 inner = new Vector3(Mathf.Max(0f, half.x - r), Mathf.Max(0f, half.y - r), Mathf.Max(0f, half.z - r));

        // Шесть граней: нормаль n и оси u, v с cross(u, v) = n — треугольник (k, k+1, k+row) смотрит наружу.
        AddFace(Vector3.right, Vector3.back, Vector3.up);
        AddFace(Vector3.left, Vector3.forward, Vector3.up);
        AddFace(Vector3.up, Vector3.right, Vector3.back);
        AddFace(Vector3.down, Vector3.right, Vector3.forward);
        AddFace(Vector3.forward, Vector3.right, Vector3.up);
        AddFace(Vector3.back, Vector3.left, Vector3.up);

        void AddFace(Vector3 n, Vector3 u, Vector3 v)
        {
            int start = verts.Count;
            for (int j = 0; j <= Segments; j++)
            {
                for (int i = 0; i <= Segments; i++)
                {
                    float a = i / (float)Segments * 2f - 1f;
                    float b = j / (float)Segments * 2f - 1f;
                    Vector3 p = n + u * a + v * b; // точка на кубе [-1, 1]
                    Vector3 onBox = Vector3.Scale(p, half);
                    Vector3 core = new Vector3(
                        Mathf.Clamp(onBox.x, -inner.x, inner.x),
                        Mathf.Clamp(onBox.y, -inner.y, inner.y),
                        Mathf.Clamp(onBox.z, -inner.z, inner.z));
                    Vector3 dir = onBox - core;
                    Vector3 normal = dir.sqrMagnitude > 1e-8f ? dir.normalized : n;
                    Vector3 pos = core + normal * r;
                    if (taper != 1f)
                    {
                        float t = Mathf.InverseLerp(-half.y, half.y, pos.y);
                        pos.x *= Mathf.Lerp(taper, 1f, t);
                    }
                    verts.Add(pos);
                    normals.Add(normal);
                }
            }

            int row = Segments + 1;
            for (int j = 0; j < Segments; j++)
            {
                for (int i = 0; i < Segments; i++)
                {
                    int k = start + j * row + i;
                    tris.Add(k);
                    tris.Add(k + 1);
                    tris.Add(k + row);
                    tris.Add(k + 1);
                    tris.Add(k + row + 1);
                    tris.Add(k + row);
                }
            }
        }

        var colors = new List<Color>(verts.Count);
        var mg = new List<Vector2>(verts.Count);
        Color c = ((Color)color).linear;
        if (QualitySettings.activeColorSpace != ColorSpace.Linear)
            c = color;
        for (int i = 0; i < verts.Count; i++)
        {
            colors.Add(c);
            mg.Add(new Vector2(metal, gloss));
        }

        var mesh = new Mesh { name = "Avatar_" + cache.Count, hideFlags = HideFlags.DontSave };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetColors(colors);
        mesh.SetUVs(3, mg);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        mesh.UploadMeshData(true);
        cache[key] = mesh;
        return mesh;
    }
}
