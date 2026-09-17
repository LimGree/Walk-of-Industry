using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Клавиша Research (T) открывает то же окно, что E по лаборатории.
/// </summary>
public class ResearchUI : MonoBehaviour
{
    public static ResearchUI Instance { get; private set; }

    public bool IsOpen =>
        MachineUI.Instance != null && MachineUI.Instance.IsOpen && MachineUI.Instance.IsLabView;

    InputSystem_Actions inputActions;

    void Awake()
    {
        Instance = this;
        inputActions = KeybindStore.Shared;
    }

    void Start()
    {
        IndustryUi.DisableHudCanvas(this);
    }

    void OnEnable()
    {
        if (inputActions != null)
            inputActions.Player.Research.performed += OnResearchToggle;
    }

    void OnDisable()
    {
        if (inputActions != null)
            inputActions.Player.Research.performed -= OnResearchToggle;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnResearchToggle(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen && !MachineUI.Instance.IsLabView)
            return;
        ToggleMenu();
    }

    public void ToggleMenu()
    {
        if (MachineUI.Instance == null)
            return;
        MachineUI.Instance.ToggleLab();
    }

    public void Open()
    {
        if (MachineUI.Instance != null)
            MachineUI.Instance.OpenLab();
    }

    public void Close()
    {
        if (MachineUI.Instance != null && MachineUI.Instance.IsLabView)
            MachineUI.Instance.Close();
    }
}
