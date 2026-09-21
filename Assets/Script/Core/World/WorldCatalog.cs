using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class WorldInfo
{
    public string id;
    public string name;
    public int seed;
    public string created;
    public string lastPlayed;
    public bool sandbox;
}

[Serializable]
public class WorldIndex
{
    public List<WorldInfo> worlds = new List<WorldInfo>();
}

public static class WorldCatalog
{
    public static WorldInfo Active { get; private set; }
    public static bool HasActive => Active != null && !string.IsNullOrEmpty(Active.id);

    static string Root
    {
        get
        {
            MigrateOldSavesOnce();
            return Path.Combine(Application.persistentDataPath, "worlds");
        }
    }
    static string IndexPath => Path.Combine(Root, "index.json");
    static bool migrated;

    static void MigrateOldSavesOnce()
    {
        if (migrated)
            return;
        migrated = true;

        string current = Application.persistentDataPath;
        DirectoryInfo parent = Directory.GetParent(current);
        if (parent == null)
            return;

        string oldRoot = Path.Combine(parent.FullName, "Walk to biome", "worlds");
        string newRoot = Path.Combine(current, "worlds");
        if (!Directory.Exists(oldRoot) || Directory.Exists(newRoot))
            return;

        try
        {
            CopyDirectory(oldRoot, newRoot);
            Debug.Log("[Worlds] Перенесены сохранения из Walk to biome → Walk of Industry");
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Worlds] Не удалось перенести старые сохранения: " + e.Message);
        }
    }

    static void CopyDirectory(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), false);
        foreach (string dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    public static string SavePath(WorldInfo world)
    {
        if (world == null || string.IsNullOrEmpty(world.id))
            return null;
        return Path.Combine(Root, world.id, "save.json");
    }

    public static string PreviewPath(WorldInfo world)
    {
        if (world == null || string.IsNullOrEmpty(world.id))
            return null;
        return Path.Combine(Root, world.id, "preview.png");
    }

    public static string ActiveSavePath => SavePath(Active);
    public static string WorldsFolder => Root;

    public static List<WorldInfo> ListWorlds()
    {
        WorldIndex index = ReadIndex();
        index.worlds.Sort((a, b) => string.CompareOrdinal(b.lastPlayed, a.lastPlayed));
        return index.worlds;
    }

    public static WorldInfo CreateWorld(string name, bool sandbox = false, int seed = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            name = sandbox ? "ТЕСТ" : "Новый мир";
        if (seed <= 0)
            seed = UnityEngine.Random.Range(1, 999999);

        Directory.CreateDirectory(Root);
        var world = new WorldInfo
        {
            id = "world_" + DateTime.UtcNow.Ticks,
            name = name.Trim(),
            seed = seed,
            created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            lastPlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            sandbox = sandbox
        };

        WorldIndex index = ReadIndex();
        index.worlds.Add(world);
        WriteIndex(index);

        Directory.CreateDirectory(Path.Combine(Root, world.id));
        var save = new SaveData
        {
            version = SaveData.CurrentVersion,
            worldName = world.name,
            seed = world.seed,
            coins = sandbox ? 500000 : Economy.StartingCoins,
            rubies = sandbox ? 999 : Economy.StartingRubies,
            tutorialSkipped = sandbox,
            tutorialFinished = sandbox
        };
        File.WriteAllText(SavePath(world), JsonUtility.ToJson(save, true));
        return world;
    }

    public static void DeleteWorld(string id)
    {
        if (string.IsNullOrEmpty(id))
            return;

        WorldIndex index = ReadIndex();
        index.worlds.RemoveAll(w => w != null && w.id == id);
        WriteIndex(index);

        string dir = Path.Combine(Root, id);
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);

        if (Active != null && Active.id == id)
            Active = null;
    }

    public static void SetActive(WorldInfo world)
    {
        Active = world;
        if (world == null)
            return;
        world.lastPlayed = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        WorldIndex index = ReadIndex();
        for (int i = 0; i < index.worlds.Count; i++)
        {
            if (index.worlds[i] != null && index.worlds[i].id == world.id)
                index.worlds[i].lastPlayed = world.lastPlayed;
        }
        WriteIndex(index);
    }

    public static void ClearActive()
    {
        Active = null;
    }

    static WorldIndex ReadIndex()
    {
        if (!File.Exists(IndexPath))
            return new WorldIndex();
        try
        {
            WorldIndex index = JsonUtility.FromJson<WorldIndex>(File.ReadAllText(IndexPath));
            return index ?? new WorldIndex();
        }
        catch
        {
            return new WorldIndex();
        }
    }

    static void WriteIndex(WorldIndex index)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(IndexPath, JsonUtility.ToJson(index ?? new WorldIndex(), true));
    }

    public static int ParseSeed(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;
        raw = raw.Trim();
        int n;
        if (int.TryParse(raw, out n))
            return Mathf.Abs(n);
        int hash = 23;
        for (int i = 0; i < raw.Length; i++)
            hash = hash * 31 + raw[i];
        return Mathf.Abs(hash % 999999) + 1;
    }

    public static int PeekCoins(WorldInfo world)
    {
        string path = SavePath(world);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return 0;
        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
            return data != null ? Mathf.Max(0, data.coins) : 0;
        }
        catch
        {
            return 0;
        }
    }

    public static void WritePreviewPng(WorldInfo world, byte[] png)
    {
        string path = PreviewPath(world);
        if (string.IsNullOrEmpty(path) || png == null || png.Length == 0)
            return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, png);
    }

    public static Texture2D LoadPreview(WorldInfo world)
    {
        string path = PreviewPath(world);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }

            return tex;
        }
        catch
        {
            return null;
        }
    }
}
