using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Тело персонажа: иерархия суставов (таз → корпус → голова, плечи → локти, бёдра → колени)
/// и мягкие детали на них по <see cref="AvatarLook"/>. Корень — между стоп, +Z — вперёд, рост ≈ 1.78 м.
/// Анимирует суставы <see cref="AvatarAnimator"/>.
/// </summary>
public class AvatarRig : MonoBehaviour
{
    public Transform hips, spine, head;
    public Transform shoulderL, shoulderR, elbowL, elbowR;
    public Transform thighL, thighR, kneeL, kneeR;
    public float hipHeight;

    readonly List<Renderer> renderers = new List<Renderer>(48);
    public IReadOnlyList<Renderer> Renderers => renderers;

    static readonly Color32 Shoe = new Color32(48, 44, 42, 255);
    static readonly Color32 Sclera = new Color32(245, 245, 240, 255);
    static readonly Color32 Pupil = new Color32(20, 18, 18, 255);
    static readonly Color32 Mouth = new Color32(120, 60, 58, 255);
    static readonly Color32 Undershirt = new Color32(205, 205, 198, 255);
    static readonly Color32 Reflective = new Color32(225, 228, 220, 255);
    static readonly Color32 HardHat = new Color32(240, 190, 40, 255);

    int layer;
    ShadowCastingMode shadowMode = ShadowCastingMode.On;

    /// <summary>Собрать тело под parent. layer — слой всех деталей (превью рисуется своей камерой).</summary>
    public static AvatarRig Build(Transform parent, AvatarLook look, int layer)
    {
        var go = new GameObject("Avatar");
        go.layer = layer;
        go.transform.SetParent(parent, false);
        AvatarRig rig = go.AddComponent<AvatarRig>();
        rig.layer = layer;
        // Купленные костюм и шляпа ([[PerkSystem]]) — только на персонаже в мире, не в экране внешности
        // и не в сохранённом облике: снял костюм — вернулась своя одежда.
        rig.perkLook = parent != null && parent.GetComponent<PlayerAvatar>() != null;
        rig.Assemble(look != null ? look.Clone() : new AvatarLook());
        return rig;
    }

