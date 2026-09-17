using UnityEngine;

/// <summary>
/// Дальность прогрузки картинки. Логика зданий сюда не завязана.
/// </summary>
public static class WorldView
{
    static Transform player;
    static Camera cam;
    static Vector3 pos;
    static int frame = -1;
    static readonly Plane[] frustum = new Plane[6];
    static bool frustumOk;

    public static bool HasPlayer
    {
        get
        {
            Tick();
            return player != null;
        }
    }

    public static Camera Cam
    {
        get
        {
            Tick();
            return cam;
        }
    }

    public static Vector3 PlayerPos
    {
        get
        {
            Tick();
            return pos;
        }
    }

    public static float Radius
    {
        get
        {
            Tick();
            return GameSettings.RenderDistance;
        }
    }

    public static bool InRange(Vector3 world)
    {
        return InRange(world, 0f);
    }

    public static bool InRange(Vector3 world, float radius)
    {
        Tick();
        if (player == null)
            return false;
        float r = radius > 0.01f ? radius : GameSettings.RenderDistance;
        float dx = world.x - pos.x;
        float dz = world.z - pos.z;
        return dx * dx + dz * dz <= r * r;
    }

    public static bool InCamera(Vector3 world, float pad = 1.2f)
    {
        Tick();
        if (!frustumOk)
            return InRange(world);
        Bounds bounds = new Bounds(world, new Vector3(pad * 2f, pad * 2f, pad * 2f));
        return GeometryUtility.TestPlanesAABB(frustum, bounds);
    }

    static void Tick()
    {
        if (Time.frameCount == frame)
            return;
        frame = Time.frameCount;
        if (player == null)
        {
            if (GameManager.Instance != null && GameManager.Instance.playerBuilder != null)
                player = GameManager.Instance.playerBuilder.transform;
        }
        if (player != null)
            pos = player.position;
        if (cam == null)
            cam = Camera.main;
        frustumOk = cam != null;
        if (frustumOk)
            GeometryUtility.CalculateFrustumPlanes(cam, frustum);
    }
}
