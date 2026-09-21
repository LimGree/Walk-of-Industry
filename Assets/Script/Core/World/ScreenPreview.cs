using UnityEngine;

public static class ScreenPreview
{
    public static byte[] CapturePng(int width = 640, int height = 360)
    {
        Camera cam = Camera.main;
        if (cam == null)
            return null;

        int w = Mathf.Max(160, width);
        int h = Mathf.Max(90, height);
        RenderTexture rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        RenderTexture prev = cam.targetTexture;
        RenderTexture prevActive = RenderTexture.active;
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply(false, false);
        cam.targetTexture = prev;
        RenderTexture.active = prevActive;
        RenderTexture.ReleaseTemporary(rt);
        byte[] png = tex.EncodeToPNG();
        Object.Destroy(tex);
        return png;
    }
}
