using UnityEngine;

public class BeltSpeedSystem : MonoBehaviour
{
    public static BeltSpeedSystem Instance { get; private set; }

    public int Level { get; private set; }
    public int GearsTowardNext { get; private set; }

    public event System.Action OnChanged;

    public int MaxLevel => Economy.BeltMaxLevel;
    public bool IsMaxed => Level >= MaxLevel;
    public int NextGearCost => IsMaxed ? 0 : Economy.BeltGearCost(Level + 1);
    public int NextCoinCost => IsMaxed ? 0 : Economy.BeltCoinCost(Level + 1);
    public int NextCost => NextGearCost;
    public float CheatMul { get; private set; } = 1f;

    public float Multiplier => Economy.BeltMultiplier(Level) * Mathf.Max(0.05f, CheatMul);
    public float Progress01 => NextGearCost > 0 ? Mathf.Clamp01((float)GearsTowardNext / NextGearCost) : 1f;
    public bool GearsReady => !IsMaxed && GearsTowardNext >= NextGearCost;

    public bool CanBuyNext
    {
        get
        {
            if (IsMaxed || !GearsReady)
                return false;
            return PlayerWallet.Instance == null || PlayerWallet.Instance.CanAfford(NextCoinCost);
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResetToNewWorld();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetToNewWorld()
    {
        Level = 0;
        GearsTowardNext = 0;
        OnChanged?.Invoke();
    }

    public void SetCheatMul(float mul)
    {
        CheatMul = Mathf.Clamp(mul, 0.05f, 50f);
        OnChanged?.Invoke();
    }

    public bool SubmitGear()
    {
        GearsTowardNext++;
        OnChanged?.Invoke();
        return true;
    }

    public bool TryBuyNext()
    {
        if (IsMaxed || !GearsReady)
            return false;
        int gears = NextGearCost;
        int coins = NextCoinCost;
        if (PlayerWallet.Instance != null && !PlayerWallet.Instance.TrySpendCoins(coins))
            return false;

        GearsTowardNext -= gears;
        Level++;
        OnChanged?.Invoke();
        return true;
    }

    public void CaptureSave(SaveData save)
    {
        if (save == null)
            return;
        save.beltLevel = Level;
        save.beltGears = GearsTowardNext;
    }

    public void ApplySave(SaveData save)
    {
        if (save == null)
        {
            ResetToNewWorld();
            return;
        }

        Level = Mathf.Clamp(save.beltLevel, 0, MaxLevel);
        GearsTowardNext = Mathf.Max(0, save.beltGears);
        OnChanged?.Invoke();
    }
}
