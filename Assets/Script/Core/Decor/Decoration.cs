using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Поставленная декорация ([[Decorations]]): здание без сокетов. Анимирует части модели
/// (лопасти, стрела крана, флаг, стрелки часов…), держит огни (включает [[DecorSystem]] ночью),
/// пишет текст на вывеске/табло, перекрашивается по E (материал <c>wi_paint</c>).
/// Табличка (<see cref="DecorCatalog.SignId"/>) хранит своё содержимое (<see cref="SignData"/>), по E открывает [[SignEditorUI]].
/// Напольные (плитка, асфальт) не занимают сетку зданий — сверху можно строить.
/// </summary>
public class Decoration : BuildingBase, IInteractable
{
    public const string PaintMaterial = "wi_paint";

    DecorCatalog.Def def;
    int tint;
    bool rigged;
    readonly Dictionary<string, Transform> parts = new Dictionary<string, Transform>();
    readonly Dictionary<Transform, Quaternion> baseRot = new Dictionary<Transform, Quaternion>();
    readonly Dictionary<Transform, Vector3> baseScale = new Dictionary<Transform, Vector3>();
    readonly Dictionary<Transform, Vector3> basePos = new Dictionary<Transform, Vector3>();
    readonly List<Light> lights = new List<Light>();
    readonly List<Renderer> paintRenderers = new List<Renderer>();
    Renderer[] blinkA;
    Renderer[] blinkB;
    Renderer cullProbe;
    TextMesh text;
    TextMesh textBack;
    float textWidth;
    float nextText;
    float phase;
    float barrier;
    bool lightsWanted;
    SignData sign;
    SignView signView;
    float nextSignTick;
    static readonly List<Light> NoLights = new List<Light>();

    public DecorCatalog.Def Def
    {
        get
        {
            if (def == null && data != null)
                def = DecorCatalog.Find(data.id);
            return def;
        }
    }

    public bool IsFloor => Def != null && Def.IsFloor;
    public bool IsSign => data != null && data.id == DecorCatalog.SignId;
    public int Tint => tint;
    /// <summary>Табличка без подсветки не занимает ночные огни [[DecorSystem]].</summary>
    public IReadOnlyList<Light> Lights => IsSign && (sign == null || !sign.glow) ? NoLights : lights;
    /// <summary>Модель не скрыта отсечением дальности.</summary>
    public bool IsShown => cullProbe == null || cullProbe.enabled;
    public bool CanInteract
    {
        get
        {
            Rig();
            return IsPlaced && (IsSign || paintRenderers.Count > 0 || Zipline.IsPost(this));
        }
    }

    public string InteractHint
    {
        get
        {
            if (Zipline.IsPost(this))
                return UiLocale.T("zip.hint");
            if (IsSign)
                return UiLocale.T("sign.hint");
            int cost = DecorSystem.TintCost;
            return cost > 0
                ? DecorText.T("decor.hint_tint", IndustryUi.Money(cost))
                : DecorText.T("decor.hint_tint_free");
        }
    }

    void Awake()
    {
        phase = Random.value * 10f;
        Rig();
    }

    // ---------- Размещение ----------

    public override void OnPlaced()
    {
        base.OnPlaced();
        Rig();
        ApplyTint();
        BuildFx();
        RefreshText(true);
        if (IsSign)
        {
            if (sign == null)
                sign = SignPresets.Default();
            RebuildSign();
        }
        DecorSystem.Register(this);
        if (Zipline.IsPost(this))
            Zipline.EnsureCables();
    }

    public override void OnRemoved()
    {
        DecorSystem.Unregister(this);
        base.OnRemoved();
    }

    protected override void OnDestroy()
    {
        DecorSystem.Unregister(this);
        base.OnDestroy();
    }

    protected override void RegisterOnGrid()
    {
        if (IsFloor)
        {
            DecorSystem.RegisterFloor(this);
            return;
        }

        base.RegisterOnGrid();
    }

    // ---------- Сохранение ----------

