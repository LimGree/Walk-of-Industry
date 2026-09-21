using System.Collections.Generic;
using UnityEngine;

public class ProductionStats : MonoBehaviour
{
    public static ProductionStats Instance { get; private set; }

    const float Window = 30f;

    readonly Dictionary<string, int> producedTotal = new Dictionary<string, int>();
    readonly Dictionary<string, int> consumedTotal = new Dictionary<string, int>();
    readonly List<Event> events = new List<Event>(256);

    int coinsGained;
    int coinsSpent;
    int rubiesGained;
    float sessionBegan;
    readonly List<MoneyEvent> money = new List<MoneyEvent>(128);

    struct Event
    {
        public float time;
        public string id;
        public int amount;
        public bool produced;
    }

    struct MoneyEvent
    {
        public float time;
        public int coins;
        public int rubies;
        public bool spent;
    }

    public IReadOnlyDictionary<string, int> ProducedTotal => producedTotal;
    public IReadOnlyDictionary<string, int> ConsumedTotal => consumedTotal;
    public int CoinsGainedTotal => coinsGained;
    public int CoinsSpentTotal => coinsSpent;
    public int RubiesGainedTotal => rubiesGained;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        sessionBegan = Time.unscaledTime;
        ResetAll();
    }

    public float SessionSeconds => Mathf.Max(0f, Time.unscaledTime - sessionBegan);

    public int TotalProducedCount()
    {
        int n = 0;
        foreach (var pair in producedTotal)
            n += pair.Value;
        return n;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetAll()
    {
        producedTotal.Clear();
        consumedTotal.Clear();
        events.Clear();
        money.Clear();
        coinsGained = 0;
        coinsSpent = 0;
        rubiesGained = 0;
    }

    public void RecordProduced(ItemData item, int amount)
    {
        if (item == null || amount <= 0 || string.IsNullOrEmpty(item.id))
            return;
        Add(producedTotal, item.id, amount);
        events.Add(new Event { time = Time.unscaledTime, id = item.id, amount = amount, produced = true });
        Trim();
        producedTotal.TryGetValue(item.id, out int total);
        AchievementSystem.NotifyProduced(item.id, total);
    }

    public void RecordConsumed(ItemData item, int amount)
    {
        if (item == null || amount <= 0 || string.IsNullOrEmpty(item.id))
            return;
        Add(consumedTotal, item.id, amount);
        events.Add(new Event { time = Time.unscaledTime, id = item.id, amount = amount, produced = false });
        Trim();
    }

    public void RecordCoinsGained(int amount)
    {
        if (amount <= 0)
            return;
        coinsGained += amount;
        money.Add(new MoneyEvent { time = Time.unscaledTime, coins = amount });
        Trim();
    }

    public void RecordCoinsSpent(int amount)
    {
        if (amount <= 0)
            return;
        coinsSpent += amount;
        money.Add(new MoneyEvent { time = Time.unscaledTime, coins = amount, spent = true });
        Trim();
    }

    public void RecordRubiesGained(int amount)
    {
        if (amount <= 0)
            return;
        rubiesGained += amount;
        money.Add(new MoneyEvent { time = Time.unscaledTime, rubies = amount });
        Trim();
    }

    public float ProducedPerMinute(string itemId)
    {
        return Rate(itemId, true);
    }

    public float ConsumedPerMinute(string itemId)
    {
        return Rate(itemId, false);
    }

    public float CoinsPerMinute()
    {
        Trim();
        float now = Time.unscaledTime;
        int sum = 0;
        for (int i = 0; i < money.Count; i++)
        {
            if (money[i].spent || now - money[i].time > Window)
                continue;
            sum += money[i].coins;
        }

        return sum * (60f / Window);
    }

    public float RubiesPerMinute()
    {
        Trim();
        float now = Time.unscaledTime;
        int sum = 0;
        for (int i = 0; i < money.Count; i++)
        {
            if (now - money[i].time > Window)
                continue;
            sum += money[i].rubies;
        }

        return sum * (60f / Window);
    }

    public float CoinsSpentPerMinute()
    {
        Trim();
        float now = Time.unscaledTime;
        int sum = 0;
        for (int i = 0; i < money.Count; i++)
        {
            if (!money[i].spent || now - money[i].time > Window)
                continue;
            sum += money[i].coins;
        }

        return sum * (60f / Window);
    }

    public void CaptureSave(SaveData save)
    {
        if (save == null)
            return;
        save.statsProduced = ToList(producedTotal);
        save.statsConsumed = ToList(consumedTotal);
        save.statsCoinsGained = coinsGained;
        save.statsCoinsSpent = coinsSpent;
        save.statsRubiesGained = rubiesGained;
    }

    public void ApplySave(SaveData save)
    {
        ResetAll();
        if (save == null)
            return;
        FromList(save.statsProduced, producedTotal);
        FromList(save.statsConsumed, consumedTotal);
        coinsGained = Mathf.Max(0, save.statsCoinsGained);
        coinsSpent = Mathf.Max(0, save.statsCoinsSpent);
        rubiesGained = Mathf.Max(0, save.statsRubiesGained);
        sessionBegan = Time.unscaledTime;
    }

    float Rate(string itemId, bool produced)
    {
        if (string.IsNullOrEmpty(itemId))
            return 0f;
        Trim();
        float now = Time.unscaledTime;
        int sum = 0;
        for (int i = 0; i < events.Count; i++)
        {
            Event e = events[i];
            if (e.produced != produced || e.id != itemId || now - e.time > Window)
                continue;
            sum += e.amount;
        }

        return sum * (60f / Window);
    }

    void Trim()
    {
        float min = Time.unscaledTime - Window;
        int i = 0;
        while (i < events.Count && events[i].time < min)
            i++;
        if (i > 0)
            events.RemoveRange(0, i);

        i = 0;
        while (i < money.Count && money[i].time < min)
            i++;
        if (i > 0)
            money.RemoveRange(0, i);
    }

    static void Add(Dictionary<string, int> map, string id, int amount)
    {
        map.TryGetValue(id, out int have);
        map[id] = have + amount;
    }

    static List<ItemAmountSave> ToList(Dictionary<string, int> map)
    {
        var list = new List<ItemAmountSave>();
        foreach (var pair in map)
        {
            if (pair.Value <= 0)
                continue;
            list.Add(new ItemAmountSave { itemId = pair.Key, amount = pair.Value });
        }

        return list;
    }

    static void FromList(List<ItemAmountSave> source, Dictionary<string, int> dest)
    {
        dest.Clear();
        if (source == null)
            return;
        for (int i = 0; i < source.Count; i++)
        {
            ItemAmountSave row = source[i];
            if (string.IsNullOrEmpty(row.itemId) || row.amount <= 0)
                continue;
            dest.TryGetValue(row.itemId, out int have);
            dest[row.itemId] = have + row.amount;
        }
    }
}
