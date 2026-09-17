using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Settings")]
    public float interactDistance = 4f;
    public LayerMask interactLayer = ~0; // всё по умолчанию

    public bool HasInteractableTarget => currentInteractable != null;

    public string InteractableHint
    {
        get
        {
            if (currentInteractable is BuildingBase building && building.data != null
                && !string.IsNullOrEmpty(building.data.displayName))
                return building.data.displayName;
            return "взаимодействие";
        }
    }

    private InputSystem_Actions inputActions;
    private Camera cam;
    private IInteractable currentInteractable;

    void Awake()
    {
        inputActions = KeybindStore.Shared;
        cam = Camera.main;
    }

    void OnEnable()
    {
        inputActions.Player.Interact.performed += OnInteract;
    }

    void OnDisable()
    {
        inputActions.Player.Interact.performed -= OnInteract;
    }

    void Update()
    {
        CheckForInteractable();
    }

    void CheckForInteractable()
    {
        currentInteractable = null;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactLayer))
        {
            currentInteractable = hit.collider.GetComponentInParent<IInteractable>();
        }
    }

    void OnInteract(InputAction.CallbackContext ctx)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            return;
        if (ResearchUI.Instance != null && ResearchUI.Instance.IsOpen)
            return;
        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            return;
        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
            return;
        if (WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen)
            return;
        if (currentInteractable != null)
            currentInteractable.Interact(gameObject);
    }
}

// Простой интерфейс для всего, с чем можно взаимодействовать
public interface IInteractable
{
    void Interact(GameObject interactor);
}