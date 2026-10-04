using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Вкладка «Снаряжение» магазина ([[WalletHud]]): плюшки персонажа из [[PerkSystem]] за рубины.
/// Вид как у декора ([[DecorShopView]]): фильтр-чипы по разделам, сетка плиток с иконкой
/// (<c>Resources/Perks/Icons/&lt;id&gt;</c>, генератор — wi_perks.py), уровнем и кнопкой.
/// </summary>
public class PerkShopView
{
    VisualElement root;
    VisualElement chips;
    VisualElement grid;
    Label hint;
    string filter = "all";

    static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();

    public VisualElement Root => root;

    public static Sprite Icon(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (!icons.TryGetValue(id, out Sprite s))
        {
            s = Resources.Load<Sprite>("Perks/Icons/" + id);
            icons[id] = s;
        }

        return s;
    }

    public VisualElement Build()
    {
        root = IndustryUi.El("PerkShop", "col", "grow");
        root.style.minHeight = 0;
        chips = IndustryUi.El("Chips", "decor-chips");
        root.Add(chips);
        ScrollView scroll = IndustryUi.Scroll("PerkScroll");
        scroll.mode = ScrollViewMode.Vertical;
        grid = IndustryUi.El("Grid", "bag-grid", "decor-grid");
        scroll.Add(grid);
        root.Add(scroll);
        hint = IndustryUi.Text("Hint", "", "decor-hint");
        root.Add(hint);
        if (PerkSystem.Instance != null)
            PerkSystem.Instance.Changed += Refresh;
        return root;
    }

    public void Refresh()
    {
        if (root == null)
            return;
        RefreshChips();
        RefreshGrid();
        hint.text = UiLocale.T("perkshop.hint", KeybindStore.Hint("Inventory"));
    }

    // ---------- Фильтр ----------

    static readonly (string id, PerkSystem.Cat cat, string key)[] Cats =
    {
        ("Move", PerkSystem.Cat.Move, "perkshop.move"),
        ("Work", PerkSystem.Cat.Work, "perkshop.work"),
        ("Economy", PerkSystem.Cat.Economy, "perkshop.economy"),
        ("Style", PerkSystem.Cat.Style, "perkshop.style"),
    };

    void RefreshChips()
    {
        chips.Clear();
        Count(null, out int have, out int total);
        AddChip("all", UiLocale.T("perkshop.all"), have, total);
        for (int i = 0; i < Cats.Length; i++)
        {
            Count(Cats[i].cat, out have, out total);
            AddChip(Cats[i].id, Title(Cats[i].key), have, total);
        }
    }

    /// <summary>«ПЕРЕДВИЖЕНИЕ» → «Передвижение» для чипа.</summary>
    static string Title(string key)
    {
        string s = UiLocale.T(key);
        return s.Length > 1 ? s.Substring(0, 1) + s.Substring(1).ToLowerInvariant() : s;
    }

    static void Count(PerkSystem.Cat? cat, out int have, out int total)
    {
        have = 0;
        total = 0;
        for (int i = 0; i < PerkSystem.All.Length; i++)
        {
            PerkSystem.Def d = PerkSystem.All[i];
            if (cat.HasValue && d.cat != cat.Value)
                continue;
            total++;
            if (PerkSystem.Has(d.id))
                have++;
        }
    }

