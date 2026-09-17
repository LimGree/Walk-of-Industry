using UnityEngine;

public enum WeatherKind
{
    Clear = 0,
    Rain = 1,
    Storm = 2
}

/// <summary>
/// Ясно / дождь / гроза. Красит сутки, не трогает симуляцию завода.
/// </summary>
public static class Weather
{
    public static WeatherKind Kind { get; set; } = WeatherKind.Clear;
    public static float Cloud { get; private set; }
    public static float Rain { get; private set; }
    public static float Flash { get; set; }

    public static float Storm => Kind == WeatherKind.Storm ? Rain : 0f;

    public static void ResetToNewWorld()
    {
        Kind = WeatherKind.Clear;
        Cloud = 0f;
        Rain = 0f;
        Flash = 0f;
    }

    public static void Tick(float dt)
    {
        float wantCloud = Kind == WeatherKind.Clear ? 0f : Kind == WeatherKind.Rain ? 0.78f : 1f;
        float wantRain = Kind == WeatherKind.Clear ? 0f : Kind == WeatherKind.Rain ? 0.72f : 1f;
        Cloud = Mathf.MoveTowards(Cloud, wantCloud, dt * 0.22f);
        Rain = Mathf.MoveTowards(Rain, wantRain, dt * 0.28f);
        Flash = Mathf.MoveTowards(Flash, 0f, dt * 5.5f);
    }

    public static DayNight.Sample Filter(DayNight.Sample sample)
    {
        float c = Cloud;
        Color overcast = new Color(0.30f, 0.34f, 0.38f, 1f);
        sample.sky = Color.Lerp(sample.sky, overcast * 0.48f, c * 0.88f);
        sample.horizon = Color.Lerp(sample.horizon, overcast * 0.82f, c * 0.72f);
        sample.ground = Color.Lerp(sample.ground, overcast * 0.4f, c * 0.45f);
        sample.fog = Color.Lerp(sample.fog, new Color(0.38f, 0.41f, 0.44f, 1f), c * 0.65f);
        sample.sunIntensity *= Mathf.Lerp(1f, 0.28f, c);
        sample.stars *= 1f - c;
        sample.exposure *= Mathf.Lerp(1f, 0.78f, c);
        sample.tint = Color.Lerp(sample.tint, sample.tint * new Color(0.70f, 0.74f, 0.80f, 1f), c * 0.55f);

        if (Flash > 0.01f)
        {
            float f = Flash;
            sample.sky = Color.Lerp(sample.sky, Color.white, f * 0.62f);
            sample.horizon = Color.Lerp(sample.horizon, new Color(0.85f, 0.90f, 1f, 1f), f * 0.5f);
            sample.sunIntensity += f * 2.1f;
            sample.exposure += f * 0.4f;
            sample.tint = Color.Lerp(sample.tint, Color.white, f * 0.28f);
        }

        return sample;
    }
}
