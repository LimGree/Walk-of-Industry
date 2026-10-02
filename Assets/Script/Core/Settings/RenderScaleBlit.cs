using UnityEngine;

/// <summary>Вспомогательная камера: копирует уменьшенный кадр на экран.</summary>
public class RenderScaleBlit : MonoBehaviour
{
    public RenderScaler owner;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (owner != null && owner.enabled && owner.Target != null)
            Graphics.Blit(owner.Target, dst);
        else
            Graphics.Blit(src, dst);
    }
}
