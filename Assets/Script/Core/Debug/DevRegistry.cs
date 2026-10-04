using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

/// <summary>
/// Команда консоли ([[DevConsole]]): статический метод <c>string X(DevArgs a)</c> в [[DevCommands]].
/// Usage — синтаксис аргументов: <c>a|b</c> — слова на выбор, <c>&lt;item&gt;</c> — значение (подсказки по id),
/// <c>[...]</c> — необязательный аргумент, <c>...</c> — дальше что угодно. Атрибутов на методе может быть
/// несколько — по одному на вариант синтаксиса. Из них же строятся /help, автодополнение и проверка ввода.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class DevCommandAttribute : Attribute
{
    public string Name { get; }
    public string Usage { get; }
    public string Help { get; }
    /// <summary>Другие имена команды через запятую.</summary>
    public string Alias { get; set; }

    public DevCommandAttribute(string name, string usage, string help)
    {
        Name = name;
        Usage = usage ?? "";
        Help = help ?? "";
    }
}

/// <summary>Аргументы команды без её имени. Индексатор — в нижнем регистре, Raw — как ввели.</summary>
public sealed class DevArgs
{
    readonly string[] args;

    public string Command { get; }
    public int Count => args.Length;

    public DevArgs(string command, string[] args)
    {
        Command = command;
        this.args = args ?? Array.Empty<string>();
    }

    public string this[int i] => Has(i) ? args[i].ToLowerInvariant() : "";
    public string Raw(int i) => Has(i) ? args[i] : "";
    public bool Has(int i) => i >= 0 && i < args.Length;

    public bool Is(int i, params string[] options)
    {
        string v = this[i];
        for (int k = 0; k < options.Length; k++)
        {
            if (v == options[k])
                return true;
        }

        return false;
    }

    public bool TryInt(int i, out int value)
    {
        return int.TryParse(Raw(i), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    public bool TryFloat(int i, out float value)
    {
        return DevRegistry.TryNumber(Raw(i), out value);
    }

    /// <summary>Аргументы с i-го до конца через пробел.</summary>
    public string From(int i)
    {
        if (!Has(i))
            return "";
        return string.Join(" ", args, i, args.Length - i);
    }
}

/// <summary>Реестр команд: поиск по атрибутам, подсказки, проверка, справка.</summary>
public static class DevRegistry
{
    public sealed class Tok
    {
        public readonly List<string> words = new List<string>(4);
        public readonly List<string> holes = new List<string>(2);
        public bool optional;
        public bool rest;
    }

    public sealed class Spec
    {
        public string usage;
        public string help;
        public readonly List<Tok> toks = new List<Tok>(4);
    }

    public sealed class Entry
    {
        public string name;
        public string key;
        public readonly List<string> aliases = new List<string>(2);
        public Func<DevArgs, string> run;
        public readonly List<Spec> specs = new List<Spec>(2);

        public string Help
        {
            get
            {
                for (int i = 0; i < specs.Count; i++)
                {
                    if (!string.IsNullOrEmpty(specs[i].help))
                        return specs[i].help;
                }

                return "";
            }
        }
    }

    public struct Match
    {
        public string line;
        public string rich;
        public int score;
    }

    public const string HintColor = "#FFD25A";

    static List<Entry> entries;
    static Dictionary<string, Entry> byKey;

    public static IReadOnlyList<Entry> Entries
    {
        get
        {
            Ensure();
            return entries;
        }
    }

    static void Ensure()
    {
        if (entries != null)
            return;
        entries = new List<Entry>(96);
        byKey = new Dictionary<string, Entry>(128);
        MethodInfo[] methods = typeof(DevCommands).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        for (int m = 0; m < methods.Length; m++)
        {
            object[] attrs = methods[m].GetCustomAttributes(typeof(DevCommandAttribute), false);
            if (attrs.Length == 0)
                continue;
            ParameterInfo[] ps = methods[m].GetParameters();
            if (ps.Length != 1 || ps[0].ParameterType != typeof(DevArgs) || methods[m].ReturnType != typeof(string))
            {
                UnityEngine.Debug.LogError("[DevConsole] Команда " + methods[m].Name + " должна быть string X(DevArgs a)");
                continue;
            }

            var run = (Func<DevArgs, string>)Delegate.CreateDelegate(typeof(Func<DevArgs, string>), methods[m]);
            for (int a = 0; a < attrs.Length; a++)
                Add((DevCommandAttribute)attrs[a], run);
        }

        entries.Sort((x, y) => string.CompareOrdinal(x.key, y.key));
    }

    static void Add(DevCommandAttribute attr, Func<DevArgs, string> run)
    {
        string key = attr.Name.ToLowerInvariant();
        if (!byKey.TryGetValue(key, out Entry entry))
        {
            entry = new Entry { name = attr.Name, key = key, run = run };
            entries.Add(entry);
            byKey[key] = entry;
        }

        if (!string.IsNullOrEmpty(attr.Alias))
        {
            string[] names = attr.Alias.Split(',');
            for (int i = 0; i < names.Length; i++)
            {
                string alias = names[i].Trim().ToLowerInvariant();
                if (alias.Length == 0 || byKey.ContainsKey(alias))
                    continue;
                entry.aliases.Add(alias);
                byKey[alias] = entry;
            }
        }

        entry.specs.Add(Parse(attr.Usage, attr.Help));
    }

    static Spec Parse(string usage, string help)
    {
        var spec = new Spec { usage = usage, help = help };
        string[] parts = usage.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i];
            var tok = new Tok();
            if (p == "...")
            {
                tok.rest = true;
                tok.optional = true;
                spec.toks.Add(tok);
                continue;
            }

            if (p.StartsWith("[") && p.EndsWith("]"))
            {
                tok.optional = true;
                p = p.Substring(1, p.Length - 2);
            }

            string[] alts = p.Split('|');
            for (int k = 0; k < alts.Length; k++)
            {
                string alt = alts[k];
                if (alt.StartsWith("<") && alt.EndsWith(">"))
                    tok.holes.Add(alt.Substring(1, alt.Length - 2).ToLowerInvariant());
                else if (alt.Length > 0)
                    tok.words.Add(alt.ToLowerInvariant());
            }

            spec.toks.Add(tok);
        }

        return spec;
    }

