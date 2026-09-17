using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "GameDatabase", menuName = "Builderment/Game Database")]
public class GameDatabase : ScriptableObject
{
    public static GameDatabase Instance { get; private set; }

    [Header("Catalog")]
    public BuildingData[] buildings;
    public ItemData[] items;
    public RecipeData[] recipes;
    public ResearchNodeData[] researches;

    readonly Dictionary<string, BuildingData> buildingsById = new Dictionary<string, BuildingData>();
    readonly Dictionary<string, ItemData> itemsById = new Dictionary<string, ItemData>();
    readonly Dictionary<string, RecipeData> recipesById = new Dictionary<string, RecipeData>();
    readonly Dictionary<string, ResearchNodeData> researchesById = new Dictionary<string, ResearchNodeData>();
    bool indexed;

    public IReadOnlyList<BuildingData> Buildings => buildings;
    public IReadOnlyList<ItemData> Items => items;
    public IReadOnlyList<RecipeData> Recipes => recipes;
    public IReadOnlyList<ResearchNodeData> Researches => researches;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        GameDatabase db = Resources.Load<GameDatabase>("GameDatabase");
        if (db == null)
        {
            Debug.LogError("[GameDatabase] Нет Assets/Resources/GameDatabase.asset");
            return;
        }

        db.Activate();
    }

    public void Activate()
    {
        Instance = this;
        RebuildIndex();
    }

    public void RebuildIndex()
    {
        buildingsById.Clear();
        itemsById.Clear();
        recipesById.Clear();
        researchesById.Clear();
        Index(buildings, buildingsById);
        Index(items, itemsById);
        Index(recipes, recipesById);
        Index(researches, researchesById);
        indexed = true;
    }

    public BuildingData GetBuilding(string id) => Get(buildingsById, id);
    public ItemData GetItem(string id) => Get(itemsById, id);
    public RecipeData GetRecipe(string id) => Get(recipesById, id);
    public ResearchNodeData GetResearch(string id) => Get(researchesById, id);

    public static BuildingData FindBuilding(string id)
    {
        return Instance != null ? Instance.GetBuilding(id) : null;
    }

    public static ItemData FindItem(string id)
    {
        return Instance != null ? Instance.GetItem(id) : null;
    }

    public static RecipeData FindRecipe(string id)
    {
        return Instance != null ? Instance.GetRecipe(id) : null;
    }

    public static ResearchNodeData FindResearch(string id)
    {
        return Instance != null ? Instance.GetResearch(id) : null;
    }

    public static BuildingData[] AllBuildings()
    {
        return Instance != null && Instance.buildings != null ? Instance.buildings : System.Array.Empty<BuildingData>();
    }

    public static ItemData[] AllItems()
    {
        return Instance != null && Instance.items != null ? Instance.items : System.Array.Empty<ItemData>();
    }

    public static RecipeData[] AllRecipes()
    {
        return Instance != null && Instance.recipes != null ? Instance.recipes : System.Array.Empty<RecipeData>();
    }

    public static ResearchNodeData[] AllResearches()
    {
        return Instance != null && Instance.researches != null ? Instance.researches : System.Array.Empty<ResearchNodeData>();
    }

    static void Index<T>(T[] source, Dictionary<string, T> map) where T : Object
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Length; i++)
        {
            T entry = source[i];
            if (entry == null)
                continue;

            string id = ReadId(entry);
            if (string.IsNullOrEmpty(id))
                continue;

            if (map.ContainsKey(id))
            {
                Debug.LogWarning("[GameDatabase] Дубль id: " + id + " (" + entry.name + ")");
                continue;
            }

            map.Add(id, entry);
        }
    }

    static T Get<T>(Dictionary<string, T> map, string id) where T : class
    {
        if (map == null || string.IsNullOrEmpty(id))
            return null;
        map.TryGetValue(Normalize(id), out T value);
        return value;
    }

    static string ReadId(Object entry)
    {
        if (entry is BuildingData building)
            return Normalize(building.id);
        if (entry is ItemData item)
            return Normalize(item.id);
        if (entry is RecipeData recipe)
            return Normalize(recipe.id);
        if (entry is ResearchNodeData research)
            return Normalize(research.id);
        return "";
    }

    public static string Normalize(string id)
    {
        return string.IsNullOrEmpty(id) ? "" : id.Trim();
    }

    void OnEnable()
    {
        if (!indexed)
            RebuildIndex();
        if (Instance == null)
            Instance = this;
    }
}
