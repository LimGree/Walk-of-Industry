using UnityEngine;
using UnityEngine.InputSystem;

public class BeltRide : MonoBehaviour
{
    public static BeltRide Instance { get; private set; }
    public bool IsRiding { get; private set; }
    /// <summary>Направление ленты в точке игрока (для вагонетки и позы), всегда по ходу груза.</summary>
    public Vector3 TravelForward { get; private set; }

    Conveyor belt;
    Splitter splitter;
    float progress;
    Vector2Int entryDir;
    Vector2Int exitDir;
    Vector3 smoothPos;
    CharacterController controller;
    PlayerMovement movement;
    bool savedCanMove;
    float lastThrottle = 1f;

    void Awake()
    {
        Instance = this;
        controller = GetComponent<CharacterController>();
        movement = GetComponent<PlayerMovement>();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static bool BuildModeOn()
    {
        PlayerBuilder builder = Object.FindFirstObjectByType<PlayerBuilder>();
        return builder != null && builder.isBuildMode;
    }

    public static void TryInteract(BuildingBase building, GameObject interactor)
    {
        if (building == null || interactor == null)
            return;
        BeltRide ride = interactor.GetComponent<BeltRide>();
        if (ride == null)
            ride = interactor.AddComponent<BeltRide>();
        if (ride.IsRiding)
        {
            ride.Stop();
            return;
        }

        if (BuildModeOn())
        {
            if (building is Conveyor && !ResearchSystem.BeltFilterUnlocked())
                return;
            if (MachineUI.Instance != null)
                MachineUI.Instance.Open(building);
            return;
        }

        // Ездить по лентам — в вагонетке ([[PerkSystem]] «cart»).
        if (!PerkSystem.Has("cart"))
        {
            UiNotification.Push(UiLocale.T("ride.need_cart"), UiLocale.T("ride.need_cart_sub"), UiStatus.Warning);
            UiAudio.PlayError();
            return;
        }

        ride.StartRide(building);
    }

    public void StartRide(BuildingBase start)
    {
        if (start == null)
            return;
        Conveyor conv = start as Conveyor;
        Splitter split = start as Splitter;
        if (conv == null && split == null)
            return;

        belt = conv;
        splitter = split;
        progress = 0f;
        if (conv != null)
            entryDir = FeederTravel(conv);
        else
            entryDir = InferEntry(split);
        exitDir = Vector2Int.zero;
        if (split != null)
            exitDir = split.TakeRideExit(entryDir);
        IsRiding = true;
        smoothPos = transform.position;
        Cart.Show(true);
        if (controller != null)
            controller.enabled = false;
        if (movement != null)
        {
            savedCanMove = movement.canMove;
            movement.canMove = false;
            movement.canLook = true;
        }
    }

    public void Stop()
    {
        if (!IsRiding)
            return;
        IsRiding = false;
        belt = null;
        splitter = null;
        Cart.Show(false);
        if (controller != null)
            controller.enabled = true;
        if (movement != null)
        {
            movement.canMove = savedCanMove;
            movement.canLook = true;
        }
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    void Update()
    {
        if (!IsRiding)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (PhotoMode.IsActive)
            return;
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            Stop();
            return;
        }

        // Пробел — спрыгнуть с подскоком.
        if (kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            Stop();
            if (movement != null)
                movement.Launch(5.5f);
            return;
        }

        if (kb != null && kb.gKey.wasPressedThisFrame && PerkSystem.Has("horn"))
            GameAudio.Player("player_horn");

        if (belt == null && splitter == null)
        {
            Stop();
            return;
        }

        float boost = BeltSpeedSystem.Instance != null ? BeltSpeedSystem.Instance.Multiplier : 1f;
        // W — разгон по ходу ленты, S — едем назад, против хода (по той же линии, через повороты и сплиттеры).
        float throttle = 1f;
        if (kb != null)
        {
            bool fwd = kb.wKey.isPressed || kb.upArrowKey.isPressed;
            bool back = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            if (fwd && !back)
                throttle = FwdThrottle;
            else if (back && !fwd)
                throttle = -BackThrottle;
        }

        lastThrottle = Mathf.Abs(throttle);
        float speed = (belt != null ? belt.speed : splitter.speed) * boost * throttle;
        float cell = GridFootprint.CellSize;
        progress += speed * Time.deltaTime / Mathf.Max(0.05f, cell);
        while (progress >= 1f)
        {
            if (!Advance())
            {
                progress = 1f;
                break;
            }
        }

        while (progress < 0f)
        {
            if (!Retreat())
            {
                progress = 0f;
                break;
            }
        }

        ApplyPose();
    }

    bool Advance()
    {
        Vector2Int nextCell;
        Vector2Int leaveDir;
        if (splitter != null)
        {
            leaveDir = exitDir.x == 0 && exitDir.y == 0 ? InferExit(splitter) : exitDir;
            nextCell = splitter.Cell + leaveDir;
        }
        else
        {
            leaveDir = belt.ExitDir;
            nextCell = belt.Cell + leaveDir;
        }

        BuildingBase dest = BuildingLinker.GetBuildingAt(nextCell);
        Conveyor nextBelt = dest as Conveyor;
        Splitter nextSplit = dest as Splitter;
        if (nextBelt != null)
        {
            entryDir = leaveDir;
            belt = nextBelt;
            splitter = null;
            exitDir = Vector2Int.zero;
            progress -= 1f;
            return true;
        }

        if (nextSplit != null)
        {
            entryDir = leaveDir;
            belt = null;
            splitter = nextSplit;
            exitDir = ChooseSplitExit(nextSplit, entryDir);
            progress -= 1f;
            return true;
        }

        return false;
    }

    CartVisual cart;

    CartVisual Cart
    {
        get
        {
            if (cart == null)
            {
                cart = GetComponent<CartVisual>();
                if (cart == null)
                    cart = gameObject.AddComponent<CartVisual>();
            }
            return cart;
        }
    }

    /// <summary>
    /// Вагонетка Mk2: на сплиттере A — влево, D — вправо (если туда идёт лента), иначе прямо.
    /// Без Mk2 — как решит сплиттер.
    /// </summary>
    static Vector2Int ChooseSplitExit(Splitter split, Vector2Int travel)
    {
        Keyboard kb = Keyboard.current;
        if (PerkSystem.Has("cart2") && kb != null)
        {
            Vector2Int left = new Vector2Int(-travel.y, travel.x);
            Vector2Int right = new Vector2Int(travel.y, -travel.x);
            Vector2Int want = kb.aKey.isPressed ? left : kb.dKey.isPressed ? right : travel;
            if (Leads(split, want))
                return want;
            if (Leads(split, travel))
                return travel;
        }

        return split.TakeRideExit(travel);
    }

    static bool Leads(Splitter split, Vector2Int dir)
    {
        BuildingBase next = BuildingLinker.GetBuildingAt(split.Cell + dir);
        return next is Conveyor || next is Splitter;
    }

    /// <summary>Во сколько раз быстрее скорости ленты едем назад.</summary>
    const float BackThrottle = 12f;
    /// <summary>Разгон по ходу ленты (W).</summary>
    const float FwdThrottle = 21f;

    static readonly Vector2Int[] Cardinals =
    {
        new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)
    };

