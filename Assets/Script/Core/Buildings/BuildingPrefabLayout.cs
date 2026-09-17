using UnityEngine;

/// <summary>
/// Правило префаба здания до поворота игроком:
/// +Z — перед / основной выход, −Z — зад / основной вход.
/// Pivot — центр footprint.
/// </summary>
public static class BuildingPrefabLayout
{
    public const string Visual = "Visual";
    public const string Level1 = "Level1";
    public const string Level2 = "Level2";
    public const string Ghost = "Ghost";
    public const string Sockets = "Sockets";
    public const string Input = "InputSocket";
    public const string Output = "OutputSocket";
    public const string Arrow = "SocketArrow";

    static readonly string[] Level1Aliases =
    {
        "Level1", "Visual/Level1", "extractor_level_1", "Assembler_Level_1"
    };

    static readonly string[] Level2Aliases =
    {
        "Level2", "Visual/Level2", "extractor_level_2", "Assembler_level_2"
    };

    public static Quaternion OutputRotation => Quaternion.identity;
    public static Quaternion InputRotation => Quaternion.Euler(0f, 180f, 0f);

    public static void ApplyPrimarySockets(BuildingBase building)
    {
        if (building == null)
            return;

        Vector2Int size = building.data != null ? building.data.size : Vector2Int.one;
        float cell = GridFootprint.CellSize;
        float halfZ = Mathf.Max(0.2f, size.y * cell * 0.5f - 0.02f);
        float y = 0.3f;

        if (building.inputSockets != null && building.inputSockets.Length > 0 && building.inputSockets[0] != null)
            PlaceSocket(building.inputSockets[0].transform, new Vector3(0f, y, -halfZ), InputRotation);

        if (building.outputSockets != null && building.outputSockets.Length > 0 && building.outputSockets[0] != null)
            PlaceSocket(building.outputSockets[0].transform, new Vector3(0f, y, halfZ), OutputRotation);

        if (building.inputSockets != null && building.inputSockets.Length > 1 && building.inputSockets[1] != null)
        {
            float halfX = Mathf.Max(0.2f, size.x * cell * 0.5f - 0.02f);
            bool fluid = building.inputSockets[1].name.IndexOf("Fluid", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (fluid)
                PlaceSocket(building.inputSockets[1].transform, new Vector3(-halfX, y, 0f), Quaternion.Euler(0f, -90f, 0f));
            else
                PlaceSocket(building.inputSockets[1].transform, new Vector3(halfX, y, 0f), Quaternion.Euler(0f, 90f, 0f));
        }
    }

    public static void PlaceSocket(Transform socket, Vector3 localPos, Quaternion localRot)
    {
        if (socket == null)
            return;
        socket.localPosition = localPos;
        socket.localRotation = localRot;
        socket.localScale = Vector3.one;
    }

    public static Transform FindNamed(Transform root, params string[] names)
    {
        if (root == null || names == null)
            return null;
        for (int n = 0; n < names.Length; n++)
        {
            Transform found = FindByPath(root, names[n]);
            if (found != null)
                return found;
        }

        return null;
    }

    public static Transform FindLevel1(Transform root)
    {
        return FindNamed(root, Level1Aliases);
    }

    public static Transform FindLevel2(Transform root)
    {
        return FindNamed(root, Level2Aliases);
    }

    public static Transform FindGhost(Transform root)
    {
        return root != null ? FindByPath(root, Ghost) : null;
    }

    static Transform FindByPath(Transform root, string path)
    {
        if (root == null || string.IsNullOrEmpty(path))
            return null;
        Transform t = root.Find(path);
        if (t != null)
            return t;

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i] != root && all[i].name == path)
                return all[i];
        }

        return null;
    }
}
