using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cat = DecorCatalog.Cat;
using Def = DecorCatalog.Def;

/// <summary>
/// Вкладка «Декорации» в окне магазина ([[WalletHud]]): красота завода, скидка дня, категории,
/// плитки с ценой в рубинах и ценой установки, коллекции. Покупка — через подтверждение.
/// </summary>
public class DecorShopView
{
    VisualElement root;
    VisualElement chips;
    VisualElement grid;
    Label setLine;
    Label beautyValue;
    Label beautyNext;
    VisualElement beautyBar;
    Label ownedValue;
    Label ownedSub;
    VisualElement dealIcon;
    Label dealName;
    Label dealPrice;
    Label hint;
    string filter = "all";

    public VisualElement Root => root;

    public VisualElement Build()
    {
        root = IndustryUi.El("DecorShop", "col", "grow");
        root.style.minHeight = 0;

        var top = IndustryUi.El("Top", "decor-top");

        var beauty = IndustryUi.El("Beauty", "decor-card");
        beauty.Add(IndustryUi.Text("L", DecorText.T("decor.beauty"), "decor-card-label"));
        beautyValue = IndustryUi.Text("V", "0", "decor-card-value");
        beauty.Add(beautyValue);
        beautyBar = IndustryUi.ProgressBar("Bar");
        beautyBar.AddToClassList("decor-bar");
        beauty.Add(beautyBar);
        beautyNext = IndustryUi.Text("N", "", "decor-card-sub");
        beauty.Add(beautyNext);
        UiTooltip.Bind(beauty, DecorText.T("decor.beauty"), DecorText.T("decor.beauty_tip"));
        top.Add(beauty);

        var owned = IndustryUi.El("Owned", "decor-card");
        owned.Add(IndustryUi.Text("L", DecorText.T("decor.tab.decor"), "decor-card-label"));
        ownedValue = IndustryUi.Text("V", "0", "decor-card-value");
        owned.Add(ownedValue);
        ownedSub = IndustryUi.Text("S", "", "decor-card-sub");
        owned.Add(ownedSub);
        top.Add(owned);

        var deal = IndustryUi.El("Deal", "decor-card", "decor-deal");
        dealIcon = IndustryUi.Icon(null, "decor-deal-icon");
        deal.Add(dealIcon);
        var dealCol = IndustryUi.El("Col", "col", "grow");
        dealCol.Add(IndustryUi.Text("L", DecorText.T("decor.deal"), "decor-card-label"));
        dealName = IndustryUi.Text("N", "", "decor-deal-name");
        dealCol.Add(dealName);
        dealPrice = IndustryUi.Text("P", "", "decor-card-sub", "ruby");
        dealCol.Add(dealPrice);
        deal.Add(dealCol);
        deal.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0)
                return;
            Def d = DecorSystem.Deal();
            if (d == null)
                return;
            filter = d.cat.ToString();
            Refresh();
            evt.StopPropagation();
        });
        UiTooltip.Bind(deal, DecorText.T("decor.deal"), DecorText.T("decor.deal_tip", DecorSystem.DealPercent));
        top.Add(deal);
        root.Add(top);

        chips = IndustryUi.El("Chips", "decor-chips");
        root.Add(chips);
        setLine = IndustryUi.Text("Set", "", "decor-set");
        root.Add(setLine);

        ScrollView scroll = IndustryUi.Scroll("DecorScroll");
        scroll.mode = ScrollViewMode.Vertical;
        grid = IndustryUi.El("Grid", "bag-grid", "decor-grid");
        scroll.Add(grid);
        root.Add(scroll);

        hint = IndustryUi.Text("Hint", "", "decor-hint");
        root.Add(hint);
        return root;
    }

    public void Refresh()
    {
        if (root == null)
            return;
        RefreshTop();
        RefreshChips();
        RefreshGrid();
        hint.text = DecorText.T("decor.hint_bag", KeybindStore.Hint("Inventory"));
    }

    void RefreshTop()
    {
        DecorSystem sys = DecorSystem.Instance;
        float b = DecorSystem.Beauty;
        beautyValue.text = Mathf.FloorToInt(b).ToString();
        if (sys != null && sys.NextBeautyStep(out int step, out int reward))
        {
            int prev = 0;
            for (int i = 0; i < DecorSystem.BeautySteps.Length; i++)
            {
                if (DecorSystem.BeautySteps[i] >= step)
                    break;
                prev = DecorSystem.BeautySteps[i];
            }

            IndustryUi.SetProgress(beautyBar, Mathf.InverseLerp(prev, step, b));
            beautyNext.text = DecorText.T("decor.next", step, reward);
        }
        else
        {
            IndustryUi.SetProgress(beautyBar, 1f);
            beautyNext.text = DecorText.T("decor.next_done");
        }

        int total = DecorCatalog.All.Count;
        int owned = sys != null ? sys.OwnedCount() : 0;
        ownedValue.text = owned + " / " + total;
        ownedSub.text = DecorText.T("decor.owned_n", owned, total);

        Def deal = DecorSystem.Deal();
        if (deal != null)
        {
            IndustryUi.SetIcon((Image)dealIcon, deal.data != null ? deal.data.icon : null);
            dealName.text = deal.Title;
            dealPrice.text = DecorSystem.IsOwned(deal)
                ? DecorText.T("decor.owned")
                : deal.rubies + " → " + DecorSystem.Price(deal) + "  (−" + DecorSystem.DealPercent + "%)";
        }
    }

    void RefreshChips()
    {
        chips.Clear();
        AddChip("all", DecorText.T("decor.cat.all"), CountOwned(null), DecorCatalog.All.Count);
        for (int i = 0; i < DecorCatalog.Categories.Length; i++)
        {
            Cat cat = DecorCatalog.Categories[i];
            int total = 0;
            IReadOnlyList<Def> all = DecorCatalog.All;
            for (int k = 0; k < all.Count; k++)
            {
                if (all[k].cat == cat)
                    total++;
            }

            AddChip(cat.ToString(), DecorCatalog.CategoryTitle(cat), CountOwned(cat), total);
        }

        Cat? set = FilterCat();
        if (set.HasValue && System.Array.IndexOf(DecorSystem.SetCats, set.Value) >= 0)
        {
            DecorSystem.SetProgress(set.Value, out int have, out int need);
            string title = DecorCatalog.CategoryTitle(set.Value);
            bool done = DecorSystem.Instance != null && DecorSystem.Instance.IsClaimed("set_" + set.Value.ToString().ToLowerInvariant());
            setLine.text = done
                ? DecorText.T("decor.set_done", title)
                : DecorText.T("decor.set", title, have, need, DecorSystem.SetReward(set.Value));
            IndustryUi.Show(setLine, true);
        }
        else
            IndustryUi.Show(setLine, false);
    }

    Cat? FilterCat()
    {
        for (int i = 0; i < DecorCatalog.Categories.Length; i++)
        {
            if (DecorCatalog.Categories[i].ToString() == filter)
                return DecorCatalog.Categories[i];
        }

        return null;
    }

    static int CountOwned(Cat? cat)
    {
        int n = 0;
        IReadOnlyList<Def> all = DecorCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            if ((!cat.HasValue || all[i].cat == cat.Value) && DecorSystem.IsOwned(all[i]))
                n++;
        }

        return n;
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

    void RefreshGrid()
    {
        grid.Clear();
        Cat? cat = FilterCat();
        IReadOnlyList<Def> all = DecorCatalog.All;
        for (int i = 0; i < all.Count; i++)
        {
            if (cat.HasValue && all[i].cat != cat.Value)
                continue;
            grid.Add(MakeTile(all[i]));
        }
    }

    VisualElement MakeTile(Def def)
    {
        DecorSystem.Offer offer = DecorSystem.OfferFor(def);
        bool deal = DecorSystem.IsDeal(def) && offer == DecorSystem.Offer.Buy;
        var tile = IndustryUi.El("Decor_" + def.id, "bag-tile", "decor-tile", "cat-decor");
        IndustryUi.SetOn(tile, offer == DecorSystem.Offer.Owned, "is-owned");
        IndustryUi.SetOn(tile, offer == DecorSystem.Offer.Trophy || offer == DecorSystem.Offer.OffSeason, "is-locked");
        tile.Add(IndustryUi.El("Stripe", "bag-tile-stripe"));
        tile.Add(IndustryUi.Text("Size", DecorCatalog.SizeLabel(def), "decor-size"));
        if (deal)
            tile.Add(IndustryUi.Text("Deal", "−" + DecorSystem.DealPercent + "%", "decor-deal-badge"));

        var well = IndustryUi.El("Well", "bag-tile-well");
        well.Add(IndustryUi.Icon(def.data != null ? def.data.icon : null, "bag-tile-icon"));
        tile.Add(well);
        tile.Add(IndustryUi.Text("T", def.Title, "bag-tile-name"));
        int coins = def.data != null ? Economy.BuildCost(def.data) : def.coins;
        tile.Add(IndustryUi.Text("Meta", "◈ " + IndustryUi.Money(coins) + "   ♥ " + BeautyLabel(def.beauty), "decor-meta"));
        tile.Add(IndustryUi.El("Spacer", "grow"));

        switch (offer)
        {
            case DecorSystem.Offer.Owned:
                tile.Add(IndustryUi.Text("State", DecorText.T("decor.owned"), "decor-state"));
                break;
            case DecorSystem.Offer.Trophy:
                AchievementSystem.Def ach = AchievementSystem.Find(def.trophy);
                string achTitle = ach.id != null ? UiLocale.T(ach.titleKey) : def.trophy;
                tile.Add(IndustryUi.Text("State", DecorText.T("decor.trophy", achTitle), "decor-state", "is-locked"));
                break;
            case DecorSystem.Offer.OffSeason:
                tile.Add(IndustryUi.Text("State", DecorText.T("decor.season_locked"), "decor-state", "is-locked"));
                break;
            default:
                int price = DecorSystem.Price(def);
                int have = PlayerWallet.Instance != null ? PlayerWallet.Instance.Rubies : 0;
                Def captured = def;
                Button buy = IndustryUi.Btn(price.ToString(), () => AskBuy(captured), "btn-small", "btn-primary", "decor-buy");
                buy.Insert(0, IndustryUi.Icon(GameHudIcons.Ruby, "decor-buy-icon"));
                buy.SetEnabled(have >= price);
                tile.Add(buy);
                break;
        }

        UiTooltip.Bind(tile, def.Title, TooltipBody(def), TooltipFoot(def));
        return tile;
    }

    static string BeautyLabel(float b)
    {
        return b >= 1f ? Mathf.RoundToInt(b).ToString() : b.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    static string TooltipBody(Def def)
    {
        string body = def.Desc;
        if (def.perk == DecorCatalog.Perk.Lamp)
            body += "\n" + DecorText.T("decor.perk_lamp");
        else if (def.perk == DecorCatalog.Perk.Rest)
            body += "\n" + DecorText.T("decor.perk_rest", Mathf.RoundToInt(DecorSystem.RestBonusSeconds));
        return body;
    }

    static string TooltipFoot(Def def)
    {
        string foot = DecorText.T("decor.size", DecorCatalog.SizeLabel(def))
            + "  ·  " + DecorText.T("decor.beauty_pts", BeautyLabel(def.beauty));
        if (def.IsFloor)
            foot += "  ·  " + DecorText.T("decor.floor");
        if (def.IsLine)
            foot += "  ·  " + DecorText.T("decor.line");
        return foot;
    }

    void AskBuy(Def def)
    {
        DecorSystem sys = DecorSystem.Instance;
        if (sys == null || DecorSystem.OfferFor(def) != DecorSystem.Offer.Buy)
            return;
        int price = DecorSystem.Price(def);
        if (PlayerWallet.Instance == null || PlayerWallet.Instance.Rubies < price)
        {
            UiAudio.PlayError();
            UiNotification.Push(NotifyKind.Resources, DecorText.T("decor.need_rubies"), def.Title, UiStatus.Warning);
            return;
        }

        int coins = def.data != null ? Economy.BuildCost(def.data) : def.coins;
        UiModal.Confirm(
            DecorText.T("decor.confirm_title", def.Title),
            DecorText.T("decor.confirm_body", price, IndustryUi.Money(coins)),
            DecorText.T("decor.buy"),
            () =>
            {
                if (!sys.TryBuy(def))
                    UiAudio.PlayError();
                Refresh();
            },
            false);
    }
}
