using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Подмена старых FBX-моделей на новые low-poly из [[ModelLibrary]] прямо в экземпляре здания.
/// Иерархия префаба не трогается: удаляются только Renderer/MeshFilter старой модели,
/// новая вешается в тот же объект (Level1/Level2 остаются, коллайдеры, сокеты и стрелки — тоже).
/// Порты входа/выхода ставятся по реальным сокетам. Ленты/трубы — по форме и входам (<see cref="SyncBelt"/>).
/// Нет модели в Resources — остаётся старая.
/// </summary>
public static class BuildingRestyle
{
    public const string VisualName = "WiVisual";
    public const string PortsName = "WiPorts";
    public const string BeltName = "WiBelt";
    public const string ArrowsName = "Arrows";

    /// <summary>id здания → модели по уровням (1 элемент — без уровней).</summary>
    static readonly Dictionary<string, string[]> Models = new Dictionary<string, string[]>
    {
        { "smelter", new[] { "smelter" } },
        { "assembler", new[] { "assembler_1", "assembler_2" } },
        { "extractor", new[] { "extractor_1", "extractor_2" } },
        { "storage_container", new[] { "storage_container" } },
        { "constructor", new[] { "constructor" } },
        { "chemical_plant", new[] { "chemical_plant" } },
        { "refinery", new[] { "refinery" } },
        { "research_lab", new[] { "research_lab" } },
        { "power_generator", new[] { "power_generator" } },
        { "oil_extractor", new[] { "oil_extractor" } },
        { "water_extractor", new[] { "water_extractor" } },
        { "fluid_storage_tank", new[] { "fluid_storage_tank" } },
        { "splitter", new[] { "splitter" } },
        { "robotic_arm", new[] { "robotic_arm" } },
        { "pipe_splitter", new[] { "pipe_splitter" } },
    };

    public static bool Enabled = true;

    // ---------- Здания ----------

    public static void Apply(BuildingBase b)
    {
        if (!Enabled || b == null || b.data == null)
            return;
        if (b is Conveyor || b is DroneLoadStation || b is DroneUnloadStation)
            return;
        Transform root = b.transform;
        Vector2 size = LocalSize(b);
        Transform marker = root.Find(VisualName);
        if (marker == null)
        {
            // Префаб без вшитой модели (старый) — подменяем на лету.
            string[] levels = ModelsFor(b);
            if (levels == null || !ModelLibrary.Has(levels[0]))
                return;

            StripOld(root);
            var go = new GameObject(VisualName);
            go.transform.SetParent(root, false);
            go.layer = root.gameObject.layer;
            marker = go.transform;

            Transform l1 = levels.Length > 1 ? BuildingPrefabLayout.FindLevel1(root) : null;
            Transform l2 = levels.Length > 1 ? BuildingPrefabLayout.FindLevel2(root) : null;
            if (l1 != null && l2 != null)
            {
                ModelLibrary.AttachAligned(levels[0], l1, root, size.x, "WiLevel1");
                ModelLibrary.AttachAligned(levels[1], l2, root, size.x, "WiLevel2");
            }
            else
                ModelLibrary.AttachAligned(levels[0], marker, root, size.x, "WiModel");
        }

        // Модель уже в префабе (WiVisual) — достраиваем только порты по сокетам.
        if (WantsPorts(b) && marker.Find(PortsName) == null)
        {
            if (b is CrafterBuilding || b is Extractor)
                BuildingPrefabLayout.ApplyPrimarySockets(b);
            AddPorts(b, marker, size);
        }
    }

    static string[] ModelsFor(BuildingBase b)
    {
        if (b is UndergroundConveyor u)
            return new[] { u.isExit ? "underground_out" : "underground_in" };
        Models.TryGetValue(b.data.id ?? "", out string[] list);
        return list;
    }

    static bool WantsPorts(BuildingBase b)
    {
        return !(b is Splitter || b is PipeSplitter || b is RoboticArm || b is UndergroundConveyor);
    }

    static Vector2 LocalSize(BuildingBase b)
    {
        float cell = GridFootprint.CellSize;
        Vector2Int s = b.data != null ? b.data.size : Vector2Int.one;
        return new Vector2(s.x * cell, s.y * cell);
    }

