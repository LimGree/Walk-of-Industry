using System;
using UnityEngine;

public static class MapSettings
{
    public static event Action Changed;

    public static bool MiniRound
    {
        get => GetInt("MiniMapRound", 0) != 0;
        set => SetInt("MiniMapRound", value ? 1 : 0);
    }

    public static bool MiniFollow
    {
        get => GetInt("MiniMapFollow", 0) != 0;
        set => SetInt("MiniMapFollow", value ? 1 : 0);
    }

    public static float MiniZoom
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat("MiniMapZoom", 0.18f), 0.06f, 1f);
        set
        {
            PlayerPrefs.SetFloat("MiniMapZoom", Mathf.Clamp(value, 0.06f, 1f));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static float MiniSize
    {
        get => Mathf.Clamp(PlayerPrefs.GetFloat("MiniMapSize", 220f), 140f, 400f);
        set
        {
            PlayerPrefs.SetFloat("MiniMapSize", Mathf.Clamp(value, 140f, 400f));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static int MiniCorner
    {
        get => Mathf.Clamp(GetInt("MiniMapCorner", 1), 0, 3);
        set => SetInt("MiniMapCorner", Mathf.Clamp(value, 0, 3));
    }

    public static float MiniOpacity
    {
        get => Mathf.Clamp01(PlayerPrefs.GetFloat("MiniMapOpacity", 0.88f));
        set
        {
            PlayerPrefs.SetFloat("MiniMapOpacity", Mathf.Clamp01(value));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }

    public static bool MiniVisible
    {
        get => GetInt("MiniMapVisible", 1) != 0;
        set => SetInt("MiniMapVisible", value ? 1 : 0);
    }

    public static bool MiniCompass
    {
        get => GetInt("MiniMapCompass", 1) != 0;
        set => SetInt("MiniMapCompass", value ? 1 : 0);
    }

    public static bool MiniCoords
    {
        get => GetInt("MiniMapCoords", 1) != 0;
        set => SetInt("MiniMapCoords", value ? 1 : 0);
    }

    public static bool MiniBiome
    {
        get => GetInt("MiniMapBiome", 1) != 0;
        set => SetInt("MiniMapBiome", value ? 1 : 0);
    }

    public static bool MiniWaypoints
    {
        get => GetInt("MiniMapWaypoints", 1) != 0;
        set => SetInt("MiniMapWaypoints", value ? 1 : 0);
    }

    public static bool MiniGrid
    {
        get => GetInt("MiniMapGrid", 0) != 0;
        set => SetInt("MiniMapGrid", value ? 1 : 0);
    }

    public static bool Holograms
    {
        get => GetInt("MapHolograms", 1) != 0;
        set => SetInt("MapHolograms", value ? 1 : 0);
    }

    public static bool WorldPause
    {
        get => GetInt("WorldMapPause", 0) != 0;
        set => SetInt("WorldMapPause", value ? 1 : 0);
    }

    public static bool WorldCompass
    {
        get => GetInt("WorldMapCompass", 1) != 0;
        set => SetInt("WorldMapCompass", value ? 1 : 0);
    }

    public static bool WorldGrid
    {
        get => GetInt("WorldMapGrid", 0) != 0;
        set => SetInt("WorldMapGrid", value ? 1 : 0);
    }

    public static bool HideUnexplored
    {
        get => GetInt("MapHideUnexplored", 1) != 0;
        set => SetInt("MapHideUnexplored", value ? 1 : 0);
    }

    public static bool AllowTeleport
    {
        get => GetInt("MapAllowTeleport", 0) != 0;
        set => SetInt("MapAllowTeleport", value ? 1 : 0);
    }

    public static int RevealRadius
    {
        get => Mathf.Clamp(GetInt("MapRevealRadius", 32), 8, 72);
        set => SetInt("MapRevealRadius", Mathf.Clamp(value, 8, 72));
    }

    static int GetInt(string key, int fallback)
    {
        return PlayerPrefs.GetInt(key, fallback);
    }

    static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
