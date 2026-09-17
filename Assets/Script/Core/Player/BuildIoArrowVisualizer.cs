using UnityEngine;

/// <summary>
/// Раньше рисовал свои стрелки I/O в режиме стройки.
/// Стрелки теперь на префабах (IoArrow_In / IoArrow_Out) — этот визуализатор выключен,
/// чтобы не дублировать входы и выходы.
/// </summary>
[DisallowMultipleComponent]
public class BuildIoArrowVisualizer : MonoBehaviour
{
    void Awake()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name.StartsWith("IoArrow_"))
                Destroy(child.gameObject);
        }

        enabled = false;
    }

    void OnEnable()
    {
        enabled = false;
    }
}
