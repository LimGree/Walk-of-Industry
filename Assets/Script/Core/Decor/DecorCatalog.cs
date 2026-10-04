using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Каталог декораций ([[Decorations]]): цены, категории, названия. Модели и части — из
/// `Resources/Decor/decor_manifest.json` (генератор `generated/source/wi_decor.py`).
/// На старте для каждой декорации создаются <see cref="BuildingData"/> (id <c>decor_*</c>) и скрытый
/// шаблон-префаб с <see cref="Decoration"/>; данные регистрируются в [[GameDatabase]], поэтому стройка,
/// снос, сохранение, undo, копирование и чертежи работают как у обычных зданий.
/// </summary>
public static class DecorCatalog
{
    public const string Prefix = "decor_";
    /// <summary>Табличка со своей надписью ([[SignEditorUI]]): модель собирается кодом из <see cref="SignData"/>.</summary>
    public const string SignId = "decor_custom_sign";

    public enum Cat { Light, Nature, Industry, Road, Rest, Monument, Season }

    /// <summary>Польза рядом стоящей декорации: фонарь — реже ночные поломки, отдых — легче ремонт.</summary>
    public enum Perk { None, Lamp, Rest }

    public static readonly Cat[] Categories =
        { Cat.Light, Cat.Nature, Cat.Industry, Cat.Road, Cat.Rest, Cat.Monument, Cat.Season };

    public sealed class Def
    {
        public string id;
        public Cat cat;
        public Vector2Int size;
        public int rubies;
        public int coins;
        public float beauty;
        public string ru, en, ruDesc, enDesc;
        /// <summary>id достижения: декорация не продаётся, выдаётся наградой.</summary>
        public string trophy;
        public bool seasonal;
        public Perk perk;
        public BuildingData data;
        public ManifestItem model;

        public string Title => UiLocale.IsRu ? ru : en;
        public string Desc => UiLocale.IsRu ? ruDesc : enDesc;
        public bool IsTrophy => !string.IsNullOrEmpty(trophy);
        public bool IsFloor => model != null && model.floor;
        public bool IsLine => model != null && model.line;
        public bool Sellable => !IsTrophy;

        public Def Trophy(string achievement) { trophy = achievement; return this; }
        public Def Seasonal() { seasonal = true; return this; }
        public Def With(Perk p) { perk = p; return this; }
    }

    // ---------- Манифест моделей (JSON из wi_decor.py) ----------

    [Serializable]
    public class ManifestFile
    {
        public List<ManifestItem> items = new List<ManifestItem>();
    }

    [Serializable]
    public class ManifestItem
    {
        public string id;
        public string model;
        public float height;
        public float width;
        public float depth;
        public List<ManifestPart> parts = new List<ManifestPart>();
        public List<ManifestLight> lights = new List<ManifestLight>();
        public bool line;
        public bool floor;
        public float blink;
        public string tintPart;
        public float[] fire;
        public float[] smoke;
        public float[] splash;
        public ManifestText text;
    }

    [Serializable]
    public class ManifestPart
    {
        public string name;
        public string model;
        public float[] pos;
    }

    [Serializable]
    public class ManifestLight
    {
        public float[] pos;
        public float[] color;
        public float range;
    }

    [Serializable]
    public class ManifestText
    {
        public float[] pos;
        public float size;
        public float[] color;
        public string kind;
    }

    public const string PartPrefix = "Part_";
    public const string LightPrefix = "DecorLight";
    public const string TextName = "DecorText";
    public const string TextBackName = "DecorTextBack";

    static readonly List<Def> all = new List<Def>();
    static readonly Dictionary<string, Def> byId = new Dictionary<string, Def>(StringComparer.OrdinalIgnoreCase);
    static GameObject holder;
    static bool built;

    public static IReadOnlyList<Def> All
    {
        get
        {
            Ensure();
            return all;
        }
    }

