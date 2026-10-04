using UnityEngine;

/// <summary>
/// Вагонетка под игроком, пока он едет по ленте ([[BeltRide]]). Собирается из кубов,
/// цвет — раскраска из [[PerkSystem]] (cart_copper / cart_gold / cart_steam).
/// Отдельный объект в мире (не дочерний игроку): стоит по ходу ленты и не крутится за камерой.
/// </summary>
public class CartVisual : MonoBehaviour
{
    Transform model;
    string builtSkin;
    Transform smoke;
    ParticleSystem smokeFx;

    public void Show(bool on)
    {
        string skin = PerkSystem.Worn("cart") ?? "";
        if (on && (model == null || builtSkin != skin))
            Rebuild(skin);
        if (model != null)
            model.gameObject.SetActive(on);
    }

    /// <summary>Поставить вагонетку: позиция и направление ленты.</summary>
    public void Place(Vector3 position, Vector3 forward)
    {
        if (model == null)
            return;
        model.position = position;
        if (forward.sqrMagnitude > 0.0001f)
            model.rotation = Quaternion.LookRotation(forward, Vector3.up);
    }

    void OnDestroy()
    {
        if (model != null)
            Destroy(model.gameObject);
    }

    void Rebuild(string skin)
    {
        if (model != null)
            Destroy(model.gameObject);
        builtSkin = skin;
        var root = new GameObject("PlayerCart");
        root.transform.position = transform.position + Vector3.down * 0.95f;
        model = root.transform;

        Color body, trim;
        switch (skin)
        {
            case "cart_copper": body = new Color(0.74f, 0.43f, 0.2f); trim = new Color(0.45f, 0.25f, 0.12f); break;
            case "cart_gold": body = new Color(0.95f, 0.76f, 0.2f); trim = new Color(0.7f, 0.5f, 0.1f); break;
            case "cart_steam": body = new Color(0.12f, 0.14f, 0.16f); trim = new Color(0.8f, 0.15f, 0.1f); break;
            default: body = new Color(0.4f, 0.43f, 0.46f); trim = new Color(0.2f, 0.22f, 0.24f); break;
        }

        Material mBody = RuntimeMaterials.Create(body);
        Material mTrim = RuntimeMaterials.Create(trim);
        Material mWheel = RuntimeMaterials.Create(new Color(0.08f, 0.08f, 0.09f));
        // корыто: дно + 4 стенки
        Box("Floor", new Vector3(0f, 0.18f, 0f), new Vector3(0.86f, 0.06f, 1.1f), mBody);
        Box("WallL", new Vector3(-0.42f, 0.42f, 0f), new Vector3(0.06f, 0.5f, 1.1f), mBody);
        Box("WallR", new Vector3(0.42f, 0.42f, 0f), new Vector3(0.06f, 0.5f, 1.1f), mBody);
        Box("WallF", new Vector3(0f, 0.42f, 0.54f), new Vector3(0.86f, 0.5f, 0.06f), mBody);
        Box("WallB", new Vector3(0f, 0.42f, -0.54f), new Vector3(0.86f, 0.5f, 0.06f), mBody);
        // обод — рамка, а не крышка: игрок сидит внутри
        Box("RimL", new Vector3(-0.44f, 0.69f, 0f), new Vector3(0.06f, 0.05f, 1.16f), mTrim);
        Box("RimR", new Vector3(0.44f, 0.69f, 0f), new Vector3(0.06f, 0.05f, 1.16f), mTrim);
        Box("RimF", new Vector3(0f, 0.69f, 0.56f), new Vector3(0.92f, 0.05f, 0.06f), mTrim);
        Box("RimB", new Vector3(0f, 0.69f, -0.56f), new Vector3(0.92f, 0.05f, 0.06f), mTrim);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
                Box("Wheel", new Vector3(sx * 0.4f, 0.1f, sz * 0.36f), new Vector3(0.08f, 0.2f, 0.2f), mWheel);
        }

        if (skin == "cart_steam")
        {
            Box("Stack", new Vector3(0f, 0.95f, 0.42f), new Vector3(0.14f, 0.5f, 0.14f), mWheel);
            Box("StackTop", new Vector3(0f, 1.22f, 0.42f), new Vector3(0.22f, 0.06f, 0.22f), mTrim);
            smoke = new GameObject("Smoke").transform;
            smoke.SetParent(model, false);
            smoke.localPosition = new Vector3(0f, 1.28f, 0.42f);
            smokeFx = smoke.gameObject.AddComponent<ParticleSystem>();
            var main = smokeFx.main;
            main.startLifetime = 1.4f;
            main.startSpeed = 1.2f;
            main.startSize = 0.35f;
            main.startColor = new Color(0.8f, 0.8f, 0.8f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = smokeFx.emission;
            em.rateOverTime = 10f;
            var shape = smokeFx.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.05f;
            var rend = smoke.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = RuntimeMaterials.Create(new Color(0.85f, 0.85f, 0.85f, 0.5f));
        }
    }

    void Box(string name, Vector3 pos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(model, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
