using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class PhotoMode : MonoBehaviour
{
    public static PhotoMode Instance { get; private set; }
    public static bool IsActive { get; private set; }

    readonly List<UIDocument> hidden = new List<UIDocument>(16);
    Transform cam;
    Transform camParent;
    Vector3 savedLocalPos;
    Quaternion savedLocalRot;
    float savedFov = 60f;
    float savedTimeScale = 1f;
    bool savedPauseAudio;
    Camera viewCam;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (IsActive)
            Exit(false);
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (KeybindStore.BlocksGameplayInput || DevConsole.IsOpen)
            return;
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;
        if (!IsActive)
        {
            if (kb.f5Key.wasPressedThisFrame
                && GameManager.Instance != null && !GameManager.Instance.IsPaused)
                Enter();
            return;
        }

        if (kb.f5Key.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
        {
            Exit(false);
            return;
        }

        if (kb.enterKey.wasPressedThisFrame)
            Snap();

        Fly(Time.unscaledDeltaTime);
    }

    void Enter()
    {
        Camera unityCam = Camera.main;
        if (unityCam == null)
            return;
        viewCam = unityCam;
        cam = unityCam.transform;
        camParent = cam.parent;
        savedLocalPos = cam.localPosition;
        savedLocalRot = cam.localRotation;
        savedFov = unityCam.fieldOfView;
        savedTimeScale = Time.timeScale;
        savedPauseAudio = AudioListener.pause;
        cam.SetParent(null, true);
        IsActive = true;
        Time.timeScale = 0f;
        AudioListener.pause = false;
        HideUi(true);
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
        UnityEngine.Cursor.visible = false;
        PlayerMovement move = FindFirstObjectByType<PlayerMovement>();
        if (move != null)
        {
            move.canMove = false;
            move.canLook = false;
        }
    }

    public void Cancel()
    {
        if (IsActive)
            Exit(false);
    }

    void Exit(bool _)
    {
        IsActive = false;
        Time.timeScale = savedTimeScale > 0.01f ? savedTimeScale : 1f;
        AudioListener.pause = savedPauseAudio;
        HideUi(false);
        if (cam != null)
        {
            if (camParent != null)
            {
                cam.SetParent(camParent, true);
                cam.localPosition = savedLocalPos;
                cam.localRotation = savedLocalRot;
            }

            if (viewCam != null)
                viewCam.fieldOfView = savedFov;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.RestoreGameplayFocus();
    }

    void Fly(float dt)
    {
        if (cam == null || viewCam == null)
            return;
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        float speed = (kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed)) ? 22f : 8f;
        Vector3 dir = Vector3.zero;
        if (kb != null)
        {
            if (kb.wKey.isPressed) dir += cam.forward;
            if (kb.sKey.isPressed) dir -= cam.forward;
            if (kb.aKey.isPressed) dir -= cam.right;
            if (kb.dKey.isPressed) dir += cam.right;
            if (kb.eKey.isPressed || kb.spaceKey.isPressed) dir += Vector3.up;
            if (kb.qKey.isPressed || kb.leftCtrlKey.isPressed) dir += Vector3.down;
        }

        if (dir.sqrMagnitude > 0.01f)
            cam.position += dir.normalized * speed * dt;

        if (mouse != null)
        {
            Vector2 look = mouse.delta.ReadValue() * 0.08f;
            cam.rotation = Quaternion.Euler(0f, look.x, 0f) * cam.rotation;
            cam.rotation = cam.rotation * Quaternion.Euler(-look.y, 0f, 0f);
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                viewCam.fieldOfView = Mathf.Clamp(viewCam.fieldOfView + (scroll > 0f ? -4f : 4f), 20f, 90f);
        }
    }

    void Snap()
    {
        byte[] png = ScreenPreview.CapturePng(1920, 1080);
        if (png == null || png.Length == 0)
            return;
        string dir = WorldCatalog.HasActive
            ? Path.Combine(WorldCatalog.WorldsFolder, WorldCatalog.Active.id, "photos")
            : Path.Combine(Application.persistentDataPath, "photos");
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
        File.WriteAllBytes(file, png);
        Debug.Log("[Photo] " + file);
    }

    void HideUi(bool hide)
    {
        if (hide)
        {
            hidden.Clear();
            UIDocument[] docs = FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < docs.Length; i++)
            {
                if (docs[i] == null || !docs[i].enabled)
                    continue;
                hidden.Add(docs[i]);
                docs[i].enabled = false;
            }
        }
        else
        {
            for (int i = 0; i < hidden.Count; i++)
            {
                if (hidden[i] != null)
                    hidden[i].enabled = true;
            }

            hidden.Clear();
        }
    }

    void OnGUI()
    {
        if (!IsActive)
            return;
        const string hint = "F5 / Esc — выход   ·   Enter — снимок   ·   WASD QE Space — полёт   ·   Shift — быстрее   ·   колесо — FOV";
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.Label(new Rect(22, Screen.height - 38, Screen.width - 40, 24), hint);
        GUI.color = Color.white;
        GUI.Label(new Rect(20, Screen.height - 40, Screen.width - 40, 24), hint);
    }
}
