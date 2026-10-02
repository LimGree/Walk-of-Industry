using UnityEngine;

/// <summary>
/// Прицел в центре экрана. Стили из настроек: инверт-квадрат (как в Minecraft, виден на любом фоне),
/// точка, крест, круг. Размер и цвет (для не-инверт стилей) — тоже настройки.
/// </summary>
public class CrosshairHud : MonoBehaviour
{
    const int BaseSize = 5;
    const float ReferenceHeight = 1080f;

    Material invertMat;
    Material colorMat;

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
        if (invertMat != null)
            Destroy(invertMat);
        if (colorMat != null)
            Destroy(colorMat);
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

    Material InvertMat()
    {
        if (invertMat != null)
            return invertMat;
        Shader shader = Resources.Load<Shader>("WalkToBiomeInvert");
        if (shader == null)
            shader = Shader.Find("Hidden/WalkToBiome/Invert");
        if (shader == null)
            return null;
        invertMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        return invertMat;
    }

    Material ColorMat()
    {
        if (colorMat != null)
            return colorMat;
        Shader shader = Shader.Find("Hidden/Internal-Colored");
        if (shader == null)
            return null;
        colorMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        colorMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        colorMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        colorMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        colorMat.SetInt("_ZWrite", 0);
        colorMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        return colorMat;
    }

    void Draw(Camera cam)
    {
        if (!Visible || cam == null || !cam.enabled)
            return;
        if (cam.cameraType != CameraType.Game)
            return;
        if (Camera.main != null && cam != Camera.main)
            return;
        // Камера может рисовать в текстуру масштаба рендера — тогда рисуем туда же.
        if (cam.targetTexture != null && cam.GetComponent<RenderScaler>() == null)
            return;

        int style = GameSettings.CrosshairStyle;
        Material material = style == 0 ? InvertMat() : ColorMat();
        if (material == null)
        {
            // Нет цветного шейдера в сборке — рисуем инверт-квадратом, чем ничего.
            style = 0;
            material = InvertMat();
        }
        if (material == null || !material.SetPass(0))
            return;

        float w = Mathf.Max(1, cam.pixelWidth);
        float h = Mathf.Max(1, cam.pixelHeight);
        float unit = h / ReferenceHeight * GameSettings.CrosshairSize;
        Color color = style == 0 ? Color.white : GameSettings.CrosshairColors[GameSettings.CrosshairColor];

        GL.PushMatrix();
        GL.LoadPixelMatrix(0f, w, 0f, h);
        GL.Begin(GL.QUADS);
        GL.Color(color);
        float cx = Mathf.Round(w * 0.5f);
        float cy = Mathf.Round(h * 0.5f);
        switch (style)
        {
            case 1:
                Quad(cx, cy, Odd(4f * unit), Odd(4f * unit));
                break;
            case 2:
            {
                float len = Mathf.Max(3f, 7f * unit);
                float thick = Mathf.Max(1f, Mathf.Round(2f * unit));
                float gap = Mathf.Max(2f, 3f * unit);
                Quad(cx, cy + gap + len * 0.5f, thick, len);
                Quad(cx, cy - gap - len * 0.5f, thick, len);
                Quad(cx + gap + len * 0.5f, cy, len, thick);
                Quad(cx - gap - len * 0.5f, cy, len, thick);
                break;
            }
            case 3:
                Ring(cx, cy, Mathf.Max(4f, 8f * unit), Mathf.Max(1f, Mathf.Round(1.6f * unit)));
                Quad(cx, cy, Odd(2f * unit), Odd(2f * unit));
                break;
            default:
            {
                float size = Odd(BaseSize * unit);
                Quad(cx, cy, size, size);
                break;
            }
        }
        GL.End();
        GL.PopMatrix();
    }

    static float Odd(float px)
    {
        int size = Mathf.Max(3, Mathf.RoundToInt(px));
        if ((size & 1) == 0)
            size++;
        return size;
    }

    static void Quad(float cx, float cy, float w, float h)
    {
        float x0 = cx - w * 0.5f;
        float x1 = cx + w * 0.5f;
        float y0 = cy - h * 0.5f;
        float y1 = cy + h * 0.5f;
        GL.Vertex3(x0, y0, 0f);
        GL.Vertex3(x1, y0, 0f);
        GL.Vertex3(x1, y1, 0f);
        GL.Vertex3(x0, y1, 0f);
    }

    static void Ring(float cx, float cy, float radius, float thick)
    {
        const int Segments = 32;
        float inner = radius - thick * 0.5f;
        float outer = radius + thick * 0.5f;
        for (int i = 0; i < Segments; i++)
        {
            float a0 = i * Mathf.PI * 2f / Segments;
            float a1 = (i + 1) * Mathf.PI * 2f / Segments;
            Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
            Vector2 d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
            GL.Vertex3(cx + d0.x * inner, cy + d0.y * inner, 0f);
            GL.Vertex3(cx + d0.x * outer, cy + d0.y * outer, 0f);
            GL.Vertex3(cx + d1.x * outer, cy + d1.y * outer, 0f);
            GL.Vertex3(cx + d1.x * inner, cy + d1.y * inner, 0f);
        }
    }
}
