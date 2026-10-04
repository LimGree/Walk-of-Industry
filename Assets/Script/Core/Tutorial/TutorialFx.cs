using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Подсветка обучения в мире. Одна задача — один вид подсветки:
/// клетки (куда ставить), обводка зданий (на что смотреть), маршрут ленты (куда вести, голубой),
/// столб света над целью шага (видно издалека). Маршрут считается не чаще <see cref="RouteEvery"/>.
/// </summary>
public class TutorialFx : MonoBehaviour
{
    const float RouteEvery = 1.5f;

    enum Kind { Cell, Building, Route }

    readonly List<Marker> pool = new List<Marker>(48);
    readonly List<Vector2Int> cells = new List<Vector2Int>(32);
    readonly List<Vector2Int> route = new List<Vector2Int>(64);
    readonly List<BuildingBase> buildings = new List<BuildingBase>(16);
    readonly List<Vector2Int> cachedRoute = new List<Vector2Int>(64);
    float nextRoute;
    TutorialStep routeStep = (TutorialStep)(-1);
    float nextCollect;
    TutorialStep collectedStep = (TutorialStep)(-1);

    Transform root;
    Material cellMat;
    Material buildingMat;
    Material routeMat;
    Mesh cube;
    Transform pillar;

    struct Marker
    {
        public GameObject go;
        public Transform tr;
        public Kind kind;
    }

    void OnDestroy()
    {
        Clear();
        if (cellMat != null)
            Destroy(cellMat);
        if (buildingMat != null)
            Destroy(buildingMat);
        if (routeMat != null)
            Destroy(routeMat);
    }

