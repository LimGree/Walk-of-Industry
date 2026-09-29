using UnityEngine;

/// <summary>
/// Один дрон станции загрузки. Не здание: живёт в мире сам, его держит [[DroneLoadStation]].
/// Цикл: стоит на площадке → взлёт → по прямой на высоте → посадка на станцию выгрузки
/// (ждёт в воздухе, если площадок или места нет) → выгрузка → назад → посадка домой.
/// </summary>
public class Drone : MonoBehaviour
{
    public enum Phase { Parked, Climb, Cruise, Hover, Descend, Unload, ClimbBack, Return, Land }

    public DroneLoadStation Home { get; private set; }
    public int HomePad { get; private set; }
    public Phase State { get; private set; }
    public ItemData CargoItem { get; private set; }
    public int CargoCount { get; private set; }
    public DroneUnloadStation Target { get; private set; }
    public bool IsBusy => State != Phase.Parked;

    int targetPad = -1;
    float timer;
    Transform visual;
    Transform crate;
    Transform[] props;
    float propSpin;

    static readonly Vector3[] PropPivots =
    {
        new Vector3(-0.56f, 0.565f, 0.56f), new Vector3(0.56f, 0.565f, 0.56f),
        new Vector3(-0.56f, 0.565f, -0.56f), new Vector3(0.56f, 0.565f, -0.56f)
    };

    public static Drone Spawn(DroneLoadStation home, int pad)
    {
        var go = new GameObject("Drone");
        Drone d = go.AddComponent<Drone>();
        d.Home = home;
        d.HomePad = pad;
        d.visual = DroneModels.Attach(go.transform, DroneModels.Drone, 1.55f);
        d.crate = d.visual != null ? DroneModels.FindChild(d.visual, "Crate") : null;
        d.props = new Transform[4];
        for (int i = 0; i < 4 && d.visual != null; i++)
        {
            // Винт — отдельная модель с центром на оси мотора, иначе вращение уходит в сторону.
            GameObject prop = ModelLibrary.Spawn("drone_prop", 0f);
            if (prop == null)
            {
                d.props[i] = DroneModels.FindChild(d.visual, "Prop_" + i);
                continue;
            }

            prop.name = "Prop_" + i;
            prop.transform.SetParent(d.visual, false);
            prop.transform.localPosition = PropPivots[i];
            prop.transform.localRotation = Quaternion.Euler(0f, i * 37f, 0f);
            d.props[i] = prop.transform;
        }
        d.SnapHome();
        return d;
    }

    void OnEnable()
    {
        if (!DroneNetwork.AllDrones.Contains(this))
            DroneNetwork.AllDrones.Add(this);
    }

    void OnDisable()
    {
        DroneNetwork.AllDrones.Remove(this);
        GameAudio.Loop(this, "bld_drone_loop", false);
    }

    public void SnapHome()
    {
        State = Phase.Parked;
        if (Home == null)
            return;
        transform.position = Home.PadWorld(HomePad);
        transform.rotation = Home.transform.rotation;
        ShowCrate(false);
    }

    /// <summary>Взять ящик и лететь. false — не готов.</summary>
    public bool Launch(ItemData item, int count, DroneUnloadStation target)
    {
        if (State != Phase.Parked || item == null || count <= 0 || target == null)
            return false;
        CargoItem = item;
        CargoCount = count;
        Target = target;
        State = Phase.Climb;
        ShowCrate(true);
        return true;
    }

    /// <summary>Груз, который не долетел (сейв, снос станции) — вернуть домой.</summary>
    public void TakeCargoBack(out ItemData item, out int count)
    {
        item = CargoItem;
        count = CargoCount;
        CargoItem = null;
        CargoCount = 0;
    }

    void ShowCrate(bool on)
    {
        if (crate != null)
            crate.gameObject.SetActive(on);
    }

