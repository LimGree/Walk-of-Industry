using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Граф производства для [[ProductionMapUI]]: что из чего, на каком станке, где добывается, что открывает.
/// Строится из [[GameDatabase]] (рецепты, исследования, <see cref="ItemData.extractedBy"/>) — руками не заполняется.
/// Глубина: сырьё = 0, иначе 1 + max(глубина ингредиентов) по самому раннему рецепту.
/// </summary>
public static class ProductionGraph
{
    public enum State
    {
        /// <summary>Рецепт/добыча закрыты исследованием.</summary>
        Locked,
        /// <summary>Открыто, но ещё ни разу не сделано.</summary>
        Open,
        /// <summary>Уже производится (есть в статистике).</summary>
        Made
    }

    public class Node
    {
        public ItemData item;
        public int depth;
        public BuildingData extractor;
        public readonly List<RecipeData> recipes = new List<RecipeData>(2);
        public readonly List<RecipeData> consumers = new List<RecipeData>(4);
        /// <summary>Исследования, которые просят этот предмет в цене.</summary>
        public readonly List<ResearchNodeData> researchUses = new List<ResearchNodeData>(2);

        public bool IsRaw => recipes.Count == 0;
        public RecipeData MainRecipe => recipes.Count > 0 ? recipes[0] : null;
    }

    static Dictionary<string, Node> nodes;
    static List<Node> ordered;

    public static IReadOnlyList<Node> All
    {
        get
        {
            Ensure();
            return ordered;
        }
    }

    public static Node Get(ItemData item)
    {
        if (item == null)
            return null;
        Ensure();
        nodes.TryGetValue(GameDatabase.Normalize(item.id), out Node n);
        return n;
    }

    public static int MaxDepth
    {
        get
        {
            Ensure();
            int max = 0;
            for (int i = 0; i < ordered.Count; i++)
                max = Mathf.Max(max, ordered[i].depth);
            return max;
        }
    }

    /// <summary>Сбросить кэш (после смены базы в редакторе).</summary>
    public static void Invalidate()
    {
        nodes = null;
        ordered = null;
    }

    static void Ensure()
    {
        if (nodes != null)
            return;
        nodes = new Dictionary<string, Node>();
        ordered = new List<Node>();

        ItemData[] items = GameDatabase.AllItems();
        for (int i = 0; items != null && i < items.Length; i++)
        {
            ItemData item = items[i];
            if (item == null || string.IsNullOrEmpty(item.id))
                continue;
            string id = GameDatabase.Normalize(item.id);
            if (nodes.ContainsKey(id))
                continue;
            var n = new Node { item = item, extractor = RecipeCodex.ExtractorOf(item) };
            nodes[id] = n;
            ordered.Add(n);
        }

        RecipeData[] recipes = GameDatabase.AllRecipes();
        for (int r = 0; recipes != null && r < recipes.Length; r++)
        {
            RecipeData recipe = recipes[r];
            if (recipe == null)
                continue;
            for (int o = 0; recipe.outputs != null && o < recipe.outputs.Count; o++)
            {
                Node n = Get(recipe.outputs[o].item);
                if (n != null && !n.recipes.Contains(recipe))
                    n.recipes.Add(recipe);
            }

            for (int i = 0; recipe.inputs != null && i < recipe.inputs.Count; i++)
            {
                Node n = Get(recipe.inputs[i].item);
                if (n != null && !n.consumers.Contains(recipe))
                    n.consumers.Add(recipe);
            }
        }

        ResearchNodeData[] research = GameDatabase.AllResearches();
        for (int r = 0; research != null && r < research.Length; r++)
        {
            ResearchNodeData node = research[r];
            if (node == null || node.requiredItems == null)
                continue;
            for (int i = 0; i < node.requiredItems.Count; i++)
            {
                Node n = Get(node.requiredItems[i].item);
                if (n != null && !n.researchUses.Contains(node))
                    n.researchUses.Add(node);
            }
        }

        // Самый ранний рецепт — первым (по глубине исследования, которое его открывает).
        for (int i = 0; i < ordered.Count; i++)
            ordered[i].recipes.Sort((a, b) => ResearchDepth(UnlockingResearch(a)).CompareTo(ResearchDepth(UnlockingResearch(b))));

        var visiting = new HashSet<Node>();
        var memo = new Dictionary<Node, int>();
        for (int i = 0; i < ordered.Count; i++)
            ordered[i].depth = Depth(ordered[i], visiting, memo);

        ordered.Sort((a, b) =>
        {
            int c = a.depth.CompareTo(b.depth);
            if (c != 0)
                return c;
            return ResearchDepth(FirstResearch(a)).CompareTo(ResearchDepth(FirstResearch(b)));
        });
    }

    static int Depth(Node n, HashSet<Node> visiting, Dictionary<Node, int> memo)
    {
        if (memo.TryGetValue(n, out int d))
            return d;
        if (n.IsRaw || !visiting.Add(n))
            return 0;
        int best = 0;
        RecipeData recipe = n.MainRecipe;
        for (int i = 0; recipe.inputs != null && i < recipe.inputs.Count; i++)
        {
            Node input = Get(recipe.inputs[i].item);
            if (input != null)
                best = Mathf.Max(best, Depth(input, visiting, memo) + 1);
        }

        visiting.Remove(n);
        memo[n] = best;
        return best;
    }

