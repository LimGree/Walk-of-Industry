using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Собирает анимации и частицы зданий: клипы и контроллеры в Assets/Art/Animations/Buildings,
/// материалы частиц в Assets/Resources/FX, а в префабах — подвижные детали (Anim_X/Move/модель),
/// ParticleSystem в FX, Animator и BuildingFx. Детали и их оси — generated/parts.json (wi_models.py).
/// Запускается сам после компиляции, если BUILD_VERSION устарел, или из меню.
/// Всё созданное — обычные ассеты: клипы можно править в окне Animation, частицы — в инспекторе.
/// </summary>
[InitializeOnLoad]
public static class BuildingFxBuilder
{
    const int Version = 1;
    const string AnimDir = "Assets/Art/Animations/Buildings";
    const string FxDir = "Assets/Resources/FX";
    const string PrefabDir = "Assets/Prefabs/Buildings";
    const string ModelDir = "Assets/Resources/Models";
    const string PartsJson = "Assets/Art/Models/Buildings/generated/parts.json";
    const string Marker = AnimDir + "/BUILD_VERSION.txt";

    static int retries;

    static BuildingFxBuilder()
    {
        EditorApplication.delayCall += AutoBuild;
    }

    static void AutoBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            if (retries++ < 20)
                EditorApplication.delayCall += AutoBuild;
            return;
        }

        if (File.Exists(Marker) && File.ReadAllText(Marker).Trim() == Version.ToString())
            return;
        Build();
    }

    [MenuItem("Walk of Industry/Rebuild building animations + FX")]
    public static void Build()
    {
        try
        {
            Directory.CreateDirectory(AnimDir);
            Directory.CreateDirectory(FxDir);
            AssetDatabase.Refresh();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "/extractor_1_drill.obj") == null)
            {
                Debug.LogWarning("[BuildingFx] модели деталей ещё не импортированы — повтор позже");
                if (retries++ < 20)
                    EditorApplication.delayCall += AutoBuild;
                return;
            }

            BuildMaterials();
            var controllers = BuildControllers();
            List<PartRow> parts = LoadParts();
            int n = 0;
            foreach (Profile p in Profiles())
            {
                if (BuildPrefab(p, parts, controllers))
                    n++;
            }

            File.WriteAllText(Marker, Version.ToString());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[BuildingFx] OK: префабов " + n + ", контроллеров " + controllers.Count);
        }
        catch (Exception e)
        {
            Debug.LogError("[BuildingFx] сборка упала: " + e);
        }
    }

    // ---------- Детали из parts.json ----------

    [Serializable]
    class PartRow
    {
        public string prefab;
        public string parent;
        public string anim;
        public string model;
        public float[] pos;
    }

    [Serializable]
    class PartList
    {
        public PartRow[] parts;
    }

    static List<PartRow> LoadParts()
    {
        var list = new List<PartRow>();
        if (!File.Exists(PartsJson))
            return list;
        PartList data = JsonUtility.FromJson<PartList>(File.ReadAllText(PartsJson));
        if (data != null && data.parts != null)
            list.AddRange(data.parts);
        return list;
    }

    // ---------- Профили зданий ----------

    enum Fx { Smoke, Steam, Sparks, Dust, Fire, Splash, Mist, Burst, BurstBlue, Chunks }

    struct FxAt
    {
        public Fx kind;
        public Vector3 pos;
        public float yaw;
        public FxAt(Fx k, float x, float y, float z, float yaw = 0f)
        {
            kind = k;
            pos = new Vector3(x, y, z);
            this.yaw = yaw;
        }
    }

    class Profile
    {
        public string prefab;
        public string controller;
        public FxAt[] fx = Array.Empty<FxAt>();
        public Vector3? light;
    }

    static IEnumerable<Profile> Profiles()
    {
        yield return new Profile { prefab = "Extractor_01.prefab", controller = "extractor",
            fx = new[] { new FxAt(Fx.Dust, 0, 0.1f, 0), new FxAt(Fx.Chunks, 0, 0.2f, 0) } };
        yield return new Profile { prefab = "Smelter_01.prefab", controller = "smelter", light = new Vector3(0.52f, 0.3f, 0f),
            fx = new[] { new FxAt(Fx.Smoke, -0.25f, 1.3f, -0.22f), new FxAt(Fx.Sparks, 0.45f, 0.28f, 0f, 90f),
                         new FxAt(Fx.Burst, 0.45f, 0.3f, 0f) } };
        yield return new Profile { prefab = "Assembler_01.prefab", controller = "assembler",
            fx = new[] { new FxAt(Fx.Burst, 0, 0.75f, 0) } };
        yield return new Profile { prefab = "Constructor_01.prefab", controller = "constructor",
            fx = new[] { new FxAt(Fx.Smoke, -0.62f, 1.72f, -0.55f), new FxAt(Fx.Dust, 0, 0.1f, 0.95f),
                         new FxAt(Fx.Burst, 0, 1.1f, 0.55f) } };
        yield return new Profile { prefab = "chemical_plant.prefab", controller = "chemical",
            fx = new[] { new FxAt(Fx.Steam, -0.75f, 1.45f, -0.65f), new FxAt(Fx.Steam, -0.75f, 1.45f, 0.55f),
                         new FxAt(Fx.Steam, 0.55f, 2.1f, -0.45f), new FxAt(Fx.Chunks, 0, 1.0f, 0) } };
        yield return new Profile { prefab = "refinery.prefab", controller = "refinery",
            fx = new[] { new FxAt(Fx.Fire, 1.1f, 2.85f, -1.0f), new FxAt(Fx.Smoke, 1.1f, 3.05f, -1.0f),
                         new FxAt(Fx.Steam, 0.95f, 1.95f, 0.2f), new FxAt(Fx.Chunks, 0.65f, 1.1f, 0.45f) } };
        yield return new Profile { prefab = "oil_extractor.prefab", controller = "oil" };
        yield return new Profile { prefab = "water_extractor.prefab", controller = null,
            fx = new[] { new FxAt(Fx.Splash, 0.65f, 0.02f, -0.6f), new FxAt(Fx.Mist, 0.65f, 0.12f, -0.6f) } };
        yield return new Profile { prefab = "PowerGenerator.prefab", controller = "generator",
            fx = new[] { new FxAt(Fx.Smoke, 0.28f, 1.25f, 0.05f) } };
        yield return new Profile { prefab = "ResearchLab.prefab", controller = "lab",
            fx = new[] { new FxAt(Fx.BurstBlue, 0, 1.0f, 0) } };
        yield return new Profile { prefab = "Splitter.prefab", controller = "splitter" };
        yield return new Profile { prefab = "PipeSplitter.prefab", controller = "pipe_splitter" };
        yield return new Profile { prefab = "robotic_arm .prefab", controller = "arm" };
        yield return new Profile { prefab = "DroneLoadStation.prefab", controller = "drone_station" };
        yield return new Profile { prefab = "DroneUnloadStation.prefab", controller = "drone_station" };
    }

    // ---------- Префабы ----------

    static bool BuildPrefab(Profile p, List<PartRow> parts, Dictionary<string, AnimatorController> controllers)
    {
        string path = PrefabDir + "/" + p.prefab;
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
        {
            Debug.LogWarning("[BuildingFx] нет префаба " + path);
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int layer = root.layer;
            foreach (PartRow row in parts)
            {
                if (row.prefab != p.prefab)
                    continue;
                Transform parent = FindDeep(root.transform, row.parent);
                if (parent == null)
                {
                    Debug.LogWarning("[BuildingFx] " + p.prefab + ": нет родителя " + row.parent);
                    continue;
                }

                Transform old = parent.Find(row.anim);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
                var pivot = new GameObject(row.anim) { layer = layer };
                pivot.transform.SetParent(parent, false);
                pivot.transform.localPosition = new Vector3(row.pos[0], row.pos[1], row.pos[2]);
                var move = new GameObject("Move") { layer = layer };
                move.transform.SetParent(pivot.transform, false);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + "/" + row.model + ".obj");
                if (model == null)
                {
                    Debug.LogWarning("[BuildingFx] нет модели " + row.model);
                    continue;
                }

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, move.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one;
                foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = layer;
            }

            Transform oldFx = root.transform.Find(BuildingFx.FxRoot);
            if (oldFx != null)
                Object.DestroyImmediate(oldFx.gameObject);
            var fxRoot = new GameObject(BuildingFx.FxRoot) { layer = layer };
            fxRoot.transform.SetParent(root.transform, false);
            var counts = new Dictionary<Fx, int>();
            foreach (FxAt f in p.fx)
            {
                counts.TryGetValue(f.kind, out int c);
                counts[f.kind] = c + 1;
                string name = "FX_" + f.kind + (c > 0 ? "_" + c : "");
                CreateFx(fxRoot.transform, name, f, layer);
            }

            if (p.light.HasValue)
            {
                var lg = new GameObject("Anim_Fire") { layer = layer };
                lg.transform.SetParent(fxRoot.transform, false);
                lg.transform.localPosition = p.light.Value;
                Light light = lg.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.55f, 0.2f);
                light.range = 2.4f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
            }

            Animator anim = root.GetComponent<Animator>();
            if (p.controller != null && controllers.TryGetValue(p.controller, out AnimatorController ctrl))
            {
                if (anim == null)
                    anim = root.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            }

            if (root.GetComponent<BuildingFx>() == null)
                root.AddComponent<BuildingFx>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }

        return null;
    }

    // ---------- Частицы ----------

    static Material matAlpha, matAdd, matChunk;

    static void BuildMaterials()
    {
        matAlpha = EnsureMaterial("fx_alpha", "Hidden/WalkToBiome/Particle", FxDir + "/fx_puff.png");
        matAdd = EnsureMaterial("fx_add", "Hidden/WalkToBiome/ParticleAdd", FxDir + "/fx_dot.png");
        matChunk = EnsureMaterial("fx_chunk", "Hidden/WalkToBiome/Particle", null);
    }

    static Material EnsureMaterial(string name, string shaderName, string texPath)
    {
        string path = FxDir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find(shaderName);
        if (mat == null)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (shader != null)
            mat.shader = shader;

        if (texPath != null)
            mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void CreateFx(Transform parent, string name, FxAt f, int layer)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = f.pos;
        go.transform.localRotation = Quaternion.Euler(0f, f.yaw, 0f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        var em = ps.emission;
        var shape = ps.shape;
        var col = ps.colorOverLifetime;
        var size = ps.sizeOverLifetime;
        var rend = go.GetComponent<ParticleSystemRenderer>();
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        rend.sharedMaterial = matAlpha;
        em.enabled = false;               // включает BuildingFx, когда здание работает
        shape.enabled = true;

        Gradient Fade(Color c0, Color c1, float peak = 0.15f)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(c0, 0f), new GradientColorKey(c1, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, peak), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        void Grow(float a, float b)
        {
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, a, 1f, b));
        }

        void Upward(float angle, float radius)
        {
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
        }

        void Burst(Color c0, Color c1)
        {
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startColor = new ParticleSystem.MinMaxGradient(c0, c1);
            main.gravityModifier = 0.8f;
            main.maxParticles = 80;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.06f;
            em.rateOverTime = 0f;
            rend.sharedMaterial = matAdd;
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.04f;
            rend.lengthScale = 1.5f;
        }

        switch (f.kind)
        {
            case Fx.Smoke:
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.4f, 3.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.3f, 0.32f, 0.6f), new Color(0.5f, 0.5f, 0.52f, 0.5f));
                main.gravityModifier = -0.04f;
                main.maxParticles = 60;
                em.rateOverTime = 6f;
                Upward(10f, 0.05f);
                Grow(0.6f, 2.2f);
                col.enabled = true;
                col.color = Fade(Color.white, Color.white);
                break;
            case Fx.Steam:
            case Fx.Mist:
                bool mist = f.kind == Fx.Mist;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 1.8f);
                main.startSpeed = mist ? new ParticleSystem.MinMaxCurve(0.1f, 0.3f) : new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = mist
                    ? new ParticleSystem.MinMaxGradient(new Color(0.75f, 0.85f, 0.95f, 0.35f))
                    : new ParticleSystem.MinMaxGradient(new Color(0.92f, 0.92f, 0.94f, 0.45f));
                main.maxParticles = 40;
                em.rateOverTime = mist ? 4f : 8f;
                Upward(mist ? 35f : 12f, 0.08f);
                Grow(0.7f, 2.6f);
                col.enabled = true;
                col.color = Fade(Color.white, Color.white, 0.1f);
                break;
            case Fx.Sparks:
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.55f, 0.15f), new Color(1f, 0.9f, 0.4f));
                main.gravityModifier = 1f;
                main.maxParticles = 40;
                em.rateOverTime = 6f;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 25f;
                shape.radius = 0.05f;
                rend.sharedMaterial = matAdd;
                break;
            case Fx.Dust:
                main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.45f, 0.38f, 0.3f, 0.5f));
                main.gravityModifier = -0.02f;
                main.maxParticles = 40;
                em.rateOverTime = 5f;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.35f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                Grow(0.6f, 1.8f);
                col.enabled = true;
                col.color = Fade(Color.white, Color.white);
                break;
            case Fx.Fire:
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.75f, 0.25f), new Color(1f, 0.4f, 0.1f));
                main.maxParticles = 40;
                em.rateOverTime = 18f;
                Upward(8f, 0.04f);
                Grow(1f, 0.2f);
                col.enabled = true;
                col.color = Fade(Color.white, new Color(1f, 0.3f, 0.1f), 0.1f);
                rend.sharedMaterial = matAdd;
                break;
            case Fx.Splash:
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 1.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.7f, 0.85f, 1f, 0.8f));
                main.gravityModifier = 1.2f;
                main.maxParticles = 50;
                em.rateOverTime = 14f;
                Upward(30f, 0.1f);
                break;
            case Fx.Burst:
                Burst(new Color(1f, 0.6f, 0.2f), new Color(1f, 0.95f, 0.5f));
                break;
            case Fx.BurstBlue:
                Burst(new Color(0.3f, 0.7f, 1f), new Color(0.7f, 0.95f, 1f));
                break;
            case Fx.Chunks:
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.07f);
                main.startRotation3D = true;
                main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = 1.5f;
                main.maxParticles = 60;
                em.rateOverTime = 0f;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.1f;
                shape.rotation = new Vector3(-90f, 0f, 0f);
                rend.renderMode = ParticleSystemRenderMode.Mesh;
                rend.mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
                rend.sharedMaterial = matChunk;
                break;
        }
    }

    // ---------- Клипы и контроллеры ----------

    static Dictionary<string, AnimatorController> BuildControllers()
    {
        var map = new Dictionary<string, AnimatorController>();
        const string A1 = "Assembler_Level_1/Anim_Carriage/Move";
        const string A2 = "Assembler_level_2/Anim_Carriage/Move";
        const string A2b = "Assembler_level_2/Anim_Carriage2/Move";
        const string V = "WiVisual/";

        map["extractor"] = Loop("extractor", 0.6f, c =>
        {
            foreach (string lv in new[] { "extractor_level_1", "extractor_level_2" })
            {
                string p = lv + "/Anim_Drill/Move";
                Euler(c, p, 0.6f, t => new Vector3(0f, 360f * t, 0f), 8, true);
                Pos(c, p, 0.6f, t => new Vector3(0f, 0.03f * Mathf.Sin(t * Mathf.PI * 4f), 0f), 9);
            }
        });
        map["assembler"] = Loop("assembler", 2f, c =>
        {
            Pos(c, A1, 2f, t => new Vector3(0.25f * Mathf.Sin(t * Mathf.PI * 2f), 0f, 0f), 9);
            Pos(c, A2, 2f, t => new Vector3(0.25f * Mathf.Sin(t * Mathf.PI * 2f), 0f, 0f), 9);
            Pos(c, A2b, 2f, t => new Vector3(-0.25f * Mathf.Sin(t * Mathf.PI * 2f), 0f, 0f), 9);
        });
        map["constructor"] = Loop("constructor", 4f, c =>
        {
            Pos(c, V + "Anim_Trolley/Move", 4f, t => new Vector3(0.6f * Mathf.Sin(t * Mathf.PI * 2f), 0f, 0f), 9);
        });
        map["chemical"] = Loop("chemical", 1.2f, c =>
        {
            Scale(c, V + "Anim_Beacon/Move", 1.2f, t => Vector3.one * (1f + 0.5f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f))), 9);
        });
        map["refinery"] = Loop("refinery", 0.8f, c =>
        {
            Scale(c, V + "Anim_Flame/Move", 0.8f, t =>
            {
                float a = t * Mathf.PI * 2f;
                float xz = 1f + 0.12f * Mathf.Sin(a * 3f);
                return new Vector3(xz, 1f + 0.35f * Mathf.Sin(a * 2f) + 0.15f * Mathf.Sin(a * 5f), xz);
            }, 17);
        });
        map["oil"] = Loop("oil", 2.4f, c =>
        {
            Euler(c, V + "Anim_Beam/Move", 2.4f, t => new Vector3(12f * Mathf.Sin(t * Mathf.PI * 2f), 0f, 0f), 13, false);
            Euler(c, V + "Anim_Crank/Move", 2.4f, t => new Vector3(360f * t, 0f, 0f), 8, true);
        });
        map["generator"] = Loop("generator", 0.5f, c =>
        {
            Euler(c, V + "Anim_Turbine/Move", 0.5f, t => new Vector3(360f * t, 0f, 0f), 8, true);
            Scale(c, V + "Anim_Coils/Move", 0.5f, t => Vector3.one * (1f + 0.25f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f))), 9);
        });
        map["lab"] = Loop("lab", 8f, c =>
        {
            Euler(c, V + "Anim_Dish/Move", 8f, t => new Vector3(0f, 360f * t, 0f), 8, true);
            Scale(c, V + "Anim_Beacon/Move", 8f, t => Vector3.one * (1f + 0.4f * (0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 8f))), 33);
        });
        map["splitter"] = Loop("splitter", 0.8f, c =>
        {
            Euler(c, V + "Anim_Hub/Move", 0.8f, t => new Vector3(0f, -360f * t, 0f), 8, true);
        });
        map["pipe_splitter"] = Loop("pipe_splitter", 0.8f, c =>
        {
            Euler(c, V + "Anim_Valve/Move", 0.8f, t => new Vector3(0f, 20f * Mathf.Sin(t * Mathf.PI * 2f), 0f), 9, false);
        });
        map["smelter"] = Loop("smelter", 0.6f, c =>
        {
            var curve = new AnimationCurve(new Keyframe(0f, 1.1f), new Keyframe(0.1f, 1.6f), new Keyframe(0.22f, 0.9f),
                new Keyframe(0.35f, 1.4f), new Keyframe(0.47f, 1.0f), new Keyframe(0.6f, 1.1f));
            AnimationUtility.SetEditorCurve(c, EditorCurveBinding.FloatCurve("FX/Anim_Fire", typeof(Light), "m_Intensity"), curve);
        });
        map["drone_station"] = Loop("drone_station", 0.5f, c =>
        {
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.25f, 0f), new Keyframe(0.5f, 1f));
            for (int i = 0; i < curve.length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Constant);
            }
            AnimationUtility.SetEditorCurve(c, EditorCurveBinding.FloatCurve("Visual/PadLamps", typeof(GameObject), "m_IsActive"), curve);
        });
        map["arm"] = ArmController();
        return map;
    }

    static AnimationClip Clip(string name, float length, bool loop, Action<AnimationClip> fill)
    {
        string path = AnimDir + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.ClearCurves();
        fill(clip);
        AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
        s.loopTime = loop;
        s.stopTime = length;
        AnimationUtility.SetAnimationClipSettings(clip, s);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    static AnimatorController NewController(string name)
    {
        string path = AnimDir + "/" + name + ".controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null)
            AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        ctrl.AddParameter("Working", AnimatorControllerParameterType.Bool);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        ctrl.AddParameter("Transfer", AnimatorControllerParameterType.Trigger);
        return ctrl;
    }

    /// <summary>Idle (стоит) ⇄ Work (петля) по bool Working, скорость Work — параметр Speed.</summary>
    static AnimatorController Loop(string name, float length, Action<AnimationClip> fill)
    {
        AnimationClip clip = Clip(name + "_Work", length, true, fill);
        AnimatorController ctrl = NewController(name);
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState idle = sm.AddState("Idle");
        AnimatorState work = sm.AddState("Work");
        work.motion = clip;
        work.speedParameterActive = true;
        work.speedParameter = "Speed";
        sm.defaultState = idle;
        AnimatorStateTransition on = idle.AddTransition(work);
        on.hasExitTime = false;
        on.duration = 0.15f;
        on.AddCondition(AnimatorConditionMode.If, 0f, "Working");
        AnimatorStateTransition off = work.AddTransition(idle);
        off.hasExitTime = false;
        off.duration = 0.25f;
        off.AddCondition(AnimatorConditionMode.IfNot, 0f, "Working");
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    /// <summary>Рука: Idle → Swing по триггеру Transfer (поворот к точке сброса и назад).</summary>
    static AnimatorController ArmController()
    {
        const float T = 0.45f;
        AnimationClip clip = Clip("arm_Swing", T, false, c =>
        {
            Euler(c, "WiVisual/Anim_Turret/Move", T, t => new Vector3(0f, 180f * Mathf.Sin(t * Mathf.PI), 0f), 9, false);
        });
        AnimatorController ctrl = NewController("arm");
        AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
        AnimatorState idle = sm.AddState("Idle");
        AnimatorState swing = sm.AddState("Swing");
        swing.motion = clip;
        sm.defaultState = idle;
        AnimatorStateTransition go = idle.AddTransition(swing);
        go.hasExitTime = false;
        go.duration = 0f;
        go.AddCondition(AnimatorConditionMode.If, 0f, "Transfer");
        AnimatorStateTransition back = swing.AddTransition(idle);
        back.hasExitTime = true;
        back.exitTime = 1f;
        back.duration = 0f;
        EditorUtility.SetDirty(ctrl);
        return ctrl;
    }

    static void Euler(AnimationClip c, string path, float T, Func<float, Vector3> f, int keys, bool linear)
    {
        Vec(c, path, typeof(Transform), "localEulerAnglesRaw", T, f, keys, linear);
    }

    static void Pos(AnimationClip c, string path, float T, Func<float, Vector3> f, int keys)
    {
        Vec(c, path, typeof(Transform), "m_LocalPosition", T, f, keys, false);
    }

    static void Scale(AnimationClip c, string path, float T, Func<float, Vector3> f, int keys)
    {
        Vec(c, path, typeof(Transform), "m_LocalScale", T, f, keys, false);
    }

    /// <summary>Все три компоненты: t — доля от 0 до 1, keys ключей по времени.</summary>
    static void Vec(AnimationClip c, string path, Type type, string prop, float T, Func<float, Vector3> f, int keys, bool linear)
    {
        var cx = new AnimationCurve();
        var cy = new AnimationCurve();
        var cz = new AnimationCurve();
        for (int i = 0; i < keys; i++)
        {
            float t = i / (float)(keys - 1);
            Vector3 v = f(t);
            cx.AddKey(new Keyframe(t * T, v.x));
            cy.AddKey(new Keyframe(t * T, v.y));
            cz.AddKey(new Keyframe(t * T, v.z));
        }

        foreach (AnimationCurve curve in new[] { cx, cy, cz })
        {
            for (int i = 0; i < curve.length; i++)
            {
                var mode = linear ? AnimationUtility.TangentMode.Linear : AnimationUtility.TangentMode.ClampedAuto;
                AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
                AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
            }
        }

        AnimationUtility.SetEditorCurve(c, EditorCurveBinding.FloatCurve(path, type, prop + ".x"), cx);
        AnimationUtility.SetEditorCurve(c, EditorCurveBinding.FloatCurve(path, type, prop + ".y"), cy);
        AnimationUtility.SetEditorCurve(c, EditorCurveBinding.FloatCurve(path, type, prop + ".z"), cz);
    }
}
