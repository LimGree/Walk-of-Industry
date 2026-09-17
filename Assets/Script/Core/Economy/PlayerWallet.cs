using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; private set; }

    public int Coins { get; private set; }
    public int Rubies { get; private set; }

    public event System.Action OnChanged;

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
        Coins = Economy.StartingCoins;
        Rubies = Economy.StartingRubies;
        OnChanged?.Invoke();
    }

    public bool CanAfford(int coins)
    {
        return coins <= 0 || Coins >= coins;
    }

    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0)
            return true;
        if (Coins < amount)
            return false;
        Coins -= amount;
        ProductionStats.Instance?.RecordCoinsSpent(amount);
        OnChanged?.Invoke();
        return true;
    }

    public void AddCoins(int amount)
    {
        if (amount == 0)
            return;
        Coins = Mathf.Max(0, Coins + amount);
        if (amount > 0)
            ProductionStats.Instance?.RecordCoinsGained(amount);
        OnChanged?.Invoke();
    }

    public void AddRubies(int amount)
    {
        if (amount == 0)
            return;
        Rubies = Mathf.Max(0, Rubies + amount);
        if (amount > 0)
            ProductionStats.Instance?.RecordRubiesGained(amount);
        OnChanged?.Invoke();
    }

    public bool TryExchangeRubies(int rubies)
    {
        if (rubies <= 0 || Rubies < rubies)
            return false;
        Rubies -= rubies;
        AddCoins(rubies * Economy.CoinsPerRuby);
        OnChanged?.Invoke();
        return true;
    }

    public void CaptureSave(SaveData save)
    {
        if (save == null)
            return;
        save.coins = Coins;
        save.rubies = Rubies;
    }

    public void ApplySave(SaveData save)
    {
        if (save == null)
        {
            ResetToNewWorld();
            return;
        }

        Coins = Mathf.Max(0, save.coins);
        Rubies = Mathf.Max(0, save.rubies);
        bool emptyWorld = save.buildings == null || save.buildings.Count == 0;
        if (Coins == 0 && Rubies == 0 && (save.version < 3 || emptyWorld))
            Coins = Economy.StartingCoins;
        OnChanged?.Invoke();
    }
}
