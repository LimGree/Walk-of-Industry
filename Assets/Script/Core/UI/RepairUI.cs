using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Image = UnityEngine.UIElements.Image;

/// <summary>
/// Окно ремонта сломанного здания ([[BreakdownSystem]]). E по сломанному станку.
/// Одна поломка — одна мини-игра: провода, сигнал, двигатель, пример, капча.
/// Мир не на паузе. Долго (дольше лимита) или провал (3 ошибки) — платишь рубином.
/// </summary>
public class RepairUI : MonoBehaviour
{
    public const float TimeLimit = 45f;
    public const int MaxMistakes = 3;

    /// <summary>Рядом зона отдыха ([[DecorSystem]]): больше времени и одна лишняя ошибка.</summary>
    bool RestBonus => target != null && DecorSystem.RestNear(target.transform.position);
    float Limit => TimeLimit + (RestBonus ? DecorSystem.RestBonusSeconds : 0f);
    int MistakeLimit => MaxMistakes + (RestBonus ? 1 : 0);
    public const int RushBaseCost = 3;

    public static RepairUI Instance { get; private set; }
    public bool IsOpen { get; private set; }

    static readonly Color[] WireColors =
    {
        new Color(0.92f, 0.24f, 0.2f),
        new Color(0.22f, 0.52f, 0.95f),
        new Color(0.98f, 0.82f, 0.18f),
        new Color(0.85f, 0.3f, 0.85f)
    };

    const string CaptchaChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    VisualElement overlay;
    VisualElement gameHost;
    Label heading;
    Label info;
    Label timerLabel;
    Label mistakesLabel;
    Label hintLabel;
    Button rushButton;

    BuildingBase target;
    int game;
    float startedAt;
    int mistakes;
    Action tick;

    void Awake()
    {
        Instance = this;
    }

    // Закрыл и открыл тот же станок — ошибки, таймер и игра не сбрасываются.
    BuildingBase memoTarget;
    int memoGame;
    int memoMistakes;
    float memoStart;

