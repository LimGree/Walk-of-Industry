using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Визуалы предметов на лентах/сплиттерах/руке: пул по типу предмета и кеш на каждый визуал
/// (рендереры, спрайт, ключ, последняя поза). Каждый кадр — только позиция, и та лишь если изменилась.
/// Владелец (лента, сплиттер, рука) держит ссылку на визуал; «брошенным» считается визуал,
/// который никто не рисовал два кадра подряд — он уходит в пул своего типа.
/// </summary>
public static class BeltItemView
{
    /// <summary>Больше этого в пуле одного типа не держим — лишнее удаляется.</summary>
    const int MaxPooledPerKey = 256;
    const int PruneEvery = 600;

    sealed class Info
    {
        public int key;
        public Renderer[] rends;
        public SpriteRenderer sprite;
        public bool prepared;
        public int lastDraw;
        public bool placed;
        public Vector3 pos;
        public Quaternion rot;
    }

    static readonly Dictionary<int, Stack<Transform>> pool = new Dictionary<int, Stack<Transform>>(16);
    static readonly Dictionary<Transform, Info> infos = new Dictionary<Transform, Info>(256);
    static readonly HashSet<Transform> pooled = new HashSet<Transform>();
    static readonly HashSet<Transform> live = new HashSet<Transform>();
    static readonly List<Transform> flushScratch = new List<Transform>(64);
    static readonly List<Transform> pruneScratch = new List<Transform>(64);
    static int cullLayer = int.MinValue;
    static int nextPrune;

    public static int LiveCount => live.Count;

    public static void BeginFrame()
    {
    }

    public static void Flush()
    {
        int frame = Time.frameCount;
        flushScratch.Clear();
        foreach (Transform visual in live)
        {
            if (visual == null)
            {
                flushScratch.Add(visual);
                continue;
            }

            // Рисовали в этом или прошлом кадре — у визуала есть владелец (порядок Update у скриптов не задан).
            if (infos.TryGetValue(visual, out Info info) && info.lastDraw >= frame - 1)
                continue;
            // Висит на ком-то (рука держит предмет, спрятанный вдали) — тоже не брошен.
            if (visual.parent != null)
                continue;
            flushScratch.Add(visual);
        }

        for (int i = 0; i < flushScratch.Count; i++)
        {
            Transform visual = flushScratch[i];
            live.Remove(visual);
            if (visual != null)
                Release(visual, null);
        }

        if (frame >= nextPrune)
        {
            nextPrune = frame + PruneEvery;
            Prune();
        }
    }

    /// <summary>Убрать из кеша визуалы, уничтоженные в обход пула.</summary>
    static void Prune()
    {
        pruneScratch.Clear();
        foreach (KeyValuePair<Transform, Info> pair in infos)
        {
            if (pair.Key == null)
                pruneScratch.Add(pair.Key);
        }

        for (int i = 0; i < pruneScratch.Count; i++)
        {
            infos.Remove(pruneScratch[i]);
            pooled.Remove(pruneScratch[i]);
        }

        live.RemoveWhere(t => t == null);
    }

    public static void ClearPool()
    {
        foreach (KeyValuePair<int, Stack<Transform>> pair in pool)
        {
            while (pair.Value.Count > 0)
            {
                Transform t = pair.Value.Pop();
                if (t == null)
                    continue;
                infos.Remove(t);
                Object.Destroy(t.gameObject);
            }
        }
        pool.Clear();
        pooled.Clear();
    }

    public static int PooledCount
    {
        get
        {
            int n = 0;
            foreach (KeyValuePair<int, Stack<Transform>> pair in pool)
                n += pair.Value.Count;
            return n;
        }
    }

    static int KeyOf(ItemData item)
    {
        return item != null ? item.GetInstanceID() : 0;
    }

    static Info InfoOf(Transform visual, ItemData item)
    {
        if (infos.TryGetValue(visual, out Info info))
            return info;
        info = new Info
        {
            key = KeyOf(item),
            rends = visual.GetComponentsInChildren<Renderer>(true)
        };
        // TryGetComponent не создаёт в редакторе объект-ошибку при промахе (в отличие от GetComponent)
        visual.TryGetComponent(out info.sprite);
        infos[visual] = info;
        return info;
    }

    public static Transform Rent(ItemData item, float itemScale)
    {
        int key = KeyOf(item);
        if (pool.TryGetValue(key, out Stack<Transform> stack))
        {
            while (stack.Count > 0)
            {
                Transform recycled = stack.Pop();
                if (recycled == null)
                    continue;
                pooled.Remove(recycled);
                Info info = InfoOf(recycled, item);
                info.key = key;
                info.placed = false;
                info.lastDraw = Time.frameCount;
                recycled.gameObject.SetActive(true);
                EnableRenderers(info);
                Prepare(recycled);
                return recycled;
            }
        }

        Transform created = Create(item, itemScale);
        if (created != null)
        {
            InfoOf(created, item).lastDraw = Time.frameCount;
            live.Add(created);
        }
        return created;
    }

    public static void Release(Transform visual, ItemData item)
    {
        if (visual == null)
            return;
        live.Remove(visual);
        if (!pooled.Add(visual))
            return;
        Info info = InfoOf(visual, item);
        visual.SetParent(null, false);
        visual.gameObject.SetActive(false);
        info.placed = false;
        if (!pool.TryGetValue(info.key, out Stack<Transform> stack))
        {
            stack = new Stack<Transform>(8);
            pool[info.key] = stack;
        }

        if (stack.Count >= MaxPooledPerKey)
        {
            pooled.Remove(visual);
            infos.Remove(visual);
            Object.Destroy(visual.gameObject);
            return;
        }

        stack.Push(visual);
    }