    public override void WriteSave(BuildingSaveData save)
    {
        base.WriteSave(save);
        if (save == null)
            return;
        if (tint != 0)
            Extra(save, "tint", tint.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (IsSign && sign != null)
            Extra(save, "sign", sign.ToJson());
    }

    static void Extra(BuildingSaveData save, string key, string value)
    {
        if (save.extras == null)
            save.extras = new List<SaveKeyValue>();
        save.extras.Add(new SaveKeyValue { key = key, value = value });
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        tint = 0;
        if (save != null && save.extras != null)
        {
            for (int i = 0; i < save.extras.Count; i++)
            {
                SaveKeyValue row = save.extras[i];
                if (row != null && row.key == "tint")
                    int.TryParse(row.value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out tint);
                else if (row != null && row.key == "sign" && IsSign)
                    sign = SignData.FromJson(row.value) ?? sign;
            }
        }

        ApplyTint();
        if (IsSign && IsPlaced)
            RebuildSign();
    }

    /// <summary>Состояние для копирования/чертежа: содержимое таблички или цвет краски.</summary>
    public string CopyState()
    {
        if (IsSign && sign != null)
            return "sign:" + sign.ToJson();
        return tint != 0 ? "tint:" + tint.ToString(System.Globalization.CultureInfo.InvariantCulture) : "";
    }

    public void PasteState(string state)
    {
        if (string.IsNullOrEmpty(state))
            return;
        if (state.StartsWith("sign:", System.StringComparison.Ordinal))
        {
            if (IsSign)
                SetSign(SignData.FromJson(state.Substring(5)));
        }
        else if (state.StartsWith("tint:", System.StringComparison.Ordinal)
            && int.TryParse(state.Substring(5), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int t))
            SetTint(t);
    }

    // ---------- Перекраска (E) ----------

    public void Interact(GameObject interactor)
    {
        if (!CanInteract)
            return;
        if (Zipline.IsPost(this))
        {
            Zipline.TryRide(this, interactor);
            return;
        }

        if (IsSign)
        {
            SignEditorUI.OpenFor(this);
            return;
        }

        int cost = DecorSystem.TintCost;
        if (cost > 0 && PlayerWallet.Instance != null && !PlayerWallet.Instance.TrySpendCoins(cost, MoneySource.Decor))
        {
            UiAudio.PlayError();
            UiNotification.Push(NotifyKind.Resources, DecorText.T("decor.no_money_tint"),
                UiLocale.T("wallet.coins_n", IndustryUi.Money(cost)), UiStatus.Warning);
            return;
        }

        tint = (tint + 1) % DecorSystem.PaintCount;
        ApplyTint();
        UiAudio.PlayToggle();
    }

    public void SetTint(int value)
    {
        tint = Mathf.Clamp(value, 0, DecorSystem.PaintCount - 1);
        ApplyTint();
    }

    void ApplyTint()
    {
        Rig();
        Material paint = DecorSystem.Paint(tint);
        for (int i = 0; i < paintRenderers.Count; i++)
        {
            Renderer r = paintRenderers[i];
            if (r == null)
                continue;
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] != null && IsPaint(mats[m]))
                {
                    mats[m] = paint;
                    changed = true;
                }
            }

