using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сетка + подсветка ВСЕХ клеток footprint ghost (center pivot).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(GridSystem))]
public class BuildGridVisualizer : MonoBehaviour
{
    [Header("References")]
    public PlayerBuilder playerBuilder;

    [Header("Appearance")]
    public int radiusInCells = 12;
    public float yOffset = 0.03f;
    public Color gridLineColor = new Color(1f, 1f, 1f, 0.22f);
    public Color validCellColor = new Color(0.25f, 1f, 0.4f, 0.32f);
    public Color invalidCellColor = new Color(1f, 0.3f, 0.25f, 0.32f);
    public Color footprintBorderColor = new Color(1f, 1f, 1f, 0.55f);

    [Header("Footprint cells")]
    [Tooltip("Макс. клеток footprint для отдельных подсветок (например 8×8).")]
    public int maxFootprintCells = 64;

    private GridSystem grid;
    private Transform gridPlane;
    private Material gridMaterial;
    private Texture2D gridTexture;
    private int lastDiameter = -1;

    // Отдельные клетки footprint
    private readonly List<Transform> cellHighlights = new List<Transform>();
    private readonly List<MeshRenderer> cellRenderers = new List<MeshRenderer>();
    private Material validCellMat;
    private Material invalidCellMat;

    // Общая рамка footprint
    private Transform footprintBorder;
    private Material borderMaterial;

    void Awake()
    {
        grid = GetComponent<GridSystem>();

        if (playerBuilder == null)
            playerBuilder = FindFirstObjectByType<PlayerBuilder>();

        CreateVisuals();
        SetVisible(false);
    }

    void LateUpdate()
    {
        if (playerBuilder == null || grid == null)
        {
            SetVisible(false);
            return;
        }

        if (!playerBuilder.isBuildMode)
        {
            SetVisible(false);
            return;
        }

        SetVisible(true);

        if (playerBuilder.IsBuildModeActive && playerBuilder.HasPlacementTarget)
        {
            UpdateVisuals(
                playerBuilder.CurrentPlacementPosition,
                playerBuilder.CurrentFootprintSize,
                playerBuilder.CanPlaceCurrent
            );
        }
        else if (playerBuilder.HasPlacementTarget)
        {
            PlaceGridPlane(playerBuilder.CurrentPlacementPosition, playerBuilder.CurrentPlacementPosition.y + yOffset);
            HideFootprint();
        }
        else
        {
            UpdateGridAroundPlayer();
        }
    }

    void CreateVisuals()
    {
        gridPlane = CreatePlaneChild("BuildGridPlane", gridLineColor, out gridMaterial);

        gridTexture = CreateCellBorderTexture(64);
        gridMaterial.mainTexture = gridTexture;
        gridMaterial.color = gridLineColor;

        validCellMat = CreateTransparentMaterial(validCellColor);
        validCellMat.renderQueue = 3100;
        invalidCellMat = CreateTransparentMaterial(invalidCellColor);
        invalidCellMat.renderQueue = 3100;
        borderMaterial = CreateTransparentMaterial(footprintBorderColor);
        borderMaterial.renderQueue = 3110;

        footprintBorder = CreatePlaneChild("FootprintBorder", borderMaterial);
        footprintBorder.gameObject.SetActive(false);

        // Пул клеток footprint
        int pool = Mathf.Clamp(maxFootprintCells, 1, 256);
        for (int i = 0; i < pool; i++)
        {
            Transform t = CreatePlaneChild($"FootprintCell_{i}", validCellMat);
            cellHighlights.Add(t);
            cellRenderers.Add(t.GetComponent<MeshRenderer>());
            t.gameObject.SetActive(false);
        }
    }

    Transform CreatePlaneChild(string objectName, Color color, out Material material)
    {
        material = CreateTransparentMaterial(color);
        material.renderQueue = 3100;
        return CreatePlaneChild(objectName, material);
    }

    Transform CreatePlaneChild(string objectName, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = objectName;
        go.transform.SetParent(transform, false);

        Destroy(go.GetComponent<Collider>());

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer.sharedMaterial = material;

        return go.transform;
    }

    static Material CreateTransparentMaterial(Color color)
    {
        return RuntimeMaterials.Create(color);
    }

