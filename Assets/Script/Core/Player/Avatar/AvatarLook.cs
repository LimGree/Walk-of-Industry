using System;
using UnityEngine;

/// <summary>
/// Внешность персонажа. Одна на все миры: хранится в профиле игрока (PlayerPrefs), не в сейве мира.
/// Набор вариантов намеренно небольшой — персонаж собирается за минуту-две.
/// </summary>
[Serializable]
public class AvatarLook
{
    public const int BodyCount = 2;
    public const int HairCount = 7;
    public const int HatCount = 4;
    public const int TopCount = 4;
    public const int BottomCount = 3;

    // Порядок важен: индексы лежат в сохранённом профиле.
    public static readonly string[] BodyKeys = { "avatar.body_slim", "avatar.body_sturdy" };
    public static readonly string[] HairKeys =
    {
        "avatar.hair_none", "avatar.hair_short", "avatar.hair_spiky", "avatar.hair_bob",
        "avatar.hair_ponytail", "avatar.hair_bun", "avatar.hair_long"
    };
    public static readonly string[] HatKeys = { "avatar.hat_none", "avatar.hat_hardhat", "avatar.hat_cap", "avatar.hat_beanie" };
    public static readonly string[] TopKeys = { "avatar.top_tshirt", "avatar.top_longsleeve", "avatar.top_hoodie", "avatar.top_vest" };
    public static readonly string[] BottomKeys = { "avatar.bottom_pants", "avatar.bottom_shorts", "avatar.bottom_overalls" };

    public static readonly Color32[] SkinColors =
    {
        new Color32(255, 224, 196, 255), new Color32(241, 194, 158, 255), new Color32(224, 172, 131, 255),
        new Color32(198, 140, 99, 255), new Color32(160, 105, 70, 255), new Color32(126, 82, 55, 255),
        new Color32(92, 60, 40, 255), new Color32(66, 43, 30, 255)
    };

    public static readonly Color32[] HairColors =
    {
        new Color32(30, 26, 24, 255), new Color32(60, 40, 28, 255), new Color32(103, 68, 40, 255),
        new Color32(140, 62, 35, 255), new Color32(196, 104, 48, 255), new Color32(220, 184, 110, 255),
        new Color32(232, 226, 210, 255), new Color32(150, 150, 150, 255), new Color32(60, 110, 200, 255),
        new Color32(220, 110, 160, 255)
    };

    public static readonly Color32[] EyeColors =
    {
        new Color32(95, 62, 35, 255), new Color32(130, 110, 55, 255), new Color32(70, 140, 80, 255),
        new Color32(70, 120, 200, 255), new Color32(130, 140, 150, 255), new Color32(200, 140, 40, 255)
    };

    public static readonly Color32[] ClothColors =
    {
        new Color32(214, 148, 62, 255), new Color32(186, 72, 68, 255), new Color32(60, 100, 170, 255),
        new Color32(40, 52, 80, 255), new Color32(70, 130, 80, 255), new Color32(100, 110, 60, 255),
        new Color32(230, 190, 50, 255), new Color32(225, 225, 220, 255), new Color32(120, 125, 130, 255),
        new Color32(35, 37, 40, 255), new Color32(120, 90, 170, 255), new Color32(50, 140, 140, 255)
    };

    public int body;
    public int skin = 1;
    public int hair = 1;
    public int hairColor = 2;
    public int eyes;
    public int hat = 1;
    public int top = 3;
    public int topColor;
    public int bottom;
    public int bottomColor = 3;

    const string PrefsKey = "AvatarLook";
    static AvatarLook current;

    /// <summary>Внешность сохранена — живой персонаж в мире пересобирается.</summary>
    public static event Action Changed;

    public static AvatarLook Current
    {
        get
        {
            if (current == null)
                current = Load();
            return current;
        }
    }

    public AvatarLook Clone()
    {
        return (AvatarLook)MemberwiseClone();
    }

    public bool SameAs(AvatarLook other)
    {
        return other != null && JsonUtility.ToJson(this) == JsonUtility.ToJson(other);
    }

    public void Clamp()
    {
        body = Mathf.Clamp(body, 0, BodyCount - 1);
        skin = Mathf.Clamp(skin, 0, SkinColors.Length - 1);
        hair = Mathf.Clamp(hair, 0, HairCount - 1);
        hairColor = Mathf.Clamp(hairColor, 0, HairColors.Length - 1);
        eyes = Mathf.Clamp(eyes, 0, EyeColors.Length - 1);
        hat = Mathf.Clamp(hat, 0, HatCount - 1);
        top = Mathf.Clamp(top, 0, TopCount - 1);
        topColor = Mathf.Clamp(topColor, 0, ClothColors.Length - 1);
        bottom = Mathf.Clamp(bottom, 0, BottomCount - 1);
        bottomColor = Mathf.Clamp(bottomColor, 0, ClothColors.Length - 1);
    }

    public static AvatarLook Random()
    {
        var look = new AvatarLook
        {
            body = UnityEngine.Random.Range(0, BodyCount),
            skin = UnityEngine.Random.Range(0, SkinColors.Length),
            hair = UnityEngine.Random.Range(0, HairCount),
            hairColor = UnityEngine.Random.Range(0, HairColors.Length),
            eyes = UnityEngine.Random.Range(0, EyeColors.Length),
            hat = UnityEngine.Random.Range(0, HatCount),
            top = UnityEngine.Random.Range(0, TopCount),
            topColor = UnityEngine.Random.Range(0, ClothColors.Length),
            bottom = UnityEngine.Random.Range(0, BottomCount),
            bottomColor = UnityEngine.Random.Range(0, ClothColors.Length)
        };
        // Верх и низ одного цвета смотрятся как пижама — разводим.
        if (look.bottomColor == look.topColor)
            look.bottomColor = (look.bottomColor + 3) % ClothColors.Length;
        return look;
    }

    static AvatarLook Load()
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        AvatarLook look = null;
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                look = JsonUtility.FromJson<AvatarLook>(json);
            }
            catch (Exception)
            {
                look = null;
            }
        }
        if (look == null)
            look = new AvatarLook();
        look.Clamp();
        return look;
    }

    public static void Save(AvatarLook look)
    {
        if (look == null)
            return;
        look.Clamp();
        current = look.Clone();
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(current));
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
