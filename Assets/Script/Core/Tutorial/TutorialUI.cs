using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// UI вводной главы. Без модалок: карточка цели (1 строка цели + 1 строка пояснения + прогресс),
/// стрелка-указатель на краю экрана до цели шага, финальная карточка с переходом к Карте производства.
/// F1 — пропустить всё, F2 — шаг, F3 — повтор.
/// </summary>
public class TutorialUI : MonoBehaviour
{
    public static TutorialUI Instance { get; private set; }

    VisualElement root;

    VisualElement hud;
    Label hudStep;
    Label hudGoal;
    Label hudBody;
    VisualElement hudProgress;
    Label hudProgressText;
    Label hudSkipHint;

    VisualElement pointer;
    Label pointerArrow;
    Label pointerDist;

    VisualElement lastGlow;
    string lastKey;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        if (TutorialSystem.Instance != null)
            TutorialSystem.Instance.Changed -= Refresh;
        UiLocale.Changed -= Refresh;
    }

    void Start()
    {
        Build();
        if (TutorialSystem.Instance != null)
            TutorialSystem.Instance.Changed += Refresh;
        UiLocale.Changed += Refresh;
        Refresh();
    }

    void LateUpdate()
    {
        TutorialSystem tut = TutorialSystem.Instance;
        if (tut == null || !tut.IsRunning)
        {
            if (lastKey != "off")
            {
                lastKey = "off";
                HideAll();
            }
            return;
        }

        tut.NoteWaitProgress();
        PollSkipKey();

        // Перерисовываем текст раз в секунду (прогресс руды, оценка времени) и на смене шага/языка.
        string key = tut.Step + "|" + Mathf.FloorToInt(tut.StepAge) + "|" + UiLocale.Code;
        if (key != lastKey)
        {
            lastKey = key;
            Apply(tut);
        }

        HudAvoid.Place(hud, right: false, fromTop: true, offset: 92f);
        UpdatePointer(tut);
        Glow(FindGlow(tut));
    }

    public bool IsModalOpen => false;

    void Build()
    {
        root = IndustryUi.Mount(this, 160);
        root.pickingMode = PickingMode.Ignore;

        hud = IndustryUi.El("TutHud", "tut-hud");
        hud.pickingMode = PickingMode.Ignore;
        hudStep = IndustryUi.Text("Step", "", "tut-step");
        hudGoal = IndustryUi.Text("G", "", "tut-goal");
        hudBody = IndustryUi.Text("B", "", "tut-body");
        hudBody.style.whiteSpace = WhiteSpace.Normal;
        hudProgress = IndustryUi.ProgressBar("TutProgress");
        hudProgressText = IndustryUi.Text("PT", "", "tut-progress-text");
        hudSkipHint = IndustryUi.Text("SkipHint", "", "tut-skip-hint");

        // Кнопок нет: в игре курсор заблокирован. Всё — клавишами (строка подсказки внизу карточки).
        hud.Add(hudStep);
        hud.Add(hudGoal);
        hud.Add(hudBody);
        hud.Add(hudProgress);
        hud.Add(hudProgressText);
        hud.Add(hudSkipHint);
        IndustryUi.Show(hud, false);
        root.Add(hud);

        pointer = IndustryUi.El("TutPointer", "tut-pointer");
        pointer.pickingMode = PickingMode.Ignore;
        pointerArrow = IndustryUi.Text("Arrow", "▲", "tut-pointer-arrow");
        pointerDist = IndustryUi.Text("Dist", "", "tut-pointer-dist");
        pointer.Add(pointerArrow);
        pointer.Add(pointerDist);
        IndustryUi.Show(pointer, false);
        root.Add(pointer);
    }

    void Refresh()
    {
        lastKey = null;
        TutorialSystem tut = TutorialSystem.Instance;
        if (tut != null && tut.IsRunning)
            Apply(tut);
        else
            HideAll();
    }

    void HideAll()
    {
        IndustryUi.Show(hud, false);
        IndustryUi.Show(pointer, false);
        Glow(null);
    }

    void Apply(TutorialSystem tut)
    {
        if (hud == null)
            return;
        bool handoff = tut.Step == TutorialStep.Handoff;
        IndustryUi.Show(hud, true);
        hud.pickingMode = PickingMode.Ignore;

        int n = (int)tut.Step + 1;
        int total = (int)TutorialStep.Handoff + 1;
        hudStep.text = UiLocale.T("tut.step_of", n, total);
        hudGoal.text = GoalText(tut);
        string body = BodyText(tut);
        hudBody.text = body;
        IndustryUi.Show(hudBody, !string.IsNullOrEmpty(body));

        bool wait = tut.Step == TutorialStep.SpeedUp || tut.Step == TutorialStep.FirstBelt || tut.Step == TutorialStep.CopperLine;
        if (wait)
        {
            int iron = tut.Submitted(TutorialSystem.IronOreId);
            int copper = tut.Submitted(TutorialSystem.CopperOreId);
            int ironNeed = tut.Required(TutorialSystem.IronOreId);
            int copperNeed = tut.Required(TutorialSystem.CopperOreId);
            float t = (Mathf.Min(iron, ironNeed) + Mathf.Min(copper, copperNeed)) / (float)Mathf.Max(1, ironNeed + copperNeed);
            IndustryUi.SetProgress(hudProgress, t);
            string text = UiLocale.T("tut.progress", Item(TutorialSystem.IronOreId), iron, ironNeed, Item(TutorialSystem.CopperOreId), copper, copperNeed);
            float eta = tut.EstimateWaitSeconds();
            if (eta > 0f && tut.Step == TutorialStep.SpeedUp)
                text += "  ·  " + UiLocale.T("tut.eta", Mathf.CeilToInt(eta / 60f));
            hudProgressText.text = text;
        }

        IndustryUi.Show(hudProgress, wait);
        IndustryUi.Show(hudProgressText, wait);
        hudSkipHint.text = handoff
            ? UiLocale.T("tut.handoff_keys", KeybindStore.Hint("ProductionMap"), "F2")
            : UiLocale.T("tut.skip_key", "F1", "F2", "F3");
    }

    static string Item(string id)
    {
        ItemData item = GameDatabase.FindItem(id);
        return item != null ? item.Title : id;
    }

    static string Building(string id)
    {
        BuildingData b = GameDatabase.FindBuilding(id);
        return b != null ? b.Title : id;
    }

    static string GoalText(TutorialSystem tut)
    {
        switch (tut.Step)
        {
            case TutorialStep.Intro:
                return UiLocale.T("tut.obj.intro");
            case TutorialStep.GoToIron:
                return UiLocale.T("tut.obj.go_iron", Item(TutorialSystem.IronOreId));
            case TutorialStep.BuildMode:
                return UiLocale.T("tut.obj.build", KeybindStore.Hint("BuildMode"));
            case TutorialStep.PlaceExtractor:
                return UiLocale.T("tut.obj.extractor", Building(TutorialSystem.ExtractorId), KeybindStore.Hint("Place"));
            case TutorialStep.WatchOutput:
                return UiLocale.T("tut.obj.output");
            case TutorialStep.PlaceLab:
                return UiLocale.T("tut.obj.lab", Building(TutorialSystem.LabId));
            case TutorialStep.RotateBuilding:
                return UiLocale.T("tut.obj.rotate", Building(TutorialSystem.ConveyorId), KeybindStore.Hint("Rotate"));
            case TutorialStep.DragBelt:
                return UiLocale.T("tut.obj.drag", KeybindStore.Hint("Place"));
            case TutorialStep.FirstBelt:
                return UiLocale.T("tut.obj.belt", KeybindStore.Hint("Place"));
            case TutorialStep.FirstOreIn:
                return UiLocale.T("tut.obj.ore_in");
            case TutorialStep.GoToCopper:
                return UiLocale.T("tut.obj.go_copper", Item(TutorialSystem.CopperOreId));
            case TutorialStep.CopperLine:
                return UiLocale.T("tut.obj.copper_line");
            case TutorialStep.SpeedUp:
                return UiLocale.T("tut.obj.speed");
            case TutorialStep.Unlocked:
                return UiLocale.T("tut.obj.unlocked", Building(TutorialSystem.SmelterId));
            case TutorialStep.PlaceSmelter:
                return UiLocale.T("tut.obj.smelter", Building(TutorialSystem.SmelterId));
            case TutorialStep.FirstIngot:
                return UiLocale.T("tut.obj.ingot", Item(TutorialSystem.IronIngotId));
            case TutorialStep.CopyLine:
                return UiLocale.T("tut.obj.copy", KeybindStore.Hint("SelectMode"), KeybindStore.Hint("Copy"));
            case TutorialStep.PasteLine:
                return UiLocale.T("tut.obj.paste", KeybindStore.Hint("Paste"));
            case TutorialStep.OpenMachine:
                return UiLocale.T("tut.obj.machine", KeybindStore.Hint("Interact"));
            case TutorialStep.Handoff:
                return UiLocale.T("tut.obj.handoff");
            default:
                return "";
        }
    }

    static string BodyText(TutorialSystem tut)
    {
        switch (tut.Step)
        {
            case TutorialStep.Intro:
                return UiLocale.T("tut.body.intro");
            case TutorialStep.GoToIron:
                return tut.StepAge > 30f
                    ? UiLocale.T("tut.body.go_map", KeybindStore.Hint("MoveSelection"))
                    : UiLocale.T("tut.body.go_iron");
            case TutorialStep.PlaceExtractor:
                return UiLocale.T("tut.body.extractor", KeybindStore.Hint("Rotate"));
            case TutorialStep.WatchOutput:
                return UiLocale.T("tut.body.output");
            case TutorialStep.PlaceLab:
                return UiLocale.T("tut.body.lab");
            case TutorialStep.RotateBuilding:
                return UiLocale.T("tut.body.rotate", KeybindStore.Hint("Rotate"));
            case TutorialStep.DragBelt:
                return UiLocale.T("tut.body.drag");
            case TutorialStep.FirstBelt:
                return UiLocale.T("tut.body.belt");
            case TutorialStep.FirstOreIn:
                return UiLocale.T("tut.body.ore_in");
            case TutorialStep.GoToCopper:
                return tut.StepAge > 30f
                    ? UiLocale.T("tut.body.go_map", KeybindStore.Hint("MoveSelection"))
                    : "";
            case TutorialStep.CopperLine:
                return UiLocale.T("tut.body.copper_line");
            case TutorialStep.SpeedUp:
                return tut.WaitStuck ? UiLocale.T("tut.body.stuck") : UiLocale.T("tut.body.speed");
            case TutorialStep.PlaceSmelter:
                return UiLocale.T("tut.body.smelter");
            case TutorialStep.FirstIngot:
                return UiLocale.T("tut.body.ingot");
            case TutorialStep.CopyLine:
                return UiLocale.T("tut.body.copy");
            case TutorialStep.PasteLine:
                return UiLocale.T("tut.body.paste", KeybindStore.Hint("Rotate"));
            case TutorialStep.OpenMachine:
                return UiLocale.T("tut.body.machine");
            case TutorialStep.Handoff:
                return UiLocale.T("tut.body.handoff", KeybindStore.Hint("ProductionMap"), KeybindStore.Hint("Goals"));
            default:
                return "";
        }
    }

    // ---------- Стрелка-указатель ----------

    void UpdatePointer(TutorialSystem tut)
    {
        if (pointer == null || root == null || root.panel == null)
            return;
        Camera cam = Camera.main;
        if (cam == null || !tut.TryGetTarget(out Vector3 target))
        {
            IndustryUi.Show(pointer, false);
            return;
        }

        Vector3 player = TutorialSystem.PlayerPos;
        float dist = Vector2.Distance(new Vector2(player.x, player.z), new Vector2(target.x, target.z));
        Vector3 sp = cam.WorldToScreenPoint(target + Vector3.up * 1.5f);
        bool behind = sp.z < 0f;
        if (behind)
            sp = new Vector3(Screen.width - sp.x, Screen.height - sp.y, -sp.z);

        float margin = Mathf.Min(Screen.width, Screen.height) * 0.08f;
        bool onScreen = !behind && sp.x > margin && sp.x < Screen.width - margin && sp.y > margin && sp.y < Screen.height - margin;

        // Цель рядом и в кадре — достаточно подсветки в мире.
        if (onScreen && dist < 10f)
        {
            IndustryUi.Show(pointer, false);
            return;
        }

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 pos = new Vector2(sp.x, sp.y);
        float angle;
        if (onScreen)
        {
            angle = 180f; // стрелка вниз, над целью
        }
        else
        {
            Vector2 dir = (pos - center).normalized;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.up;
            float sx = (Screen.width * 0.5f - margin) / Mathf.Max(0.0001f, Mathf.Abs(dir.x));
            float sy = (Screen.height * 0.5f - margin) / Mathf.Max(0.0001f, Mathf.Abs(dir.y));
            pos = center + dir * Mathf.Min(sx, sy);
            // «▲» смотрит вверх; rotate в UI Toolkit — по часовой.
            angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
        }

        Vector2 panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(pos.x, Screen.height - pos.y));
        pointer.style.left = panelPos.x - 30f;
        pointer.style.top = panelPos.y - 30f;
        pointerArrow.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
        pointerDist.text = UiLocale.T("tut.meters", Mathf.RoundToInt(dist));
        IndustryUi.Show(pointer, true);
    }

    // ---------- Подсветка UI ----------

    VisualElement FindGlow(TutorialSystem tut)
    {
        switch (tut.Step)
        {
            case TutorialStep.BuildMode:
                return FindHint(KeybindStore.Hint("BuildMode"));
            case TutorialStep.PlaceExtractor:
                return HotbarSlot(TutorialSystem.ExtractorId);
            case TutorialStep.PlaceLab:
                return HotbarSlot(TutorialSystem.LabId);
            case TutorialStep.RotateBuilding:
                if (TutorialSystem.Builder != null && TutorialSystem.Builder.HasHeldBuilding)
                    return FindHint(KeybindStore.Hint("Rotate"));
                return HotbarSlot(TutorialSystem.ConveyorId);
            case TutorialStep.DragBelt:
            case TutorialStep.FirstBelt:
                return HotbarSlot(TutorialSystem.ConveyorId);
            case TutorialStep.GoToIron:
            case TutorialStep.GoToCopper:
                return tut.StepAge > 30f ? FindHint(KeybindStore.Hint("MoveSelection")) : null;
            case TutorialStep.PlaceSmelter:
                return HotbarSlot(TutorialSystem.SmelterId) ?? FindHint(KeybindStore.Hint("Inventory"));
            case TutorialStep.CopyLine:
                if (TutorialSystem.Builder != null && TutorialSystem.Builder.Selection != null
                    && TutorialSystem.Builder.Selection.HasSelectedBuildings)
                    return FindHint(KeybindStore.Hint("Copy"));
                return FindHint(KeybindStore.Hint("SelectMode"));
            case TutorialStep.PasteLine:
                return FindHint(KeybindStore.Hint("Paste"));
            case TutorialStep.OpenMachine:
                return FindHint(KeybindStore.Hint("Interact"));
            default:
                return null;
        }
    }

    static VisualElement HotbarSlot(string buildingId)
    {
        return InventoryUI.Instance != null ? InventoryUI.Instance.FindHotbarBuilding(buildingId) : null;
    }

    VisualElement FindHint(string key)
    {
        if (string.IsNullOrEmpty(key) || InputHintUI.Instance == null)
            return null;
        VisualElement bar = InputHintUI.Instance.Bar;
        if (bar == null)
            return null;
        for (int i = 0; i < bar.childCount; i++)
        {
            VisualElement chip = bar[i];
            Label cap = chip.Q<Label>(className: "hint-key");
            if (cap != null && cap.text == key)
                return chip;
        }
        return null;
    }

    VisualElement FindNamed(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (root != null)
        {
            VisualElement own = root.Q(name);
            if (own != null)
                return own;
        }

        UIDocument[] docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);
        for (int i = 0; i < docs.Length; i++)
        {
            if (docs[i] == null || docs[i].rootVisualElement == null)
                continue;
            VisualElement found = docs[i].rootVisualElement.Q(name);
            if (found != null)
                return found;
        }
        return null;
    }

    void Glow(VisualElement el)
    {
        if (lastGlow == el)
        {
            if (el != null)
                el.EnableInClassList("tut-glow", true);
            return;
        }

        if (lastGlow != null)
            lastGlow.EnableInClassList("tut-glow", false);
        lastGlow = el;
        if (el != null)
            el.EnableInClassList("tut-glow", true);
    }

    // ---------- Ввод ----------

    void PollSkipKey()
    {
        TutorialSystem tut = TutorialSystem.Instance;
        if (tut == null || !tut.IsRunning || KeybindStore.IsListening || UiModal.IsOpen)
            return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        if (keyboard.f1Key.wasPressedThisFrame)
        {
            ConfirmSkip();
            return;
        }

        if (keyboard.f2Key.wasPressedThisFrame)
            tut.SkipStep();
        if (keyboard.f3Key.wasPressedThisFrame)
            tut.RepeatStep();
    }

    /// <summary>Пропуск всего обучения — два окна подряд: на тестах пропускали случайно и не узнавали основ.</summary>
    static void ConfirmSkip()
    {
        UiModal.Confirm(
            UiLocale.T("tut.skip_confirm1_title"),
            UiLocale.T("tut.skip_confirm1_body"),
            UiLocale.T("tut.skip_confirm1_ok"),
            () => UiModal.Confirm(
                UiLocale.T("tut.skip_confirm2_title"),
                UiLocale.T("tut.skip_confirm2_body"),
                UiLocale.T("tut.skip_confirm2_ok"),
                () =>
                {
                    if (TutorialSystem.Instance != null)
                        TutorialSystem.Instance.Skip();
                }));
    }

}