    /// <summary>Снести отрисовку старой модели, не трогая сокеты, стрелки, груз и наши визуалы.</summary>
    public static void StripOld(Transform root)
    {
        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            Renderer r = rends[i];
            if (r == null || !(r is MeshRenderer || r is SkinnedMeshRenderer))
                continue;
            if (Keep(r.transform, root))
                continue;
            GameObject go = r.gameObject;
            if (go.GetComponent<TextMesh>() != null)
                continue;
            Object.DestroyImmediate(r);
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null)
                Object.DestroyImmediate(mf);
        }
    }

    static bool Keep(Transform t, Transform root)
    {
        while (t != null && t != root)
        {
            string n = t.name;
            if (n == VisualName || n == BeltName || n == PortsName || n == "WiLevel1" || n == "WiLevel2"
                || n == "WiModel" || n.StartsWith("Wiport_")
                || n.StartsWith("IoArrow") || n.StartsWith("io_arrow") || n.StartsWith("SocketArrow")
                || n.IndexOf("BeltItem", System.StringComparison.OrdinalIgnoreCase) >= 0
                || n == "BreakdownMark" || n == "IdleBang")
                return true;
            if (t.GetComponent<BuildingSocket>() != null || t.GetComponent<SocketArrow>() != null)
                return true;
            t = t.parent;
        }

        return false;
    }

    /// <summary>Сокеты поменялись (рецепт НПЗ) — пересобрать порты.</summary>
    public static void RefreshPorts(BuildingBase b)
    {
        if (b == null)
            return;
        Transform marker = b.transform.Find(VisualName);
        if (marker == null)
            return;
        Transform old = marker.Find(PortsName);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        if (WantsPorts(b))
            AddPorts(b, marker, LocalSize(b));
    }

    static void AddPorts(BuildingBase b, Transform parent, Vector2 size)
    {
        Transform root = b.transform;
        var ports = new GameObject(PortsName);
        ports.transform.SetParent(parent, false);
        ports.layer = root.gameObject.layer;
        var used = new List<Vector3>();
        AddPortSet(b, b.inputSockets, true, root, ports.transform, size, used);
        AddPortSet(b, b.outputSockets, false, root, ports.transform, size, used);
    }

    static bool IsFluidSocket(BuildingBase b, BuildingSocket s)
    {
        if (b is Refinery refinery && refinery.IsPipeSocket(s))
            return true;
        if (b is OilExtractor || b is WaterExtractor || b is FluidStorageTank)
            return true;
        return s.name.IndexOf("Fluid", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static void AddPortSet(BuildingBase b, BuildingSocket[] sockets, bool input, Transform root, Transform parent, Vector2 size, List<Vector3> used)
    {
        if (sockets == null)
            return;
        float hx = size.x * 0.5f;
        float hz = size.y * 0.5f;
        for (int i = 0; i < sockets.Length; i++)
        {
            BuildingSocket s = sockets[i];
            if (s == null)
                continue;
            Vector3 local = root.InverseTransformPoint(s.transform.position);
            Vector3 outward = root.InverseTransformDirection(s.GetOutward());
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.0001f)
                continue;
            Vector3 pos;
            float yaw;
            if (Mathf.Abs(outward.z) >= Mathf.Abs(outward.x))
            {
                float side = Mathf.Sign(outward.z);
                pos = new Vector3(Mathf.Clamp(local.x, -hx + 0.3f, hx - 0.3f), 0f, side * hz);
                yaw = side > 0f ? 0f : 180f;
            }
            else
            {
                float side = Mathf.Sign(outward.x);
                pos = new Vector3(side * hx, 0f, Mathf.Clamp(local.z, -hz + 0.3f, hz - 0.3f));
                yaw = side > 0f ? 90f : 270f;
            }

            bool dup = false;
            for (int u = 0; u < used.Count; u++)
            {
                if ((used[u] - pos).sqrMagnitude < 0.04f)
                    dup = true;
            }

            if (dup)
                continue;
            used.Add(pos);
            bool fluid = IsFluidSocket(b, s);
            string model = fluid ? "port_fluid" : input ? "port_in" : "port_out";
            GameObject go = ModelLibrary.Spawn(model, 0f);
            if (go == null)
                continue;
            go.name = "Wi" + model;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.layer = parent.gameObject.layer;
            foreach (Transform c in go.GetComponentsInChildren<Transform>(true))
                c.gameObject.layer = parent.gameObject.layer;
        }
    }

    // ---------- Ленты и трубы ----------

    public static string BeltVariant(BeltShape shape, BeltInMask mask)
    {
        bool left = BeltRules.Has(mask, BeltInMask.Left);
        bool right = BeltRules.Has(mask, BeltInMask.Right);
        switch (shape)
        {
            case BeltShape.Corner: return left ? "corner_l" : "corner_r";
            case BeltShape.Tee: return left && !right ? "tee_l" : "tee_r";
            case BeltShape.Sides: return "sides";
            case BeltShape.Triple: return "triple";
            default: return "straight";
        }
    }

    /// <summary>
    /// Лента/труба: в показанной форме держим модель нужного варианта в осях самой ленты
    /// (выход +Z), без доворота и зеркала формы — шевроны всегда смотрят по ходу груза.
    /// </summary>
    public static void SyncBelt(Conveyor belt, GameObject shown, BeltShape shape, BeltInMask mask)
    {
        if (!Enabled || belt == null || shown == null)
            return;
        string model = (belt is Pipe ? "pipe_" : "belt_") + BeltVariant(shape, mask);
        if (!ModelLibrary.Has(model))
            return;

        Transform holder = shown.transform;
        Transform existing = holder.Find(BeltName);
        if (existing != null && existing.childCount > 0 && existing.GetChild(0).name == model)
        {
            Align(existing, holder, belt.transform);
            return;
        }

        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);
        StripOld(holder);
        var wrap = new GameObject(BeltName);
        wrap.layer = holder.gameObject.layer;
        wrap.transform.SetParent(holder, false);
        Transform t = ModelLibrary.AttachAligned(model, wrap.transform, belt.transform, 0f, model);
        if (t == null)
        {
            Object.DestroyImmediate(wrap);
            return;
        }

        t.localPosition = Vector3.zero;
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        Align(wrap.transform, holder, belt.transform);
    }

    static void Align(Transform wrap, Transform holder, Transform belt)
    {
        wrap.position = belt.position;
        wrap.rotation = belt.rotation;
        // Зеркало формы (scale.x < 0 у holder) снимаем своим отрицательным X:
        // у каждого варианта своя модель, зеркалить не нужно.
        Vector3 hs = holder.lossyScale;
        Vector3 bs = belt.lossyScale;
        float sx = Mathf.Abs(hs.x) < 0.0001f ? 1f : hs.x;
        wrap.localScale = new Vector3(
            Mathf.Abs(bs.x) / sx,
            Mathf.Abs(bs.y) / Mathf.Max(0.0001f, Mathf.Abs(hs.y)),
            Mathf.Abs(bs.z) / Mathf.Max(0.0001f, Mathf.Abs(hs.z)));
    }

    public static bool HasWiBelt(Conveyor belt)
    {
        if (belt == null)
            return false;
        foreach (Transform t in belt.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == BeltName)
                return true;
        }

        return false;
    }

    public const string EdgeMaterial = "wi_edge";

    /// <summary>
    /// Фильтр ленты красит края (рельсы, материал wi_edge) в цвет предмета; без фильтра — родной цвет.
    /// Красим по индексу материала, поэтому неважно, как импорт OBJ разбил объекты.
    /// </summary>
    public static void PaintBeltEdges(Conveyor belt, Color? tint, MaterialPropertyBlock block, int colorId, int baseColorId)
    {
        foreach (Renderer r in belt.GetComponentsInChildren<Renderer>(true))
        {
            if (r == null || r is SpriteRenderer)
                continue;
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !mats[i].name.StartsWith(EdgeMaterial))
                    continue;
                block.Clear();
                if (tint.HasValue)
                {
                    block.SetColor(colorId, tint.Value);
                    block.SetColor(baseColorId, tint.Value);
                }

                r.SetPropertyBlock(block, i);
            }
        }
    }
}