    void Start()
    {
        if (overlay != null)
            return;
        Build();
        IndustryUi.Show(overlay, false);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Сломанное здание — открыть ремонт вместо обычного меню.</summary>
    public static bool TryOpen(BuildingBase building)
    {
        if (building == null || !building.IsBroken || Instance == null)
            return false;
        // Ремкомплект: ремонт без мини-игры, пока есть заряды на сегодня.
        if (PerkSystem.Instance != null && PerkSystem.Instance.TryUseKit())
        {
            Instance.Finish(building, 0);
            UiNotification.Push(UiLocale.T("gear.kit_used"), UiLocale.T("gear.kit_left", PerkSystem.Instance.KitLeft, PerkSystem.KitCharges), UiStatus.Completed);
            return true;
        }

        Instance.Open(building);
        return true;
    }

    public void Open(BuildingBase building)
    {
        if (overlay == null)
            Build();
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            MachineUI.Instance.Close();
        target = building;
        bool resume = memoTarget == building;
        game = resume ? memoGame : building.BreakGame;
        IsOpen = true;
        UiStack.Opened("repair", Close, 88);
        IndustryUi.Show(overlay, true);
        UiAudio.PlayModal();
        StartGame();
        if (resume)
        {
            mistakes = memoMistakes;
            startedAt = memoStart;
            RefreshHeader();
        }
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    /// <summary>Консоль (/minigame type): открыть ремонт этого здания с выбранной мини-игрой.</summary>
    public void DevOpen(BuildingBase building, int forcedGame)
    {
        if (building == null || !building.IsBroken)
            return;
        if (memoTarget == building)
            memoTarget = null;
        Open(building);
        game = Mathf.Clamp(forcedGame, 0, BreakdownSystem.GameCount - 1);
        StartGame();
    }

    /// <summary>Консоль (/minigame win): засчитать текущую мини-игру.</summary>
    public bool DevWin()
    {
        if (!IsOpen || target == null)
            return false;
        Win();
        return true;
    }

    /// <summary>Консоль (/minigame fail): провалить — как три ошибки подряд.</summary>
    public bool DevFail()
    {
        if (!IsOpen || target == null)
            return false;
        mistakes = MistakeLimit - 1;
        Mistake();
        return true;
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        UiStack.Closed("repair");
        tick = null;
        if (target != null && target.IsBroken)
        {
            memoTarget = target;
            memoGame = game;
            memoMistakes = mistakes;
            memoStart = startedAt;
        }
        else
            memoTarget = null;
        target = null;
        IndustryUi.Show(overlay, false);
        if (gameHost != null)
            gameHost.Clear();
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    void Update()
    {
        if (!IsOpen)
            return;
        if (target == null || !target.IsBroken)
        {
            Close();
            return;
        }

        float elapsed = Time.time - startedAt;
        if (timerLabel != null)
        {
            timerLabel.text = UiLocale.T("repair.timer", Mathf.FloorToInt(elapsed), Mathf.RoundToInt(Limit));
            IndustryUi.SetOn(timerLabel, elapsed > Limit, "warn");
        }

        tick?.Invoke();
    }

    // ---------- Каркас ----------

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, 88);
        overlay = IndustryUi.OverlayPanel(UiLocale.T("repair.window"), null, Close);
        VisualElement panel = IndustryUi.PanelOf(overlay);
        if (panel != null)
            panel.AddToClassList("win-auto");
        IndustryUi.WindowHints(overlay,
            ("Esc", UiLocale.T("repair.hint_later")));

        VisualElement body = overlay.Q("Body") ?? panel;
        heading = IndustryUi.Text("Heading", "", "heading-3");
        body.Add(heading);
        info = IndustryUi.Text("Info", "", "muted");
        body.Add(info);

        var status = IndustryUi.El("Status", "row", "status-row");
        status.style.justifyContent = Justify.SpaceBetween;
        status.style.marginTop = 8;
        timerLabel = IndustryUi.Text("Timer", "", "body-text");
        mistakesLabel = IndustryUi.Text("Mistakes", "", "body-text");
        status.Add(timerLabel);
        status.Add(mistakesLabel);
        body.Add(status);

        hintLabel = IndustryUi.Text("Hint", "", "gold");
        hintLabel.style.marginTop = 10;
        hintLabel.style.whiteSpace = WhiteSpace.Normal;
        body.Add(hintLabel);

        gameHost = IndustryUi.El("Game", "col");
        gameHost.style.marginTop = 12;
        gameHost.style.minHeight = 260;
        body.Add(gameHost);

        var footer = IndustryUi.El("Footer", "row");
        footer.style.justifyContent = Justify.SpaceBetween;
        footer.style.marginTop = 14;
        rushButton = IndustryUi.Btn("", Rush, "btn-small");
        footer.Add(rushButton);
        footer.Add(IndustryUi.Btn(UiLocale.T("repair.close"), Close, "btn-small", "btn-ghost"));
        body.Add(footer);

        overlay.pickingMode = PickingMode.Position;
        root.Add(overlay);
    }

    void StartGame()
    {
        mistakes = 0;
        startedAt = Time.time;
        tick = null;
        gameHost.Clear();
        RefreshHeader();
        switch (game)
        {
            case BreakdownSystem.GameWires: BuildWires(); break;
            case BreakdownSystem.GameSignal: BuildSignal(); break;
            case BreakdownSystem.GameEngine: BuildEngine(); break;
            case BreakdownSystem.GameMath: BuildMath(); break;
            default: BuildCaptcha(); break;
        }
    }

    void RefreshHeader()
    {
        if (target == null)
            return;
        string name = target.data != null ? target.data.Title : "Building";
        heading.text = UiLocale.T("repair.title", name);
        BreakdownSystem sys = BreakdownSystem.Instance;
        int surcharge = sys != null ? sys.RepairSurcharge(target) : 0;
        float age = sys != null ? sys.BrokenAgeHours(target) : 0f;
        string line = UiLocale.T(target.BreakMode == 2 ? "repair.mode_weak" : "repair.mode_dead")
            + "  ·  " + UiLocale.T("repair.age", Mathf.FloorToInt(age));
        if (surcharge > 0)
            line += "  ·  " + UiLocale.T("repair.surcharge", surcharge);
        if (RestBonus)
            line += "  ·  " + DecorText.T("decor.perk_rest", Mathf.RoundToInt(DecorSystem.RestBonusSeconds));
        info.text = line;
        mistakesLabel.text = UiLocale.T("repair.mistakes", mistakes, MistakeLimit);

        int rush = RushBaseCost + surcharge;
        int have = PlayerWallet.Instance != null ? PlayerWallet.Instance.Rubies : 0;
        IndustryUi.SetButtonLabel(rushButton, UiLocale.T("repair.rush", rush));
        rushButton.SetEnabled(have >= rush);
    }

    void Mistake()
    {
        mistakes++;
        UiAudio.PlayError();
        mistakesLabel.text = UiLocale.T("repair.mistakes", mistakes, MistakeLimit);
        if (mistakes < MistakeLimit)
            return;

        // Провал: рубин за сорванный ремонт и новая неисправность.
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.PayRubyPenalty(1);
        UiNotification.Push(NotifyKind.Breakdown, UiLocale.T("repair.fail_title"), UiLocale.T("repair.fail_body"), UiStatus.Error);
        game = BreakdownSystem.RollGame(target);
        StartGame();
    }

    void Win()
    {
        if (target == null)
            return;
        BuildingBase done = target;
        BreakdownSystem sys = BreakdownSystem.Instance;
        int penalty = (Time.time - startedAt > Limit ? 1 : 0) + (sys != null ? sys.RepairSurcharge(done) : 0);
        if (penalty > 0 && PlayerWallet.Instance != null)
            PlayerWallet.Instance.PayRubyPenalty(penalty);
        Finish(done, penalty);
    }

    void Rush()
    {
        if (target == null || PlayerWallet.Instance == null)
            return;
        BreakdownSystem sys = BreakdownSystem.Instance;
        int cost = RushBaseCost + (sys != null ? sys.RepairSurcharge(target) : 0);
        if (!PlayerWallet.Instance.TrySpendRubies(cost, MoneySource.Repair))
        {
            UiAudio.PlayError();
            return;
        }

        Finish(target, cost);
    }

    void Finish(BuildingBase done, int paid)
    {
        string name = done.data != null ? done.data.Title : "Building";
        // Кроме мини-игры ремонт стоит 3–5% баланса (процент растёт с балансом).
        int fee = PlayerWallet.Instance != null ? Economy.RepairFee(PlayerWallet.Instance.Coins) : 0;
        if (fee > 0)
            PlayerWallet.Instance.AddCoins(-fee, MoneySource.Repair);
        if (BreakdownSystem.Instance != null)
            BreakdownSystem.Instance.Repair(done, true);
        else
            done.ClearBroken();
        UiAudio.PlayNotify();
        UiNotification.Push(NotifyKind.Breakdown,
            UiLocale.T("repair.done_title"),
            (paid > 0 ? UiLocale.T("repair.done_paid", name, paid) : UiLocale.T("repair.done_body", name))
                + (fee > 0 ? "  " + UiLocale.T("repair.fee", IndustryUi.Money(fee)) : ""),
            UiStatus.Completed);
        Close();
    }

    void SetHint(string text)
    {
        hintLabel.text = text ?? "";
    }

    static Button Plain(string label, Action onClick)
    {
        return IndustryUi.Btn(label, onClick, "btn-small");
    }

    // ---------- 1. Провода ----------

    void BuildWires()
    {
        SetHint(UiLocale.T("repair.g_wires"));
        int n = WireColors.Length;
        int[] left = Shuffled(n);
        int[] right = Shuffled(n);
        var linked = new Dictionary<int, int>();   // левый слот → правый слот
        int picked = -1;

        var row = IndustryUi.El("Wires", "row");
        row.style.height = 280;
        row.style.alignItems = Align.Stretch;
        var leftCol = IndustryUi.El("L", "col");
        var rightCol = IndustryUi.El("R", "col");
        var canvas = IndustryUi.El("Canvas", "grow");
        leftCol.style.justifyContent = Justify.SpaceAround;
        rightCol.style.justifyContent = Justify.SpaceAround;
        canvas.pickingMode = PickingMode.Ignore;

        var leftPlugs = new VisualElement[n];
        var rightPlugs = new VisualElement[n];

        for (int i = 0; i < n; i++)
        {
            int slot = i;
            leftPlugs[i] = Plug(WireColors[left[i]]);
            leftPlugs[i].RegisterCallback<PointerDownEvent>(_ =>
            {
                if (linked.ContainsKey(slot))
                    return;
                picked = slot;
                UiAudio.PlayClick();
                for (int k = 0; k < n; k++)
                    Highlight(leftPlugs[k], k == picked);
            });
            leftCol.Add(leftPlugs[i]);

            rightPlugs[i] = Plug(WireColors[right[i]]);
            rightPlugs[i].RegisterCallback<PointerDownEvent>(_ =>
            {
                if (picked < 0 || linked.ContainsValue(slot))
                    return;
                if (left[picked] != right[slot])
                {
                    Highlight(leftPlugs[picked], false);
                    picked = -1;
                    Mistake();
                    return;
                }

                linked[picked] = slot;
                Highlight(leftPlugs[picked], false);
                picked = -1;
                UiAudio.PlayClick();
                canvas.MarkDirtyRepaint();
                if (linked.Count == n)
                    Win();
            });
            rightCol.Add(rightPlugs[i]);
        }

        canvas.generateVisualContent += ctx =>
        {
            Painter2D p = ctx.painter2D;
            p.lineWidth = 7f;
            p.lineCap = LineCap.Round;
            foreach (var pair in linked)
            {
                Vector2 a = canvas.WorldToLocal(leftPlugs[pair.Key].worldBound.center);
                Vector2 b = canvas.WorldToLocal(rightPlugs[pair.Value].worldBound.center);
                p.strokeColor = WireColors[left[pair.Key]];
                p.BeginPath();
                p.MoveTo(a);
                p.BezierCurveTo(new Vector2((a.x + b.x) * 0.5f, a.y), new Vector2((a.x + b.x) * 0.5f, b.y), b);
                p.Stroke();
            }
        };

        row.Add(leftCol);
        row.Add(canvas);
        row.Add(rightCol);
        gameHost.Add(row);
    }

    static VisualElement Plug(Color color)
    {
        var plug = new VisualElement();
        plug.style.width = 64;
        plug.style.height = 34;
        plug.style.backgroundColor = color;
        plug.style.borderTopLeftRadius = 6;
        plug.style.borderTopRightRadius = 6;
        plug.style.borderBottomLeftRadius = 6;
        plug.style.borderBottomRightRadius = 6;
        SetBorder(plug, 2f, new Color(0f, 0f, 0f, 0.5f));
        plug.pickingMode = PickingMode.Position;
        return plug;
    }

    static void Highlight(VisualElement el, bool on)
    {
        SetBorder(el, on ? 4f : 2f, on ? Color.white : new Color(0f, 0f, 0f, 0.5f));
    }

    static void SetBorder(VisualElement el, float width, Color color)
    {
        el.style.borderTopWidth = width;
        el.style.borderBottomWidth = width;
        el.style.borderLeftWidth = width;
        el.style.borderRightWidth = width;
        el.style.borderTopColor = color;
        el.style.borderBottomColor = color;
        el.style.borderLeftColor = color;
        el.style.borderRightColor = color;
    }

    static int[] Shuffled(int n)
    {
        var a = new int[n];
        for (int i = 0; i < n; i++)
            a[i] = i;
        for (int i = n - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (a[i], a[j]) = (a[j], a[i]);
        }

        return a;
    }

    // ---------- 2. Сигнал ----------

    void BuildSignal()
    {
        SetHint(UiLocale.T("repair.g_signal"));
        float targetFreq = UnityEngine.Random.Range(10f, 90f);
        float hold = 0f;
        const float holdNeed = 1.5f;
        const int barCount = 24;

        var bars = IndustryUi.El("Bars", "row");
        bars.style.height = 150;
        bars.style.alignItems = Align.FlexEnd;
        bars.style.justifyContent = Justify.SpaceBetween;
        bars.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
        bars.style.paddingLeft = 8;
        bars.style.paddingRight = 8;
        var barEls = new VisualElement[barCount];
        for (int i = 0; i < barCount; i++)
        {
            var bar = new VisualElement();
            bar.style.width = Length.Percent(80f / barCount);
            bar.style.height = Length.Percent(10);
            barEls[i] = bar;
            bars.Add(bar);
        }

        Label strengthLabel = IndustryUi.Text("Strength", "", "body-text");
        strengthLabel.style.marginTop = 8;
        var slider = new Slider(UiLocale.T("repair.freq"), 0f, 100f) { value = targetFreq > 50f ? 5f : 95f };
        slider.style.marginTop = 10;
        VisualElement holdTrack = IndustryUi.ProgressBar("Hold");
        holdTrack.style.marginTop = 10;

        gameHost.Add(bars);
        gameHost.Add(strengthLabel);
        gameHost.Add(slider);
        gameHost.Add(holdTrack);

        tick = () =>
        {
            float strength = Mathf.Clamp01(1f - Mathf.Abs(slider.value - targetFreq) / 30f);
            float t = Time.time;
            for (int i = 0; i < barCount; i++)
            {
                float noise = Mathf.PerlinNoise(i * 0.37f, t * 3.1f);
                float clean = 0.5f + 0.5f * Mathf.Sin(i * 0.55f + t * 5f);
                float h = Mathf.Lerp(noise, clean, strength) * (0.25f + 0.75f * strength);
                barEls[i].style.height = Length.Percent(Mathf.Clamp(h * 100f, 4f, 100f));
                barEls[i].style.backgroundColor = Color.Lerp(new Color(0.8f, 0.3f, 0.2f), new Color(0.3f, 0.9f, 0.4f), strength);
            }

            strengthLabel.text = UiLocale.T("repair.signal", Mathf.RoundToInt(strength * 100f));
            if (strength >= 0.93f)
                hold += Time.deltaTime;
            else
                hold = Mathf.Max(0f, hold - Time.deltaTime * 2f);
            IndustryUi.SetProgress(holdTrack, hold / holdNeed);
            if (hold >= holdNeed)
            {
                tick = null;
                Win();
            }
        };
    }

    // ---------- 3. Двигатель ----------

    void BuildEngine()
    {
        const int need = 3;
        const float zoneWidth = 0.16f;
        int starts = 0;
        float zone = UnityEngine.Random.Range(0.35f, 0.84f - zoneWidth);
        float needle = 0f;
        float dir = 1f;
        bool holding = false;
        SetHint(UiLocale.T("repair.g_engine", starts, need));

        var track = new VisualElement();
        track.style.height = 34;
        track.style.marginTop = 30;
        track.style.backgroundColor = new Color(0f, 0f, 0f, 0.4f);
        SetBorder(track, 1f, new Color(1f, 1f, 1f, 0.25f));
        var zoneEl = new VisualElement();
        zoneEl.style.position = Position.Absolute;
        zoneEl.style.top = 0;
        zoneEl.style.bottom = 0;
        zoneEl.style.backgroundColor = new Color(0.25f, 0.85f, 0.35f, 0.75f);
        var needleEl = new VisualElement();
        needleEl.style.position = Position.Absolute;
        needleEl.style.top = -6;
        needleEl.style.bottom = -6;
        needleEl.style.width = 5;
        needleEl.style.backgroundColor = Color.white;
        track.Add(zoneEl);
        track.Add(needleEl);

        void PlaceZone()
        {
            zoneEl.style.left = Length.Percent(zone * 100f);
            zoneEl.style.width = Length.Percent(zoneWidth * 100f);
        }

        PlaceZone();

        var holdBtn = IndustryUi.El("Hold", "btn", "btn-primary");
        var holdLabel = IndustryUi.Text("L", UiLocale.T("repair.hold"), "btn-label");
        holdLabel.pickingMode = PickingMode.Ignore;
        holdBtn.Add(holdLabel);
        holdBtn.style.marginTop = 30;
        holdBtn.style.height = 64;
        holdBtn.style.justifyContent = Justify.Center;
        holdBtn.style.alignItems = Align.Center;
        holdBtn.pickingMode = PickingMode.Position;

        holdBtn.RegisterCallback<PointerDownEvent>(evt =>
        {
            holding = true;
            needle = 0f;
            dir = 1f;
            holdBtn.CapturePointer(evt.pointerId);
            UiAudio.PlayClick();
        });
        holdBtn.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (!holding)
                return;
            holding = false;
            holdBtn.ReleasePointer(evt.pointerId);
            if (needle >= zone && needle <= zone + zoneWidth)
            {
                starts++;
                UiAudio.PlayNotify();
                if (starts >= need)
                {
                    Win();
                    return;
                }

                zone = UnityEngine.Random.Range(0.2f, 1f - zoneWidth);
                PlaceZone();
                SetHint(UiLocale.T("repair.g_engine", starts, need));
            }
            else
                Mistake();
            needle = 0f;
        });

