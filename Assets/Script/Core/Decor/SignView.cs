using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Модель таблички ([[Decorations]]) по <see cref="SignData"/>: стойка (два столба, столб, кронштейн,
/// наклонная, постамент), доска с рамкой и элементы на лицевой (и, если двусторонняя, обратной) стороне.
/// Собирается кодом заново при каждой правке — в мире (<see cref="Decoration"/>) и в превью [[SignEditorUI]].
/// Грани элементов: <c>FaceFront</c> — локальная X вправо для зрителя, Y вверх, элементы ближе к зрителю по −Z.
/// </summary>
public class SignView : MonoBehaviour
{
    public const string RootName = "SignModel";
    const float BoardDepth = 0.06f;
    const float FaceGap = 0.004f;
    const float LayerStep = 0.0035f;
    const int FontSize = 64;

    struct ElementRef
    {
        public Transform root;
        public SignElement el;
    }

    struct Tinted<T>
    {
        public T target;
        public Color color;
    }

    public Transform Board { get; private set; }
    public Transform FaceFront { get; private set; }
    public Vector2 BoardSize { get; private set; }
    /// <summary>Верх модели над землёй (для коллайдера).</summary>
    public float Top { get; private set; }
    public Renderer Probe { get; private set; }

    readonly List<ElementRef> front = new List<ElementRef>();
    readonly List<Tinted<TextMesh>> texts = new List<Tinted<TextMesh>>();
    readonly List<Tinted<SpriteRenderer>> sprites = new List<Tinted<SpriteRenderer>>();
    Transform swing;
    float phase;
    bool animate;
    float brightness = 1f;

    public int Count => front.Count;

    /// <summary>Точка для ночной подсветки: перед лицевой стороной доски.</summary>
    public Vector3 LightPoint => Board != null ? Board.position + Board.forward * 0.9f + Vector3.up * 0.15f : transform.position;

    // ---------- Сборка ----------

    public static SignView Build(Transform parent, SignData data, int layer, bool animate)
    {
        Transform old = parent.Find(RootName);
        if (old != null)
        {
            old.name = RootName + "_old";
            old.gameObject.SetActive(false);
            Destroy(old.gameObject);
        }

        var go = new GameObject(RootName);
        go.transform.SetParent(parent, false);
        var view = go.AddComponent<SignView>();
        view.animate = animate;
        view.phase = Random.value * 10f;
        view.Construct(data ?? SignPresets.Default());
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
        return view;
    }

    void Construct(SignData data)
    {
        Vector2 size = data.BoardSize;
        float w = size.x;
        float h = size.y;
        BoardSize = size;
        Material post = Mat(data.postColor);
        Material board = Mat(data.board);
        Material frame = Mat(data.frameColor);
        float fw = data.frame == 0 ? 0f : data.frame == 1 ? 0.035f : 0.075f;
        float side = w * 0.5f + fw;

        Transform holder = transform;
        Vector3 boardPos;
        Quaternion boardRot = Quaternion.identity;
        switch (data.Mount)
        {
            case SignMount.Pole:
            {
                const float bottom = 1.45f;
                Box(holder, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.34f, 0.1f, 0.34f), post);
                Box(holder, "Pole", new Vector3(0f, (bottom + 0.06f) * 0.5f, 0f), new Vector3(0.1f, bottom + 0.06f, 0.1f), post);
                Box(holder, "Collar", new Vector3(0f, bottom - fw - 0.04f, 0f), new Vector3(0.16f, 0.08f, 0.16f), post);
                boardPos = new Vector3(0f, bottom + h * 0.5f, 0f);
                Top = bottom + h + fw;
                break;
            }
            case SignMount.Hanging:
            {
                const float postX = -0.88f;
                const float armY = 2.78f;
                float bx = postX + 0.22f + w * 0.5f;
                Box(holder, "Base", new Vector3(postX, 0.05f, 0f), new Vector3(0.3f, 0.1f, 0.3f), post);
                Box(holder, "Post", new Vector3(postX, (armY + 0.12f) * 0.5f, 0f), new Vector3(0.1f, armY + 0.12f, 0.1f), post);
                float armEnd = bx + w * 0.5f + 0.06f;
                Box(holder, "Arm", new Vector3((postX + armEnd) * 0.5f, armY, 0f), new Vector3(armEnd - postX, 0.06f, 0.06f), post);
                Box(holder, "Cap", new Vector3(armEnd, armY, 0f), new Vector3(0.09f, 0.09f, 0.09f), frame);
                var brace = Box(holder, "Brace", new Vector3(postX + 0.2f, armY - 0.2f, 0f), new Vector3(0.04f, 0.56f, 0.04f), post);
                brace.localRotation = Quaternion.Euler(0f, 0f, -45f);

                swing = new GameObject("Swing").transform;
                swing.SetParent(holder, false);
                swing.localPosition = new Vector3(bx, armY, 0f);
                const float chain = 0.24f;
                for (int s = -1; s <= 1; s += 2)
                {
                    float cx = s * (w * 0.5f - 0.1f);
                    Box(swing, "Chain", new Vector3(cx, -chain * 0.5f, 0f), new Vector3(0.018f, chain, 0.018f), post);
                    Box(swing, "Ring", new Vector3(cx, -0.015f, 0f), new Vector3(0.05f, 0.03f, 0.05f), post);
                }

                holder = swing;
                boardPos = new Vector3(0f, -chain - fw - h * 0.5f, 0f);
                Top = armY + 0.12f;
                break;
            }
            case SignMount.Plaque:
            {
                const float tilt = 18f;
                float cos = Mathf.Cos(tilt * Mathf.Deg2Rad);
                float cy = 0.28f + (h * 0.5f + fw) * cos;
                for (int s = -1; s <= 1; s += 2)
                {
                    float lx = s * (side + 0.035f);
                    Box(holder, "Leg", new Vector3(lx, cy * 0.5f + 0.02f, 0f), new Vector3(0.06f, cy + 0.04f, 0.06f), post);
                    Box(holder, "Foot", new Vector3(lx, 0.03f, 0f), new Vector3(0.1f, 0.06f, 0.34f), post);
                }

                boardPos = new Vector3(0f, cy, 0f);
                boardRot = Quaternion.Euler(-tilt, 0f, 0f);
                Top = cy + (h * 0.5f + fw) * cos;
                break;
            }
            case SignMount.Stand:
            {
                const float baseH = 0.36f;
                Box(holder, "Plinth", new Vector3(0f, baseH * 0.5f, 0f), new Vector3(side * 2f + 0.16f, baseH, 0.42f), post);
                Box(holder, "PlinthTop", new Vector3(0f, baseH + 0.02f, 0f), new Vector3(side * 2f + 0.06f, 0.04f, 0.3f), frame);
                boardPos = new Vector3(0f, baseH + 0.04f + fw + h * 0.5f, 0f);
                Top = baseH + 0.04f + h + fw * 2f;
                break;
            }
            default:
            {
                const float bottom = 0.95f;
                float postH = bottom + h + fw + 0.06f;
                for (int s = -1; s <= 1; s += 2)
                {
                    float px = s * (side + 0.045f);
                    Box(holder, "Post", new Vector3(px, postH * 0.5f, 0f), new Vector3(0.08f, postH, 0.08f), post);
                    Box(holder, "Footing", new Vector3(px, 0.04f, 0f), new Vector3(0.18f, 0.08f, 0.18f), post);
                    Box(holder, "Cap", new Vector3(px, postH + 0.015f, 0f), new Vector3(0.11f, 0.03f, 0.11f), frame);
                    Box(holder, "Bolt", new Vector3(s * (side + 0.012f), bottom + h * 0.5f, 0f), new Vector3(0.05f, 0.05f, 0.05f), post);
                }

                boardPos = new Vector3(0f, bottom + h * 0.5f, 0f);
                Top = postH + 0.03f;
                break;
            }
        }

        Board = new GameObject("Board").transform;
        Board.SetParent(holder, false);
        Board.localPosition = boardPos;
        Board.localRotation = boardRot;

        Transform slab = Box(Board, "Slab", Vector3.zero, new Vector3(w, h, BoardDepth), board);
        Probe = slab.GetComponent<Renderer>();
        if (fw > 0f)
        {
            float fd = BoardDepth + (data.frame == 2 ? 0.04f : 0.02f);
            Box(Board, "FrameT", new Vector3(0f, h * 0.5f + fw * 0.5f, 0f), new Vector3(w + fw * 2f, fw, fd), frame);
            Box(Board, "FrameB", new Vector3(0f, -h * 0.5f - fw * 0.5f, 0f), new Vector3(w + fw * 2f, fw, fd), frame);
            Box(Board, "FrameL", new Vector3(-w * 0.5f - fw * 0.5f, 0f, 0f), new Vector3(fw, h, fd), frame);
            Box(Board, "FrameR", new Vector3(w * 0.5f + fw * 0.5f, 0f, 0f), new Vector3(fw, h, fd), frame);
        }

        // TextMesh и квад читаются со стороны −Z своей локали: лицевая грань развёрнута на 180°.
        FaceFront = Face("FaceFront", BoardDepth * 0.5f + FaceGap, 180f);
        FillFace(FaceFront, data, front);
        if (data.twoSided)
            FillFace(Face("FaceBack", -(BoardDepth * 0.5f + FaceGap), 0f), data, null);
    }

    Transform Face(string name, float z, float yaw)
    {
        Transform f = new GameObject(name).transform;
        f.SetParent(Board, false);
        f.localPosition = new Vector3(0f, 0f, z);
        f.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return f;
    }

    void FillFace(Transform face, SignData data, List<ElementRef> refs)
    {
        float w = BoardSize.x;
        float h = BoardSize.y;
        for (int i = 0; i < data.items.Count; i++)
        {
            SignElement el = data.items[i];
            var root = new GameObject("El" + i).transform;
            root.SetParent(face, false);
            root.localPosition = new Vector3(el.x * w, el.y * h, -(i + 1) * LayerStep);
            // плюс — по часовой стрелке для зрителя
            root.localRotation = Quaternion.Euler(0f, 0f, -el.rot);
            switch (el.Kind)
            {
                case SignKind.Icon: MakeIcon(root, el, h); break;
                case SignKind.Plate: MakePlate(root, el, w, h); break;
                default: MakeText(root, el, h); break;
            }

            refs?.Add(new ElementRef { root = root, el = el });
        }
    }

    void MakeText(Transform root, SignElement el, float boardH)
    {
        string value = string.IsNullOrEmpty(el.text) ? " " : el.text;
        float em = el.size * boardH;
        Color color = SignColors.Parse(el.color);
        if (el.shadow)
        {
            var sh = Label(root, "Shadow", value, em, el, new Color(0f, 0f, 0f, 0.6f));
            sh.transform.localPosition = new Vector3(em * 0.06f, -em * 0.06f, LayerStep * 0.4f);
        }

        Label(root, "Text", value, em, el, color);
    }

    TextMesh Label(Transform root, string name, string value, float em, SignElement el, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        TextMesh tm = go.AddComponent<TextMesh>();
        tm.fontSize = FontSize;
        tm.characterSize = em / (FontSize * 0.1f);
        tm.richText = false;
        tm.lineSpacing = 1f;
        switch (el.align)
        {
            case 1: tm.anchor = TextAnchor.MiddleLeft; tm.alignment = TextAlignment.Left; break;
            case 2: tm.anchor = TextAnchor.MiddleRight; tm.alignment = TextAlignment.Right; break;
            default: tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; break;
        }

        FontStyle style = FontStyle.Normal;
        if (el.Kind == SignKind.Text && el.bold && el.italic)
            style = FontStyle.BoldAndItalic;
        else if (el.Kind == SignKind.Text && el.bold)
            style = FontStyle.Bold;
        else if (el.Kind == SignKind.Text && el.italic)
            style = FontStyle.Italic;
        tm.fontStyle = style;
        Font f = DecorCatalog.DecorFont();
        if (f != null)
        {
            tm.font = f;
            MeshRenderer rend = go.GetComponent<MeshRenderer>();
            rend.sharedMaterial = DecorCatalog.TextMaterial(f);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }

        tm.text = value;
        tm.color = color * brightness;
        texts.Add(new Tinted<TextMesh> { target = tm, color = color });
        return tm;
    }

    void MakeIcon(Transform root, SignElement el, float boardH)
    {
        Sprite sprite = IconSprite(el.text);
        if (sprite == null)
        {
            var missing = new SignElement { kind = (int)SignKind.Symbol, text = "?", size = el.size, color = "7A8088" };
            Label(root, "Missing", "?", el.size * boardH, missing, SignColors.Parse("7A8088"));
            return;
        }

        var go = new GameObject("Icon");
        go.transform.SetParent(root, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        sr.receiveShadows = false;
        Bounds b = sprite.bounds;
        float span = Mathf.Max(0.0001f, Mathf.Max(b.size.x, b.size.y));
        float k = el.size * boardH / span;
        go.transform.localScale = new Vector3(k, k, 1f);
        go.transform.localPosition = new Vector3(-b.center.x * k, -b.center.y * k, 0f);
        Color tint = SignColors.Parse(el.color);
        sr.color = tint * brightness;
        sprites.Add(new Tinted<SpriteRenderer> { target = sr, color = tint });
    }

    static void MakePlate(Transform root, SignElement el, float boardW, float boardH)
    {
        var go = new GameObject("Plate");
        go.transform.SetParent(root, false);
        go.transform.localScale = new Vector3(el.w * boardW, el.h * boardH, 1f);
        go.AddComponent<MeshFilter>().sharedMesh = QuadMesh();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = Mat(el.color);
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Иконка по id: <c>item:…</c> — предмет, <c>bld:…</c> — здание или декорация.</summary>
    public static Sprite IconSprite(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        int colon = id.IndexOf(':');
        string kind = colon > 0 ? id.Substring(0, colon) : "item";
        string key = colon > 0 ? id.Substring(colon + 1) : id;
        if (kind == "bld")
        {
            BuildingData b = GameDatabase.FindBuilding(key);
            return b != null ? b.icon : null;
        }

        ItemData item = GameDatabase.FindItem(key);
        return item != null ? item.icon : null;
    }

    // ---------- Живое ----------

    void Update()
    {
        if (swing == null || !animate)
            return;
        Camera cam = Camera.main;
        if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 60f * 60f)
            return;
        float t = Time.time + phase;
        float wind = 0.6f + 0.4f * Mathf.PerlinNoise(t * 0.25f, 3.1f);
        swing.localRotation = Quaternion.Euler(Mathf.Sin(t * 1.4f) * 3.2f * wind, 0f, Mathf.Sin(t * 0.9f + 1f) * 0.8f);
    }

    /// <summary>Ночью надписи без подсветки тускнеют вместе с миром (шрифт рисуется без освещения).</summary>
    public void SetBrightness(float k)
    {
        k = Mathf.Clamp01(k);
        if (Mathf.Abs(k - brightness) < 0.01f)
            return;
        brightness = k;
        for (int i = 0; i < texts.Count; i++)
        {
            if (texts[i].target == null)
                continue;
            Color c = texts[i].color;
            texts[i].target.color = new Color(c.r * k, c.g * k, c.b * k, c.a);
        }

        for (int i = 0; i < sprites.Count; i++)
        {
            if (sprites[i].target == null)
                continue;
            Color c = sprites[i].color;
            sprites[i].target.color = new Color(c.r * k, c.g * k, c.b * k, c.a);
        }
    }

    // ---------- Выбор мышью в редакторе ----------

    /// <summary>Прямоугольник элемента на лицевой стороне в долях доски (центр доски — 0,0).</summary>
    public bool TryGetRect(int index, out Rect rect)
    {
        rect = default;
        if (index < 0 || index >= front.Count || front[index].root == null || FaceFront == null)
            return false;
        bool any = false;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        foreach (Renderer r in front[index].root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name == "Shadow")
                continue;
            Bounds b = r.localBounds;
            if (b.size.sqrMagnitude < 1e-10f)
                continue;
            for (int c = 0; c < 4; c++)
            {
                Vector3 p = b.center + new Vector3((c & 1) == 0 ? -b.extents.x : b.extents.x, (c & 2) == 0 ? -b.extents.y : b.extents.y, 0f);
                Vector3 local = FaceFront.InverseTransformPoint(r.transform.TransformPoint(p));
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
                any = true;
            }
        }

        if (!any)
        {
            // меш шрифта ещё не собран — оценка по размеру (средняя буква ≈ 0.55 em)
            SignElement el = front[index].el;
            Vector3 c0 = front[index].root.localPosition;
            float em = el.size * BoardSize.y;
            float halfW = em * 0.5f;
            float halfH = em * 0.5f;
            if (el.Kind == SignKind.Plate)
            {
                halfW = el.w * BoardSize.x * 0.5f;
                halfH = el.h * BoardSize.y * 0.5f;
            }
            else if (el.Kind == SignKind.Text)
            {
                string[] lines = (el.text ?? "").Split('\n');
                int longest = 1;
                for (int l = 0; l < lines.Length; l++)
                    longest = Mathf.Max(longest, lines[l].Length);
                halfW = longest * em * 0.275f;
                halfH = lines.Length * em * 0.5f;
            }

            float left = el.Kind == SignKind.Text && el.align == 1 ? 0f : el.Kind == SignKind.Text && el.align == 2 ? -2f * halfW : -halfW;
            min = new Vector2(c0.x + left, c0.y - halfH);
            max = new Vector2(c0.x + left + halfW * 2f, c0.y + halfH);
        }

        rect = Rect.MinMaxRect(min.x / BoardSize.x, min.y / BoardSize.y, max.x / BoardSize.x, max.y / BoardSize.y);
        return true;
    }

    /// <summary>Верхний элемент под точкой (доли доски) или −1.</summary>
    public int HitTest(Vector2 boardPoint)
    {
        for (int i = front.Count - 1; i >= 0; i--)
        {
            if (TryGetRect(i, out Rect r) && Grow(r, 0.02f).Contains(boardPoint))
                return i;
        }

        return -1;
    }

    static Rect Grow(Rect r, float pad)
    {
        return Rect.MinMaxRect(r.xMin - pad, r.yMin - pad, r.xMax + pad, r.yMax + pad);
    }

    // ---------- Меши и материалы ----------

    static Mesh cube;
    static Mesh quad;
    static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();

    static Transform Box(Transform parent, string name, Vector3 pos, Vector3 size, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.AddComponent<MeshFilter>().sharedMesh = CubeMesh();
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    static Mesh CubeMesh()
    {
        if (cube == null)
            cube = BuiltinMesh(PrimitiveType.Cube);
        return cube;
    }

    static Mesh QuadMesh()
    {
        if (quad == null)
            quad = BuiltinMesh(PrimitiveType.Quad);
        return quad;
    }

    static Mesh BuiltinMesh(PrimitiveType type)
    {
        GameObject tmp = GameObject.CreatePrimitive(type);
        Mesh m = tmp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(tmp);
        return m;
    }

    /// <summary>Освещаемый материал цвета «RRGGBB» (копия wi_paint, но не перекрашивается по E).</summary>
    public static Material Mat(string hex)
    {
        string key = SignColors.Clean(hex, "FFFFFF");
        if (mats.TryGetValue(key, out Material m) && m != null)
            return m;
        Color c = SignColors.Parse(key);
        Material baseMat = ModelLibrary.FindMaterial(Decoration.PaintMaterial);
        m = baseMat != null ? new Material(baseMat) : RuntimeMaterials.Create(c);
        m.name = "sign_" + key;
        m.color = c;
        mats[key] = m;
        return m;
    }
}
