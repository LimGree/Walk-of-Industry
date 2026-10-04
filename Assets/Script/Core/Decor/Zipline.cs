using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Тросовая дорога: декор «Опора троса» (покупается в магазине декора, ставится за монеты).
/// Каждая опора соединяется тросом с ближайшей другой в пределах <see cref="MaxCells"/> клеток.
/// E у опоры — едешь по тросу к её паре ([[ZiplineRide]]), Пробел — отцепиться.
/// </summary>
public static class Zipline
{
    public const string PostId = "decor_zipline_post";
    public const float PostHeight = 4.6f;
    public const int MaxCells = 40;
    static readonly Vector3 TopLocal = new Vector3(0f, 4.35f, 0f);

    public static bool IsPost(BuildingBase b) => b != null && b.data != null && b.data.id == PostId;

    public static Vector3 Top(BuildingBase post) => post.transform.TransformPoint(TopLocal);

    /// <summary>Пара опоры: ближайшая другая опора не дальше 40 клеток.</summary>
    public static Decoration Partner(Decoration post)
    {
        if (post == null)
            return null;
        float max = MaxCells * GridFootprint.CellSize;
        Decoration best = null;
        float bestD = max * max;
        IReadOnlyList<Decoration> all = DecorSystem.All;
        for (int i = 0; i < all.Count; i++)
        {
            Decoration d = all[i];
            if (d == null || d == post || !IsPost(d) || !d.IsPlaced)
                continue;
            float dist = (d.transform.position - post.transform.position).sqrMagnitude;
            if (dist <= bestD)
            {
                bestD = dist;
                best = d;
            }
        }

        return best;
    }

    /// <summary>Точка троса между верхушками, с провисом 6% длины посередине.</summary>
    public static Vector3 CablePoint(Vector3 a, Vector3 b, float t)
    {
        Vector3 p = Vector3.Lerp(a, b, t);
        float sag = Vector3.Distance(a, b) * 0.06f;
        p.y -= Mathf.Sin(t * Mathf.PI) * sag;
        return p;
    }

    public static void TryRide(Decoration post, GameObject rider)
    {
        Decoration to = Partner(post);
        if (to == null)
        {
            UiNotification.Push(UiLocale.T("zip.no_pair"), UiLocale.T("zip.no_pair_sub", MaxCells), UiStatus.Warning);
            UiAudio.PlayError();
            return;
        }

        ZiplineRide ride = rider.GetComponent<ZiplineRide>();
        if (ride == null)
            ride = rider.AddComponent<ZiplineRide>();
        ride.Begin(Top(post), Top(to), to);
        EnsureCables();
    }

    // ---------- Модель опоры (кодом: манифеста декора для неё нет) ----------

    public static void BuildPostModel(Transform parent)
    {
        Material steel = RuntimeMaterials.Create(new Color(0.32f, 0.35f, 0.38f));
        Material yellow = RuntimeMaterials.Create(new Color(0.95f, 0.74f, 0.16f));
        Material dark = RuntimeMaterials.Create(new Color(0.09f, 0.09f, 0.1f));
        Box(parent, "Base", new Vector3(0f, 0.06f, 0f), new Vector3(0.8f, 0.12f, 0.8f), dark);
        Box(parent, "Pole", new Vector3(0f, 2.2f, 0f), new Vector3(0.2f, 4.3f, 0.2f), steel);
        for (int i = 0; i < 4; i++)
            Box(parent, "Stripe", new Vector3(0f, 0.45f + i * 0.22f, 0f), new Vector3(0.22f, 0.1f, 0.22f), i % 2 == 0 ? yellow : dark);
        Box(parent, "Arm", new Vector3(0f, 4.38f, 0f), new Vector3(0.18f, 0.16f, 1f), steel);
        Box(parent, "Pulley", new Vector3(0f, 4.3f, 0f), new Vector3(0.12f, 0.3f, 0.3f), yellow);
        Box(parent, "Brace", new Vector3(0f, 3.9f, 0.25f), new Vector3(0.08f, 0.7f, 0.08f), steel);
    }

