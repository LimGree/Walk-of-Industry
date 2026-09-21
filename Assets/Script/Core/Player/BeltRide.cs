using UnityEngine;
using UnityEngine.InputSystem;

public class BeltRide : MonoBehaviour
{
    public static BeltRide Instance { get; private set; }
    public bool IsRiding { get; private set; }

    Conveyor belt;
    Splitter splitter;
    float progress;
    Vector2Int entryDir;
    Vector2Int exitDir;
    Vector3 smoothPos;
    CharacterController controller;
    PlayerMovement movement;
    bool savedCanMove;

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
            if (MachineUI.Instance != null)
                MachineUI.Instance.Open(building);
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
            entryDir = -conv.ExitDir;
        else
            entryDir = InferEntry(split);
        exitDir = Vector2Int.zero;
        if (split != null)
            exitDir = split.TakeRideExit(entryDir);
        IsRiding = true;
        smoothPos = transform.position;
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

        if (belt == null && splitter == null)
        {
            Stop();
            return;
        }

        float boost = BeltSpeedSystem.Instance != null ? BeltSpeedSystem.Instance.Multiplier : 1f;
        float throttle = 1f;
        if (kb != null)
        {
            bool fwd = kb.wKey.isPressed || kb.upArrowKey.isPressed;
            bool back = kb.sKey.isPressed || kb.downArrowKey.isPressed;
            if (fwd && !back)
                throttle = 7f;
            else if (back && !fwd)
                throttle = 0.28f;
        }

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
            exitDir = nextSplit.TakeRideExit(entryDir);
            progress -= 1f;
            return true;
        }

        return false;
    }

    void ApplyPose()
    {
        Vector3 pos;
        if (splitter != null)
            pos = splitter.RideWorld(entryDir, exitDir, progress);
        else
        {
            BeltInMask side = BeltRules.SideFromTravel(belt.ExitDir, entryDir);
            pos = BeltRules.PathWorld(belt.transform, side, progress, 0.15f);
        }

        pos.y += 1.05f;
        float blend = 1f - Mathf.Exp(-10f * Time.deltaTime);
        smoothPos = Vector3.Lerp(smoothPos, pos, blend);
        transform.position = smoothPos;
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
