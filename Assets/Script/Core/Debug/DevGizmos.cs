using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Отладочные линии в мире для консоли (/gizmos, /grid show, /drones path): порты зданий,
/// радиусы генераторов, занятость клеток, чанки WorldSim, маршруты дронов.
/// Один меш из линий, пересобирается 5 раз в секунду, рисуется каждый кадр.
/// </summary>
public class DevGizmos : MonoBehaviour
{
    public enum Layer { Ports, Power, Occupancy, Chunks, Drones }

    enum Pen { In, Out, Belt, Power, PowerOff, Cell, Ghost, Chunk, Drone, Route }

    static readonly Color[] PenColors =
    {
        new Color(0.3f, 1f, 0.4f),
        new Color(1f, 0.6f, 0.15f),
        new Color(1f, 0.92f, 0.3f),
        new Color(1f, 0.85f, 0.2f),
        new Color(0.5f, 0.5f, 0.5f),
        new Color(0.3f, 0.85f, 1f),
        new Color(1f, 0.25f, 0.25f),
        new Color(0.95f, 0.35f, 1f),
        new Color(1f, 1f, 1f),
        new Color(0.45f, 0.6f, 1f)
    };

    const int LayerCount = 5;
    const float Rebuild = 0.2f;
    const float Near = 24f;

    static DevGizmos instance;
    static readonly bool[] layers = new bool[LayerCount];

    readonly List<Vector3> verts = new List<Vector3>(4096);
    readonly List<int>[] lines = new List<int>[PenColors.Length];
    Material[] mats;
    Mesh mesh;
    float nextBuild;

    public static bool IsOn(Layer layer) => layers[(int)layer];

    public static bool Any
    {
        get
        {
            for (int i = 0; i < LayerCount; i++)
            {
                if (layers[i])
                    return true;
            }

            return false;
        }
    }

    public static void Set(Layer layer, bool on)
    {
        layers[(int)layer] = on;
        if (on)
            Ensure();
        if (instance != null)
            instance.nextBuild = 0f;
    }

    public static void Clear()
    {
        for (int i = 0; i < LayerCount; i++)
            layers[i] = false;
    }

    static void Ensure()
    {
        if (instance != null)
            return;
        var go = new GameObject("DevGizmos");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<DevGizmos>();
    }

