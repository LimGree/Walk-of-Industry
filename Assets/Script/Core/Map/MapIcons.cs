using UnityEngine;

public static class MapIcons
{
    static Texture2D arrow;
    static Sprite arrowSprite;

    public static Texture2D PlayerArrow
    {
        get
        {
            if (arrow == null)
                arrow = BuildArrow();
            return arrow;
        }
    }

    public static Sprite PlayerArrowSprite
    {
        get
        {
            if (arrowSprite == null)
            {
                Texture2D tex = PlayerArrow;
                arrowSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 64f);
            }
            return arrowSprite;
        }
    }

    static Texture2D BuildArrow()
    {
        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[s * s];

        Color32 fill = new Color32(255, 214, 64, 255);
        Color32 edge = new Color32(28, 24, 12, 255);
        Vector2 tip = new Vector2(31.5f, 56f);
        Vector2 left = new Vector2(8f, 10f);
        Vector2 right = new Vector2(55f, 10f);
        Vector2 notch = new Vector2(31.5f, 22f);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = PointInTri(p, tip, left, notch) || PointInTri(p, tip, notch, right);
                if (!inside)
                    continue;

                float d = Mathf.Min(
                    DistToSeg(p, tip, left),
                    DistToSeg(p, tip, right),
                    DistToSeg(p, left, notch),
                    DistToSeg(p, right, notch));
                pixels[y * s + x] = d < 1.6f ? edge : fill;
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);
        return tex;
    }

    static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    static float DistToSeg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }
}
