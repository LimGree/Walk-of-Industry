using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>Откуда пришли или куда ушли монеты/рубины — для учёта и ребаланса экономики.</summary>
public enum MoneySource
{
    Other,
    LabSale,      // продажа предметов в лабу
    Refund,       // возврат за снос
    Research,     // награда за исследование
    DecorReward,  // рубины за наборы/красоту декора
    Build,        // постройка
    Upgrade,      // улучшение здания
    BeltUpgrade,  // скорость лент
    Decor,        // покупка и установка декора
    Repair,       // ремонт и штрафы поломок
    Upkeep,       // содержание зданий
    Drones,       // покупка дронов
    Exchange,     // обмен рубинов на монеты
    Undo,         // отмена/повтор действий
    Cheat         // консоль, тестовая площадка
}

/// <summary>
/// Журнал экономики за сессию: доходы и расходы по источникам, поминутно, с балансом на конец минуты.
/// Время — игровое (пауза не идёт). Показывается во вкладке «Экономика» лаборатории (<see cref="EconomyGraph"/>),
/// сводка — в консоли (economy).
/// </summary>
public static class EconomyLedger
{
    public static readonly int SourceCount = Enum.GetValues(typeof(MoneySource)).Length;
    const int MaxMinutes = 600;

    public sealed class Minute
    {
        public int index;
        public readonly int[] coinIn = new int[SourceCount];
        public readonly int[] coinOut = new int[SourceCount];
        public readonly int[] rubyIn = new int[SourceCount];
        public readonly int[] rubyOut = new int[SourceCount];
        public int coinsEnd;
        public int rubiesEnd;
        public int buildings;

        public int CoinIncome { get { int s = 0; for (int i = 0; i < coinIn.Length; i++) s += coinIn[i]; return s; } }
        public int CoinExpense { get { int s = 0; for (int i = 0; i < coinOut.Length; i++) s += coinOut[i]; return s; } }
    }

    static readonly List<Minute> minutes = new List<Minute>(128);
    static float playSeconds;
    static int startCoins;
    static int startRubies;

    public static IReadOnlyList<Minute> Minutes => minutes;
    public static float PlaySeconds => playSeconds;
    public static int StartCoins => startCoins;

    /// <summary>Расходные статьи: возврат по ним (неудачное улучшение) уменьшает расход, а не считается доходом.</summary>
    public static bool IsExpense(MoneySource s)
    {
        return s == MoneySource.Build || s == MoneySource.Upgrade || s == MoneySource.BeltUpgrade
            || s == MoneySource.Decor || s == MoneySource.Repair || s == MoneySource.Drones || s == MoneySource.Upkeep;
    }

    /// <summary>Новая сессия (мир загружен/создан): журнал с нуля, стартовый баланс — текущий.</summary>
    public static void Reset(int coins, int rubies)
    {
        minutes.Clear();
        playSeconds = 0f;
        startCoins = coins;
        startRubies = rubies;
        Current().coinsEnd = coins;
        Current().rubiesEnd = rubies;
    }

    static Minute Current()
    {
        int index = Mathf.FloorToInt(playSeconds / 60f);
        if (minutes.Count > 0 && minutes[minutes.Count - 1].index == index)
            return minutes[minutes.Count - 1];
        var m = new Minute { index = index };
        if (minutes.Count > 0)
        {
            Minute prev = minutes[minutes.Count - 1];
            m.coinsEnd = prev.coinsEnd;
            m.rubiesEnd = prev.rubiesEnd;
            m.buildings = prev.buildings;
        }
        minutes.Add(m);
        if (minutes.Count > MaxMinutes)
            minutes.RemoveAt(0);
        return m;
    }

    /// <summary>Изменение баланса: coins/rubies со знаком (+ пришло, − ушло).</summary>
    public static void Record(MoneySource source, int coins, int rubies)
    {
        if (coins == 0 && rubies == 0)
            return;
        Minute m = Current();
        int s = (int)source;
        bool expense = IsExpense(source);
        if (coins > 0)
        {
            if (expense)
                m.coinOut[s] -= coins;
            else
                m.coinIn[s] += coins;
        }
        else if (coins < 0)
            m.coinOut[s] += -coins;
        if (rubies > 0)
        {
            if (expense)
                m.rubyOut[s] -= rubies;
            else
                m.rubyIn[s] += rubies;
        }
        else if (rubies < 0)
            m.rubyOut[s] += -rubies;
    }

    /// <summary>Каждый кадр от кошелька: идёт игровое время, на конец минуты пишется баланс.</summary>
    public static void Tick(float dt, int coins, int rubies)
    {
        if (dt <= 0f)
            return;
        playSeconds += dt;
        Minute m = Current();
        m.coinsEnd = coins;
        m.rubiesEnd = rubies;
        m.buildings = WorldSim.Buildings.Count;
    }

    /// <summary>Сумма по источнику за последние n минут (n ≤ 0 — за всю сессию).</summary>
    public static void Sum(MoneySource source, int lastMinutes, out int coinIn, out int coinOut, out int rubyIn, out int rubyOut)
    {
        coinIn = coinOut = rubyIn = rubyOut = 0;
        int s = (int)source;
        int from = lastMinutes > 0 ? Mathf.FloorToInt(playSeconds / 60f) - lastMinutes + 1 : int.MinValue;
        for (int i = minutes.Count - 1; i >= 0; i--)
        {
            Minute m = minutes[i];
            if (m.index < from)
                break;
            coinIn += m.coinIn[s];
            coinOut += m.coinOut[s];
            rubyIn += m.rubyIn[s];
            rubyOut += m.rubyOut[s];
        }
    }

    /// <summary>Минут в окне (для «в минуту»): не больше прожитых в сессии.</summary>
    public static float WindowMinutes(int lastMinutes)
    {
        float played = Mathf.Max(1f / 60f, playSeconds / 60f);
        return lastMinutes > 0 ? Mathf.Min(lastMinutes, played) : played;
    }

    /// <summary>Короткая сводка для консоли.</summary>
    public static string Summary(int lastMinutes)
    {
        var sb = new StringBuilder();
        float win = WindowMinutes(lastMinutes);
        sb.Append(lastMinutes > 0 ? "за " + lastMinutes + " мин" : "за сессию").Append(" (")
            .Append((playSeconds / 60f).ToString("0.0", CultureInfo.InvariantCulture)).Append(" мин игры)\n");
        int totalIn = 0, totalOut = 0;
        foreach (MoneySource s in Enum.GetValues(typeof(MoneySource)))
        {
            Sum(s, lastMinutes, out int ci, out int co, out int ri, out int ro);
            if (ci == 0 && co == 0 && ri == 0 && ro == 0)
                continue;
            totalIn += ci;
            totalOut += co;
            sb.Append(s).Append(": +").Append(ci).Append(" −").Append(co);
            if (ri != 0 || ro != 0)
                sb.Append("  рубины +").Append(ri).Append(" −").Append(ro);
            sb.Append('\n');
        }
        sb.Append("итого: +").Append(totalIn).Append(" −").Append(totalOut)
            .Append("  (").Append((totalIn / win).ToString("0", CultureInfo.InvariantCulture)).Append("/мин дохода)");
        return sb.ToString();
    }
}
