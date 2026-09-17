using System.Collections.Generic;
using UnityEngine;

public class TutorialFx : MonoBehaviour
{
    const int PoolKeep = 28;

    readonly List<Marker> pool = new List<Marker>(32);
    readonly List<Vector2Int> cells = new List<Vector2Int>(32);
    readonly List<BuildingBase> buildings = new List<BuildingBase>(16);
    Transform root;
    Material cellMat;
    Material buildingMat;
    Mesh cube;

    struct Marker
    {
        public GameObject go;
        public Transform tr;
        public Renderer rend;
        public bool building;
    }

    void OnDestroy()
    {
        Clear();
        if (cellMat != null)
            Destroy(cellMat);
        if (buildingMat != null)
            Destroy(buildingMat);
    }

    public void Clear()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].go != null)
                pool[i].go.SetActive(false);
        }
    }

    public void Sync()
    {
        TutorialSystem tut = TutorialSystem.Instance;
        if (tut == null || !tut.IsRunning)
        {
            Clear();
            return;
        }

        cells.Clear();
        buildings.Clear();
        Color cellColor = new Color(1f, 0.82f, 0.25f, 0.55f);
        Collect(tut, cells, buildings, ref cellColor);
        ShowCells(cells, cellColor);
        ShowBuildings(buildings);
        Pulse();
    }

    static void Collect(TutorialSystem tut, List<Vector2Int> cells, List<BuildingBase> buildings, ref Color cellColor)
    {
        Vector3 from = TutorialSystem.PlayerPos;
        switch (tut.Step)
        {
            case TutorialStep.LookMove:
                TutorialSystem.CollectVeinCells(TutorialSystem.IronOreId, cells, 10, from);
                TutorialSystem.CollectVeinCells(TutorialSystem.CopperOreId, cells, 6, from);
                cellColor = new Color(1f, 0.85f, 0.4f, 0.28f);
                break;
            case TutorialStep.IronOne:
            case TutorialStep.IronThree:
                CollectFreeVeins(TutorialSystem.IronOreId, cells, 18, from);
                CollectExtractors(TutorialSystem.IronOreId, buildings);
                break;
            case TutorialStep.Copy:
                CollectExtractors(TutorialSystem.IronOreId, buildings);
                break;
            case TutorialStep.PasteCopper:
                CollectFreeVeins(TutorialSystem.CopperOreId, cells, 18, from);
                CollectExtractors(TutorialSystem.CopperOreId, buildings);
                cellColor = new Color(0.95f, 0.55f, 0.2f, 0.55f);
                break;
            case TutorialStep.Lab:
                CollectLabSpot(cells);
                ResearchLab lab = TutorialSystem.FindLab();
                if (lab != null)
                    buildings.Add(lab);
                break;
            case TutorialStep.StartResearch:
            case TutorialStep.ResearchIronIngot:
            case TutorialStep.ResearchCopperIngot:
                lab = TutorialSystem.FindLab();
                if (lab != null)
                    buildings.Add(lab);
                break;
            case TutorialStep.Belts:
            case TutorialStep.WaitChapter1:
                if (tut.Step == TutorialStep.Belts || tut.WaitStuck)
                {
                    CollectExtractors(TutorialSystem.IronOreId, buildings);
                    CollectExtractors(TutorialSystem.CopperOreId, buildings);
                    lab = TutorialSystem.FindLab();
                    if (lab != null)
                        buildings.Add(lab);
                }
                break;
            case TutorialStep.PlaceSmelter:
                CollectBeltCellsNear(TutorialSystem.IronOreId, cells, 10);
                break;
            case TutorialStep.PickRecipe:
            case TutorialStep.FirstSmelt:
                CollectBuildings(TutorialSystem.SmelterId, buildings);
                break;
            case TutorialStep.CopySmelter:
                CollectBuildings(TutorialSystem.SmelterId, buildings);
                CollectFreeVeins(TutorialSystem.CopperOreId, cells, 10, from);
                CollectBeltCellsNear(TutorialSystem.CopperOreId, cells, 8);
                break;
        }
    }

    static void CollectFreeVeins(string resourceId, List<Vector2Int> dest, int max, Vector3 from)
    {
        var raw = new List<Vector2Int>(32);
        TutorialSystem.CollectVeinCells(resourceId, raw, 40, from);
        for (int i = 0; i < raw.Count && dest.Count < max; i++)
        {
            if (!GridOccupancy.IsCellFree(raw[i]))
                continue;
            dest.Add(raw[i]);
        }
    }

    static void CollectExtractors(string resourceId, List<BuildingBase> dest)
    {
        Extractor[] list = Object.FindObjectsByType<Extractor>(FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            Extractor e = list[i];
            if (e == null || e.resource == null || !TutorialSystem.IdsEqual(e.resource.id, resourceId))
                continue;
            dest.Add(e);
        }
    }

    static void CollectBuildings(string id, List<BuildingBase> dest)
    {
        BuildingBase[] list = Object.FindObjectsByType<BuildingBase>(FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            if (list[i] != null && list[i].data != null && TutorialSystem.IdsEqual(list[i].data.id, id))
                dest.Add(list[i]);
        }
    }

    static void CollectLabSpot(List<Vector2Int> dest)
    {
        Vector3 iron = ClusterCenter(TutorialSystem.IronOreId);
        Vector3 copper = ClusterCenter(TutorialSystem.CopperOreId);
        if (iron.sqrMagnitude < 0.01f && copper.sqrMagnitude < 0.01f)
            return;
        Vector3 mid = iron.sqrMagnitude < 0.01f ? copper
            : copper.sqrMagnitude < 0.01f ? iron
            : (iron + copper) * 0.5f;
        Vector2Int origin = BuildingLinker.WorldToCell(mid);
        for (int z = -4; z <= 4 && dest.Count < 12; z++)
        {
            for (int x = -4; x <= 4 && dest.Count < 12; x++)
            {
                Vector2Int cell = origin + new Vector2Int(x, z);
                if (!GridOccupancy.IsCellFree(cell) || ResourceNode.HasNode(cell))
                    continue;
                if (WorldBiomeMap.Instance != null && WorldBiomeMap.Instance.IsOcean(cell))
                    continue;
                dest.Add(cell);
            }
        }
    }

    static void CollectBeltCellsNear(string resourceId, List<Vector2Int> dest, int max)
    {
        Extractor[] list = Object.FindObjectsByType<Extractor>(FindObjectsSortMode.None);
        for (int i = 0; i < list.Length && dest.Count < max; i++)
        {
            Extractor e = list[i];
            if (e == null || e.resource == null || !TutorialSystem.IdsEqual(e.resource.id, resourceId))
                continue;
            if (e.outputSockets == null)
                continue;
            for (int s = 0; s < e.outputSockets.Length && dest.Count < max; s++)
            {
                BuildingSocket socket = e.outputSockets[s];
                if (socket == null)
                    continue;
                Vector2Int cell = BuildingLinker.GetSocketFrontCell(socket);
                if (GridOccupancy.IsCellFree(cell))
                    dest.Add(cell);
                BuildingBase front = BuildingLinker.GetBuildingAt(cell);
                if (front is Conveyor)
                {
                    Vector2Int next = BuildingLinker.WorldToCell(front.transform.position);
                    dest.Add(next);
                }
            }
        }
    }

    static Vector3 ClusterCenter(string resourceId)
    {
        Vector3 sum = Vector3.zero;
        int n = 0;
        Extractor[] list = Object.FindObjectsByType<Extractor>(FindObjectsSortMode.None);
        for (int i = 0; i < list.Length; i++)
        {
            Extractor e = list[i];
            if (e == null || e.resource == null || !TutorialSystem.IdsEqual(e.resource.id, resourceId))
                continue;
            sum += e.transform.position;
            n++;
        }
        return n > 0 ? sum / n : Vector3.zero;
    }

    void ShowCells(List<Vector2Int> list, Color color)
    {
        Ensure();
        int used = 0;
        float size = GridFootprint.CellSize * 0.82f;
        for (int i = 0; i < list.Count; i++)
        {
            Marker m = Take(used++, false);
            Vector3 pos = TutorialSystem.CellWorld(list[i]);
            pos.y += 0.12f;
            m.tr.SetPositionAndRotation(pos, Quaternion.identity);
            m.tr.localScale = new Vector3(size, 0.08f, size);
            if (m.rend != null)
                m.rend.sharedMaterial = Tint(cellMat, color);
            m.go.SetActive(true);
        }

        HideFrom(used, false);
    }

    void ShowBuildings(List<BuildingBase> list)
    {
        Ensure();
        int used = 0;
        for (int i = 0; i < list.Count; i++)
        {
            BuildingBase b = list[i];
            if (b == null)
                continue;
            Marker m = Take(used++, true);
            Bounds bounds = RendererBounds(b);
            m.tr.SetPositionAndRotation(bounds.center, Quaternion.identity);
            Vector3 size = bounds.size;
            if (size.sqrMagnitude < 0.01f)
                size = Vector3.one;
            m.tr.localScale = size * 1.08f;
            m.go.SetActive(true);
        }

        HideFrom(used, true);
    }

    static Bounds RendererBounds(BuildingBase building)
    {
        Renderer[] rs = building.GetComponentsInChildren<Renderer>();
        bool any = false;
        Bounds bounds = new Bounds(building.transform.position, Vector3.one * 0.8f);
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] == null || !rs[i].enabled)
                continue;
            if (!any)
            {
                bounds = rs[i].bounds;
                any = true;
            }
            else
                bounds.Encapsulate(rs[i].bounds);
        }
        return bounds;
    }

    void Pulse()
    {
        float s = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 4.2f);
        Color cell = new Color(1f, 0.82f, 0.25f, 0.35f + 0.2f * (s - 1f) * 8f);
        Color building = new Color(1f, 0.9f, 0.35f, 0.12f + 0.12f * (s - 1f) * 8f);
        if (cellMat != null)
        {
            cellMat.color = cell;
            if (cellMat.HasProperty("_Color"))
                cellMat.SetColor("_Color", cell);
        }
        if (buildingMat != null)
        {
            buildingMat.color = building;
            if (buildingMat.HasProperty("_Color"))
                buildingMat.SetColor("_Color", building);
        }
    }

    void HideFrom(int used, bool building)
    {
        int seen = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].building != building)
                continue;
            if (seen >= used && pool[i].go != null)
                pool[i].go.SetActive(false);
            seen++;
        }
    }

    Marker Take(int index, bool building)
    {
        int seen = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].building != building)
                continue;
            if (seen == index)
                return pool[i];
            seen++;
        }

        return Add(building);
    }

    Marker Add(bool building)
    {
        Ensure();
        GameObject go = new GameObject(building ? "TutBuilding" : "TutCell");
        go.transform.SetParent(root, false);
        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = cube;
        MeshRenderer rend = go.AddComponent<MeshRenderer>();
        rend.sharedMaterial = building ? buildingMat : cellMat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        var marker = new Marker { go = go, tr = go.transform, rend = rend, building = building };
        pool.Add(marker);
        return marker;
    }

    void Ensure()
    {
        if (root == null)
        {
            GameObject go = new GameObject("TutorialFx");
            go.transform.SetParent(transform, false);
            root = go.transform;
        }

        if (cube == null)
            cube = CubeMesh();
        if (cellMat == null)
            cellMat = RuntimeMaterials.Create(new Color(1f, 0.82f, 0.25f, 0.5f));
        if (buildingMat == null)
        {
            buildingMat = RuntimeMaterials.Create(new Color(1f, 0.9f, 0.35f, 0.22f));
            if (buildingMat != null)
            {
                buildingMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                buildingMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                buildingMat.SetInt("_ZWrite", 0);
                buildingMat.renderQueue = 3100;
            }
        }
    }

    static Material Tint(Material mat, Color color)
    {
        if (mat == null)
            return null;
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        mat.color = color;
        return mat;
    }

    static Mesh CubeMesh()
    {
        var mesh = new Mesh { name = "TutCube" };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
            new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
            new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
            new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
        };
        mesh.triangles = new[]
        {
            0, 2, 1, 0, 3, 2,
            4, 5, 6, 4, 6, 7,
            0, 1, 5, 0, 5, 4,
            3, 6, 2, 3, 7, 6,
            0, 7, 3, 0, 4, 7,
            1, 2, 6, 1, 6, 5
        };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