    public static Entry Find(string name)
    {
        Ensure();
        if (string.IsNullOrEmpty(name))
            return null;
        byKey.TryGetValue(name.ToLowerInvariant(), out Entry entry);
        return entry;
    }

    // ---------- Разбор строки ----------

    /// <summary>Команда без «/», слова и недописанный хвост (пусто, если строка кончается пробелом).</summary>
    public static void SplitInput(string text, List<string> done, out string partial)
    {
        done.Clear();
        string t = (text ?? "").TrimStart();
        if (t.StartsWith("/"))
            t = t.Substring(1);
        bool trailing = t.Length > 0 && t[t.Length - 1] == ' ';
        string[] parts = t.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int n = trailing ? parts.Length : parts.Length - 1;
        for (int i = 0; i < n; i++)
            done.Add(parts[i]);
        partial = trailing || parts.Length == 0 ? "" : parts[parts.Length - 1];
    }

    /// <summary>Позиции токенов spec, куда может встать следующий аргумент после args[0..].</summary>
    static void Advance(Spec spec, IList<string> args, int ti, int ai, HashSet<int> into)
    {
        if (ti < spec.toks.Count && spec.toks[ti].rest)
        {
            into.Add(ti);
            return;
        }

        if (ai == args.Count)
        {
            into.Add(ti);
            if (ti < spec.toks.Count && spec.toks[ti].optional)
                Advance(spec, args, ti + 1, ai, into);
            return;
        }

        if (ti >= spec.toks.Count)
            return;
        Tok tok = spec.toks[ti];
        if (Fits(tok, args[ai]))
            Advance(spec, args, ti + 1, ai + 1, into);
        if (tok.optional)
            Advance(spec, args, ti + 1, ai, into);
    }

