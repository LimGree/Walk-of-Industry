using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Recipe", menuName = "Builderment/Recipe Data")]
public class RecipeData : ScriptableObject
{
    [Header("Basic Info")]
    public string id;
    public string displayName;

    [Header("Recipe")]
    public List<ItemStack> inputs = new List<ItemStack>();
    public List<ItemStack> outputs = new List<ItemStack>();

    [Header("Settings")]
    public float craftTime = 2f;
    public BuildingData requiredBuilding;
    public List<string> allowedBuildingIds = new List<string>();

    public bool AllowsBuilding(BuildingData building)
    {
        if (building == null)
            return false;

        string buildingId = GameDatabase.Normalize(building.id);
        if (HasAllowedIds())
            return ContainsId(buildingId);

        if (requiredBuilding != null)
            return GameDatabase.Normalize(requiredBuilding.id) == buildingId;

        return true;
    }

    public bool HasAllowedIds()
    {
        if (allowedBuildingIds == null)
            return false;
        for (int i = 0; i < allowedBuildingIds.Count; i++)
        {
            if (!string.IsNullOrEmpty(allowedBuildingIds[i]))
                return true;
        }
        return false;
    }

    bool ContainsId(string buildingId)
    {
        for (int i = 0; i < allowedBuildingIds.Count; i++)
        {
            if (GameDatabase.Normalize(allowedBuildingIds[i]) == buildingId)
                return true;
        }
        return false;
    }

    void OnValidate()
    {
        if (HasAllowedIds())
            return;
        if (requiredBuilding == null || string.IsNullOrEmpty(requiredBuilding.id))
            return;
        if (allowedBuildingIds == null)
            allowedBuildingIds = new List<string>();
        allowedBuildingIds.Add(GameDatabase.Normalize(requiredBuilding.id));
    }
}