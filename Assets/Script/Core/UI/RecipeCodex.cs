using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Каталог «как получить предмет»: жила + крафт.
/// </summary>
public static class RecipeCodex
{
    public struct Entry
    {
        public ItemData item;
        public List<BuildingData> extractBuildings;
        public List<RecipeData> recipes;
        public int sortKey;
    }

    static readonly string[][] ExtractTable =
    {
        new[] { "iron_ore", "extractor" },
        new[] { "cooper_ore", "extractor" },
        new[] { "coal_ore", "extractor" },
        new[] { "stone", "extractor" },
        new[] { "sand", "extractor" },
        new[] { "sulfur", "extractor" },
        new[] { "log", "extractor" },
        new[] { "crude_oil", "oil_extractor" },
        new[] { "water", "water_extractor" }
    };

    public static List<Entry> Build()
    {
        var map = new Dictionary<string, Entry>();
        ItemData[] items = GameDatabase.AllItems();
        for (int i = 0; i < items.Length; i++)
        {
            ItemData item = items[i];
            if (item == null || string.IsNullOrEmpty(item.id))
                continue;
            string id = GameDatabase.Normalize(item.id);
            map[id] = new Entry
            {
                item = item,
                extractBuildings = new List<BuildingData>(2),
                recipes = new List<RecipeData>(4),
                sortKey = int.MaxValue
            };
        }

        for (int i = 0; i < ExtractTable.Length; i++)
        {
            string itemId = ExtractTable[i][0];
            BuildingData building = GameDatabase.FindBuilding(ExtractTable[i][1]);
            if (!map.TryGetValue(itemId, out Entry entry) || building == null)
                continue;
            if (!BuildingUnlocked(building))
                continue;
            if (!entry.extractBuildings.Contains(building))
                entry.extractBuildings.Add(building);
            map[itemId] = entry;
        }

        RecipeData[] recipes = GameDatabase.AllRecipes();
        for (int i = 0; i < recipes.Length; i++)
        {
            RecipeData recipe = recipes[i];
            if (recipe == null || recipe.outputs == null)
                continue;
            if (!RecipeUnlocked(recipe))
                continue;
            for (int o = 0; o < recipe.outputs.Count; o++)
            {
                ItemStack stack = recipe.outputs[o];
                if (stack == null || stack.item == null || string.IsNullOrEmpty(stack.item.id))
                    continue;
                string id = GameDatabase.Normalize(stack.item.id);
                if (!map.TryGetValue(id, out Entry entry))
                {
                    entry = new Entry
                    {
                        item = stack.item,
                        extractBuildings = new List<BuildingData>(1),
                        recipes = new List<RecipeData>(4),
                        sortKey = int.MaxValue
                    };
                }

                if (!entry.recipes.Contains(recipe))
                    entry.recipes.Add(recipe);
                map[id] = entry;
            }
        }

        Dictionary<string, int> order = UnlockOrder();
        var list = new List<Entry>(map.Count);
        foreach (KeyValuePair<string, Entry> pair in map)
        {
            Entry entry = pair.Value;
            if ((entry.extractBuildings == null || entry.extractBuildings.Count == 0)
                && (entry.recipes == null || entry.recipes.Count == 0))
                continue;
            if (order.TryGetValue(pair.Key, out int key))
                entry.sortKey = key;
            if (entry.recipes != null && entry.recipes.Count > 1)
                entry.recipes.Sort(CompareRecipeUnlock);
            list.Add(entry);
        }

        list.Sort(CompareUnlock);
        return list;
    }

    public static bool Matches(Entry entry, string query)
    {
        if (string.IsNullOrEmpty(query))
            return true;
        if (entry.item != null
            && (Contains(entry.item.displayName, query)
                || Contains(entry.item.id, query)
                || Contains(entry.item.description, query)))
            return true;

        if (entry.extractBuildings != null)
        {
            for (int i = 0; i < entry.extractBuildings.Count; i++)
            {
                BuildingData building = entry.extractBuildings[i];
                if (building != null && (Contains(building.displayName, query) || Contains(building.id, query)))
                    return true;
            }
        }

        if (entry.recipes != null)
        {
            for (int i = 0; i < entry.recipes.Count; i++)
            {
                RecipeData recipe = entry.recipes[i];
                if (recipe == null)
                    continue;
                if (Contains(recipe.displayName, query) || Contains(recipe.id, query))
                    return true;
                if (ContainsStacks(recipe.inputs, query) || ContainsStacks(recipe.outputs, query))
                    return true;
                BuildingData building = BuildingOf(recipe);
                if (building != null && (Contains(building.displayName, query) || Contains(building.id, query)))
                    return true;
            }
        }

        return false;
    }

    public static BuildingData BuildingOf(RecipeData recipe)
    {
        if (recipe == null)
            return null;
        if (recipe.requiredBuilding != null)
            return recipe.requiredBuilding;
        if (recipe.allowedBuildingIds == null)
            return null;
        for (int i = 0; i < recipe.allowedBuildingIds.Count; i++)
        {
            BuildingData building = GameDatabase.FindBuilding(recipe.allowedBuildingIds[i]);
            if (building != null)
                return building;
        }

        return null;
    }