    void AddChip(string id, string label, int have, int total)
    {
        var chip = IndustryUi.El("Chip_" + id, "bag-chip", "cat-decor");
        chip.Add(IndustryUi.El("Dot", "bag-chip-dot"));
        chip.Add(IndustryUi.Text("L", label, "bag-chip-label"));
        chip.Add(IndustryUi.Text("N", have + "/" + total, "bag-chip-count"));
        IndustryUi.SetOn(chip, id == filter, "is-selected");
        chip.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;
            if (filter != id)
            {
                filter = id;
                UiAudio.PlayToggle();
                Refresh();
            }

            evt.StopPropagation();
        });
        chips.Add(chip);
    }

    // ---------- Плитки ----------

    void RefreshGrid()
    {
        grid.Clear();
        for (int i = 0; i < PerkSystem.All.Length; i++)
        {
            PerkSystem.Def d = PerkSystem.All[i];
            if (filter != "all" && d.cat.ToString() != filter)
                continue;
            grid.Add(Tile(d));
        }
    }

    VisualElement Tile(PerkSystem.Def d)
    {
        int level = PerkSystem.Level(d.id);
        bool maxed = PerkSystem.IsMaxed(d);
        bool reqOk = PerkSystem.RequirementMet(d);
        var tile = IndustryUi.El("Perk_" + d.id, "bag-tile", "decor-tile", "perk-tile");
        IndustryUi.SetOn(tile, level > 0, "is-owned");
        IndustryUi.SetOn(tile, !reqOk && level == 0, "is-locked");
        tile.Add(IndustryUi.El("Stripe", "bag-tile-stripe"));
        if (d.kind == PerkSystem.Kind.Gear)
            tile.Add(IndustryUi.Text("Kind", UiLocale.T("perkshop.badge_gear"), "decor-size"));

        var well = IndustryUi.El("Well", "bag-tile-well");
        well.Add(IndustryUi.Icon(Icon(d.id), "bag-tile-icon"));
        tile.Add(well);
        tile.Add(IndustryUi.Text("T", d.Title, "bag-tile-name"));
        string meta = d.Leveled
            ? UiLocale.T("perkshop.level", level, d.prices.Length)
            : level > 0 ? UiLocale.T("perkshop.owned") : "";
        tile.Add(IndustryUi.Text("Meta", meta, "decor-meta"));
        tile.Add(IndustryUi.El("Spacer", "grow"));

        if (!reqOk && level == 0)
        {
            PerkSystem.Def req = PerkSystem.Find(d.requires);
            tile.Add(IndustryUi.Text("State", UiLocale.T("perkshop.requires", req != null ? req.Title : d.requires), "decor-state", "is-locked"));
        }
        else if (!maxed)
        {
            int price = PerkSystem.NextPrice(d);
            bool afford = GameSettings.Sandbox || (PlayerWallet.Instance != null && PlayerWallet.Instance.Rubies >= price);
            string label = level == 0 ? price.ToString() : UiLocale.T("perkshop.up", price);
            Button buy = IndustryUi.Btn(label, () => Buy(d), "btn-small", "btn-primary", "decor-buy");
            buy.Insert(0, IndustryUi.Icon(GameHudIcons.Ruby, "decor-buy-icon"));
            buy.SetEnabled(afford);
            tile.Add(buy);
        }
        else if (d.kind == PerkSystem.Kind.Cosmetic && d.id != "horn")
        {
            bool on = d.id == "pet" ? PerkSystem.PetOn : PerkSystem.IsWorn(d.id);
            string id = d.id;
            tile.Add(IndustryUi.Btn(on ? UiLocale.T("perkshop.take_off") : UiLocale.T("perkshop.wear"), () =>
            {
                if (id == "pet")
                    PerkSystem.Instance?.TogglePet();
                else
                    PerkSystem.Instance?.Wear(id);
                UiAudio.PlayToggle();
            }, "btn-small", on ? "btn-ghost" : "btn-primary", "decor-buy"));
        }
        else
            tile.Add(IndustryUi.Text("State", UiLocale.T("perkshop.owned"), "decor-state"));

        UiTooltip.Bind(tile, d.Title, d.Info, Foot(d));
        return tile;
    }

    static string Foot(PerkSystem.Def d)
    {
        if (!d.Leveled)
            return UiLocale.T("perkshop.price_one", d.prices[0]);
        return UiLocale.T("perkshop.price_levels", string.Join(" / ", d.prices));
    }

    void Buy(PerkSystem.Def d)
    {
        if (PerkSystem.Instance != null && PerkSystem.Instance.TryBuy(d))
        {
            UiAudio.PlayConfirm();
            UiNotification.Push(UiLocale.T("perkshop.bought", d.Title), d.Info, UiStatus.Completed);
        }
        else
            UiAudio.PlayError();
    }
}