    public static bool IsDecorId(string id)
    {
        return !string.IsNullOrEmpty(id) && id.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDecor(BuildingData data)
    {
        return data != null && IsDecorId(data.id);
    }

    public static Def Find(string id)
    {
        if (!IsDecorId(id))
            return null;
        Ensure();
        byId.TryGetValue(id.Trim(), out Def def);
        return def;
    }

    public static Def Find(BuildingData data)
    {
        return data != null ? Find(data.id) : null;
    }

    public static bool TryName(string id, bool desc, out string text)
    {
        text = null;
        if (!IsDecorId(id))
            return false;
        Def def = FindDefOnly(id);
        if (def == null)
            return false;
        text = desc ? def.Desc : def.Title;
        return !string.IsNullOrEmpty(text);
    }

    /// <summary>Только таблица (без моделей) — для названий из DataLocale до старта сцены.</summary>
    static Def FindDefOnly(string id)
    {
        if (all.Count == 0)
            FillTable();
        byId.TryGetValue(id.Trim(), out Def def);
        return def;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        Ensure();
    }

    public static void Ensure()
    {
        // holder пропал (выход из Play без перезагрузки домена) — шаблоны собираются заново.
        if (built && holder != null)
            return;
        if (all.Count == 0)
            FillTable();
        if (!Application.isPlaying)
            return;
        built = true;

        Dictionary<string, ManifestItem> models = LoadManifest();
        holder = new GameObject("DecorTemplates");
        holder.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(holder);

        for (int i = 0; i < all.Count; i++)
        {
            Def def = all[i];
            models.TryGetValue(def.id, out def.model);
            def.data = MakeData(def);
            def.data.prefab = MakeTemplate(def);
            if (GameDatabase.Instance != null)
                GameDatabase.Instance.RegisterExtraBuilding(def.data);
            else
                GameDatabase.PendingExtra.Add(def.data);
        }
    }

    static Dictionary<string, ManifestItem> LoadManifest()
    {
        var map = new Dictionary<string, ManifestItem>(StringComparer.OrdinalIgnoreCase);
        TextAsset json = Resources.Load<TextAsset>("Decor/decor_manifest");
        if (json == null)
        {
            Debug.LogWarning("[Decor] Нет Resources/Decor/decor_manifest.json — декорации без моделей");
            return map;
        }

        ManifestFile file = JsonUtility.FromJson<ManifestFile>(json.text);
        if (file == null || file.items == null)
            return map;
        for (int i = 0; i < file.items.Count; i++)
        {
            ManifestItem item = file.items[i];
            if (item != null && !string.IsNullOrEmpty(item.id))
                map[item.id] = item;
        }

        return map;
    }

    static BuildingData MakeData(Def def)
    {
        var data = ScriptableObject.CreateInstance<BuildingData>();
        data.hideFlags = HideFlags.DontUnloadUnusedAsset;
        data.name = def.id;
        data.id = def.id;
        data.displayName = def.en;
        data.description = def.enDesc;
        data.size = def.size;
        data.canRotate = true;
        data.buildCost = def.coins;
        data.icon = Resources.Load<Sprite>("Decor/Icons/" + def.id);
        return data;
    }

    static GameObject MakeTemplate(Def def)
    {
        var go = new GameObject(def.id);
        go.transform.SetParent(holder.transform, false);
        int layer = LayerMask.NameToLayer("buildings");
        if (layer < 0)
            layer = 0;

        var deco = go.AddComponent<Decoration>();
        deco.data = def.data;
        deco.drawFootprintGizmo = false;
        deco.maxOutputBuffer = 0;
        deco.inputSockets = new BuildingSocket[0];
        deco.outputSockets = new BuildingSocket[0];

        ManifestItem m = def.model;
        float height = m != null ? Mathf.Max(0.05f, m.height) : 1f;
        if (m != null && m.floor)
            height = 0.06f;
        if (def.id == Zipline.PostId)
            height = Zipline.PostHeight;
        if (def.id == SignId)
            height = 2.2f;
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(def.size.x * 0.94f, height, def.size.y * 0.94f);
        box.center = new Vector3(0f, height * 0.5f, 0f);

        var visual = new GameObject(BuildingRestyle.VisualName);
        visual.transform.SetParent(go.transform, false);

        if (m != null)
        {
            GameObject body = ModelLibrary.Spawn(m.model, 0f);
            if (body != null)
            {
                body.name = "WiModel";
                body.transform.SetParent(visual.transform, false);
                body.transform.localPosition = Vector3.zero;
                body.transform.localRotation = Quaternion.identity;
            }
            else
                Debug.LogWarning("[Decor] Нет модели " + m.model);

            if (m.parts != null)
            {
                for (int i = 0; i < m.parts.Count; i++)
                {
                    ManifestPart part = m.parts[i];
                    GameObject p = ModelLibrary.Spawn(part.model, 0f);
                    if (p == null)
                        continue;
                    p.name = PartPrefix + part.name;
                    p.transform.SetParent(visual.transform, false);
                    p.transform.localPosition = Vec(part.pos);
                    p.transform.localRotation = Quaternion.identity;
                }
            }

            if (m.lights != null)
            {
                for (int i = 0; i < m.lights.Count; i++)
                {
                    ManifestLight l = m.lights[i];
                    var lgo = new GameObject(LightPrefix + i);
                    lgo.transform.SetParent(go.transform, false);
                    lgo.transform.localPosition = Vec(l.pos);
                    Light light = lgo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = Mathf.Max(1f, l.range);
                    light.color = Col(l.color);
                    light.intensity = 1.6f;
                    light.shadows = LightShadows.None;
                    light.renderMode = LightRenderMode.Auto;
                    light.enabled = false;
                }
            }

            if (m.text != null && !string.IsNullOrEmpty(m.text.kind))
            {
                // Надпись с двух сторон: передняя смотрит в +Z, задняя — зеркально за щитом.
                // Обе односторонние и с тестом глубины, поэтому с каждой стороны читается своя.
                Vector3 front = Vec(m.text.pos);
                Vector3 back = new Vector3(front.x, front.y, -front.z - BackTextGap);
                MakeText(visual.transform, TextName, front, 180f, m.text);
                MakeText(visual.transform, TextBackName, back, 0f, m.text);
            }
        }

        if (def.id == Zipline.PostId && m == null)
            Zipline.BuildPostModel(visual.transform);
        if (def.id == SignId)
            BuildSignTemplate(go.transform, visual.transform, layer);

        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
        return go;
    }

    /// <summary>Шаблон таблички: модель по умолчанию (её видно в призраке стройки) и выключенная ночная подсветка.</summary>
    static void BuildSignTemplate(Transform root, Transform visual, int layer)
    {
        SignView view = SignView.Build(visual, SignPresets.Default(), layer, false);
        var lgo = new GameObject(LightPrefix + "0");
        lgo.transform.SetParent(root, false);
        lgo.transform.position = view.LightPoint;
        Light light = lgo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 4.5f;
        light.color = new Color(1f, 0.86f, 0.62f);
        light.intensity = 1.6f;
        light.shadows = LightShadows.None;
        light.enabled = false;
    }

    /// <summary>Задняя надпись чуть дальше от центра: у вывесок сзади рамка толще лицевой панели.</summary>
    const float BackTextGap = 0.025f;

    static void MakeText(Transform parent, string name, Vector3 pos, float yaw, ManifestText spec)
    {
        var tgo = new GameObject(name);
        tgo.transform.SetParent(parent, false);
        tgo.transform.localPosition = pos;
        // TextMesh читается со стороны −Z локали: yaw 180 — лицом вперёд (+Z), 0 — назад.
        tgo.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        TextMesh tm = tgo.AddComponent<TextMesh>();
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontSize = 48;
        tm.characterSize = Mathf.Max(0.005f, spec.size);
        tm.color = Col(spec.color);
        tm.richText = false;
        Font f = DecorFont();
        if (f != null)
        {
            tm.font = f;
            MeshRenderer rend = tgo.GetComponent<MeshRenderer>();
            rend.sharedMaterial = TextMaterial(f);
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rend.receiveShadows = false;
        }
    }

    static Material textMaterial;

    /// <summary>Материал шрифта с отсечением задних граней и тестом глубины (см. WalkToBiomeText).</summary>
    public static Material TextMaterial(Font f)
    {
        if (textMaterial != null)
            return textMaterial;
        Shader shader = Resources.Load<Shader>("WalkToBiomeText");
        if (shader == null)
            shader = Shader.Find("Hidden/WalkToBiome/Text");
        if (shader == null)
            return f.material;
        textMaterial = new Material(shader) { name = "DecorText", mainTexture = f.material.mainTexture };
        // Динамический шрифт пересобирает атлас при новых буквах — подхватываем новую текстуру.
        Font.textureRebuilt += rebuilt =>
        {
            if (textMaterial != null && rebuilt == f)
                textMaterial.mainTexture = rebuilt.material.mainTexture;
        };
        return textMaterial;
    }

    static Font font;

    public static Font DecorFont()
    {
        if (font != null)
            return font;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return font;
    }

    public static Vector3 Vec(float[] a)
    {
        if (a == null || a.Length < 3)
            return Vector3.zero;
        return new Vector3(a[0], a[1], a[2]);
    }

    static Color Col(float[] a)
    {
        if (a == null || a.Length < 3)
            return Color.white;
        return new Color(a[0], a[1], a[2], 1f);
    }

    // ---------- Таблица ----------

    const int RubyPriceScale = 40; // ×2 к прежним 20 (плейтест: декор дешёвый)

    static Def D(string id, Cat cat, int sx, int sz, int rubies, int coins, float beauty,
        string ru, string en, string ruDesc, string enDesc)
    {
        var def = new Def
        {
            id = id,
            cat = cat,
            size = new Vector2Int(sx, sz),
            // Ребаланс: рубиновая цена ×20; монетная установка ×2 идёт через Economy.BuildCost.
            rubies = rubies * RubyPriceScale,
            coins = coins,
            beauty = beauty,
            ru = ru,
            en = en,
            ruDesc = ruDesc,
            enDesc = enDesc
        };
        all.Add(def);
        byId[id] = def;
        return def;
    }

    static void FillTable()
    {
        all.Clear();
        byId.Clear();

        // Освещение: ночью светят, станки рядом реже ломаются.
        D("decor_street_lamp", Cat.Light, 1, 1, 4, 40, 3, "Уличный фонарь", "Street Lamp",
            "Тёплый свет ночью. Станки рядом ломаются реже. E — перекрасить.", "Warm light at night. Nearby machines break less. E — recolor.").With(Perk.Lamp);
        D("decor_floodlight", Cat.Light, 1, 1, 6, 60, 3, "Прожектор на мачте", "Floodlight Mast",
            "Яркий белый свет на большой радиус. Защищает станки от ночных поломок.", "Bright white light over a wide radius. Guards machines at night.").With(Perk.Lamp);
        D("decor_beacon", Cat.Light, 1, 1, 3, 30, 2, "Сигнальная мигалка", "Warning Beacon",
            "Крутится и мигает оранжевым.", "Spins and flashes orange.");
        D("decor_fire_barrel", Cat.Light, 1, 1, 4, 30, 3, "Бочка-костёр", "Fire Barrel",
            "Живой огонь с искрами. Светит и греет ночную смену.", "Live fire with sparks. Lights up the night shift.").With(Perk.Lamp);
        D("decor_garland", Cat.Light, 2, 1, 6, 60, 5, "Гирлянда на столбиках", "String Lights",
            "Разноцветные лампочки мигают по очереди.", "Colored bulbs blink in turn.").With(Perk.Lamp);
        D("decor_neon_sign", Cat.Light, 2, 1, 10, 120, 8, "Неоновая вывеска", "Neon Sign",
            "Название твоего завода неоном. E — перекрасить рамку.", "Your factory name in neon. E — recolor the frame.");

        // Природа
        D("decor_bush", Cat.Nature, 1, 1, 2, 15, 2, "Куст", "Bush",
            "Зелень между лентами.", "Greenery between belts.");
        D("decor_potted_tree", Cat.Nature, 1, 1, 3, 25, 3, "Дерево в кадке", "Potted Tree",
            "Маленькое дерево в горшке. E — перекрасить кадку.", "A small tree in a pot. E — recolor the pot.");
        D("decor_flowerbed", Cat.Nature, 2, 1, 4, 40, 5, "Клумба", "Flowerbed",
            "Цветы в каменном бордюре.", "Flowers in a stone border.");
        D("decor_big_tree", Cat.Nature, 2, 2, 8, 80, 9, "Большое дерево", "Big Tree",
            "Раскидистое дерево, крона качается на ветру.", "A wide tree, the crown sways in the wind.");
        D("decor_rock_garden", Cat.Nature, 2, 2, 7, 70, 8, "Сад камней", "Rock Garden",
            "Камни, мох и граблёный гравий.", "Stones, moss and raked gravel.");
        D("decor_pond", Cat.Nature, 2, 2, 9, 90, 10, "Пруд с камышом", "Reed Pond",
            "Тихая вода, кувшинки, камыш.", "Still water, lily pads and reeds.");
        D("decor_fountain", Cat.Nature, 3, 3, 20, 250, 22, "Фонтан", "Fountain",
            "Центр площади: бьющая вода и брызги.", "A plaza centerpiece with splashing water.");

        // Промзона
        D(Zipline.PostId, Cat.Industry, 1, 1, 1, 50, 1, "Опора троса", "Zipline Post",
            "Тросовая дорога: две опоры до 40 клеток соединяются тросом. E у опоры — едешь, Пробел — отцепиться.",
            "Zipline: two posts up to 40 cells apart get a cable. E at a post to ride, Space to let go.");
        D("decor_barrels", Cat.Industry, 1, 1, 2, 20, 1, "Штабель бочек", "Barrel Stack",
            "Бочки на поддоне. E — перекрасить.", "Barrels on a pallet. E — recolor.");
        D("decor_pallets", Cat.Industry, 1, 1, 2, 15, 1, "Стопка поддонов", "Pallet Stack",
            "Поддоны и ящик сверху.", "Pallets with a crate on top.");
        D("decor_cable_reel", Cat.Industry, 1, 1, 2, 20, 1, "Катушка кабеля", "Cable Reel",
            "Деревянная катушка медного кабеля.", "A wooden reel of copper cable.");
        D("decor_toolbox", Cat.Industry, 1, 1, 3, 25, 2, "Ящик с инструментами", "Tool Chest",
            "Красный шкаф механика на колёсах.", "A mechanic's red chest on wheels.");
        D("decor_pipe_pile", Cat.Industry, 2, 1, 4, 40, 3, "Куча труб", "Pipe Pile",
            "Трубы ждут монтажа.", "Pipes waiting to be fitted.");
        D("decor_scaffold", Cat.Industry, 2, 1, 5, 50, 3, "Строительные леса", "Scaffolding",
            "Леса с настилами и лестницей.", "Scaffolding with planks and a ladder.");
        D("decor_tower_crane", Cat.Industry, 3, 3, 25, 300, 18, "Башенный кран", "Tower Crane",
            "Стрела медленно поворачивается, на макушке мигает огонь.", "The jib slowly turns, a red light blinks on top.");

        // Дороги и ограждения (забор, сетка и асфальт тянутся линией)
        D("decor_fence", Cat.Road, 1, 1, 1, 10, 1, "Секция забора", "Fence Section",
            "Ставь линией ПКМ. E — перекрасить.", "Drag a line with RMB. E — recolor.");
        D("decor_wire_fence", Cat.Road, 1, 1, 1, 10, 1, "Сетка с колючкой", "Barbed Wire Fence",
            "Строгая граница участка. Ставится линией.", "A strict yard border. Place in lines.");
        D("decor_cone", Cat.Road, 1, 1, 1, 5, 1, "Конусы и барьер", "Cones & Barrier",
            "Дорожные конусы и полосатый барьер.", "Traffic cones and a striped barrier.");
        D("decor_sign_arrow", Cat.Road, 1, 1, 2, 15, 1, "Указатель-стрелка", "Arrow Sign",
            "Показывает дорогу. R — повернуть.", "Points the way. R — rotate.");
        D(SignId, Cat.Road, 2, 1, 5, 40, 4, "Табличка", "Custom Sign",
            "Своя надпись: текст, символы, иконки и плашки. E — редактор: цвет, размер, поворот, положение, стойка и подсветка.",
            "Your own sign: text, symbols, icons and plates. E — editor: color, size, rotation, position, stand and night light.");
        D("decor_paving", Cat.Road, 1, 1, 3, 3, 0.25f, "Тротуарная плитка", "Paving",
            "Напольная: здания и ленты ставятся поверх.", "Floor tile: buildings and belts go on top.");
        D("decor_asphalt", Cat.Road, 1, 1, 3, 3, 0.25f, "Асфальт с разметкой", "Road Asphalt",
            "Напольный, разметка идёт вдоль линии. Здания ставятся поверх.", "Floor tile, markings follow the line. Buildings go on top.");
        D("decor_barrier_gate", Cat.Road, 2, 1, 6, 60, 3, "Шлагбаум", "Barrier Gate",
            "Поднимается, когда подходишь.", "Lifts when you walk up.");
        D("decor_factory_gate", Cat.Road, 3, 1, 12, 150, 10, "Ворота завода", "Factory Gate",
            "Кирпичные столбы и табличка с названием завода.", "Brick pillars and a sign with your factory name.");

        // Зона отдыха: рядом со сломанным станком ремонт проще.
        D("decor_vending", Cat.Rest, 1, 1, 5, 50, 3, "Торговый автомат", "Vending Machine",
            "Светится. Рядом ремонт идёт легче.", "Glows. Repairs nearby are easier.").With(Perk.Rest);
        D("decor_cooler", Cat.Rest, 1, 1, 3, 25, 2, "Кулер с водой", "Water Cooler",
            "Глоток воды перед ремонтом.", "A sip of water before a repair.").With(Perk.Rest);
        D("decor_bbq", Cat.Rest, 1, 1, 4, 35, 3, "Мангал", "BBQ Grill",
            "Тлеют угли, идёт дымок.", "Glowing coals and a wisp of smoke.").With(Perk.Rest);
        D("decor_bench", Cat.Rest, 2, 1, 4, 40, 4, "Скамейка", "Bench",
            "Отдых для механиков. E — перекрасить.", "A rest for mechanics. E — recolor.").With(Perk.Rest);
        D("decor_picnic", Cat.Rest, 2, 2, 6, 70, 6, "Стол для пикника", "Picnic Table",
            "Стол под зонтом. E — перекрасить зонт.", "A table under a parasol. E — recolor the parasol.").With(Perk.Rest);
        D("decor_gazebo", Cat.Rest, 3, 3, 15, 200, 16, "Беседка", "Gazebo",
            "Беседка с лавками и фонарём. E — перекрасить крышу.", "A gazebo with benches and a lamp. E — recolor the roof.").With(Perk.Rest);

        // Памятники и «живые» декорации
        D("decor_flagpole", Cat.Monument, 1, 1, 5, 50, 4, "Флагшток", "Flagpole",
            "Флаг развевается. E — сменить цвет флага.", "The flag waves. E — change its color.");
        D("decor_clock", Cat.Monument, 1, 1, 6, 60, 4, "Уличные часы", "Street Clock",
            "Показывают игровое время.", "Shows the in-game time.");
        D("decor_scoreboard", Cat.Monument, 2, 1, 12, 120, 6, "Табло рекордов", "Record Board",
            "Живая статистика завода: произведено, монеты, исследования.", "Live factory stats: produced, coins, research.");
        D("decor_gear_monument", Cat.Monument, 2, 2, 0, 150, 15, "Монумент-шестерня", "Gear Monument",
            "Награда за 1000 шестерёнок. Золотая шестерня вращается.", "Reward for 1000 gears. The golden gear turns.").Trophy("gears_1000");
        D("decor_rocket", Cat.Monument, 3, 3, 0, 300, 25, "Ракета-памятник", "Rocket Monument",
            "Награда за все исследования.", "Reward for finishing all research.").Trophy(AchievementSystem.ResearchAllId);
        D("decor_robot", Cat.Monument, 1, 1, 8, 60, 5, "Робот-талисман", "Robot Mascot",
            "Машет рукой проходящим. E — перекрасить.", "Waves at passers-by. E — recolor.");
        D("decor_windmill", Cat.Monument, 2, 2, 10, 120, 8, "Ветряк", "Wind Turbine",
            "Лопасти крутятся без остановки.", "The blades never stop turning.");
        D("decor_radio_tower", Cat.Monument, 2, 2, 12, 140, 7, "Радиовышка", "Radio Tower",
            "Красно-белая вышка, на макушке мигает огонь.", "A red tower with a blinking top light.");
        D("decor_hologram", Cat.Monument, 2, 2, 15, 160, 10, "Голограмма-проектор", "Hologram Projector",
            "Парящая светящаяся эмблема.", "A floating glowing emblem.");
        D("decor_golden_cup", Cat.Monument, 1, 1, 0, 50, 12, "Золотой кубок", "Golden Cup",
            "Награда за все остальные достижения.", "Reward for every other achievement.").Trophy(AchievementSystem.AllAchievementsId);

        // Сезонные: в магазине с декабря по февраль, купленные остаются навсегда.
        D("decor_snowman", Cat.Season, 1, 1, 4, 20, 4, "Снеговик", "Snowman",
            "Зимняя декорация: в магазине с декабря по февраль.", "Winter decoration: in the shop December to February.").Seasonal();
        D("decor_xmas_tree", Cat.Season, 2, 2, 10, 100, 12, "Ёлка", "Christmas Tree",
            "Огоньки мигают. В магазине с декабря по февраль.", "Blinking lights. In the shop December to February.").Seasonal().With(Perk.Lamp);
    }

    public static string CategoryTitle(Cat cat)
    {
        switch (cat)
        {
            case Cat.Light: return DecorText.T("decor.cat.light");
            case Cat.Nature: return DecorText.T("decor.cat.nature");
            case Cat.Industry: return DecorText.T("decor.cat.industry");
            case Cat.Road: return DecorText.T("decor.cat.road");
            case Cat.Rest: return DecorText.T("decor.cat.rest");
            case Cat.Monument: return DecorText.T("decor.cat.monument");
            default: return DecorText.T("decor.cat.season");
        }
    }

    public static string SizeLabel(Def def)
    {
        return def == null ? "" : def.size.x + "×" + def.size.y;
    }
}
