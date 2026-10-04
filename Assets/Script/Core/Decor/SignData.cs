using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

public enum SignMount { Posts, Pole, Hanging, Plaque, Stand }

public enum SignKind { Text, Symbol, Icon, Plate }

/// <summary>
/// Содержимое таблички ([[Decorations]], модель — <see cref="SignView"/>, редактор — [[SignEditorUI]]):
/// стойка, размер, рамка, цвета и элементы (текст, символ, иконка предмета/здания, цветная плашка).
/// Координаты элементов — доли доски (−0.5…0.5 от центра), размеры — доли её высоты, поэтому смена
/// размера доски не ломает раскладку. В сейве, undo, копировании и чертежах — JSON (extras «sign»).
/// </summary>
[Serializable]
public class SignData
{
    public const int MaxElements = 24;
    public const int MaxText = 160;

    public int mount;
    public int size = 1;
    public int frame = 1;
    public string board = "6B4A2B";
    public string frameColor = "C9A24A";
    public string postColor = "3A3F44";
    public bool twoSided = true;
    public bool glow;
    public List<SignElement> items = new List<SignElement>();

    public SignMount Mount => (SignMount)Mathf.Clamp(mount, 0, 4);

    public string ToJson()
    {
        return JsonUtility.ToJson(this);
    }

    public static SignData FromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return null;
        try
        {
            SignData d = JsonUtility.FromJson<SignData>(json);
            d?.Sanitize();
            return d;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Sign] Битые данные таблички: " + e.Message);
            return null;
        }
    }

    public SignData Clone()
    {
        return FromJson(ToJson()) ?? new SignData();
    }

    public void Sanitize()
    {
        mount = Mathf.Clamp(mount, 0, 4);
        size = Mathf.Clamp(size, 0, 2);
        frame = Mathf.Clamp(frame, 0, 2);
        board = SignColors.Clean(board, "6B4A2B");
        frameColor = SignColors.Clean(frameColor, "C9A24A");
        postColor = SignColors.Clean(postColor, "3A3F44");
        if (items == null)
            items = new List<SignElement>();
        items.RemoveAll(e => e == null);
        if (items.Count > MaxElements)
            items.RemoveRange(MaxElements, items.Count - MaxElements);
        for (int i = 0; i < items.Count; i++)
            items[i].Sanitize();
    }

    /// <summary>Размер доски в метрах: S / M / L; подвесная чуть уже — её держит кронштейн.</summary>
    public Vector2 BoardSize
    {
        get
        {
            Vector2 s;
            switch (size)
            {
                case 0: s = new Vector2(1.0f, 0.6f); break;
                case 2: s = new Vector2(1.86f, 1.15f); break;
                default: s = new Vector2(1.5f, 0.85f); break;
            }

            if (Mount == SignMount.Hanging)
                s *= 0.82f;
            return s;
        }
    }
}

[Serializable]
public class SignElement
{
    public int kind;
    /// <summary>Текст, символ или id иконки (<c>item:iron_ore</c>, <c>bld:smelter</c>).</summary>
    public string text = "";
    public float x;
    public float y;
    public float rot;
    /// <summary>Высота строки / символа / иконки в долях высоты доски.</summary>
    public float size = 0.22f;
    /// <summary>Плашка: ширина и высота в долях доски.</summary>
    public float w = 0.6f;
    public float h = 0.2f;
    public string color = "FFFFFF";
    public bool bold;
    public bool italic;
    public bool shadow;
    /// <summary>0 — по центру, 1 — влево, 2 — вправо.</summary>
    public int align;

    public SignKind Kind => (SignKind)Mathf.Clamp(kind, 0, 3);

    public void Sanitize()
    {
        kind = Mathf.Clamp(kind, 0, 3);
        if (text == null)
            text = "";
        if (text.Length > SignData.MaxText)
            text = text.Substring(0, SignData.MaxText);
        x = Mathf.Clamp(x, -0.6f, 0.6f);
        y = Mathf.Clamp(y, -0.6f, 0.6f);
        rot = Mathf.Repeat(rot + 180f, 360f) - 180f;
        size = Mathf.Clamp(size, 0.04f, 1.2f);
        w = Mathf.Clamp(w, 0.02f, 1.2f);
        h = Mathf.Clamp(h, 0.02f, 1.2f);
        align = Mathf.Clamp(align, 0, 2);
        color = SignColors.Clean(color, "FFFFFF");
    }

