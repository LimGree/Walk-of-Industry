using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;

public class SaveSystem : MonoBehaviour
{
    public static SaveSystem Instance { get; private set; }

    [Header("Autosave")]
    public float autoSaveInterval = 120f;

    float nextAutoSave;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
        nextAutoSave = Time.unscaledTime + Mathf.Max(30f, autoSaveInterval);
    }

    void Update()
    {
        if (!WorldCatalog.HasActive)
            return;
        if (autoSaveInterval <= 0f)
            return;
        if (Time.unscaledTime < nextAutoSave)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;

        SaveGame();
        nextAutoSave = Time.unscaledTime + autoSaveInterval;
    }

    void OnApplicationQuit()
    {
        if (WorldCatalog.HasActive)
            SaveGame();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused && WorldCatalog.HasActive)
            SaveGame();
    }

    public void SaveGame()
    {
        if (!WorldCatalog.HasActive)
        {
            Debug.LogWarning("[Save] Нет активного мира");
            return;
        }

        SaveData data = new SaveData
        {
            version = SaveData.CurrentVersion,
            worldName = WorldCatalog.Active.name,
            seed = WorldCatalog.Active.seed
        };

        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            data.hasPlayer = true;
            data.playerPos = player.transform.position;
            data.playerYaw = player.transform.eulerAngles.y;
            data.playerPitch = player.Pitch;
        }

        BuildingBase[] buildings = FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingBase building = buildings[i];
            if (building == null || building.data == null)
                continue;
            if (string.IsNullOrEmpty(building.data.id))
                continue;

            BuildingSaveData row = new BuildingSaveData
            {
                buildingId = building.data.id,
                position = building.transform.position,
                rotationY = building.transform.eulerAngles.y,
                level = building.ReadLevel()
            };
            building.WriteSave(row);
            data.buildings.Add(row);
        }

        if (ResearchSystem.Instance != null)
            data.research = ResearchSystem.Instance.CaptureSave();
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.CaptureSave(data);
        if (BeltSpeedSystem.Instance != null)
            BeltSpeedSystem.Instance.CaptureSave(data);
        if (MapMarkerSystem.Instance != null)
            MapMarkerSystem.Instance.CaptureSave(data);
        if (MapExploration.Instance != null)
            MapExploration.Instance.CaptureSave(data);
        if (ProductionStats.Instance != null)
            ProductionStats.Instance.CaptureSave(data);
        if (DayNightCycle.Instance != null)
            DayNightCycle.Instance.CaptureSave(data);
        else
        {
            data.worldHour = DayNight.Hour;
            data.worldDay = DayNight.Day;
        }
        if (WeatherCycle.Instance != null)
            WeatherCycle.Instance.CaptureSave(data);
        else
            data.worldWeather = (int)Weather.Kind;
        if (TutorialSystem.Instance != null)
            TutorialSystem.Instance.CaptureSave(data);
        if (AchievementSystem.Instance != null)
            AchievementSystem.Instance.CaptureSave(data);

        PlayerInventory inv = Object.FindFirstObjectByType<PlayerInventory>();
        if (inv != null)
        {
            data.hotbarBuildingIds = inv.CaptureHotbarIds();
            data.hotbarSelectedIndex = inv.selectedIndex;
        }

        string path = WorldCatalog.ActiveSavePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        if (WorldCatalog.Active != null)
            WorldCatalog.WritePreviewPng(WorldCatalog.Active, ScreenPreview.CapturePng());
        WorldCatalog.SetActive(WorldCatalog.Active);
        Debug.Log($"[Save] v{data.version}  {data.buildings.Count} зданий → {path}");
    }

    public void LoadGame()
    {
        IEnumerator routine = LoadGameRoutine(null);
        while (routine.MoveNext())
        {
        }
    }

    public IEnumerator LoadGameRoutine(System.Action<float> onProgress)
    {
        void Report(float t)
        {
            onProgress?.Invoke(Mathf.Clamp01(t));
        }

        if (!WorldCatalog.HasActive)
        {
            Debug.Log("[Save] Нет активного мира — новая сессия сцены");
            Report(1f);
            yield break;
        }

        string path = WorldCatalog.ActiveSavePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            if (PlayerWallet.Instance != null)
                PlayerWallet.Instance.ResetToNewWorld();
            if (BeltSpeedSystem.Instance != null)
                BeltSpeedSystem.Instance.ResetToNewWorld();
            if (MapMarkerSystem.Instance != null)
                MapMarkerSystem.Instance.ResetToNewWorld();
            if (MapExploration.Instance != null)
                MapExploration.Instance.ResetToNewWorld();
            if (ProductionStats.Instance != null)
                ProductionStats.Instance.ResetAll();
            if (DayNightCycle.Instance != null)
                DayNightCycle.Instance.ResetToNewWorld();
            else
                DayNight.ResetToNewWorld();
            if (WeatherCycle.Instance != null)
                WeatherCycle.Instance.ResetToNewWorld();
            else
                Weather.ResetToNewWorld();
            if (TutorialSystem.Instance != null)
                TutorialSystem.Instance.OnWorldReady(false, null);
            Report(1f);
            yield break;
        }

        SaveData data = SaveData.Normalize(JsonUtility.FromJson<SaveData>(File.ReadAllText(path)));
        if (data == null)
        {
            Debug.LogError("[Save] Не удалось прочитать сохранение");
            AchievementSystem.Mute = false;
            Report(1f);
            yield break;
        }

        BuildingData[] catalog = GameDatabase.AllBuildings();
        BuildingLinker.SuppressRelink = true;
        AchievementSystem.Mute = true;
        UndergroundConveyor.BeginLoad();
        ClearWorldBuildings();
        Report(0.08f);
        yield return null;

        var spawned = new List<BuildingBase>();
        var states = new List<BuildingSaveData>();
        int count = 0;
        int total = data.buildings != null ? Mathf.Max(1, data.buildings.Count) : 1;
        if (data.buildings != null)
        {
            for (int i = 0; i < data.buildings.Count; i++)
            {
                BuildingBase building = SpawnBuilding(data.buildings[i], catalog);
                if (building == null)
                    continue;
                spawned.Add(building);
                states.Add(data.buildings[i]);
                count++;
                if (count % 8 == 0)
                {
                    Report(0.08f + 0.7f * (i / (float)total));
                    yield return null;
                }
            }
        }

        BuildingLinker.SuppressRelink = false;
        Report(0.82f);
        yield return null;
        BuildingLinker.RelinkAll();

        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] == null)
                continue;
            spawned[i].ApplyLevel(states[i].level);
            spawned[i].ReadSave(states[i]);
            if (i % 16 == 0)
                yield return null;
        }

        UndergroundConveyor.FinishLoad();
        Report(0.92f);
        if (ResearchSystem.Instance != null)
            ResearchSystem.Instance.ApplySave(data.research);
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.ApplySave(data);
        if (BeltSpeedSystem.Instance != null)
            BeltSpeedSystem.Instance.ApplySave(data);
        if (MapMarkerSystem.Instance != null)
            MapMarkerSystem.Instance.ApplySave(data);
        if (MapExploration.Instance != null)
            MapExploration.Instance.ApplySave(data);
        if (ProductionStats.Instance != null)
            ProductionStats.Instance.ApplySave(data);
        if (DayNightCycle.Instance != null)
            DayNightCycle.Instance.ApplySave(data);
        else
        {
            DayNight.Hour = DayNight.WrapHour(data.worldHour);
            DayNight.Day = Mathf.Max(1, data.worldDay);
        }
        if (WeatherCycle.Instance != null)
            WeatherCycle.Instance.ApplySave(data);
        else
            Weather.Kind = data.worldWeather >= 0 && data.worldWeather <= 2
                ? (WeatherKind)data.worldWeather
                : WeatherKind.Clear;

        if (TutorialSystem.Instance != null)
            TutorialSystem.Instance.PrepareFromSave(true, data);
        if (AchievementSystem.Instance != null)
            AchievementSystem.Instance.ApplySave(data);

        PlayerInventory inv = Object.FindFirstObjectByType<PlayerInventory>();
        if (inv != null)
        {
            inv.ApplyHotbarIds(data.hotbarBuildingIds);
            inv.SelectSlot(data.hotbarSelectedIndex);
        }

        if (data.hasPlayer)
        {
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null)
                player.ApplySavedPose(data.playerPos, data.playerYaw, data.playerPitch);
        }

        if (WorldCatalog.Active != null && WorldCatalog.Active.sandbox && count == 0)
        {
            TestYard.Populate();
            BuildingBase[] after = Object.FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
            count = after != null ? after.Length : count;
        }

        if (TutorialSystem.Instance != null)
            TutorialSystem.Instance.OnWorldReady(true, data);

        AchievementSystem.Mute = false;
        nextAutoSave = Time.unscaledTime + Mathf.Max(30f, autoSaveInterval);
        Debug.Log($"[Save] Загружено зданий: {count}  (файл v{data.version})");
        Report(1f);
    }

    static BuildingData FindBuildingData(string id, BuildingData[] catalog)
    {
        if (string.IsNullOrEmpty(id) || catalog == null)
            return null;

        string key = GameDatabase.Normalize(id);
        for (int i = 0; i < catalog.Length; i++)
        {
            BuildingData d = catalog[i];
            if (d != null && GameDatabase.Normalize(d.id) == key)
                return d;
        }

        return GameDatabase.FindBuilding(id);
    }

    static void ClearWorldBuildings()
    {
        UndergroundConveyor.SuppressPairDestroy = true;
        BuildingBase[] buildings = Object.FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        for (int i = 0; i < buildings.Length; i++)
        {
            BuildingBase b = buildings[i];
            if (b == null)
                continue;
            b.OnRemoved();
            Object.Destroy(b.gameObject);
        }
        UndergroundConveyor.SuppressPairDestroy = false;
    }

    static BuildingBase SpawnBuilding(BuildingSaveData bsd, BuildingData[] catalog)
    {
        if (bsd == null)
            return null;

        BuildingData data = FindBuildingData(bsd.buildingId, catalog);
        bool pairExit = data != null && data.IsPairedStraight && UndergroundConveyor.IsExitSave(bsd);
        GameObject prefab = UndergroundConveyor.PrefabFor(data, pairExit);
        if (data == null || prefab == null)
        {
            Debug.LogWarning("[SaveSystem] Нет префаба для id=" + bsd.buildingId);
            return null;
        }

        Quaternion rot = Quaternion.Euler(0f, bsd.rotationY, 0f);
        GameObject go = Object.Instantiate(prefab, bsd.position, rot);
        BuildingBase building = go.GetComponent<BuildingBase>();
        if (building != null)
        {
            building.data = data;
            building.OnPlaced();
            return building;
        }

        if (GridSystem.Instance != null)
        {
            Vector2Int size = GridFootprint.GetRotatedSize(data.size, bsd.rotationY);
            GridFootprint.Register(go, bsd.position, size);
        }

        return null;
    }
}
