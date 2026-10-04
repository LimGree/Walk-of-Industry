using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Видимые плюшки персонажа ([[PerkSystem]]): след на бегу (искры/пар/листья), крылья над головой при
/// планировании (с расцветкой), дрон-компаньон за плечом — светит ночью и показывает на сломанный/стоящий станок.
/// </summary>
public class PlayerPerkFx : MonoBehaviour
{
    PlayerMovement movement;
    Vector3 lastPos;
    float speed;

    ParticleSystem trail;
    string trailBuilt;

    Transform wings;
    string wingsBuilt;
    ParticleSystem glideSmoke;

    Transform pet;
    Transform petArrow;
    Light petLight;
    Renderer petLamp;
    Material petLampMat;
    BuildingBase petTarget;
    float nextPetScan;
    Vector3 petVel;

    void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        lastPos = transform.position;
    }

    void LateUpdate()
    {
        float dt = Mathf.Max(0.0001f, Time.deltaTime);
        Vector3 d = transform.position - lastPos;
        d.y = 0f;
        speed = Mathf.Lerp(speed, d.magnitude / dt, 1f - Mathf.Exp(-8f * dt));
        lastPos = transform.position;

        UpdateTrail();
        UpdateWings();
        UpdatePet(dt);
    }

    // ---------- След ----------

    void UpdateTrail()
    {
        string id = PerkSystem.Worn("trail");
        if (id != trailBuilt)
        {
            trailBuilt = id;
            if (trail != null)
                Destroy(trail.gameObject);
            trail = string.IsNullOrEmpty(id) ? null : MakeTrail(id);
        }

        if (trail == null)
            return;
        bool grounded = movement != null && GetComponent<CharacterController>() is CharacterController cc && cc.enabled && cc.isGrounded;
        var em = trail.emission;
        em.enabled = grounded && speed > 6.5f && !PhotoMode.IsActive;
    }

    ParticleSystem MakeTrail(string id)
    {
        var go = new GameObject("PerkTrail");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -0.9f, -0.2f);
        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;
        var em = ps.emission;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.15f;
        Color c;
        switch (id)
        {
            case "trail_sparks":
                c = new Color(1f, 0.7f, 0.2f, 1f);
                main.startLifetime = 0.4f; main.startSpeed = 2.5f; main.startSize = 0.06f; main.gravityModifier = 1f;
                em.rateOverTime = 45f;
                break;
            case "trail_steam":
                c = new Color(1f, 1f, 1f, 0.4f);
                main.startLifetime = 1f; main.startSpeed = 0.5f; main.startSize = 0.4f; main.gravityModifier = -0.05f;
                em.rateOverTime = 12f;
                break;
            default: // листья
                c = new Color(0.35f, 0.6f, 0.2f, 1f);
                main.startLifetime = 1.5f; main.startSpeed = 1f; main.startSize = 0.12f; main.gravityModifier = 0.2f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                em.rateOverTime = 10f;
                break;
        }

        main.startColor = c;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = RuntimeMaterials.Create(c);
        em.enabled = false;
        return ps;
    }

    // ---------- Крылья ----------

    void UpdateWings()
    {
        bool gliding = movement != null && movement.IsGliding;
        string skin = PerkSystem.Worn("wings") ?? "";
        if (gliding && (wings == null || wingsBuilt != skin))
            BuildWings(skin);
        if (wings != null && wings.gameObject.activeSelf != gliding)
            wings.gameObject.SetActive(gliding);
        if (glideSmoke != null)
        {
            var em = glideSmoke.emission;
            em.enabled = gliding;
        }
    }

    void BuildWings(string skin)
    {
        if (wings != null)
            Destroy(wings.gameObject);
        wingsBuilt = skin;
        wings = new GameObject("Wings").transform;
        wings.SetParent(transform, false);
        wings.localPosition = new Vector3(0f, 1.9f, -0.1f);
        const int segs = 7;
        for (int i = 0; i < segs; i++)
        {
            float t = i / (float)(segs - 1);
            Color c;
            if (skin == "wings_stripes")
                c = i % 2 == 0 ? new Color(0.95f, 0.74f, 0.16f) : new Color(0.08f, 0.08f, 0.09f);
            else if (skin == "wings_sunset")
                c = Color.Lerp(new Color(1f, 0.55f, 0.15f), new Color(0.55f, 0.2f, 0.6f), Mathf.Abs(t - 0.5f) * 2f);
            else
                c = new Color(0.16f, 0.36f, 0.44f);
            float x = Mathf.Lerp(-2f, 2f, t);
            float y = -Mathf.Pow(Mathf.Abs(x) / 2f, 2f) * 0.6f;
            GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(seg.GetComponent<Collider>());
            seg.transform.SetParent(wings, false);
            seg.transform.localPosition = new Vector3(x, y + 0.6f, 0f);
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, -x * 14f);
            seg.transform.localScale = new Vector3(0.62f, 0.05f, 1.1f);
            seg.GetComponent<MeshRenderer>().sharedMaterial = RuntimeMaterials.Create(c);
        }

        // Стропы к плечам.
        for (int s = -1; s <= 1; s += 2)
        {
            GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(line.GetComponent<Collider>());
            line.transform.SetParent(wings, false);
            line.transform.localPosition = new Vector3(s * 0.8f, 0.05f, 0f);
            line.transform.localRotation = Quaternion.Euler(0f, 0f, s * 35f);
            line.transform.localScale = new Vector3(0.02f, 1.3f, 0.02f);
            line.GetComponent<MeshRenderer>().sharedMaterial = RuntimeMaterials.Create(new Color(0.2f, 0.2f, 0.2f));
        }

        if (glideSmoke != null)
            Destroy(glideSmoke.gameObject);
        glideSmoke = null;
        if (skin == "wings_sunset")
        {
            var go = new GameObject("GlideSmoke");
            go.transform.SetParent(wings, false);
            go.transform.localPosition = new Vector3(0f, 0.4f, -0.5f);
            glideSmoke = go.AddComponent<ParticleSystem>();
            var main = glideSmoke.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 1.6f;
            main.startSpeed = 0.2f;
            main.startSize = 0.5f;
            Color c = new Color(1f, 0.6f, 0.4f, 0.35f);
            main.startColor = c;
            var em = glideSmoke.emission;
            em.rateOverTime = 16f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = RuntimeMaterials.Create(c);
        }

        wings.gameObject.SetActive(false);
    }

    // ---------- Дрон-компаньон ----------

    void UpdatePet(float dt)
    {
        bool on = PerkSystem.PetOn && !PhotoMode.IsActive;
        if (!on)
        {
            if (pet != null)
                pet.gameObject.SetActive(false);
            return;
        }

        if (pet == null)
            BuildPet();
        pet.gameObject.SetActive(true);

        // Летает за правым плечом, покачивается.
        Vector3 want = transform.TransformPoint(new Vector3(0.75f, 1.35f, -0.45f)) + Vector3.up * Mathf.Sin(Time.time * 2.2f) * 0.08f;
        pet.position = Vector3.SmoothDamp(pet.position, want, ref petVel, 0.25f);

        if (Time.unscaledTime >= nextPetScan)
        {
            nextPetScan = Time.unscaledTime + 2f;
            petTarget = FindTrouble();
        }

        bool night = DayNight.Hour >= 20f || DayNight.Hour < 6f;
        petLight.enabled = night;

        if (petTarget != null)
        {
            Vector3 to = petTarget.transform.position - pet.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                pet.rotation = Quaternion.Slerp(pet.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-6f * dt));
            petArrow.gameObject.SetActive(true);
            bool blink = Mathf.Repeat(Time.time, 0.8f) < 0.4f;
            Color c = petTarget.IsBroken ? new Color(1f, 0.2f, 0.15f) : new Color(1f, 0.82f, 0.2f);
            SetLamp(blink ? c : c * 0.3f);
        }
        else
        {
            pet.rotation = Quaternion.Slerp(pet.rotation, transform.rotation, 1f - Mathf.Exp(-4f * dt));
            petArrow.gameObject.SetActive(false);
            SetLamp(new Color(0.35f, 1f, 0.45f));
        }
    }

    void SetLamp(Color c)
    {
        if (petLampMat == null)
            return;
        petLampMat.color = c;
        if (petLampMat.HasProperty("_EmissionColor"))
            petLampMat.SetColor("_EmissionColor", c);
    }

    /// <summary>Ближайший сломанный или простаивающий станок в 80 клетках.</summary>
    BuildingBase FindTrouble()
    {
        float best = 80f * GridFootprint.CellSize;
        best *= best;
        BuildingBase found = null;
        IReadOnlyList<BuildingBase> all = WorldSim.Buildings;
        for (int i = 0; i < all.Count; i++)
        {
            BuildingBase b = all[i];
            if (b == null || !b.IsPlaced || !b.IsBroken)
                continue;
            float d = (b.transform.position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                found = b;
            }
        }

        if (found != null || MachineIdleHud.Instance == null)
            return found;
        IReadOnlyList<MachineIdleHud.IdleRow> rows = MachineIdleHud.Instance.Rows;
        for (int i = 0; i < rows.Count; i++)
        {
            BuildingBase b = rows[i].building;
            if (b == null)
                continue;
            float d = (b.transform.position - transform.position).sqrMagnitude;
            if (d < best)
            {
                best = d;
                found = b;
            }
        }

        return found;
    }

    void BuildPet()
    {
        pet = new GameObject("PetDrone").transform;
        pet.position = transform.position + Vector3.up * 2f;
        Material body = RuntimeMaterials.Create(new Color(0.16f, 0.36f, 0.44f));
        Material dark = RuntimeMaterials.Create(new Color(0.08f, 0.08f, 0.09f));
        Material copper = RuntimeMaterials.Create(new Color(0.74f, 0.43f, 0.2f));
        Cube(pet, new Vector3(0f, 0f, 0f), new Vector3(0.26f, 0.1f, 0.26f), body);
        Cube(pet, new Vector3(0f, -0.07f, 0f), new Vector3(0.16f, 0.05f, 0.16f), copper);
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Cube(pet, new Vector3(sx * 0.17f, 0.02f, sz * 0.17f), new Vector3(0.05f, 0.03f, 0.05f), dark);
                Cube(pet, new Vector3(sx * 0.17f, 0.05f, sz * 0.17f), new Vector3(0.16f, 0.008f, 0.02f), dark);
            }
        }

        petLampMat = RuntimeMaterials.Create(new Color(0.35f, 1f, 0.45f));
        petLamp = Cube(pet, new Vector3(0f, 0f, 0.14f), new Vector3(0.06f, 0.04f, 0.02f), petLampMat);
        petArrow = new GameObject("Arrow").transform;
        petArrow.SetParent(pet, false);
        petArrow.localPosition = new Vector3(0f, -0.16f, 0.12f);
        Cube(petArrow, new Vector3(0f, 0f, 0.08f), new Vector3(0.04f, 0.04f, 0.18f), copper);
        Transform tip = Cube(petArrow, new Vector3(0f, 0f, 0.19f), new Vector3(0.1f, 0.04f, 0.06f), copper).transform;
        tip.localRotation = Quaternion.Euler(0f, 45f, 0f);
        petArrow.gameObject.SetActive(false);

        var lgo = new GameObject("PetLight");
        lgo.transform.SetParent(pet, false);
        lgo.transform.localPosition = new Vector3(0f, -0.2f, 0f);
        petLight = lgo.AddComponent<Light>();
        petLight.type = LightType.Point;
        petLight.range = 8f;
        petLight.intensity = 1.2f;
        petLight.color = new Color(0.85f, 0.95f, 1f);
        petLight.shadows = LightShadows.None;
    }

    static Renderer Cube(Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return r;
    }

    void OnDestroy()
    {
        if (pet != null)
            Destroy(pet.gameObject);
    }
}