    public SignElement Copy()
    {
        return (SignElement)MemberwiseClone();
    }

    // ---------- Конструкторы для шаблонов ----------

    public static SignElement Text(string text, float x, float y, float size, string color,
        bool bold = false, float rot = 0f, bool italic = false, bool shadow = false, int align = 0)
    {
        return new SignElement
        {
            kind = (int)SignKind.Text, text = text, x = x, y = y, size = size, color = color,
            bold = bold, rot = rot, italic = italic, shadow = shadow, align = align
        };
    }

    public static SignElement Symbol(string glyph, float x, float y, float size, string color, float rot = 0f, bool shadow = false)
    {
        return new SignElement
        {
            kind = (int)SignKind.Symbol, text = glyph, x = x, y = y, size = size, color = color, rot = rot, shadow = shadow
        };
    }

    public static SignElement Icon(string id, float x, float y, float size, float rot = 0f)
    {
        return new SignElement { kind = (int)SignKind.Icon, text = id, x = x, y = y, size = size, rot = rot, color = "FFFFFF" };
    }

    public static SignElement Plate(float x, float y, float w, float h, string color, float rot = 0f)
    {
        return new SignElement { kind = (int)SignKind.Plate, x = x, y = y, w = w, h = h, color = color, rot = rot };
    }
}

/// <summary>Палитра таблички и разбор цвета «RRGGBB».</summary>
public static class SignColors
{
    public static readonly string[] Palette =
    {
        "FFFFFF", "C8CCD0", "7A8088", "2A2D31", "0E0F11",
        "D23A2E", "6E1F2A", "F08A24", "F2C230", "C9A24A",
        "8CC63F", "2E9B4E", "2B3A2E", "1F8A8A", "3EC1E0",
        "2F6FD6", "1C2E5A", "7B4BC4", "E0559A", "E3D3A8",
        "9A6A3A", "6B4A2B",
    };

    public static string Clean(string hex, string fallback)
    {
        string s = Normalize(hex);
        return s ?? fallback;
    }

    /// <summary>«#abc123» / «ABC123» → «ABC123»; не цвет — null.</summary>
    public static string Normalize(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return null;
        string s = hex.Trim().TrimStart('#').ToUpperInvariant();
        if (s.Length != 6)
            return null;
        for (int i = 0; i < 6; i++)
        {
            char c = s[i];
            bool ok = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');
            if (!ok)
                return null;
        }

        return s;
    }

    public static Color Parse(string hex)
    {
        string s = Normalize(hex) ?? "FFFFFF";
        int v = int.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f, 1f);
    }

    public static string ToHex(Color c)
    {
        return ColorUtility.ToHtmlStringRGB(c);
    }
}

/// <summary>
/// Символы для табличек: только то, что точно есть во встроенном шрифте (набор WGL4) —
/// иначе в сборке вместо значка будет пустой квадрат.
/// </summary>
public static class SignGlyphs
{
    public static readonly string[] All =
    {
        "→", "←", "↑", "↓", "↔", "↕", "►", "◄", "▲", "▼",
        "●", "○", "■", "□", "♦", "♥", "♣", "♠", "☼", "☺",
        "♪", "♫", "∞", "√", "≈", "±", "×", "÷", "°", "№",
        "!", "?", "#", "&", "@", "%", "$", "€", "©", "Ω",
    };
}

/// <summary>Готовые таблички: шаблоны в редакторе и выставка в [[TestYard]].</summary>
public static class SignPresets
{
    public struct Preset
    {
        public string id;
        public string titleKey;
        public Func<SignData> make;
    }

