using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SaveData
{
    public const int CurrentVersion = 9;

    public int version = CurrentVersion;
    public string worldName;
    public int seed;
    public bool hasPlayer;
    public Vector3 playerPos;
    public float playerYaw;
    public float playerPitch;
    public List<BuildingSaveData> buildings = new List<BuildingSaveData>();
    public ResearchSaveData research = new ResearchSaveData();
    public List<string> hotbarBuildingIds = new List<string>();
    public int hotbarSelectedIndex;
    public int coins;
    public int rubies;
    public int beltLevel;
    public int beltGears;
    public List<ItemAmountSave> statsProduced = new List<ItemAmountSave>();
    public List<ItemAmountSave> statsConsumed = new List<ItemAmountSave>();
    public int statsCoinsGained;
    public int statsCoinsSpent;
    public int statsRubiesGained;
    public List<MapMarkerSave> markers = new List<MapMarkerSave>();
    public int exploreWidth;
    public int exploreHeight;
    public string exploredBits;
    public float worldHour = 9f;
    public int worldDay = 1;
    public int worldWeather;
    public int tutorialStep;
    public bool tutorialFinished;
    public bool tutorialSkipped;
    public List<string> achievements = new List<string>();
    public bool timeCheated;
    public List<SaveKeyValue> extras = new List<SaveKeyValue>();

    public static SaveData Normalize(SaveData data)
    {
        if (data == null)
            return null;

        if (data.version <= 0)
            data.version = 1;
        if (data.buildings == null)
            data.buildings = new List<BuildingSaveData>();
        if (data.research == null)
            data.research = new ResearchSaveData();
        if (data.research.inProgress == null)
            data.research.inProgress = new List<ResearchProgressSave>();
        if (data.extras == null)
            data.extras = new List<SaveKeyValue>();
        if (data.hotbarBuildingIds == null)
            data.hotbarBuildingIds = new List<string>();
        if (data.statsProduced == null)
            data.statsProduced = new List<ItemAmountSave>();
        if (data.statsConsumed == null)
            data.statsConsumed = new List<ItemAmountSave>();
        if (data.markers == null)
            data.markers = new List<MapMarkerSave>();
        if (data.achievements == null)
            data.achievements = new List<string>();
        if (data.version < 6)
            data.worldHour = 9f;
        data.worldHour = DayNight.WrapHour(data.worldHour);
        if (data.worldDay < 1)
            data.worldDay = 1;
        if (data.version < 9)
            data.tutorialFinished = true;

        for (int i = 0; i < data.buildings.Count; i++)
        {
            BuildingSaveData building = data.buildings[i];
            if (building == null)
                continue;
            if (building.inputBuffer == null)
                building.inputBuffer = new List<ItemAmountSave>();
            if (building.outputBuffer == null)
                building.outputBuffer = new List<ItemAmountSave>();
            if (building.storage == null)
                building.storage = new List<ItemAmountSave>();
            if (building.cargo == null)
                building.cargo = new List<BeltItemSave>();
            if (building.extras == null)
                building.extras = new List<SaveKeyValue>();
        }

        return data;
    }
}

[Serializable]
public class BuildingSaveData
{
    public string buildingId;
    public Vector3 position;
    public float rotationY;
    public int level = 1;
    public string recipeId;
    public float craftProgress;
    public int stateInt;
    public float stateFloat;
    public string filterItemId;
    public string heldItemId;
    public List<ItemAmountSave> inputBuffer = new List<ItemAmountSave>();
    public List<ItemAmountSave> outputBuffer = new List<ItemAmountSave>();
    public List<ItemAmountSave> storage = new List<ItemAmountSave>();
    public List<BeltItemSave> cargo = new List<BeltItemSave>();
    public List<SaveKeyValue> extras = new List<SaveKeyValue>();
}

[Serializable]
public class ResearchSaveData
{
    public List<string> unlockedResearchIds = new List<string>();
    public string currentResearchId;
    public List<ItemAmountSave> submittedItems = new List<ItemAmountSave>();
    public List<ResearchProgressSave> inProgress = new List<ResearchProgressSave>();
}

[Serializable]
public class ResearchProgressSave
{
    public string researchId;
    public List<ItemAmountSave> submitted = new List<ItemAmountSave>();
}

[Serializable]
public class ItemAmountSave
{
    public string itemId;
    public int amount;
}

[Serializable]
public class BeltItemSave
{
    public string itemId;
    public float progress;
    public int entryX;
    public int entryY;
    public int exitX;
    public int exitY;
}

[Serializable]
public class SaveKeyValue
{
    public string key;
    public string value;
}

[Serializable]
public class MapMarkerSave
{
    public int id;
    public int x;
    public int z;
    public string label;
    public float r = 1f;
    public float g = 0.85f;
    public float b = 0.25f;
    public int hidden;
    public string createdAt;
}

public static class SaveItems
{
    public static List<ItemAmountSave> FromCounts(Dictionary<ItemData, int> source)
    {
        var list = new List<ItemAmountSave>();
        if (source == null)
            return list;

        foreach (var pair in source)
        {
            if (pair.Key == null || string.IsNullOrEmpty(pair.Key.id) || pair.Value <= 0)
                continue;
            list.Add(new ItemAmountSave { itemId = pair.Key.id, amount = pair.Value });
        }

        return list;
    }

    public static void ToCounts(List<ItemAmountSave> source, Dictionary<ItemData, int> dest)
    {
        if (dest == null)
            return;
        dest.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            ItemAmountSave entry = source[i];
            ItemData item = GameDatabase.FindItem(entry.itemId);
            if (item == null || entry.amount <= 0)
                continue;
            dest.TryGetValue(item, out int have);
            dest[item] = have + entry.amount;
        }
    }

    public static List<ItemAmountSave> FromQueue(Queue<ItemData> source)
    {
        var list = new List<ItemAmountSave>();
        if (source == null)
            return list;

        foreach (ItemData item in source)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
                continue;
            list.Add(new ItemAmountSave { itemId = item.id, amount = 1 });
        }

        return list;
    }

    public static void ToQueue(List<ItemAmountSave> source, Queue<ItemData> dest)
    {
        if (dest == null)
            return;
        dest.Clear();
        if (source == null)
            return;

        for (int i = 0; i < source.Count; i++)
        {
            ItemAmountSave entry = source[i];
            ItemData item = GameDatabase.FindItem(entry.itemId);
            if (item == null)
                continue;
            int count = Mathf.Max(1, entry.amount);
            for (int n = 0; n < count; n++)
                dest.Enqueue(item);
        }
    }

    public static ItemData Find(string id)
    {
        return GameDatabase.FindItem(id);
    }
}
