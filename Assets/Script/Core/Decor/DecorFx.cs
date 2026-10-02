using UnityEngine;

/// <summary>Частицы декораций: искры костра, дымок мангала, брызги фонтана (шейдеры частиц из Resources).</summary>
public static class DecorFx
{
    static Material puffMat;
    static Material sparkMat;
    static Texture2D dot;

    public static void Sparks(Transform parent, Vector3 local)
    {
        ParticleSystem ps = Make(parent, "Sparks", local);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.3f, 1f), new Color(1f, 0.45f, 0.1f, 1f));
        main.gravityModifier = -0.15f;
        main.maxParticles = 40;
        var emission = ps.emission;
        emission.rateOverTime = 10f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 18f;
        shape.radius = 0.12f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        FadeOut(ps);
        Finish(ps, SparkMaterial());
    }

    public static void Smoke(Transform parent, Vector3 local)
    {
        ParticleSystem ps = Make(parent, "Smoke", local);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.75f, 0.75f, 0.45f), new Color(0.55f, 0.55f, 0.57f, 0.35f));
        main.gravityModifier = -0.03f;
        main.maxParticles = 30;
        var emission = ps.emission;
        emission.rateOverTime = 4f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.15f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2f));
        FadeOut(ps);
        Finish(ps, PuffMaterial());
    }

    public static void Splash(Transform parent, Vector3 local)
    {
        ParticleSystem ps = Make(parent, "Splash", local);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.9f, 1f, 0.8f), new Color(0.55f, 0.78f, 1f, 0.6f));
        main.gravityModifier = 0.6f;
        main.maxParticles = 60;
        var emission = ps.emission;
        emission.rateOverTime = 24f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 35f;
        shape.radius = 0.05f;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        FadeOut(ps);
        Finish(ps, PuffMaterial());
    }

    static ParticleSystem Make(Transform parent, string name, Vector3 local)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = local;
        go.layer = parent.gameObject.layer;
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.duration = 3f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        return ps;
    }

    static void FadeOut(ParticleSystem ps)
    {
        var color = ps.colorOverLifetime;
        color.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.12f), new GradientAlphaKey(0f, 1f) });
        color.color = grad;
    }

    static void Finish(ParticleSystem ps, Material mat)
    {
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sharedMaterial = mat;
        ps.Play();
    }

    static Material PuffMaterial()
    {
        if (puffMat != null)
            return puffMat;
        puffMat = Build("WalkToBiomeParticle", "Hidden/WalkToBiome/Particle", "FX/fx_puff", "DecorPuff", new Color(0.9f, 0.9f, 0.9f, 0.85f));
        return puffMat;
    }

    static Material SparkMaterial()
    {
        if (sparkMat != null)
            return sparkMat;
        sparkMat = Build("WalkToBiomeParticleAdd", "Hidden/WalkToBiome/ParticleAdd", "FX/fx_dot", "DecorSpark", new Color(1f, 0.8f, 0.5f, 1f));
        return sparkMat;
    }

    static Material Build(string resource, string shaderName, string texture, string name, Color tint)
    {
        Shader shader = Resources.Load<Shader>(resource);
        if (shader == null)
            shader = Shader.Find(shaderName);
        Texture2D tex = Resources.Load<Texture2D>(texture);
        if (tex == null)
            tex = Dot();
        Material mat;
        if (shader != null)
        {
            mat = new Material(shader) { name = name };
            mat.SetTexture("_MainTex", tex);
            mat.SetColor("_Color", tint);
            RuntimeMaterials.ApplyWorldGfx(mat);
        }
        else
            mat = RuntimeMaterials.Create(tex, tint);
        return mat;
    }

    static Texture2D Dot()
    {
        if (dot != null)
            return dot;
        const int n = 32;
        dot = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n - 0.5f;
                float dy = (y + 0.5f) / n - 0.5f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                dot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        }

        dot.Apply(false, true);
        return dot;
    }
}
