using UnityEngine;

/// <summary>
/// Тряска камеры: «травма» 0..1, затухает по реальному времени. Сила — настройка «Тряска камеры».
/// Смещение и крен читает PlayerMovement и добавляет к локальной позе камеры.
/// </summary>
public static class CameraFx
{
    static float trauma;
    static float lastTime;

    /// <summary>Толчок. strength 0..1; source — точка в мире, дальше 40 м толчок слабеет до нуля.</summary>
    public static void Shake(float strength, Vector3? source = null)
    {
        float scale = GameSettings.CameraShake;
        if (scale <= 0.001f)
            return;
        if (source.HasValue && WorldView.PlayerPos != Vector3.zero)
        {
            float dist = Vector3.Distance(source.Value, WorldView.PlayerPos);
            strength *= Mathf.Clamp01(1f - dist / 40f);
        }
        trauma = Mathf.Clamp01(trauma + strength * scale);
    }

    /// <summary>Смещение (м) и крен (°) на этот кадр.</summary>
    public static void Sample(out Vector3 offset, out float roll)
    {
        float now = Time.unscaledTime;
        float dt = Mathf.Clamp(now - lastTime, 0f, 0.1f);
        lastTime = now;
        trauma = Mathf.Max(0f, trauma - dt * 1.6f);
        float s = trauma * trauma;
        if (s <= 0.0001f)
        {
            offset = Vector3.zero;
            roll = 0f;
            return;
        }
        float t = now * 28f;
        offset = new Vector3(
            (Mathf.PerlinNoise(t, 0.1f) - 0.5f) * 0.16f,
            (Mathf.PerlinNoise(0.3f, t) - 0.5f) * 0.12f,
            0f) * s;
        roll = (Mathf.PerlinNoise(t, 7.7f) - 0.5f) * 4f * s;
    }
}
