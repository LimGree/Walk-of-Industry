using System;
using System.Collections.Generic;
using UnityEngine;

public class MapMarkerSystem : MonoBehaviour
{
    public static MapMarkerSystem Instance { get; private set; }

    public const float HideDistance = 14f;

    public static readonly Color[] Palette =
    {
        new Color(1f, 0.38f, 0.32f),
        new Color(0.35f, 0.82f, 1f),
        new Color(0.48f, 1f, 0.52f),
        new Color(1f, 0.84f, 0.28f),
        new Color(0.82f, 0.48f, 1f),
        new Color(1f, 0.58f, 0.22f)
    };

    public readonly List<MapMarkerSave> Markers = new List<MapMarkerSave>();
    public event Action OnChanged;

    int nextId = 1;
    readonly List<Holo> holos = new List<Holo>();
    Camera cam;
    Font font;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        ClearHolos();
    }

    void LateUpdate()
    {
        SyncHolos();
        UpdateHolos();
    }

    public void ResetToNewWorld()
    {
        Markers.Clear();
        nextId = 1;
        ClearHolos();
        OnChanged?.Invoke();
    }

    public MapMarkerSave Add(Vector2Int cell, string label)
    {
        Color color = Palette[Markers.Count % Palette.Length];
        var marker = new MapMarkerSave
        {
            id = nextId++,
            x = cell.x,
            z = cell.y,
            label = string.IsNullOrWhiteSpace(label) ? UiLocale.T("map.marker_default") : label.Trim(),
            r = color.r,
            g = color.g,
            b = color.b,
            hidden = 0,
            createdAt = DateTime.Now.ToString("o")
        };
        Markers.Add(marker);
        OnChanged?.Invoke();
        return marker;
    }

    public void Remove(int id)
    {
        for (int i = Markers.Count - 1; i >= 0; i--)
        {
            if (Markers[i] == null || Markers[i].id != id)
                continue;
            Markers.RemoveAt(i);
            OnChanged?.Invoke();
            return;
        }
    }

    public void Rename(int id, string label)
    {
        MapMarkerSave marker = Find(id);
        if (marker == null)
            return;
        marker.label = string.IsNullOrWhiteSpace(label) ? UiLocale.T("map.marker_default") : label.Trim();
        OnChanged?.Invoke();
    }

    public void SetHidden(int id, bool hidden)
    {
        MapMarkerSave marker = Find(id);
        if (marker == null)
            return;
        marker.hidden = hidden ? 1 : 0;
        OnChanged?.Invoke();
    }

    public void SetColor(int id, Color color)
    {
        MapMarkerSave marker = Find(id);
        if (marker == null)
            return;
        marker.r = color.r;
        marker.g = color.g;
        marker.b = color.b;
        OnChanged?.Invoke();
    }

    public MapMarkerSave Find(int id)
    {
        for (int i = 0; i < Markers.Count; i++)
        {
            if (Markers[i] != null && Markers[i].id == id)
                return Markers[i];
        }
        return null;
    }

    public void CaptureSave(SaveData save)
    {
        if (save == null)
            return;
        save.markers = new List<MapMarkerSave>(Markers);
    }

    public void ApplySave(SaveData save)
    {
        Markers.Clear();
        nextId = 1;
        if (save != null && save.markers != null)
        {
            for (int i = 0; i < save.markers.Count; i++)
            {
                MapMarkerSave row = save.markers[i];
                if (row == null)
                    continue;
                Markers.Add(row);
                if (row.id >= nextId)
                    nextId = row.id + 1;
            }
        }
        OnChanged?.Invoke();
    }

    void SyncHolos()
    {
        while (holos.Count < Markers.Count)
            holos.Add(CreateHolo());
        while (holos.Count > Markers.Count)
        {
            int last = holos.Count - 1;
            if (holos[last] != null && holos[last].root != null)
                Destroy(holos[last].root);
            holos.RemoveAt(last);
        }
    }

    void UpdateHolos()
    {
        if (cam == null)
            cam = WorldView.Cam;
        if (cam == null)
            return;

        bool hologramsOn = MapSettings.Holograms;
        Vector3 camPos = cam.transform.position;
        for (int i = 0; i < Markers.Count; i++)
        {
            MapMarkerSave marker = Markers[i];
            Holo holo = holos[i];
            if (marker == null || holo == null || holo.root == null)
                continue;

            Vector3 world = CellWorld(new Vector2Int(marker.x, marker.z));
            float dist = Vector3.Distance(camPos, world);
            bool show = hologramsOn && marker.hidden == 0 && dist >= HideDistance;
            if (holo.root.activeSelf != show)
                holo.root.SetActive(show);
            if (!show)
                continue;

            Vector3 aim = world + Vector3.up * 8f;
            Vector3 dir = aim - camPos;
            if (dir.sqrMagnitude < 0.01f)
                continue;
            dir.Normalize();
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude < 0.0001f)
                flat = cam.transform.forward;
            flat.Normalize();
            float lift = Mathf.Max(dir.y, 0.08f);
            dir = new Vector3(flat.x, lift, flat.z).normalized;
            float showDist = Mathf.Clamp(dist, 16f, 56f);
            Vector3 pos = camPos + dir * showDist;
            holo.root.transform.position = pos;
            holo.root.transform.rotation = Quaternion.LookRotation(pos - camPos, Vector3.up);
            float scale = Mathf.Clamp(showDist * 0.028f, 0.85f, 1.85f);
            holo.root.transform.localScale = new Vector3(scale, scale, scale);

            string name = string.IsNullOrEmpty(marker.label) ? UiLocale.T("map.marker_default") : marker.label;
            int cells = Mathf.RoundToInt(dist);
            Color color = new Color(marker.r, marker.g, marker.b, 1f);
            float width = Mathf.Clamp(Mathf.Max(name.Length, 4) * 0.38f, 2.6f, 7.2f);
            if (holo.back != null)
            {
                holo.back.transform.localScale = new Vector3(width, 1.25f, 1f);
                holo.back.sharedMaterial.color = new Color(0.04f, 0.1f, 0.14f, 0.82f);
            }
            if (holo.bar != null)
            {
                holo.bar.transform.localScale = new Vector3(width * 0.9f, 0.07f, 1f);
                holo.bar.transform.localPosition = new Vector3(0f, -0.54f, -0.01f);
                holo.bar.sharedMaterial.color = color;
            }
            if (holo.label != null)
            {
                holo.label.text = name;
                holo.label.color = Color.white;
            }
            if (holo.dist != null)
            {
                holo.dist.text = UiLocale.T("map.cells", cells.ToString());
                holo.dist.color = color;
            }
        }
    }

    Holo CreateHolo()
    {
        var root = new GameObject("MapHolo");
        root.transform.SetParent(transform, false);

        var backGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        backGo.name = "Back";
        backGo.transform.SetParent(root.transform, false);
        backGo.transform.localPosition = Vector3.zero;
        backGo.transform.localScale = new Vector3(3.6f, 1.25f, 1f);
        Destroy(backGo.GetComponent<Collider>());
        var back = backGo.GetComponent<MeshRenderer>();
        back.sharedMaterial = RuntimeMaterials.Create(new Color(0.04f, 0.1f, 0.14f, 0.82f));

        var barGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        barGo.name = "Bar";
        barGo.transform.SetParent(root.transform, false);
        barGo.transform.localPosition = new Vector3(0f, -0.54f, -0.01f);
        barGo.transform.localScale = new Vector3(3.2f, 0.07f, 1f);
        Destroy(barGo.GetComponent<Collider>());
        var bar = barGo.GetComponent<MeshRenderer>();
        bar.sharedMaterial = RuntimeMaterials.Create(Color.yellow);

        if (font == null)
            font = Resources.Load<Font>("UI/LiberationSans");

        TextMesh nameTm = MakeText(root.transform, "Label", new Vector3(0f, 0.18f, -0.02f), 0.08f);
        TextMesh distTm = MakeText(root.transform, "Dist", new Vector3(0f, -0.28f, -0.02f), 0.055f);

        root.SetActive(false);
        return new Holo { root = root, back = back, bar = bar, label = nameTm, dist = distTm };
    }

    TextMesh MakeText(Transform parent, string name, Vector3 localPos, float characterSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 52;
        tm.characterSize = characterSize;
        tm.fontStyle = FontStyle.Bold;
        tm.color = Color.white;
        if (font != null)
        {
            tm.font = font;
            MeshRenderer tr = go.GetComponent<MeshRenderer>();
            if (tr != null && font.material != null)
                tr.sharedMaterial = font.material;
        }
        return tm;
    }

    void ClearHolos()
    {
        for (int i = 0; i < holos.Count; i++)
        {
            if (holos[i] != null && holos[i].root != null)
                Destroy(holos[i].root);
        }
        holos.Clear();
    }

    public static Vector3 CellWorld(Vector2Int cell)
    {
        if (GridSystem.Instance != null)
        {
            float size = GridSystem.Instance.cellSize;
            Vector3 origin = GridSystem.Instance.origin;
            return new Vector3(origin.x + cell.x * size, 0f, origin.z + cell.y * size);
        }
        return new Vector3(cell.x, 0f, cell.y);
    }

    class Holo
    {
        public GameObject root;
        public MeshRenderer back;
        public MeshRenderer bar;
        public TextMesh label;
        public TextMesh dist;
    }
}
