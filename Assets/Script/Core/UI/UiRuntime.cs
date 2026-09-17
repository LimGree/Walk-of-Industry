using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class UiRuntime : MonoBehaviour
{
    public static bool DebugUi;

    static UiRuntime instance;
    static UIDocument doc;
    static Label debugLabel;

    public static VisualElement HostRoot
    {
        get
        {
            Ensure();
            return doc != null ? doc.rootVisualElement : null;
        }
    }

    public static void Ensure()
    {
        if (instance != null)
            return;
        var go = new GameObject("UiRuntime");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<UiRuntime>();
        doc = go.AddComponent<UIDocument>();
        doc.panelSettings = IndustryUi.Settings(900);
        VisualElement root = doc.rootVisualElement;
        root.Clear();
        root.AddToClassList("root");
        root.pickingMode = PickingMode.Ignore;
        StyleSheet tokens = IndustryUi.Tokens();
        if (tokens != null && !root.styleSheets.Contains(tokens))
            root.styleSheets.Add(tokens);
        StyleSheet theme = IndustryUi.Theme();
        if (theme != null && !root.styleSheets.Contains(theme))
            root.styleSheets.Add(theme);
        debugLabel = IndustryUi.Text("Debug", "UI DEBUG", "badge", "badge-warn");
        debugLabel.style.position = Position.Absolute;
        debugLabel.style.left = 24;
        debugLabel.style.top = 8;
        debugLabel.pickingMode = PickingMode.Ignore;
        debugLabel.style.display = DisplayStyle.None;
        root.Add(debugLabel);
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null && kb.f8Key.wasPressedThisFrame)
            DebugUi = !DebugUi;
        if (debugLabel != null)
            debugLabel.style.display = DebugUi ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
