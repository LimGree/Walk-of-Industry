using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Settings")]
    public float interactDistance = 4f;
    public LayerMask interactLayer = ~0; // всё по умолчанию

    public bool HasInteractableTarget => currentInteractable != null
        && !(currentInteractable is Decoration decor && !decor.CanInteract);

    public string InteractableHint
    {
        get
        {
            if (currentInteractable is Decoration decoration)
                return decoration.InteractHint;
            if (currentInteractable is Conveyor || currentInteractable is Splitter)
            {
                if (!BeltRide.BuildModeOn())
                    return UiLocale.T("hint.ride_belt");
                if (currentInteractable is Conveyor && !ResearchSystem.BeltFilterUnlocked())
                    return "";
                return UiLocale.T("hint.belt_menu");
            }
            if (currentInteractable is BuildingBase broken && broken.IsBroken)
                return UiLocale.T("hint.repair", broken.data != null ? broken.data.Title : "");
            if (currentInteractable is BuildingBase building && building.data != null
                && !string.IsNullOrEmpty(building.data.Title))
                return building.data.Title;
            return UiLocale.T("hint.interact");
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
        if (KeybindStore.BlocksGameplayInput || PhotoMode.IsActive)
            return;
        if (BeltRide.Instance != null && BeltRide.Instance.IsRiding)
        {
            BeltRide.Instance.Stop();
            return;
        }
        if (UiStack.GameplayBlocked)
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