    public static List<BuildingData> BuildingsOf(RecipeData recipe)
    {
        var list = new List<BuildingData>(4);
        if (recipe == null)
            return list;
        if (recipe.HasAllowedIds())
        {
            for (int i = 0; i < recipe.allowedBuildingIds.Count; i++)
            {
                BuildingData building = GameDatabase.FindBuilding(recipe.allowedBuildingIds[i]);
                if (building != null && !list.Contains(building))
                    list.Add(building);
            }
        }

        if (list.Count == 0 && recipe.requiredBuilding != null)
            list.Add(recipe.requiredBuilding);
        return list;
    }

    public static ResearchNodeData UnlockForRecipe(RecipeData recipe)
    {
        if (recipe == null)
            return null;
        ResearchNodeData[] nodes = GameDatabase.AllResearches();
        string recipeId = GameDatabase.Normalize(recipe.id);
        for (int i = 0; i < nodes.Length; i++)
        {
            ResearchNodeData node = nodes[i];
            if (node == null || node.unlockedRecipes == null)
                continue;
            for (int r = 0; r < node.unlockedRecipes.Count; r++)
            {
                RecipeData unlocked = node.unlockedRecipes[r];
                if (unlocked == recipe)
                    return node;
                if (unlocked != null && GameDatabase.Normalize(unlocked.id) == recipeId)
                    return node;
            }
        }

        return null;
    }

    public static ResearchNodeData UnlockForBuilding(BuildingData building)
    {
        if (building == null)
            return null;
        ResearchNodeData[] nodes = GameDatabase.AllResearches();
        string buildingId = GameDatabase.Normalize(building.id);
        for (int i = 0; i < nodes.Length; i++)
        {
            ResearchNodeData node = nodes[i];
            if (node == null || node.unlockedBuildings == null)
                continue;
            for (int b = 0; b < node.unlockedBuildings.Count; b++)
            {
                BuildingData unlocked = node.unlockedBuildings[b];
                if (unlocked == building)
                    return node;
                if (unlocked != null && GameDatabase.Normalize(unlocked.id) == buildingId)
                    return node;
            }
        }

        return null;
    }

    static int CompareUnlock(Entry a, Entry b)
    {
        int cmp = a.sortKey.CompareTo(b.sortKey);
        if (cmp != 0)
            return cmp;
        string left = a.item != null ? a.item.displayName : "";
        string right = b.item != null ? b.item.displayName : "";
        return string.Compare(left, right, System.StringComparison.CurrentCultureIgnoreCase);
    }

    static int CompareRecipeUnlock(RecipeData a, RecipeData b)
    {
        return RecipeRank(a).CompareTo(RecipeRank(b));
    }

    static bool BuildingUnlocked(BuildingData building)
    {
        if (building == null)
            return false;
        ResearchSystem research = ResearchSystem.Instance;
        if (research == null || research.IsBuildingUnlocked(building))
            return true;
        string id = GameDatabase.Normalize(building.id);
        if (ContainsBuilding(research.startingBuildings, id))
            return true;
        List<ResearchNodeData> nodes = research.GetAllNodes();
        for (int i = 0; i < nodes.Count; i++)
        {
            ResearchNodeData node = nodes[i];
            if (node == null || !research.IsResearchUnlocked(node))
                continue;
            if (ContainsBuilding(node.unlockedBuildings, id))
                return true;
        }

        return false;
    }

    static bool RecipeUnlocked(RecipeData recipe)
    {
        if (recipe == null)
            return false;
        ResearchSystem research = ResearchSystem.Instance;
        if (research == null || research.IsRecipeUnlocked(recipe))
            return true;
        string id = GameDatabase.Normalize(recipe.id);
        if (ContainsRecipe(research.startingRecipes, id))
            return true;
        List<ResearchNodeData> nodes = research.GetAllNodes();
        for (int i = 0; i < nodes.Count; i++)
        {
            ResearchNodeData node = nodes[i];
            if (node == null || !research.IsResearchUnlocked(node))
                continue;
            if (ContainsRecipe(node.unlockedRecipes, id))
                return true;
        }

        return false;
    }

