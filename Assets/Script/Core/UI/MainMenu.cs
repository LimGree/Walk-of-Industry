using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class MainMenu : MonoBehaviour
{
    public const string GameSceneName = "SampleScene";
    public const string MenuSceneName = "MainMenu";
    public const string LoadingSceneName = "Loading";

    VisualElement home;
    VisualElement settings;
    VisualElement worlds;
    VisualElement create;
    ScrollView worldList;
    TextField nameField;
    TextField seedField;
    Button continueBtn;
    bool rebuildQueued;

    void Awake()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        GameAudio.Ensure();
        GameSettings.Apply();
        GameAudio.PlayMusic("music/mus_menu");
        BuildUi();
        UiLocale.Changed += QueueRebuild;
    }

    void OnDestroy()
    {
        UiLocale.Changed -= QueueRebuild;
    }

    void QueueRebuild()
    {
        rebuildQueued = true;
    }

    void Update()
    {
        bool chord = SandboxChordHeld();
        if (continueBtn != null)
        {
            string label = chord ? UiLocale.T("menu.test_yard") : UiLocale.T("menu.continue");
            IndustryUi.SetButtonLabel(continueBtn, label);
            if (string.IsNullOrEmpty(continueBtn.Q<Label>(className: "btn-label")?.text))
                continueBtn.text = label;
        }

        if (chord && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame
            && home != null && home.resolvedStyle.display != DisplayStyle.None)
            ContinueLatest();
    }

    void LateUpdate()
    {
        if (!rebuildQueued)
            return;
        rebuildQueued = false;
        BuildUi();
    }

    void OnContinuePointer(PointerDownEvent evt)
    {
        if (evt.button != 0 || !SandboxChordHeld())
            return;
        evt.StopImmediatePropagation();
        ContinueLatest();
    }

    public static void LoadGame()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(LoadingSceneName);
    }

    public static void LoadMenu()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (GameManager.Instance != null)
            GameManager.Instance.PrepareLeaveGameplay();
        SceneManager.LoadScene(MenuSceneName);
    }

    void BuildUi()
    {
        string page = CurrentPage();
        VisualElement root = IndustryUi.Mount(this, 10);
        var bg = IndustryUi.El("Bg", "bg-menu");
        root.Add(bg);

        home = MenuPanel("Home");
        home.Add(IndustryUi.Text("Title", "WALK", "display"));
        home.Add(IndustryUi.Text("Title2", "OF INDUSTRY", "title-hero"));
        home.Add(IndustryUi.Text("Tag", GameBranding.Tagline, "tagline"));
        continueBtn = IndustryUi.Btn(UiLocale.T("menu.continue"), ContinueLatest, "btn-primary");
        continueBtn.name = "Continue";
        continueBtn.RegisterCallback<PointerDownEvent>(OnContinuePointer, TrickleDown.TrickleDown);
        home.Add(continueBtn);
        home.Add(IndustryUi.Btn(UiLocale.T("menu.worlds"), ShowWorlds));
        home.Add(IndustryUi.Btn(UiLocale.T("menu.settings"), ShowSettings));
        home.Add(IndustryUi.Btn(UiLocale.T("menu.exit"), Quit, "btn-ghost"));
        bg.Add(home);

        worlds = MenuPanel("Worlds");
        worlds.style.width = 640;
        worlds.Add(IndustryUi.Text("Title", UiLocale.T("menu.worlds"), "title-hero"));
        worldList = new ScrollView();
        worldList.AddToClassList("scroll");
        worldList.style.maxHeight = 420;
        worlds.Add(worldList);
        worlds.Add(IndustryUi.Btn(UiLocale.T("menu.new_world"), ShowCreate, "btn-primary"));
        worlds.Add(IndustryUi.Btn(UiLocale.T("menu.back"), ShowHome, "btn-ghost"));
        bg.Add(worlds);

        create = MenuPanel("Create");
        create.Add(IndustryUi.Text("Title", UiLocale.T("menu.new_world"), "title-hero"));
        nameField = new TextField { value = UiLocale.T("menu.default_world", WorldCatalog.ListWorlds().Count + 1) };
        nameField.AddToClassList("field");
        nameField.label = "";
        nameField.style.unityTextAlign = TextAnchor.MiddleLeft;
        create.Add(nameField);
        seedField = new TextField { value = "" };
        seedField.AddToClassList("field");
        seedField.label = UiLocale.T("menu.seed_field");
        create.Add(seedField);
        var seedRow = IndustryUi.El("SeedRow", "row");
        seedRow.Add(IndustryUi.Btn(UiLocale.T("menu.seed_random"), () => { seedField.value = ""; }, "btn-small", "btn-ghost"));
        seedRow.Add(IndustryUi.Btn(UiLocale.T("menu.seed_copy"), CopyLastSeed, "btn-small", "btn-ghost"));
        create.Add(seedRow);
        create.Add(IndustryUi.Btn(UiLocale.T("menu.create"), CreateAndPlay, "btn-primary"));
        create.Add(IndustryUi.Btn(UiLocale.T("menu.back"), ShowWorlds, "btn-ghost"));
        bg.Add(create);

        settings = MenuPanel("Settings");
        settings.AddToClassList("settings-shell");
        SettingsHub.Fill(settings, ShowHome, SettingsHub.CurrentTab);
        bg.Add(settings);

        if (page == "settings")
            ShowSettings();
        else if (page == "worlds")
            ShowWorlds();
        else if (page == "create")
            ShowCreate();
        else
            ShowHome();
    }

    string CurrentPage()
    {
        if (settings != null && settings.resolvedStyle.display == DisplayStyle.Flex)
            return "settings";
        if (worlds != null && worlds.resolvedStyle.display == DisplayStyle.Flex)
            return "worlds";
        if (create != null && create.resolvedStyle.display == DisplayStyle.Flex)
            return "create";
        return "home";
    }

    VisualElement MenuPanel(string name)
    {
        var panel = IndustryUi.El(name, "panel", "panel-menu");
        IndustryUi.Show(panel, false);
        return panel;
    }

    void ShowSettings()
    {
        HideAll();
        SettingsHub.Fill(settings, ShowHome, SettingsHub.CurrentTab);
        IndustryUi.Show(settings, true);
    }

    void ShowHome()
    {
        HideAll();
        IndustryUi.Show(home, true);
    }

    void ShowWorlds()
    {
        HideAll();
        IndustryUi.Show(worlds, true);
        RefreshWorldList();
    }

    void ShowCreate()
    {
        HideAll();
        IndustryUi.Show(create, true);
        if (nameField != null)
            nameField.value = UiLocale.T("menu.default_world", WorldCatalog.ListWorlds().Count + 1);
        if (seedField != null)
            seedField.value = "";
    }

    void HideAll()
    {
        IndustryUi.Show(home, false);
        IndustryUi.Show(settings, false);
        IndustryUi.Show(worlds, false);
        IndustryUi.Show(create, false);
    }

    void PlayWorld(WorldInfo world)
    {
        WorldCatalog.SetActive(world);
        LoadGame();
    }

    void CreateAndPlay()
    {
        int seed = WorldCatalog.ParseSeed(seedField != null ? seedField.value : "");
        WorldInfo world = WorldCatalog.CreateWorld(nameField != null ? nameField.value : "", false, seed);
        PlayWorld(world);
    }

    void CopyLastSeed()
    {
        if (seedField == null)
            return;
        List<WorldInfo> list = WorldCatalog.ListWorlds();
        if (list.Count == 0)
            return;
        seedField.value = list[0].seed.ToString();
    }

    void RefreshWorldList()
    {
        if (worldList == null)
            return;
        for (int c = 0; c < worldList.childCount; c++)
        {
            Texture2D old = worldList[c].userData as Texture2D;
            if (old != null)
                Destroy(old);
        }

        worldList.Clear();
        List<WorldInfo> list = WorldCatalog.ListWorlds();
        if (list.Count == 0)
        {
            worldList.Add(IndustryUi.Text("Empty", UiLocale.T("menu.empty_worlds"), "muted"));
            return;
        }

        for (int i = 0; i < list.Count; i++)
        {
            WorldInfo world = list[i];
            var card = IndustryUi.El(world.id, "card", "world-card");
            var body = IndustryUi.El("Body", "row");
            Texture2D preview = WorldCatalog.LoadPreview(world);
            var thumb = IndustryUi.El("Thumb", "world-preview");
            if (preview != null)
            {
                thumb.style.backgroundImage = new StyleBackground(preview);
                card.userData = preview;
            }
            body.Add(thumb);
            var meta = IndustryUi.El("Meta", "col", "grow");
            meta.Add(IndustryUi.Text("Idx", UiLocale.T("menu.world_n", (i + 1).ToString("00")), "label-caps"));
            meta.Add(IndustryUi.Text("Name", world.name, "heading-3"));
            meta.Add(IndustryUi.Text("Played", UiLocale.T("menu.last_played", world.lastPlayed), "caption"));
            meta.Add(IndustryUi.Text("Seed", UiLocale.T("menu.seed", world.seed), "caption"));
            int coins = WorldCatalog.PeekCoins(world);
            meta.Add(IndustryUi.Text("Coins", UiLocale.T("menu.world_coins", coins), "caption"));
            body.Add(meta);
            card.Add(body);
            var actions = IndustryUi.El("A", "row");
            actions.Add(IndustryUi.Btn(UiLocale.T("menu.play"), () => PlayWorld(world), "btn-small", "btn-primary"));
            WorldInfo captured = world;
            actions.Add(IndustryUi.Btn(UiLocale.T("menu.delete"), () =>
            {
                UiModal.Confirm(
                    UiLocale.T("menu.delete_title"),
                    UiLocale.T("menu.delete_body", captured.name),
                    UiLocale.T("menu.delete"),
                    () =>
                    {
                        WorldCatalog.DeleteWorld(captured.id);
                        RefreshWorldList();
                    });
            }, "btn-small", "btn-danger"));
            card.Add(actions);
            worldList.Add(card);
        }
    }

    void ContinueLatest()
    {
        if (SandboxChordHeld())
        {
            PlayWorld(WorldCatalog.CreateWorld("ТЕСТ", true));
            return;
        }

        List<WorldInfo> list = WorldCatalog.ListWorlds();
        if (list.Count == 0)
        {
            ShowCreate();
            return;
        }
        PlayWorld(list[0]);
    }

    static bool SandboxChordHeld()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return false;
        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
        bool alt = kb.leftAltKey.isPressed || kb.rightAltKey.isPressed;
        return shift && ctrl && alt;
    }

    static void Quit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
