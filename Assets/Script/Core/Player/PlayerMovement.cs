using UnityEngine;
using UnityEngine.InputSystem;

// Повесить на капсулу игрока (нужен CharacterController на этом же объекте).
// Камера должна быть ДОЧЕРНИМ объектом игрока (примерно на высоте головы),
// её нужно перетащить в поле cameraTransform.
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public bool canMove = true;
    public bool canLook = true;
    public float Pitch => pitch;

    [Header("Camera")]
    public Transform cameraTransform; // дочерняя камера
    public float mouseSensitivity = 200f;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Movement")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 8f;
    public float jumpHeight = 1.2f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private Vector3 velocity;
    private float pitch = 0f; // наклон камеры вверх/вниз
    Camera viewCam;
    float baseFov = 60f;
    float zoomStrength = 0.55f;
    Light flashlight;
    bool flashlightOn;

    // Input System variables
    private InputSystem_Actions inputActions;
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool isSprinting;
    private bool jumpPressed;
    private bool wasGrounded = true;
    private float stepTimer;

    // Настройки управления и камеры (GameSettings): переключаемые бег/зум, автобег, сглаживание, покачивание.
    bool sprintLatched;
    bool zoomLatched;
    bool zoomActive;
    bool autoRun;
    Vector2 smoothLook;
    Vector3 camBase;
    bool camBaseSet;
    float bobPhase;
    float bobWeight;

    // Вид камеры (F6): от первого лица → третье сзади (сдвиг вбок — настройка) → третье спереди.
    public enum CameraView { First, ThirdBack, ThirdFront }
    const string CameraViewPref = "CamView";
    const float ThirdDistance = 3.2f;
    const float ThirdHeight = 0.25f;
    static readonly RaycastHit[] camHits = new RaycastHit[8];
    CameraView view;
    float camDistance;

    public CameraView View => view;

    /// <summary>
    /// На сколько камера отъехала от головы. Добавляется к дальности стройки и взаимодействия:
    /// лучи идут из камеры, и в третьем лице иначе «съедались» бы эти метры.
    /// </summary>
    public static float ReachBonus { get; private set; }

    static Transform playerRoot;
    static readonly RaycastHit[] aimHits = new RaycastHit[16];

    /// <summary>Луч прицела, который не упирается в самого игрока (в 3-м лице он идёт сквозь тело).</summary>
    public static bool AimRaycast(Ray ray, out RaycastHit hit, float distance, int mask, QueryTriggerInteraction triggers)
    {
        int n = Physics.RaycastNonAlloc(ray, aimHits, distance, mask, triggers);
        hit = default;
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            Collider c = aimHits[i].collider;
            if (c == null || aimHits[i].distance >= best)
                continue;
            if (playerRoot != null && c.transform.IsChildOf(playerRoot))
                continue;
            best = aimHits[i].distance;
            hit = aimHits[i];
            found = true;
        }
        return found;
    }

    void Awake()
    {
        playerRoot = transform;
        inputActions = KeybindStore.Shared;
    }

    public void ApplySavedPose(Vector3 position, float yaw, float savedPitch)
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();
        bool was = controller != null && controller.enabled;
        if (controller != null)
            controller.enabled = false;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        pitch = Mathf.Clamp(savedPitch, minPitch, maxPitch);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        if (controller != null)
            controller.enabled = was;
    }

    public void TeleportToCell(Vector2Int cell)
    {
        if (WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsReady)
            cell = WorldBiomeMap.Instance.NearestWalkable(cell);

        Vector3 world = GridSystem.Instance != null
            ? GridSystem.Instance.GetCellCenter(cell, transform.position.y)
            : new Vector3(cell.x, transform.position.y, cell.y);
        world.y += 80f;
        if (Physics.Raycast(world, Vector3.down, out RaycastHit hit, 200f))
            world.y = hit.point.y + 0.08f;
        else
            world.y = transform.position.y;
        ApplySavedPose(world, transform.eulerAngles.y, pitch);
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        BindCamera();
        if (GetComponent<BeltRide>() == null)
            gameObject.AddComponent<BeltRide>();
        if (GetComponent<PlayerAvatar>() == null)
            gameObject.AddComponent<PlayerAvatar>();
        EnsureFlashlight();
        zoomStrength = Mathf.Clamp(PlayerPrefs.GetFloat("CamZoom", 0.55f), 0.2f, 0.85f);
        view = (CameraView)Mathf.Clamp(PlayerPrefs.GetInt(CameraViewPref, 0), 0, 2);

        // Подписка на события
        inputActions.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        inputActions.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        inputActions.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        inputActions.Player.Sprint.performed += ctx =>
        {
            if (GameSettings.SprintToggle)
                sprintLatched = !sprintLatched;
            else
                isSprinting = true;
        };
        inputActions.Player.Sprint.canceled += ctx =>
        {
            if (!GameSettings.SprintToggle)
                isSprinting = false;
        };

        inputActions.Player.Jump.performed += ctx => jumpPressed = true;
        inputActions.Player.Jump.canceled += ctx => jumpPressed = false;
    }

    void Update()
    {

        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            RestoreFov();
            return;
        }

        if (!UiStack.GameplayBlocked
            && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            ToggleFlashlight();

        if (TutorialSystem.Instance != null && TutorialSystem.Instance.IsModal)
        {
            RestoreFov();
            return;
        }

        if (UiStack.GameplayBlocked)
        {
            moveInput = Vector2.zero;
            lookInput = Vector2.zero;
            jumpPressed = false;
            isSprinting = false;
            sprintLatched = false;
            autoRun = false;
            if (canMove)
                HandleMovement();
            HandleZoom();
            ApplyCameraFx();
            return;
        }

        InputAction autoRunAction = KeybindStore.GetAction("AutoRun");
        if (autoRunAction != null && autoRunAction.WasPressedThisFrame())
            autoRun = !autoRun;

        InputAction viewAction = KeybindStore.GetAction("CameraView");
        if (viewAction != null && viewAction.WasPressedThisFrame() && !PhotoMode.IsActive)
            CycleView();

        if (canLook) HandleMouseLook();
        if (canMove) HandleMovement();
        HandleZoom();
        ApplyCameraFx();
    }

    /// <summary>Покачивание при ходьбе + тряска поверх базовой позы камеры.</summary>
    void ApplyCameraFx()
    {
        if (cameraTransform == null || !camBaseSet || PhotoMode.IsActive)
            return;
        float dt = Time.deltaTime;
        bool grounded = controller != null && controller.isGrounded;
        Vector2 move = EffectiveMove();
        bool walking = grounded && canMove && move.sqrMagnitude > 0.04f;
        bool sprinting = GameSettings.SprintToggle ? sprintLatched : isSprinting;
        bobWeight = Mathf.MoveTowards(bobWeight, walking ? 1f : 0f, dt * 4f);
        if (walking)
            bobPhase += dt * (sprinting ? 13f : 9.5f);
        float amp = GameSettings.HeadBob * bobWeight;
        Vector3 bob = new Vector3(Mathf.Cos(bobPhase * 0.5f) * 0.035f, Mathf.Abs(Mathf.Sin(bobPhase * 0.5f)) * 0.055f - 0.0275f, 0f) * amp;
        CameraFx.Sample(out Vector3 shake, out float roll);
        if (view != CameraView.First)
        {
            ApplyThirdPerson(shake, roll);
            return;
        }
        ReachBonus = 0f;
        cameraTransform.localPosition = camBase + bob + shake;
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, roll + Mathf.Cos(bobPhase * 0.5f) * 0.5f * amp);
    }

    void CycleView()
    {
        view = (CameraView)(((int)view + 1) % 3);
        PlayerPrefs.SetInt(CameraViewPref, (int)view);
        camDistance = 0f; // выезжать плавно от головы, а не прыгать
    }

    /// <summary>Камера за плечом или спереди; не проходит сквозь здания и рельеф.</summary>
    void ApplyThirdPerson(Vector3 shake, float roll)
    {
        bool front = view == CameraView.ThirdFront;
        Quaternion rot = Quaternion.Euler(pitch, front ? 180f : 0f, roll);
        Vector3 pivot = camBase + Vector3.up * ThirdHeight;
        Vector3 offset = rot * new Vector3(front ? 0f : GameSettings.ThirdPersonSide, 0f, -ThirdDistance);

        Vector3 pivotWorld = transform.TransformPoint(pivot);
        Vector3 dirWorld = transform.TransformDirection(offset);
        float want = dirWorld.magnitude;
        float allowed = want;
        if (want > 0.001f)
        {
            Vector3 dir = dirWorld / want;
            int n = Physics.SphereCastNonAlloc(pivotWorld, 0.2f, dir, camHits, want, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = camHits[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || camHits[i].distance <= 0f)
                    continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0.3f, camHits[i].distance - 0.05f));
            }
        }

        // Внутрь — сразу (не видеть сквозь стену), наружу — плавно.
        camDistance = allowed < camDistance
            ? allowed
            : Mathf.Lerp(camDistance, allowed, 1f - Mathf.Exp(-8f * Time.deltaTime));
        float k = want > 0.001f ? camDistance / want : 0f;
        cameraTransform.localPosition = pivot + offset * k + shake;
        cameraTransform.localRotation = rot;
        ReachBonus = front ? 0f : camDistance;
    }

    Vector2 EffectiveMove()
    {
        if (!autoRun)
            return moveInput;
        return new Vector2(moveInput.x, 1f);
    }

    void EnsureFlashlight()
    {
        if (cameraTransform == null)
            BindCamera();
        if (cameraTransform == null)
            return;
        Transform existing = cameraTransform.Find("Flashlight");
        if (existing != null)
            flashlight = existing.GetComponent<Light>();
        if (flashlight == null)
        {
            var go = new GameObject("Flashlight");
            go.transform.SetParent(cameraTransform, false);
            go.transform.localPosition = new Vector3(0.12f, -0.08f, 0.08f);
            go.transform.localRotation = Quaternion.identity;
            flashlight = go.AddComponent<Light>();
        }

        flashlight.type = LightType.Spot;
        flashlight.range = 24f;
        flashlight.spotAngle = 58f;
        flashlight.innerSpotAngle = 28f;
        flashlight.intensity = 2.4f;
        flashlight.color = new Color(1f, 0.96f, 0.88f, 1f);
        flashlight.shadows = LightShadows.None;
        flashlight.enabled = flashlightOn;
    }

    void ToggleFlashlight()
    {
        EnsureFlashlight();
        if (flashlight == null)
            return;
        flashlightOn = !flashlightOn;
        flashlight.enabled = flashlightOn;
    }

    void OnDisable()
    {
        RestoreFov();
    }

    void BindCamera()
    {
        if (cameraTransform != null)
            viewCam = cameraTransform.GetComponent<Camera>();
        if (viewCam == null)
            viewCam = GetComponentInChildren<Camera>();
        if (viewCam != null)
            baseFov = GameSettings.FieldOfView;
        if (cameraTransform != null && !camBaseSet)
        {
            camBase = cameraTransform.localPosition;
            camBaseSet = true;
        }
    }

    void RestoreFov()
    {
        if (viewCam != null && baseFov > 1f)
            viewCam.fieldOfView = baseFov;
    }

    bool ZoomBlocked()
    {
        return UiStack.GameplayBlocked;
    }

    void HandleZoom()
    {
        if (viewCam == null)
            BindCamera();
        if (viewCam == null)
            return;

        InputAction zoom = KeybindStore.GetAction("Zoom");
        bool blocked = ZoomBlocked();
        bool hold;
        if (GameSettings.ZoomToggle)
        {
            if (blocked)
                zoomLatched = false;
            else if (zoom != null && zoom.WasPressedThisFrame())
                zoomLatched = !zoomLatched;
            hold = zoomLatched;
        }
        else
            hold = !blocked && zoom != null && zoom.IsPressed();
        zoomActive = hold;
        if (hold && Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                zoomStrength = Mathf.Clamp(zoomStrength + Mathf.Sign(scroll) * 0.06f, 0.2f, 0.85f);
                PlayerPrefs.SetFloat("CamZoom", zoomStrength);
            }
        }

        baseFov = GameSettings.FieldOfView;
        float target = hold ? Mathf.Lerp(baseFov, 18f, zoomStrength) : baseFov;
        float t = 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime);
        viewCam.fieldOfView = Mathf.Lerp(viewCam.fieldOfView, target, t);
    }

    void HandleMouseLook()
    {
        Vector2 look = lookInput;
        if (GameSettings.MouseSmoothing)
        {
            smoothLook = Vector2.Lerp(smoothLook, look, 1f - Mathf.Exp(-22f * Time.unscaledDeltaTime));
            look = smoothLook;
        }
        else
            smoothLook = look;
        float sens = mouseSensitivity * GameSettings.MouseSensitivity * (zoomActive ? GameSettings.ZoomSensitivity : 1f);
        float mouseX = look.x * sens * Time.deltaTime * (GameSettings.InvertX ? -1f : 1f);
        float mouseY = look.y * sens * Time.deltaTime * (GameSettings.InvertY ? -1f : 1f);

        // Поворот тела игрока по горизонтали (yaw)
        transform.Rotate(Vector3.up * mouseX);

        // Наклон камеры по вертикали (pitch) — только камера, не всё тело
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void HandleMovement()
    {
        bool isGrounded = controller.isGrounded;
        if (isGrounded && velocity.y < 0)
            velocity.y = -2f; // прижимает к земле, чтобы isGrounded не мигал

        if (isGrounded && !wasGrounded)
            GameAudio.Player("player_land");
        wasGrounded = isGrounded;

        if (WorldBiomeMap.BlocksPlayer(transform.position, controller.radius))
            PushOutOfOcean();

        if (autoRun && moveInput.y < -0.3f)
            autoRun = false;
        Vector2 input = EffectiveMove();
        if (GameSettings.SprintToggle && input.sqrMagnitude < 0.01f)
            sprintLatched = false;
        bool sprinting = GameSettings.SprintToggle ? sprintLatched : isSprinting;
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        float speed = sprinting ? sprintSpeed : walkSpeed;
        Vector3 wish = move * speed * Time.deltaTime;
        if (!TryWalk(wish))
        {
            if (!TryWalk(new Vector3(wish.x, 0f, 0f)))
                TryWalk(new Vector3(0f, 0f, wish.z));
        }

        if (isGrounded && move.sqrMagnitude > 0.2f)
        {
            float stride = sprinting ? 0.52f : 0.72f;
            stepTimer += Time.deltaTime;
            if (stepTimer >= stride)
            {
                stepTimer = 0f;
                int n = Random.Range(1, 5);
                GameAudio.Player("player_step_0" + n);
            }
        }
        else
            stepTimer = 0f;

        // Прыжок
        if (jumpPressed && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpPressed = false; // Сбрасываем флаг, чтобы не прыгал каждый кадр
            GameAudio.Player("player_jump");
        }

        // Гравитация
        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    bool TryWalk(Vector3 delta)
    {
        if (controller == null)
            return true;
        if (delta.sqrMagnitude < 0.0000001f)
            return true;

        Vector3 before = transform.position;
        controller.Move(delta);
        if (!WorldBiomeMap.BlocksPlayer(transform.position, controller.radius))
            return true;

        controller.Move(before - transform.position);
        return false;
    }

    void PushOutOfOcean()
    {
        if (controller == null)
            return;

        Vector3 center = WorldBiomeMap.Instance != null
            ? WorldBiomeMap.Instance.PlayableCenterWorld
            : transform.position;
        Vector3 dir = center - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;
        controller.Move(dir.normalized * walkSpeed * Time.deltaTime);
    }
}