    public static readonly Preset[] All =
    {
        P("default", "sign.preset.default", Default),
        P("warehouse", "sign.preset.warehouse", Warehouse),
        P("caution", "sign.preset.caution", Caution),
        P("welcome", "sign.preset.welcome", Welcome),
        P("directions", "sign.preset.directions", Directions),
        P("menu", "sign.preset.menu", Menu),
        P("chain", "sign.preset.chain", Chain),
        P("award", "sign.preset.award", Award),
        P("drawing", "sign.preset.drawing", Drawing),
        P("sticker", "sign.preset.sticker", Sticker),
        P("neon", "sign.preset.neon", Neon),
        P("blank", "sign.preset.blank", Blank),
    };

    static Preset P(string id, string key, Func<SignData> make)
    {
        return new Preset { id = id, titleKey = key, make = make };
    }

    public static SignData Find(string id)
    {
        for (int i = 0; i < All.Length; i++)
        {
            if (All[i].id == id)
                return All[i].make();
        }

        return Default();
    }

    static bool Ru => UiLocale.IsRu;

    /// <summary>Только что поставленная табличка: подсказывает, что её можно настроить.</summary>
    public static SignData Default()
    {
        var d = new SignData { mount = (int)SignMount.Posts, board = "6B4A2B", frameColor = "C9A24A", postColor = "4A3424" };
        d.items.Add(SignElement.Text(Ru ? "МОЯ ТАБЛИЧКА" : "MY SIGN", 0f, 0.1f, 0.24f, "F4E6C2", bold: true, shadow: true));
        string key = KeybindStore.Hint("Interact");
        if (string.IsNullOrEmpty(key))
            key = "E";
        d.items.Add(SignElement.Text(key + (Ru ? " — настроить" : " — customize"), 0f, -0.22f, 0.13f, "E3D3A8", italic: true));
        return d;
    }

    public static SignData Blank()
    {
        return new SignData { mount = (int)SignMount.Posts, board = "E3D3A8", frameColor = "6B4A2B", postColor = "4A3424" };
    }

    static SignData Warehouse()
    {
        var d = new SignData { mount = (int)SignMount.Posts, board = "2F6FD6", frameColor = "FFFFFF", postColor = "7A8088" };
        d.items.Add(SignElement.Plate(-0.32f, 0f, 0.3f, 0.78f, "1C2E5A"));
        d.items.Add(SignElement.Icon("bld:storage_container", -0.32f, 0.02f, 0.62f));
        d.items.Add(SignElement.Text(Ru ? "СКЛАД №1" : "STORAGE 1", 0.15f, 0.16f, 0.26f, "FFFFFF", bold: true));
        d.items.Add(SignElement.Text(Ru ? "приёмка руды" : "ore intake", 0.15f, -0.08f, 0.14f, "C8CCD0"));
        d.items.Add(SignElement.Symbol("→", 0.15f, -0.3f, 0.26f, "F2C230"));
        return d;
    }

    static SignData Caution()
    {
        var d = new SignData { mount = (int)SignMount.Pole, frame = 2, board = "F2C230", frameColor = "0E0F11", postColor = "2A2D31" };
        // диагональные полосы сверху и снизу — повёрнутые плашки
        for (int i = 0; i < 7; i++)
        {
            float x = -0.45f + i * 0.15f;
            d.items.Add(SignElement.Plate(x, 0.42f, 0.05f, 0.3f, "0E0F11", 35f));
            d.items.Add(SignElement.Plate(x, -0.42f, 0.05f, 0.3f, "0E0F11", 35f));
        }

        d.items.Add(SignElement.Plate(0f, 0.42f, 1.02f, 0.02f, "F2C230"));
        d.items.Add(SignElement.Symbol("▲", -0.3f, 0.02f, 0.5f, "0E0F11"));
        d.items.Add(SignElement.Text("!", -0.3f, -0.02f, 0.3f, "F2C230", bold: true));
        d.items.Add(SignElement.Text(Ru ? "ОСТОРОЖНО\nКОНВЕЙЕР" : "CAUTION\nCONVEYOR", 0.12f, 0.01f, 0.17f, "0E0F11", bold: true));
        return d;
    }

