#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Проверка данных перед билдом: входы станков против рецептов, рецепты без станка/исследования,
/// цена исследования из предметов, которые нельзя получить, id с пробелами.
/// Меню: Walk of Industry → Validate Content. Вызывается из [[AlphaBuild]] (ошибки — в лог, билд не стопорит).
/// </summary>
public static class ContentValidator
{
    [MenuItem("Walk of Industry/Validate Content")]
    public static void RunMenu()
    {
        int errors = Run(out string report);
        if (errors > 0)
            Debug.LogError(report);
        else
            Debug.Log(report);
    }

    public static int Run(out string report)
    {
        var sb = new StringBuilder();
        int errors = 0;
        int warnings = 0;
        GameDatabase db = Resources.Load<GameDatabase>("GameDatabase");
        if (db == null)
        {
            report = "[Validate] GameDatabase не найден в Resources";
            return 1;
        }

        if (GameDatabase.Instance == null)
            db.Activate();
        ProductionGraph.Invalidate();

        RecipeData[] recipes = GameDatabase.AllRecipes();
        BuildingData[] buildings = GameDatabase.AllBuildings();
        ResearchNodeData[] research = GameDatabase.AllResearches();

        // 1. Входы станков: ленты и трубы отдельно.
        for (int b = 0; buildings != null && b < buildings.Length; b++)
        {
            BuildingData data = buildings[b];
            if (data == null || data.prefab == null)
                continue;
            CrafterBuilding crafter = data.prefab.GetComponent<CrafterBuilding>();
            if (crafter == null)
                continue;
            CountSockets(crafter, out int solidIn, out int fluidIn);
            int needSolid = 0;
            int needFluid = 0;
            string worst = null;
            for (int r = 0; recipes != null && r < recipes.Length; r++)
            {
                RecipeData recipe = recipes[r];
                if (recipe == null || !recipe.AllowsBuilding(data) || recipe.inputs == null)
                    continue;
                int s = 0, f = 0;
                for (int i = 0; i < recipe.inputs.Count; i++)
                {
                    if (recipe.inputs[i].item == null)
                        continue;
                    if (recipe.inputs[i].item.isFluid)
                        f++;
                    else
                        s++;
                }

                if (s > needSolid || f > needFluid)
                    worst = recipe.id;
                needSolid = Mathf.Max(needSolid, s);
                needFluid = Mathf.Max(needFluid, f);
            }

            if (needSolid > solidIn || needFluid > fluidIn)
            {
                errors++;
                sb.AppendLine($"ОШИБКА  {data.id}: рецепту «{worst}» нужно входов лента/труба {needSolid}/{needFluid}, у префаба {solidIn}/{fluidIn}");
            }
        }

        // 2. Рецепты: станок есть, исследование/старт открывает.
        var unlockedByResearch = new HashSet<RecipeData>();
        for (int i = 0; research != null && i < research.Length; i++)
        {
            if (research[i] != null && research[i].unlockedRecipes != null)
                foreach (RecipeData r in research[i].unlockedRecipes)
                    if (r != null)
                        unlockedByResearch.Add(r);
        }

        for (int r = 0; recipes != null && r < recipes.Length; r++)
        {
            RecipeData recipe = recipes[r];
            if (recipe == null)
                continue;
            if (RecipeCodex.BuildingOf(recipe) == null)
            {
                errors++;
                sb.AppendLine($"ОШИБКА  рецепт {recipe.id}: нет станка");
            }

            if (!unlockedByResearch.Contains(recipe))
            {
                warnings++;
                sb.AppendLine($"предупр. рецепт {recipe.id}: ни одно исследование его не открывает (только старт?)");
            }

            if (recipe.id != null && recipe.id != recipe.id.Trim())
            {
                warnings++;
                sb.AppendLine($"предупр. рецепт «{recipe.id}»: пробел в id (Normalize спасает, но лучше убрать с миграцией сейвов)");
            }
        }

        // 3. Цена исследований — из получаемых предметов; предметы-тупики.
        for (int i = 0; research != null && i < research.Length; i++)
        {
            ResearchNodeData node = research[i];
            if (node == null || node.requiredItems == null)
                continue;
            foreach (ItemStack stack in node.requiredItems)
            {
                ProductionGraph.Node n = ProductionGraph.Get(stack != null ? stack.item : null);
                if (n == null || (n.IsRaw && n.extractor == null))
                {
                    errors++;
                    sb.AppendLine($"ОШИБКА  {node.id}: в цене {(stack != null && stack.item != null ? stack.item.id : "null")} — его нельзя ни добыть, ни сделать");
                }
            }
        }

        foreach (ProductionGraph.Node n in ProductionGraph.All)
        {
            if (n.consumers.Count == 0 && n.researchUses.Count == 0 && n.depth < 7)
            {
                warnings++;
                sb.AppendLine($"предупр. {n.item.id}: тупик — не нужен ни в рецептах, ни в исследованиях");
            }
        }

        report = $"[Validate] ошибок {errors}, предупреждений {warnings}\n" + sb;
        return errors;
    }

    static void CountSockets(CrafterBuilding crafter, out int solid, out int fluid)
    {
        solid = 0;
        fluid = 0;
        // Сокеты, которые создаются кодом в Awake (химзавод) или меняются по рецепту (НПЗ).
        if (crafter is ChemicalPlant)
        {
            solid = 1;
            fluid = 1;
            return;
        }

        if (crafter is Refinery)
        {
            solid = 1;
            fluid = 1; // один сокет, но меняет тип по рецепту
            return;
        }

        if (crafter.inputSockets == null)
            return;
        foreach (BuildingSocket s in crafter.inputSockets)
        {
            if (s == null)
                continue;
            if (s.name.IndexOf("Fluid", System.StringComparison.OrdinalIgnoreCase) >= 0)
                fluid++;
            else
                solid++;
        }
    }
}
#endif
