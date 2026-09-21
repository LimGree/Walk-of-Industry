using System.Collections.Generic;
using UnityEngine;

public class PowerGenerator : BuildingBase, IInteractable
{
    static readonly List<PowerGenerator> live = new List<PowerGenerator>();

    [Header("Power Settings")]
    public ItemData fuelItem;
    public float fuelBurnTime = 10f;
    public float powerRadius = 15f;
    public float speedMultiplier = 1.5f;

    float remainingFuelTime;
    bool isPowered;

    public bool Powered => isPowered;
    public float RemainingFuel => remainingFuelTime;
    public float SpeedMul => isPowered ? speedMultiplier : 1f;

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

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        if (!IsFuel(item))
            return false;
        remainingFuelTime += Mathf.Max(0.1f, fuelBurnTime);
        isPowered = true;
        return true;
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