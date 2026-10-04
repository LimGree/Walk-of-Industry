using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// То, что консоли нужно делать каждый кадр: свободная и верхняя камера, скрытый HUD,
/// покадровый шаг симуляции, журнал ошибок для /bugreport. Живёт на объекте [[DevConsole]].
/// </summary>
public class DevRuntime : MonoBehaviour
{
    public enum CamMode { Player, Free, Top }

    public struct LogRow
    {
        public float time;
        public LogType type;
        public string text;
        public string stack;
    }

    const int ErrorCap = 60;

    static DevRuntime instance;
    static readonly List<LogRow> errors = new List<LogRow>(ErrorCap);
    static readonly List<UIDocument> hiddenDocs = new List<UIDocument>(16);

    public static CamMode Cam { get; private set; }
    public static float TopHeight { get; private set; } = 24f;
    public static bool HudHidden { get; private set; }
    public static IReadOnlyList<LogRow> Errors => errors;

    Transform cam;
    Transform camParent;
    Vector3 savedLocalPos;
    Quaternion savedLocalRot;
    float freeYaw;
    float freePitch;
    int stepFrames;

    public static DevRuntime Ensure(GameObject host)
    {
        if (instance != null || host == null)
            return instance;
        instance = host.GetComponent<DevRuntime>();
        if (instance == null)
            instance = host.AddComponent<DevRuntime>();
        return instance;
    }

    void OnEnable()
    {
        Application.logMessageReceived += OnLog;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= OnLog;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>Новая сцена — своя камера и свой UI: режим камеры и скрытый HUD не переносятся.</summary>
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Cam = CamMode.Player;
        cam = null;
        PlayerMovement.ExternalCamera = false;
        PlayerMovement.FreeCamera = false;
        hiddenDocs.Clear();
        HudHidden = false;
        stepFrames = 0;
    }

    static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log)
            return;
        if (errors.Count >= ErrorCap)
            errors.RemoveAt(0);
        errors.Add(new LogRow { time = Time.realtimeSinceStartup, type = type, text = condition, stack = stackTrace });
    }

    // ---------- Камера ----------

    /// <summary>Свободная камера или вид сверху. false — нет камеры.</summary>
    public static bool SetCam(CamMode mode, float height = -1f)
    {
        if (instance == null)
            return false;
        if (height > 0f)
            TopHeight = Mathf.Clamp(height, 4f, 200f);
        return instance.ApplyCam(mode);
    }

    bool ApplyCam(CamMode mode)
    {
        if (mode == Cam)
            return true;
        if (Cam == CamMode.Player)
        {
            Camera main = Camera.main;
            if (main == null)
                return false;
            cam = main.transform;
            camParent = cam.parent;
            savedLocalPos = cam.localPosition;
            savedLocalRot = cam.localRotation;
            cam.SetParent(null, true);
        }

        Cam = mode;
        PlayerMovement.ExternalCamera = mode != CamMode.Player;
        PlayerMovement.FreeCamera = mode == CamMode.Free;
        if (mode == CamMode.Free && cam != null)
        {
            Vector3 e = cam.eulerAngles;
            freeYaw = e.y;
            freePitch = e.x > 180f ? e.x - 360f : e.x;
        }

        if (mode == CamMode.Player && cam != null)
        {
            if (camParent != null)
            {
                cam.SetParent(camParent, false);
                cam.localPosition = savedLocalPos;
                cam.localRotation = savedLocalRot;
            }

            cam = null;
        }

        return true;
    }

    void LateUpdate()
    {
        if (Cam == CamMode.Player)
            return;
        if (cam == null || PhotoMode.IsActive)
            return;
        float dt = Time.unscaledDeltaTime;
        if (Cam == CamMode.Top)
        {
            PlayerMovement move = FindFirstObjectByType<PlayerMovement>();
            if (move == null)
                return;
            cam.position = move.transform.position + Vector3.up * TopHeight;
            cam.rotation = Quaternion.Euler(90f, move.transform.eulerAngles.y, 0f);
            return;
        }

        if (DevConsole.IsOpen || UiStack.GameplayBlocked)
            return;
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 look = mouse.delta.ReadValue() * 0.1f * GameSettings.MouseSensitivity;
            freeYaw += look.x;
            freePitch = Mathf.Clamp(freePitch - look.y, -89f, 89f);
        }

        cam.rotation = Quaternion.Euler(freePitch, freeYaw, 0f);
        if (kb == null)
            return;
        Vector3 dir = Vector3.zero;
        if (kb.wKey.isPressed) dir += cam.forward;
        if (kb.sKey.isPressed) dir -= cam.forward;
        if (kb.aKey.isPressed) dir -= cam.right;
        if (kb.dKey.isPressed) dir += cam.right;
        if (kb.eKey.isPressed || kb.spaceKey.isPressed) dir += Vector3.up;
        if (kb.qKey.isPressed || kb.leftCtrlKey.isPressed) dir += Vector3.down;
        float speed = (kb.leftShiftKey.isPressed ? 30f : 10f) * PlayerMovement.DevSpeedMul;
        if (dir.sqrMagnitude > 0.01f)
            cam.position += dir.normalized * speed * dt;
    }

    // ---------- HUD ----------

    /// <summary>Скрыть или вернуть весь интерфейс, кроме консоли. Возвращает сколько панелей затронуто.</summary>
    public static int SetHud(bool show)
    {
        if (show)
        {
            int n = 0;
            for (int i = 0; i < hiddenDocs.Count; i++)
            {
                if (hiddenDocs[i] == null || hiddenDocs[i].rootVisualElement == null)
                    continue;
                hiddenDocs[i].rootVisualElement.style.display = StyleKeyword.Null;
                n++;
            }

            hiddenDocs.Clear();
            HudHidden = false;
            return n;
        }

        UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < docs.Length; i++)
        {
            UIDocument doc = docs[i];
            if (doc == null || !doc.enabled || hiddenDocs.Contains(doc))
                continue;
            if (instance != null && doc.transform.IsChildOf(instance.transform))
                continue;
            // Не выключаем UIDocument: при enabled=false Unity уничтожает дерево, а UI собран кодом.
            VisualElement root = doc.rootVisualElement;
            if (root == null || root.resolvedStyle.display == DisplayStyle.None)
                continue;
            root.style.display = DisplayStyle.None;
            hiddenDocs.Add(doc);
        }

        HudHidden = true;
        return hiddenDocs.Count;
    }

    // ---------- Шаг симуляции ----------

    /// <summary>Прогнать frames кадров и снова встать на паузу консоли.</summary>
    public static void Step(int frames)
    {
        if (instance == null)
            return;
        GameSettings.DevFrozen = true;
        instance.stepFrames = Mathf.Max(1, frames);
        Time.timeScale = Mathf.Max(0.01f, GameSettings.GameSpeed * GameSettings.DevTimeScale);
    }

    void Update()
    {
        if (stepFrames <= 0)
            return;
        stepFrames--;
        if (stepFrames > 0)
            return;
        GameSettings.ApplyTimeScale();
        DevConsole.Log("шаг завершён · кадр " + Time.frameCount, DevConsole.Kind.System);
    }
}