        gameHost.Add(track);
        gameHost.Add(holdBtn);

        tick = () =>
        {
            if (holding)
            {
                // Ускоряется с каждым удачным запуском.
                needle += dir * Time.deltaTime * (0.75f + 0.2f * starts);
                if (needle >= 1f) { needle = 1f; dir = -1f; }
                if (needle <= 0f) { needle = 0f; dir = 1f; }
            }

            needleEl.style.left = Length.Percent(needle * 100f);
        };
    }

    // ---------- 4. Пример ----------

    void BuildMath()
    {
        const int need = 3;
        int solved = 0;
        int answer = 0;

        var question = IndustryUi.Text("Q", "", "heading-1");
        question.style.marginTop = 20;
        question.style.unityTextAlign = TextAnchor.MiddleCenter;
        var field = new TextField { name = "Answer" };
        field.AddToClassList("field");
        field.style.marginTop = 16;
        field.style.fontSize = 28;
        var check = IndustryUi.Btn(UiLocale.T("repair.check"), null, "btn-primary");
        check.style.marginTop = 12;

        void Next()
        {
            SetHint(UiLocale.T("repair.g_math", solved, need));
            bool add = UnityEngine.Random.value < 0.5f;
            int a = UnityEngine.Random.Range(5, 100);
            int b;
            if (add)
            {
                b = UnityEngine.Random.Range(1, 101 - a);
                answer = a + b;
            }
            else
            {
                b = UnityEngine.Random.Range(1, a + 1);
                answer = a - b;
            }

            question.text = a + (add ? " + " : " − ") + b + " = ?";
            field.value = "";
            field.schedule.Execute(() => field.Focus());
        }

        void Submit()
        {
            if (!int.TryParse(field.value.Trim(), out int got) || got != answer)
            {
                Mistake();
                if (IsOpen && game == BreakdownSystem.GameMath && gameHost.Contains(question))
                    Next();
                return;
            }

            solved++;
            UiAudio.PlayClick();
            if (solved >= need)
            {
                Win();
                return;
            }

            Next();
        }

        check.clicked += Submit;
        HookEnter(field, Submit);
        gameHost.Add(question);
        gameHost.Add(field);
        gameHost.Add(check);
        Next();
    }

    static void HookEnter(TextField field, Action onEnter)
    {
        field.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                return;
            evt.StopPropagation();
            onEnter();
        }, TrickleDown.TrickleDown);
    }

    // ---------- 5. Капча ----------

    void BuildCaptcha()
    {
        if (UnityEngine.Random.value < 0.5f && BuildCaptchaIcons())
            return;
        BuildCaptchaText();
    }

    void BuildCaptchaText()
    {
        SetHint(UiLocale.T("repair.g_captcha"));
        string code = "";

        var box = new VisualElement();
        box.style.height = 110;
        box.style.marginTop = 10;
        box.style.flexDirection = FlexDirection.Row;
        box.style.justifyContent = Justify.Center;
        box.style.alignItems = Align.Center;
        box.style.backgroundColor = new Color(0.86f, 0.84f, 0.78f, 1f);
        box.style.overflow = Overflow.Hidden;

        var field = new TextField { name = "Captcha" };
        field.AddToClassList("field");
        field.style.marginTop = 14;
        field.style.fontSize = 26;
        var check = IndustryUi.Btn(UiLocale.T("repair.check"), null, "btn-primary");
        check.style.marginTop = 12;

        void Regen()
        {
            box.Clear();
            var chars = new char[5];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = CaptchaChars[UnityEngine.Random.Range(0, CaptchaChars.Length)];
            code = new string(chars);

            for (int i = 0; i < chars.Length; i++)
            {
                var ch = new Label(chars[i].ToString());
                ch.pickingMode = PickingMode.Ignore;
                ch.style.fontSize = UnityEngine.Random.Range(38, 52);
                ch.style.unityFontStyleAndWeight = FontStyle.Bold;
                ch.style.color = Color.HSVToRGB(UnityEngine.Random.value, 0.7f, 0.45f);
                ch.style.marginLeft = UnityEngine.Random.Range(2, 10);
                ch.style.marginRight = UnityEngine.Random.Range(2, 10);
                ch.style.rotate = new Rotate(new Angle(UnityEngine.Random.Range(-28f, 28f), AngleUnit.Degree));
                ch.style.translate = new Translate(0, UnityEngine.Random.Range(-14f, 14f));
                box.Add(ch);
            }

            // Шумовые полосы поверх букв.
            for (int i = 0; i < 7; i++)
            {
                var line = new VisualElement();
                line.pickingMode = PickingMode.Ignore;
                line.style.position = Position.Absolute;
                line.style.left = Length.Percent(UnityEngine.Random.Range(-10f, 60f));
                line.style.top = Length.Percent(UnityEngine.Random.Range(10f, 90f));
                line.style.width = Length.Percent(UnityEngine.Random.Range(30f, 70f));
                line.style.height = UnityEngine.Random.Range(2, 4);
                line.style.backgroundColor = Color.HSVToRGB(UnityEngine.Random.value, 0.5f, 0.35f);
                line.style.rotate = new Rotate(new Angle(UnityEngine.Random.Range(-20f, 20f), AngleUnit.Degree));
                box.Add(line);
            }

            field.value = "";
            field.schedule.Execute(() => field.Focus());
        }

        void Submit()
        {
            if (!string.Equals(field.value.Trim(), code, StringComparison.OrdinalIgnoreCase))
            {
                Mistake();
                if (IsOpen && gameHost.Contains(box))
                    Regen();
                return;
            }

            Win();
        }

        check.clicked += Submit;
        HookEnter(field, Submit);
        var again = IndustryUi.Btn(UiLocale.T("repair.captcha_new"), Regen, "btn-small", "btn-ghost");
        again.style.marginTop = 6;
        gameHost.Add(box);
        gameHost.Add(again);
        gameHost.Add(field);
        gameHost.Add(check);
        Regen();
    }

    bool BuildCaptchaIcons()
    {
        var pool = new List<ItemData>();
        ItemData[] all = GameDatabase.AllItems();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].icon != null)
                pool.Add(all[i]);
        }

        if (pool.Count < 5)
            return false;

        const int cells = 9;
        var gridEl = new VisualElement();
        gridEl.style.flexDirection = FlexDirection.Row;
        gridEl.style.flexWrap = Wrap.Wrap;
        gridEl.style.justifyContent = Justify.Center;
        gridEl.style.marginTop = 8;
        gridEl.style.width = 3 * 104;
        gridEl.style.alignSelf = Align.Center;

        var check = IndustryUi.Btn(UiLocale.T("repair.check"), null, "btn-primary");
        check.style.marginTop = 12;
        var picked = new bool[cells];
        var isTarget = new bool[cells];

        void Regen()
        {
            gridEl.Clear();
            ItemData want = pool[UnityEngine.Random.Range(0, pool.Count)];
            SetHint(UiLocale.T("repair.g_captcha_pick", want.Title));
            int count = UnityEngine.Random.Range(2, 5);
            int[] order = Shuffled(cells);
            for (int i = 0; i < cells; i++)
            {
                picked[i] = false;
                isTarget[i] = false;
            }

            for (int i = 0; i < count; i++)
                isTarget[order[i]] = true;

            for (int i = 0; i < cells; i++)
            {
                int idx = i;
                ItemData item = want;
                if (!isTarget[i])
                {
                    do
                        item = pool[UnityEngine.Random.Range(0, pool.Count)];
                    while (item == want);
                }

                var cell = new VisualElement();
                cell.style.width = 96;
                cell.style.height = 96;
                cell.style.marginLeft = 4;
                cell.style.marginRight = 4;
                cell.style.marginTop = 4;
                cell.style.marginBottom = 4;
                cell.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
                cell.style.alignItems = Align.Center;
                cell.style.justifyContent = Justify.Center;
                SetBorder(cell, 2f, new Color(1f, 1f, 1f, 0.15f));
                var img = new Image { sprite = item.icon, scaleMode = ScaleMode.ScaleToFit };
                img.pickingMode = PickingMode.Ignore;
                img.style.width = 72;
                img.style.height = 72;
                img.style.rotate = new Rotate(new Angle(UnityEngine.Random.Range(-18f, 18f), AngleUnit.Degree));
                cell.Add(img);
                cell.RegisterCallback<PointerDownEvent>(_ =>
                {
                    picked[idx] = !picked[idx];
                    UiAudio.PlayClick();
                    SetBorder(cell, picked[idx] ? 4f : 2f, picked[idx] ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 1f, 1f, 0.15f));
                });
                gridEl.Add(cell);
            }
        }

        check.clicked += () =>
        {
            for (int i = 0; i < cells; i++)
            {
                if (picked[i] != isTarget[i])
                {
                    Mistake();
                    if (IsOpen && gameHost.Contains(gridEl))
                        Regen();
                    return;
                }
            }

            Win();
        };

        gameHost.Add(gridEl);
        gameHost.Add(check);
        Regen();
        return true;
    }
}
