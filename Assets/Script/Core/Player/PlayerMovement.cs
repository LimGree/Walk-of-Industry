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

    // Плюшки ([[PerkSystem]]): прыжки в воздухе, рывок, фонарь на каске.
    bool jumpEdge;
    int airJumpsLeft;
    bool fwdHeld;
    float lastFwdTap = -10f;
    float dashUntil;
    float dashReadyAt;
    Vector3 dashDir;
    Light headLamp;
    float nextWaterToast;
    const float DashCells = 6f;
    const float DashTime = 0.22f;
    const float DashCooldown = 3f;
    const float LakeSpeedMul = 0.6f;
    static readonly float[] AirJumpShare = { 0.7f, 0.5f };

    // Крылья: взлёт + планирование; крюк-кошка: полёт по тросу к точке.
    bool gliding;
    float wingsReadyAt;
    bool grappling;
    Vector3 grappleTarget;
    float grappleUntil;
    LineRenderer grappleLine;
    const float WingsLaunchHeight = 12f;
    const float WingsCooldown = 4f;
    const float GlideFall = 1.4f;
    const float GlideSpeed = 11f;
    const float GrappleRange = 30f;
    const float GrappleSpeed = 72f;

    public bool IsGliding => gliding;
    public bool IsGrappling => grappling;

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

    /// <summary>Консоль (/speed): множитель скорости ходьбы и полёта noclip.</summary>
    public static float DevSpeedMul = 1f;
    /// <summary>Консоль (/noclip): полёт сквозь всё, без гравитации.</summary>
    public static bool DevNoclip;
    /// <summary>Камерой управляет консоль (/cam free|top): своя поза камеры не применяется.</summary>
    public static bool ExternalCamera;
    /// <summary>Свободная камера консоли: игрок стоит, мышь и WASD — у камеры.</summary>
    public static bool FreeCamera;
    bool noclipActive;

    /// <summary>Консоль (/cam first|third|front).</summary>
    public void SetView(CameraView next)
    {
        view = next;
        PlayerPrefs.SetInt(CameraViewPref, (int)view);
        camDistance = 0f;
    }

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
        if (GetComponent<PlayerPerkFx>() == null)
            gameObject.AddComponent<PlayerPerkFx>();
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

        inputActions.Player.Jump.performed += ctx => { jumpPressed = true; jumpEdge = true; };
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

        if (canLook && !FreeCamera) HandleMouseLook();
        if (canMove && !FreeCamera) HandleMovement();
        UpdateHeadLamp();
        HandleZoom();
        ApplyCameraFx();
    }

    /// <summary>Покачивание при ходьбе + тряска поверх базовой позы камеры.</summary>
    void ApplyCameraFx()
    {
        if (cameraTransform == null || !camBaseSet || PhotoMode.IsActive || ExternalCamera)
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
        if (DevNoclip)
        {
            NoclipMove();
            return;
        }

        if (noclipActive)
        {
            noclipActive = false;
            velocity = Vector3.zero;
            controller.enabled = true;
        }

        if (grappling)
        {
            UpdateGrapple();
            return;
        }

        bool isGrounded = controller.isGrounded;
        if (gliding && isGrounded && velocity.y <= 0f)
            gliding = false;
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
        float speed = (sprinting ? sprintSpeed : walkSpeed) * PerkSystem.SpeedMul * DevSpeedMul;
        if (WorldBiomeMap.InLake(transform.position))
            speed *= LakeSpeedMul;
        Vector3 wish = move * speed * Time.deltaTime;
        UpdateDash(input);
        if (gliding && !isGrounded)
            wish += transform.forward * GlideSpeed * Time.deltaTime;
        if (Time.time < dashUntil)
            wish += dashDir * (DashCells * GridFootprint.CellSize / DashTime) * Time.deltaTime;
        if (!TryWalk(wish))
        {
            if (!TryWalk(new Vector3(wish.x, 0f, 0f)))
                TryWalk(new Vector3(0f, 0f, wish.z));
            WaterToast();
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

        // Прыжок (высота — «Пружинные подошвы»)
        if (isGrounded)
            airJumpsLeft = PerkSystem.AirJumps;
        if (jumpEdge && GearUsesJump())
        {
            // Крылья в руках: Пробел — взлёт/сложить/раскрыть, обычного прыжка нет.
            WingsPress(isGrounded);
            jumpPressed = false;
        }
        else if (jumpPressed && isGrounded && !GearUsesJump())
        {
            velocity.y = Mathf.Sqrt(PerkSystem.JumpHeight(jumpHeight) * -2f * gravity);
            jumpPressed = false; // Сбрасываем флаг, чтобы не прыгал каждый кадр
            GameAudio.Player("player_jump");
        }
        else if (jumpEdge && !isGrounded && airJumpsLeft > 0 && !GearUsesJump())
        {
            // Двойной/тройной прыжок: новое нажатие Пробела в воздухе.
            int used = PerkSystem.AirJumps - airJumpsLeft;
            float share = AirJumpShare[Mathf.Clamp(used, 0, AirJumpShare.Length - 1)];
            velocity.y = Mathf.Sqrt(PerkSystem.JumpHeight(jumpHeight) * share * -2f * gravity);
            airJumpsLeft--;
            GameAudio.Player("player_jump");
        }

        jumpEdge = false;

        // Гравитация (на крыльях — плавное снижение)
        velocity.y += gravity * Time.deltaTime;
        if (gliding && velocity.y < -GlideFall)
            velocity.y = -GlideFall;
        controller.Move(velocity * Time.deltaTime);
    }

    /// <summary>Полёт сквозь стены: WASD по взгляду камеры, Пробел — вверх, Ctrl/C — вниз, бег — быстрее.</summary>
    void NoclipMove()
    {
        if (!noclipActive)
        {
            noclipActive = true;
            gliding = false;
            controller.enabled = false;
        }

        velocity = Vector3.zero;
        Vector2 input = EffectiveMove();
        Transform look = cameraTransform != null ? cameraTransform : transform;
        Vector3 dir = look.forward * input.y + look.right * input.x;
        Keyboard kb = Keyboard.current;
        if (jumpPressed)
            dir += Vector3.up;
        if (kb != null && (kb.leftCtrlKey.isPressed || kb.cKey.isPressed))
            dir += Vector3.down;
        if (dir.sqrMagnitude > 1f)
            dir.Normalize();
        bool sprinting = GameSettings.SprintToggle ? sprintLatched : isSprinting;
        float speed = walkSpeed * 2.5f * (sprinting ? 3f : 1f) * DevSpeedMul;
        transform.position += dir * speed * Time.deltaTime;
    }

    /// <summary>Снаряжение в руках само забирает Пробел (крылья) — двойной прыжок тогда не срабатывает.</summary>
    static bool GearUsesJump()
    {
        return GearHotbar.Instance != null && GearHotbar.Instance.Current == "wings";
    }

    /// <summary>Подбросить вверх (спрыгнуть с вагонетки и т.п.).</summary>
    public void Launch(float upSpeed)
    {
        velocity.y = upSpeed;
        // Пробел, которым спрыгнули, не должен сразу стать двойным прыжком.
        jumpEdge = false;
        jumpPressed = false;
    }

    /// <summary>Крылья в руках: Пробел на земле — взлёт на 12 м и планирование; в воздухе — сложить/раскрыть.</summary>
    void WingsPress(bool grounded)
    {
        if (grounded)
        {
            if (Time.time < wingsReadyAt)
                return;
            velocity.y = Mathf.Sqrt(WingsLaunchHeight * -2f * gravity);
            jumpPressed = false;
            gliding = true;
            wingsReadyAt = Time.time + WingsCooldown;
            GameAudio.Player("player_jump");
            CameraFx.Shake(0.2f);
            return;
        }

        gliding = !gliding;
    }

    /// <summary>Крюк-кошка в руках, ЛКМ: луч из камеры до 30 м — по зданиям и земле.</summary>
    public void FireGrapple()
    {
        if (grappling || viewCam == null)
            return;
        Ray ray = viewCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!AimRaycast(ray, out RaycastHit hit, GrappleRange + ReachBonus, ~0, QueryTriggerInteraction.Ignore))
        {
            UiNotification.Push(UiLocale.T("gear.grapple_far"), "", UiStatus.Warning);
            UiAudio.PlayError();
            return;
        }

        if (WorldBiomeMap.BlocksPlayer(hit.point))
        {
            UiNotification.Push(UiLocale.T("water.lake"), UiLocale.T("water.lake_sub"), UiStatus.Warning);
            return;
        }

        grappleTarget = hit.point + hit.normal * 0.6f;
        grappling = true;
        gliding = false;
        grappleUntil = Time.time + 1f;
        velocity = Vector3.zero;
        GameAudio.Player("player_jump");
        EnsureGrappleLine();
        grappleLine.enabled = true;
    }

    void UpdateGrapple()
    {
        Vector3 pos = transform.position + Vector3.up * 1f;
        Vector3 to = grappleTarget - pos;
        float dist = to.magnitude;
        bool done = dist < 1.4f || Time.time > grappleUntil;
        if (!done)
        {
            Vector3 before = transform.position;
            controller.Move(to / dist * Mathf.Min(dist, GrappleSpeed * Time.deltaTime));
            // Упёрлись (стена, вода) — отпускаем трос.
            if ((transform.position - before).sqrMagnitude < 0.0001f || WorldBiomeMap.BlocksPlayer(transform.position, controller.radius))
            {
                if (WorldBiomeMap.BlocksPlayer(transform.position, controller.radius))
                    controller.Move(before - transform.position);
                done = true;
            }
        }

        if (grappleLine != null)
        {
            grappleLine.SetPosition(0, transform.position + Vector3.up * 1.3f + transform.right * 0.25f);
            grappleLine.SetPosition(1, grappleTarget);
        }

        if (!done)
            return;
        grappling = false;
        velocity = Vector3.up * 4f; // подскок на краю
        if (grappleLine != null)
            grappleLine.enabled = false;
    }

    void EnsureGrappleLine()
    {
        if (grappleLine != null)
            return;
        var go = new GameObject("GrappleLine");
        go.transform.SetParent(transform, false);
        grappleLine = go.AddComponent<LineRenderer>();
        grappleLine.positionCount = 2;
        grappleLine.startWidth = 0.04f;
        grappleLine.endWidth = 0.025f;
        grappleLine.useWorldSpace = true;
        grappleLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        grappleLine.sharedMaterial = RuntimeMaterials.Create(new Color(0.25f, 0.22f, 0.2f));
        grappleLine.enabled = false;
    }

    /// <summary>Рывок: двойное нажатие «вперёд» — 6 клеток за 0.22 с, перезарядка 3 с.</summary>
    void UpdateDash(Vector2 input)
    {
        bool fwd = input.y > 0.5f && !autoRun;
        if (fwd && !fwdHeld)
        {
            if (Time.time - lastFwdTap < 0.28f && PerkSystem.Has("dash") && Time.time >= dashReadyAt)
            {
                dashDir = transform.forward;
                dashDir.y = 0f;
                dashDir.Normalize();
                dashUntil = Time.time + DashTime;
                dashReadyAt = Time.time + DashCooldown;
                GameAudio.Player("player_jump");
                CameraFx.Shake(0.15f);
                lastFwdTap = -10f;
            }
            else
                lastFwdTap = Time.time;
        }

        fwdHeld = fwd;
    }

    /// <summary>Упёрся в воду — шутка про акул и подсказка про ласты (не чаще раза в 20 с).</summary>
    void WaterToast()
    {
        if (Time.unscaledTime < nextWaterToast || WorldBiomeMap.Instance == null)
            return;
        Vector3 ahead = transform.position + transform.forward * 1.2f;
        Vector2Int cell = BuildingLinker.WorldToCell(ahead);
        if (!WorldBiomeMap.Instance.BlocksWalk(cell))
            return;
        nextWaterToast = Time.unscaledTime + 20f;
        bool ocean = WorldBiomeMap.Instance.IsOcean(cell);
        UiNotification.Push(UiLocale.T(ocean ? "water.ocean" : "water.lake"), UiLocale.T(ocean ? "water.ocean_sub" : "water.lake_sub"), UiStatus.Warning);
    }

    /// <summary>«Фонарь на каске»: свет вокруг игрока ночью, сам.</summary>
    void UpdateHeadLamp()
    {
        bool want = PerkSystem.Has("lamp") && (DayNight.Hour >= 21f || DayNight.Hour < 6f);
        if (headLamp == null)
        {
            if (!want)
                return;
            var go = new GameObject("HeadLamp");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 2.2f, 0f);
            headLamp = go.AddComponent<Light>();
            headLamp.type = LightType.Point;
            headLamp.range = 12f;
            headLamp.intensity = 1.6f;
            headLamp.color = new Color(1f, 0.93f, 0.78f);
            headLamp.shadows = LightShadows.None;
        }

        if (headLamp.enabled != want)
            headLamp.enabled = want;
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
        if (WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsReady)
        {
            Vector2Int here = BuildingLinker.WorldToCell(transform.position);
            Vector2Int dry = WorldBiomeMap.Instance.NearestWalkable(here, 24);
            if (dry != here)
                center = WorldBiomeMap.Instance.CellWorld(dry);
        }
        Vector3 dir = center - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            dir = Vector3.forward;
        controller.Move(dir.normalized * walkSpeed * Time.deltaTime);
    }
}