    /// <summary>
    /// Шаг назад: в клетку, откуда мы въехали в текущую (cell − entryDir). Только если она действительно
    /// подаёт сюда (лента выходом в нашу клетку или сплиттер рядом). Начало линии — стоп.
    /// </summary>
    bool Retreat()
    {
        Vector2Int curCell = splitter != null ? splitter.Cell : belt.Cell;
        Vector2Int prevCell = curCell - entryDir;
        BuildingBase dest = BuildingLinker.GetBuildingAt(prevCell);

        if (dest is Conveyor prevBelt && prevBelt.Cell + prevBelt.ExitDir == curCell)
        {
            belt = prevBelt;
            splitter = null;
            exitDir = Vector2Int.zero;
            entryDir = FeederTravel(prevBelt);
            progress += 1f;
            return true;
        }

        if (dest is Splitter prevSplit)
        {
            belt = null;
            splitter = prevSplit;
            exitDir = curCell - prevSplit.Cell;
            entryDir = InferEntry(prevSplit);
            progress += 1f;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Направление движения, с которым груз въезжает в ленту: от того, кто её кормит
    /// (сначала прямо сзади, потом с боков). Никто не кормит — как будто въехали прямо.
    /// </summary>
    static Vector2Int FeederTravel(Conveyor target)
    {
        if (target == null)
            return new Vector2Int(0, 1);
        Vector2Int exit = target.ExitDir;
        if (Feeds(target.Cell - exit, target.Cell))
            return exit;
        for (int i = 0; i < Cardinals.Length; i++)
        {
            Vector2Int travel = Cardinals[i];
            if (travel == exit || travel == -exit)
                continue;
            if (Feeds(target.Cell - travel, target.Cell))
                return travel;
        }

        return exit;
    }

    static bool Feeds(Vector2Int from, Vector2Int to)
    {
        BuildingBase b = BuildingLinker.GetBuildingAt(from);
        if (b is Conveyor c)
            return c.Cell + c.ExitDir == to;
        return b is Splitter;
    }

    Vector3 PathAt(float t)
    {
        if (splitter != null)
            return splitter.RideWorld(entryDir, exitDir, t);
        BeltInMask side = BeltRules.SideFromTravel(belt.ExitDir, entryDir);
        return BeltRules.PathWorld(belt.transform, side, t, 0.15f);
    }

    void ApplyPose()
    {
        Vector3 pos = PathAt(progress);
        // Касательная к пути по ходу груза (на углах поворачивает вместе с лентой).
        float t0 = Mathf.Clamp01(progress - 0.04f);
        float t1 = Mathf.Clamp01(progress + 0.04f);
        Vector3 tangent = PathAt(t1) - PathAt(t0);
        tangent.y = 0f;
        if (tangent.sqrMagnitude > 0.000001f)
            TravelForward = Vector3.Slerp(TravelForward.sqrMagnitude > 0.01f ? TravelForward : tangent.normalized,
                tangent.normalized, 1f - Mathf.Exp(-14f * Time.deltaTime));

        pos.y += 1.05f;
        // Чем быстрее едем, тем жёстче догоняем точку на ленте — иначе на ×21 камера отстаёт на клетки.
        float blend = 1f - Mathf.Exp(-(10f + lastThrottle * 2f) * Time.deltaTime);
        smoothPos = Vector3.Lerp(smoothPos, pos, blend);
        transform.position = smoothPos;
        Cart.Place(smoothPos + Vector3.down * 0.95f, TravelForward);
    }

    static Vector2Int InferEntry(Splitter split)
    {
        if (split == null)
            return new Vector2Int(0, -1);
        Vector2Int from = BuildingLinker.WorldToCell(split.transform.position - split.transform.forward);
        Vector2Int delta = split.Cell - from;
        if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
            return delta;
        return BuildingLinker.ToCardinal(split.transform.forward);
    }

    static Vector2Int InferExit(Splitter split)
    {
        return BuildingLinker.ToCardinal(split.transform.forward);
    }
}
