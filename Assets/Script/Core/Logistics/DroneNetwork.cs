using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Общие числа дронов ([[DroneLoadStation]] → [[Drone]] → [[DroneUnloadStation]]):
/// исследования, грузоподъёмность, скорость, слоты, цена дрона в рубинах.
/// </summary>
public static class DroneNetwork
{
    public const string ResearchDrones = "research_drones";
    public const string ResearchSpeed1 = "research_drone_speed_1";
    public const string ResearchSpeed2 = "research_drone_speed_2";
    public const string ResearchCargo1 = "research_drone_cargo_1";
    public const string ResearchCargo2 = "research_drone_cargo_2";
    public const string ResearchSlots2 = "research_drone_slots_2";
    public const string ResearchSlots3 = "research_drone_slots_3";
    public const string ResearchSlots4 = "research_drone_slots_4";

    /// <summary>Консоль (/drones speed): множитель скорости всех дронов.</summary>
    public static float DevSpeedMul = 1f;

    public const int MaxDrones = 4;
    public const int ReadyCrates = 4;
    public const int UnloadCapacity = 1000;
    public const float CruiseHeight = 12f;
    public const float ClimbSpeed = 7f;
    public const float SealIdleSeconds = 12f;

    /// <summary>Рубли за 2-го, 3-го, 4-го дрона (первый в комплекте).</summary>
    static readonly int[] DronePrice = { 0, 5, 8, 12 };

    static bool Has(string id)
    {
        ResearchSystem rs = ResearchSystem.Instance;
        return rs != null && rs.IsResearchIdUnlocked(id);
    }

    public static int Capacity()
    {
        if (Has(ResearchCargo2))
            return 150;
        if (Has(ResearchCargo1))
            return 100;
        return 50;
    }

    /// <summary>Лента 3-го уровня ≈ 2.5 × 1.9 м/с; дрон ×10 от неё, прокачка сверху.</summary>
    public static float Speed()
    {
        float belt3 = 2.5f * Economy.BeltMultiplier(3);
        float mul = Has(ResearchSpeed2) ? 1.8f : Has(ResearchSpeed1) ? 1.35f : 1f;
        return belt3 * 10f * mul;
    }

    public static int Slots()
    {
        if (Has(ResearchSlots4))
            return 4;
        if (Has(ResearchSlots3))
            return 3;
        if (Has(ResearchSlots2))
            return 2;
        return 1;
    }

    public static int PriceForNext(int owned)
    {
        if (owned < 1 || owned >= MaxDrones)
            return 0;
        return DronePrice[owned];
    }

    public static readonly List<Drone> AllDrones = new List<Drone>();

    public static void CollectUnloadStations(List<DroneUnloadStation> into)
    {
        into.Clear();
        DroneUnloadStation[] all = Object.FindObjectsByType<DroneUnloadStation>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].IsPlaced)
                into.Add(all[i]);
        }
    }

    public static string CellText(BuildingBase b)
    {
        if (b == null)
            return "";
        Vector2Int c = BuildingLinker.WorldToCell(b.transform.position);
        return c.x + "," + c.y;
    }
}
