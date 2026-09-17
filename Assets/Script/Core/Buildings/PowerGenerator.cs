using UnityEngine;

public class PowerGenerator : BuildingBase
{
    [Header("Power Settings")]
    public ItemData fuelItem;                 // какой предмет является топливом (уголь и т.д.)
    public float fuelBurnTime = 10f;          // сколько секунд работает от 1 топлива
    public float powerRadius = 15f;           // радиус, в котором ускоряет машины
    public float speedMultiplier = 1.5f;      // во сколько раз ускоряет

    private float remainingFuelTime = 0f;
    private bool isPowered = false;

    public override void OnPlaced()
    {
        base.OnPlaced();
        remainingFuelTime = 0f;
        isPowered = false;
    }

    void Update()
    {
        // Тратим топливо
        if (remainingFuelTime > 0f)
        {
            remainingFuelTime -= Time.deltaTime;
            isPowered = true;
        }
        else
        {
            isPowered = false;
        }

        // Можно потом добавить визуал (огонь, свет и т.д.)
    }

    public override bool TryReceiveItem(ItemData item, BuildingSocket fromSocket)
    {
        // Принимаем только топливо
        if (item != fuelItem) return false;

        // Добавляем время горения
        remainingFuelTime += fuelBurnTime;
        return true;
    }

    // Другие здания могут спросить, есть ли рядом работающий генератор
    public bool IsPowered() => isPowered;

    public float GetSpeedMultiplier() => isPowered ? speedMultiplier : 1f;

    // Можно вызывать из других зданий
    public static float GetNearbySpeedMultiplier(Vector3 position, float checkRadius = 20f)
    {
        var generators = FindObjectsByType<PowerGenerator>(FindObjectsSortMode.None);
        float bestMultiplier = 1f;

        foreach (var gen in generators)
        {
            if (!gen.isPowered) continue;

            float dist = Vector3.Distance(position, gen.transform.position);
            if (dist <= gen.powerRadius)
            {
                bestMultiplier = Mathf.Max(bestMultiplier, gen.speedMultiplier);
            }
        }

        return bestMultiplier;
    }
}