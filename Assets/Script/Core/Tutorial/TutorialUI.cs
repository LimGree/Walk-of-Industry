using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class TutorialUI : MonoBehaviour
{
    public static TutorialUI Instance { get; private set; }

    VisualElement root;
    VisualElement modal;
    Label modalTitle;
    Label modalBody;
    Label modalHint;
    Button modalStart;
    Button modalSkip;
    Button modalPlay;

    VisualElement hud;
    Label hudGoal;
    Label hudBody;
    Label hudSkipHint;
    VisualElement hudActions;

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
        if (tut != null && tut.IsRunning)
        {
            tut.NoteWaitProgress();
            PollSkipKey();
        }

        if (tut == null || !tut.IsRunning)
        {
            if (lastKey != "off")
            {
                lastKey = "off";
                HideAll();
                if (GameManager.Instance != null)
                    GameManager.Instance.RestoreGameplayFocus();
            }
            return;
        }

        string key = tut.Step + "|" + Mathf.FloorToInt(tut.StepAge) + "|" + UiLocale.Code;
        if (key != lastKey)
        {
            lastKey = key;
            Apply(tut);
        }

        if (tut.IsModal)
            HoldModalFocus();

        ApplyGlow(tut);
    }

    public bool IsModalOpen
    {
        get
        {
            TutorialSystem tut = TutorialSystem.Instance;
            return tut != null && tut.IsModal && modal != null && modal.style.display == DisplayStyle.Flex;
        }
    }

    void Build()
    {
        root = IndustryUi.Mount(this, 160);
        root.pickingMode = PickingMode.Ignore;

        modal = IndustryUi.El("TutModal", "tut-modal");
        modal.pickingMode = PickingMode.Position;
        var dim = IndustryUi.El("Dim", "tut-dim");
        dim.pickingMode = PickingMode.Position;
        var panel = IndustryUi.El("Panel", "panel", "tut-panel");
        modalTitle = IndustryUi.Text("TT", "", "heading-2");
        modalBody = IndustryUi.Text("TB", "", "body-text");
        modalBody.style.whiteSpace = WhiteSpace.Normal;
        var actions = IndustryUi.El("Actions", "row", "tut-actions");
        modalSkip = IndustryUi.Btn(UiLocale.T("tut.skip"), OnSkip, "btn-ghost");
        modalStart = IndustryUi.Btn(UiLocale.T("tut.start"), OnStart, "btn-primary");
        modalPlay = IndustryUi.Btn(UiLocale.T("tut.play"), OnPlay, "btn-primary");
        modalStart.name = "TutStart";
        modalSkip.pickingMode = PickingMode.Position;
        modalStart.pickingMode = PickingMode.Position;
        modalPlay.pickingMode = PickingMode.Position;
        actions.Add(modalSkip);
        actions.Add(modalStart);
        actions.Add(modalPlay);
        panel.Add(modalTitle);
        panel.Add(modalBody);
        panel.Add(actions);
        modalHint = IndustryUi.Text("Hint", "", "tut-skip-hint");
        panel.Add(modalHint);
        modal.Add(dim);
        modal.Add(panel);
        IndustryUi.Show(modal, false);
        root.Add(modal);

        hud = IndustryUi.El("TutHud", "tut-hud");
        hud.pickingMode = PickingMode.Ignore;
        hudGoal = IndustryUi.Text("G", "", "tut-goal");
        hudBody = IndustryUi.Text("B", "", "tut-body");
        hudBody.style.whiteSpace = WhiteSpace.Normal;
        hudSkipHint = IndustryUi.Text("SkipHint", "", "tut-skip-hint");
        hudActions = IndustryUi.El("HudAct", "row", "tut-hud-actions");
        hudActions.pickingMode = PickingMode.Position;
        hudActions.Add(IndustryUi.Btn(UiLocale.T("tut.repeat"), OnRepeat, "btn-small", "btn-ghost"));
        hudActions.Add(IndustryUi.Btn(UiLocale.T("tut.skip_lab"), OnSkipLab, "btn-small", "btn-ghost"));
        hudActions.Add(IndustryUi.Btn(UiLocale.T("tut.skip_belts"), OnSkipBelts, "btn-small", "btn-ghost"));
        hud.Add(hudGoal);
        hud.Add(hudBody);
        hud.Add(hudActions);
        hud.Add(hudSkipHint);
        IndustryUi.Show(hud, false);
        root.Add(hud);
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
        IndustryUi.Show(modal, false);
        IndustryUi.Show(hud, false);
        if (root != null)
            root.pickingMode = PickingMode.Ignore;
        Glow(null);
    }

    void Apply(TutorialSystem tut)
    {
        bool welcome = tut.Step == TutorialStep.Welcome;
        bool bye = tut.Step == TutorialStep.Farewell;
        bool quiet = tut.Step == TutorialStep.WaitChapter1;

        IndustryUi.Show(modal, welcome || bye);
        IndustryUi.Show(hud, !welcome && !bye);
        IndustryUi.Show(modalStart, welcome);
        IndustryUi.Show(modalPlay, bye);
        IndustryUi.Show(hudBody, !quiet);
        if (root != null)
            root.pickingMode = welcome || bye ? PickingMode.Position : PickingMode.Ignore;

        if (welcome || bye)
            HoldModalFocus();
        else if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();

        if (welcome)
        {
            modalTitle.text = UiLocale.T("tut.welcome.title");
            modalBody.text = UiLocale.T("tut.welcome.body");
            IndustryUi.SetButtonLabel(modalStart, UiLocale.T("tut.start"));
            IndustryUi.SetButtonLabel(modalSkip, UiLocale.T("tut.skip"));
            if (modalHint != null)
                modalHint.text = UiLocale.T("tut.skip_key", "F1", "F2", "F3");
        }
        else if (bye)
        {
            modalTitle.text = UiLocale.T("tut.bye.title");
            modalBody.text = UiLocale.T("tut.bye.body");
            IndustryUi.SetButtonLabel(modalPlay, UiLocale.T("tut.play"));
            IndustryUi.SetButtonLabel(modalSkip, UiLocale.T("tut.skip"));
            if (modalHint != null)
                modalHint.text = UiLocale.T("tut.skip_key", "F1", "F2", "F3");
        }

        hudGoal.text = GoalText(tut);
        hudBody.text = BodyText(tut);
        if (hudSkipHint != null)
            hudSkipHint.text = UiLocale.T("tut.skip_key", "F1", "F2", "F3");
    }

    static string GoalText(TutorialSystem tut)
    {
        string b = KeybindStore.Hint("BuildMode");
        string i = KeybindStore.Hint("Inventory");
        string tab = KeybindStore.Hint("SelectMode");
        string copy = KeybindStore.Hint("Copy");
        string paste = KeybindStore.Hint("Paste");
        string interact = KeybindStore.Hint("Interact");
        string research = KeybindStore.Hint("Research");
        string place = KeybindStore.Hint("Place");

        switch (tut.Step)
        {
            case TutorialStep.LookMove:
                return UiLocale.T("tut.obj.look");
            case TutorialStep.BuildMode:
                return UiLocale.T("tut.obj.build", b);
            case TutorialStep.Hotbar:
                return UiLocale.T("tut.obj.hotbar", i);
            case TutorialStep.IronOne:
                return UiLocale.T("tut.obj.iron");
            case TutorialStep.IronThree:
                return UiLocale.T("tut.obj.iron3");
            case TutorialStep.Copy:
                return UiLocale.T("tut.obj.copy", tab, copy);
            case TutorialStep.PasteCopper:
                return UiLocale.T("tut.obj.paste", paste);
            case TutorialStep.Lab:
                return UiLocale.T("tut.obj.lab");
            case TutorialStep.StartResearch:
                return UiLocale.T("tut.obj.research", interact, research);
            case TutorialStep.Belts:
                return UiLocale.T("tut.obj.belts", place);
            case TutorialStep.WaitChapter1:
                return UiLocale.T(
                    "tut.obj.wait",
                    tut.Submitted(TutorialSystem.IronOreId),
                    tut.Required(TutorialSystem.IronOreId),
                    tut.Submitted(TutorialSystem.CopperOreId),
                    tut.Required(TutorialSystem.CopperOreId));
            case TutorialStep.PlaceSmelter:
                return UiLocale.T("tut.obj.smelter");
            case TutorialStep.ResearchIronIngot:
                return UiLocale.T(
                    "tut.obj.ingot_iron",
                    interact,
                    research,
                    tut.Submitted(TutorialSystem.IronOreId),
                    tut.Required(TutorialSystem.IronOreId));
            case TutorialStep.PickRecipe:
                return UiLocale.T("tut.obj.recipe", interact);
            case TutorialStep.FirstSmelt:
                return UiLocale.T("tut.obj.smelt");
            case TutorialStep.ResearchCopperIngot:
                return UiLocale.T(
                    "tut.obj.ingot_copper",
                    interact,
                    research,
                    tut.Submitted(TutorialSystem.CopperOreId),
                    tut.Required(TutorialSystem.CopperOreId));
            case TutorialStep.CopySmelter:
                return UiLocale.T("tut.obj.copy_smelter", tab, copy, paste);
            default:
                return "";
        }
    }

    static string BodyText(TutorialSystem tut)
    {
        string rot = KeybindStore.Hint("Rotate");
        string map = KeybindStore.Hint("MoveSelection");
        string interact = KeybindStore.Hint("Interact");
        string research = KeybindStore.Hint("Research");
        string empty = KeybindStore.Hint("SelectMode");

        switch (tut.Step)
        {
            case TutorialStep.LookMove:
                return UiLocale.T("tut.body.look");
            case TutorialStep.BuildMode:
                return UiLocale.T("tut.body.build");
            case TutorialStep.Hotbar:
                return UiLocale.T("tut.body.hotbar");
            case TutorialStep.IronOne:
                return tut.StepAge > 40f
                    ? UiLocale.T("tut.body.iron_map", map)
                    : UiLocale.T("tut.body.iron", rot);
            case TutorialStep.IronThree:
                return UiLocale.T("tut.body.iron3", rot);
            case TutorialStep.Copy:
                return UiLocale.T("tut.body.copy", empty);
            case TutorialStep.PasteCopper:
                return tut.StepAge > 40f
                    ? UiLocale.T("tut.body.paste_map", map)
                    : UiLocale.T("tut.body.paste", rot);
            case TutorialStep.Lab:
                return UiLocale.T("tut.body.lab");
            case TutorialStep.StartResearch:
                return tut.StepAge > 15f
                    ? UiLocale.T("tut.body.research_stuck", interact, research)
                    : UiLocale.T("tut.body.research");
            case TutorialStep.Belts:
                return UiLocale.T("tut.body.belts", rot);
            case TutorialStep.WaitChapter1:
                return UiLocale.T("tut.body.wait", interact);
            case TutorialStep.PlaceSmelter:
                return UiLocale.T("tut.body.smelter");
            case TutorialStep.ResearchIronIngot:
                return UiLocale.T("tut.body.ingot_iron", interact, research);
            case TutorialStep.PickRecipe:
                return UiLocale.T("tut.body.recipe");
            case TutorialStep.FirstSmelt:
                return UiLocale.T("tut.body.smelt");
            case TutorialStep.ResearchCopperIngot:
                return UiLocale.T("tut.body.ingot_copper", interact, research);
            case TutorialStep.CopySmelter:
                return UiLocale.T("tut.body.copy_smelter");
            default:
                return "";
        }
    }

    void ApplyGlow(TutorialSystem tut)
    {
        VisualElement target = FindGlow(tut);
        Glow(target);
        if (tut.Step == TutorialStep.Welcome)
            Glow(modalStart);
    }

    VisualElement FindGlow(TutorialSystem tut)
    {
        switch (tut.Step)
        {
            case TutorialStep.BuildMode:
                return FindHint(KeybindStore.Hint("BuildMode"));
            case TutorialStep.Hotbar:
                if (InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen)
                {
                    VisualElement card = InventoryUI.Instance.FindBagCard(NextHotbarNeed());
                    if (card != null)
                        return card;
                    return InventoryUI.Instance.FirstEmptySlot();
                }
                return FindHint(KeybindStore.Hint("Inventory"));
            case TutorialStep.IronOne:
            case TutorialStep.PasteCopper:
                if (tut.StepAge > 40f)
                    return FindHint(KeybindStore.Hint("MoveSelection"));
                return null;
            case TutorialStep.Copy:
                if (TutorialSystem.Builder != null && TutorialSystem.Builder.Selection != null
                    && TutorialSystem.Builder.Selection.HasSelectedBuildings)
                    return FindHint(KeybindStore.Hint("Copy"));
                return FindHint(KeybindStore.Hint("SelectMode"));
            case TutorialStep.StartResearch:
                return GlowResearch(tut, TutorialSystem.BasicId);
            case TutorialStep.ResearchIronIngot:
                return GlowResearch(tut, TutorialSystem.IronIngotResearchId);
            case TutorialStep.ResearchCopperIngot:
                return GlowResearch(tut, TutorialSystem.CopperIngotResearchId);
            case TutorialStep.PlaceSmelter:
                if (InventoryUI.Instance != null)
                {
                    VisualElement slot = InventoryUI.Instance.FindHotbarBuilding(TutorialSystem.SmelterId);
                    if (slot != null)
                        return slot;
                    if (InventoryUI.Instance.IsBagOpen)
                        return InventoryUI.Instance.FindBagCard(TutorialSystem.SmelterId);
                }
                return FindHint(KeybindStore.Hint("Inventory"));
            case TutorialStep.PickRecipe:
                return FindNamed("Rec_" + TutorialSystem.IronIngotRecipeId)
                    ?? FindHint(KeybindStore.Hint("Interact"));
            case TutorialStep.CopySmelter:
                if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
                    return FindNamed("Rec_" + TutorialSystem.CopperIngotRecipeId);
                if (TutorialSystem.Builder != null && TutorialSystem.Builder.Selection != null
                    && TutorialSystem.Builder.Selection.HasClipboard)
                    return FindHint(KeybindStore.Hint("Paste"));
                if (TutorialSystem.Builder != null && TutorialSystem.Builder.Selection != null
                    && TutorialSystem.Builder.Selection.HasSelectedBuildings)
                    return FindHint(KeybindStore.Hint("Copy"));
                return FindHint(KeybindStore.Hint("SelectMode"));
            default:
                return null;
        }
    }

    VisualElement GlowResearch(TutorialSystem tut, string researchId)
    {
        VisualElement node = FindNamed("Res_" + researchId);
        if (node != null)
            return node;
        VisualElement start = FindNamed("ResearchStart");
        if (start != null)
            return start;
        if (tut.StepAge > 15f)
        {
            PlayerInteractor interactor = TutorialSystem.Builder != null
                ? TutorialSystem.Builder.GetComponent<PlayerInteractor>()
                : null;
            if (interactor != null && interactor.HasInteractableTarget)
                return FindHint(KeybindStore.Hint("Interact"));
            return FindHint(KeybindStore.Hint("Research"));
        }
        return null;
    }

    static string NextHotbarNeed()
    {
        if (!HasHotbar(TutorialSystem.ExtractorId))
            return TutorialSystem.ExtractorId;
        if (!HasHotbar(TutorialSystem.ConveyorId))
            return TutorialSystem.ConveyorId;
        return TutorialSystem.LabId;
    }

    static bool HasHotbar(string id)
    {
        PlayerInventory inv = TutorialSystem.Inventory;
        if (inv == null || inv.hotbar == null)
            return false;
        for (int i = 0; i < inv.hotbar.Length; i++)
        {
            if (inv.hotbar[i] != null && TutorialSystem.IdsEqual(inv.hotbar[i].id, id))
                return true;
        }
        return false;
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

    void PollSkipKey()
    {
        TutorialSystem tut = TutorialSystem.Instance;
        if (tut == null || !tut.IsRunning)
            return;
        if (KeybindStore.IsListening)
            return;
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;
        if (keyboard.f1Key.wasPressedThisFrame)
        {
            tut.Skip();
            return;
        }

        if (keyboard.f2Key.wasPressedThisFrame)
            tut.SkipStep();
        if (keyboard.f3Key.wasPressedThisFrame)
            tut.RepeatStep();
    }

    void OnRepeat()
    {
        TutorialSystem.Instance?.RepeatStep();
    }

    void OnSkipLab()
    {
        TutorialSystem.Instance?.SkipToLab();
    }

    void OnSkipBelts()
    {
        TutorialSystem.Instance?.SkipToBelts();
    }

    void HoldModalFocus()
    {
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        PlayerMovement movement = Object.FindFirstObjectByType<PlayerMovement>();
        if (movement == null)
            return;
        movement.canMove = false;
        movement.canLook = false;
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

    void OnStart()
    {
        TutorialSystem.Instance?.AcceptWelcome();
    }

    void OnPlay()
    {
        TutorialSystem.Instance?.FinishFarewell();
    }

    void OnSkip()
    {
        TutorialSystem.Instance?.Skip();
    }
}