    static SignData Welcome()
    {
        var d = new SignData { mount = (int)SignMount.Hanging, frame = 2, board = "9A6A3A", frameColor = "6B4A2B", postColor = "2A2D31" };
        d.items.Add(SignElement.Text(Ru ? "Добро\nпожаловать!" : "Welcome\nhome!", 0f, 0.02f, 0.23f, "F4E6C2", italic: true, shadow: true));
        d.items.Add(SignElement.Symbol("♥", -0.4f, 0.3f, 0.2f, "D23A2E", -15f));
        d.items.Add(SignElement.Symbol("♥", 0.4f, 0.3f, 0.2f, "D23A2E", 15f));
        d.items.Add(SignElement.Symbol("☼", 0.38f, -0.3f, 0.24f, "F2C230", 0f));
        return d;
    }

    static SignData Directions()
    {
        var d = new SignData { mount = (int)SignMount.Pole, size = 2, board = "2E9B4E", frameColor = "FFFFFF", postColor = "7A8088" };
        d.items.Add(SignElement.Text(Ru ? "← ШАХТА" : "← MINE", -0.44f, 0.28f, 0.17f, "FFFFFF", bold: true, align: 1));
        d.items.Add(SignElement.Text(Ru ? "ПЛАВИЛЬНЯ →" : "SMELTERS →", 0.44f, 0f, 0.17f, "FFFFFF", bold: true, align: 2));
        d.items.Add(SignElement.Text(Ru ? "↑ ЛАБОРАТОРИЯ" : "↑ LAB", -0.44f, -0.28f, 0.17f, "FFFFFF", bold: true, align: 1));
        d.items.Add(SignElement.Plate(0f, 0.14f, 0.92f, 0.012f, "FFFFFF"));
        d.items.Add(SignElement.Plate(0f, -0.14f, 0.92f, 0.012f, "FFFFFF"));
        return d;
    }

    static SignData Menu()
    {
        var d = new SignData { mount = (int)SignMount.Plaque, size = 2, frame = 2, board = "2B3A2E", frameColor = "9A6A3A", postColor = "6B4A2B" };
        d.items.Add(SignElement.Text(Ru ? "МЕНЮ" : "MENU", 0f, 0.33f, 0.2f, "FFFFFF", bold: true));
        d.items.Add(SignElement.Symbol("♪", -0.22f, 0.33f, 0.16f, "F2C230", -10f));
        d.items.Add(SignElement.Symbol("♫", 0.22f, 0.33f, 0.16f, "F2C230", 10f));
        d.items.Add(SignElement.Text(Ru ? "Кофе ........ 5\nЧай .......... 3\nПончик ...... 8" : "Coffee ...... 5\nTea .......... 3\nDonut ....... 8",
            -0.4f, -0.1f, 0.13f, "E3D3A8", align: 1));
        d.items.Add(SignElement.Text(Ru ? "мел!" : "chalk!", 0.33f, -0.33f, 0.11f, "E0559A", italic: true, rot: 12f));
        return d;
    }

    static SignData Chain()
    {
        var d = new SignData { mount = (int)SignMount.Posts, size = 2, board = "2A2D31", frameColor = "F08A24", postColor = "2A2D31" };
        d.items.Add(SignElement.Text(Ru ? "ЛИНИЯ ЖЕЛЕЗА" : "IRON LINE", 0f, 0.33f, 0.15f, "F08A24", bold: true));
        d.items.Add(SignElement.Icon("item:iron_ore", -0.33f, -0.04f, 0.38f));
        d.items.Add(SignElement.Symbol("→", -0.165f, -0.04f, 0.24f, "C8CCD0"));
        d.items.Add(SignElement.Icon("item:iron_ingot", 0f, -0.04f, 0.38f));
        d.items.Add(SignElement.Symbol("→", 0.165f, -0.04f, 0.24f, "C8CCD0"));
        d.items.Add(SignElement.Icon("item:gear", 0.33f, -0.04f, 0.38f));
        d.items.Add(SignElement.Text(Ru ? "руда   слиток   шестерня" : "ore     ingot     gear", 0f, -0.36f, 0.1f, "7A8088"));
        return d;
    }

