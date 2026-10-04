using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>Предметы: выдать в здание, залить склады и баки, справка по предмету.</summary>
public static partial class DevCommands
{
    [DevCommand("give", "<item> [<n>]", "положить предмет в здание под прицелом (сколько возьмёт)")]
    static string Give(DevArgs a)
    {
        ItemData item = GameDatabase.FindItem(a.Raw(0));
        if (item == null)
            return Err("нет предмета " + a.Raw(0));
        int want = 1;
        if (a.Has(1) && (!a.TryInt(1, out want) || want <= 0))
            return UsageOf(a);
        BuildingBase b = AimedBuilding();
        if (b == null)
            return Err("под прицелом нет здания");

        int given = 0;
        if (b is StorageContainer store)
        {
            while (given < want && store.TryAddOne(item))
                given++;
        }
        else if (b is DroneUnloadStation unload)
        {
            int room = Mathf.Max(0, DroneNetwork.UnloadCapacity - unload.Total);
            int n = Mathf.Min(want, room);
            if (n > 0 && unload.Deliver(item, n))
                given = n;
        }
        else
        {
            BuildingSocket socket = b.inputSockets != null && b.inputSockets.Length > 0 ? b.inputSockets[0] : null;
            while (given < want && b.TryReceiveItem(item, socket))
                given++;
        }

        if (given == 0)
            return Warn(Name(b) + " не принимает " + item.id);
        return given < want
            ? Warn(Name(b) + ": принято " + given + " из " + want + " " + item.id + " (больше не влезло)")
            : Ok(Name(b) + ": +" + given + " " + item.id);
    }

    enum FillTarget { Here, All }

