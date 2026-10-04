using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Производство: скорость станков, состояние станка, простой, рецепты, поток предметов.</summary>
public static partial class DevCommands
{
    [DevCommand("craft", "speed", "текущие множители")]
    [DevCommand("craft", "speed reset", "все множители обратно в 1")]
    [DevCommand("craft", "speed <x> [here]", "множитель скорости всех станков и добычи; here — только под прицелом")]
    [DevCommand("craft", "speed <x> radius <r>", "множитель для зданий в радиусе r клеток")]
    static string CraftSpeedCmd(DevArgs a)
    {
        if (a[0] != "speed")
            return UsageOf(a);
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        if (!a.Has(1))
        {
            int local = 0;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && !Mathf.Approximately(all[i].DevWork, 1f))
                    local++;
            }

            return "craft speed ×" + F(BuildingBase.DevGlobalWork) + (local > 0 ? ", своих множителей у " + local + " зданий" : "");
        }

        if (a[1] == "reset")
        {
            BuildingBase.DevGlobalWork = 1f;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null)
                    all[i].DevWork = 1f;
            }

            return Ok("craft speed ×1, свои множители сброшены");
        }

        if (!a.TryFloat(1, out float mul) || mul < 0f)
            return UsageOf(a);
        mul = Mathf.Clamp(mul, 0f, 100f);

        if (a[2] == "here")
        {
            BuildingBase b = AimedBuilding();
            if (b == null)
                return Err("под прицелом нет здания");
            b.DevWork = mul;
            return Ok(Name(b) + ": ×" + F(mul) + " (итого ×" + F(b.DevWorkMul) + ")");
        }

        if (a[2] == "radius")
        {
            if (!a.TryFloat(3, out float r) || r <= 0f)
                return UsageOf(a);
            var list = new List<BuildingBase>(64);
            CollectNear(r, list);
            for (int i = 0; i < list.Count; i++)
                list[i].DevWork = mul;
            return Ok("×" + F(mul) + " у " + list.Count + " зданий в радиусе " + F(r));
        }

        if (a.Has(2))
            return UsageOf(a);
        BuildingBase.DevGlobalWork = mul;
        return Ok("craft speed ×" + F(mul) + " для всех станков и добычи");
    }

    [DevCommand("machine", "info", "станок под прицелом: рецепт, прогресс, буферы, питание, простой")]
    [DevCommand("machine", "finish", "завершить текущий цикл станка под прицелом")]
    static string Machine(DevArgs a)
    {
        if (!a.Is(0, "info", "finish"))
            return UsageOf(a);
        BuildingBase b = AimedBuilding();
        if (b == null)
            return Err("под прицелом нет здания");
        return a[0] == "finish" ? MachineFinish(b) : MachineInfo(b);
    }

    static string MachineFinish(BuildingBase b)
    {
        if (b is CrafterBuilding c)
        {
            if (c.currentRecipe == null)
                return Err("у станка нет рецепта");
            if (c.DevFinishCycle())
                return Ok(Name(b) + ": цикл завершён, выдано " + StacksText(c.currentRecipe.outputs));
            return Warn(Name(b) + ": не завершить — " + (c.IdleText() ?? "нет ингредиентов или места на выходе"));
        }

        if (b is Extractor e)
        {
            e.DevFinishCycle();
            return Ok(Name(b) + ": добыча на следующем тике");
        }

        if (b is OilExtractor o)
        {
            o.DevFinishCycle();
            return Ok(Name(b) + ": добыча на следующем кадре");
        }

        if (b is WaterExtractor w)
        {
            w.DevFinishCycle();
            return Ok(Name(b) + ": добыча на следующем кадре");
        }

        return Err(Name(b) + " — не станок");
    }

    static string MachineInfo(BuildingBase b)
    {
        var sb = new StringBuilder(512);
        Vector2Int cell = CellOf(b);
        sb.Append(b.data != null ? b.data.Title : b.name).Append("  [").Append(Name(b)).Append("]  ").Append(b.GetType().Name);
        sb.Append("\nклетка ").Append(CellText(cell)).Append(" · поворот ").Append(F(Mathf.Repeat(b.transform.eulerAngles.y, 360f), "0"))
            .Append("° · размер ").Append(b.FootprintSize.x).Append('×').Append(b.FootprintSize.y)
            .Append(" · уровень ").Append(b.ReadLevel());
        float power = PowerGenerator.GetNearbySpeedMultiplier(b.transform.position);
        sb.Append("\nпитание ×").Append(F(power)).Append(power > 1f ? " (генератор рядом)" : " (без генератора)");
        if (!Mathf.Approximately(b.DevWorkMul, 1f))
            sb.Append(" · консоль ×").Append(F(b.DevWorkMul));
        if (b.IsBroken)
            sb.Append("\nСЛОМАН: ").Append(b.BreakMode == 2 ? "работает в 1/4 силы" : "стоит").Append(" · игра ").Append(MinigameNames[Mathf.Clamp(b.BreakGame, 0, MinigameNames.Length - 1)]);
        sb.Append("\nвыход: ").Append(b.OutputBufferCount).Append('/').Append(b.maxOutputBuffer);

        if (b is CrafterBuilding c)
        {
            RecipeData r = c.currentRecipe;
            sb.Append("\nрецепт: ").Append(r != null ? RecipeLine(r) : "—");
            if (r != null)
            {
                float need = Economy.CraftNeed(r);
                sb.Append("\nпрогресс ").Append(F(c.craftProgress / Mathf.Max(0.01f, need) * 100f, "0")).Append("% · скорость ×").Append(F(c.CraftSpeed))
                    .Append(" · цикл ").Append(F(c.GetEffectiveCraftTime() / Mathf.Max(0.01f, b.DevWorkMul), "0.##")).Append("с");
                sb.Append("\nвходы:");
                if (r.inputs != null)
                {
                    for (int i = 0; i < r.inputs.Count; i++)
                    {
                        ItemStack s = r.inputs[i];
                        if (s == null || s.item == null)
                            continue;
                        sb.Append(' ').Append(s.item.id).Append(' ').Append(c.CountInput(s.item)).Append('/').Append(s.amount);
                    }
                }
            }

            string idle = c.IdleText();
            sb.Append("\nсостояние: ").Append(idle ?? (b.IsBroken ? "сломан" : "работает"));
            List<RecipeData> recipes = c.AvailableRecipes();
            if (recipes.Count > 1)
            {
                sb.Append("\nрецепты:");
                for (int i = 0; i < recipes.Count; i++)
                    sb.Append(' ').Append(Btn(recipes[i].id, "/setrecipe " + recipes[i].id + " here"));
            }
        }
        else if (b is Extractor e)
        {
            sb.Append("\nдобывает: ").Append(e.resource != null ? e.resource.id : "— (нет жилы)")
                .Append(" · ").Append(e.CurrentItemsPerCycle).Append(" за ").Append(F(e.CurrentInterval / Mathf.Max(0.01f, b.DevWorkMul))).Append("с")
                .Append(" · цикл ").Append(F(e.CycleProgress * 100f, "0")).Append('%');
            sb.Append("\nсостояние: ").Append(e.resource == null ? "нет жилы" : e.IsOutputJammed ? "выход забит" : b.IsBroken ? "сломан" : "работает");
        }
        else if (b is OilExtractor o)
        {
            sb.Append("\nнефть: ").Append(o.RichnessName).Append(" · ").Append(o.CurrentItemsPerCycle).Append(" за ")
                .Append(F(o.CurrentInterval / Mathf.Max(0.01f, b.DevWorkMul))).Append("с · цикл ").Append(F(o.CycleProgress * 100f, "0")).Append('%');
        }
        else if (b is WaterExtractor w)
        {
            sb.Append("\nвода: ").Append(w.CurrentItemsPerCycle).Append(" за ")
                .Append(F(w.CurrentInterval / Mathf.Max(0.01f, b.DevWorkMul))).Append("с · цикл ").Append(F(w.CycleProgress * 100f, "0")).Append('%');
        }
        else if (b is StorageContainer s)
        {
            sb.Append("\nслоты:");
            IReadOnlyList<ItemStack> slots = s.Slots;
            for (int i = 0; i < slots.Count; i++)
                sb.Append(' ').Append(slots[i].IsEmpty ? "—" : slots[i].item.id + " " + slots[i].amount + "/" + slots[i].item.maxStack);
        }
        else if (b is PowerGenerator g)
        {
            sb.Append("\nтопливо ").Append(F(g.RemainingFuel, "0.#")).Append("с из ").Append(F(g.MaxFuelTime, "0")).Append(" · ")
                .Append(g.Powered ? "работает ×" + F(g.speedMultiplier) + " в радиусе " + F(g.powerRadius, "0") : "без топлива");
        }
        else if (b is DroneLoadStation ls)
        {
            sb.Append("\nдронов ").Append(ls.OwnedDrones).Append(" (в полёте ").Append(ls.FlyingCount()).Append(") · ящик ")
                .Append(ls.CrateItem != null ? ls.CrateItem.id + " " + ls.CrateCount : "—").Append(" · готово ящиков ").Append(ls.ReadyCount)
                .Append(" · цель ").Append(ls.Target != null ? DroneNetwork.CellText(ls.Target) : "—");
        }
        else if (b is DroneUnloadStation us)
        {
            sb.Append("\nсклад ").Append(us.Total).Append('/').Append(DroneNetwork.UnloadCapacity).Append(" · летят ").Append(us.IncomingDrones());
            foreach (KeyValuePair<ItemData, int> pair in us.Storage)
                sb.Append("\n  ").Append(pair.Key != null ? pair.Key.id : "?").Append(' ').Append(pair.Value);
        }
        else if (b is Conveyor)
            sb.Append('\n').Append(BeltInfo(b as Conveyor));

        return sb.ToString();
    }

    static string StacksText(List<ItemStack> stacks)
    {
        var sb = new StringBuilder(32);
        AppendStacks(sb, stacks);
        return sb.ToString();
    }

    [DevCommand("idle", "list", "простаивающие станки с причиной и кнопкой телепорта")]
    static string Idle(DevArgs a)
    {
        if (a[0] != "list")
            return UsageOf(a);
        PlayerMovement move = Player();
        Vector3 from = move != null ? move.transform.position : Vector3.zero;
        var rows = new List<(BuildingBase b, string why, float d)>(32);
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            BuildingBase b = all[i];
            if (b == null || !b.IsPlaced)
                continue;
            string why = null;
            if (b is CrafterBuilding c)
                why = c.IdleText();
            else if (b is Extractor e)
                why = e.resource == null ? "нет жилы" : e.IsOutputJammed ? UiLocale.T("idle.output_full") : null;
            else if ((b is OilExtractor || b is WaterExtractor) && !b.HasOutputSpace(1))
                why = UiLocale.T("idle.output_full");
            if (b.IsBroken && BreakdownSystem.CanBreak(b))
                why = (b.BreakMode == 2 ? "сломан (1/4 силы)" : "сломан") + (why != null ? "; " + why : "");
            if (why == null)
                continue;
            rows.Add((b, why, (b.transform.position - from).sqrMagnitude));
        }

        if (rows.Count == 0)
            return Ok("все станки работают");
        rows.Sort((x, y) => x.d.CompareTo(y.d));
        var sb = new StringBuilder(1024);
        sb.Append("простаивают: ").Append(rows.Count).Append(" (ближние первыми)");
        int shown = Mathf.Min(30, rows.Count);
        for (int i = 0; i < shown; i++)
        {
            sb.Append('\n').Append(Name(rows[i].b)).Append(' ').Append(CellText(CellOf(rows[i].b))).Append(" — ").Append(rows[i].why)
                .Append("  ").Append(TpBtn(rows[i].b));
        }

        if (rows.Count > shown)
            sb.Append("\n… и ещё ").Append(rows.Count - shown);
        return sb.ToString();
    }

    [DevCommand("setrecipe", "<recipe> [here]", "поставить рецепт станку под прицелом")]
    static string SetRecipe(DevArgs a)
    {
        RecipeData recipe = GameDatabase.FindRecipe(a.Raw(0));
        if (recipe == null)
            return Err("нет рецепта " + a.Raw(0));
        if (a.Has(1) && a[1] != "here")
            return UsageOf(a);
        if (!(AimedBuilding() is CrafterBuilding c))
            return Err("под прицелом нет станка");
        if (!recipe.AllowsBuilding(c.data))
        {
            var sb = new StringBuilder(128);
            RecipeData[] all = GameDatabase.AllRecipes();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].AllowsBuilding(c.data))
                    sb.Append(' ').Append(Btn(all[i].id, "/setrecipe " + all[i].id + " here"));
            }

            return Err(recipe.id + " не для " + Name(c) + ". подходят:" + (sb.Length > 0 ? sb.ToString() : " —"));
        }

        c.SetRecipe(recipe);
        bool locked = ResearchSystem.Instance != null && !ResearchSystem.Instance.IsRecipeUnlocked(recipe);
        return Ok(Name(c) + " ← " + recipe.id + (locked ? " (рецепт ещё не открыт исследованием)" : ""));
    }

    [DevCommand("throughput", "[<item>]", "производство и расход в минуту по предметам (окно 30 с)")]
    static string Throughput(DevArgs a)
    {
        ProductionStats stats = ProductionStats.Instance;
        if (stats == null)
            return Err("no stats");
        if (a.Has(0))
        {
            ItemData item = GameDatabase.FindItem(a.Raw(0));
            if (item == null)
                return Err("нет предмета " + a.Raw(0));
            int makers = 0, users = 0;
            IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
            for (int i = 0; i < all.Count; i++)
            {
                BuildingBase b = all[i];
                if (b == null || !b.IsPlaced)
                    continue;
                if (b is CrafterBuilding c && c.currentRecipe != null)
                {
                    if (HasStack(c.currentRecipe.outputs, item))
                        makers++;
                    if (HasStack(c.currentRecipe.inputs, item))
                        users++;
                }
                else if (b is Extractor e && e.resource == item)
                    makers++;
                else if (b is OilExtractor o && o.resource == item)
                    makers++;
                else if (b is WaterExtractor w && w.resource == item)
                    makers++;
            }

            float plus = stats.ProducedPerMinute(item.id);
            float minus = stats.ConsumedPerMinute(item.id);
            stats.ProducedTotal.TryGetValue(item.id, out int madeTotal);
            stats.ConsumedTotal.TryGetValue(item.id, out int usedTotal);
            return item.id + ": +" + F(plus, "0.#") + " / −" + F(minus, "0.#") + " в мин, итого " + Signed(plus - minus)
                + "\nпроизводят " + makers + " · расходуют " + users + " зданий"
                + "\nза сессию: сделано " + madeTotal + ", израсходовано " + usedTotal + " · на складах " + CountStored(item);
        }

        var rows = new List<(string id, float plus, float minus)>(32);
        ItemData[] items = GameDatabase.AllItems();
        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null || string.IsNullOrEmpty(items[i].id))
                continue;
            float plus = stats.ProducedPerMinute(items[i].id);
            float minus = stats.ConsumedPerMinute(items[i].id);
            if (plus > 0.01f || minus > 0.01f)
                rows.Add((items[i].id, plus, minus));
        }

        if (rows.Count == 0)
            return Warn("за последние 30 с ничего не произведено");
        rows.Sort((x, y) => y.plus.CompareTo(x.plus));
        var sb = new StringBuilder(1024);
        sb.Append("в минуту:  +произв  −расход  итого");
        for (int i = 0; i < rows.Count; i++)
        {
            sb.Append('\n').Append(rows[i].id.PadRight(18)).Append(F(rows[i].plus, "0.#").PadLeft(8)).Append(F(rows[i].minus, "0.#").PadLeft(9))
                .Append(Signed(rows[i].plus - rows[i].minus).PadLeft(8)).Append("  ").Append(Btn("?", "/throughput " + rows[i].id));
        }

        return sb.ToString();
    }

    static string Signed(float v)
    {
        return (v >= 0f ? "+" : "−") + F(Mathf.Abs(v), "0.#");
    }
}
