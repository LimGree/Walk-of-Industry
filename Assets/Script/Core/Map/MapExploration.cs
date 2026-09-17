using System;
using System.Collections.Generic;
using UnityEngine;

public class MapExploration : MonoBehaviour
{
    public static MapExploration Instance { get; private set; }

    public int Version { get; private set; }
    public Texture2D FogTexture { get; private set; }

    byte[] mask;
    Color32[] fogPixels;
    int width;
    int height;
    int minX;
    int minZ;
    bool dirty;
    Vector2Int lastCell = new Vector2Int(int.MinValue, int.MinValue);
    float nextBuildingReveal;
    Transform player;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        MapSettings.Changed += OnSettings;
    }

    void OnDestroy()
    {
        MapSettings.Changed -= OnSettings;
        if (Instance == this)
            Instance = null;
        if (FogTexture != null)
            Destroy(FogTexture);
    }

    void OnSettings()
    {
        lastCell = new Vector2Int(int.MinValue, int.MinValue);
        dirty = true;
        Version++;
    }

    void LateUpdate()
    {
        Tick();
    }

    public void ResetToNewWorld()
    {
        mask = null;
        lastCell = new Vector2Int(int.MinValue, int.MinValue);
        dirty = true;
        Ensure();
        Version++;
    }

    public bool IsExplored(Vector2Int cell)
    {
        if (!MapSettings.HideUnexplored)
            return true;
        Ensure();
        if (mask == null)
            return true;
        int i = Index(cell);
        return i >= 0 && mask[i] > 8;
    }

    public void Tick()
    {
        Ensure();
        if (mask == null)
            return;

        if (!WorldView.HasPlayer)
            return;
        if (player == null)
            player = GameManager.Instance != null && GameManager.Instance.playerBuilder != null
                ? GameManager.Instance.playerBuilder.transform
                : null;
        if (player != null)
        {
            Vector2Int cell = WorldToCell(WorldView.PlayerPos);
            if (cell != lastCell)
            {
                lastCell = cell;
                RevealAround(cell, MapSettings.RevealRadius);
            }
        }

        if (Time.unscaledTime >= nextBuildingReveal)
        {
            nextBuildingReveal = Time.unscaledTime + 1.2f;
            RevealBuildings();
        }

        if (dirty)
            ApplyFog();
    }

    public void CaptureSave(SaveData save)
    {
        if (save == null)
            return;
        Ensure();
        save.exploreWidth = width;
        save.exploreHeight = height;
        save.exploredBits = Pack();
    }

    public void ApplySave(SaveData save)
    {
        Ensure();
        lastCell = new Vector2Int(int.MinValue, int.MinValue);
        if (save == null || save.version < 5 || string.IsNullOrEmpty(save.exploredBits))
        {
            if (save != null && save.version < 5)
                Fill(255);
            else
                Fill(0);
            dirty = true;
            ApplyFog();
            return;
        }

        Unpack(save.exploredBits, save.exploreWidth, save.exploreHeight);
        dirty = true;
        ApplyFog();
    }

    void Ensure()
    {
        WorldBiomeMap map = WorldBiomeMap.Instance;
        if (map == null || !map.IsReady)
            return;
        if (mask != null && width == map.MapWidth && height == map.MapHeight
            && minX == map.MapMinX && minZ == map.MapMinZ)
            return;

        width = map.MapWidth;
        height = map.MapHeight;
        minX = map.MapMinX;
        minZ = map.MapMinZ;
        mask = new byte[width * height];
        fogPixels = new Color32[width * height];
        if (FogTexture != null)
            Destroy(FogTexture);
        FogTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        FogTexture.filterMode = FilterMode.Bilinear;
        FogTexture.wrapMode = TextureWrapMode.Clamp;
        Fill(0);
        dirty = true;
    }

    void Fill(byte value)
    {
        if (mask == null)
            return;
        for (int i = 0; i < mask.Length; i++)
            mask[i] = value;
        dirty = true;
    }

    void RevealAround(Vector2Int cell, int radius)
    {
        int r = Mathf.Clamp(radius, 4, 80);
        int soft = Mathf.Max(3, r / 4);
        int x0 = Mathf.Max(minX, cell.x - r);
        int x1 = Mathf.Min(minX + width - 1, cell.x + r);
        int z0 = Mathf.Max(minZ, cell.y - r);
        int z1 = Mathf.Min(minZ + height - 1, cell.y + r);
        float rf = r;
        for (int z = z0; z <= z1; z++)
        {
            for (int x = x0; x <= x1; x++)
            {
                float d = Vector2.Distance(new Vector2(x, z), new Vector2(cell.x, cell.y));
                if (d > rf)
                    continue;
                int v = d <= r - soft ? 255 : Mathf.RoundToInt(255f * (rf - d) / soft);
                int i = (z - minZ) * width + (x - minX);
                if (v > mask[i])
                {
                    mask[i] = (byte)v;
                    dirty = true;
                }
            }
        }
    }

    void RevealBuildings()
    {
        var objects = new List<GameObject>(64);
        GridOccupancy.CollectAllOccupiedObjects(objects, new HashSet<int>());
        var cells = new List<Vector2Int>(8);
        for (int i = 0; i < objects.Count; i++)
        {
            if (objects[i] == null || !GridOccupancy.TryGetCells(objects[i], cells))
                continue;
            for (int c = 0; c < cells.Count; c++)
                RevealCell(cells[c], 255);
        }
    }

    void RevealCell(Vector2Int cell, byte value)
    {
        int i = Index(cell);
        if (i < 0)
            return;
        if (value > mask[i])
        {
            mask[i] = value;
            dirty = true;
        }
    }

    int Index(Vector2Int cell)
    {
        int x = cell.x - minX;
        int z = cell.y - minZ;
        if (x < 0 || z < 0 || x >= width || z >= height)
            return -1;
        return z * width + x;
    }

    void ApplyFog()
    {
        if (FogTexture == null || fogPixels == null || mask == null)
            return;
        Color32 hidden = new Color32(6, 9, 14, 240);
        Color32 clear = new Color32(0, 0, 0, 0);
        for (int i = 0; i < mask.Length; i++)
        {
            byte vis = mask[i];
            if (vis >= 250)
                fogPixels[i] = clear;
            else if (vis == 0)
                fogPixels[i] = hidden;
            else
            {
                byte a = (byte)Mathf.RoundToInt((1f - vis / 255f) * 240f);
                fogPixels[i] = new Color32(6, 9, 14, a);
            }
        }
        FogTexture.SetPixels32(fogPixels);
        FogTexture.Apply(false, false);
        dirty = false;
        Version++;
    }

    string Pack()
    {
        if (mask == null || mask.Length == 0)
            return "";
        int n = (mask.Length + 7) / 8;
        var bits = new byte[n];
        for (int i = 0; i < mask.Length; i++)
        {
            if (mask[i] > 0)
                bits[i >> 3] |= (byte)(1 << (i & 7));
        }
        return Convert.ToBase64String(bits);
    }

    void Unpack(string data, int savedW, int savedH)
    {
        Fill(0);
        if (string.IsNullOrEmpty(data) || savedW <= 0 || savedH <= 0)
            return;
        byte[] bits;
        try
        {
            bits = Convert.FromBase64String(data);
        }
        catch
        {
            return;
        }

        int copyW = Mathf.Min(width, savedW);
        int copyH = Mathf.Min(height, savedH);
        for (int z = 0; z < copyH; z++)
        {
            for (int x = 0; x < copyW; x++)
            {
                int src = z * savedW + x;
                if (src < 0 || src >= savedW * savedH)
                    continue;
                int byteI = src >> 3;
                if (byteI < 0 || byteI >= bits.Length)
                    continue;
                if ((bits[byteI] & (1 << (src & 7))) == 0)
                    continue;
                mask[z * width + x] = 255;
            }
        }
    }

    static Vector2Int WorldToCell(Vector3 world)
    {
        if (GridSystem.Instance != null)
            return GridSystem.Instance.WorldToCell(world);
        return new Vector2Int(Mathf.RoundToInt(world.x), Mathf.RoundToInt(world.z));
    }
}