    static bool ContainsBuilding(IList<BuildingData> list, string id)
    {
        if (list == null || string.IsNullOrEmpty(id))
            return false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && GameDatabase.Normalize(list[i].id) == id)
                return true;
        }

        return false;
    }

    static bool ContainsRecipe(IList<RecipeData> list, string id)
    {
        if (list == null || string.IsNullOrEmpty(id))
            return false;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null && GameDatabase.Normalize(list[i].id) == id)
                return true;
        }

        return false;
    }

    static Dictionary<string, int> UnlockOrder()
    {
        var order = new Dictionary<string, int>();
        int next = 0;

        void TouchItem(ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
                return;
            string id = GameDatabase.Normalize(item.id);
            if (!order.ContainsKey(id))
                order[id] = next++;
        }

        void TouchRecipe(RecipeData recipe)
        {
            if (recipe == null || recipe.outputs == null)
                return;
            for (int o = 0; o < recipe.outputs.Count; o++)
            {
                ItemStack stack = recipe.outputs[o];
                if (stack != null)
                    TouchItem(stack.item);
            }
        }

        for (int i = 0; i < ExtractTable.Length; i++)
        {
            BuildingData building = GameDatabase.FindBuilding(ExtractTable[i][1]);
            if (!BuildingUnlocked(building))
                continue;
            TouchItem(GameDatabase.FindItem(ExtractTable[i][0]));
        }

        ResearchSystem research = ResearchSystem.Instance;
        if (research != null && research.startingRecipes != null)
        {
            for (int i = 0; i < research.startingRecipes.Count; i++)
                TouchRecipe(research.startingRecipes[i]);
        }

        List<ResearchNodeData> nodes = NodesInTreeOrder();
        for (int i = 0; i < nodes.Count; i++)
        {
            ResearchNodeData node = nodes[i];
            if (node == null)
                continue;
            if (research != null && !research.IsResearchUnlocked(node))
                continue;
            if (node.unlockedRecipes != null)
            {
                for (int r = 0; r < node.unlockedRecipes.Count; r++)
                    TouchRecipe(node.unlockedRecipes[r]);
            }

            if (node.unlockedBuildings != null)
            {
                for (int b = 0; b < node.unlockedBuildings.Count; b++)
                {
                    BuildingData building = node.unlockedBuildings[b];
                    if (building == null)
                        continue;
                    string buildingId = GameDatabase.Normalize(building.id);
                    for (int e = 0; e < ExtractTable.Length; e++)
                    {
                        if (ExtractTable[e][1] != buildingId)
                            continue;
                        TouchItem(GameDatabase.FindItem(ExtractTable[e][0]));
                    }
                }
            }
        }

        return order;
    }

    static int RecipeRank(RecipeData recipe)
    {
        if (recipe == null)
            return int.MaxValue;
        ResearchSystem research = ResearchSystem.Instance;
        if (research != null && research.startingRecipes != null)
        {
            int start = research.startingRecipes.IndexOf(recipe);
            if (start >= 0)
                return start;
        }

        List<ResearchNodeData> nodes = NodesInTreeOrder();
        for (int i = 0; i < nodes.Count; i++)
        {
            ResearchNodeData node = nodes[i];
            if (node == null || node.unlockedRecipes == null)
                continue;
            for (int r = 0; r < node.unlockedRecipes.Count; r++)
            {
                if (node.unlockedRecipes[r] == recipe)
                    return 100 + i * 20 + r;
            }
        }

        return 10000;
    }

    static List<ResearchNodeData> NodesInTreeOrder()
    {
        List<ResearchNodeData> nodes = ResearchSystem.Instance != null
            ? ResearchSystem.Instance.GetAllNodes()
            : new List<ResearchNodeData>(GameDatabase.AllResearches());
        var depths = new Dictionary<ResearchNodeData, int>();
        var index = new Dictionary<ResearchNodeData, int>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null && !index.ContainsKey(nodes[i]))
                index[nodes[i]] = i;
        }

        var ordered = new List<ResearchNodeData>(nodes);
        ordered.Sort((a, b) =>
        {
            int da = Depth(a, depths, 0);
            int db = Depth(b, depths, 0);
            if (da != db)
                return da.CompareTo(db);
            int ia = a != null && index.TryGetValue(a, out int xa) ? xa : 999;
            int ib = b != null && index.TryGetValue(b, out int xb) ? xb : 999;
            return ia.CompareTo(ib);
        });
        return ordered;
    }

    static int Depth(ResearchNodeData node, Dictionary<ResearchNodeData, int> memo, int guard)
    {
        if (node == null)
            return 0;
        if (memo.TryGetValue(node, out int cached))
            return cached;
        if (guard > 16)
            return 0;
        int depth = 0;
        if (node.requiredResearches != null)
        {
            for (int i = 0; i < node.requiredResearches.Count; i++)
            {
                int d = Depth(node.requiredResearches[i], memo, guard + 1) + 1;
                if (d > depth)
                    depth = d;
            }
        }

        memo[node] = depth;
        return depth;
    }

    static bool ContainsStacks(IList<ItemStack> stacks, string query)
    {
        if (stacks == null)
            return false;
        for (int i = 0; i < stacks.Count; i++)
        {
            ItemStack stack = stacks[i];
            if (stack == null || stack.item == null)
                continue;
            if (Contains(stack.item.displayName, query) || Contains(stack.item.id, query))
                return true;
        }

        return false;
    }

    static bool Contains(string text, string query)
    {
        return !string.IsNullOrEmpty(text)
            && text.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
