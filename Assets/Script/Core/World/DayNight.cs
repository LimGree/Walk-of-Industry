using UnityEngine;

/// <summary>
/// Часы мира 0…24 и палитра неба/солнца. Крутит время [[DayNightCycle]].
/// </summary>
public static class DayNight
{
    public const float Dawn = 5.4f;
    public const float Sunrise = 6.35f;
    public const float Noon = 12f;
    public const float Sunset = 18.55f;
    public const float Dusk = 19.7f;

    public static float Hour { get; set; } = 9f;
    public static int Day { get; set; } = 1;

    public enum ClockParts
    {
        Hours = 0,
        HoursMinutes = 1,
        HoursMinutesSeconds = 2
    }

    public struct Sample
    {
        public Vector3 sunDir;
        public Color sunColor;
        public Color sky;
        public Color horizon;
        public Color ground;
        public Color sunset;
        public Color fog;
        public Color tint;
        public float sunIntensity;
        public float stars;
        public float exposure;
        public float dayFactor;
    }

    struct Key
    {
        public float hour;
        public Color sky;
        public Color horizon;
        public Color ground;
        public Color sun;
        public Color sunset;
        public float intensity;
        public float stars;
        public float exposure;
    }

    static readonly Key[] Keys =
    {
        K(0.00f, C(0.08f, 0.11f, 0.20f), C(0.12f, 0.16f, 0.26f), C(0.07f, 0.07f, 0.09f), C(0.70f, 0.76f, 0.90f), C(0.22f, 0.24f, 0.34f), 0.32f, 0.88f, 0.96f),
        K(4.20f, C(0.10f, 0.12f, 0.22f), C(0.16f, 0.14f, 0.24f), C(0.08f, 0.07f, 0.09f), C(0.74f, 0.70f, 0.84f), C(0.34f, 0.22f, 0.26f), 0.28f, 0.82f, 0.96f),
        K(5.15f, C(0.10f, 0.12f, 0.24f), C(0.42f, 0.22f, 0.28f), C(0.08f, 0.05f, 0.05f), C(0.95f, 0.55f, 0.38f), C(0.95f, 0.32f, 0.22f), 0.18f, 0.55f, 0.82f),
        K(5.85f, C(0.22f, 0.28f, 0.48f), C(0.98f, 0.52f, 0.32f), C(0.16f, 0.10f, 0.08f), C(1.00f, 0.72f, 0.42f), C(1.00f, 0.42f, 0.16f), 0.42f, 0.12f, 0.95f),
        K(6.50f, C(0.38f, 0.55f, 0.82f), C(0.98f, 0.78f, 0.58f), C(0.24f, 0.20f, 0.16f), C(1.00f, 0.88f, 0.68f), C(1.00f, 0.55f, 0.28f), 0.78f, 0f, 1.05f),
        K(8.00f, C(0.40f, 0.64f, 0.94f), C(0.78f, 0.88f, 0.96f), C(0.30f, 0.28f, 0.24f), C(1.00f, 0.96f, 0.88f), C(0.90f, 0.70f, 0.48f), 1.02f, 0f, 1.12f),
        K(12.0f, C(0.36f, 0.62f, 0.98f), C(0.72f, 0.86f, 0.96f), C(0.32f, 0.30f, 0.26f), C(1.00f, 0.98f, 0.94f), C(0.70f, 0.78f, 0.90f), 1.18f, 0f, 1.18f),
        K(16.2f, C(0.40f, 0.60f, 0.90f), C(0.86f, 0.82f, 0.70f), C(0.30f, 0.26f, 0.20f), C(1.00f, 0.92f, 0.78f), C(0.95f, 0.62f, 0.32f), 1.05f, 0f, 1.10f),
        K(17.7f, C(0.32f, 0.42f, 0.68f), C(0.98f, 0.62f, 0.32f), C(0.22f, 0.14f, 0.10f), C(1.00f, 0.70f, 0.38f), C(1.00f, 0.40f, 0.12f), 0.70f, 0.05f, 1.00f),
        K(18.55f, C(0.18f, 0.18f, 0.38f), C(0.95f, 0.38f, 0.18f), C(0.14f, 0.08f, 0.06f), C(1.00f, 0.52f, 0.22f), C(1.00f, 0.28f, 0.08f), 0.38f, 0.22f, 0.92f),
        K(19.4f, C(0.12f, 0.12f, 0.28f), C(0.48f, 0.24f, 0.36f), C(0.10f, 0.07f, 0.08f), C(0.80f, 0.52f, 0.56f), C(0.74f, 0.28f, 0.32f), 0.28f, 0.50f, 0.92f),
        K(21.0f, C(0.09f, 0.12f, 0.22f), C(0.14f, 0.16f, 0.26f), C(0.07f, 0.07f, 0.09f), C(0.72f, 0.76f, 0.90f), C(0.24f, 0.22f, 0.32f), 0.34f, 0.82f, 0.96f),
        K(24.0f, C(0.08f, 0.11f, 0.20f), C(0.12f, 0.16f, 0.26f), C(0.07f, 0.07f, 0.09f), C(0.70f, 0.76f, 0.90f), C(0.22f, 0.24f, 0.34f), 0.32f, 0.88f, 0.96f)
    };