    public static Transform Create(ItemData item, float itemScale)
    {
        GameObject root = new GameObject(item != null ? "BeltItem_" + item.id : "BeltItem");
        root.hideFlags = HideFlags.DontSave;
        if (!BeltItemBake.TryAttach(root, item, itemScale)
            && !TryAttachWorldModel(root, item, itemScale)
            && !TryAttachIcon(root, item, itemScale))
            AttachFallbackCube(root, itemScale);

        DisableShadows(root);
        DisableColliders(root);
        ApplyWorldCullLayer(root);
        Info info = InfoOf(root.transform, item);
        info.prepared = true;
        return root.transform;
    }

    /// <summary>Визуал перешёл к новому владельцу (лента приняла предмет вместе с ним).</summary>
    public static void Prepare(Transform visual)
    {
        if (visual == null)
            return;
        visual.SetParent(null, true);
        Info info = InfoOf(visual, null);
        if (!info.prepared)
        {
            DisableColliders(visual.gameObject);
            ApplyWorldCullLayer(visual.gameObject);
            info.prepared = true;
        }

        info.lastDraw = Time.frameCount;
        live.Add(visual);
    }

    public static void Update(Transform visual, Vector3 position, Vector3 look)
    {
        if (visual == null)
            return;
        Info info = InfoOf(visual, null);
        info.lastDraw = Time.frameCount;
        live.Add(visual);
        GameObject go = visual.gameObject;
        if (!go.activeSelf)
        {
            go.SetActive(true);
            info.placed = false;
        }
        EnableRenderers(info);

        Quaternion rot;
        if (info.sprite != null)
        {
            Camera cam = WorldView.Cam;
            Vector3 toCam = cam != null ? position - cam.transform.position : Vector3.zero;
            rot = toCam.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(toCam.normalized, Vector3.up) : info.rot;
        }
        else
        {
            look.y = 0f;
            rot = look.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(look.normalized, Vector3.up) : info.rot;
        }

        // Стоящий груз (забитая лента) не трогаем: запись в Transform — самое дорогое здесь.
        if (info.placed && info.pos == position && info.rot == rot)
            return;
        visual.SetPositionAndRotation(position, rot);
        info.pos = position;
        info.rot = rot;
        info.placed = true;
    }

    public static void Destroy(Transform visual)
    {
        if (visual == null)
            return;
        live.Remove(visual);
        pooled.Remove(visual);
        infos.Remove(visual);
        Object.Destroy(visual.gameObject);
    }

    static void EnableRenderers(Info info)
    {
        Renderer[] rends = info.rends;
        if (rends == null)
            return;
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null && !rends[i].enabled)
                rends[i].enabled = true;
        }
    }

    static bool TryAttachWorldModel(GameObject root, ItemData item, float itemScale)
    {
        if (item == null || item.worldPrefab == null)
            return false;

        GameObject model = Object.Instantiate(item.worldPrefab, root.transform);
        model.name = "Model";
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        DisableColliders(model);

        if (!FitChildToSize(root.transform, model.transform, itemScale))
        {
            Object.DestroyImmediate(model);
            return false;
        }

        return true;
    }

    static bool TryAttachIcon(GameObject root, ItemData item, float itemScale)
    {
        if (item == null || item.icon == null)
            return false;

        SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
        sr.sprite = item.icon;
        sr.shadowCastingMode = ShadowCastingMode.Off;
        sr.receiveShadows = false;

        float maxDim = Mathf.Max(item.icon.bounds.size.x, item.icon.bounds.size.y, 0.001f);
        root.transform.localScale = Vector3.one * (itemScale / maxDim);
        return true;
    }

    static void AttachFallbackCube(GameObject root, float itemScale)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Cube";
        cube.transform.SetParent(root.transform, false);
        cube.transform.localScale = Vector3.one * itemScale;
        DisableColliders(cube);
    }

    static bool FitChildToSize(Transform root, Transform child, float targetSize)
    {
        Renderer[] renderers = child.GetComponentsInChildren<Renderer>();
        if (renderers == null || renderers.Length == 0)
            return false;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        float maxDim = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
        if (maxDim < 0.0001f)
            return false;

        child.localScale *= targetSize / maxDim;

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                bounds.Encapsulate(renderers[i].bounds);
        }

        child.position += root.position - bounds.center;
        return true;
    }

    /// <summary>Груз мелкий и его много — тени от него почти не видны, а теневых проходов добавляют.</summary>
    static void DisableShadows(GameObject go)
    {
        Renderer[] rends = go.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
            rends[i].shadowCastingMode = ShadowCastingMode.Off;
    }

    static void DisableColliders(GameObject go)
    {
        if (go == null)
            return;

        Collider[] cols = go.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }
    }

    public static void ApplyWorldCullLayer(GameObject go)
    {
        if (cullLayer == int.MinValue)
            cullLayer = LayerMask.NameToLayer("buildings");
        if (cullLayer >= 0)
            SetLayer(go, cullLayer);
    }

    static void SetLayer(GameObject go, int layer)
    {
        if (go == null)
            return;
        go.layer = layer;
        Transform[] kids = go.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < kids.Length; i++)
        {
            if (kids[i] != null)
                kids[i].gameObject.layer = layer;
        }
    }
}
