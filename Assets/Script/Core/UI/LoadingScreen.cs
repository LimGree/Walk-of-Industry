using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

public class LoadingScreen : MonoBehaviour
{
    VisualElement fill;
    Label status;
    Label percent;

    float shown;
    float target;
    string statusText;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
        UnityEngine.Cursor.visible = true;
        GameAudio.Ensure();
        GameAudio.PlayMusic("music/mus_menu");
        BuildUi();
        StartCoroutine(LoadGame());
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        shown = Mathf.Lerp(shown, target, 1f - Mathf.Exp(-5.5f * dt));
        if (target > shown)
            shown = Mathf.MoveTowards(shown, target, dt * 0.22f);
        ApplyBar();
    }

    IEnumerator LoadGame()
    {
        SetTarget(0.06f, UiLocale.T("load.world"));
        yield return null;

        AsyncOperation op = SceneManager.LoadSceneAsync(MainMenu.GameSceneName);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            SetTarget(Mathf.Lerp(0.06f, 0.42f, op.progress / 0.9f), UiLocale.T("load.scene"));
            yield return null;
        }

        SetTarget(0.46f, UiLocale.T("load.start"));
        op.allowSceneActivation = true;
        while (!op.isDone)
        {
            Creep(0.52f);
            yield return null;
        }

        yield return null;
        SetTarget(0.55f, UiLocale.T("load.biomes"));
        yield return WaitReady(25f, 0.62f, () => WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsReady);
        if (WorldBiomeMap.Instance == null || !WorldBiomeMap.Instance.IsReady)
        {
            yield return FailToMenu(UiLocale.T("load.fail_biomes"));
            yield break;
        }

        SetTarget(0.64f, UiLocale.T("load.resources"));
        yield return WaitReady(60f, 0.86f, () =>
        {
            WorldResourceScatterer scatter = WorldResourceScatterer.Instance;
            if (scatter == null)
                return false;
            SetTarget(Mathf.Lerp(0.64f, 0.86f, scatter.ScatterProgress), UiLocale.T("load.resources"));
            return scatter.IsScattered;
        });
        if (WorldResourceScatterer.Instance == null || !WorldResourceScatterer.Instance.IsScattered)
        {
            yield return FailToMenu(UiLocale.T("load.fail_resources"));
            yield break;
        }

        SetTarget(0.88f, UiLocale.T("load.save"));
        if (SaveSystem.Instance != null)
        {
            yield return SaveSystem.Instance.LoadGameRoutine(p =>
                SetTarget(Mathf.Lerp(0.88f, 0.97f, p), UiLocale.T("load.save")));
        }

        SetTarget(1f, UiLocale.T("load.done"));
        while (shown < 0.995f)
            yield return null;
        yield return new WaitForSecondsRealtime(0.18f);
        Destroy(gameObject);
    }

    IEnumerator WaitReady(float seconds, float cap, System.Func<bool> ready)
    {
        float timeout = Time.unscaledTime + seconds;
        while (!ready())
        {
            if (Time.unscaledTime > timeout)
                yield break;
            Creep(cap);
            yield return null;
        }
    }

    IEnumerator FailToMenu(string reason)
    {
        SetTarget(0f, reason + "  " + UiLocale.T("load.back"));
        if (percent != null)
            percent.text = "";
        yield return new WaitForSecondsRealtime(2.2f);
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(MainMenu.MenuSceneName);
        Destroy(gameObject);
    }

    void Creep(float cap)
    {
        target = Mathf.Min(cap, target + Time.unscaledDeltaTime * 0.04f);
    }

    void SetTarget(float value, string text)
    {
        target = Mathf.Clamp01(value);
        statusText = text;
        if (status != null)
            status.text = text;
    }

    void ApplyBar()
    {
        float t = Mathf.Clamp01(shown);
        if (fill != null)
            fill.style.width = Length.Percent(t * 100f);
        if (percent != null)
            percent.text = Mathf.RoundToInt(t * 100f) + "%";
        if (status != null && !string.IsNullOrEmpty(statusText))
            status.text = statusText;
    }

    void BuildUi()
    {
        VisualElement root = IndustryUi.Mount(this, 800);
        var screen = IndustryUi.El("Bg", "bg-menu");
        screen.style.justifyContent = Justify.Center;
        screen.style.alignItems = Align.Center;
        var box = IndustryUi.El("Box", "col");
        box.style.width = 720;
        box.Add(IndustryUi.Text("Title", GameBranding.TitleCaps, "display"));
        box.Add(IndustryUi.Text("Tag", GameBranding.Tagline, "tagline"));
        status = IndustryUi.Text("Status", UiLocale.T("load.loading"), "body-text");
        box.Add(status);
        var track = IndustryUi.El("Track", "progress-track");
        track.style.marginTop = 18;
        track.style.height = 10;
        fill = IndustryUi.El("Fill", "progress-fill");
        fill.style.width = Length.Percent(0);
        fill.style.height = 10;
        track.Add(fill);
        box.Add(track);
        percent = IndustryUi.Text("Pct", "0%", "muted");
        percent.style.marginTop = 10;
        box.Add(percent);
        screen.Add(box);
        root.Add(screen);
    }
}
