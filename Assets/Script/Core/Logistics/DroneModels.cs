using UnityEngine;

/// <summary>
/// Модели дронов и станций из [[ModelLibrary]] (`Resources/Models`).
/// Точки площадок совпадают с генератором моделей (generated/wi_models.py).
/// </summary>
public static class DroneModels
{
    public const string Drone = "drone";
    public const string LoadStation = "drone_load_station";
    public const string UnloadStation = "drone_unload_station";

    /// <summary>Верх площадки в метрах (как в генераторе моделей).</summary>
    public const float PadTop = 0.62f;

    public static readonly Vector3[] LoadPads =
    {
        new Vector3(-1.2f, PadTop, 0.3f), new Vector3(1.2f, PadTop, 0.3f),
        new Vector3(-1.2f, PadTop, 1.75f), new Vector3(1.2f, PadTop, 1.75f)
    };

    public static readonly Vector3[] UnloadPads =
    {
        new Vector3(-1.2f, PadTop, -1.75f), new Vector3(1.2f, PadTop, -1.75f),
        new Vector3(-1.2f, PadTop, -0.3f), new Vector3(1.2f, PadTop, -0.3f)
    };

    /// <summary>Создать child "Visual" с моделью. width — ожидаемая ширина по X в метрах.</summary>
    public static Transform Attach(Transform parent, string model, float width)
    {
        if (parent == null)
            return null;
        Transform existing = parent.Find(BuildingPrefabLayout.Visual);
        if (existing != null)
            return existing;

        Transform t = ModelLibrary.AttachAligned(model, parent, parent, width, BuildingPrefabLayout.Visual);
        if (t != null)
        {
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return t;
        }

        Debug.LogWarning("[Drones] нет модели Resources/" + ModelLibrary.Folder + model);
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(go.GetComponent<Collider>());
        go.name = BuildingPrefabLayout.Visual;
        go.transform.SetParent(parent, false);
        go.transform.localScale = new Vector3(width, width * 0.3f, width);
        go.transform.localPosition = new Vector3(0f, width * 0.15f, 0f);
        return go.transform;
    }

    public static Transform FindChild(Transform root, string name)
    {
        return ModelLibrary.FindChild(root, name);
    }
}