    static void Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // ---------- Тросы ----------

    static ZiplineCables cables;

    public static void EnsureCables()
    {
        if (cables != null)
            return;
        var go = new GameObject("ZiplineCables");
        cables = go.AddComponent<ZiplineCables>();
    }
}

/// <summary>Рисует тросы между парами опор (пересчёт раз в секунду).</summary>
public class ZiplineCables : MonoBehaviour
{
    readonly List<LineRenderer> lines = new List<LineRenderer>();
    Material mat;
    float next;

    void Update()
    {
        if (Time.unscaledTime < next)
            return;
        next = Time.unscaledTime + 1f;
        if (mat == null)
            mat = RuntimeMaterials.Create(new Color(0.15f, 0.14f, 0.13f));

        var drawn = new HashSet<(int, int)>();
        int used = 0;
        IReadOnlyList<Decoration> all = DecorSystem.All;
        for (int i = 0; i < all.Count; i++)
        {
            Decoration a = all[i];
            if (!Zipline.IsPost(a) || !a.IsPlaced)
                continue;
            Decoration b = Zipline.Partner(a);
            if (b == null)
                continue;
            int ia = a.GetInstanceID(), ib = b.GetInstanceID();
            var key = ia < ib ? (ia, ib) : (ib, ia);
            if (!drawn.Add(key))
                continue;
            LineRenderer lr = Line(used++);
            Vector3 pa = Zipline.Top(a), pb = Zipline.Top(b);
            const int n = 12;
            lr.positionCount = n;
            for (int k = 0; k < n; k++)
                lr.SetPosition(k, Zipline.CablePoint(pa, pb, k / (float)(n - 1)));
            lr.enabled = true;
        }

        for (int i = used; i < lines.Count; i++)
            lines[i].enabled = false;
    }

    LineRenderer Line(int index)
    {
        while (lines.Count <= index)
        {
            var go = new GameObject("Cable");
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lines.Add(lr);
        }

        return lines[index];
    }
}

/// <summary>Езда по тросу: игрок висит под тросом и едет к паре, Пробел — отцепиться.</summary>
public class ZiplineRide : MonoBehaviour
{
    const float Speed = 14f;
    const float HangBelow = 2f;

    public static bool Active { get; private set; }

    Vector3 a;
    Vector3 b;
    Decoration target;
    float t;
    float length;
    CharacterController controller;
    PlayerMovement movement;
    bool savedCanMove;

    public void Begin(Vector3 from, Vector3 to, Decoration toPost)
    {
        if (Active)
            return;
        controller = GetComponent<CharacterController>();
        movement = GetComponent<PlayerMovement>();
        a = from;
        b = to;
        target = toPost;
        t = 0f;
        length = Mathf.Max(0.5f, Vector3.Distance(a, b));
        Active = true;
        if (controller != null)
            controller.enabled = false;
        if (movement != null)
        {
            savedCanMove = movement.canMove;
            movement.canMove = false;
        }

        GameAudio.Player("player_jump");
    }

    void Update()
    {
        if (!Active)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        bool drop = kb != null && kb.spaceKey.wasPressedThisFrame;
        t += Speed * Time.deltaTime / length;
        Vector3 cable = Zipline.CablePoint(a, b, Mathf.Clamp01(t));
        transform.position = cable - Vector3.up * HangBelow;
        if (drop)
        {
            End(false);
            return;
        }

        if (t >= 1f)
            End(true);
    }

    void End(bool arrived)
    {
        Active = false;
        if (arrived && target != null)
        {
            // Спрыгнуть рядом с опорой, а не в неё.
            Vector3 away = (b - a);
            away.y = 0f;
            Vector3 land = target.transform.position + away.normalized * 1.2f;
            land.y += 3f;
            if (Physics.Raycast(land, Vector3.down, out RaycastHit hit, 20f))
                land = hit.point + Vector3.up * 0.1f;
            transform.position = land;
        }

        if (controller != null)
            controller.enabled = true;
        if (movement != null)
            movement.canMove = savedCanMove;
    }
}
