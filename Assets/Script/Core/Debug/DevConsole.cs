using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class DevConsole : MonoBehaviour
{
    const float TripleWindow = 0.55f;
    const int HistoryCap = 40;
    const int LogCap = 200;

    public static bool IsOpen { get; private set; }

    static DevConsole instance;
    VisualElement root;
    VisualElement panel;
    ScrollView logView;
    TextField field;
    Label hints;
    readonly List<string> log = new List<string>(64);
    readonly List<string> history = new List<string>(16);
    readonly List<string> matches = new List<string>(16);
    int historyIndex = -1;
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

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        BuildUi();
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            IsOpen = false;
        }
    }

    void Update()
    {
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

    void BuildUi()
    {
        VisualElement host = IndustryUi.Mount(this, 980);
        host.pickingMode = PickingMode.Ignore;
        panel = IndustryUi.El("DevConsole", "dev-console");
        panel.pickingMode = PickingMode.Position;
        var head = IndustryUi.Text("Title", "DEV", "dev-console-title");
        logView = IndustryUi.Scroll("DevLog");
        logView.AddToClassList("dev-console-log");
        field = new TextField { name = "Cmd" };
        field.AddToClassList("dev-console-input");
        field.value = "/";
        hints = IndustryUi.Text("Hints", "", "dev-console-hints");
        panel.Add(head);
        panel.Add(logView);
        panel.Add(field);
        panel.Add(hints);
        host.Add(panel);
        IndustryUi.Show(panel, false);

        field.RegisterCallback<KeyDownEvent>(OnFieldKey, TrickleDown.TrickleDown);
        field.RegisterValueChangedCallback(evt => RefreshHints(evt.newValue));
    }

    void OnFieldKey(KeyDownEvent evt)
    {
        if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
        {
            Submit();
            evt.StopImmediatePropagation();
            return;
        }
        if (evt.keyCode == KeyCode.Tab)
        {
            Complete();
            evt.StopImmediatePropagation();
            return;
        }
        if (evt.keyCode == KeyCode.UpArrow)
        {
            History(-1);
            evt.StopImmediatePropagation();
            return;
        }
        if (evt.keyCode == KeyCode.DownArrow)
        {
            History(1);
            evt.StopImmediatePropagation();
        }
    }

    void Submit()
    {
        string line = field.value != null ? field.value.Trim() : "";
        if (string.IsNullOrEmpty(line) || line == "/")
            return;
        Print("> " + line);
        if (history.Count == 0 || history[history.Count - 1] != line)
        {
            history.Add(line);
            if (history.Count > HistoryCap)
                history.RemoveAt(0);
        }
        historyIndex = history.Count;
        string result = DevCommands.Run(line);
        if (!string.IsNullOrEmpty(result))
            Print(result);
        field.value = "/";
        field.cursorIndex = 1;
        field.selectIndex = 1;
        RefreshHints("/");
    }

    void Complete()
    {
        RefreshHints(field.value);
        if (matches.Count == 0)
            return;
        field.value = matches[0];
        field.cursorIndex = field.value.Length;
        field.selectIndex = field.value.Length;
        RefreshHints(field.value);
    }

    void History(int dir)
    {
        if (history.Count == 0)
            return;
        historyIndex = Mathf.Clamp(historyIndex + dir, 0, history.Count);
        if (historyIndex >= history.Count)
        {
            field.value = "/";
            return;
        }
        field.value = history[historyIndex];
        field.cursorIndex = field.value.Length;
        field.selectIndex = field.value.Length;
    }

    void RefreshHints(string text)
    {
        DevCommands.Suggest(text, matches);
        if (matches.Count == 0)
        {
            hints.text = "";
            return;
        }
        int n = Mathf.Min(8, matches.Count);
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
                sb.Append("   ");
            sb.Append(matches[i]);
        }
        hints.text = sb.ToString();
    }

    public void Print(string line)
    {
        if (string.IsNullOrEmpty(line) || logView == null)
            return;
        string[] parts = line.Replace("\r", "").Split('\n');
        for (int i = 0; i < parts.Length; i++)
        {
            log.Add(parts[i]);
            logView.Add(IndustryUi.Text("L", parts[i], "dev-console-line"));
        }
        while (log.Count > LogCap && logView.childCount > 0)
        {
            log.RemoveAt(0);
            logView.RemoveAt(0);
        }
        logView.scrollOffset = new Vector2(0f, 99999f);
    }

    void SetOpen(bool on)
    {
        IsOpen = on;
        IndustryUi.Show(panel, on);
        panel.pickingMode = on ? PickingMode.Position : PickingMode.Ignore;
        if (on)
        {
            field.value = "/";
            field.schedule.Execute(() =>
            {
                field.Focus();
                field.cursorIndex = 1;
                field.selectIndex = 1;
            });
            RefreshHints("/");
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            KeybindStore.SetPlayerMapEnabled(false);
        }
        else
        {
            KeybindStore.SetPlayerMapEnabled(true);
            if (GameManager.Instance != null)
                GameManager.Instance.RestoreGameplayFocus();
        }
    }
}
