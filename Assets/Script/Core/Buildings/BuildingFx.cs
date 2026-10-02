using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Анимации и частицы здания. Animator (контроллер из `Assets/Art/Animations/Buildings`) получает
/// bool <c>Working</c>, float <c>Speed</c> и trigger <c>Transfer</c> (рука). Частицы — дети `FX`:
/// `FX_*` крутятся, пока здание работает; `FX_Burst*`/`FX_Chunks` выстреливают на готовый крафт/добычу.
/// Всё гаснет за радиусом прогрузки и по настройке «Эффекты зданий». Собирает префабы BuildingFxBuilder (Editor).
/// </summary>
[DisallowMultipleComponent]
public class BuildingFx : MonoBehaviour
{
    public const string FxRoot = "FX";

    static readonly int WorkingId = Animator.StringToHash("Working");
    static readonly int SpeedId = Animator.StringToHash("Speed");
    static readonly int TransferId = Animator.StringToHash("Transfer");
    const float CheckInterval = 0.25f;

    // Один тикер на все здания: проверки размазаны по кадрам (раньше все здания,
    // загруженные в одном кадре, проверялись синхронно — пик раз в 0.25 с).
    static readonly List<BuildingFx> all = new List<BuildingFx>(256);
    // Новые (только что поставленные/включённые) — первая проверка в ближайшем кадре, без очереди.
    static readonly List<BuildingFx> fresh = new List<BuildingFx>(16);
    static int cursor;
    static float sliceDebt;
    static int qualityFrame = -1;
    static int quality;

    BuildingBase building;
    Animator anim;
    readonly List<ParticleSystem> loops = new List<ParticleSystem>();
    readonly List<float> loopRates = new List<float>();
    readonly List<ParticleSystem> bursts = new List<ParticleSystem>();
    int index = -1;
    bool lastWork;
    int lastQuality = -1;
    float nextBurst;