    static Texture2D CreateCellBorderTexture(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color line = new Color(1f, 1f, 1f, 1f);
        int border = Mathf.Max(1, size / 32);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool isBorder =
                    x < border ||
                    y < border ||
                    x >= size - border ||
                    y >= size - border;

                texture.SetPixel(x, y, isBorder ? line : clear);
            }
        }

        texture.Apply();
        return texture;
    }

    void UpdateGridAroundPlayer()
    {
        Transform player = playerBuilder.transform;
        Vector3 pos = player.position;
        pos.y = grid.origin.y;
        PlaceGridPlane(pos, pos.y + yOffset);

        HideFootprint();
    }

    void HideFootprint()
    {
        if (footprintBorder != null)
            footprintBorder.gameObject.SetActive(false);
        for (int i = 0; i < cellHighlights.Count; i++)
        {
            if (cellHighlights[i] != null)
                cellHighlights[i].gameObject.SetActive(false);
        }
    }

    void PlaceGridPlane(Vector3 worldPos, float surfaceY)
    {
        float cell = grid.cellSize;
        Vector2Int centerCell = GridFootprint.GetMinCell(worldPos, Vector2Int.one);
        int diameter = radiusInCells * 2 + 1;
        float worldSize = diameter * cell;
        float planeScale = worldSize / 10f;

        if (diameter != lastDiameter)
        {
            gridMaterial.mainTextureScale = new Vector2(diameter, diameter);
            lastDiameter = diameter;
        }

        gridPlane.position = grid.GetCellCenter(centerCell, surfaceY);
        gridPlane.localScale = new Vector3(planeScale, 1f, planeScale);
    }

    void UpdateVisuals(Vector3 placementCenter, Vector2Int footprintSize, bool canPlace)
    {
        footprintSize = GridFootprint.NormalizeSize(footprintSize);
        float surfaceY = placementCenter.y + yOffset;
        float cell = grid.cellSize;

        PlaceGridPlane(placementCenter, surfaceY);

        // --- Рамка всего footprint (center pivot) ---
        Vector3 fpWorld = GridFootprint.GetWorldSize(footprintSize);
        footprintBorder.position = new Vector3(placementCenter.x, surfaceY + 0.002f, placementCenter.z);
        footprintBorder.localScale = new Vector3(
            fpWorld.x / 10f * 1.02f,
            1f,
            fpWorld.z / 10f * 1.02f
        );
        borderMaterial.color = canPlace
            ? new Color(0.4f, 1f, 0.5f, 0.35f)
            : new Color(1f, 0.35f, 0.3f, 0.4f);
        footprintBorder.gameObject.SetActive(true);

        // --- Каждая клетка footprint ---
        Vector2Int min = GridFootprint.GetMinCell(placementCenter, footprintSize);
        Material cellMat = canPlace ? validCellMat : invalidCellMat;
        int needed = footprintSize.x * footprintSize.y;
        int idx = 0;

        for (int x = 0; x < footprintSize.x; x++)
        {
            for (int z = 0; z < footprintSize.y; z++)
            {
                if (idx >= cellHighlights.Count)
                    break;

                Vector2Int cellCoord = min + new Vector2Int(x, z);
                Transform t = cellHighlights[idx];
                t.gameObject.SetActive(true);
                t.position = grid.GetCellCenter(cellCoord, surfaceY + 0.001f);
                t.localScale = new Vector3(cell / 10f * 0.92f, 1f, cell / 10f * 0.92f);
                cellRenderers[idx].sharedMaterial = cellMat;
                idx++;
            }
        }

        for (; idx < cellHighlights.Count; idx++)
            cellHighlights[idx].gameObject.SetActive(false);
    }

    void SetVisible(bool visible)
    {
        if (gridPlane != null)
            gridPlane.gameObject.SetActive(visible);

        if (footprintBorder != null)
            footprintBorder.gameObject.SetActive(visible);

        if (!visible)
        {
            for (int i = 0; i < cellHighlights.Count; i++)
            {
                if (cellHighlights[i] != null)
                    cellHighlights[i].gameObject.SetActive(false);
            }
        }
    }

    void OnDestroy()
    {
        if (gridMaterial != null) Destroy(gridMaterial);
        if (validCellMat != null) Destroy(validCellMat);
        if (invalidCellMat != null) Destroy(invalidCellMat);
        if (borderMaterial != null) Destroy(borderMaterial);
        if (gridTexture != null) Destroy(gridTexture);
    }
}