    static SignData Award()
    {
        var d = new SignData { mount = (int)SignMount.Stand, size = 0, frame = 2, board = "6E1F2A", frameColor = "C9A24A", postColor = "2A2D31" };
        d.items.Add(SignElement.Text(Ru ? "ЛУЧШИЙ\nЦЕХ" : "BEST\nWORKSHOP", 0f, 0.06f, 0.22f, "F2C230", bold: true, shadow: true));
        d.items.Add(SignElement.Text("♦  ♦  ♦", 0f, -0.33f, 0.14f, "C9A24A"));
        return d;
    }

    static SignData Drawing()
    {
        // домик-завод из плашек: стены, крыша из двух повёрнутых плашек, труба, дым кружками
        var d = new SignData { mount = (int)SignMount.Posts, board = "3EC1E0", frameColor = "FFFFFF", postColor = "7A8088" };
        d.items.Add(SignElement.Plate(0f, -0.4f, 1f, 0.2f, "2E9B4E"));
        d.items.Add(SignElement.Plate(-0.12f, -0.12f, 0.36f, 0.42f, "D23A2E"));
        d.items.Add(SignElement.Plate(-0.21f, 0.15f, 0.22f, 0.06f, "2A2D31", -28f));
        d.items.Add(SignElement.Plate(-0.03f, 0.15f, 0.22f, 0.06f, "2A2D31", 28f));
        d.items.Add(SignElement.Plate(-0.12f, -0.22f, 0.08f, 0.22f, "6B4A2B"));
        d.items.Add(SignElement.Plate(-0.22f, -0.03f, 0.07f, 0.12f, "F2C230"));
        d.items.Add(SignElement.Plate(-0.02f, -0.03f, 0.07f, 0.12f, "F2C230"));
        d.items.Add(SignElement.Plate(0.12f, 0.12f, 0.06f, 0.44f, "7A8088"));
        d.items.Add(SignElement.Symbol("●", 0.16f, 0.38f, 0.14f, "FFFFFF"));
        d.items.Add(SignElement.Symbol("●", 0.24f, 0.42f, 0.18f, "E3E6E8"));
        d.items.Add(SignElement.Symbol("☼", -0.4f, 0.34f, 0.26f, "F2C230"));
        d.items.Add(SignElement.Text(Ru ? "ЗАВОД" : "FACTORY", 0.33f, -0.12f, 0.15f, "1C2E5A", bold: true, rot: -8f));
        return d;
    }

    static SignData Sticker()
    {
        var d = new SignData { mount = (int)SignMount.Posts, board = "FFFFFF", frameColor = "2A2D31", postColor = "2A2D31" };
        d.items.Add(SignElement.Plate(-0.18f, 0.06f, 0.72f, 0.42f, "D23A2E", -9f));
        d.items.Add(SignElement.Text(Ru ? "НОВИНКА!" : "NEW!", -0.18f, 0.07f, 0.24f, "FFFFFF", bold: true, rot: -9f));
        d.items.Add(SignElement.Plate(0.3f, -0.26f, 0.34f, 0.28f, "F2C230", 14f));
        d.items.Add(SignElement.Text("-40%", 0.3f, -0.26f, 0.16f, "0E0F11", bold: true, rot: 14f));
        d.items.Add(SignElement.Symbol("☼", 0.36f, 0.3f, 0.3f, "F08A24", 20f));
        d.items.Add(SignElement.Text(Ru ? "повернуть можно всё" : "rotate anything", -0.12f, -0.38f, 0.09f, "7A8088", italic: true, rot: 4f));
        return d;
    }

    static SignData Neon()
    {
        var d = new SignData { mount = (int)SignMount.Posts, board = "0E0F11", frameColor = "E0559A", postColor = "0E0F11", glow = true };
        d.items.Add(SignElement.Text(Ru ? "ОТКРЫТО" : "OPEN", 0f, 0.12f, 0.3f, "E0559A", bold: true, shadow: true));
        d.items.Add(SignElement.Text("24/7", 0f, -0.22f, 0.22f, "3EC1E0", bold: true));
        d.items.Add(SignElement.Plate(0f, -0.04f, 0.7f, 0.015f, "3EC1E0"));
        return d;
    }
}