    static bool Fits(Tok tok, string value)
    {
        string v = value.ToLowerInvariant();
        for (int i = 0; i < tok.words.Count; i++)
        {
            if (tok.words[i] == v)
                return true;
        }

        for (int i = 0; i < tok.holes.Count; i++)
        {
            if (HoleAccepts(tok.holes[i], value))
                return true;
        }

        return false;
    }

    static bool IsNumberHole(string hole)
    {
        switch (hole)
        {
            case "n":
            case "x":
            case "r":
            case "z":
            case "deg":
            case "frames":
            case "min":
            case "h":
                return true;
            default:
                return false;
        }
    }

    static bool HoleAccepts(string hole, string value)
    {
        if (IsNumberHole(hole))
            return TryNumber(value, out _);
        if (!DevCommands.HoleIsStrict(hole))
            return true;
        IList<string> ids = DevCommands.HoleValues(hole);
        if (ids == null)
            return true;
        for (int i = 0; i < ids.Count; i++)
        {
            if (string.Equals(ids[i], value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static bool TryNumber(string raw, out float value)
    {
        value = 0f;
        if (string.IsNullOrEmpty(raw))
            return false;
        string s = raw.Trim().TrimEnd('%', 'x', 'X').Replace(',', '.');
        return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    // ---------- Подсказки ----------

    /// <summary>Строки-продолжения для ввода (без «/»), лучшие первыми. rich — с подсветкой совпавших букв.</summary>
    public static void Suggest(string text, List<Match> into)
    {
        Ensure();
        into.Clear();
        var done = new List<string>(8);
        SplitInput(text, done, out string partial);
        if (done.Count == 0)
        {
            SuggestCommands(partial, into);
            return;
        }

        Entry entry = Find(done[0]);
        if (entry == null)
            return;
        var args = done.GetRange(1, done.Count - 1);
        string head = string.Join(" ", done) + " ";
        var seen = new HashSet<string>();
        var hits = new List<int>(16);
        for (int s = 0; s < entry.specs.Count; s++)
        {
            var at = new HashSet<int>();
            Advance(entry.specs[s], args, 0, 0, at);
            foreach (int ti in at)
            {
                if (ti >= entry.specs[s].toks.Count)
                    continue;
                Tok tok = entry.specs[s].toks[ti];
                for (int w = 0; w < tok.words.Count; w++)
                    Offer(head, tok.words[w], partial, seen, hits, into);
                for (int h = 0; h < tok.holes.Count; h++)
                {
                    IList<string> ids = DevCommands.HoleValues(tok.holes[h]);
                    if (ids == null)
                        continue;
                    for (int k = 0; k < ids.Count; k++)
                        Offer(head, ids[k], partial, seen, hits, into);
                }
            }
        }

        into.Sort((a, b) => b.score != a.score ? b.score.CompareTo(a.score) : string.CompareOrdinal(a.line, b.line));
    }

    static void Offer(string head, string value, string partial, HashSet<string> seen, List<int> hits, List<Match> into)
    {
        if (string.IsNullOrEmpty(value))
            return;
        int score = Fuzzy(value, partial, hits);
        if (score < 0)
            return;
        string line = head + value;
        if (!seen.Add(line.ToLowerInvariant()))
            return;
        into.Add(new Match { line = line, rich = Escape(head) + Highlight(value, hits), score = score });
    }

    static void SuggestCommands(string partial, List<Match> into)
    {
        var hits = new List<int>(16);
        var seen = new HashSet<string>();
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            int score = Fuzzy(e.name, partial, hits);
            int aliasScore = -1;
            for (int a = 0; a < e.aliases.Count && score < 0; a++)
                aliasScore = Math.Max(aliasScore, Fuzzy(e.aliases[a], partial, null));
            if (score >= 0)
            {
                seen.Add(e.key);
                into.Add(new Match { line = e.name, rich = Highlight(e.name, hits), score = score + 5 });
            }
            else if (aliasScore >= 0)
            {
                seen.Add(e.key);
                into.Add(new Match { line = e.name, rich = Escape(e.name), score = aliasScore });
            }
        }

        // «rsk» → «research skip»: команда вместе с первым словом.
        if (partial.Length >= 2)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                for (int s = 0; s < e.specs.Count; s++)
                {
                    if (e.specs[s].toks.Count == 0)
                        continue;
                    Tok first = e.specs[s].toks[0];
                    for (int w = 0; w < first.words.Count; w++)
                    {
                        string line = e.name + " " + first.words[w];
                        if (!seen.Add(line.ToLowerInvariant()))
                            continue;
                        int score = Fuzzy(line, partial, hits);
                        if (score >= 0 && Fuzzy(e.name, partial, null) < 0)
                            into.Add(new Match { line = line, rich = Highlight(line, hits), score = score - 50 });
                    }
                }
            }
        }

        into.Sort((a, b) => b.score != a.score ? b.score.CompareTo(a.score) : string.CompareOrdinal(a.line, b.line));
    }

    /// <summary>
    /// Нечёткое совпадение: префикс лучше всего, потом подстрока, потом буквы по порядку
    /// (с бонусом за начала слов). -1 — не подходит. hits — индексы совпавших букв.
    /// </summary>
    public static int Fuzzy(string candidate, string query, List<int> hits)
    {
        hits?.Clear();
        if (string.IsNullOrEmpty(query))
            return 1;
        if (string.IsNullOrEmpty(candidate))
            return -1;
        string c = candidate.ToLowerInvariant();
        string q = query.ToLowerInvariant();
        if (c.StartsWith(q, StringComparison.Ordinal))
        {
            if (hits != null)
            {
                for (int i = 0; i < q.Length; i++)
                    hits.Add(i);
            }

            return 1000 - c.Length;
        }

        int at = c.IndexOf(q, StringComparison.Ordinal);
        if (at >= 0)
        {
            if (hits != null)
            {
                for (int i = 0; i < q.Length; i++)
                    hits.Add(at + i);
            }

            return 700 - at * 4 - c.Length;
        }

        int score = 400;
        int ci = 0;
        int last = -1;
        for (int qi = 0; qi < q.Length; qi++)
        {
            char want = q[qi];
            if (want == ' ')
                continue;
            int found = -1;
            for (; ci < c.Length; ci++)
            {
                if (c[ci] == want)
                {
                    found = ci;
                    ci++;
                    break;
                }
            }

            if (found < 0)
            {
                hits?.Clear();
                return -1;
            }

            bool wordStart = found == 0 || c[found - 1] == ' ' || c[found - 1] == '_';
            score += wordStart ? 12 : 0;
            score -= last >= 0 ? (found - last - 1) * 3 : found * 2;
            last = found;
            hits?.Add(found);
        }

        return Math.Max(0, score - c.Length);
    }

    static string Highlight(string text, List<int> hits)
    {
        if (hits == null || hits.Count == 0)
            return Escape(text);
        var sb = new StringBuilder(text.Length + hits.Count * 24);
        int h = 0;
        for (int i = 0; i < text.Length; i++)
        {
            bool on = h < hits.Count && hits[h] == i;
            if (on)
            {
                sb.Append("<color=").Append(HintColor).Append("><b>").Append(EscapeChar(text[i])).Append("</b></color>");
                h++;
            }
            else
                sb.Append(EscapeChar(text[i]));
        }

        return sb.ToString();
    }

    public static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('<') < 0)
            return text ?? "";
        return text.Replace("<", "<noparse><</noparse>");
    }

