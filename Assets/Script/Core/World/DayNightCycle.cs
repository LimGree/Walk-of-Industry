using UnityEngine;

/// <summary>
/// Крутит [[DayNight.Hour]] в игровой сцене и каждый кадр красит небо/солнце.
/// Слайдеры в инспекторе — чтобы не ждать реальные сутки.
/// </summary>
public class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }

    [Header("Preview")]
    [Range(0f, 24f)]
    [Tooltip("Час мира. В Play можно крутить без ожидания суток.")]
    public float previewHour = 9f;
    [Min(1)]
    [Tooltip("Номер дня (1 = первый день мира).")]
    public int previewDay = 1;

    void Awake()
    {
        Instance = this;
        DayNight.Hour = DayNight.WrapHour(previewHour);
        DayNight.Day = Mathf.Max(1, previewDay);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnValidate()
    {
        previewHour = DayNight.WrapHour(previewHour);
        previewDay = Mathf.Max(1, previewDay);
        DayNight.Hour = previewHour;
        DayNight.Day = previewDay;
        if (Application.isPlaying)
            GameSettings.ApplyAtmosphere();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return;
        if (!GameSettings.DayNightEnabled)
            return;

        float minutes = Mathf.Max(1f, GameSettings.DayLengthMinutes);
        DayNight.Advance(24f * Time.deltaTime / (minutes * 60f));
        SyncFromWorld();
    }

    void LateUpdate()
    {
        SyncFromWorld();
        GameSettings.ApplyAtmosphere();
    }

    void SyncFromWorld()
    {
        previewHour = DayNight.Hour;
        previewDay = Mathf.Max(1, DayNight.Day);
    }

    public void CaptureSave(SaveData data)
    {
        if (data == null)
            return;
        data.worldHour = DayNight.Hour;
        data.worldDay = DayNight.Day;
    }

    public void ApplySave(SaveData data)
    {
        if (data == null)
            ResetToNewWorld();
        else
        {
            DayNight.Hour = DayNight.WrapHour(data.worldHour);
            DayNight.Day = Mathf.Max(1, data.worldDay);
        }
        SyncFromWorld();
        GameSettings.ApplyAtmosphere();
    }

    public void ResetToNewWorld()
    {
        DayNight.ResetToNewWorld();
        SyncFromWorld();
        GameSettings.ApplyAtmosphere();
    }
}