    static ResearchNodeData FirstResearch(Node n)
    {
        return n.MainRecipe != null ? UnlockingResearch(n.MainRecipe) : null;
    }

    // ---------- Состояние ----------

    public static State StateOf(Node n)
    {
        if (n == null)
            return State.Locked;
        if (ProductionStats.Instance != null && ProductionStats.Instance.TotalProduced(n.item) > 0)
            return State.Made;
        if (IsAvailable(n))
            return State.Open;
        return State.Locked;
    }

    public static bool IsAvailable(Node n)
    {
        if (n == null)
            return false;
        if (n.extractor != null && BuildingUnlocked(n.extractor))
            return true;
        for (int i = 0; i < n.recipes.Count; i++)
        {
            if (RecipeUnlocked(n.recipes[i]))
                return true;
        }

        return false;
    }

    public static bool RecipeUnlocked(RecipeData recipe)
    {
        if (recipe == null)
            return false;
        if (ResearchSystem.Instance == null)
            return true;
        if (!ResearchSystem.Instance.IsRecipeUnlocked(recipe))
            return false;
        BuildingData b = RecipeCodex.BuildingOf(recipe);
        return b == null || BuildingUnlocked(b);
    }

    public static bool BuildingUnlocked(BuildingData b)
    {
        return b == null || ResearchSystem.Instance == null || ResearchSystem.Instance.IsBuildingUnlocked(b);
    }

    /// <summary>Исследование, которое открывает рецепт (null — открыт со старта или узла нет).</summary>
    public static ResearchNodeData UnlockingResearch(RecipeData recipe)
    {
        if (recipe == null)
            return null;
        ResearchNodeData[] all = GameDatabase.AllResearches();
        for (int i = 0; all != null && i < all.Length; i++)
        {
            if (all[i] != null && all[i].unlockedRecipes != null && all[i].unlockedRecipes.Contains(recipe))
                return all[i];
        }

        return null;
    }

    /// <summary>Исследование, которое открывает здание.</summary>
    public static ResearchNodeData UnlockingResearch(BuildingData building)
    {
        if (building == null)
            return null;
        ResearchNodeData[] all = GameDatabase.AllResearches();
        for (int i = 0; all != null && i < all.Length; i++)
        {
            if (all[i] != null && all[i].unlockedBuildings != null && all[i].unlockedBuildings.Contains(building))
                return all[i];
        }

        return null;
    }

    /// <summary>Что мешает сделать предмет: первое закрытое исследование (рецепт или станок).</summary>
    public static ResearchNodeData Blocker(Node n)
    {
        if (n == null || IsAvailable(n))
            return null;
        if (n.extractor != null)
            return UnlockingResearch(n.extractor);
        RecipeData recipe = n.MainRecipe;
        if (recipe == null)
            return null;
        if (ResearchSystem.Instance != null && !ResearchSystem.Instance.IsRecipeUnlocked(recipe))
            return UnlockingResearch(recipe);
        return UnlockingResearch(RecipeCodex.BuildingOf(recipe));
    }

    static readonly Dictionary<ResearchNodeData, int> researchDepth = new Dictionary<ResearchNodeData, int>();

    public static int ResearchDepth(ResearchNodeData node)
    {
        if (node == null)
            return -1;
        if (researchDepth.TryGetValue(node, out int d))
            return d;
        researchDepth[node] = 0; // защита от цикла
        int best = 0;
        for (int i = 0; node.requiredResearches != null && i < node.requiredResearches.Count; i++)
            best = Mathf.Max(best, ResearchDepth(node.requiredResearches[i]) + 1);
        researchDepth[node] = best;
        return best;
    }

    // ---------- Расчёт ----------

    /// <summary>Сколько предметов в минуту делает один станок по рецепту (на базовой скорости).</summary>
    public static float PerMinute(RecipeData recipe, ItemData output)
    {
        if (recipe == null)
            return 0f;
        float need = Economy.CraftNeed(recipe);
        int amount = 0;
        for (int i = 0; recipe.outputs != null && i < recipe.outputs.Count; i++)
        {
            if (recipe.outputs[i].item == output)
                amount += recipe.outputs[i].amount;
        }

        return need > 0f ? amount * 60f / need : 0f;
    }

    /// <summary>Добыча одного экстрактора в минуту.</summary>
    public static float ExtractPerMinute(BuildingData extractor)
    {
        float interval = 1.2f;
        if (extractor != null && extractor.prefab != null)
        {
            Extractor e = extractor.prefab.GetComponent<Extractor>();
            if (e != null)
                interval = e.extractInterval;
        }

        return 60f / Mathf.Max(0.05f, interval * Economy.ExtractTimeMul);
    }

    /// <summary>Сколько предмета на входе нужно в минуту, чтобы рецепт выдавал <paramref name="ratePerMin"/> продукта.</summary>
    public static float InputPerMinute(RecipeData recipe, ItemData output, ItemStack input, float ratePerMin)
    {
        int outAmount = 0;
        for (int i = 0; recipe.outputs != null && i < recipe.outputs.Count; i++)
        {
            if (recipe.outputs[i].item == output)
                outAmount += recipe.outputs[i].amount;
        }

        return outAmount > 0 ? ratePerMin * input.amount / outAmount : 0f;
    }
}