    public void Clear()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].go != null)
                pool[i].go.SetActive(false);
        }

        if (pillar != null)
            pillar.gameObject.SetActive(false);
    }

    public void Sync()
    {
        TutorialSystem tut = TutorialSystem.Instance;
        if (tut == null || !tut.IsRunning)
        {
            Clear();
            return;
        }

        // Сбор клеток перебирает жилы и здания — 4 раза в секунду хватает, пульс рисуется каждый кадр.
        if (Time.unscaledTime >= nextCollect || tut.Step != collectedStep)
        {
            nextCollect = Time.unscaledTime + 0.25f;
            collectedStep = tut.Step;
            cells.Clear();
            route.Clear();
            buildings.Clear();
            Collect(tut);
        }

        Show(cells, Kind.Cell, 0.82f, 0.08f, 0.12f);
        Show(route, Kind.Route, 0.5f, 0.06f, 0.16f);
        ShowBuildings(buildings);
        ShowPillar(tut);
        Pulse();
    }

    void Collect(TutorialSystem tut)
    {
        Vector3 from = TutorialSystem.PlayerPos;
        ResearchLab lab = TutorialSystem.FindLab();
        switch (tut.Step)
        {
            case TutorialStep.GoToIron:
            case TutorialStep.BuildMode:
                TutorialSystem.CollectVeinCells(TutorialSystem.IronOreId, cells, 8, from);
                break;
            case TutorialStep.PlaceExtractor:
                CollectFreeVeins(TutorialSystem.IronOreId, cells, 18, from);
                break;
            case TutorialStep.WatchOutput:
                CollectExtractors(TutorialSystem.IronOreId, buildings);
                CollectOutputCells(TutorialSystem.IronOreId, cells);
                break;
            case TutorialStep.PlaceLab:
                CollectLabSpot(cells);
                break;
            case TutorialStep.RotateBuilding:
                if (lab != null)
                    buildings.Add(lab);
                break;
            case TutorialStep.DragBelt:
            case TutorialStep.FirstBelt:
                if (lab != null)
                    buildings.Add(lab);
                Route(tut.Step, TutorialSystem.FirstExtractor(TutorialSystem.IronOreId), lab);
                break;
            case TutorialStep.FirstOreIn:
                if (lab != null)
                    buildings.Add(lab);
                break;
            case TutorialStep.GoToCopper:
                TutorialSystem.CollectVeinCells(TutorialSystem.CopperOreId, cells, 8, from);
                break;
            case TutorialStep.CopperLine:
                if (TutorialSystem.CountExtractors(TutorialSystem.CopperOreId) == 0)
                    CollectFreeVeins(TutorialSystem.CopperOreId, cells, 18, from);
                else
                    Route(tut.Step, TutorialSystem.FirstExtractor(TutorialSystem.CopperOreId), lab);
                if (lab != null)
                    buildings.Add(lab);
                break;
            case TutorialStep.SpeedUp:
                if (tut.WaitStuck)
                {
                    CollectExtractors(TutorialSystem.IronOreId, buildings);
                    CollectExtractors(TutorialSystem.CopperOreId, buildings);
                    if (lab != null)
                        buildings.Add(lab);
                }
                else
                {
                    CollectFreeVeins(TutorialSystem.IronOreId, cells, 6, from);
                    CollectFreeVeins(TutorialSystem.CopperOreId, cells, 12, from);
                }
                break;
            case TutorialStep.PlaceSmelter:
                CollectLineBelts(TutorialSystem.IronOreId, cells, 8);
                break;
            case TutorialStep.FirstIngot:
            case TutorialStep.CopyLine:
            case TutorialStep.OpenMachine:
                CollectBuildings(TutorialSystem.SmelterId, buildings);
                break;
            case TutorialStep.PasteLine:
                CollectLineBelts(TutorialSystem.CopperOreId, cells, 8);
                break;
        }
    }

    /// <summary>Маршрут ленты от выхода экстрактора до входа лаборатории (кэш на шаг).</summary>
    void Route(TutorialStep step, BuildingBase from, BuildingBase to)
    {
        if (from == null || to == null)
            return;
        if (routeStep != step || Time.unscaledTime >= nextRoute)
        {
            routeStep = step;
            nextRoute = Time.unscaledTime + RouteEvery;
            if (!BeltRouteHint.Between(from, to, cachedRoute))
                cachedRoute.Clear();
        }

        for (int i = 0; i < cachedRoute.Count; i++)
        {
            // Уже стоящая лента не нуждается в подсказке.
            if (BuildingLinker.GetBuildingAt(cachedRoute[i]) is Conveyor)
                continue;
            route.Add(cachedRoute[i]);
        }
    }

    static void CollectFreeVeins(string resourceId, List<Vector2Int> dest, int max, Vector3 from)
    {
        var raw = new List<Vector2Int>(48);
        TutorialSystem.CollectVeinCells(resourceId, raw, 48, from);
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

    static void CollectOutputCells(string resourceId, List<Vector2Int> dest)
    {
        Extractor e = TutorialSystem.FirstExtractor(resourceId);
        if (e == null || e.outputSockets == null)
            return;
        for (int s = 0; s < e.outputSockets.Length; s++)
        {
            if (e.outputSockets[s] != null)
                dest.Add(BuildingLinker.GetSocketFrontCell(e.outputSockets[s]));
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

    /// <summary>Свободные клетки в 3–7 клетках от выхода первого железного экстрактора.</summary>
    static void CollectLabSpot(List<Vector2Int> dest)
    {
        Extractor e = TutorialSystem.FirstExtractor(TutorialSystem.IronOreId);
        if (e == null || e.outputSockets == null || e.outputSockets.Length == 0 || e.outputSockets[0] == null)
            return;
        BuildingSocket socket = e.outputSockets[0];
        Vector2Int front = BuildingLinker.GetSocketFrontCell(socket);
        Vector2Int dir = BuildingLinker.SocketWorldCardinal(socket);
        Vector2Int side = new Vector2Int(-dir.y, dir.x);
        for (int d = 3; d <= 7 && dest.Count < 10; d++)
        {
            for (int s = -1; s <= 1 && dest.Count < 10; s++)
            {
                Vector2Int cell = front + dir * d + side * s;
                if (!GridOccupancy.IsCellFree(cell) || ResourceNode.HasNode(cell))
                    continue;
                if (WorldBiomeMap.Instance != null && (WorldBiomeMap.Instance.IsOcean(cell) || WorldBiomeMap.Instance.IsWater(cell)))
                    continue;
                dest.Add(cell);
            }
        }
    }

    /// <summary>Ленты линии от экстрактора руды: туда врезается плавильня.</summary>
    static void CollectLineBelts(string resourceId, List<Vector2Int> dest, int max)
    {
        Extractor e = TutorialSystem.FirstExtractor(resourceId);
        if (e == null || e.outputSockets == null)
            return;
        for (int s = 0; s < e.outputSockets.Length; s++)
        {
            if (e.outputSockets[s] == null)
                continue;
            Vector2Int cell = BuildingLinker.GetSocketFrontCell(e.outputSockets[s]);
            var seen = new HashSet<Vector2Int>();
            for (int n = 0; n < 40 && dest.Count < max; n++)
            {
                if (!seen.Add(cell))
                    break;
                if (!(BuildingLinker.GetBuildingAt(cell) is Conveyor belt))
                    break;
                // Первую клетку у самого экстрактора пропускаем — плавильню ставят чуть дальше.
                if (n >= 1)
                    dest.Add(cell);
                Vector2Int next = cell + BuildingLinker.ToCardinal(belt.transform.forward);
                cell = next;
            }
        }
    }

    void Show(List<Vector2Int> list, Kind kind, float scale, float height, float lift)
    {
        Ensure();
        int used = 0;
        float size = GridFootprint.CellSize * scale;
        for (int i = 0; i < list.Count; i++)
        {
            Marker m = Take(used++, kind);
            Vector3 pos = TutorialSystem.CellWorld(list[i]);
            pos.y += lift;
            m.tr.SetPositionAndRotation(pos, Quaternion.identity);
            m.tr.localScale = new Vector3(size, height, size);
            m.go.SetActive(true);
        }

        HideFrom(used, kind);
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
            Marker m = Take(used++, Kind.Building);
            Bounds bounds = RendererBounds(b);
            m.tr.SetPositionAndRotation(bounds.center, Quaternion.identity);
            Vector3 size = bounds.size;
            if (size.sqrMagnitude < 0.01f)
                size = Vector3.one;
            m.tr.localScale = size * 1.08f;
            m.go.SetActive(true);
        }

        HideFrom(used, Kind.Building);
    }

    /// <summary>Столб света над целью шага — виден из-за горы.</summary>
    void ShowPillar(TutorialSystem tut)
    {
        Ensure();
        bool show = tut.TryGetTarget(out Vector3 target);
        if (show)
        {
            Vector3 player = TutorialSystem.PlayerPos;
            float flat = Vector2.Distance(new Vector2(player.x, player.z), new Vector2(target.x, target.z));
            show = flat > 6f;
        }

        if (pillar == null)
        {
            Marker m = Add(Kind.Building);
            pool.Remove(m);
            pillar = m.tr;
            pillar.name = "TutPillar";
        }

        pillar.gameObject.SetActive(show);
        if (!show)
            return;
        float w = 0.35f + 0.05f * Mathf.Sin(Time.unscaledTime * 3f);
        pillar.SetPositionAndRotation(target + Vector3.up * 14f, Quaternion.identity);
        pillar.localScale = new Vector3(w, 28f, w);
    }

    static Bounds RendererBounds(BuildingBase building)
    {
        Renderer[] rs = building.GetComponentsInChildren<Renderer>();
        bool any = false;
        Bounds bounds = new Bounds(building.transform.position, Vector3.one * 0.8f);
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] == null || !rs[i].enabled || rs[i] is TextMesh || rs[i].GetComponent<TextMesh>() != null)
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
        float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f);
        SetColor(cellMat, new Color(1f, 0.82f, 0.25f, 0.35f + 0.25f * k));
        SetColor(buildingMat, new Color(1f, 0.9f, 0.35f, 0.12f + 0.12f * k));
        // Маршрут «бежит»: прозрачность чуть сдвинута по фазе.
        SetColor(routeMat, new Color(0.35f, 0.85f, 1f, 0.45f + 0.3f * (1f - k)));
    }

    static void SetColor(Material mat, Color c)
    {
        if (mat == null)
            return;
        mat.color = c;
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", c);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", c);
    }

    void HideFrom(int used, Kind kind)
    {
        int seen = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].kind != kind)
                continue;
            if (seen >= used && pool[i].go != null)
                pool[i].go.SetActive(false);
            seen++;
        }
    }

    Marker Take(int index, Kind kind)
    {
        int seen = 0;
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].kind != kind)
                continue;
            if (seen == index)
                return pool[i];
            seen++;
        }

        return Add(kind);
    }

    Marker Add(Kind kind)
    {
        Ensure();
        GameObject go = new GameObject("Tut" + kind);
        go.transform.SetParent(root, false);
        MeshFilter filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = cube;
        MeshRenderer rend = go.AddComponent<MeshRenderer>();
        rend.sharedMaterial = kind == Kind.Building ? buildingMat : kind == Kind.Route ? routeMat : cellMat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        var marker = new Marker { go = go, tr = go.transform, kind = kind };
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
        if (routeMat == null)
            routeMat = RuntimeMaterials.Create(new Color(0.35f, 0.85f, 1f, 0.6f));
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
