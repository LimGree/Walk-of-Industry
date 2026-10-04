using System.Collections.Generic;

/// <summary>Строки интерфейса декораций (магазин, тосты, подсказки) — RU/EN, язык как у [[UiLocale]].</summary>
public static class DecorText
{
    public static string T(string key)
    {
        if (UiLocale.ShowKeys)
            return key ?? "";
        if (key != null && Table.TryGetValue(key, out var pair))
            return UiLocale.IsRu ? pair.ru : pair.en;
        return key ?? "";
    }

    public static string T(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    static readonly Dictionary<string, (string ru, string en)> Table = new Dictionary<string, (string ru, string en)>
    {
        { "decor.cat.all", ("Все", "All") },
        { "decor.cat.light", ("Свет", "Lights") },
        { "decor.cat.nature", ("Природа", "Nature") },
        { "decor.cat.industry", ("Промзона", "Industrial") },
        { "decor.cat.road", ("Дороги", "Roads") },
        { "decor.cat.rest", ("Отдых", "Rest") },
        { "decor.cat.monument", ("Памятники", "Landmarks") },
        { "decor.cat.season", ("Сезонные", "Seasonal") },

        { "decor.tab.decor", ("Декорации", "Decorations") },
        { "decor.tab.exchange", ("Обмен рубинов", "Ruby exchange") },
        { "decor.sub", ("Купи за рубины один раз — ставь сколько угодно за монеты", "Buy once with rubies — place as many as you like for coins") },
        { "decor.beauty", ("Красота завода", "Factory beauty") },
        { "decor.beauty_tip", ("Сумма очков красоты поставленных декораций. Одинаковые после 10-й считаются на четверть. За пороги — рубины.", "Beauty points of placed decorations. Copies past the 10th count a quarter. Milestones pay rubies.") },
        { "decor.next", ("Следующая награда: {0} → +{1}", "Next reward: {0} → +{1}") },
        { "decor.next_done", ("Все награды за красоту получены", "All beauty rewards claimed") },
        { "decor.owned_n", ("Куплено {0} из {1}", "Owned {0} of {1}") },
        { "decor.deal", ("Скидка дня", "Deal of the day") },
        { "decor.deal_tip", ("−{0}% до конца игровых суток", "−{0}% until the end of the in-game day") },
        { "decor.buy", ("Купить", "Buy") },
        { "decor.owned", ("Куплено", "Owned") },
        { "decor.place_cost", ("Установка: {0}", "Placement: {0}") },
        { "decor.beauty_pts", ("Красота +{0}", "Beauty +{0}") },
        { "decor.trophy", ("Награда: {0}", "Reward: {0}") },
        { "decor.trophy_locked", ("Не продаётся", "Not for sale") },
        { "decor.season_locked", ("С 1 декабря", "From December 1") },
        { "decor.need_rubies", ("Не хватает рубинов", "Not enough rubies") },
        { "decor.perk_lamp", ("Ночью станки рядом ломаются реже", "Nearby machines break less at night") },
        { "decor.perk_rest", ("Ремонт рядом: +{0} с и +1 ошибка", "Repairs nearby: +{0}s and +1 mistake") },
        { "decor.set", ("Коллекция «{0}»: {1}/{2} → +{3}", "{0} collection: {1}/{2} → +{3}") },
        { "decor.set_done", ("Коллекция «{0}» собрана", "{0} collection complete") },
        { "decor.size", ("Размер {0}", "Size {0}") },
        { "decor.floor", ("Напольная", "Floor") },
        { "decor.line", ("Линией", "Line") },
        { "decor.hint_bag", ("Купленные декорации — в сумке ({0}), вкладка «Декорации»", "Owned decorations are in the bag ({0}), Decorations tab") },
        { "decor.confirm_title", ("Купить «{0}»?", "Buy {0}?") },
        { "decor.confirm_body", ("{0} рубинов. Декорация откроется навсегда, установка — {1} монет за штуку.", "{0} rubies. Unlocks forever; each placement costs {1} coins.") },

        { "decor.toast_bought", ("Декорация куплена", "Decoration bought") },
        { "decor.toast_bought_body", ("{0} — в сумке, вкладка «Декорации»", "{0} — in the bag, Decorations tab") },
        { "decor.toast_trophy", ("Новая декорация-награда", "New trophy decoration") },
        { "decor.toast_beauty", ("Красота завода {0}", "Factory beauty {0}") },
        { "decor.toast_beauty_body", ("Награда: +{0} рубинов", "Reward: +{0} rubies") },
        { "decor.toast_set", ("Коллекция собрана", "Collection complete") },
        { "decor.toast_set_body", ("«{0}»: +{1} рубинов", "{0}: +{1} rubies") },
        { "decor.toast_locked", ("Декорация не куплена", "Decoration not owned") },
        { "decor.toast_locked_body", ("Купи её в магазине ({0})", "Buy it in the shop ({0})") },

        { "decor.hint_tint", ("перекрасить · {0}", "recolor · {0}") },
        { "decor.hint_tint_free", ("перекрасить", "recolor") },
        { "decor.no_money_tint", ("Не хватает монет на краску", "Not enough coins for paint") },

        { "decor.stats_title", ("РЕКОРДЫ ЗАВОДА", "FACTORY RECORDS") },
        { "decor.stats_items", ("Произведено: {0}", "Produced: {0}") },
        { "decor.stats_coins", ("Заработано: {0}", "Earned: {0}") },
        { "decor.stats_research", ("Исследований: {0}/{1}", "Research: {0}/{1}") },
        { "decor.stats_day", ("День {0}", "Day {0}") },
        { "decor.default_name", ("МОЙ ЗАВОД", "MY FACTORY") },
    };
}
