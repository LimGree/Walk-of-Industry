using UnityEngine;

[CreateAssetMenu(fileName = "New Building", menuName = "Builderment/Building Data")]
public class BuildingData : ScriptableObject
{
    [Header("Basic Info")]
    public string id;
    public string displayName;
    [TextArea] public string description;

    [Header("Prefabs")]
    public GameObject prefab;
    public GameObject ghostPrefab;

    [Header("Conveyor")]
    [Tooltip("Если задан — это конвейер: в хотбаре один слот, прямой/угол выбирается сам.")]
    public GameObject cornerPrefab;
    [Tooltip("Гост угла для превью линии. Если пусто — берётся cornerPrefab.")]
    public GameObject cornerGhostPrefab;
    [Tooltip("T: вход сзади + один бок (зеркало на другой бок). conveer_4_5.")]
    public GameObject teePrefab;
    [Tooltip("Гост T. Если пусто — берётся teePrefab.")]
    public GameObject teeGhostPrefab;
    [Tooltip("Рисунок стрелок для второго рукава T (без зеркала меша).")]
    public Material teeMirrorMaterial;
    [Tooltip("Два бока, сзади нет. conveer_6.")]
    public GameObject sidesPrefab;
    [Tooltip("Гост двух боков. Если пусто — берётся sidesPrefab.")]
    public GameObject sidesGhostPrefab;
    [Tooltip("Три входа. conveer_3.")]
    public GameObject triplePrefab;
    [Tooltip("Гост трёх входов. Если пусто — берётся triplePrefab.")]
    public GameObject tripleGhostPrefab;
    [Tooltip("Труба: ставится линией, как конвейер, но возит только жидкости.")]
    public bool isPipe;
    [Tooltip("Парный выход подземного конвейера. Если задан — вход и выход ставятся одним жестом по прямой.")]
    public GameObject pairExitPrefab;
    [Tooltip("Максимум пустых клеток между входом и выходом (сами концы не считаются).")]
    public int pairMaxGap = 5;

    [Header("Visuals")]
    public Sprite icon;

    [Header("Grid & Placement")]
    [Tooltip("Клетки footprint: X = по оси X, Y = по оси Z. Pivot префаба = ЦЕНТР footprint.")]
    public Vector2Int size = Vector2Int.one;
    public bool canRotate = true;

    [Header("Economy")]
    [Tooltip("Цена установки в монетах.")]
    public int buildCost = 0;

    [Header("Placement")]
    [Tooltip("Ставить только на жилу. Для экстрактора и будущих шахтёров.")]
    public bool requiresResourceNode;
    [Tooltip("Можно ставить на воду. Для водокачки.")]
    public bool allowOnWater;
    [Tooltip("Только на воду (озеро / океан). Для водокачки.")]
    public bool requiresWater;

    public bool IsConveyor => cornerPrefab != null || isPipe;
    public bool IsPairedStraight => pairExitPrefab != null;

    public GameObject GetDefaultPlacePrefab()
    {
        return prefab;
    }
}
