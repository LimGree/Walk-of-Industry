using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class BlueprintBuilding
{
    public string buildingId;
    public int ox;
    public int oy;
    public float yaw;
    public int level;
    public string recipeId;
    public string filterItemId;
    public bool pairExit;
    public int pairId;
    /// <summary>Содержимое таблички / краска декора (<see cref="Decoration.CopyState"/>).</summary>
    public string decor;
}

[Serializable]
public class BlueprintFile
{
    public string id;
    public string name;
    public string created;
    public BlueprintBuilding[] buildings;
}

public class BlueprintRecord
{
    public BlueprintFile file;
    public string folder;
    public string iconPath;
    public bool global;
}

public static class BlueprintLibrary
{
    public const string FileName = "blueprint.json";
    public const string IconName = "icon.png";

    public static string GlobalFolder
    {
        get
        {
            string path = Path.Combine(Application.persistentDataPath, "blueprints");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    public static string WorldFolder()
    {
        if (!WorldCatalog.HasActive)
            return null;
        string path = Path.Combine(WorldCatalog.WorldsFolder, WorldCatalog.Active.id, "blueprints");
        Directory.CreateDirectory(path);
        return path;
    }

    public static List<BlueprintRecord> List(bool global)
    {
        var result = new List<BlueprintRecord>();
        string root = global ? GlobalFolder : WorldFolder();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            return result;

        string[] dirs = Directory.GetDirectories(root);
        for (int i = 0; i < dirs.Length; i++)
        {
            BlueprintRecord rec = ReadFolder(dirs[i], global);
            if (rec != null)
                result.Add(rec);
        }

        result.Sort((a, b) => string.Compare(
            a.file != null ? a.file.name : "",
            b.file != null ? b.file.name : "",
            StringComparison.OrdinalIgnoreCase));
        return result;
    }

    public static BlueprintRecord ReadFolder(string folder, bool global)
    {
        if (string.IsNullOrEmpty(folder))
            return null;
        string jsonPath = Path.Combine(folder, FileName);
        if (!File.Exists(jsonPath))
            return null;
        try
        {
            BlueprintFile file = JsonUtility.FromJson<BlueprintFile>(File.ReadAllText(jsonPath));
            if (file == null || file.buildings == null || file.buildings.Length == 0)
                return null;
            if (string.IsNullOrEmpty(file.id))
                file.id = Path.GetFileName(folder);
            return new BlueprintRecord
            {
                file = file,
                folder = folder,
                iconPath = Path.Combine(folder, IconName),
                global = global
            };
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Blueprints] " + e.Message);
            return null;
        }
    }

    public static BlueprintRecord Save(string name, IList<BlueprintBuilding> buildings, byte[] iconPng, bool global)
    {
        if (buildings == null || buildings.Count == 0)
            return null;
        string root = global ? GlobalFolder : WorldFolder();
        if (string.IsNullOrEmpty(root))
            return null;

        string id = Guid.NewGuid().ToString("N");
        string folder = Path.Combine(root, id);
        Directory.CreateDirectory(folder);
        var file = new BlueprintFile
        {
            id = id,
            name = string.IsNullOrWhiteSpace(name) ? UiLocale.T("bp.untitled") : name.Trim(),
            created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            buildings = ToArray(buildings)
        };
        File.WriteAllText(Path.Combine(folder, FileName), JsonUtility.ToJson(file, true));
        if (iconPng != null && iconPng.Length > 0)
            File.WriteAllBytes(Path.Combine(folder, IconName), iconPng);
        return new BlueprintRecord
        {
            file = file,
            folder = folder,
            iconPath = Path.Combine(folder, IconName),
            global = global
        };
    }

    public static bool Rename(BlueprintRecord rec, string name)
    {
        if (rec == null || rec.file == null || string.IsNullOrEmpty(rec.folder))
            return false;
        rec.file.name = string.IsNullOrWhiteSpace(name) ? rec.file.name : name.Trim();
        File.WriteAllText(Path.Combine(rec.folder, FileName), JsonUtility.ToJson(rec.file, true));
        return true;
    }

    public static bool Delete(BlueprintRecord rec)
    {
        if (rec == null || string.IsNullOrEmpty(rec.folder) || !Directory.Exists(rec.folder))
            return false;
        try
        {
            Directory.Delete(rec.folder, true);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Blueprints] " + e.Message);
            return false;
        }
    }

    public static byte[] CaptureTopDown(IReadOnlyList<BuildingBase> buildings)
    {
        if (buildings == null || buildings.Count == 0)
            return null;

        Bounds bounds = default;
        bool any = false;
        for (int i = 0; i < buildings.Count; i++)
        {
            BuildingBase b = buildings[i];
            if (b == null)
                continue;
            bool hadMesh = false;
            Renderer[] rends = b.GetComponentsInChildren<Renderer>();
            for (int r = 0; r < rends.Length; r++)
            {
                if (rends[r] == null || !rends[r].enabled)
                    continue;
                hadMesh = true;
                if (!any)
                {
                    bounds = rends[r].bounds;
                    any = true;
                }
                else
                    bounds.Encapsulate(rends[r].bounds);
            }

            if (!hadMesh)
            {
                Bounds cell = new Bounds(b.transform.position, Vector3.one * GridFootprint.CellSize);
                if (!any)
                {
                    bounds = cell;
                    any = true;
                }
                else
                    bounds.Encapsulate(cell);
            }
        }

        if (!any)
            return null;

        var hide = new List<Renderer>();
        PlayerMovement player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            Renderer[] playerRends = player.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < playerRends.Length; i++)
            {
                if (playerRends[i] != null && playerRends[i].enabled)
                {
                    playerRends[i].enabled = false;
                    hide.Add(playerRends[i]);
                }
            }
        }

        int size = 256;
        var go = new GameObject("BlueprintCam");
        Camera cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.10f, 0.12f, 0.14f, 1f);
        cam.nearClipPlane = 0.2f;
        cam.farClipPlane = 120f;
        cam.cullingMask = ~(1 << 5);
        cam.enabled = false;
        cam.aspect = 1f;
        float span = Mathf.Max(bounds.size.x, bounds.size.z, GridFootprint.CellSize * 2f);
        cam.orthographicSize = span * 0.55f + 0.8f;
        Vector3 center = bounds.center;
        go.transform.position = new Vector3(center.x, bounds.max.y + 24f, center.z);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        RenderTexture rt = RenderTexture.GetTemporary(size, size, 16, RenderTextureFormat.ARGB32);
        RenderTexture prev = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply(false, false);
        cam.targetTexture = null;
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        byte[] png = tex.EncodeToPNG();
        UnityEngine.Object.Destroy(tex);
        UnityEngine.Object.Destroy(go);

        for (int i = 0; i < hide.Count; i++)
        {
            if (hide[i] != null)
                hide[i].enabled = true;
        }

        return png;
    }

    public static Sprite LoadIcon(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;
        try
        {
            byte[] bytes = File.ReadAllBytes(path);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(bytes))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }
        catch
        {
            return null;
        }
    }

    static BlueprintBuilding[] ToArray(IList<BlueprintBuilding> buildings)
    {
        var arr = new BlueprintBuilding[buildings.Count];
        for (int i = 0; i < buildings.Count; i++)
            arr[i] = buildings[i];
        return arr;
    }
}
