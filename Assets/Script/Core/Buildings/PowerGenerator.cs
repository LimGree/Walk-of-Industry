using System.Collections.Generic;
using UnityEngine;

public class PowerGenerator : BuildingBase, IInteractable
{
    static readonly List<PowerGenerator> live = new List<PowerGenerator>();

    [Header("Power Settings")]
    public ItemData fuelItem;
    public float fuelBurnTime = 10f;
    [Tooltip("Сколько порций топлива (угля) влезает в топку; дальше генератор не берёт.")]
    public int maxFuelItems = 10;
    [Tooltip("Секунд работы от одной порции нефти (подаётся трубой слева).")]
    public float oilBurnTime = 6f;
    public float powerRadius = 15f;
    public float speedMultiplier = 1.5f;

    float remainingFuelTime;
    bool isPowered;
    ItemData oilItem;

    const string FluidSocketName = "InputSocketFluid";

    public bool Powered => isPowered;
    public float RemainingFuel => remainingFuelTime;
    public float SpeedMul => isPowered ? speedMultiplier : 1f;

    void Awake()
    {
        EnsureSockets();
    }

    void OnEnable()
    {
        if (!live.Contains(this))
            live.Add(this);
    }

    void OnDisable()
    {
        live.Remove(this);
    }

    public override void OnPlaced()
    {
        EnsureSockets();
        BuildingPrefabLayout.ApplyPrimarySockets(this);
        base.OnPlaced();
        if (remainingFuelTime <= 0f)
            isPowered = false;
    }

    void Update()
    {
        if (remainingFuelTime > 0f)
        {
            remainingFuelTime -= Time.deltaTime;
            isPowered = remainingFuelTime > 0f;
        }
        else
            isPowered = false;
    }

    /// <summary>Запас топлива в секундах, больше которого топка не принимает.</summary>
    public float MaxFuelTime => Mathf.Max(1, maxFuelItems) * Mathf.Max(0.1f, fuelBurnTime);

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        float burn = BurnTime(item);
        if (burn <= 0f)
            return false;
        // топка полна — предмет остаётся на ленте/в трубе
        if (remainingFuelTime + burn > MaxFuelTime + 0.01f)
            return false;
        remainingFuelTime += burn;
        isPowered = true;
        return true;
    }

    /// <summary>Секунд работы от предмета; 0 — не топливо. Уголь — лентой сзади, нефть — трубой слева.</summary>
    float BurnTime(ItemData item)
    {
        if (item == null)
            return 0f;
        if (item.isFluid)
            return IsOil(item) ? Mathf.Max(0.1f, oilBurnTime) : 0f;
        return IsFuel(item) ? Mathf.Max(0.1f, fuelBurnTime) : 0f;
    }

    bool IsFuel(ItemData item)
    {
        if (item == null)
            return false;
        if (fuelItem != null)
            return item == fuelItem;
        string id = item.id != null ? item.id.Trim().ToLowerInvariant() : "";
        return id == "coal_ore" || id == "coal" || id == "log";
    }

    bool IsOil(ItemData item)
    {
        if (oilItem == null)
            oilItem = GameDatabase.FindItem("crude_oil");
        return item != null && item == oilItem;
    }

    /// <summary>Второй вход — труба для нефти (−X). Сокет создаётся кодом: префабу не нужен.</summary>
    void EnsureSockets()
    {
        if (inputSockets == null || inputSockets.Length == 0 || inputSockets[0] == null)
            return;
        for (int i = 1; i < inputSockets.Length; i++)
        {
            if (inputSockets[i] != null && inputSockets[i].name == FluidSocketName)
                return;
        }

        Transform existing = transform.Find(FluidSocketName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(FluidSocketName);
        if (existing == null)
            go.transform.SetParent(transform, false);
        go.layer = gameObject.layer;
        float halfX = Mathf.Max(0.2f, (data != null ? data.size.x : 1) * GridFootprint.CellSize * 0.5f - 0.02f);
        BuildingPrefabLayout.PlaceSocket(go.transform, new Vector3(-halfX, 0.3f, 0f), Quaternion.Euler(0f, -90f, 0f));
        BuildingSocket socket = go.GetComponent<BuildingSocket>();
        if (socket == null)
            socket = go.AddComponent<BuildingSocket>();
        socket.socketType = SocketType.Input;
        inputSockets = new[] { inputSockets[0], socket };
    }

    public bool IsPowered() => isPowered;
    public float GetSpeedMultiplier() => SpeedMul;

    public static float GetNearbySpeedMultiplier(Vector3 position, float checkRadius = 20f)
    {
        float best = 1f;
        for (int i = live.Count - 1; i >= 0; i--)
        {
            PowerGenerator gen = live[i];
            if (gen == null)
            {
                live.RemoveAt(i);
                continue;
            }
            if (!gen.isPowered || !gen.IsPlaced)
                continue;
            float dist = Vector3.Distance(position, gen.transform.position);
            if (dist <= gen.powerRadius)
                best = Mathf.Max(best, gen.speedMultiplier);
        }

        return best;
    }

    public override void WriteSave(BuildingSaveData save)
    {
        base.WriteSave(save);
        if (save == null)
            return;
        save.stateFloat = remainingFuelTime;
    }

    public override void ReadSave(BuildingSaveData save)
    {
        base.ReadSave(save);
        remainingFuelTime = save != null ? Mathf.Max(0f, save.stateFloat) : 0f;
        isPowered = remainingFuelTime > 0f;
    }

    public void Interact(GameObject interactor)
    {
        if (MachineUI.Instance != null)
            MachineUI.Instance.Open(this);
    }
}