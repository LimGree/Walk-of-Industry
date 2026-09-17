using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Стрелка I/O. Модель ставишь руками. В игре видна только в режиме строительства.
/// В редакторе всегда на месте.
/// </summary>
[DisallowMultipleComponent]
public class SocketArrow : MonoBehaviour
{
    static readonly List<SocketArrow> live = new List<SocketArrow>();
    static bool buildMode;

    MeshRenderer[] rends;

    public static SocketArrow Ensure(BuildingSocket owner)
    {
        if (owner == null)
            return null;
        return owner.GetComponentInChildren<SocketArrow>(true);
    }

    public static void SetBuildMode(bool on)
    {
        buildMode = on;
        RefreshAll();
    }

    public static void RefreshOn(Transform root)
    {
        if (root == null)
            return;
        SocketArrow[] arrows = root.GetComponentsInChildren<SocketArrow>(true);
        for (int i = 0; i < arrows.Length; i++)
        {
            if (arrows[i] != null)
                arrows[i].Apply();
        }
    }

    public static void BindNamed(Transform root)
    {
        if (root == null)
            return;
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || !IsArrowName(t.name))
                continue;
            if (t.GetComponent<SocketArrow>() == null)
                t.gameObject.AddComponent<SocketArrow>();
        }
    }

    static bool IsArrowName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        return name.StartsWith("IoArrow_In", System.StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("IoArrow_Out", System.StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("io_arrow", System.StringComparison.OrdinalIgnoreCase);
    }

    static void RefreshAll()
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            if (live[i] == null)
            {
                live.RemoveAt(i);
                continue;
            }

            live[i].Apply();
        }
    }

    void Awake()
    {
        rends = GetComponentsInChildren<MeshRenderer>(true);
        Apply();
    }

    void OnEnable()
    {
        if (!live.Contains(this))
            live.Add(this);
        Apply();
    }

    void OnDisable()
    {
        live.Remove(this);
    }

    public void Apply()
    {
        bool show = !Application.isPlaying || buildMode;
        if (show && Application.isPlaying)
        {
            Conveyor belt = GetComponentInParent<Conveyor>();
            if (belt != null)
                show = belt.ShouldShowIoArrow(this);
        }

        if (rends == null || rends.Length == 0)
            rends = GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null)
                rends[i].enabled = show;
        }
    }
}
