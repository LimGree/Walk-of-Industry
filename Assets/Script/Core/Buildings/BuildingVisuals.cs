using UnityEngine;

public static class BuildingVisuals
{
    public static void ApplyPlaced(BuildingBase building, int level)
    {
        if (building == null)
            return;
        Transform root = building.transform;
        Transform ghost = BuildingPrefabLayout.FindGhost(root);
        if (ghost != null)
            ghost.gameObject.SetActive(false);

        ApplyLevel(building, level);
        SocketArrow.BindNamed(root);
    }

    public static void ApplyLevel(BuildingBase building, int level)
    {
        if (building == null)
            return;
        Transform root = building.transform;
        Transform level1 = BuildingPrefabLayout.FindLevel1(root);
        Transform level2 = BuildingPrefabLayout.FindLevel2(root);
        bool up = level >= 2 && level2 != null;
        if (level1 != null)
            level1.gameObject.SetActive(!up);
        if (level2 != null)
            level2.gameObject.SetActive(up);
    }

    public static void PrepareGhostInstance(GameObject instance)
    {
        if (instance == null)
            return;

        Collider[] cols = instance.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] != null)
                cols[i].enabled = false;
        }

        Transform root = instance.transform;
        Transform ghost = BuildingPrefabLayout.FindGhost(root);
        Transform visual = root.Find(BuildingPrefabLayout.Visual);
        Transform level1 = BuildingPrefabLayout.FindLevel1(root);
        Transform level2 = BuildingPrefabLayout.FindLevel2(root);

        if (ghost != null)
        {
            if (visual != null)
                visual.gameObject.SetActive(false);
            if (level1 != null && (visual == null || !level1.IsChildOf(visual)))
                level1.gameObject.SetActive(false);
            if (level2 != null)
                level2.gameObject.SetActive(false);
            ghost.gameObject.SetActive(true);
        }
        else
        {
            if (level2 != null)
                level2.gameObject.SetActive(false);
            if (level1 != null)
                level1.gameObject.SetActive(true);
        }

        SocketArrow.BindNamed(root);
        instance.tag = "Untagged";
        if (instance.GetComponent<GhostTint>() == null)
            instance.AddComponent<GhostTint>();
    }

    public static bool HasNestedGhost(GameObject prefab)
    {
        return prefab != null && BuildingPrefabLayout.FindGhost(prefab.transform) != null;
    }

    public static GameObject SourceForGhost(BuildingData data)
    {
        if (data == null)
            return null;
        if (data.prefab != null && (HasNestedGhost(data.prefab) || !data.IsConveyor))
            return data.prefab;
        if (data.ghostPrefab != null)
            return data.ghostPrefab;
        return data.prefab;
    }

    public static bool IsGhostRoot(Transform t)
    {
        while (t != null)
        {
            if (t.name == BuildingPrefabLayout.Ghost)
                return t.gameObject.activeInHierarchy;
            t = t.parent;
        }

        return false;
    }

    public static void SetArrowsVisible(Transform root, bool visible)
    {
        SocketArrow.BindNamed(root);
    }
}
