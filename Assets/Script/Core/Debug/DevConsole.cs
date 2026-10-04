using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Консоль разработчика: тройное ` или ' — открыть, Esc — закрыть. Команды — [[DevCommands]], реестр — [[DevRegistry]].
/// Подсказки с нечётким поиском (↑↓ — выбор, Tab/Enter — вставить), синтаксис текущей команды, проверка ввода,
/// цветной лог с повторами «×N», кнопки в строках, копирование по клику, история между сессиями,
/// «;» — несколько команд, autoexec.cfg — при входе в мир. Пока консоль закрыта, новые строки видны в углу.
/// </summary>
public class DevConsole : MonoBehaviour
{
    public enum Kind { Info, Input, Ok, Warn, Error, System }

    const float TripleWindow = 0.55f;
    const int HistoryCap = 300;
    const int LogCap = 400;
    const int SuggestShown = 12;
    const int OverlayCap = 5;
    const long OverlayMs = 6000;
    const string HistoryKey = "DevConsoleHistory";
    const string AutoexecFile = "autoexec.cfg";

    static readonly Regex ButtonRx = new Regex(@"\[\[([^\|\]]+)\|([^\]]+)\]\]", RegexOptions.Compiled);

    public static bool IsOpen { get; private set; }

    /// <summary>Файл, который выполняется при каждом входе в мир.</summary>
    public static string AutoexecPath => Path.Combine(Application.persistentDataPath, AutoexecFile);

    static DevConsole instance;

    sealed class Row
    {
        public VisualElement el;
        public Label count;
        public string plain;
        public Kind kind;
        public int repeats = 1;
    }

    VisualElement panel;
    ScrollView logView;
    Label newButton;
    ScrollView suggest;
    Label hint;
    VisualElement inputWrap;
    TextField field;
    Label ghost;
    VisualElement overlay;

    readonly List<Row> rows = new List<Row>(LogCap);
    readonly List<string> history = new List<string>(64);
    readonly List<DevRegistry.Match> matches = new List<DevRegistry.Match>(32);
    readonly List<Label> suggestRows = new List<Label>(SuggestShown);
    int historyIndex = -1;
    bool browsingHistory;
    int pick;
    bool picked;
    bool stickToBottom = true;
    bool closeRequested;
    bool worldReady;
    int tapCount;
    float lastTap;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Ensure();
    }

    public static DevConsole Ensure()
    {
        if (instance != null)
            return instance;
        var go = new GameObject("DevConsole");
        Object.DontDestroyOnLoad(go);
        instance = go.AddComponent<DevConsole>();
        return instance;
    }

    /// <summary>Строка в лог консоли (видна и при закрытой консоли — в углу экрана).</summary>
    public static void Log(string text, Kind kind = Kind.System)
    {
        Ensure().Print(text, kind);
    }

    /// <summary>Закрыть консоль на следующем кадре (команда открыла своё окно).</summary>
    public static void CloseSoon()
    {
        if (instance != null)
            instance.closeRequested = true;
    }

    /// <summary>Весь лог простым текстом (для /bugreport и «копировать всё»).</summary>
    public static string LogText()
    {
        if (instance == null)
            return "";
        var sb = new StringBuilder(4096);
        for (int i = 0; i < instance.rows.Count; i++)
        {
            Row r = instance.rows[i];
            sb.Append(r.plain);
            if (r.repeats > 1)
                sb.Append("  ×").Append(r.repeats);
            sb.Append('\n');
        }

        return sb.ToString();
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        DevRuntime.Ensure(gameObject);
        LoadHistory();
        BuildUi();
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            SaveHistory();
            instance = null;
            IsOpen = false;
        }
    }

    void OnApplicationQuit()
    {
        SaveHistory();
    }

    void Update()
    {
        if (closeRequested)
        {
            closeRequested = false;
            if (IsOpen)
                SetOpen(false);
        }

        TickAutoexec();

        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;
        if (kb.backquoteKey.wasPressedThisFrame || kb.quoteKey.wasPressedThisFrame)
        {
            if (IsOpen)
                return;
            float now = Time.unscaledTime;
            if (now - lastTap > TripleWindow)
                tapCount = 0;
            tapCount++;
            lastTap = now;
            if (tapCount >= 3)
            {
                tapCount = 0;
                SetOpen(true);
            }
        }

        if (IsOpen && kb.escapeKey.wasPressedThisFrame)
            SetOpen(false);
    }

    // ---------- Интерфейс ----------

    void BuildUi()
    {
        VisualElement host = IndustryUi.Mount(this, 980);
        host.pickingMode = PickingMode.Ignore;

        overlay = IndustryUi.El("DevOverlay", "dev-console-overlay");
        overlay.pickingMode = PickingMode.Ignore;
        host.Add(overlay);

        panel = IndustryUi.El("DevConsole", "dev-console");
        panel.pickingMode = PickingMode.Position;

        var head = IndustryUi.El("Head", "dev-console-head");
        head.Add(IndustryUi.Text("Title", "DEV", "dev-console-title"));
        var spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        head.Add(spacer);
        head.Add(HeadButton("копировать всё", CopyAll));
        head.Add(HeadButton("/help", () => Execute("/help")));
        head.Add(HeadButton("autoexec", AutoexecInfo));

        var logWrap = IndustryUi.El("LogWrap", "dev-console-log-wrap");
        logView = IndustryUi.Scroll("DevLog");
        logView.AddToClassList("dev-console-log");
        logView.verticalScroller.valueChanged += _ => OnLogScrolled();
        logView.contentContainer.RegisterCallback<GeometryChangedEvent>(_ =>
        {
            if (stickToBottom)
                logView.schedule.Execute(ScrollToEnd);
        });
        newButton = IndustryUi.Text("New", "↓ новые сообщения", "dev-console-new");
        newButton.pickingMode = PickingMode.Position;
        newButton.RegisterCallback<ClickEvent>(_ =>
        {
            stickToBottom = true;
            ScrollToEnd();
            IndustryUi.Show(newButton, false);
        });
        IndustryUi.Show(newButton, false);
        logWrap.Add(logView);
        logWrap.Add(newButton);

        suggest = IndustryUi.Scroll("Suggest");
        suggest.AddToClassList("dev-console-suggest");
        suggest.pickingMode = PickingMode.Position;

        hint = IndustryUi.Text("Hint", "", "dev-console-hint");
        hint.enableRichText = true;

        inputWrap = IndustryUi.El("CmdWrap", "dev-console-input-wrap");
        ghost = IndustryUi.Text("Ghost", "", "dev-console-ghost");
        ghost.pickingMode = PickingMode.Ignore;
        field = new TextField { name = "Cmd" };
        field.AddToClassList("dev-console-input");
        field.value = "/";
        inputWrap.Add(ghost);
        inputWrap.Add(field);

        panel.Add(head);
        panel.Add(logWrap);
        panel.Add(suggest);
        panel.Add(hint);
        panel.Add(inputWrap);
        host.Add(panel);
        IndustryUi.Show(panel, false);

        field.RegisterCallback<KeyDownEvent>(OnFieldKey, TrickleDown.TrickleDown);
        // Tab и стрелки — наши (подсказки, история), а не переход фокуса на другой элемент.
        field.RegisterCallback<NavigationMoveEvent>(evt =>
        {
            evt.StopPropagation();
            field.focusController?.IgnoreEvent(evt);
        }, TrickleDown.TrickleDown);
        field.RegisterValueChangedCallback(evt =>
        {
            browsingHistory = false;
            historyIndex = history.Count;
            pick = 0;
            picked = false;
            RefreshHints(evt.newValue);
        });
    }

    static Label HeadButton(string text, System.Action onClick)
    {
        Label b = IndustryUi.Text("HeadBtn", text, "dev-console-btn", "dev-console-head-btn");
        b.pickingMode = PickingMode.Position;
        b.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            onClick();
        });
        return b;
    }

    void SetOpen(bool on)
    {
        IsOpen = on;
        IndustryUi.Show(panel, on);
        panel.pickingMode = on ? PickingMode.Position : PickingMode.Ignore;
        if (on)
        {
            overlay.Clear();
            browsingHistory = false;
            historyIndex = history.Count;
            SetField("/");
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.cursorIndex = 1;
                field.selectIndex = 1;
            });
            stickToBottom = true;
            logView.schedule.Execute(ScrollToEnd);
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            KeybindStore.SetPlayerMapEnabled(false);
        }
        else
        {
            SaveHistory();
            KeybindStore.SetPlayerMapEnabled(true);
            if (GameManager.Instance != null)
                GameManager.Instance.RestoreGameplayFocus();
        }
    }

    // ---------- Ввод ----------

    void OnFieldKey(KeyDownEvent evt)
    {
        // Символьная половина Tab/Enter — не вставлять в строку.
        if (evt.keyCode == KeyCode.None && (evt.character == '\t' || evt.character == '\n' || evt.character == '\r'))
        {
            evt.StopImmediatePropagation();
            return;
        }

        switch (evt.keyCode)
        {
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
                if (picked && pick < VisibleCount)
                    Accept(pick);
                else
                    Submit();
                evt.StopImmediatePropagation();
                return;
            case KeyCode.Tab:
                Accept(pick);
                evt.StopImmediatePropagation();
                return;
            case KeyCode.UpArrow:
            case KeyCode.DownArrow:
                int dir = evt.keyCode == KeyCode.UpArrow ? -1 : 1;
                if (UseHistoryKeys(evt))
                    History(dir);
                else
                    MovePick(dir);
                evt.StopImmediatePropagation();
                return;
        }
    }

    /// <summary>↑↓ листают историю: с Ctrl, при пустой строке, во время листания и когда подсказок нет.</summary>
    bool UseHistoryKeys(KeyDownEvent evt)
    {
        if (evt.ctrlKey || browsingHistory || VisibleCount == 0)
            return true;
        SplitChain(field.value, out _, out string seg);
        return seg.Trim().Length == 0 || seg.Trim() == "/";
    }

    int VisibleCount => Mathf.Min(SuggestShown, matches.Count);

    void MovePick(int dir)
    {
        int n = VisibleCount;
        if (n == 0)
            return;
        // Первая подсказка подсвечена и так (её берёт Tab): первое ↓ — сразу на вторую.
        pick = picked ? (pick + dir + n) % n : (dir > 0 ? Mathf.Min(1, n - 1) : n - 1);
        picked = true;
        PaintPick();
    }

    void PaintPick()
    {
        for (int i = 0; i < suggestRows.Count; i++)
            IndustryUi.SetOn(suggestRows[i], i == pick, "is-on");
        // Только когда выбирают стрелками: сразу после пересборки списка разметки ещё нет.
        if (picked && pick < suggestRows.Count)
            suggest.ScrollTo(suggestRows[pick]);
        RefreshGhost();
    }

    void Accept(int index)
    {
        RefreshHints(field.value);
        if (index < 0 || index >= matches.Count)
            return;
        SplitChain(field.value, out string head, out _);
        string line = matches[index].line;
        string text = Prefix(head) + line;
        if (DevRegistry.WantsMore(line))
            text += " ";
        SetField(text);
    }

    /// <summary>
    /// Текст строки из кода. Без события изменения: во время обработки клавиши оно приходит позже
    /// и сбрасывало бы листание истории.
    /// </summary>
    void SetField(string text)
    {
        field.SetValueWithoutNotify(text);
        field.cursorIndex = text.Length;
        field.selectIndex = text.Length;
        pick = 0;
        picked = false;
        RefreshHints(text);
    }

    void History(int dir)
    {
        if (history.Count == 0)
            return;
        browsingHistory = true;
        historyIndex = Mathf.Clamp(historyIndex + dir, 0, history.Count);
        SetField(historyIndex >= history.Count ? "/" : history[historyIndex]);
    }

    void Submit()
    {
        string line = field.value != null ? field.value.Trim() : "";
        if (line.Length == 0 || line == "/")
            return;
        AddHistory(line);
        Print("> " + line, Kind.Input);
        stickToBottom = true;
        RunLine(line);
        browsingHistory = false;
        historyIndex = history.Count;
        SetField("/");
    }

    /// <summary>Выполнить строку (можно несколько команд через «;») и вывести результаты.</summary>
    void RunLine(string line)
    {
        string[] parts = line.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            string seg = parts[i].Trim();
            if (seg.Length == 0 || seg == "/")
                continue;
            PrintResult(DevCommands.Run(seg));
        }
    }

    /// <summary>Команда с кнопки в логе — как будто ввели руками.</summary>
    void Execute(string command)
    {
        Print("> " + command, Kind.Input);
        stickToBottom = true;
        RunLine(command);
        RefreshHints(field.value);
    }

    static void SplitChain(string text, out string head, out string seg)
    {
        text = text ?? "";
        int i = text.LastIndexOf(';');
        if (i < 0)
        {
            head = "";
            seg = text;
            return;
        }

        string rest = text.Substring(i + 1);
        seg = rest.TrimStart();
        head = text.Substring(0, i + 1 + rest.Length - seg.Length);
    }

    /// <summary>Что ставится перед вставленной подсказкой: «/» или предыдущие команды цепочки с пробелом.</summary>
    static string Prefix(string head)
    {
        if (head.Length == 0)
            return "/";
        return head.EndsWith(" ") ? head : head + " ";
    }

    // ---------- Подсказки ----------

    void RefreshHints(string text)
    {
        SplitChain(text, out string head, out string seg);
        DevRegistry.Suggest(seg, matches);
        if (pick >= VisibleCount)
            pick = 0;

        if (DevCommands.AwaitingConfirm)
        {
            hint.text = "<color=" + DevRegistry.HintColor + ">ждёт подтверждения: yes / да — выполнить, другое — отмена</color>";
            IndustryUi.SetOn(inputWrap, false, "is-bad");
        }
        else
        {
            hint.text = DevRegistry.Hint(seg, out bool bad);
            IndustryUi.SetOn(inputWrap, bad, "is-bad");
        }

        IndustryUi.Show(hint, !string.IsNullOrEmpty(hint.text));

        suggest.Clear();
        suggestRows.Clear();
        int n = VisibleCount;
        var done = new List<string>(4);
        DevRegistry.SplitInput(seg, done, out _);
        bool commandLevel = done.Count == 0;
        for (int i = 0; i < n; i++)
        {
            DevRegistry.Match m = matches[i];
            string rich = "/" + m.rich;
            if (commandLevel)
            {
                int space = m.line.IndexOf(' ');
                DevRegistry.Entry e = DevRegistry.Find(space > 0 ? m.line.Substring(0, space) : m.line);
                if (e != null && !string.IsNullOrEmpty(e.Help))
                    rich += "   <color=#6A786A>" + DevRegistry.Escape(e.Help) + "</color>";
            }

            Label row = IndustryUi.Text("S", rich, "dev-console-suggest-line");
            row.enableRichText = true;
            row.pickingMode = PickingMode.Position;
            int index = i;
            row.RegisterCallback<ClickEvent>(_ =>
            {
                Accept(index);
                field.Focus();
            });
            suggest.Add(row);
            suggestRows.Add(row);
        }

        PaintPick();
        IndustryUi.Show(suggest, n > 0);
    }

    void RefreshGhost()
    {
        if (ghost == null)
            return;
        string typed = field.value ?? "";
        SplitChain(typed, out string head, out string seg);
        int index = pick < VisibleCount ? pick : 0;
        string best = matches.Count > index ? matches[index].line : "";
        string segBody = seg.StartsWith("/") ? seg.Substring(1) : seg;
        bool show = best.Length > segBody.Length && best.StartsWith(segBody, System.StringComparison.OrdinalIgnoreCase);
        ghost.text = show ? head + seg + best.Substring(segBody.Length) : "";
    }

    // ---------- Лог ----------

    void PrintResult(string result)
    {
        if (string.IsNullOrEmpty(result))
            return;
        Kind kind = Kind.Info;
        if (result.Length >= 2 && result[0] == DevCommands.Mark)
        {
            switch (result[1])
            {
                case 'e': kind = Kind.Error; break;
                case 'w': kind = Kind.Warn; break;
                case 'o': kind = Kind.Ok; break;
                case 's': kind = Kind.System; break;
            }

            result = result.Substring(2);
        }

        Print(result, kind);
    }

    public void Print(string text, Kind kind = Kind.Info)
    {
        if (string.IsNullOrEmpty(text) || logView == null)
            return;
        bool wasAtBottom = AtBottom();
        string[] parts = text.Replace("\r", "").Split('\n');
        for (int i = 0; i < parts.Length; i++)
            AddLine(parts[i], kind);
        while (rows.Count > LogCap)
        {
            logView.Remove(rows[0].el);
            rows.RemoveAt(0);
        }

        if (stickToBottom || wasAtBottom)
        {
            stickToBottom = true;
            logView.schedule.Execute(ScrollToEnd);
        }
        else
            IndustryUi.Show(newButton, true);
    }

    void AddLine(string line, Kind kind)
    {
        string plain = ButtonRx.Replace(line, "[$1]");
        Row last = rows.Count > 0 ? rows[rows.Count - 1] : null;
        if (last != null && last.kind == kind && last.plain == plain && kind != Kind.Input)
        {
            last.repeats++;
            last.count.text = "×" + last.repeats;
            IndustryUi.Show(last.count, true);
            if (!IsOpen)
                ShowOverlay(plain + "  ×" + last.repeats, kind);
            return;
        }

        var row = new Row { plain = plain, kind = kind };
        row.el = IndustryUi.El("L", "dev-console-row", KindClass(kind));
        row.el.pickingMode = PickingMode.Position;
        int at = 0;
        foreach (Match m in ButtonRx.Matches(line))
        {
            if (m.Index > at)
                row.el.Add(LineText(line.Substring(at, m.Index - at)));
            row.el.Add(LineButton(m.Groups[1].Value, m.Groups[2].Value));
            at = m.Index + m.Length;
        }

        if (at < line.Length || at == 0)
            row.el.Add(LineText(line.Substring(at)));
        row.count = IndustryUi.Text("Count", "", "dev-console-count");
        IndustryUi.Show(row.count, false);
        row.el.Add(row.count);
        Row captured = row;
        row.el.RegisterCallback<ClickEvent>(_ => CopyRow(captured));
        logView.Add(row.el);
        rows.Add(row);
        if (!IsOpen)
            ShowOverlay(plain, kind);
    }

    static Label LineText(string text)
    {
        Label label = IndustryUi.Text("T", DevRegistry.Escape(text), "dev-console-line");
        label.enableRichText = true;
        label.pickingMode = PickingMode.Ignore;
        return label;
    }

    Label LineButton(string label, string command)
    {
        Label b = IndustryUi.Text("B", label, "dev-console-btn");
        b.pickingMode = PickingMode.Position;
        b.tooltip = command + "  (Shift — вставить в строку)";
        b.RegisterCallback<ClickEvent>(evt =>
        {
            evt.StopPropagation();
            if ((evt.modifiers & EventModifiers.Shift) != 0)
            {
                SetField(command);
                field.Focus();
                return;
            }

            Execute(command);
        });
        return b;
    }

    static string KindClass(Kind kind)
    {
        switch (kind)
        {
            case Kind.Input: return "is-in";
            case Kind.Ok: return "is-ok";
            case Kind.Warn: return "is-warn";
            case Kind.Error: return "is-err";
            case Kind.System: return "is-sys";
            default: return "is-info";
        }
    }

    void CopyRow(Row row)
    {
        GUIUtility.systemCopyBuffer = row.repeats > 1 ? row.plain + "  ×" + row.repeats : row.plain;
        row.el.AddToClassList("is-copied");
        row.el.schedule.Execute(() => row.el.RemoveFromClassList("is-copied")).ExecuteLater(450);
    }

    void CopyAll()
    {
        GUIUtility.systemCopyBuffer = LogText();
        Print("лог скопирован: " + rows.Count + " строк", Kind.System);
    }

    bool AtBottom()
    {
        if (logView == null)
            return true;
        float max = logView.contentContainer.layout.height - logView.contentViewport.layout.height;
        return float.IsNaN(max) || max <= 1f || logView.scrollOffset.y >= max - 4f;
    }

    void OnLogScrolled()
    {
        bool bottom = AtBottom();
        stickToBottom = bottom;
        if (bottom)
            IndustryUi.Show(newButton, false);
    }

    void ScrollToEnd()
    {
        if (logView == null)
            return;
        Scroller bar = logView.verticalScroller;
        bar.value = bar.highValue;
        stickToBottom = true;
        IndustryUi.Show(newButton, false);
    }

    // ---------- Строки при закрытой консоли ----------

    void ShowOverlay(string text, Kind kind)
    {
        if (overlay == null)
            return;
        Label label = IndustryUi.Text("O", DevRegistry.Escape(text), "dev-console-overlay-line", KindClass(kind));
        label.enableRichText = true;
        label.pickingMode = PickingMode.Ignore;
        overlay.Add(label);
        while (overlay.childCount > OverlayCap)
            overlay.RemoveAt(0);
        label.schedule.Execute(() => label.RemoveFromHierarchy()).ExecuteLater(OverlayMs);
    }

    // ---------- История ----------

    void AddHistory(string line)
    {
        history.Remove(line);
        history.Add(line);
        while (history.Count > HistoryCap)
            history.RemoveAt(0);
        PlayerPrefs.SetString(HistoryKey, string.Join("\n", history));
    }

    void LoadHistory()
    {
        history.Clear();
        string saved = PlayerPrefs.GetString(HistoryKey, "");
        if (saved.Length == 0)
            return;
        string[] lines = saved.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim().Length > 0)
                history.Add(lines[i].Trim());
        }

        while (history.Count > HistoryCap)
            history.RemoveAt(0);
        historyIndex = history.Count;
    }

    void SaveHistory()
    {
        PlayerPrefs.SetString(HistoryKey, string.Join("\n", history));
        PlayerPrefs.Save();
    }

    // ---------- autoexec.cfg ----------

    void TickAutoexec()
    {
        bool ready = WorldCatalog.HasActive && GameManager.Instance != null && !LoadingScreen.IsLoading
            && SaveSystem.Instance != null;
        if (ready == worldReady)
            return;
        worldReady = ready;
        if (ready)
            RunAutoexec();
    }

    void RunAutoexec()
    {
        string path = AutoexecPath;
        if (!File.Exists(path))
            return;
        string[] lines;
        try
        {
            lines = File.ReadAllLines(path);
        }
        catch (System.Exception e)
        {
            Print("autoexec: не прочитать " + path + " — " + e.Message, Kind.Error);
            return;
        }

        int n = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith("#") || line.StartsWith("//"))
                continue;
            Print("> " + line, Kind.System);
            RunLine(line);
            n++;
        }

        Print("autoexec.cfg: выполнено строк " + n, Kind.System);
    }

    void AutoexecInfo()
    {
        string path = AutoexecPath;
        GUIUtility.systemCopyBuffer = path;
        Print(File.Exists(path)
            ? "autoexec.cfg выполняется при каждом входе в мир:\n" + path + "\n(путь в буфере обмена)"
            : "autoexec.cfg нет. Создай файл — по команде в строке (# — комментарий, ; — несколько команд), он выполнится при входе в мир:\n" + path + "\n(путь в буфере обмена)",
            Kind.System);
    }
}
