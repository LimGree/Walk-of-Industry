using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBuilder : MonoBehaviour
{
    [Header("References")]
    public PlayerInventory inventory;
    public PlayerMovement playerMovement;
    public BuildMenuUI buildMenuUI;
    public Camera playerCamera;
    public LayerMask buildLayer = ~0;
    public LayerMask collisionLayer = ~0;
    public LayerMask demolishLayer = ~0;

    [Header("Settings")]
    public float maxBuildDistance = 12f;
    public Material ghostValidMaterial;
    public Material ghostInvalidMaterial;

    public bool IsBuildModeActive => isBuildMode && currentBuildingData != null;
    public bool HasPlacementTarget { get; private set; }
    public Vector3 CurrentPlacementPosition { get; private set; }
    public bool CanPlaceCurrent { get; private set; }
    public Vector2Int CurrentFootprintSize { get; private set; } = Vector2Int.one;

    public bool isBuildMode = false;
    public event Action<bool> OnBuildModeChanged;

    public bool BlocksBuildInput => selection != null && selection.BlocksBuildInput;
    public bool IsLineStrokeActive => strokeActive;
    public int PreviewBuildCount => strokeActive ? Mathf.Max(1, strokeSlots.Count) : 1;
    public bool HasHeldBuilding => currentBuildingData != null;
    public GameObject CurrentGhost => currentGhost;
    public BuildingData CurrentBuildingData => currentBuildingData;
    public BuildSelectionController Selection => selection;

    BuildSelectionController selection;
    private InputSystem_Actions inputActions;
    private GameObject currentGhost;
    private BuildingData currentBuildingData;
    private float currentRotationY = 0f;
    private bool canPlace = false;
    private int indexBuilding = 0;

    struct LineSlot
    {
        public Vector2Int min;
        public Vector3 pos;
        public bool valid;
        public float yaw;
    }

    bool strokeActive;
    BuildingData strokeBuilding;
    float strokeYaw;
    Vector2Int strokeSize = Vector2Int.one;
    Vector2Int strokeStartMin;
    Vector2Int? strokeAxis;
    readonly List<LineSlot> strokeSlots = new List<LineSlot>(16);
    readonly List<GameObject> lineGhosts = new List<GameObject>(16);
    const int MaxLineBuildings = 64;

    void Awake()
    {
        inputActions = KeybindStore.Shared;

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();

        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerCamera == null)
            playerCamera = Camera.main;

        if (buildMenuUI == null)
            buildMenuUI = FindFirstObjectByType<BuildMenuUI>();

        selection = GetComponent<BuildSelectionController>();
        if (selection == null)
            selection = gameObject.AddComponent<BuildSelectionController>();

        isBuildMode = false;
        SocketArrow.SetBuildMode(false);
    }

    void OnEnable()
    {
        inputActions.Player.Place.started += OnPlaceStarted;
        inputActions.Player.Place.canceled += OnPlaceCanceled;
        inputActions.Player.Demolish.performed += OnDemolish;
        inputActions.Player.Rotate.performed += OnRotate;
        inputActions.Player.BuildMode.performed += OnBuildModeToggle;
    }

    void OnDisable()
    {
        inputActions.Player.Place.started -= OnPlaceStarted;
        inputActions.Player.Place.canceled -= OnPlaceCanceled;
        inputActions.Player.Demolish.performed -= OnDemolish;
        inputActions.Player.Rotate.performed -= OnRotate;
        inputActions.Player.BuildMode.performed -= OnBuildModeToggle;
        EndStroke();
        DestroyGhost();
        ClearPlacementTarget();
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
        {
            EndStroke();
            return;
        }

        BuildingData selected = inventory != null ? inventory.GetSelectedBuilding() : null;
        if (selected != currentBuildingData)
        {
            EndStroke();
            currentBuildingData = selected;
            RecreateGhost();
        }

        if (!isBuildMode)
        {
            EndStroke();
            DestroyGhost();
            ClearPlacementTarget();
            return;
        }

        if (buildMenuUI != null && buildMenuUI.IsOpen)
        {
            EndStroke();
            if (currentGhost != null)
                currentGhost.SetActive(false);
            ClearPlacementTarget();
            return;
        }

        if (WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen)
        {
            EndStroke();
            if (currentGhost != null)
                currentGhost.SetActive(false);
            ClearPlacementTarget();
            return;
        }

        UpdateAim();

        if (BlocksBuildInput)
        {
            EndStroke();
            if (currentGhost != null)
                currentGhost.SetActive(false);
            Conveyor.ClearWorldVisualOverrides();
            Conveyor.PreviewExits.Clear();
            return;
        }

        if (IsBuildModeActive)
        {
            if (strokeActive)
                currentRotationY = strokeYaw;

            if (strokeActive)
                TickLineStroke();
            else
                UpdateGhost();
        }
        else
        {
            EndStroke();
            DestroyGhost();
        }
    }

    bool IsGameplayBuildInputBlocked()
    {
        if (KeybindStore.BlocksGameplayInput)
            return true;
        if (GameManager.Instance != null && GameManager.Instance.IsPaused)
            return true;
        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            return true;
        if (ResearchUI.Instance != null && ResearchUI.Instance.IsOpen)
            return true;
        if (buildMenuUI != null && buildMenuUI.IsOpen)
            return true;
        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            return true;
        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
            return true;
        if (WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen)
            return true;
        if (TutorialSystem.Instance != null && TutorialSystem.Instance.IsModal)
            return true;
        if (UiModal.IsOpen)
            return true;
        if (DevConsole.IsOpen)
            return true;
        return false;
    }

    void OnRotate(InputAction.CallbackContext ctx)
    {
        if (!isBuildMode || strokeActive || IsGameplayBuildInputBlocked() || BlocksBuildInput)
            return;
        HandleRotateKey();
    }

    void OnDemolish(InputAction.CallbackContext ctx)
    {
        if (!isBuildMode || IsGameplayBuildInputBlocked() || BlocksBuildInput)
            return;
        EndStroke();
        TryDemolish();
    }

    void HandleRotateKey()
    {
        if (HasHeldBuilding)
        {
            currentRotationY += 90f;
            if (currentRotationY >= 360f)
                currentRotationY = 0f;
            if (currentGhost != null)
                currentGhost.transform.rotation = Quaternion.Euler(0f, currentRotationY, 0f);
            if (playerCamera != null)
                GameAudio.World("world_rotate", playerCamera.transform.position);
            return;
        }

        TryRotatePlacedBuilding();
    }

    bool TryRotatePlacedBuilding()
    {
        if (playerCamera == null || GridSystem.Instance == null)
            return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, demolishLayer))
            return false;
        if (IsStrokeGhost(hit.transform))
            return false;

        BuildingBase building = hit.collider.GetComponentInParent<BuildingBase>();
        if (building == null)
            return false;
        if (building is UndergroundConveyor)
            return false;

        float prevYaw = building.transform.eulerAngles.y;
        float newYaw = prevYaw + 90f;
        building.transform.rotation = Quaternion.Euler(0f, newYaw, 0f);

        if (building.data != null)
        {
            Vector2Int newSize = GridFootprint.GetRotatedSize(building.data.size, newYaw);
            Vector2Int minCell = GridFootprint.GetMinCell(building.transform.position, newSize);

            GridOccupancy.Unregister(building.gameObject);
            if (!GridOccupancy.IsAreaFree(minCell, newSize))
            {
                building.transform.rotation = Quaternion.Euler(0f, prevYaw, 0f);
                building.ReRegisterOnGrid();
                Debug.LogWarning($"[Builder] Rotate blocked: footprint occupied for {building.name}");
                return true;
            }

            GridOccupancy.Register(building.gameObject, minCell, newSize);
        }
        else
        {
            building.ReRegisterOnGrid();
        }

        building.OnRotated();
        GameAudio.World("world_rotate", building.transform.position);
        return true;
    }

    void OnBuildModeToggle(InputAction.CallbackContext ctx)
    {
        if (IsGameplayBuildInputBlocked())
            return;

        if (!isBuildMode)
            EnterBuildMode(openMenu: false);
        else
            ExitBuildMode();
    }

    public void EnterBuildMode(bool openMenu)
    {
        isBuildMode = true;
        SocketArrow.SetBuildMode(true);

        if (buildMenuUI != null && buildMenuUI.IsOpen)
            buildMenuUI.CloseMenu(restorePlayerControl: false);

        ApplyGameplayCursorAndControl(buildMenuOpen: false);
        OnBuildModeChanged?.Invoke(true);
    }

    public void ExitBuildMode()
    {
        isBuildMode = false;
        SocketArrow.SetBuildMode(false);
        EndStroke();
        DestroyGhost();
        ClearPlacementTarget();

        if (buildMenuUI != null && buildMenuUI.IsOpen)
            buildMenuUI.CloseMenu(restorePlayerControl: false);

        ApplyGameplayCursorAndControl(buildMenuOpen: false);
        OnBuildModeChanged?.Invoke(false);
    }

    public void OnBuildMenuClosedAfterSelection()
    {
        ApplyGameplayCursorAndControl(buildMenuOpen: false);
    }

    void ApplyGameplayCursorAndControl(bool buildMenuOpen)
    {
        if (buildMenuOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (playerMovement != null)
            {
                playerMovement.canMove = false;
                playerMovement.canLook = false;
            }
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (playerMovement != null)
            {
                playerMovement.canMove = true;
                playerMovement.canLook = true;
            }
        }
    }

    public void SetBuildMode(bool enabled)
    {
        if (enabled)
            EnterBuildMode(openMenu: false);
        else
            ExitBuildMode();
    }

    void RecreateGhost()
    {
        ClearLineGhosts();
        DestroyGhost();

        if (!isBuildMode || currentBuildingData == null)
            return;

        GameObject ghostSource = BuildingVisuals.SourceForGhost(currentBuildingData);
        if (ghostSource == null)
            return;

        currentGhost = Instantiate(ghostSource);
        BuildingVisuals.PrepareGhostInstance(currentGhost);

        Conveyor belt = currentGhost.GetComponent<Conveyor>();
        if (belt == null && currentBuildingData.IsConveyor)
            belt = currentGhost.AddComponent<Conveyor>();
        if (belt != null)
            belt.PreparePreview(currentBuildingData);
    }

    void DestroyGhost()
    {
        ClearLineGhosts();
        if (currentGhost != null)
        {
            Destroy(currentGhost);
            currentGhost = null;
        }
    }

    void ClearLineGhosts()
    {
        for (int i = 0; i < lineGhosts.Count; i++)
        {
            if (lineGhosts[i] != null)
                Destroy(lineGhosts[i]);
        }
        lineGhosts.Clear();
        Conveyor.ClearWorldVisualOverrides();
        Conveyor.PreviewExits.Clear();
    }

    void ClearPlacementTarget()
    {
        HasPlacementTarget = false;
        CanPlaceCurrent = false;
        CurrentFootprintSize = Vector2Int.one;
    }

    public bool TryGetAimCell(out Vector2Int cell, out Vector3 worldPos)
    {
        cell = default;
        worldPos = default;
        if (!IsAimingAtBuildSurface())
            return false;
        worldPos = CurrentPlacementPosition;
        cell = GridFootprint.GetMinCell(worldPos, Vector2Int.one);
        return true;
    }

    void UpdateAim()
    {
        if (playerCamera == null)
        {
            ClearPlacementTarget();
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, buildLayer))
        {
            canPlace = false;
            ClearPlacementTarget();
            return;
        }

        Quaternion rot = Quaternion.Euler(0f, currentRotationY, 0f);
        Vector2Int size = currentBuildingData != null ? GetPlacementSize(rot) : Vector2Int.one;
        Vector3 placePos = currentBuildingData != null
            ? SnapToGrid(hit.point)
            : (GridSystem.Instance != null
                ? GridSystem.Instance.SnapToGrid(hit.point)
                : GridFootprint.SnapCenter(hit.point, Vector2Int.one));

        CurrentPlacementPosition = placePos;
        CurrentFootprintSize = size;
        HasPlacementTarget = true;
        CanPlaceCurrent = currentBuildingData != null && IsPlacementValid(placePos, rot);
        canPlace = CanPlaceCurrent;
    }

    void UpdateGhost()
    {
        if (currentGhost == null)
        {
            if (currentBuildingData != null)
                RecreateGhost();
            if (currentGhost == null)
                return;
        }

        if (!HasPlacementTarget)
        {
            currentGhost.SetActive(false);
            Conveyor.ClearWorldVisualOverrides();
            Conveyor.PreviewExits.Clear();
            return;
        }

        Vector3 placePos = CurrentPlacementPosition;
        Quaternion rot = Quaternion.Euler(0f, currentRotationY, 0f);
        currentGhost.transform.SetPositionAndRotation(placePos, rot);

        Conveyor.PreviewExits.Clear();
        Conveyor ghostBelt = currentGhost.GetComponent<Conveyor>();
        if (ghostBelt != null)
        {
            Conveyor.RegisterPreviewExit(placePos, currentRotationY);
            ghostBelt.Preview(placePos, rot);
            Conveyor.ApplyWorldVisualOverrides();
        }
        else
            Conveyor.ClearWorldVisualOverrides();

        Conveyor.PreviewExits.Clear();

        canPlace = IsPlacementValid(placePos, rot);
        CanPlaceCurrent = canPlace;
        TintGhost(currentGhost, canPlace);
        currentGhost.SetActive(true);
    }

    void TintGhost(GameObject ghost, bool valid)
    {
        GhostTint.Apply(ghost, valid, ghostValidMaterial, ghostInvalidMaterial);
    }

    Vector3 SnapToGrid(Vector3 position)
    {
        Vector2Int size = GetPlacementSize(Quaternion.Euler(0f, currentRotationY, 0f));
        if (GridSystem.Instance != null)
            return GridSystem.Instance.SnapFootprintCenter(position, size);
        return GridFootprint.SnapCenter(position, size);
    }

    bool IsPlacementValid(Vector3 position, Quaternion rotation, bool checkLabLimit = true)
    {
        Vector2Int size = GetPlacementSize(rotation);

        if (GridSystem.Instance != null)
        {
            Vector2Int minCell = GridFootprint.GetMinCell(position, size);
            if (!GridOccupancy.IsAreaFree(minCell, size))
                return false;
            bool allowWater = currentBuildingData != null && currentBuildingData.allowOnWater;
            bool requireWater = currentBuildingData != null && currentBuildingData.requiresWater;
            if (!WorldBiomeMap.CanBuild(minCell, size, allowWater, requireWater))
                return false;
        }

        if (checkLabLimit
            && IsResearchLabData(currentBuildingData)
            && ResearchSystem.Instance != null
            && !ResearchSystem.Instance.CanPlaceAnotherLab())
        {
            return false;
        }

        if (PlayerWallet.Instance != null
            && !PlayerWallet.Instance.CanAfford(Economy.BuildCost(currentBuildingData)))
        {
            return false;
        }

        if (NeedsResourceNode(currentBuildingData))
        {
            Vector2Int minCell = GridFootprint.GetMinCell(position, size);
            if (!ResourceNode.HasNodeInArea(minCell, size))
                return false;
        }

        float cell = GridFootprint.CellSize;
        Vector3 half = GridFootprint.GetHalfExtents(size);
        Vector3 halfExtents = new Vector3(
            Mathf.Max(0.1f, half.x - 0.01f),
            0.9f,
            Mathf.Max(0.1f, half.z - 0.01f)
        );
        Vector3 areaCenter = position + Vector3.up * halfExtents.y;

        Collider[] ghostColliders = null;
        if (currentGhost != null)
        {
            ghostColliders = currentGhost.GetComponentsInChildren<Collider>();
            foreach (var col in ghostColliders)
                col.enabled = true;
        }

        LayerMask checkMask = collisionLayer;
        if (checkMask == 0)
        {
            int floorLayer = LayerMask.NameToLayer("Floor");
            checkMask = floorLayer != -1 ? buildLayer & ~(1 << floorLayer) : buildLayer;
        }

        Collider[] overlaps = Physics.OverlapBox(
            areaCenter,
            halfExtents,
            Quaternion.identity,
            checkMask
        );

        if (ghostColliders != null)
        {
            foreach (var col in ghostColliders)
                col.enabled = false;
        }

        foreach (Collider overlap in overlaps)
        {
            if (overlap == null) continue;
            if (IsStrokeGhost(overlap.transform))
                continue;
            if (IsNonBlockingCollider(overlap))
                continue;
            return false;
        }

        return true;
    }

    bool IsStrokeGhost(Transform t)
    {
        if (t == null)
            return false;
        if (currentGhost != null && t.IsChildOf(currentGhost.transform))
            return true;
        for (int i = 0; i < lineGhosts.Count; i++)
        {
            if (lineGhosts[i] != null && t.IsChildOf(lineGhosts[i].transform))
                return true;
        }
        return false;
    }

    Vector2Int GetPlacementSize(Quaternion rotation)
    {
        Vector2Int size = currentBuildingData != null ? currentBuildingData.size : Vector2Int.one;
        return GridFootprint.GetRotatedSize(size, rotation.eulerAngles.y);
    }

    static bool IsResearchLabData(BuildingData data)
    {
        return data != null && data.id == "research_lab";
    }

    public static bool IsExtractorData(BuildingData data)
    {
        return data != null && data.id == "extractor";
    }

    public static bool NeedsResourceNode(BuildingData data)
    {
        if (data == null)
            return false;
        return data.requiresResourceNode || IsExtractorData(data);
    }

    static bool IsNonBlockingCollider(Collider col)
    {
        if (col == null)
            return true;

        // Занятость клеток уже проверена через GridOccupancy.
        // Коллайдеры зданий/ленты не должны блокировать соседнюю клетку.
        if (col.GetComponentInParent<BuildingBase>() != null)
            return true;

        if (col.GetComponentInParent<ResourceNode>() != null)
            return true;

        if (col.name.IndexOf("BeltItem", System.StringComparison.OrdinalIgnoreCase) >= 0
            || (col.transform.parent != null
                && col.transform.parent.name.IndexOf("BeltItem", System.StringComparison.OrdinalIgnoreCase) >= 0))
            return true;

        int floorLayer = LayerMask.NameToLayer("Floor");
        if (floorLayer != -1 && col.gameObject.layer == floorLayer)
            return true;

        string n = col.gameObject.name;
        if (n.IndexOf("Floor", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Ground", System.StringComparison.OrdinalIgnoreCase) >= 0
            || n.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    void OnPlaceStarted(InputAction.CallbackContext ctx)
    {
        if (BlocksBuildInput)
            return;
        if (IsGameplayBuildInputBlocked() || !IsBuildModeActive || !IsAimingAtBuildSurface())
            return;
        if (currentBuildingData == null || currentBuildingData.prefab == null)
            return;
        if (ResearchSystem.Instance != null
            && !ResearchSystem.Instance.IsBuildingUnlocked(currentBuildingData))
        {
            Debug.LogWarning($"[Builder] Locked: {currentBuildingData.displayName}");
            return;
        }

        BeginStroke();
    }

    void OnPlaceCanceled(InputAction.CallbackContext ctx)
    {
        if (BlocksBuildInput)
            return;
        if (!strokeActive)
            return;

        if (IsGameplayBuildInputBlocked() || !IsBuildModeActive || !IsAimingAtBuildSurface())
        {
            EndStroke();
            return;
        }

        RebuildStrokeSlots();
        CommitStroke();
    }

    void BeginStroke()
    {
        Quaternion rot = Quaternion.Euler(0f, currentRotationY, 0f);
        strokeActive = true;
        strokeBuilding = currentBuildingData;
        strokeYaw = currentRotationY;
        strokeSize = GetPlacementSize(rot);
        strokeStartMin = GridFootprint.GetMinCell(CurrentPlacementPosition, strokeSize);
        strokeAxis = null;
        strokeSlots.Clear();
    }

    void EndStroke()
    {
        strokeActive = false;
        strokeBuilding = null;
        strokeAxis = null;
        strokeSlots.Clear();
        ClearLineGhosts();
        Conveyor.ClearWorldVisualOverrides();
        Conveyor.PreviewExits.Clear();
        if (currentGhost != null && isBuildMode)
            currentGhost.SetActive(HasPlacementTarget);
    }

    bool IsAimingAtBuildSurface()
    {
        if (playerCamera == null)
            return false;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, buildLayer))
            return false;

        CurrentPlacementPosition = SnapToGrid(hit.point);
        HasPlacementTarget = true;
        return true;
    }

    void TickLineStroke()
    {
        if (!strokeActive)
            return;

        if (IsGameplayBuildInputBlocked()
            || !IsBuildModeActive
            || currentBuildingData != strokeBuilding
            || !HasPlacementTarget)
        {
            EndStroke();
            return;
        }

        currentRotationY = strokeYaw;
        RebuildStrokeSlots();
        RefreshLineGhosts();
    }

    void RebuildStrokeSlots()
    {
        strokeSlots.Clear();
        if (!strokeActive)
            return;
        if (strokeBuilding != null && strokeBuilding.IsPairedStraight)
        {
            RebuildPairedSlots();
            return;
        }

        Vector2Int currentMin = GridFootprint.GetMinCell(CurrentPlacementPosition, strokeSize);
        Vector2Int delta = currentMin - strokeStartMin;

        if (!strokeAxis.HasValue
            && (Mathf.Abs(delta.x) >= strokeSize.x || Mathf.Abs(delta.y) >= strokeSize.y))
        {
            strokeAxis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2Int(1, 0)
                : new Vector2Int(0, 1);
        }

        int extra = 0;
        int dir = 1;
        int step = 1;
        if (strokeAxis.HasValue)
        {
            int axisDelta = strokeAxis.Value.x != 0 ? delta.x : delta.y;
            step = strokeAxis.Value.x != 0 ? strokeSize.x : strokeSize.y;
            dir = axisDelta >= 0 ? 1 : -1;
            extra = Mathf.Min(Mathf.Abs(axisDelta) / step, MaxLineBuildings - 1);
        }

        Quaternion rot = Quaternion.Euler(0f, strokeYaw, 0f);
        bool isLab = IsResearchLabData(strokeBuilding);
        int labCapacity = GetLabCapacity();
        int labUsed = 0;
        int pieceCost = Economy.BuildCost(strokeBuilding);
        int coinBudget = PlayerWallet.Instance != null && pieceCost > 0
            ? PlayerWallet.Instance.Coins / pieceCost
            : extra + 1;
        int paid = 0;

        for (int i = 0; i <= extra; i++)
        {
            Vector2Int min = strokeAxis.HasValue
                ? LineMinAt(i, dir, step)
                : strokeStartMin;
            Vector3 pos = GridFootprint.MinCellToCenter(min, strokeSize, CurrentPlacementPosition.y);
            bool valid = IsPlacementValid(pos, rot, checkLabLimit: false);
            if (valid && isLab)
            {
                valid = labUsed < labCapacity;
                if (valid)
                    labUsed++;
            }

            if (valid)
            {
                if (paid >= coinBudget)
                    valid = false;
                else
                    paid++;
            }

            strokeSlots.Add(new LineSlot { min = min, pos = pos, valid = valid, yaw = strokeYaw });
        }

        ApplySmartYawToLastSlot();

        if (strokeSlots.Count > 0)
        {
            CurrentPlacementPosition = strokeSlots[strokeSlots.Count - 1].pos;
            CurrentFootprintSize = strokeSize;
        }
    }

    int GetLabCapacity()
    {
        if (!IsResearchLabData(strokeBuilding) || ResearchSystem.Instance == null)
            return MaxLineBuildings;
        return Mathf.Max(0, ResearchSystem.Instance.GetMaxResearchLabs()
            - ResearchSystem.Instance.CountPlacedLabs());
    }

    void RebuildPairedSlots()
    {
        Vector2Int currentMin = GridFootprint.GetMinCell(CurrentPlacementPosition, strokeSize);
        Vector2Int delta = currentMin - strokeStartMin;

        if (!strokeAxis.HasValue
            && (Mathf.Abs(delta.x) >= strokeSize.x || Mathf.Abs(delta.y) >= strokeSize.y))
        {
            strokeAxis = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2Int(1, 0)
                : new Vector2Int(0, 1);
        }

        int extra = 0;
        int dir = 1;
        int step = 1;
        int maxExtra = Mathf.Max(1, strokeBuilding.pairMaxGap) + 1;
        if (strokeAxis.HasValue)
        {
            int axisDelta = strokeAxis.Value.x != 0 ? delta.x : delta.y;
            step = strokeAxis.Value.x != 0 ? strokeSize.x : strokeSize.y;
            dir = axisDelta >= 0 ? 1 : -1;
            extra = Mathf.Min(Mathf.Abs(axisDelta) / step, maxExtra);
        }

        Quaternion rot = Quaternion.Euler(0f, strokeYaw, 0f);
        Vector3 inPos = GridFootprint.MinCellToCenter(strokeStartMin, strokeSize, CurrentPlacementPosition.y);
        bool inOk = IsPlacementValid(inPos, rot, checkLabLimit: false);
        int cost = Economy.BuildCost(strokeBuilding);
        bool canPay = PlayerWallet.Instance == null || cost <= 0 || PlayerWallet.Instance.CanAfford(cost);
        float yaw = strokeYaw;
        strokeSlots.Add(new LineSlot { min = strokeStartMin, pos = inPos, valid = inOk && extra >= 1 && canPay, yaw = yaw });

        if (extra >= 1)
        {
            yaw = YawFromCellDir(strokeAxis.Value, dir);
            rot = Quaternion.Euler(0f, yaw, 0f);
            Vector2Int outMin = LineMinAt(extra, dir, step);
            Vector3 outPos = GridFootprint.MinCellToCenter(outMin, strokeSize, CurrentPlacementPosition.y);
            bool outOk = IsPlacementValid(outPos, rot, checkLabLimit: false);
            LineSlot entrance = strokeSlots[0];
            entrance.yaw = yaw;
            entrance.valid = inOk && outOk && canPay;
            strokeSlots[0] = entrance;
            strokeSlots.Add(new LineSlot { min = outMin, pos = outPos, valid = inOk && outOk && canPay, yaw = yaw });
            CurrentPlacementPosition = outPos;
        }

        CurrentFootprintSize = strokeSize;
    }

    static float YawFromCellDir(Vector2Int axis, int dir)
    {
        int x = axis.x * dir;
        int y = axis.y * dir;
        if (x > 0)
            return 90f;
        if (x < 0)
            return 270f;
        if (y < 0)
            return 180f;
        return 0f;
    }

    void ApplySmartYawToLastSlot()
    {
        if (strokeSlots.Count == 0 || strokeBuilding == null || !strokeBuilding.IsConveyor)
            return;

        int last = strokeSlots.Count - 1;
        LineSlot slot = strokeSlots[last];
        var strokeCells = new HashSet<Vector2Int>(strokeSlots.Count);
        for (int i = 0; i < strokeSlots.Count; i++)
            strokeCells.Add(BuildingLinker.WorldToCell(strokeSlots[i].pos));

        Vector2Int cell = BuildingLinker.WorldToCell(slot.pos);
        slot.yaw = BuildingLinker.MaybeSmartYaw(cell, strokeYaw, strokeCells);
        strokeSlots[last] = slot;
    }

    Vector2Int LineMinAt(int index, int dir, int step)
    {
        return strokeStartMin + new Vector2Int(
            strokeAxis.Value.x * index * step * dir,
            strokeAxis.Value.y * index * step * dir);
    }

    void RefreshLineGhosts()
    {
        if (currentGhost != null)
            currentGhost.SetActive(false);

        bool lineOk = strokeSlots.Count > 0;
        for (int i = 0; i < strokeSlots.Count; i++)
        {
            if (!strokeSlots[i].valid)
                lineOk = false;
        }

        while (lineGhosts.Count < strokeSlots.Count)
            lineGhosts.Add(CreateLineGhost(lineGhosts.Count));

        for (int i = lineGhosts.Count - 1; i >= strokeSlots.Count; i--)
        {
            if (lineGhosts[i] != null)
                Destroy(lineGhosts[i]);
            lineGhosts.RemoveAt(i);
        }

        Conveyor.PreviewExits.Clear();
        if (strokeBuilding != null && strokeBuilding.IsConveyor)
        {
            for (int i = 0; i < strokeSlots.Count; i++)
                Conveyor.RegisterPreviewExit(strokeSlots[i].pos, strokeSlots[i].yaw);
        }

        for (int i = 0; i < strokeSlots.Count; i++)
        {
            GameObject ghost = lineGhosts[i];
            if (ghost == null)
            {
                ghost = CreateLineGhost(i);
                lineGhosts[i] = ghost;
            }
            if (ghost == null)
                continue;

            Vector3 pos = strokeSlots[i].pos;
            Quaternion rot = Quaternion.Euler(0f, strokeSlots[i].yaw, 0f);
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(pos, rot);

            Conveyor belt = ghost.GetComponent<Conveyor>();
            if (belt != null)
                belt.Preview(pos, rot);

            TintGhost(ghost, lineOk);
        }

        if (strokeBuilding != null && strokeBuilding.IsConveyor)
            Conveyor.ApplyWorldVisualOverrides();
        else
            Conveyor.ClearWorldVisualOverrides();

        Conveyor.PreviewExits.Clear();
        canPlace = lineOk;
        CanPlaceCurrent = lineOk;
    }

    GameObject CreateLineGhost(int slotIndex = 0)
    {
        if (strokeBuilding == null)
            return null;

        bool exit = strokeBuilding.IsPairedStraight && slotIndex > 0;
        GameObject source = exit && strokeBuilding.pairExitPrefab != null
            ? strokeBuilding.pairExitPrefab
            : BuildingVisuals.SourceForGhost(strokeBuilding);
        if (source == null)
            return null;

        GameObject ghost = Instantiate(source);
        BuildingVisuals.PrepareGhostInstance(ghost);

        Conveyor belt = ghost.GetComponent<Conveyor>();
        if (belt == null && strokeBuilding.IsConveyor)
            belt = ghost.AddComponent<Conveyor>();
        if (belt != null)
            belt.PreparePreview(strokeBuilding);

        return ghost;
    }

    void CommitStroke()
    {
        if (!strokeActive)
            return;

        bool lineOk = strokeSlots.Count > 0;
        for (int i = 0; i < strokeSlots.Count; i++)
        {
            if (!strokeSlots[i].valid)
            {
                lineOk = false;
                break;
            }
        }

        if (!lineOk || (strokeBuilding.IsPairedStraight && strokeSlots.Count != 2))
        {
            if (playerCamera != null)
                GameAudio.World("world_invalid", playerCamera.transform.position);
            EndStroke();
            return;
        }

        int pieces = strokeBuilding.IsPairedStraight ? 1 : strokeSlots.Count;
        int lineCost = Economy.BuildCost(currentBuildingData) * pieces;
        if (PlayerWallet.Instance != null && !PlayerWallet.Instance.CanAfford(lineCost))
        {
            EndStroke();
            return;
        }

        Vector3 soundPos = strokeSlots[strokeSlots.Count - 1].pos;
        if (strokeBuilding.IsPairedStraight)
            SpawnPaired(strokeSlots[0], strokeSlots[1]);
        else
        {
            for (int i = 0; i < strokeSlots.Count; i++)
            {
                Quaternion rot = Quaternion.Euler(0f, strokeSlots[i].yaw, 0f);
                SpawnAt(strokeSlots[i].pos, rot);
            }
        }

        if (currentBuildingData != null)
        {
            if (currentBuildingData.IsConveyor || currentBuildingData.IsPairedStraight)
                GameAudio.World("world_place_belt", soundPos);
            else
                GameAudio.World("world_place", soundPos);
        }

        EndStroke();
    }

    void SpawnPaired(LineSlot entrance, LineSlot exit)
    {
        if (currentBuildingData == null || currentBuildingData.prefab == null || currentBuildingData.pairExitPrefab == null)
            return;

        int cost = Economy.BuildCost(currentBuildingData);
        if (PlayerWallet.Instance != null && !PlayerWallet.Instance.TrySpendCoins(cost))
        {
            GameAudio.World("world_invalid", entrance.pos);
            return;
        }

        Quaternion rot = Quaternion.Euler(0f, entrance.yaw, 0f);
        GameObject inGo = Instantiate(currentBuildingData.prefab, entrance.pos, rot);
        GameObject outGo = Instantiate(currentBuildingData.pairExitPrefab, exit.pos, rot);
        inGo.name = inGo.name + indexBuilding;
        indexBuilding += 1;
        outGo.name = outGo.name + indexBuilding;
        indexBuilding += 1;

        UndergroundConveyor entranceBelt = inGo.GetComponent<UndergroundConveyor>();
        UndergroundConveyor exitBelt = outGo.GetComponent<UndergroundConveyor>();
        if (entranceBelt == null)
            entranceBelt = inGo.AddComponent<UndergroundConveyor>();
        if (exitBelt == null)
            exitBelt = outGo.AddComponent<UndergroundConveyor>();

        entranceBelt.data = currentBuildingData;
        exitBelt.data = currentBuildingData;
        UndergroundConveyor.BindPair(entranceBelt, exitBelt);
        entranceBelt.OnPlaced();
        exitBelt.OnPlaced();
    }

    void SpawnAt(Vector3 placePos, Quaternion placeRot)
    {
        if (currentBuildingData == null || currentBuildingData.prefab == null)
            return;

        int cost = Economy.BuildCost(currentBuildingData);
        if (PlayerWallet.Instance != null && !PlayerWallet.Instance.TrySpendCoins(cost))
        {
            GameAudio.World("world_invalid", placePos);
            return;
        }

        GameObject go = Instantiate(currentBuildingData.prefab, placePos, placeRot);
        go.name = go.name + $"{indexBuilding}";
        indexBuilding += 1;

        BuildingBase buildingBase = go.GetComponent<BuildingBase>();
        if (buildingBase != null)
        {
            buildingBase.data = currentBuildingData;
            buildingBase.OnPlaced();
        }
        else
        {
            RegisterGenericOnGrid(go);
        }
    }

    void RegisterGenericOnGrid(GameObject obj)
    {
        if (obj == null || GridSystem.Instance == null)
            return;

        Vector2Int size = GetPlacementSize(obj.transform.rotation);
        GridFootprint.Register(obj, obj.transform.position, size);
    }

    void TryDemolish()
    {
        if (playerCamera == null)
            return;

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, maxBuildDistance, demolishLayer))
            return;

        BuildingBase building = hit.collider.GetComponentInParent<BuildingBase>();
        if (building == null)
            return;

        GameAudio.World("world_demolish", building.transform.position);
        Economy.PayRefund(building);
        building.OnRemoved();
        Destroy(building.gameObject);
    }
}
