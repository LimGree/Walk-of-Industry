using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class BeltItemView
{
    static readonly Dictionary<int, Stack<Transform>> pool = new Dictionary<int, Stack<Transform>>(16);
    static readonly HashSet<Transform> pooled = new HashSet<Transform>();

    public static void BeginFrame()
    {
    }

    public static void Flush()
    {
    }

    public static void ClearPool()
    {
        foreach (KeyValuePair<int, Stack<Transform>> pair in pool)
        {
            while (pair.Value.Count > 0)
            {
                Transform t = pair.Value.Pop();
                if (t != null)
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

    public static Transform Rent(ItemData item, float itemScale)
    {
        int key = item != null ? item.GetInstanceID() : 0;
        Stack<Transform> stack;
        if (pool.TryGetValue(key, out stack) && stack.Count > 0)
        {
            Transform recycled = stack.Pop();
            if (recycled != null)
            {
                pooled.Remove(recycled);
                recycled.gameObject.SetActive(true);
                Prepare(recycled);
                return recycled;
            }
        }
        return Create(item, itemScale);
    }

    public static void Release(Transform visual, ItemData item)
    {
        if (visual == null)
            return;
        if (!pooled.Add(visual))
            return;
        visual.SetParent(null, false);
        visual.gameObject.SetActive(false);
        int key = item != null ? item.GetInstanceID() : 0;
        Stack<Transform> stack;
        if (!pool.TryGetValue(key, out stack))
        {
            stack = new Stack<Transform>(8);
            pool[key] = stack;
        }
        stack.Push(visual);
    }

    public static Transform Create(ItemData item, float itemScale)
    {
        GameObject root = new GameObject(item != null ? "BeltItem_" + item.id : "BeltItem");
        root.hideFlags = HideFlags.DontSave;
        if (!TryAttachWorldModel(root, item, itemScale) && !TryAttachIcon(root, item, itemScale))
            AttachFallbackCube(root, itemScale);

        DisableColliders(root);
        ApplyWorldCullLayer(root);
        return root.transform;
    }

    public static void Prepare(Transform visual)
    {
        if (visual == null)
            return;
        visual.SetParent(null, true);
        DisableColliders(visual.gameObject);
        ApplyWorldCullLayer(visual.gameObject);
    }

    public static void Update(Transform visual, Vector3 position, Vector3 look)
    {
        if (visual == null)
            return;

        visual.position = position;

        SpriteRenderer sprite = visual.GetComponent<SpriteRenderer>();
        if (sprite != null)
        {
            Camera cam = WorldView.Cam;
            if (cam != null)
            {
                Vector3 toCam = visual.position - cam.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                    visual.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
            }
            return;
        }

        look.y = 0f;
        if (look.sqrMagnitude > 0.0001f)
            visual.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
    }

    public static void Destroy(Transform visual)
    {
        if (visual != null)
            Object.Destroy(visual.gameObject);
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
            Object.Destroy(model);
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
        int layer = LayerMask.NameToLayer("buildings");
        if (layer >= 0)
            SetLayer(go, layer);
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