    float SpeedMul()
    {
        float mul = 1f;
        if (Home != null && Home.BreakMode == 2)
            mul *= 0.25f;
        return mul;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (Home == null)
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        bool flying = State != Phase.Parked;
        SpinProps(flying, dt);
        GameAudio.Loop(this, "bld_drone_loop", flying && WorldView.InRange(transform.position));
        if (!flying)
        {
            // Станцию перенесли или повернули — дрон остаётся на своей площадке.
            if ((transform.position - Home.PadWorld(HomePad)).sqrMagnitude > 0.0001f)
                SnapHome();
            return;
        }

        float cruiseY = Home.transform.position.y + DroneNetwork.CruiseHeight;
        float speed = DroneNetwork.Speed() * SpeedMul();
        float climb = DroneNetwork.ClimbSpeed * SpeedMul();

        switch (State)
        {
            case Phase.Climb:
                if (!TargetAlive())
                {
                    State = Phase.ClimbBack;
                    break;
                }
                if (Rise(cruiseY, climb, dt))
                    State = Phase.Cruise;
                break;

            case Phase.Cruise:
                if (!TargetAlive())
                {
                    State = Phase.Return;
                    break;
                }
                if (FlyTo(Above(Target.transform.position, cruiseY), speed, dt))
                    State = Phase.Hover;
                break;

            case Phase.Hover:
                if (!TargetAlive())
                {
                    State = Phase.Return;
                    break;
                }
                timer -= dt;
                if (timer > 0f)
                    break;
                timer = 0.5f;
                if (Target.CanLand(CargoCount))
                {
                    targetPad = Target.ReservePad(this);
                    if (targetPad >= 0)
                        State = Phase.Descend;
                }
                break;

            case Phase.Descend:
                if (!TargetAlive())
                {
                    State = Phase.ClimbBack;
                    break;
                }
                Vector3 pad = Target.PadWorld(targetPad);
                if (FlyTo(Above(pad, transform.position.y), speed, dt) && Rise(pad.y, climb, dt))
                {
                    State = Phase.Unload;
                    timer = 0.8f;
                }
                break;

            case Phase.Unload:
                timer -= dt;
                if (timer > 0f)
                    break;
                if (TargetAlive() && Target.Deliver(CargoItem, CargoCount))
                {
                    CargoItem = null;
                    CargoCount = 0;
                    ShowCrate(false);
                }
                ReleasePad();
                State = Phase.ClimbBack;
                break;

            case Phase.ClimbBack:
                ReleasePad();
                if (Rise(cruiseY, climb, dt))
                    State = Phase.Return;
                break;

            case Phase.Return:
                if (FlyTo(Above(Home.PadWorld(HomePad), cruiseY), speed, dt))
                    State = Phase.Land;
                break;

            case Phase.Land:
                Vector3 home = Home.PadWorld(HomePad);
                if (FlyTo(Above(home, transform.position.y), speed, dt) && Rise(home.y, climb, dt))
                {
                    if (CargoCount > 0)
                        Home.AcceptReturnedCargo(this);
                    SnapHome();
                }
                break;
        }

        Tilt(dt);
    }

    bool TargetAlive()
    {
        return Target != null && Target.IsPlaced;
    }

    void ReleasePad()
    {
        if (targetPad >= 0 && Target != null)
            Target.ReleasePad(targetPad, this);
        targetPad = -1;
    }

    static Vector3 Above(Vector3 p, float y)
    {
        return new Vector3(p.x, y, p.z);
    }

    bool Rise(float y, float climb, float dt)
    {
        Vector3 p = transform.position;
        p.y = Mathf.MoveTowards(p.y, y, climb * dt);
        transform.position = p;
        return Mathf.Abs(p.y - y) < 0.01f;
    }

    Vector3 lastMove;

    bool FlyTo(Vector3 target, float speed, float dt)
    {
        Vector3 p = transform.position;
        Vector3 d = target - p;
        d.y = 0f;
        float dist = d.magnitude;
        if (dist < 0.02f)
        {
            lastMove = Vector3.zero;
            transform.position = new Vector3(target.x, p.y, target.z);
            return true;
        }

        // Плавно тормозим у цели.
        float step = Mathf.Min(dist, Mathf.Min(speed, 1.5f + dist * 2.5f) * dt);
        Vector3 move = d / dist * step;
        transform.position = p + move;
        lastMove = move / Mathf.Max(dt, 0.0001f);
        if (dist > 0.5f)
        {
            Quaternion face = Quaternion.LookRotation(new Vector3(d.x, 0f, d.z));
            transform.rotation = Quaternion.Slerp(transform.rotation, face, 1f - Mathf.Exp(-6f * dt));
        }

        return false;
    }

    void Tilt(float dt)
    {
        if (visual == null)
            return;
        float lean = Mathf.Clamp(lastMove.magnitude / Mathf.Max(1f, DroneNetwork.Speed()), 0f, 1f) * 14f;
        Quaternion want = Quaternion.Euler(lean, 0f, 0f);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, want, 1f - Mathf.Exp(-5f * dt));
    }

    void SpinProps(bool on, float dt)
    {
        if (props == null)
            return;
        propSpin = Mathf.MoveTowards(propSpin, on ? 1500f : 0f, 1200f * dt);
        if (propSpin <= 0f)
            return;
        for (int i = 0; i < props.Length; i++)
        {
            if (props[i] != null)
                props[i].Rotate(0f, (i % 2 == 0 ? 1f : -1f) * propSpin * dt, 0f, Space.Self);
        }
    }

    void OnDestroy()
    {
        ReleasePad();
        DroneNetwork.AllDrones.Remove(this);
    }
}
