using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// HUD целей ([[GoalSystem]]): карточка текущей вехи с галочками, отслеживаемая цепочка, карточка подсказки
/// «в первый раз». J — журнал: все вехи + справочник прочитанных подсказок.
/// Клик по условию/шагу — [[ProductionMapUI]] на нужном предмете.
/// </summary>
public class GoalsUI : MonoBehaviour
{
    public const string WindowId = "goals";
    const int SortOrder = 111;

    public static GoalsUI Instance { get; private set; }
    public bool IsOpen { get; private set; }

    VisualElement hud;
    Label goalCaption;
    Label goalTitle;
    Label hintKeys;
    VisualElement goalConds;
    Label goalReward;
    VisualElement trackBox;
    Label trackTitle;
    VisualElement trackSteps;
    VisualElement hintCard;
    Label hintTitle;
    Label hintText;

    VisualElement overlay;
    VisualElement journal;
    InputAction goalsAction;
    float nextRefresh;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        Build();
        if (GoalSystem.Instance != null)
            GoalSystem.Instance.Changed += Refresh;
        UiLocale.Changed += Refresh;
        InputSystem_Actions actions = KeybindStore.Shared;
        goalsAction = actions != null ? actions.asset.FindAction("Player/Goals", false) : null;
        if (goalsAction != null)
            goalsAction.performed += OnHotkey;
        SetOpen(false);
        Refresh();
    }

    void OnDestroy()
    {
        if (GoalSystem.Instance != null)
            GoalSystem.Instance.Changed -= Refresh;
        UiLocale.Changed -= Refresh;
        if (goalsAction != null)
            goalsAction.performed -= OnHotkey;
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (goalsAction == null && !KeybindStore.BlocksGameplayInput)
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb.jKey.wasPressedThisFrame)
                Hotkey();
        }

        // Миникарту двигают и меняют ей размер — место карточек считаем каждый кадр.
        HudAvoid.Place(hud, right: true, fromTop: true, offset: 24f);
        HudAvoid.Place(hintCard, right: true, fromTop: false, offset: 150f);

        // Прогресс (n/N) меняется без событий — обновляем раз в секунду.
        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 1f;
            Refresh();
        }
    }

    void OnHotkey(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput && !IsOpen)
            return;
        Hotkey();
    }

    void Hotkey()
    {
        // Shift+J — спрятать/показать карточку цели, без журнала.
        InputSystem_Actions input = KeybindStore.Shared;
        if (input != null && input.Player.Modifier.IsPressed())
        {
            ToggleCard();
            return;
        }

        UiStack.Hotkey(WindowId, () =>
        {
            if (!IsOpen)
                SetOpen(true);
        }, () => SetOpen(false), SortOrder);
    }

    public void ToggleCard()
    {
        GameSettings.GoalCard = !GameSettings.GoalCard;
        UiNotification.Push(GameSettings.GoalCard ? UiLocale.T("goal.card_on") : UiLocale.T("goal.card_off"),
            UiLocale.T("goal.card_how", KeybindStore.Hint("Modifier") + "+" + KeybindStore.Hint("Goals")));
        Refresh();
    }

    public void SetOpen(bool open)
    {
        if (open && (LoadingScreen.IsLoading || (GameManager.Instance != null && GameManager.Instance.IsPaused)))
            return;
        IsOpen = open;
        IndustryUi.Show(overlay, open);
        if (open)
        {
            UiStack.Opened(WindowId, () => SetOpen(false), SortOrder);
            GoalSystem.Instance?.DismissHint();
            FillJournal();
        }
        else
            UiStack.Closed(WindowId);
        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    // ---------- Построение ----------

    void Build()
    {
        VisualElement root = IndustryUi.Mount(this, SortOrder);
        root.pickingMode = PickingMode.Ignore;

        hud = IndustryUi.El("GoalHud", "goal-hud");
        hud.pickingMode = PickingMode.Ignore;
        goalCaption = IndustryUi.Text("Cap", "", "tut-step");
        hud.Add(goalCaption);
        goalTitle = IndustryUi.Text("Title", "", "tut-goal");
        goalConds = IndustryUi.El("Conds", "col");
        goalReward = IndustryUi.Text("Reward", "", "caption");
        hud.Add(goalTitle);
        hud.Add(goalConds);
        hud.Add(goalReward);

        trackBox = IndustryUi.El("Track", "col", "goal-track");
        trackTitle = IndustryUi.Text("TT", "", "tut-step");
        trackSteps = IndustryUi.El("Steps", "col");
        trackBox.Add(trackTitle);
        trackBox.Add(trackSteps);
        hud.Add(trackBox);
        root.Add(hud);

        hintCard = IndustryUi.El("HintCard", "hint-card");
        hintCard.pickingMode = PickingMode.Ignore;
        hintTitle = IndustryUi.Text("HT", "", "tut-goal");
        hintText = IndustryUi.Text("HB", "", "tut-body");
        hintText.style.whiteSpace = WhiteSpace.Normal;
        hintCard.Add(IndustryUi.Text("Cap", UiLocale.T("tip.caption"), "tut-step"));
        hintCard.Add(hintTitle);
        hintCard.Add(hintText);
        // Курсор в игре заблокирован — без кнопок: карточка уходит сама, J — справочник.
        hintKeys = IndustryUi.Text("Keys", "", "tut-skip-hint");
        hintCard.Add(hintKeys);
        IndustryUi.Show(hintCard, false);
        root.Add(hintCard);

        overlay = IndustryUi.OverlayPanel(UiLocale.T("goal.journal"), null, () => SetOpen(false));
        VisualElement body = overlay.Q("Body") ?? IndustryUi.PanelOf(overlay);
        IndustryUi.WindowHints(overlay, (KeybindStore.Hint("Goals"), UiLocale.T("win.close")));
        ScrollView scroll = IndustryUi.Scroll("JournalScroll");
        scroll.AddToClassList("grow");
        journal = IndustryUi.El("Journal", "col");
        scroll.Add(journal);
        body.Add(scroll);
        overlay.pickingMode = PickingMode.Position;
        root.Add(overlay);
    }

    void Refresh()
    {
        GoalSystem g = GoalSystem.Instance;
        if (hud == null)
            return;
        bool show = g != null && g.Active && !PhotoMode.IsActive;
        IndustryUi.Show(hud, show && GameSettings.GoalCard && (g.Current != null || g.TrackedItem != null));
        if (show)
        {
            FillGoal(g);
            FillTrack(g);
        }

        GoalSystem.HintDef hint = g != null ? g.ShowingHint : null;
        IndustryUi.Show(hintCard, hint != null);
        if (hint != null)
        {
            hintTitle.text = hint.Title;
            hintText.text = hint.Text;
            hintKeys.text = UiLocale.T("tip.keys", KeybindStore.Hint("Goals"));
        }

        goalCaption.text = UiLocale.T("goal.caption", KeybindStore.Hint("Goals"), KeybindStore.Hint("ProductionMap"));

        if (IsOpen)
            FillJournal();
    }

    void FillGoal(GoalSystem g)
    {
        GoalSystem.Milestone m = g.Current;
        IndustryUi.Show(goalTitle, m != null);
        IndustryUi.Show(goalConds, m != null);
        IndustryUi.Show(goalReward, m != null);
        goalConds.Clear();
        if (m == null)
            return;
        goalTitle.text = m.Title;
        for (int i = 0; i < m.conds.Length; i++)
            goalConds.Add(CondRow(g, m.conds[i], false));
        goalReward.text = UiLocale.T("goal.reward", IndustryUi.Money(m.reward));
    }

    VisualElement CondRow(GoalSystem g, GoalSystem.Cond c, bool clickable)
    {
        bool ok = g.IsDone(c);
        string text = (ok ? "✓ " : "○ ") + GoalSystem.CondText(c);
        if (c.count > 1)
            text += "  " + Mathf.Min(g.Progress(c), c.count) + "/" + c.count;
        Label row = IndustryUi.Text("Cond", text, "goal-cond");
        row.EnableInClassList("goal-cond-done", ok);
        ItemData item = GoalSystem.CondItem(c);
        if (clickable && item != null && !ok)
        {
            row.pickingMode = PickingMode.Position;
            row.AddToClassList("goal-cond-link");
            row.RegisterCallback<ClickEvent>(_ => ProductionMapUI.Instance?.Open(item));
        }

        return row;
    }

    void FillTrack(GoalSystem g)
    {
        IndustryUi.Show(trackBox, g.TrackedItem != null);
        trackSteps.Clear();
        if (g.TrackedItem == null)
            return;
        trackTitle.text = UiLocale.T("goal.tracking", g.TrackedItem.Title);
        List<(ItemData item, bool ok)> steps = g.TrackedSteps();
        for (int i = 0; i < steps.Count; i++)
        {
            ItemData item = steps[i].item;
            Label row = IndustryUi.Text("Step", (steps[i].ok ? "✓ " : "○ ") + item.Title, "goal-cond");
            row.EnableInClassList("goal-cond-done", steps[i].ok);
            trackSteps.Add(row);
        }
    }

    void FillJournal()
    {
        GoalSystem g = GoalSystem.Instance;
        journal.Clear();
        if (g == null)
            return;
        string toggleKey = KeybindStore.Hint("Modifier") + "+" + KeybindStore.Hint("Goals");
        journal.Add(IndustryUi.Btn(GameSettings.GoalCard ? UiLocale.T("goal.hide_card", toggleKey) : UiLocale.T("goal.show_card", toggleKey),
            () => { ToggleCard(); FillJournal(); }, "btn-small", "btn-ghost"));
        journal.Add(IndustryUi.Text("GoalsCap", UiLocale.T("goal.all"), "label-caps", "pm-section"));
        for (int i = 0; i < GoalSystem.Milestones.Length; i++)
        {
            GoalSystem.Milestone m = GoalSystem.Milestones[i];
            bool done = g.IsMilestoneDone(m.id);
            bool current = g.Current == m;
            var card = IndustryUi.El("M", "card", "goal-card");
            card.EnableInClassList("goal-card-done", done);
            card.EnableInClassList("goal-card-current", current);
            card.Add(IndustryUi.Text("T", (done ? "✓ " : current ? "▶ " : "") + (i + 1) + ". " + m.Title, "heading-3"));
            card.Add(IndustryUi.Text("I", m.Info, "muted"));
            if (current || done)
            {
                for (int c = 0; c < m.conds.Length; c++)
                    card.Add(CondRow(g, m.conds[c], true));
            }

            card.Add(IndustryUi.Text("R", UiLocale.T("goal.reward", IndustryUi.Money(m.reward)), "caption"));
            journal.Add(card);
        }

        journal.Add(IndustryUi.Text("HintsCap", UiLocale.T("tip.reference"), "label-caps", "pm-section"));
        if (g.HintsSeen.Count == 0)
            journal.Add(IndustryUi.Text("None", UiLocale.T("tip.none"), "muted"));
        for (int i = 0; i < g.HintsSeen.Count; i++)
        {
            GoalSystem.HintDef h = GoalSystem.FindHint(g.HintsSeen[i]);
            if (h == null)
                continue;
            var card = IndustryUi.El("H", "card", "goal-card");
            card.Add(IndustryUi.Text("T", h.Title, "heading-3"));
            card.Add(IndustryUi.Text("B", h.Text, "muted"));
            journal.Add(card);
        }
    }
}
