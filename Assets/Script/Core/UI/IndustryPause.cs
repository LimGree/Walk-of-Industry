using UnityEngine;
using UnityEngine.UIElements;

public class IndustryPause
{
    MonoBehaviour host;
    VisualElement root;
    VisualElement home;
    VisualElement settings;
    VisualElement achievements;
    ScrollView achieveList;
    Label sessionStats;
    bool listening;
    string settingsTab = "general";

    public bool Visible => root != null && root.style.display == DisplayStyle.Flex;

    public void Build(MonoBehaviour owner)
    {
        host = owner;
        if (!listening)
        {
            UiLocale.Changed += Relocalize;
            listening = true;
        }
        if (root != null)
            return;
        VisualElement mount = IndustryUi.Mount(host, 500);
        root = IndustryUi.Screen("Pause");
        root.Add(IndustryUi.El("Dim", "dim"));
        mount.Add(root);

        home = IndustryUi.El("Home", "panel", "panel-menu");
        home.Add(IndustryUi.Text("T", UiLocale.T("pause.title"), "title-hero"));
        home.Add(IndustryUi.Btn(UiLocale.T("pause.resume"), () => GameManager.Instance.SetPaused(false), "btn-primary"));
        home.Add(IndustryUi.Btn(UiLocale.T("pause.save"), () =>
        {
            if (SaveSystem.Instance != null)
                SaveSystem.Instance.SaveGame();
            UiAudio.PlayConfirm();
            UiNotification.Push(UiLocale.T("pause.saved"), "", UiStatus.Completed);
        }));
        sessionStats = IndustryUi.Text("Session", "", "caption", "pause-stats");
        home.Add(sessionStats);
        home.Add(IndustryUi.El("Div1", "divider"));
        home.Add(IndustryUi.Btn(UiLocale.T("pause.achievements"), ShowAchievements));
        home.Add(IndustryUi.Btn(UiLocale.T("menu.settings"), () => ShowSettings("general")));
        home.Add(IndustryUi.El("Div2", "divider"));
        home.Add(IndustryUi.Btn(UiLocale.T("pause.exit"), () =>
        {
            UiModal.Confirm(
                UiLocale.T("pause.exit_title"),
                UiLocale.T("pause.exit_body"),
                UiLocale.T("pause.exit"),
                () => MainMenu.LoadMenu());
        }, "btn-danger"));
        home.Add(IndustryUi.Text("Esc", UiLocale.T("pause.esc"), "esc-hint"));
        root.Add(home);

        achievements = IndustryUi.El("Ach", "panel", "panel-menu");
        achievements.Add(IndustryUi.Text("AT", UiLocale.T("pause.achievements"), "title-hero"));
        achieveList = IndustryUi.Scroll("AchList");
        achievements.Add(achieveList);
        achievements.Add(IndustryUi.Btn(UiLocale.T("menu.back"), ShowHome, "btn-ghost"));
        root.Add(achievements);

        settings = IndustryUi.El("Settings", "panel", "panel-menu");
        settings.AddToClassList("settings-shell");
        SettingsHub.Fill(settings, ShowHome, settingsTab);
        root.Add(settings);

        IndustryUi.Show(root, false);
        ShowHome();
    }

    public void SetVisible(bool on)
    {
        IndustryUi.Show(root, on);
        if (on)
            ShowHome();
    }

    void RefreshSessionStats()
    {
        if (sessionStats == null)
            return;
        ProductionStats stats = ProductionStats.Instance;
        float sec = stats != null ? stats.SessionSeconds : 0f;
        int min = Mathf.FloorToInt(sec / 60f);
        int s = Mathf.FloorToInt(sec % 60f);
        BuildingBase[] buildings = Object.FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        Conveyor[] belts = Object.FindObjectsByType<Conveyor>(FindObjectsSortMode.None);
        int items = stats != null ? stats.TotalProducedCount() : 0;
        int coins = stats != null ? stats.CoinsGainedTotal : 0;
        sessionStats.text = UiLocale.T("pause.session",
            min.ToString("00") + ":" + s.ToString("00"),
            buildings.Length.ToString(),
            belts.Length.ToString(),
            items.ToString(),
            coins.ToString());
    }

    public void ShowHome()
    {
        IndustryUi.Show(home, true);
        IndustryUi.Show(settings, false);
        IndustryUi.Show(achievements, false);
        RefreshSessionStats();
    }

    void ShowAchievements()
    {
        FillAchievements();
        IndustryUi.Show(home, false);
        IndustryUi.Show(settings, false);
        IndustryUi.Show(achievements, true);
    }

    void FillAchievements()
    {
        if (achieveList == null)
            return;
        achieveList.Clear();
        AchievementSystem sys = AchievementSystem.Instance;
        int done = 0;
        for (int i = 0; i < AchievementSystem.Catalog.Length; i++)
        {
            AchievementSystem.Def def = AchievementSystem.Catalog[i];
            bool on = sys != null && sys.IsUnlocked(def.id);
            if (on)
                done++;
            var row = IndustryUi.El("A", "achieve-row");
            if (on)
                row.AddToClassList("is-on");
            row.Add(IndustryUi.Text("M", on ? "★" : "·", on ? "gold" : "muted"));
            var col = IndustryUi.El("C", "col", "grow");
            col.Add(IndustryUi.Text("T", UiLocale.T(def.titleKey), on ? "body-text" : "muted"));
            col.Add(IndustryUi.Text("B", UiLocale.T(def.bodyKey), "caption"));
            row.Add(col);
            achieveList.Add(row);
        }

        achieveList.Insert(0, IndustryUi.Text("Sum", done + " / " + AchievementSystem.Catalog.Length, "caption"));
    }

    void ShowSettings(string tab = "general")
    {
        settingsTab = string.IsNullOrEmpty(tab) ? SettingsHub.CurrentTab : tab;
        SettingsHub.Fill(settings, ShowHome, settingsTab);
        IndustryUi.Show(home, false);
        IndustryUi.Show(settings, true);
    }

    void Relocalize()
    {
        if (host == null)
            return;
        bool vis = Visible;
        bool onSettings = settings != null && settings.resolvedStyle.display == DisplayStyle.Flex;
        string tab = SettingsHub.CurrentTab;
        MonoBehaviour owner = host;
        VisualElement delayHost = UiRuntime.HostRoot;
        if (delayHost == null)
        {
            RebuildNow(owner, vis, onSettings, tab);
            return;
        }
        delayHost.schedule.Execute(() => RebuildNow(owner, vis, onSettings, tab));
    }

    void RebuildNow(MonoBehaviour owner, bool vis, bool onSettings, string tab)
    {
        root = null;
        home = null;
        settings = null;
        Build(owner);
        IndustryUi.Show(root, vis);
        if (!vis)
            return;
        if (onSettings)
            ShowSettings(tab);
        else
            ShowHome();
    }
}