    public void SetShadowMode(ShadowCastingMode mode)
    {
        shadowMode = mode;
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
                renderers[i].shadowCastingMode = mode;
        }
    }

    public void SetReceiveShadows(bool on)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] != null)
                renderers[i].receiveShadows = on;
        }
    }

    Transform Joint(string name, Transform parent, Vector3 localPos)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        return go.transform;
    }

    void Part(string name, Transform parent, Vector3 center, Vector3 size, float radius, Color32 color,
        float gloss = 0.15f, float metal = 0f, float taper = 1f, Vector3 euler = default)
    {
        var go = new GameObject(name) { layer = layer };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.AddComponent<MeshFilter>().sharedMesh = AvatarMeshes.RoundedBox(size, radius, color, gloss, metal, taper);
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = AvatarMeshes.Material;
        mr.shadowCastingMode = shadowMode;
        renderers.Add(mr);
    }

    static Color32 Shade(Color32 c, float k)
    {
        return new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), 255);
    }

    bool perkLook;
    string perkHat;
    string perkOutfit;

    void Assemble(AvatarLook look)
    {
        look.Clamp();
        bool sturdy = look.body == 1;
        Color32 skin = AvatarLook.SkinColors[look.skin];
        Color32 hairC = AvatarLook.HairColors[look.hairColor];
        Color32 eyeC = AvatarLook.EyeColors[look.eyes];
        Color32 topC = AvatarLook.ClothColors[look.topColor];
        Color32 botC = AvatarLook.ClothColors[look.bottomColor];
        if (perkLook)
            ApplyPerkLook(look, ref topC, ref botC);

        float torsoW = sturdy ? 0.45f : 0.38f;
        float torsoD = sturdy ? 0.26f : 0.22f;
        float armW = sturdy ? 0.135f : 0.115f;
        float legW = sturdy ? 0.17f : 0.15f;
        hipHeight = 0.86f;

        // ----- таз и ноги -----
        hips = Joint("Hips", transform, new Vector3(0f, hipHeight, 0f));
        Part("Pelvis", hips, Vector3.zero, new Vector3(torsoW - 0.04f, 0.17f, torsoD - 0.01f), 0.06f, botC);

        bool shorts = look.bottom == 1;
        for (int s = -1; s <= 1; s += 2)
        {
            Transform thigh = Joint(s < 0 ? "ThighL" : "ThighR", hips, new Vector3(s * (torsoW * 0.25f), -0.03f, 0f));
            Transform knee = Joint(s < 0 ? "KneeL" : "KneeR", thigh, new Vector3(0f, -0.40f, 0f));
            Part("Thigh", thigh, new Vector3(0f, -0.2f, 0f), new Vector3(legW, 0.42f, legW + 0.02f), 0.06f, botC);
            Part("Shin", knee, new Vector3(0f, -0.19f, 0f), new Vector3(legW - 0.015f, 0.38f, legW), 0.055f, shorts ? skin : botC);
            if (shorts)
                Part("Cuff", thigh, new Vector3(0f, -0.38f, 0f), new Vector3(legW + 0.012f, 0.06f, legW + 0.032f), 0.03f, Shade(botC, 0.85f));
            Part("Shoe", knee, new Vector3(0f, -0.395f, 0.035f), new Vector3(legW + 0.005f, 0.095f, 0.26f), 0.04f, Shoe, 0.3f);
            if (s < 0) { thighL = thigh; kneeL = knee; } else { thighR = thigh; kneeR = knee; }
        }

        // ----- корпус и одежда -----
        spine = Joint("Spine", hips, new Vector3(0f, 0.06f, 0f));
        Color32 shirt = look.top == 3 ? Undershirt : topC;
        Part("Torso", spine, new Vector3(0f, 0.25f, 0f), new Vector3(torsoW, 0.5f, torsoD), 0.08f, shirt, 0.12f, 0f, 0.86f);

        switch (look.top)
        {
            case 2: // худи: капюшон и карман
                Part("Hood", spine, new Vector3(0f, 0.48f, -torsoD * 0.42f), new Vector3(torsoW * 0.72f, 0.15f, 0.13f), 0.06f, Shade(topC, 0.9f));
                Part("Pocket", spine, new Vector3(0f, 0.1f, torsoD * 0.5f), new Vector3(torsoW * 0.58f, 0.13f, 0.03f), 0.015f, Shade(topC, 0.82f));
                Part("CordL", spine, new Vector3(-0.04f, 0.36f, torsoD * 0.5f + 0.01f), new Vector3(0.012f, 0.1f, 0.012f), 0.006f, Reflective);
                Part("CordR", spine, new Vector3(0.04f, 0.36f, torsoD * 0.5f + 0.01f), new Vector3(0.012f, 0.1f, 0.012f), 0.006f, Reflective);
                break;
            case 3: // рабочий жилет поверх серой футболки
                Part("Vest", spine, new Vector3(0f, 0.22f, 0f), new Vector3(torsoW + 0.03f, 0.42f, torsoD + 0.03f), 0.08f, topC, 0.2f, 0f, 0.88f);
                Part("VestGap", spine, new Vector3(0f, 0.3f, torsoD * 0.5f + 0.012f), new Vector3(0.07f, 0.3f, 0.012f), 0.005f, Undershirt);
                Part("Stripe1", spine, new Vector3(0f, 0.11f, 0f), new Vector3(torsoW + 0.045f, 0.035f, torsoD + 0.045f), 0.015f, Reflective, 0.6f, 0.2f, 0.9f);
                Part("Stripe2", spine, new Vector3(0f, 0.28f, 0f), new Vector3(torsoW + 0.04f, 0.035f, torsoD + 0.04f), 0.015f, Reflective, 0.6f, 0.2f, 0.92f);
                break;
        }

        if (look.bottom == 2) // комбинезон: нагрудник и лямки
        {
            Part("Bib", spine, new Vector3(0f, 0.16f, torsoD * 0.5f + 0.01f), new Vector3(torsoW * 0.7f, 0.28f, 0.03f), 0.015f, botC);
            Part("BibPocket", spine, new Vector3(0f, 0.2f, torsoD * 0.5f + 0.027f), new Vector3(torsoW * 0.3f, 0.09f, 0.012f), 0.005f, Shade(botC, 0.82f));
            for (int s = -1; s <= 1; s += 2)
                Part("Strap", spine, new Vector3(s * torsoW * 0.27f, 0.36f, 0f), new Vector3(0.045f, 0.3f, torsoD + 0.025f), 0.012f, botC, 0.15f, 0f, 1f, new Vector3(0f, 0f, s * 4f));
        }

        Part("Neck", spine, new Vector3(0f, 0.53f, 0f), new Vector3(0.11f, 0.09f, 0.11f), 0.04f, skin);

        // ----- руки -----
        bool longSleeve = look.top == 1 || look.top == 2;
        Color32 sleeve = look.top == 3 ? Undershirt : topC;
        for (int s = -1; s <= 1; s += 2)
        {
            Transform shoulder = Joint(s < 0 ? "ShoulderL" : "ShoulderR", spine, new Vector3(s * (torsoW * 0.5f + armW * 0.5f + 0.005f), 0.45f, 0f));
            Transform elbow = Joint(s < 0 ? "ElbowL" : "ElbowR", shoulder, new Vector3(0f, -0.27f, 0f));
            Part("UpperArm", shoulder, new Vector3(0f, -0.13f, 0f), new Vector3(armW, 0.29f, armW + 0.01f), 0.05f, sleeve);
            Part("Forearm", elbow, new Vector3(0f, -0.12f, 0f), new Vector3(armW - 0.01f, 0.26f, armW), 0.045f, longSleeve ? sleeve : skin);
            Part("Hand", elbow, new Vector3(0f, -0.285f, 0.005f), new Vector3(armW - 0.005f, 0.1f, armW + 0.005f), 0.04f, skin);
            if (s < 0) { shoulderL = shoulder; elbowL = elbow; } else { shoulderR = shoulder; elbowR = elbow; }
        }

        // ----- голова -----
        head = Joint("Head", spine, new Vector3(0f, 0.56f, 0f));
        Part("Skull", head, new Vector3(0f, 0.16f, 0f), new Vector3(0.3f, 0.31f, 0.28f), 0.1f, skin, 0.2f);
        Part("Nose", head, new Vector3(0f, 0.13f, 0.145f), new Vector3(0.04f, 0.06f, 0.04f), 0.018f, Shade(skin, 0.93f), 0.2f);
        Part("Mouth", head, new Vector3(0f, 0.07f, 0.139f), new Vector3(0.07f, 0.014f, 0.012f), 0.006f, Mouth);
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * 0.065f;
            Part("Sclera", head, new Vector3(x, 0.17f, 0.136f), new Vector3(0.072f, 0.066f, 0.02f), 0.012f, Sclera, 0.5f);
            Part("Iris", head, new Vector3(x, 0.168f, 0.144f), new Vector3(0.042f, 0.05f, 0.014f), 0.01f, eyeC, 0.6f);
            Part("Pupil", head, new Vector3(x, 0.168f, 0.15f), new Vector3(0.02f, 0.026f, 0.008f), 0.004f, Pupil, 0.8f);
            Part("Glint", head, new Vector3(x + 0.01f, 0.18f, 0.153f), new Vector3(0.01f, 0.01f, 0.004f), 0.002f, Sclera, 0.9f);
            Part("Brow", head, new Vector3(x, 0.222f, 0.141f), new Vector3(0.075f, 0.018f, 0.018f), 0.007f, hairC, 0.1f, 0f, 1f, new Vector3(0f, 0f, -s * 6f));
            Part("Ear", head, new Vector3(s * 0.152f, 0.15f, -0.005f), new Vector3(0.035f, 0.075f, 0.06f), 0.015f, Shade(skin, 0.96f));
        }

        BuildHair(look, hairC);
        BuildHat(look, topC);
        if (perkOutfit == "outfit_night")
        {
            // Светоотражающие полосы на корпусе и штанинах.
            Color32 reflect = new Color32(225, 235, 240, 255);
            Part("Reflect1", spine, new Vector3(0f, 0.18f, 0f), new Vector3(torsoW + 0.012f, 0.035f, torsoD + 0.012f), 0.01f, reflect, 0.9f, 0.4f);
            Part("Reflect2", spine, new Vector3(0f, 0.34f, 0f), new Vector3(torsoW + 0.012f, 0.035f, torsoD + 0.012f), 0.01f, reflect, 0.9f, 0.4f);
            if (thighL != null)
                Part("ReflectL", thighL, new Vector3(0f, -0.3f, 0f), new Vector3(legW + 0.014f, 0.03f, legW + 0.034f), 0.01f, reflect, 0.9f, 0.4f);
            if (thighR != null)
                Part("ReflectR", thighR, new Vector3(0f, -0.3f, 0f), new Vector3(legW + 0.014f, 0.03f, legW + 0.034f), 0.01f, reflect, 0.9f, 0.4f);
        }
    }

    /// <summary>Костюм — фасон и цвета; шляпа — своя модель в <see cref="BuildHat"/>.</summary>
    void ApplyPerkLook(AvatarLook look, ref Color32 topC, ref Color32 botC)
    {
        perkOutfit = PerkSystem.Worn("outfit");
        perkHat = PerkSystem.Worn("hat");
        switch (perkOutfit)
        {
            case "outfit_welder":
                look.top = 1; look.bottom = 0;
                topC = new Color32(92, 74, 58, 255); botC = new Color32(52, 48, 45, 255);
                break;
            case "outfit_engineer":
                look.top = 0; look.bottom = 2;
                topC = new Color32(232, 232, 228, 255); botC = new Color32(38, 86, 168, 255);
                break;
            case "outfit_miner":
                look.top = 1; look.bottom = 2;
                topC = new Color32(62, 62, 68, 255); botC = new Color32(40, 40, 46, 255);
                break;
            case "outfit_night":
                look.top = 3; look.bottom = 0;
                topC = new Color32(255, 128, 24, 255); botC = new Color32(28, 34, 52, 255);
                break;
        }

        if (!string.IsNullOrEmpty(perkHat))
            look.hat = 1; // причёска «под каской»
    }

    void BuildHair(AvatarLook look, Color32 c)
    {
        bool hat = look.hat != 0;
        switch (look.hair)
        {
            case 0:
                return;
            case 1: // короткая
            case 2: // ёжик
            case 4: // хвост
            case 5: // пучок
                Part("HairTop", head, new Vector3(0f, 0.292f, -0.005f), new Vector3(0.318f, 0.085f, 0.298f), 0.04f, c, 0.3f);
                Part("HairBack", head, new Vector3(0f, 0.2f, -0.121f), new Vector3(0.318f, 0.19f, 0.07f), 0.035f, c, 0.3f);
                for (int s = -1; s <= 1; s += 2)
                    Part("HairSide", head, new Vector3(s * 0.153f, 0.235f, -0.025f), new Vector3(0.03f, 0.1f, 0.2f), 0.012f, c, 0.3f);
                break;
            case 3: // каре
            case 6: // длинные
                Part("HairTop", head, new Vector3(0f, 0.292f, -0.005f), new Vector3(0.322f, 0.09f, 0.3f), 0.04f, c, 0.3f);
                Part("Fringe", head, new Vector3(0f, 0.265f, 0.128f), new Vector3(0.3f, 0.06f, 0.04f), 0.02f, c, 0.3f);
                float len = look.hair == 6 ? 0.44f : 0.26f;
                Part("HairBack", head, new Vector3(0f, 0.3f - len * 0.5f, -0.122f), new Vector3(0.326f, len, 0.075f), 0.035f, c, 0.3f);
                for (int s = -1; s <= 1; s += 2)
                    Part("HairSide", head, new Vector3(s * 0.158f, 0.3f - len * 0.42f, -0.015f), new Vector3(0.04f, len * 0.84f, 0.24f), 0.018f, c, 0.3f);
                break;
        }

        if (look.hair == 2 && !hat)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = (i % 3 - 1) * 0.085f;
                float z = i < 3 ? 0.05f : -0.06f;
                Part("Spike", head, new Vector3(x, 0.345f, z), new Vector3(0.065f, 0.085f, 0.065f), 0.02f, c, 0.3f, 0f, 0.55f,
                    new Vector3(i < 3 ? 14f : -12f, 0f, -x * 120f));
            }
        }
        if (look.hair == 4)
        {
            Part("Tail", head, new Vector3(0f, 0.11f, -0.2f), new Vector3(0.085f, 0.24f, 0.085f), 0.04f, c, 0.3f, 0f, 0.7f, new Vector3(-18f, 0f, 0f));
            Part("Tie", head, new Vector3(0f, 0.22f, -0.165f), new Vector3(0.07f, 0.03f, 0.07f), 0.015f, AvatarLook.ClothColors[look.topColor]);
        }
        if (look.hair == 5 && !hat)
            Part("Bun", head, new Vector3(0f, 0.36f, -0.08f), new Vector3(0.13f, 0.12f, 0.13f), 0.06f, c, 0.3f);
    }

    void BuildHat(AvatarLook look, Color32 topC)
    {
        switch (perkHat)
        {
            case "hat_hardhat_lamp":
            {
                Color32 orange = new Color32(240, 120, 30, 255);
                Part("HatDome", head, new Vector3(0f, 0.33f, 0f), new Vector3(0.335f, 0.16f, 0.315f), 0.075f, orange, 0.65f);
                Part("HatBrim", head, new Vector3(0f, 0.268f, 0.015f), new Vector3(0.37f, 0.026f, 0.37f), 0.012f, orange, 0.65f);
                Part("HatLamp", head, new Vector3(0f, 0.33f, 0.17f), new Vector3(0.09f, 0.07f, 0.05f), 0.02f, new Color32(255, 240, 160, 255), 0.95f, 0.2f);
                return;
            }
            case "hat_miner":
            {
                Color32 black = new Color32(30, 30, 34, 255);
                Part("HatDome", head, new Vector3(0f, 0.33f, 0f), new Vector3(0.34f, 0.17f, 0.32f), 0.08f, black, 0.5f);
                Part("HatBrim", head, new Vector3(0f, 0.268f, 0.0f), new Vector3(0.36f, 0.024f, 0.36f), 0.012f, black, 0.5f);
                Part("HatLampRim", head, new Vector3(0f, 0.34f, 0.17f), new Vector3(0.11f, 0.11f, 0.04f), 0.04f, new Color32(190, 150, 70, 255), 0.7f, 0.6f);
                Part("HatLamp", head, new Vector3(0f, 0.34f, 0.19f), new Vector3(0.07f, 0.07f, 0.02f), 0.03f, new Color32(255, 245, 200, 255), 0.95f);
                return;
            }
            case "hat_crown":
            {
                Color32 gold = new Color32(240, 196, 60, 255);
                Part("CrownBand", head, new Vector3(0f, 0.31f, 0f), new Vector3(0.31f, 0.08f, 0.29f), 0.02f, gold, 0.8f, 0.7f);
                for (int i = 0; i < 5; i++)
                {
                    float a = i / 5f * Mathf.PI * 2f;
                    Part("CrownSpike", head, new Vector3(Mathf.Sin(a) * 0.13f, 0.39f, Mathf.Cos(a) * 0.12f), new Vector3(0.05f, 0.09f, 0.05f), 0.01f, gold, 0.8f, 0.7f);
                }

                Part("CrownGem", head, new Vector3(0f, 0.32f, 0.15f), new Vector3(0.04f, 0.04f, 0.02f), 0.01f, new Color32(200, 30, 70, 255), 0.95f);
                return;
            }
        }

        switch (look.hat)
        {
            case 1: // строительная каска
                Part("HatDome", head, new Vector3(0f, 0.33f, 0f), new Vector3(0.335f, 0.16f, 0.315f), 0.075f, HardHat, 0.65f);
                Part("HatBrim", head, new Vector3(0f, 0.268f, 0.015f), new Vector3(0.37f, 0.026f, 0.37f), 0.012f, HardHat, 0.65f);
                Part("HatRidge", head, new Vector3(0f, 0.41f, 0f), new Vector3(0.05f, 0.03f, 0.29f), 0.012f, Shade(HardHat, 0.9f), 0.65f);
                break;
            case 2: // кепка
                Part("CapCrown", head, new Vector3(0f, 0.315f, -0.005f), new Vector3(0.315f, 0.11f, 0.3f), 0.05f, topC);
                Part("CapVisor", head, new Vector3(0f, 0.27f, 0.185f), new Vector3(0.23f, 0.02f, 0.13f), 0.008f, Shade(topC, 0.85f));
                break;
            case 3: // шапка
                Part("Beanie", head, new Vector3(0f, 0.315f, -0.005f), new Vector3(0.325f, 0.15f, 0.305f), 0.1f, topC);
                Part("BeanieBand", head, new Vector3(0f, 0.255f, -0.005f), new Vector3(0.332f, 0.05f, 0.312f), 0.02f, Shade(topC, 0.82f));
                break;
        }
    }
}
