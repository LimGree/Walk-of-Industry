using System.Collections.Generic;
using UnityEngine;

public class MachineIdleHud : MonoBehaviour
{
    const float IdleAfter = 5f;
    const float ScanEvery = 0.5f;

    public static MachineIdleHud Instance { get; private set; }

    public struct IdleRow
    {
        public BuildingBase building;
        public string reason;
        public Vector2Int cell;
        public string Name;
    }

    public IReadOnlyList<IdleRow> Rows => rows;

    float nextScan;
    readonly Dictionary<int, float> idleSince = new Dictionary<int, float>();
    readonly Dictionary<int, Transform> marks = new Dictionary<int, Transform>();
    readonly List<IdleRow> rows = new List<IdleRow>(16);
    static Font markFont;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        Billboard();
        if (Time.unscaledTime < nextScan)
            return;
        nextScan = Time.unscaledTime + ScanEvery;
        Scan();
    }

    void Scan()
    {
        rows.Clear();
        float now = Time.unscaledTime;
        var seen = new HashSet<int>();

        CrafterBuilding[] crafters = Object.FindObjectsByType<CrafterBuilding>(FindObjectsSortMode.None);
        for (int i = 0; i < crafters.Length; i++)
            Note(crafters[i], crafters[i] != null ? crafters[i].IdleReason() : null, now, seen);

        Extractor[] extractors = Object.FindObjectsByType<Extractor>(FindObjectsSortMode.None);
        for (int i = 0; i < extractors.Length; i++)
        {
            Extractor ex = extractors[i];
            Note(ex, ex != null && ex.IsOutputJammed ? "output" : null, now, seen);
        }

        var stale = new List<int>();
        foreach (var pair in idleSince)
        {
            if (!seen.Contains(pair.Key))
                stale.Add(pair.Key);
        }

        for (int i = 0; i < stale.Count; i++)
        {
            int id = stale[i];
            idleSince.Remove(id);
            DropMark(id);
        }

        for (int i = 0; i < rows.Count; i++)
            EnsureMark(rows[i].building);
    }

    void Note(BuildingBase building, string reason, float now, HashSet<int> seen)
    {
        if (building == null || !building.IsPlaced || string.IsNullOrEmpty(reason))
            return;
        int id = building.GetInstanceID();
        seen.Add(id);
        if (!idleSince.ContainsKey(id))
            idleSince[id] = now;
        if (now - idleSince[id] < IdleAfter)
            return;
        rows.Add(new IdleRow
        {
            building = building,
            reason = reason,
            cell = BuildingLinker.WorldToCell(building.transform.position),
            Name = building.data != null ? building.data.displayName : "Building"
        });
    }

    void EnsureMark(BuildingBase building)
    {
        if (building == null)
            return;
        int id = building.GetInstanceID();
        if (marks.TryGetValue(id, out Transform existing) && existing != null)
            return;
        Transform mark = CreateMark(building);
        marks[id] = mark;
    }

    static Transform CreateMark(BuildingBase building)
    {
        var go = new GameObject("IdleBang");
        go.transform.SetParent(building.transform, false);
        float y = MarkerHeight(building);
        go.transform.localPosition = new Vector3(0f, y, 0f);
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.text = "!";
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.characterSize = 0.18f;
        tm.fontSize = 64;
        tm.fontStyle = FontStyle.Bold;
        tm.color = new Color(1f, 0.84f, 0.12f, 1f);
        if (markFont == null)
        {
            markFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (markFont == null)
                markFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        if (markFont != null)
            tm.font = markFont;
        MeshRenderer rend = go.GetComponent<MeshRenderer>();
        if (rend != null)
        {
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        return go.transform;
    }

    static float MarkerHeight(BuildingBase building)
    {
        Renderer[] rends = building.GetComponentsInChildren<Renderer>();
        float maxY = 1.4f;
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] == null || !rends[i].enabled)
                continue;
            maxY = Mathf.Max(maxY, rends[i].bounds.max.y - building.transform.position.y);
        }

        return maxY + 0.45f;
    }

    void DropMark(int id)
    {
        if (!marks.TryGetValue(id, out Transform mark))
            return;
        marks.Remove(id);
        if (mark != null)
            Object.Destroy(mark.gameObject);
    }

    void Billboard()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;
        Vector3 camPos = cam.transform.position;
        foreach (var pair in marks)
        {
            Transform mark = pair.Value;
            if (mark == null)
                continue;
            Vector3 toCam = camPos - mark.position;
            if (toCam.sqrMagnitude < 0.01f)
                continue;
            mark.rotation = Quaternion.LookRotation(toCam);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        foreach (var pair in marks)
        {
            if (pair.Value != null)
                Object.Destroy(pair.Value.gameObject);
        }

        marks.Clear();
    }
}
