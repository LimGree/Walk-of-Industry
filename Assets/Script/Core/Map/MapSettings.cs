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

    /// <summary>Угол 0..3; установка угла сбрасывает свободную позицию.</summary>
    public static int MiniCorner
    {
        get => Mathf.Clamp(GetInt("MiniMapCorner", 1), 0, 3);
        set
        {
            PlayerPrefs.SetInt("MiniMapCustom", 0);
            SetInt("MiniMapCorner", Mathf.Clamp(value, 0, 3));
        }
    }

    /// <summary>Миникарта стоит в свободной точке (перетащили мышкой), а не в углу.</summary>
    public static bool MiniCustom => GetInt("MiniMapCustom", 0) != 0;

    /// <summary>Свободная позиция: 0..1 по свободному месту экрана (0 — левый/верхний край).</summary>
    public static Vector2 MiniPos => new Vector2(
        Mathf.Clamp01(PlayerPrefs.GetFloat("MiniMapPosX", 1f)),
        Mathf.Clamp01(PlayerPrefs.GetFloat("MiniMapPosY", 0f)));

    public static void SetMiniPos(Vector2 pos)
    {
        PlayerPrefs.SetFloat("MiniMapPosX", Mathf.Clamp01(pos.x));
        PlayerPrefs.SetFloat("MiniMapPosY", Mathf.Clamp01(pos.y));
        SetInt("MiniMapCustom", 1);
    }

    /// <summary>Вернуть сохранённое состояние позиции (для «Отмена» в режиме перетаскивания).</summary>
    public static void RestoreMiniPos(bool custom, Vector2 pos)
    {
        PlayerPrefs.SetFloat("MiniMapPosX", Mathf.Clamp01(pos.x));
        PlayerPrefs.SetFloat("MiniMapPosY", Mathf.Clamp01(pos.y));
        SetInt("MiniMapCustom", custom ? 1 : 0);
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

    /// <summary>Здания на карте и миникарте (слой «Здания» в легенде).</summary>
    public static bool ShowBuildings
    {
        get => GetInt("MapShowBuildings", 1) != 0;
        set => SetInt("MapShowBuildings", value ? 1 : 0);
    }

    /// <summary>Жилы ресурсов на карте (слой «Жилы» в легенде).</summary>
    public static bool ShowVeins
    {
        get => GetInt("MapShowVeins", 1) != 0;
        set => SetInt("MapShowVeins", value ? 1 : 0);
    }

    public static bool ShowDrones
    {
        get => GetInt("MapShowDrones", 1) != 0;
        set => SetInt("MapShowDrones", value ? 1 : 0);
    }

    /// <summary>Скрытые типы жил (подписи через «|»).</summary>
    public static bool IsVeinHidden(string label)
    {
        if (string.IsNullOrEmpty(label))
            return false;
        string all = PlayerPrefs.GetString("MapHiddenVeins", "");
        return ("|" + all + "|").Contains("|" + label + "|");
    }

    public static void SetVeinHidden(string label, bool hidden)
    {
        if (string.IsNullOrEmpty(label))
            return;
        var set = new System.Collections.Generic.List<string>(PlayerPrefs.GetString("MapHiddenVeins", "").Split('|'));
        set.RemoveAll(string.IsNullOrEmpty);
        set.Remove(label);
        if (hidden)
            set.Add(label);
        PlayerPrefs.SetString("MapHiddenVeins", string.Join("|", set));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    /// <summary>Масштаб полной карты при открытии: 0 — как в прошлый раз, 1 — около игрока, 2 — вся карта.</summary>
    public static int FullOpenZoom
    {
        get => Mathf.Clamp(GetInt("MapOpenZoom", 1), 0, 2);
        set => SetInt("MapOpenZoom", Mathf.Clamp(value, 0, 2));
    }

    /// <summary>Цвет новых меток: -1 — по очереди, иначе индекс MapMarkerSystem.Palette.</summary>
    public static int WaypointColor
    {
        get => Mathf.Clamp(GetInt("MapWaypointColor", -1), -1, 5);
        set => SetInt("MapWaypointColor", Mathf.Clamp(value, -1, 5));
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