    [DevCommand("fill", "storage [here|all] [<item>]", "склады до максимума; с предметом — очистить и залить им")]
    [DevCommand("fill", "storage <item> [here|all]", "то же, предмет первым")]
    [DevCommand("fill", "tank [here|all] [<fluid>]", "жидкостные баки до максимума; с жидкостью — очистить и залить ей")]
    [DevCommand("fill", "tank <fluid> [here|all]", "то же, жидкость первой")]
    static string Fill(DevArgs a)
    {
        bool tank = a[0] == "tank";
        if (!tank && a[0] != "storage")
            return UsageOf(a);

        FillTarget target = FillTarget.Here;
        ItemData item = null;
        for (int i = 1; i < a.Count; i++)
        {
            if (a[i] == "here")
                target = FillTarget.Here;
            else if (a[i] == "all")
                target = FillTarget.All;
            else
            {
                item = GameDatabase.FindItem(a.Raw(i));
                if (item == null)
                    return Err("нет предмета " + a.Raw(i));
            }
        }

        if (item != null && item.isFluid != tank)
            return Err(tank ? item.id + " не жидкость — для складов /fill storage" : item.id + " жидкость — для неё /fill tank");

        var targets = new List<StorageContainer>();
        if (target == FillTarget.Here)
        {
            BuildingBase b = AimedBuilding();
            if (!(b is StorageContainer s) || (s is FluidStorageTank) != tank)
                return Err(tank ? "под прицелом нет жидкостного бака" : "под прицелом нет склада");
            targets.Add(s);
        }
        else
        {
            IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] is StorageContainer s && s != null && s.IsPlaced && (s is FluidStorageTank) == tank)
                    targets.Add(s);
            }
        }

        int filled = 0;
        int skipped = 0;
        long added = 0;
        for (int i = 0; i < targets.Count; i++)
        {
            StorageContainer s = targets[i];
            ItemData use = item;
            if (use != null)
                s.DevClear();
            else
                use = s.StoredType;
            if (use == null)
            {
                skipped++;
                continue;
            }

            added += s.DevFill(use);
            filled++;
        }

        string what = tank ? "баков" : "складов";
        if (filled == 0)
            return Warn("нечего заливать: " + (skipped > 0 ? "пустые — укажи " + (tank ? "<fluid>" : "<item>") : "нет " + what));
        string text = "залито " + what + ": " + filled + " (+" + added + ")";
        if (skipped > 0)
            text += ", пустых пропущено " + skipped;
        return Ok(text);
    }

    [DevCommand("item", "info <item>", "цена, рецепты, где производится и где расходуется")]
    static string ItemInfo(DevArgs a)
    {
        if (a[0] != "info")
            return UsageOf(a);
        ItemData item = GameDatabase.FindItem(a.Raw(1));
        if (item == null)
            return Err("нет предмета " + a.Raw(1));

        var sb = new StringBuilder(512);
        sb.Append(item.Title).Append("  [").Append(item.id).Append(']');
        sb.Append("\nцена в лабе ").Append(item.sellValue).Append("c · стак ").Append(item.maxStack);
        if (item.isFluid)
            sb.Append(" · жидкость");
        if (item.isFuel)
            sb.Append(" · топливо ").Append(F(item.fuelValue));
        if (!string.IsNullOrEmpty(item.extractedBy))
            sb.Append("\nдобывается: ").Append(item.extractedBy);

        RecipeData[] recipes = GameDatabase.AllRecipes();
        ResearchSystem rs = ResearchSystem.Instance;
        var made = new StringBuilder();
        var used = new StringBuilder();
        for (int i = 0; i < recipes.Length; i++)
        {
            RecipeData r = recipes[i];
            if (r == null)
                continue;
            bool outputs = HasStack(r.outputs, item);
            bool inputs = HasStack(r.inputs, item);
            if (!outputs && !inputs)
                continue;
            string line = "\n  " + RecipeLine(r) + (rs != null && !rs.IsRecipeUnlocked(r) ? "  (закрыт)" : "");
            if (outputs)
                made.Append(line);
            if (inputs)
                used.Append(line);
        }

        sb.Append("\nпроизводится:").Append(made.Length > 0 ? made.ToString() : " —");
        sb.Append("\nрасходуется:").Append(used.Length > 0 ? used.ToString() : " —");

        var research = new StringBuilder();
        ResearchNodeData[] nodes = GameDatabase.AllResearches();
        for (int i = 0; i < nodes.Length; i++)
        {
            ResearchNodeData n = nodes[i];
            if (n == null || n.requiredItems == null)
                continue;
            for (int k = 0; k < n.requiredItems.Count; k++)
            {
                if (n.requiredItems[k].item != item)
                    continue;
                research.Append("\n  ").Append(n.id).Append(" ×").Append(ResearchSystem.Need(n.requiredItems[k].amount));
                break;
            }
        }

        if (research.Length > 0)
            sb.Append("\nнужен исследованиям:").Append(research);

        ProductionStats stats = ProductionStats.Instance;
        if (stats != null)
        {
            sb.Append("\nсейчас в минуту: +").Append(F(stats.ProducedPerMinute(item.id), "0.#"))
                .Append(" / −").Append(F(stats.ConsumedPerMinute(item.id), "0.#"));
        }

        int stock = CountStored(item);
        if (stock > 0)
            sb.Append(" · на складах ").Append(stock);
        return sb.ToString();
    }

    static bool HasStack(List<ItemStack> stacks, ItemData item)
    {
        if (stacks == null)
            return false;
        for (int i = 0; i < stacks.Count; i++)
        {
            if (stacks[i] != null && stacks[i].item == item)
                return true;
        }

        return false;
    }

    static string RecipeLine(RecipeData r)
    {
        var sb = new StringBuilder(96);
        sb.Append(r.id).Append(": ");
        AppendStacks(sb, r.inputs);
        sb.Append(" → ");
        AppendStacks(sb, r.outputs);
        sb.Append("  ").Append(F(Economy.CraftNeed(r), "0.#")).Append("с");
        string where = r.requiredBuilding != null ? r.requiredBuilding.id : "";
        if (r.HasAllowedIds())
            where = string.Join("/", r.allowedBuildingIds);
        if (where.Length > 0)
            sb.Append(" в ").Append(where);
        return sb.ToString();
    }

    static void AppendStacks(StringBuilder sb, List<ItemStack> stacks)
    {
        if (stacks == null || stacks.Count == 0)
        {
            sb.Append('—');
            return;
        }

        for (int i = 0; i < stacks.Count; i++)
        {
            if (i > 0)
                sb.Append(" + ");
            ItemStack s = stacks[i];
            sb.Append(s != null ? s.amount : 0).Append(' ').Append(s != null && s.item != null ? s.item.id : "?");
        }
    }

    static int CountStored(ItemData item)
    {
        int n = 0;
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            if (!(all[i] is StorageContainer s) || s == null || !s.IsPlaced)
                continue;
            IReadOnlyList<ItemStack> slots = s.Slots;
            for (int k = 0; k < slots.Count; k++)
            {
                if (slots[k] != null && slots[k].item == item)
                    n += slots[k].amount;
            }
        }

        return n;
    }
}
