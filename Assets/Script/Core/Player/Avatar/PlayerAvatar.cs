using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Тело игрока в мире. Вешается на объект игрока (<see cref="PlayerMovement"/> добавляет сам).
/// Прячет старую капсулу; от первого лица тело видно только тенью, от третьего и в фоторежиме — целиком.
/// Внешность — <see cref="AvatarLook.Current"/>, общая для всех миров; после сохранения тело пересобирается.
/// </summary>
[DisallowMultipleComponent]
public class PlayerAvatar : MonoBehaviour
{
    static PlayerAvatar instance;

    PlayerMovement movement;
    CharacterController controller;
    AvatarRig rig;
    AvatarAnimator animator;
    Vector3 lastPos;
    bool lastVisible = true;
    bool rebuildQueued;

    /// <summary>Взмах рукой: поставил или снёс здание.</summary>
    public static void PlayAction()
    {
        if (instance != null && instance.animator != null)
            instance.animator.TriggerAction();
    }

    void Awake()
    {
        instance = this;
        movement = GetComponent<PlayerMovement>();
        controller = GetComponent<CharacterController>();
        // Старая капсула-заглушка: коллайдеры остаются, видимость — нет.
        MeshRenderer capsule = GetComponent<MeshRenderer>();
        if (capsule != null)
            capsule.enabled = false;
        Rebuild();
        AvatarLook.Changed += QueueRebuild;
    }

    void OnDestroy()
    {
        AvatarLook.Changed -= QueueRebuild;
        if (instance == this)
            instance = null;
    }

    void QueueRebuild()
    {
        rebuildQueued = true;
    }

    void Rebuild()
    {
        if (rig != null)
            Destroy(rig.gameObject);
        rig = AvatarRig.Build(transform, AvatarLook.Current, gameObject.layer);
        // Ноги на дне капсулы CharacterController.
        float feet = -1f;
        if (controller != null)
            feet = controller.center.y - controller.height * 0.5f - controller.skinWidth;
        rig.transform.localPosition = new Vector3(0f, feet, 0f);
        rig.transform.localRotation = Quaternion.identity;
        animator = new AvatarAnimator(rig);
        lastPos = transform.position;
        lastVisible = !lastVisible; // применить видимость заново
        ApplyVisibility();
    }

    void LateUpdate()
    {
        if (rebuildQueued)
        {
            rebuildQueued = false;
            Rebuild();
        }
        if (rig == null)
            return;

        ApplyVisibility();

        float dt = Time.deltaTime;
        Vector3 pos = transform.position;
        Vector3 delta = pos - lastPos;
        lastPos = pos;
        delta.y = 0f;
        float speed = dt > 0.0001f ? delta.magnitude / dt : 0f;
        // Телепорт/загрузка — не бег.
        if (speed > 30f)
            speed = 0f;

        float walk = movement != null ? movement.walkSpeed : 5f;
        float sprint = movement != null ? movement.sprintSpeed : 8f;
        float speed01 = speed <= walk
            ? speed / Mathf.Max(0.01f, walk)
            : 1f + Mathf.InverseLerp(walk, sprint, speed);
        if (speed < 0.15f)
            speed01 = 0f;

        bool riding = BeltRide.Instance != null && BeltRide.Instance.IsRiding;
        var motion = new AvatarAnimator.Motion
        {
            speed01 = riding ? 0f : speed01,
            grounded = riding || controller == null || controller.isGrounded,
            lookPitch = movement != null ? movement.Pitch : 0f
        };
        animator.Tick(dt, motion);
    }

    void ApplyVisibility()
    {
        bool visible = PhotoMode.IsActive
            || (movement != null && movement.View != PlayerMovement.CameraView.First);
        if (visible == lastVisible)
            return;
        lastVisible = visible;
        rig.SetShadowMode(visible ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly);
    }
}