    void Awake()
    {
        building = GetComponent<BuildingBase>();
        anim = GetComponent<Animator>();
        Transform fx = transform.Find(FxRoot);
        if (fx == null)
            return;
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>(true))
        {
            string n = ps.gameObject.name;
            if (n.StartsWith("FX_Burst") || n.StartsWith("FX_Chunks"))
            {
                bursts.Add(ps);
                continue;
            }

            loops.Add(ps);
            loopRates.Add(ps.emission.rateOverTimeMultiplier);
            SetEmission(ps, false, 0f);
        }
    }

    void Start()
    {
        // Визуал станций дронов создаётся в Awake самого здания — привязки пересобрать.
        if (anim != null)
            anim.Rebind();
    }

    void OnEnable()
    {
        if (index >= 0)
            return;
        index = all.Count;
        all.Add(this);
        fresh.Add(this);
        Ticker.Ensure();
    }

    void OnDisable()
    {
        if (index < 0)
            return;
        int last = all.Count - 1;
        BuildingFx moved = all[last];
        all[index] = moved;
        moved.index = index;
        all.RemoveAt(last);
        index = -1;
    }

    /// <summary>Настройка «Эффекты зданий»: PlayerPrefs (реестр) читаем раз в кадр, а не на каждом здании.</summary>
    static int Quality
    {
        get
        {
            if (qualityFrame != Time.frameCount)
            {
                qualityFrame = Time.frameCount;
                quality = GameSettings.BuildingFxQuality;
            }
            return quality;
        }
    }

    static void TickAll()
    {
        if (fresh.Count > 0)
        {
            int fq = Quality;
            for (int i = 0; i < fresh.Count; i++)
            {
                BuildingFx fx = fresh[i];
                if (fx != null && fx.index >= 0)
                    fx.Check(fq);
            }
            fresh.Clear();
        }

        int count = all.Count;
        if (count == 0)
            return;
        // Каждое здание — раз в CheckInterval, за кадр — соответствующая доля списка.
        sliceDebt += count * Time.unscaledDeltaTime / CheckInterval;
        int budget = Mathf.Min(count, (int)sliceDebt);
        sliceDebt -= budget;
        if (sliceDebt > count)
            sliceDebt = count;
        int q = Quality;
        for (int i = 0; i < budget; i++)
        {
            if (cursor >= all.Count)
                cursor = 0;
            if (all.Count == 0)
                return;
            BuildingFx fx = all[cursor++];
            if (fx != null)
                fx.Check(q);
        }
    }

    void Check(int quality)
    {
        bool placed = building != null && building.IsPlaced;
        bool near = placed && quality > 0 && WorldView.InRange(transform.position);
        bool work = near && IsWorking();

        if (anim != null)
        {
            anim.enabled = near;
            if (near)
            {
                anim.SetBool(WorkingId, work);
                anim.SetFloat(SpeedId, SpeedMul());
            }
        }

        if (work == lastWork && quality == lastQuality)
            return;
        lastWork = work;
        lastQuality = quality;
        float k = quality == 1 ? 0.45f : 1f;
        for (int i = 0; i < loops.Count; i++)
            SetEmission(loops[i], work, loopRates[i] * k);
    }

    static void SetEmission(ParticleSystem ps, bool on, float rate)
    {
        if (ps == null)
            return;
        var em = ps.emission;
        em.enabled = on;
        if (on)
        {
            em.rateOverTimeMultiplier = rate;
            if (!ps.isPlaying)
                ps.Play();
        }
    }

    float SpeedMul()
    {
        float mul = 1f;
        if (building == null)
            return mul;
        if (building.ReadLevel() >= 2)
            mul *= 1.5f;
        if (building.BreakMode == 2)
            mul *= 0.25f;
        if (building is PowerGenerator || building is CrafterBuilding || building is Extractor)
            mul *= Mathf.Clamp(PowerGenerator.GetNearbySpeedMultiplier(transform.position), 1f, 2f);
        return mul;
    }

    bool IsWorking()
    {
        BuildingBase b = building;
        if (b.BreakMode == 1)
            return false;
        switch (b)
        {
            case CrafterBuilding c:
                return c.currentRecipe != null && c.IdleReason() == null;
            case Extractor e:
                return e.resource != null && !e.IsOutputJammed;
            case OilExtractor _:
            case WaterExtractor _:
                return b.HasOutputSpace(1);
            case PowerGenerator g:
                return g.Powered;
            case ResearchLab _:
                return true;
            case Splitter s:
                return s.CargoCount > 0;
            case PipeSplitter p:
                return p.Buffered > 0;
            case DroneLoadStation ls:
                return ls.FlyingCount() > 0;
            case DroneUnloadStation us:
                return us.IncomingDrones() > 0;
        }

        return false;
    }

    // ---------- Выбросы ----------

    /// <summary>Готовый крафт / добыча: вспышка искр и кусочков в цвет предмета.</summary>
    public static void Burst(BuildingBase b, ItemData item, int count = 10)
    {
        if (b == null)
            return;
        BuildingFx fx = b.GetComponent<BuildingFx>();
        if (fx != null)
            fx.DoBurst(item, count);
    }

    /// <summary>Разовое действие для Animator (рука: взмах с предметом).</summary>
    public static void Trigger(BuildingBase b)
    {
        if (b == null)
            return;
        BuildingFx fx = b.GetComponent<BuildingFx>();
        if (fx != null && fx.anim != null && fx.anim.isActiveAndEnabled)
            fx.anim.SetTrigger(TransferId);
    }

    void DoBurst(ItemData item, int count)
    {
        if (bursts.Count == 0 || Time.time < nextBurst)
            return;
        int quality = Quality;
        if (quality <= 0 || !WorldView.InRange(transform.position))
            return;
        nextBurst = Time.time + 0.15f;
        Color tint = item != null ? Conveyor.FilterTint(item) : Color.white;
        int n = quality == 1 ? Mathf.Max(1, count / 2) : count;
        for (int i = 0; i < bursts.Count; i++)
        {
            ParticleSystem ps = bursts[i];
            var p = new ParticleSystem.EmitParams { applyShapeToPosition = true };
            if (ps.gameObject.name.StartsWith("FX_Chunks"))
                p.startColor = tint;
            else if (!ps.gameObject.name.StartsWith("FX_BurstBlue"))
                p.startColor = Color.Lerp(new Color(1f, 0.75f, 0.35f), tint, 0.5f);
            ps.Emit(p, n);
        }
    }

    /// <summary>Скрытый объект, который раз в кадр двигает общий тикер.</summary>
    sealed class Ticker : MonoBehaviour
    {
        static Ticker instance;

        public static void Ensure()
        {
            if (instance != null || !Application.isPlaying)
                return;
            var go = new GameObject("BuildingFxTicker") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<Ticker>();
        }

        void Update()
        {
            TickAll();
        }
    }
}