    static string EscapeChar(char ch)
    {
        return ch == '<' ? "<noparse><</noparse>" : ch.ToString();
    }

    /// <summary>Есть ли у строки продолжение (следующий аргумент) — тогда после вставки ставится пробел.</summary>
    public static bool WantsMore(string line)
    {
        Ensure();
        var done = new List<string>(8);
        SplitInput(line + " ", done, out _);
        if (done.Count == 0)
            return false;
        Entry entry = Find(done[0]);
        if (entry == null)
            return false;
        var args = done.GetRange(1, done.Count - 1);
        for (int s = 0; s < entry.specs.Count; s++)
        {
            var at = new HashSet<int>();
            Advance(entry.specs[s], args, 0, 0, at);
            foreach (int ti in at)
            {
                if (ti < entry.specs[s].toks.Count)
                    return true;
            }
        }

        return false;
    }

    // ---------- Подсказка по аргументам и проверка ----------

    /// <summary>
    /// Синтаксис команды под вводом с подсвеченным текущим аргументом и описанием.
    /// bad — ввод уже не подходит ни под один вариант (неизвестная команда или аргумент).
    /// </summary>
    public static string Hint(string text, out bool bad)
    {
        Ensure();
        bad = false;
        var done = new List<string>(8);
        SplitInput(text, done, out string partial);
        string name = done.Count > 0 ? done[0] : partial;
        if (string.IsNullOrEmpty(name))
            return "<color=#7C8A7C>Tab — дополнить · ↑↓ — подсказки/история · Enter — выполнить · ; — несколько команд</color>";

        Entry entry = Find(name);
        if (entry == null)
        {
            if (done.Count > 0)
            {
                bad = true;
                return "<color=#FF7A6A>нет команды «" + Escape(name) + "»</color>";
            }

            return "";
        }

        var args = done.Count > 0 ? done.GetRange(1, done.Count - 1) : new List<string>();
        bool typingName = done.Count == 0;
        var sb = new StringBuilder(128);
        int shown = 0;
        for (int s = 0; s < entry.specs.Count; s++)
        {
            Spec spec = entry.specs[s];
            var at = new HashSet<int>();
            Advance(spec, args, 0, 0, at);
            if (at.Count == 0)
                continue;
            int cur = int.MaxValue;
            foreach (int ti in at)
                cur = Math.Min(cur, ti);
            if (shown > 0)
                sb.Append('\n');
            sb.Append("<color=#9FB89F>/").Append(Escape(entry.name)).Append("</color>");
            for (int t = 0; t < spec.toks.Count; t++)
            {
                string word = UsageWord(spec, t);
                bool on = !typingName && t == cur;
                sb.Append(' ');
                if (on)
                    sb.Append("<color=").Append(HintColor).Append("><b>").Append(Escape(word)).Append("</b></color>");
                else
                    sb.Append("<color=#7C8A7C>").Append(Escape(word)).Append("</color>");
            }

            if (!string.IsNullOrEmpty(spec.help))
                sb.Append("  <color=#6A786A>— ").Append(Escape(spec.help)).Append("</color>");
            shown++;
            if (shown >= 4)
                break;
        }

        if (shown == 0)
        {
            bad = true;
            return "<color=#FF7A6A>не подходит:</color> " + Escape(Usage(entry));
        }

        return sb.ToString();
    }

