using UnityEngine;

public class CrosshairHud : MonoBehaviour
{
    const int BaseSize = 5;
    const float ReferenceHeight = 1080f;

    Material mat;

    void OnEnable()
    {
        Camera.onPostRender += Draw;
    }

    void OnDisable()
    {
        Camera.onPostRender -= Draw;
    }

    void OnDestroy()
    {
        if (mat != null)
            Destroy(mat);
    }

    bool Visible
    {
        get
        {
            if (GameManager.Instance != null && GameManager.Instance.IsPaused)
                return false;
            return UnityEngine.Cursor.lockState == CursorLockMode.Locked;
        }
    }

    Material Mat()
    {
        if (mat != null)
            return mat;
        Shader shader = Resources.Load<Shader>("WalkToBiomeInvert");
        if (shader == null)
            shader = Shader.Find("Hidden/WalkToBiome/Invert");
        if (shader == null)
            return null;
        mat = new Material(shader);
        mat.hideFlags = HideFlags.HideAndDontSave;
        return mat;
    }

    void Draw(Camera cam)
    {
        if (!Visible || cam == null || !cam.enabled)
            return;
        if (cam.cameraType != CameraType.Game)
            return;
        if (cam.targetTexture != null)
            return;
        if (Camera.main != null && cam != Camera.main)
            return;

        Material material = Mat();
        if (material == null || !material.SetPass(0))
            return;

        int size = SizePx();
        float px = 1f / Mathf.Max(1, Screen.width);
        float py = 1f / Mathf.Max(1, Screen.height);
        float hx = size * 0.5f * px;
        float hy = size * 0.5f * py;
        float x0 = 0.5f - hx;
        float x1 = 0.5f + hx;
        float y0 = 0.5f - hy;
        float y1 = 0.5f + hy;

        GL.PushMatrix();
        GL.LoadOrtho();
        GL.Begin(GL.QUADS);
        GL.Color(Color.white);
        GL.Vertex3(x0, y0, 0f);
        GL.Vertex3(x1, y0, 0f);
        GL.Vertex3(x1, y1, 0f);
        GL.Vertex3(x0, y1, 0f);
        GL.End();
        GL.PopMatrix();
    }

    static int SizePx()
    {
        int size = Mathf.Max(3, Mathf.RoundToInt(BaseSize * Screen.height / ReferenceHeight));
        if ((size & 1) == 0)
            size++;
        return size;
    }
}