            if (changed)
                r.sharedMaterials = mats;
        }
    }

    static bool IsPaint(Material m)
    {
        return m.name.StartsWith(PaintMaterial, System.StringComparison.Ordinal);
    }

    // ---------- Табличка ----------

    /// <summary>Копия содержимого таблички (правки — через <see cref="SetSign"/>).</summary>
    public SignData Sign => sign != null ? sign.Clone() : SignPresets.Default();

    public void SetSign(SignData value)
    {
        if (!IsSign)
            return;
        sign = value != null ? value.Clone() : SignPresets.Default();
        if (IsPlaced)
            RebuildSign();
    }

    void RebuildSign()
    {
        Transform visual = transform.Find(BuildingRestyle.VisualName);
        if (visual == null || sign == null)
            return;
        signView = SignView.Build(visual, sign, gameObject.layer, true);
        cullProbe = signView.Probe;
        signView.SetBrightness(SignBrightness());
        nextSignTick = Time.unscaledTime + 0.5f;

        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            float top = Mathf.Max(0.5f, signView.Top);
            box.size = new Vector3(box.size.x, top, box.size.z);
            box.center = new Vector3(box.center.x, top * 0.5f, box.center.z);
        }

        if (lights.Count > 0 && lights[0] != null)
            lights[0].transform.position = signView.LightPoint;
        if (!sign.glow)
            SetLightsOn(false);
        RecaptureCullRenderers();
    }

    void SignTick()
    {
        if (signView == null || Time.unscaledTime < nextSignTick)
            return;
        nextSignTick = Time.unscaledTime + 0.5f;
        signView.SetBrightness(SignBrightness());
    }

    /// <summary>Шрифт без освещения: ночью надпись тускнеет вместе с миром, с подсветкой — нет.</summary>
    float SignBrightness()
    {
        if ((sign != null && sign.glow) || !GameSettings.DayNightEnabled)
            return 1f;
        return Mathf.Lerp(0.42f, 1f, DayNight.Evaluate(DayNight.Hour).dayFactor);
    }

    // ---------- Сборка ----------

    void Rig()
    {
        if (rigged)
            return;
        rigged = true;
        Transform visual = transform.Find(BuildingRestyle.VisualName);
        if (visual != null)
        {
            for (int i = 0; i < visual.childCount; i++)
            {
                Transform c = visual.GetChild(i);
                if (!c.name.StartsWith(DecorCatalog.PartPrefix))
                    continue;
                string key = c.name.Substring(DecorCatalog.PartPrefix.Length);
                parts[key] = c;
                baseRot[c] = c.localRotation;
                baseScale[c] = c.localScale;
                basePos[c] = c.localPosition;
            }

            Transform t = visual.Find(DecorCatalog.TextName);
            text = t != null ? t.GetComponent<TextMesh>() : null;
            Transform tb = visual.Find(DecorCatalog.TextBackName);
            textBack = tb != null ? tb.GetComponent<TextMesh>() : null;
            Transform body = visual.Find("WiModel");
            cullProbe = body != null ? body.GetComponentInChildren<Renderer>(true) : null;
        }

        foreach (Light l in GetComponentsInChildren<Light>(true))
            lights.Add(l);

        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer)
                continue;
            Material[] mats = r.sharedMaterials;
            for (int m = 0; m < mats.Length; m++)
            {
                if (mats[m] != null && IsPaint(mats[m]))
                {
                    paintRenderers.Add(r);
                    break;
                }
            }
        }

        blinkA = PartRenderers("BulbsA", "LightsA");
        blinkB = PartRenderers("BulbsB", "LightsB");
        if (blinkA.Length == 0)
        {
            // мигалка радиовышки/крана: отдельная часть Light или объект Lamp в модели
            blinkA = PartRenderers("Light");
            if (blinkA.Length == 0 && Def != null && Def.model != null && Def.model.blink > 0f)
                blinkA = NamedRenderers("Lamp");
        }
    }

    Renderer[] PartRenderers(params string[] names)
    {
        var list = new List<Renderer>();
        for (int i = 0; i < names.Length; i++)
        {
            if (parts.TryGetValue(names[i], out Transform t) && t != null)
                list.AddRange(t.GetComponentsInChildren<Renderer>(true));
        }

        return list.ToArray();
    }

    Renderer[] NamedRenderers(string objectName)
    {
        var list = new List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject.name == objectName && !(r is ParticleSystemRenderer))
                list.Add(r);
        }

        return list.ToArray();
    }

    void BuildFx()
    {
        DecorCatalog.ManifestItem m = Def != null ? Def.model : null;
        if (m == null || transform.Find("DecorFx") != null)
            return;
        var fx = new GameObject("DecorFx");
        fx.transform.SetParent(transform, false);
        fx.layer = gameObject.layer;
        if (m.fire != null && m.fire.Length >= 3)
            DecorFx.Sparks(fx.transform, DecorCatalog.Vec(m.fire));
        if (m.smoke != null && m.smoke.Length >= 3)
            DecorFx.Smoke(fx.transform, DecorCatalog.Vec(m.smoke));
        if (m.splash != null && m.splash.Length >= 3)
            DecorFx.Splash(fx.transform, DecorCatalog.Vec(m.splash));
    }

    // ---------- Анимация ----------

    void Update()
    {
        if (!IsPlaced || Def == null)
            return;
        // Здание скрыто отсечением дальности ([[WorldView]]) — части не трогаем, иначе мигалка «висит» в воздухе.
        if (cullProbe != null && !cullProbe.enabled)
            return;
        Camera cam = Camera.main;
        if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > 70f * 70f)
            return;

        if (IsSign)
        {
            SignTick();
            return;
        }

        float t = Time.time + phase;
        float dt = Time.deltaTime;
        switch (Def.id)
        {
            case "decor_windmill": Spin("Blades", Vector3.forward, 70f, dt); break;
            case "decor_tower_crane": SpinYawing("Jib", t); break;
            case "decor_beacon": Spin("Head", Vector3.up, 260f, dt); break;
            case "decor_gear_monument": Spin("Gear", Vector3.forward, 14f, dt); break;
            case "decor_hologram":
                Spin("Holo", Vector3.up, 40f, dt);
                Bob("Holo", t, 0.06f, 1.2f);
                break;
            case "decor_big_tree": Sway("Crown", t, 1.6f, 0.7f); break;
            case "decor_pond": Sway("Reeds", t, 4f, 1.3f); break;
            case "decor_fire_barrel": Flicker("Flame", t); break;
            case "decor_bbq": Flicker("Coals", t); break;
            case "decor_fountain": Pulse("Jet", t); break;
            case "decor_robot": Wave("Arm", t); break;
            case "decor_flagpole": Flag("Flag", t); break;
            case "decor_clock": Clock(); break;
            case "decor_barrier_gate": Barrier(cam, dt); break;
        }

        Blink(t);
        FlickerLights(t);
        RefreshText(false);
    }

    Transform Part(string name)
    {
        parts.TryGetValue(name, out Transform p);
        return p;
    }

    void Spin(string name, Vector3 axis, float degPerSec, float dt)
    {
        Transform p = Part(name);
        if (p != null)
            p.localRotation *= Quaternion.AngleAxis(degPerSec * dt, axis);
    }

    void SpinYawing(string name, float t)
    {
        // стрела крана: поворот туда-обратно с паузами
        Transform p = Part(name);
        if (p == null)
            return;
        float yaw = Mathf.Sin(t * 0.12f) * 110f;
        p.localRotation = baseRot[p] * Quaternion.Euler(0f, yaw, 0f);
    }

    void Bob(string name, float t, float amp, float speed)
    {
        Transform p = Part(name);
        if (p != null)
            p.localPosition = basePos[p] + Vector3.up * (Mathf.Sin(t * speed) * amp);
    }

    void Sway(string name, float t, float deg, float speed)
    {
        Transform p = Part(name);
        if (p == null)
            return;
        float x = Mathf.Sin(t * speed) * deg;
        float z = Mathf.Sin(t * speed * 0.73f + 1.3f) * deg * 0.6f;
        p.localRotation = baseRot[p] * Quaternion.Euler(x, 0f, z);
    }

    void Flicker(string name, float t)
    {
        Transform p = Part(name);
        if (p == null)
            return;
        float n = Mathf.PerlinNoise(t * 4f, 0.3f);
        float k = 0.8f + n * 0.45f;
        Vector3 s = baseScale[p];
        p.localScale = new Vector3(s.x * (0.95f + n * 0.1f), s.y * k, s.z * (0.95f + n * 0.1f));
    }

    void Pulse(string name, float t)
    {
        Transform p = Part(name);
        if (p == null)
            return;
        float k = 1f + Mathf.Sin(t * 3.1f) * 0.08f + Mathf.Sin(t * 7.3f) * 0.03f;
        Vector3 s = baseScale[p];
        p.localScale = new Vector3(s.x, s.y * k, s.z);
    }

    void Wave(string name, float t)
    {
        Transform p = Part(name);
        if (p == null)
            return;
        // машет сериями: 3 с машет, 2 с отдыхает
        float cycle = Mathf.Repeat(t, 5f);
        float amp = cycle < 3f ? Mathf.Sin(cycle / 3f * Mathf.PI) : 0f;
        float a = Mathf.Sin(t * 9f) * 22f * amp;
        p.localRotation = baseRot[p] * Quaternion.Euler(0f, 0f, a);
    }

    void Flag(string name, float t)
    {
        Transform p = Part(name);
        if (p == null)
            return;
        float wind = 0.6f + 0.4f * Mathf.PerlinNoise(t * 0.3f, 2f);
        float yaw = Mathf.Sin(t * 2.4f) * 9f * wind + Mathf.Sin(t * 5.1f) * 3f;
        float roll = Mathf.Sin(t * 1.7f) * 2f;
        p.localRotation = baseRot[p] * Quaternion.Euler(0f, yaw, roll);
    }

    void Clock()
    {
        float hour = DayNight.Hour;
        float h12 = Mathf.Repeat(hour, 12f);
        float minutes = (hour - Mathf.Floor(hour)) * 60f;
        float hourDeg = h12 / 12f * 360f;
        float minDeg = minutes / 60f * 360f;
        // лицевая сторона (+Z): по часовой = отрицательный угол вокруг +Z; обратная — зеркально
        SetZ("HourF", -hourDeg);
        SetZ("MinF", -minDeg);
        SetZ("HourB", hourDeg);
        SetZ("MinB", minDeg);
    }

    void SetZ(string name, float deg)
    {
        Transform p = Part(name);
        if (p != null)
            p.localRotation = baseRot[p] * Quaternion.Euler(0f, 0f, deg);
    }

    void Barrier(Camera cam, float dt)
    {
        Transform p = Part("Arm");
        if (p == null)
            return;
        bool near = cam != null && (cam.transform.position - transform.position).sqrMagnitude < 5f * 5f;
        barrier = Mathf.MoveTowards(barrier, near ? 1f : 0f, dt * 1.4f);
        float e = barrier * barrier * (3f - 2f * barrier);
        p.localRotation = baseRot[p] * Quaternion.Euler(0f, 0f, 80f * e);
    }

    void Blink(float t)
    {
        float period = Def.model != null ? Def.model.blink : 0f;
        if (period <= 0f)
            return;
        bool on = Mathf.Repeat(t, period * 2f) < period;
        SetRenderers(blinkA, on);
        if (blinkB != null && blinkB.Length > 0)
            SetRenderers(blinkB, !on);
        if (lightsWanted && Def.id != "decor_garland" && Def.id != "decor_xmas_tree")
        {
            for (int i = 0; i < lights.Count; i++)
            {
                if (lights[i] != null && lights[i].enabled)
                    lights[i].intensity = on ? 1.8f : 0.25f;
            }
        }
    }

    static void SetRenderers(Renderer[] list, bool on)
    {
        if (list == null)
            return;
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && list[i].enabled != on)
                list[i].enabled = on;
        }
    }

    void FlickerLights(float t)
    {
        if (!lightsWanted)
            return;
        string id = Def.id;
        if (id != "decor_fire_barrel" && id != "decor_bbq")
            return;
        float k = 1.2f + Mathf.PerlinNoise(t * 5f, 1.7f) * 0.9f;
        for (int i = 0; i < lights.Count; i++)
        {
            if (lights[i] != null)
                lights[i].intensity = k;
        }
    }

    /// <summary>[[DecorSystem]] включает свет ночью у ближних декораций.</summary>
    public void SetLightsOn(bool on)
    {
        if (IsSign && (sign == null || !sign.glow))
            on = false;
        lightsWanted = on;
        for (int i = 0; i < lights.Count; i++)
        {
            Light l = lights[i];
            if (l == null)
                continue;
            if (l.enabled != on)
                l.enabled = on;
            if (on)
                l.intensity = 1.6f;
        }
    }

    // ---------- Текст ----------

    void RefreshText(bool force)
    {
        if (text == null || Def == null || Def.model == null || Def.model.text == null)
            return;
        if (!force && Time.unscaledTime < nextText)
            return;
        nextText = Time.unscaledTime + 2f;
        string kind = Def.model.text.kind;
        string value;
        if (kind == "stats")
            value = StatsText();
        else
        {
            string name = WorldCatalog.Active != null ? WorldCatalog.Active.name : null;
            value = string.IsNullOrEmpty(name) ? DecorText.T("decor.default_name") : name.ToUpperInvariant();
        }

        if (text.text == value)
            return;
        text.text = value;
        FitText();
        if (textBack != null)
        {
            textBack.text = value;
            textBack.characterSize = text.characterSize;
        }
    }

    void FitText()
    {
        if (textWidth <= 0f)
        {
            float w = Def.size.x * GridFootprint.CellSize;
            textWidth = Mathf.Max(0.4f, w * 0.75f);
        }

        text.characterSize = Def.model.text.size;
        // ширина строки по оценке (средняя буква ≈ 0.55 em, em = characterSize·fontSize·0.1)
        int longest = 1;
        foreach (string line in text.text.Split('\n'))
            longest = Mathf.Max(longest, line.Length);
        float got = longest * text.characterSize * text.fontSize * 0.055f;
        if (got > textWidth)
            text.characterSize *= textWidth / got;
    }

    static string StatsText()
    {
        ProductionStats stats = ProductionStats.Instance;
        ResearchSystem rs = ResearchSystem.Instance;
        int produced = stats != null ? stats.TotalProducedCount() : 0;
        int coins = stats != null ? stats.CoinsGainedTotal : 0;
        int done = rs != null ? rs.CompletedResearchCount : 0;
        int total = GameDatabase.AllResearches().Length;
        return DecorText.T("decor.stats_title") + "\n"
            + DecorText.T("decor.stats_items", IndustryUi.Money(produced)) + "\n"
            + DecorText.T("decor.stats_coins", IndustryUi.Money(coins)) + "\n"
            + DecorText.T("decor.stats_research", done, total) + "\n"
            + DecorText.T("decor.stats_day", DayNight.Day);
    }
}
