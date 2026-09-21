using System.Collections.Generic;
using UnityEngine;

public class AchievementSystem : MonoBehaviour
{
    public static AchievementSystem Instance { get; private set; }
    public static bool Mute;

    public struct Def
    {
        public string id;
        public string titleKey;
        public string bodyKey;
    }

    public static readonly Def[] Catalog =
    {
        new Def { id = "first_belt", titleKey = "ach.first_belt", bodyKey = "ach.first_belt_body" },
        new Def { id = "first_extractor", titleKey = "ach.first_extractor", bodyKey = "ach.first_extractor_body" },
        new Def { id = "first_smelt", titleKey = "ach.first_smelt", bodyKey = "ach.first_smelt_body" },
        new Def { id = "first_research", titleKey = "ach.first_research", bodyKey = "ach.first_research_body" },
        new Def { id = "first_underground", titleKey = "ach.first_underground", bodyKey = "ach.first_underground_body" },
        new Def { id = "first_splitter", titleKey = "ach.first_splitter", bodyKey = "ach.first_splitter_body" },
        new Def { id = "first_pipe", titleKey = "ach.first_pipe", bodyKey = "ach.first_pipe_body" },
        new Def { id = "gears_1000", titleKey = "ach.gears_1000", bodyKey = "ach.gears_1000_body" },
        new Def { id = "coins_5000", titleKey = "ach.coins_5000", bodyKey = "ach.coins_5000_body" },
        new Def { id = "night_watch", titleKey = "ach.night_watch", bodyKey = "ach.night_watch_body" },
        new Def { id = "first_generator", titleKey = "ach.first_generator", bodyKey = "ach.first_generator_body" },
        new Def { id = "factory_25", titleKey = "ach.factory_25", bodyKey = "ach.factory_25_body" }
    };

    readonly HashSet<string> unlocked = new HashSet<string>();
    bool timeCheated;
    int placedBuildings;

    public IReadOnlyCollection<string> UnlockedIds => unlocked;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public bool IsUnlocked(string id)
    {
        return !string.IsNullOrEmpty(id) && unlocked.Contains(id);
    }

    public static void NotifyPlaced(BuildingBase building)
    {
        if (Mute || Instance == null || building == null || building.data == null)
            return;
        Instance.placedBuildings++;
        string id = building.data.id ?? "";
        if (building is Conveyor && !(building is Pipe))
            Instance.Unlock("first_belt");
        if (id == "extractor")
            Instance.Unlock("first_extractor");
        if (id == "underground_conveyor")
            Instance.Unlock("first_underground");
        if (id == "splitter")
            Instance.Unlock("first_splitter");
        if (id == "pipe" || building is Pipe)
            Instance.Unlock("first_pipe");
        if (id == "power_generator")
            Instance.Unlock("first_generator");
        if (Instance.placedBuildings >= 25)
            Instance.Unlock("factory_25");
    }

    public static void NotifyProduced(string itemId, int total)
    {
        if (Mute || Instance == null || string.IsNullOrEmpty(itemId))
            return;
        string id = itemId.ToLowerInvariant();
        if (id.IndexOf("ingot") >= 0)
            Instance.Unlock("first_smelt");
        if (id == "gear" && total >= 1000)
            Instance.Unlock("gears_1000");
    }

    public static void NotifyCoins(int coins)
    {
        if (Mute || Instance == null)
            return;
        if (coins >= 5000)
            Instance.Unlock("coins_5000");
    }

    public static void NotifyResearchLive()
    {
        if (Mute || Instance == null)
            return;
        Instance.Unlock("first_research");
    }

    public static void NotifyTimeCheat()
    {
        if (Instance == null)
            return;
        Instance.timeCheated = true;
    }

    public static void TickNight()
    {
        if (Mute || Instance == null)
            return;
        if (Instance.timeCheated || !GameSettings.DayNightEnabled)
            return;
        float h = DayNight.Hour;
        if (h >= 21f || h < DayNight.Dawn)
            Instance.Unlock("night_watch");
    }

    public void Unlock(string id)
    {
        if (string.IsNullOrEmpty(id) || !unlocked.Add(id))
            return;
        Def def = Find(id);
        UiAudio.PlayNotify();
        UiNotification.Push(
            UiLocale.T("ach.toast"),
            def.id != null ? UiLocale.T(def.titleKey) : id,
            UiStatus.Completed);
    }

    public static Def Find(string id)
    {
        for (int i = 0; i < Catalog.Length; i++)
        {
            if (Catalog[i].id == id)
                return Catalog[i];
        }

        return default;
    }

    public void CaptureSave(SaveData save)
    {
        if (save == null)
            return;
        save.achievements = new List<string>(unlocked);
        save.timeCheated = timeCheated;
    }

    public void ApplySave(SaveData save)
    {
        unlocked.Clear();
        timeCheated = false;
        placedBuildings = 0;
        if (save == null)
            return;
        timeCheated = save.timeCheated;
        if (save.achievements == null)
            return;
        for (int i = 0; i < save.achievements.Count; i++)
        {
            if (!string.IsNullOrEmpty(save.achievements[i]))
                unlocked.Add(save.achievements[i]);
        }
    }

    public void ResetToNewWorld()
    {
        unlocked.Clear();
        timeCheated = false;
        placedBuildings = 0;
    }
}