    void Awake()
    {
        for (int i = 0; i < lines.Length; i++)
            lines[i] = new List<int>(512);
        mesh = new Mesh { name = "DevGizmos" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.MarkDynamic();
        mats = new Material[PenColors.Length];
        for (int i = 0; i < mats.Length; i++)
        {
            Material mat = RuntimeMaterials.Create(PenColors[i]);
            if (mat.HasProperty("_WalkLightTint"))
                mat.SetColor("_WalkLightTint", Color.white);
            if (mat.HasProperty("_ZTest"))
                mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mat.renderQueue = 4000;
            mats[i] = mat;
        }
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
        if (mesh != null)
            Destroy(mesh);
    }

    void LateUpdate()
    {
        if (!Any)
            return;
        if (Time.unscaledTime >= nextBuild)
        {
            nextBuild = Time.unscaledTime + Rebuild;
            BuildMesh();
        }

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Count > 0)
                Graphics.DrawMesh(mesh, Matrix4x4.identity, mats[i], 0, null, i);
        }
    }

    void BuildMesh()
    {
        verts.Clear();
        for (int i = 0; i < lines.Length; i++)
            lines[i].Clear();

        Vector3 focus = Focus();
        if (IsOn(Layer.Occupancy))
            DrawOccupancy(focus);
        if (IsOn(Layer.Ports))
            DrawPorts(focus);
        if (IsOn(Layer.Power))
            DrawPower();
        if (IsOn(Layer.Chunks))
            DrawChunks(focus);
        if (IsOn(Layer.Drones))
            DrawDrones();

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.subMeshCount = lines.Length;
        for (int i = 0; i < lines.Length; i++)
            mesh.SetIndices(lines[i], MeshTopology.Lines, i, false);
        mesh.RecalculateBounds();
        mesh.bounds = new Bounds(focus, Vector3.one * 100000f);
    }

    static Vector3 Focus()
    {
        PlayerMovement move = FindFirstObjectByType<PlayerMovement>();
        if (move != null)
            return move.transform.position;
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    void Line(Pen pen, Vector3 a, Vector3 b)
    {
        int i = verts.Count;
        verts.Add(a);
        verts.Add(b);
        lines[(int)pen].Add(i);
        lines[(int)pen].Add(i + 1);
    }

    void Arrow(Pen pen, Vector3 from, Vector3 to)
    {
        Line(pen, from, to);
        Vector3 d = to - from;
        d.y = 0f;
        if (d.sqrMagnitude < 0.0001f)
            return;
        d = d.normalized * 0.18f;
        Vector3 side = new Vector3(-d.z, 0f, d.x);
        Line(pen, to, to - d + side);
        Line(pen, to, to - d - side);
    }

    void Square(Pen pen, Vector2Int cell, float y, float inset)
    {
        float cs = GridFootprint.CellSize;
        Vector3 c = GridSystem.Instance != null ? GridSystem.Instance.GetCellCenter(cell, y) : new Vector3(cell.x * cs, y, cell.y * cs);
        float h = cs * 0.5f - inset;
        Vector3 a = c + new Vector3(-h, 0f, -h);
        Vector3 b = c + new Vector3(h, 0f, -h);
        Vector3 e = c + new Vector3(h, 0f, h);
        Vector3 f = c + new Vector3(-h, 0f, h);
        Line(pen, a, b);
        Line(pen, b, e);
        Line(pen, e, f);
        Line(pen, f, a);
    }

    void Circle(Pen pen, Vector3 center, float radius)
    {
        const int Seg = 48;
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= Seg; i++)
        {
            float t = i / (float)Seg * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(t) * radius, 0f, Mathf.Sin(t) * radius);
            Line(pen, prev, next);
            prev = next;
        }
    }

    void DrawOccupancy(Vector3 focus)
    {
        Vector2Int origin = BuildingLinker.WorldToCell(focus);
        int r = Mathf.RoundToInt(Near);
        for (int x = -r; x <= r; x++)
        {
            for (int y = -r; y <= r; y++)
            {
                Vector2Int cell = origin + new Vector2Int(x, y);
                GameObject obj = GridOccupancy.GetAt(cell);
                if (obj == null)
                    continue;
                BuildingBase b = obj.GetComponent<BuildingBase>();
                bool ok = b != null && b.IsPlaced;
                Square(ok ? Pen.Cell : Pen.Ghost, cell, obj.transform.position.y + 0.06f, 0.06f);
            }
        }
    }

    void DrawPorts(Vector3 focus)
    {
        float r2 = Near * Near;
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            BuildingBase b = all[i];
            if (b == null || !b.IsPlaced)
                continue;
            Vector3 p = b.transform.position;
            if ((p - focus).sqrMagnitude > r2)
                continue;
            if (b is Conveyor belt)
            {
                Vector3 up = p + Vector3.up * 0.55f;
                Arrow(Pen.Belt, up - BuildingLinker.CardinalToWorld(belt.ExitDir) * 0.3f, up + BuildingLinker.CardinalToWorld(belt.ExitDir) * 0.35f);
                continue;
            }

            Sockets(b.inputSockets, Pen.In, true, p.y);
            Sockets(b.outputSockets, Pen.Out, false, p.y);
        }
    }

    void Sockets(BuildingSocket[] sockets, Pen pen, bool input, float baseY)
    {
        if (sockets == null)
            return;
        for (int i = 0; i < sockets.Length; i++)
        {
            BuildingSocket s = sockets[i];
            if (s == null)
                continue;
            Vector2Int front = BuildingLinker.GetSocketFrontCell(s);
            Vector3 outside = GridSystem.Instance != null
                ? GridSystem.Instance.GetCellCenter(front, baseY + 0.6f)
                : new Vector3(front.x, baseY + 0.6f, front.y);
            Vector3 at = s.transform.position;
            at.y = baseY + 0.6f;
            if (input)
                Arrow(pen, outside, at);
            else
                Arrow(pen, at, outside);
        }
    }

    void DrawPower()
    {
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            if (!(all[i] is PowerGenerator g) || g == null || !g.IsPlaced)
                continue;
            Vector3 c = g.transform.position + Vector3.up * 0.15f;
            Pen pen = g.Powered ? Pen.Power : Pen.PowerOff;
            Circle(pen, c, g.powerRadius);
            Line(pen, c, c + Vector3.up * 3f);
        }
    }

    void DrawChunks(Vector3 focus)
    {
        float size = WorldSim.CullCell;
        int cx = Mathf.FloorToInt(focus.x / size);
        int cz = Mathf.FloorToInt(focus.z / size);
        const int R = 3;
        float y = focus.y + 0.2f;
        float x0 = (cx - R) * size;
        float x1 = (cx + R + 1) * size;
        float z0 = (cz - R) * size;
        float z1 = (cz + R + 1) * size;
        for (int i = -R; i <= R + 1; i++)
        {
            float x = (cx + i) * size;
            float z = (cz + i) * size;
            Line(Pen.Chunk, new Vector3(x, y, z0), new Vector3(x, y, z1));
            Line(Pen.Chunk, new Vector3(x0, y, z), new Vector3(x1, y, z));
        }
    }

    void DrawDrones()
    {
        DroneLoadStation[] stations = FindObjectsByType<DroneLoadStation>(FindObjectsSortMode.None);
        for (int i = 0; i < stations.Length; i++)
        {
            DroneLoadStation st = stations[i];
            if (st == null || !st.IsPlaced || st.Target == null)
                continue;
            float y = st.transform.position.y + DroneNetwork.CruiseHeight;
            Vector3 a = st.transform.position;
            Vector3 b = st.Target.transform.position;
            Line(Pen.Route, new Vector3(a.x, y, a.z), new Vector3(b.x, y, b.z));
            Line(Pen.Route, a, new Vector3(a.x, y, a.z));
            Line(Pen.Route, b, new Vector3(b.x, y, b.z));
        }

        for (int i = 0; i < DroneNetwork.AllDrones.Count; i++)
        {
            Drone d = DroneNetwork.AllDrones[i];
            if (d == null || !d.IsBusy)
                continue;
            Arrow(Pen.Drone, d.transform.position, d.DevGoal());
        }
    }
}
