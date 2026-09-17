using UnityEngine;
using UnityEngine.UIElements;

public class IndustryPause
{
    MonoBehaviour host;
    VisualElement root;
    VisualElement home;
    VisualElement settings;
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
        home.Add(IndustryUi.El("Div1", "divider"));
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

    public void ShowHome()
    {
        IndustryUi.Show(home, true);
        IndustryUi.Show(settings, false);
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