    static string UsageWord(Spec spec, int t)
    {
        string[] parts = spec.usage.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return t < parts.Length ? parts[t] : "";
    }

    /// <summary>Все варианты синтаксиса команды одной строкой.</summary>
    public static string Usage(Entry entry)
    {
        if (entry == null)
            return "";
        var sb = new StringBuilder(64);
        for (int i = 0; i < entry.specs.Count; i++)
        {
            if (i > 0)
                sb.Append("  |  ");
            sb.Append('/').Append(entry.name);
            if (entry.specs[i].usage.Length > 0)
                sb.Append(' ').Append(entry.specs[i].usage);
        }

        return sb.ToString();
    }

    /// <summary>Подробно про одну команду: все варианты и описания.</summary>
    public static string Describe(Entry entry)
    {
        var sb = new StringBuilder(128);
        sb.Append('/').Append(entry.name);
        if (entry.aliases.Count > 0)
            sb.Append("  (также: ").Append(string.Join(", ", entry.aliases)).Append(')');
        for (int i = 0; i < entry.specs.Count; i++)
        {
            sb.Append("\n  /").Append(entry.name);
            if (entry.specs[i].usage.Length > 0)
                sb.Append(' ').Append(entry.specs[i].usage);
            if (!string.IsNullOrEmpty(entry.specs[i].help))
                sb.Append("  — ").Append(entry.specs[i].help);
        }

        return sb.ToString();
    }
}