    public static Sample Evaluate(float hour)
    {
        hour = WrapHour(hour);
        Key a;
        Key b;
        float t;
        Pick(hour, out a, out b, out t);

        Vector3 sunDir = SunDirection(hour);
        float elev = sunDir.y;
        float day = saturate(Mathf.InverseLerp(-0.08f, 0.22f, elev));

        Sample s = new Sample
        {
            sunDir = sunDir,
            sunColor = Color.Lerp(a.sun, b.sun, t),
            sky = Color.Lerp(a.sky, b.sky, t),
            horizon = Color.Lerp(a.horizon, b.horizon, t),
            ground = Color.Lerp(a.ground, b.ground, t),
            sunset = Color.Lerp(a.sunset, b.sunset, t),
            sunIntensity = Mathf.Lerp(a.intensity, b.intensity, t),
            stars = Mathf.Lerp(a.stars, b.stars, t),
            exposure = Mathf.Lerp(a.exposure, b.exposure, t),
            dayFactor = day
        };

        s.fog = Color.Lerp(s.horizon, s.sky, 0.18f);
        s.fog.a = 1f;
        Color fill = Color.Lerp(new Color(0.42f, 0.50f, 0.68f, 1f), s.sunColor, day);
        float fillMul = Mathf.Lerp(0.70f, 1f, day);
        s.tint = fill * fillMul;
        s.tint.a = 1f;
        return s;
    }

    public static Vector3 SunDirection(float hour)
    {
        hour = WrapHour(hour);
        float solar = (hour - 6f) / 24f * Mathf.PI * 2f;
        Vector3 dir = new Vector3(Mathf.Cos(solar), Mathf.Sin(solar), 0.22f);
        return dir.normalized;
    }

    public static string FormatHour(float hour)
    {
        return FormatClock(hour, Day, ClockParts.HoursMinutes, false);
    }

    public static string FormatClock(float hour, int day, ClockParts parts, bool showDay)
    {
        hour = WrapHour(hour);
        int h = Mathf.FloorToInt(hour);
        float frac = (hour - h) * 3600f;
        int m = Mathf.FloorToInt(frac / 60f);
        int s = Mathf.FloorToInt(frac - m * 60f);
        string time;
        switch (parts)
        {
            case ClockParts.Hours:
                time = h.ToString("00");
                break;
            case ClockParts.HoursMinutesSeconds:
                time = h.ToString("00") + ":" + m.ToString("00") + ":" + s.ToString("00");
                break;
            default:
                time = h.ToString("00") + ":" + m.ToString("00");
                break;
        }

        if (!showDay)
            return time;
        return UiLocale.T("clock.day", Mathf.Max(1, day)) + "  " + time;
    }

    public static void Advance(float hours)
    {
        float h = Hour + hours;
        while (h >= 24f)
        {
            h -= 24f;
            Day++;
        }

        while (h < 0f)
        {
            h += 24f;
            if (Day > 1)
                Day--;
        }

        Hour = h;
        if (Day < 1)
            Day = 1;
    }

    public static float WrapHour(float hour)
    {
        hour %= 24f;
        if (hour < 0f)
            hour += 24f;
        return hour;
    }

    public static void ResetToNewWorld()
    {
        Hour = 7.4f;
        Day = 1;
    }

    static void Pick(float hour, out Key a, out Key b, out float t)
    {
        a = Keys[0];
        b = Keys[1];
        t = 0f;
        for (int i = 0; i < Keys.Length - 1; i++)
        {
            if (hour >= Keys[i].hour && hour <= Keys[i + 1].hour)
            {
                a = Keys[i];
                b = Keys[i + 1];
                float span = b.hour - a.hour;
                t = span <= 0.0001f ? 0f : (hour - a.hour) / span;
                t = Smooth(t);
                return;
            }
        }
    }

    static float Smooth(float t)
    {
        t = saturate(t);
        return t * t * (3f - 2f * t);
    }

    static float saturate(float v)
    {
        return Mathf.Clamp01(v);
    }

    static Color C(float r, float g, float b)
    {
        return new Color(r, g, b, 1f);
    }

    static Key K(float hour, Color sky, Color horizon, Color ground, Color sun, Color sunset, float intensity, float stars, float exposure)
    {
        return new Key
        {
            hour = hour,
            sky = sky,
            horizon = horizon,
            ground = ground,
            sun = sun,
            sunset = sunset,
            intensity = intensity,
            stars = stars,
            exposure = exposure
        };
    }
}
