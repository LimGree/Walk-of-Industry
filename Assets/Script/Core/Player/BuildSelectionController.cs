using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class BuildSelectionController : MonoBehaviour
{
    public bool BlocksBuildInput =>
        selectionMode || pasteActive || moveActive;

    public bool IsSelectionMode => selectionMode;
    public bool IsPasteActive => pasteActive;
    public bool IsMoveActive => moveActive;
    public bool HasClipboard => clipboard.Count > 0;
    public bool HasSelectedBuildings => selectedBuildings.Count > 0;

    public int CountClipboard(string buildingId)
    {
        int n = 0;
        for (int i = 0; i < clipboard.Count; i++)
        {
            if (clipboard[i].data != null && TutorialSystem.IdsEqual(clipboard[i].data.id, buildingId))
                n++;
        }
        return n;
    }

    public IReadOnlyList<BuildingBase> SelectedBuildings
    {
        get
        {
            selectedBuildings.RemoveAll(b => b == null);
            return selectedBuildings;
        }
    }

    PlayerBuilder builder;
    PlayerInventory inventory;
    InputSystem_Actions input;
    InputAction undoAction;
    InputAction redoAction;
    int lastHistoryFrame = -1;

    bool selectionMode;
    bool boxSelecting;
    Vector2Int boxStart;
    Vector2Int lastBoxEnd;
    readonly HashSet<Vector2Int> selectedCells = new HashSet<Vector2Int>();
    readonly List<BuildingBase> selectedBuildings = new List<BuildingBase>();

    readonly List<ClipItem> clipboard = new List<ClipItem>();
    Vector2Int clipOrigin;

    bool pasteActive;
    bool moveActive;
    Vector2Int previewAnchor;
    Vector2Int groupOrigin;
    Vector2Int grabCell;
    readonly List<PreviewItem> preview = new List<PreviewItem>();
    readonly object reserveToken = new object();

    readonly List<MoveRecord> moveRecords = new List<MoveRecord>();
    readonly HashSet<GameObject> moveIgnore = new HashSet<GameObject>();

    readonly List<Transform> selectHighlights = new List<Transform>();
    Transform highlightRoot;
    Material selectMat;
    Material boxMat;
    const int HighlightPoolKeep = 16;

    struct ClipItem
    {
        public BuildingData data;
        public Vector2Int minOffset;
        public float yaw;
        public int level;
        public RecipeData recipe;
        public ItemData filter;
        public bool pairExit;
        public int pairId;
    }

    struct PreviewItem
    {
        public BuildingData data;
        public Vector2Int minOffset;
        public float yaw;
        public int level;
        public RecipeData recipe;
        public ItemData filter;
        public bool pairExit;
        public int pairId;
        public GameObject ghost;
        public bool valid;
    }

    struct MoveRecord
    {
        public BuildingBase building;
        public Vector3 pos;
        public float yaw;
        public bool[] rendererEnabled;
        public Renderer[] renderers;
    }

    void Awake()
    {
        builder = GetComponent<PlayerBuilder>();
        inventory = GetComponent<PlayerInventory>();
        if (inventory == null)
            inventory = FindFirstObjectByType<PlayerInventory>();
        input = KeybindStore.Shared;
    }

    void OnEnable()
    {
        input.Player.SelectMode.performed += OnSelectMode;
        input.Player.ClearSelection.performed += OnClearSelection;
        input.Player.Copy.performed += OnCopy;
        input.Player.Paste.performed += OnPaste;
        input.Player.MoveSelection.performed += OnMove;
        input.Player.Delete.performed += OnDelete;
        input.Player.Place.started += OnPlaceStarted;
        input.Player.Place.canceled += OnPlaceCanceled;
        input.Player.Rotate.performed += OnRotate;
        BindUndoAction();
    }

    void OnDisable()
    {
        input.Player.SelectMode.performed -= OnSelectMode;
        input.Player.ClearSelection.performed -= OnClearSelection;
        input.Player.Copy.performed -= OnCopy;
        input.Player.Paste.performed -= OnPaste;
        input.Player.MoveSelection.performed -= OnMove;
        input.Player.Delete.performed -= OnDelete;
        input.Player.Place.started -= OnPlaceStarted;
        input.Player.Place.canceled -= OnPlaceCanceled;
        input.Player.Rotate.performed -= OnRotate;
        if (undoAction != null)
            undoAction.performed -= OnUndo;
        if (redoAction != null)
            redoAction.performed -= OnRedo;
        CancelPreview();
    }

    void BindUndoAction()
    {
        if (undoAction == null)
        {
            undoAction = input != null ? input.asset.FindAction("Player/Undo", false) : null;
            if (undoAction != null)
                undoAction.performed += OnUndo;
        }
        if (redoAction == null)
        {
            redoAction = input != null ? input.asset.FindAction("Player/Redo", false) : null;
            if (redoAction != null)
                redoAction.performed += OnRedo;
        }
    }

    void Update()
    {
        TickDeleteHold();
        if (builder == null || !builder.isBuildMode)
        {
            if (!boxSelecting && (selectionMode || pasteActive || moveActive))
                ExitAll();
            return;
        }

        PollBoxSelect();
        PollUndoHotkey();
        PollClearHotkey();

        if (UiModal.IsOpen)
            return;

        if (IsSelectionPanelOpen())
        {
            RefreshSelectedBuildings();
            RefreshSelectionVisuals();
            return;
        }

        if (IsBlocked())
        {
            if (boxSelecting)
            {
                RefreshSelectedBuildings();
                RefreshSelectionVisuals();
                return;
            }

            if (pasteActive || moveActive)
                return;
            RefreshSelectedBuildings();
            RefreshSelectionVisuals();
            return;
        }

        if (pasteActive || moveActive)
        {
            if (!builder.TryGetAimCell(out _, out _))
                return;
            TickPreview();
        }

        RefreshSelectedBuildings();
        RefreshSelectionVisuals();
    }

    void PollBoxSelect()
    {
        if (!boxSelecting)
            return;
        if (builder != null && builder.TryGetAimCell(out Vector2Int cell, out _))
            lastBoxEnd = cell;
        if (PlaceHeld())
            return;
        FinishBoxSelect();
    }

    void PollUndoHotkey()
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;
        bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
        if (!ctrl)
            return;
        if (kb.yKey.wasPressedThisFrame)
        {
            TryRedo();
            return;
        }
        if (undoAction == null && kb.zKey.wasPressedThisFrame
            && !kb.leftShiftKey.isPressed && !kb.rightShiftKey.isPressed)
            TryUndo();
    }

    void PollClearHotkey()
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        Keyboard kb = Keyboard.current;
        if (kb == null)
            return;
        if ((kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) && kb.dKey.wasPressedThisFrame)
            ClearSelectionKeepMode();
    }

    bool PlaceHeld()
    {
        if (input != null && input.Player.Place.IsPressed())
            return true;
        Mouse mouse = Mouse.current;
        return mouse != null && mouse.rightButton.isPressed;
    }

    void FinishBoxSelect()
    {
        if (!boxSelecting)
            return;
        boxSelecting = false;
        AddBoxToSelection(boxStart, lastBoxEnd);
    }

    bool IsBlocked()
    {
        return UiStack.GameplayBlocked;
    }

    static bool IsSelectionPanelOpen()
    {
        return SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen;
    }

    bool ModifierHeld()
    {
        return input.Player.Modifier.IsPressed();
    }

    void OnSelectMode(InputAction.CallbackContext ctx)
    {
        if (builder == null || !builder.isBuildMode || IsBlocked())
            return;
        if (boxSelecting)
            return;
        if (inventory == null)
            return;

        inventory.SelectEmptyTool();
        if (selectionMode)
            ExitAll();
        else
            selectionMode = true;
    }

    void OnClearSelection(InputAction.CallbackContext ctx)
    {
        ClearSelectionKeepMode();
    }

    void ClearSelectionKeepMode()
    {
        if (builder == null || !builder.isBuildMode)
            return;
        if (KeybindStore.BlocksGameplayInput)
            return;
        boxSelecting = false;
        if (pasteActive || moveActive)
            CancelPreview();
        ClearSelectionOnly();
    }

    void OnUndo(InputAction.CallbackContext ctx)
    {
        if (ShiftHeld())
            return;
        TryUndo();
    }

    void OnRedo(InputAction.CallbackContext ctx)
    {
        TryRedo();
    }

    static bool ShiftHeld()
    {
        Keyboard kb = Keyboard.current;
        return kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
    }

    void TryUndo()
    {
        ApplyHistory(undo: true);
    }

    void TryRedo()
    {
        ApplyHistory(undo: false);
    }

    void ApplyHistory(bool undo)
    {
        if (builder == null || !builder.isBuildMode)
            return;
        if (KeybindStore.BlocksGameplayInput || UiModal.IsOpen)
            return;
        if (boxSelecting)
            return;
        if (pasteActive || moveActive)
            CancelPreview();
        if (lastHistoryFrame == Time.frameCount)
            return;
        bool ok = undo ? BuildUndo.Undo() : BuildUndo.Redo();
        if (!ok)
            return;
        lastHistoryFrame = Time.frameCount;
        ClearSelectionOnly();
        GameAudio.World("world_copy", builder.transform.position);
    }

    void OnDelete(InputAction.CallbackContext ctx)
    {
        if (!selectionMode || IsBlocked() || pasteActive || moveActive || boxSelecting)
            return;

        RefreshSelectedBuildings();
        if (selectedBuildings.Count == 0)
            return;

        // «Подтверждать массовое удаление»: много зданий — удерживай клавишу.
        int threshold = GameSettings.ConfirmMassDelete;
        if (threshold > 0 && selectedBuildings.Count >= threshold)
        {
            deleteHoldStart = Time.unscaledTime;
            deleteHolding = true;
            return;
        }

        DeleteSelectedNow();
    }

    bool deleteHolding;
    float deleteHoldStart;

    void TickDeleteHold()
    {
        if (!deleteHolding)
            return;
        bool held = input != null && input.Player.Delete.IsPressed();
        if (!held || !selectionMode || IsBlocked())
        {
            deleteHolding = false;
            HoldPrompt.Hide();
            return;
        }
        float t = (Time.unscaledTime - deleteHoldStart) / GameSettings.HoldTime;
        HoldPrompt.Show(UiLocale.T("hold.delete", selectedBuildings.Count), t);
        if (t < 1f)
            return;
        deleteHolding = false;
        HoldPrompt.Hide();
        DeleteSelectedNow();
    }

    void DeleteSelectedNow()
    {
        RefreshSelectedBuildings();
        if (selectedBuildings.Count == 0)
            return;
        BuildUndo.Begin();
        var skip = new HashSet<int>();
        for (int i = 0; i < selectedBuildings.Count; i++)
        {
            BuildingBase b = selectedBuildings[i];
            if (b == null)
                continue;
            int id = b.GetInstanceID();
            if (skip.Contains(id))
                continue;
            UndergroundConveyor tunnel = b as UndergroundConveyor;
            if (tunnel != null && tunnel.Paired != null)
                skip.Add(tunnel.Paired.GetInstanceID());
            BuildUndo.NoteRemoved(b);
            Economy.PayRefund(b);
            b.OnRemoved();
            Destroy(b.gameObject);
        }
        BuildUndo.End();

        ClearSelectionOnly();
    }

    void ClearSelectionOnly()
    {
        selectedCells.Clear();
        selectedBuildings.Clear();
        boxSelecting = false;
        TrimHighlights();
    }

    public bool TrySnapshotSelection(List<BlueprintBuilding> dest)
    {
        if (dest == null)
            return false;
        dest.Clear();
        RefreshSelectedBuildings();
        ExpandUndergroundPairs();
        if (selectedBuildings.Count == 0)
            return false;

        Vector2Int origin = SelectionOrigin();
        for (int i = 0; i < selectedBuildings.Count; i++)
        {
            BuildingBase b = selectedBuildings[i];
            if (b == null || b.data == null || string.IsNullOrEmpty(b.data.id))
                continue;
            Vector2Int min = GridFootprint.GetMinCell(b.transform.position, b.FootprintSize);
            UndergroundConveyor tunnel = b as UndergroundConveyor;
            RecipeData recipe = ReadRecipe(b);
            ItemData filter = ReadFilter(b);
            dest.Add(new BlueprintBuilding
            {
                buildingId = b.data.id,
                ox = min.x - origin.x,
                oy = min.y - origin.y,
                yaw = b.transform.eulerAngles.y,
                level = b.ReadLevel(),
                recipeId = recipe != null ? recipe.id : "",
                filterItemId = filter != null ? filter.id : "",
                pairExit = tunnel != null && tunnel.isExit,
                pairId = tunnel != null ? tunnel.PairId : 0
            });
        }

        return dest.Count > 0;
    }

    public bool LoadBlueprint(IList<BlueprintBuilding> buildings)
    {
        if (pasteActive || moveActive)
            CancelPreview();
        clipboard.Clear();
        clipOrigin = Vector2Int.zero;
        if (buildings == null)
            return false;

        for (int i = 0; i < buildings.Count; i++)
        {
            BlueprintBuilding piece = buildings[i];
            if (piece == null || string.IsNullOrEmpty(piece.buildingId))
                continue;
            BuildingData data = GameDatabase.FindBuilding(piece.buildingId);
            if (data == null)
                continue;
            clipboard.Add(new ClipItem
            {
                data = data,
                minOffset = new Vector2Int(piece.ox, piece.oy),
                yaw = piece.yaw,
                level = piece.level,
                recipe = GameDatabase.FindRecipe(piece.recipeId),
                filter = GameDatabase.FindItem(piece.filterItemId),
                pairExit = piece.pairExit,
                pairId = piece.pairId
            });
        }

        return clipboard.Count > 0;
    }

    Vector2Int SelectionOrigin()
    {
        Vector2Int origin = new Vector2Int(int.MaxValue, int.MaxValue);
        for (int i = 0; i < selectedBuildings.Count; i++)
        {
            Vector2Int min = GridFootprint.GetMinCell(
                selectedBuildings[i].transform.position,
                selectedBuildings[i].FootprintSize);
            origin.x = Mathf.Min(origin.x, min.x);
            origin.y = Mathf.Min(origin.y, min.y);
        }

        return origin;
    }

    void OnCopy(InputAction.CallbackContext ctx)
    {
        if (!selectionMode || IsBlocked())
            return;
        if (BlueprintLibraryUI.Instance != null && BlueprintLibraryUI.Instance.IsOpen)
            return;
        RefreshSelectedBuildings();
        ExpandUndergroundPairs();
        if (selectedBuildings.Count == 0)
            return;

        clipboard.Clear();
        Vector2Int origin = SelectionOrigin();
        clipOrigin = origin;
        for (int i = 0; i < selectedBuildings.Count; i++)
        {
            BuildingBase b = selectedBuildings[i];
            if (b.data == null)
                continue;
            Vector2Int min = GridFootprint.GetMinCell(b.transform.position, b.FootprintSize);
            UndergroundConveyor tunnel = b as UndergroundConveyor;
            clipboard.Add(new ClipItem
            {
                data = b.data,
                minOffset = min - origin,
                yaw = b.transform.eulerAngles.y,
                level = b.ReadLevel(),
                recipe = ReadRecipe(b),
                filter = ReadFilter(b),
                pairExit = tunnel != null && tunnel.isExit,
                pairId = tunnel != null ? tunnel.PairId : 0
            });
        }

        ClearSelectionOnly();
        if (builder != null)
            GameAudio.World("world_copy", builder.transform.position);
    }

    void OnPaste(InputAction.CallbackContext ctx)
    {
        if (BlueprintLibraryUI.Instance != null && BlueprintLibraryUI.Instance.IsOpen)
            return;
        if (builder == null || !builder.isBuildMode || IsBlocked())
            return;
        if (clipboard.Count == 0 || !builder.TryGetAimCell(out Vector2Int cell, out _))
            return;

        CancelPreview();
        ClearSelectionOnly();
        pasteActive = true;
        grabCell = cell;
        groupOrigin = clipOrigin;
        previewAnchor = cell;
        BuildPreviewFromClipboard();
        TickPreview();
        GameAudio.World("world_paste", builder.transform.position);
    }

    void OnMove(InputAction.CallbackContext ctx)
    {
        if (!selectionMode || IsBlocked())
            return;
        RefreshSelectedBuildings();
        if (selectedBuildings.Count == 0 || !builder.TryGetAimCell(out Vector2Int cell, out _))
            return;

        CancelPreview();
        moveActive = true;
        grabCell = cell;
        BeginMove();
        TickPreview();
    }

    void OnPlaceStarted(InputAction.CallbackContext ctx)
    {
        if (IsBlocked() || builder == null || !builder.isBuildMode)
            return;

        if (pasteActive || moveActive)
            return;

        if (!selectionMode)
            return;

        if (CanBulkPlaceExtractors())
            return;

        if (!builder.TryGetAimCell(out Vector2Int cell, out _))
            return;

        boxSelecting = true;
        boxStart = cell;
        lastBoxEnd = cell;
    }

    void OnPlaceCanceled(InputAction.CallbackContext ctx)
    {
        if (pasteActive || moveActive)
        {
            if (!builder.TryGetAimCell(out _, out _))
                return;
            TickPreview();
            if (PreviewAllValid())
                CommitPreview();
            return;
        }

        if (selectionMode && CanBulkPlaceExtractors() && !boxSelecting)
        {
            PromptBulkExtractors();
            return;
        }

        if (!selectionMode || !boxSelecting)
            return;
        if (PlaceHeld())
            return;
        FinishBoxSelect();
    }

    void OnRotate(InputAction.CallbackContext ctx)
    {
        if (IsBlocked() || builder == null || !builder.isBuildMode)
            return;

        bool inPlace = ModifierHeld();
        if (pasteActive || moveActive)
        {
            RotatePreview(inPlace);
            TickPreview();
            return;
        }

        if (!selectionMode)
            return;

        RefreshSelectedBuildings();
        if (selectedBuildings.Count == 0)
            return;

        if (inPlace)
            RotateSelectionInPlace();
        else
            RotateSelectionAroundCenter();
    }

    void AddBoxToSelection(Vector2Int a, Vector2Int b)
    {
        int minX = Mathf.Min(a.x, b.x);
        int maxX = Mathf.Max(a.x, b.x);
        int minZ = Mathf.Min(a.y, b.y);
        int maxZ = Mathf.Max(a.y, b.y);
        var seen = new HashSet<int>();

        for (int x = minX; x <= maxX; x++)
        {
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);
                selectedCells.Add(cell);
                BuildingBase building = BuildingLinker.GetBuildingAt(cell);
                if (building == null)
                    building = DecorSystem.FloorAt(cell);
                if (building == null || !seen.Add(building.GetInstanceID()))
                    continue;

                List<Vector2Int> cells = new List<Vector2Int>();
                GridFootprint.CollectCells(building.transform.position, building.FootprintSize, cells);
                for (int i = 0; i < cells.Count; i++)
                    selectedCells.Add(cells[i]);
            }
        }
    }

    void ExpandUndergroundPairs()
    {
        int n = selectedBuildings.Count;
        for (int i = 0; i < n; i++)
        {
            UndergroundConveyor tunnel = selectedBuildings[i] as UndergroundConveyor;
            if (tunnel == null || tunnel.Paired == null)
                continue;
            if (selectedBuildings.Contains(tunnel.Paired))
                continue;
            selectedBuildings.Add(tunnel.Paired);
            List<Vector2Int> cells = new List<Vector2Int>(4);
            GridFootprint.CollectCells(tunnel.Paired.transform.position, tunnel.Paired.FootprintSize, cells);
            for (int c = 0; c < cells.Count; c++)
                selectedCells.Add(cells[c]);
        }
    }

    static void BindPastedTunnels(List<BuildingBase> spawned)
    {
        if (spawned == null)
            return;
        var byPair = new Dictionary<int, UndergroundConveyor>();
        for (int i = 0; i < spawned.Count; i++)
        {
            UndergroundConveyor tunnel = spawned[i] as UndergroundConveyor;
            if (tunnel == null || tunnel.PairId <= 0)
                continue;
            if (byPair.TryGetValue(tunnel.PairId, out UndergroundConveyor other) && other != null)
            {
                UndergroundConveyor entrance = tunnel.isExit ? other : tunnel;
                UndergroundConveyor exit = tunnel.isExit ? tunnel : other;
                UndergroundConveyor.BindPair(entrance, exit);
                byPair.Remove(tunnel.PairId);
            }
            else
                byPair[tunnel.PairId] = tunnel;
        }
    }

    void RefreshSelectedBuildings()
    {
        selectedBuildings.Clear();
        var seen = new HashSet<int>();
        foreach (Vector2Int cell in selectedCells)
        {
            BuildingBase b = BuildingLinker.GetBuildingAt(cell);
            if (b != null && seen.Add(b.GetInstanceID()))
                selectedBuildings.Add(b);
            // напольная декорация под зданием — отдельно ([[DecorSystem]])
            Decoration floor = DecorSystem.FloorAt(cell);
            if (floor != null && seen.Add(floor.GetInstanceID()))
                selectedBuildings.Add(floor);
        }
    }

    BuildingData HeldExtractor()
    {
        if (inventory == null)
            return null;
        BuildingData data = inventory.GetSelectedBuilding();
        if (!PlayerBuilder.IsExtractorData(data))
            return null;
        if (ResearchSystem.Instance != null && !ResearchSystem.Instance.IsBuildingUnlocked(data))
            return null;
        return data;
    }

    bool CanBulkPlaceExtractors()
    {
        return selectionMode && HeldExtractor() != null && CountFreeSelectedVeins() > 0;
    }

    int CountFreeSelectedVeins()
    {
        int n = 0;
        foreach (Vector2Int cell in selectedCells)
        {
            if (IsFreeVeinCell(cell))
                n++;
        }

        return n;
    }

    static bool IsFreeVeinCell(Vector2Int cell)
    {
        if (!ResourceNode.HasNode(cell))
            return false;
        return BuildingLinker.GetBuildingAt(cell) == null;
    }

    Vector3 ExtractorWorldPos(Vector2Int cell)
    {
        float y = 0f;
        if (builder != null && builder.HasPlacementTarget)
            y = builder.CurrentPlacementPosition.y;
        else if (GridSystem.Instance != null)
            y = GridSystem.Instance.origin.y;
        return GridFootprint.MinCellToCenter(cell, Vector2Int.one, y);
    }

    void PromptBulkExtractors()
    {
        BuildingData data = HeldExtractor();
        int count = CountFreeSelectedVeins();
        if (data == null || count <= 0)
        {
            if (builder != null)
                GameAudio.World("world_invalid", builder.transform.position);
            return;
        }

        int cost = Economy.BuildCost(data) * count;
        UiModal.Confirm(
            UiLocale.T("select.extractors_title"),
            UiLocale.T("select.extractors_body", count, cost),
            UiLocale.T("select.extractors_ok"),
            PlaceExtractorsOnSelectedVeins,
            danger: false);
    }

    void PlaceExtractorsOnSelectedVeins()
    {
        BuildingData data = HeldExtractor();
        if (data == null || builder == null)
            return;

        var cells = new List<Vector2Int>();
        foreach (Vector2Int cell in selectedCells)
        {
            if (IsFreeVeinCell(cell))
                cells.Add(cell);
        }

        if (cells.Count == 0)
            return;

        float yaw = builder.PlacementYaw;
        int placed = 0;
        Vector3 sound = builder.transform.position;
        BuildUndo.Begin();
        for (int i = 0; i < cells.Count; i++)
        {
            Vector3 pos = ExtractorWorldPos(cells[i]);
            if (!builder.TrySpawnBuilding(data, pos, yaw))
                break;
            placed++;
            sound = pos;
        }
        BuildUndo.End();

        if (placed > 0)
            GameAudio.World("world_place", sound);
        else
            GameAudio.World("world_invalid", builder.transform.position);
        RefreshSelectedBuildings();
        RefreshSelectionVisuals();
    }

    void BuildPreviewFromClipboard()
    {
        preview.Clear();
        for (int i = 0; i < clipboard.Count; i++)
        {
            ClipItem c = clipboard[i];
            preview.Add(new PreviewItem
            {
                data = c.data,
                minOffset = c.minOffset,
                yaw = c.yaw,
                level = c.level,
                recipe = c.recipe,
                filter = c.filter,
                pairExit = c.pairExit,
                pairId = c.pairId,
                ghost = CreateGhost(c.data, c.pairExit)
            });
        }
    }

    void BeginMove()
    {
        ExpandUndergroundPairs();
        preview.Clear();
        moveRecords.Clear();
        moveIgnore.Clear();

        Vector2Int origin = new Vector2Int(int.MaxValue, int.MaxValue);
        for (int i = 0; i < selectedBuildings.Count; i++)
        {
            Vector2Int min = GridFootprint.GetMinCell(
                selectedBuildings[i].transform.position,
                selectedBuildings[i].FootprintSize);
            origin.x = Mathf.Min(origin.x, min.x);
            origin.y = Mathf.Min(origin.y, min.y);
        }

        for (int i = 0; i < selectedBuildings.Count; i++)
        {
            BuildingBase b = selectedBuildings[i];
            moveIgnore.Add(b.gameObject);
            Renderer[] rends = b.GetComponentsInChildren<Renderer>(true);
            bool[] enabled = new bool[rends.Length];
            for (int r = 0; r < rends.Length; r++)
            {
                enabled[r] = rends[r].enabled;
                rends[r].enabled = false;
            }

            moveRecords.Add(new MoveRecord
            {
                building = b,
                pos = b.transform.position,
                yaw = b.transform.eulerAngles.y,
                renderers = rends,
                rendererEnabled = enabled
            });

            GridOccupancy.Unregister(b.gameObject);

            Vector2Int min = GridFootprint.GetMinCell(b.transform.position, b.FootprintSize);
            preview.Add(new PreviewItem
            {
                data = b.data,
                minOffset = min - origin,
                yaw = b.transform.eulerAngles.y,
                level = b.ReadLevel(),
                recipe = ReadRecipe(b),
                filter = ReadFilter(b),
                ghost = CreateGhost(b.data)
            });
        }

        groupOrigin = origin;
        previewAnchor = origin;
    }

    Vector2Int PreviewOrigin(Vector2Int aim)
    {
        if (pasteActive)
            return aim;
        return groupOrigin + (aim - grabCell);
    }

    void TickPreview()
    {
        if (!builder.TryGetAimCell(out Vector2Int aim, out Vector3 world))
            return;

        Vector2Int origin = PreviewOrigin(aim);
        float y = world.y;
        var cells = new List<Vector2Int>(32);
        bool allValid = true;
        int labUsed = 0;
        int labCap = LabCapacity();

        Conveyor.PreviewExits.Clear();
        for (int i = 0; i < preview.Count; i++)
        {
            PreviewItem feed = preview[i];
            if (feed.data == null || !feed.data.IsConveyor)
                continue;
            Vector2Int feedSize = GridFootprint.GetRotatedSize(feed.data.size, feed.yaw);
            Vector3 feedPos = GridFootprint.MinCellToCenter(origin + feed.minOffset, feedSize, y);
            Conveyor.RegisterPreviewExit(feedPos, feed.yaw);
        }

        for (int i = 0; i < preview.Count; i++)
        {
            PreviewItem item = preview[i];
            if (item.data == null)
                continue;

            Vector2Int size = GridFootprint.GetRotatedSize(item.data.size, item.yaw);
            Vector2Int min = origin + item.minOffset;
            Vector3 pos = GridFootprint.MinCellToCenter(min, size, y);
            bool valid = DecorSystem.IsAreaFreeFor(item.data, min, size, reserveToken, moveActive ? moveIgnore : null)
                && WorldBiomeMap.CanBuild(min, size, item.data.allowOnWater, item.data.requiresWater);
            if (valid && pasteActive && item.data.id == "research_lab")
            {
                valid = labUsed < labCap;
                if (valid)
                    labUsed++;
            }
            if (valid && PlayerBuilder.NeedsResourceNode(item.data)
                && !ResourceNode.HasNodeInArea(min, size))
                valid = false;
            // Чертёж из другого мира не вставит некупленную декорацию.
            if (valid && pasteActive && DecorCatalog.IsDecor(item.data) && !DecorSystem.IsOwned(item.data))
                valid = false;

            item.valid = valid;
            preview[i] = item;
            if (!valid)
                allValid = false;

            PlaceGhost(item.ghost, pos, item.yaw, valid);

            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.y; z++)
                    cells.Add(min + new Vector2Int(x, z));
            }
        }

        GridOccupancy.Reserve(reserveToken, cells);
        Conveyor.ApplyWorldVisualOverrides();
        TintPreview(allValid);
        Conveyor.PreviewExits.Clear();
    }

    bool PreviewAllValid()
    {
        if (preview.Count == 0)
            return false;
        for (int i = 0; i < preview.Count; i++)
        {
            if (!preview[i].valid)
                return false;
        }
        return true;
    }

    void CommitPreview()
    {
        if (!PreviewAllValid())
            return;

        builder.TryGetAimCell(out Vector2Int aim, out Vector3 world);
        Vector2Int origin = PreviewOrigin(aim);
        float y = world.y;

        if (moveActive)
        {
            selectedCells.Clear();
            BuildUndo.Begin();
            for (int i = 0; i < preview.Count && i < moveRecords.Count; i++)
            {
                PreviewItem item = preview[i];
                MoveRecord rec = moveRecords[i];
                if (rec.building == null)
                    continue;
                Vector2Int size = GridFootprint.GetRotatedSize(item.data.size, item.yaw);
                Vector2Int min = origin + item.minOffset;
                Vector3 oldPos = rec.pos;
                float oldYaw = rec.yaw;
                rec.building.transform.SetPositionAndRotation(
                    GridFootprint.MinCellToCenter(min, size, rec.pos.y),
                    Quaternion.Euler(0f, item.yaw, 0f));
                RestoreRenderers(rec);
                rec.building.ReRegisterOnGrid();
                rec.building.OnRotated();
                BuildUndo.NoteEdit(rec.building, oldPos, oldYaw);
                for (int x = 0; x < size.x; x++)
                {
                    for (int z = 0; z < size.y; z++)
                        selectedCells.Add(min + new Vector2Int(x, z));
                }
            }
            BuildUndo.End();
            moveActive = false;
            moveRecords.Clear();
            CancelPreview(keepSelection: true);
            return;
        }

        var spawned = new List<BuildingBase>(preview.Count);
        BuildUndo.Begin();
        for (int i = 0; i < preview.Count; i++)
        {
            PreviewItem item = preview[i];
            if (item.data == null || item.data.prefab == null)
                continue;
            Vector2Int size = GridFootprint.GetRotatedSize(item.data.size, item.yaw);
            Vector2Int min = origin + item.minOffset;
            Vector3 pos = GridFootprint.MinCellToCenter(min, size, y);
            GameObject prefab = UndergroundConveyor.PrefabFor(item.data, item.pairExit);
            if (prefab == null)
                continue;
            GameObject go = Instantiate(prefab, pos, Quaternion.Euler(0f, item.yaw, 0f));
            BuildingBase b = go.GetComponent<BuildingBase>();
            if (b != null)
            {
                b.data = item.data;
                UndergroundConveyor tunnel = b as UndergroundConveyor;
                if (tunnel != null)
                    tunnel.SetPairMeta(item.pairExit, item.pairId);
                b.OnPlaced();
                b.ApplyLevel(item.level);
                ApplyRecipe(b, item.recipe);
                ApplyFilter(b, item.filter);
                BuildUndo.NotePlaced(b);
                spawned.Add(b);
            }
            else
                GridFootprint.Register(go, pos, size);
        }

        BindPastedTunnels(spawned);
        BuildUndo.End();
        CancelPreview(keepSelection: false);
        ClearSelectionOnly();
    }

    void RotatePreview(bool inPlace)
    {
        if (preview.Count == 0)
            return;

        Vector2 pivot = PreviewPivot();
        for (int i = 0; i < preview.Count; i++)
        {
            PreviewItem item = preview[i];
            Vector2Int oldSize = GridFootprint.GetRotatedSize(item.data.size, item.yaw);
            Vector2 center = new Vector2(
                item.minOffset.x + (oldSize.x - 1) * 0.5f,
                item.minOffset.y + (oldSize.y - 1) * 0.5f);
            item.yaw += 90f;
            Vector2Int newSize = GridFootprint.GetRotatedSize(item.data.size, item.yaw);

            if (!inPlace)
            {
                Vector2 rel = center - pivot;
                center = pivot + new Vector2(rel.y, -rel.x);
            }

            item.minOffset = new Vector2Int(
                Mathf.RoundToInt(center.x - (newSize.x - 1) * 0.5f),
                Mathf.RoundToInt(center.y - (newSize.y - 1) * 0.5f));
            preview[i] = item;
        }
    }

    Vector2 PreviewPivot()
    {
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        for (int i = 0; i < preview.Count; i++)
        {
            PreviewItem item = preview[i];
            Vector2Int size = GridFootprint.GetRotatedSize(item.data.size, item.yaw);
            minX = Mathf.Min(minX, item.minOffset.x);
            minZ = Mathf.Min(minZ, item.minOffset.y);
            maxX = Mathf.Max(maxX, item.minOffset.x + size.x - 1);
            maxZ = Mathf.Max(maxZ, item.minOffset.y + size.y - 1);
        }
        return new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
    }

    void RotateSelectionAroundCenter()
    {
        if (!TryPlanSelectionRotate(inPlace: false, out List<Planned> planned))
            return;
        ApplyPlanned(planned);
    }

    void RotateSelectionInPlace()
    {
        if (!TryPlanSelectionRotate(inPlace: true, out List<Planned> planned))
            return;
        ApplyPlanned(planned);
    }

    struct Planned
    {
        public BuildingBase building;
        public Vector3 pos;
        public float yaw;
        public Vector2Int min;
        public Vector2Int size;
    }

    bool TryPlanSelectionRotate(bool inPlace, out List<Planned> planned)
    {
        planned = new List<Planned>(selectedBuildings.Count);
        if (selectedBuildings.Count == 0)
            return false;

        Vector2 pivot = SelectionPivot();
        var ignore = new HashSet<GameObject>();
        for (int i = 0; i < selectedBuildings.Count; i++)
            ignore.Add(selectedBuildings[i].gameObject);

        for (int i = 0; i < selectedBuildings.Count; i++)
        {
            BuildingBase b = selectedBuildings[i];
            Vector2Int oldSize = b.FootprintSize;
            Vector2Int oldMin = GridFootprint.GetMinCell(b.transform.position, oldSize);
            Vector2 center = new Vector2(
                oldMin.x + (oldSize.x - 1) * 0.5f,
                oldMin.y + (oldSize.y - 1) * 0.5f);

            float yaw = b.transform.eulerAngles.y + 90f;
            Vector2Int size = GridFootprint.GetRotatedSize(
                b.data != null ? b.data.size : Vector2Int.one, yaw);

            if (!inPlace)
            {
                Vector2 rel = center - pivot;
                center = pivot + new Vector2(rel.y, -rel.x);
            }

            Vector2Int min = new Vector2Int(
                Mathf.RoundToInt(center.x - (size.x - 1) * 0.5f),
                Mathf.RoundToInt(center.y - (size.y - 1) * 0.5f));
            Vector3 pos = GridFootprint.MinCellToCenter(min, size, b.transform.position.y);
            planned.Add(new Planned { building = b, pos = pos, yaw = yaw, min = min, size = size });
        }

        for (int i = 0; i < planned.Count; i++)
        {
            Planned p = planned[i];
            bool allowWater = p.building.data != null && p.building.data.allowOnWater;
            bool requireWater = p.building.data != null && p.building.data.requiresWater;
            if (!DecorSystem.IsAreaFreeFor(p.building.data, p.min, p.size, null, ignore)
                || !WorldBiomeMap.CanBuild(p.min, p.size, allowWater, requireWater))
                return false;
            if (PlayerBuilder.NeedsResourceNode(p.building.data) && !ResourceNode.HasNodeInArea(p.min, p.size))
                return false;
        }
        return true;
    }

    Vector2 SelectionPivot()
    {
        int minX = int.MaxValue, minZ = int.MaxValue, maxX = int.MinValue, maxZ = int.MinValue;
        bool any = false;
        foreach (Vector2Int cell in selectedCells)
        {
            any = true;
            minX = Mathf.Min(minX, cell.x);
            minZ = Mathf.Min(minZ, cell.y);
            maxX = Mathf.Max(maxX, cell.x);
            maxZ = Mathf.Max(maxZ, cell.y);
        }
        if (!any)
            return Vector2.zero;
        return new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
    }

    void ApplyPlanned(List<Planned> planned)
    {
        selectedCells.Clear();
        BuildUndo.Begin();
        for (int i = 0; i < planned.Count; i++)
            GridOccupancy.Unregister(planned[i].building.gameObject);

        for (int i = 0; i < planned.Count; i++)
        {
            Planned p = planned[i];
            Vector3 oldPos = p.building.transform.position;
            float oldYaw = p.building.transform.eulerAngles.y;
            p.building.transform.SetPositionAndRotation(p.pos, Quaternion.Euler(0f, p.yaw, 0f));
            p.building.ReRegisterOnGrid();
            p.building.OnRotated();
            BuildUndo.NoteEdit(p.building, oldPos, oldYaw);
            for (int x = 0; x < p.size.x; x++)
            {
                for (int z = 0; z < p.size.y; z++)
                    selectedCells.Add(p.min + new Vector2Int(x, z));
            }
        }
        BuildUndo.End();
    }

    int LabCapacity()
    {
        if (ResearchSystem.Instance == null)
            return 64;
        return Mathf.Max(0, ResearchSystem.Instance.GetMaxResearchLabs()
            - ResearchSystem.Instance.CountPlacedLabs());
    }

    static RecipeData ReadRecipe(BuildingBase b)
    {
        CrafterBuilding crafter = b as CrafterBuilding;
        return crafter != null ? crafter.currentRecipe : null;
    }

    static ItemData ReadFilter(BuildingBase b)
    {
        RoboticArm arm = b as RoboticArm;
        if (arm != null)
            return arm.Filter;
        Conveyor belt = b as Conveyor;
        return belt != null ? belt.Filter : null;
    }

    static void ApplyRecipe(BuildingBase b, RecipeData recipe)
    {
        CrafterBuilding crafter = b as CrafterBuilding;
        if (crafter == null || recipe == null)
            return;
        if (ResearchSystem.Instance != null && !ResearchSystem.Instance.IsRecipeUnlocked(recipe))
            return;
        crafter.SetRecipe(recipe);
    }

    static void ApplyFilter(BuildingBase b, ItemData filter)
    {
        RoboticArm arm = b as RoboticArm;
        if (arm != null)
            arm.SetFilter(filter);
        Conveyor belt = b as Conveyor;
        if (belt != null)
            belt.SetFilter(filter);
    }

    public bool TryRotateSelected()
    {
        if (!selectionMode || selectedBuildings.Count == 0)
            return false;
        RotateSelectionInPlace();
        if (builder != null)
            GameAudio.World("world_rotate", builder.transform.position);
        return true;
    }

    GameObject CreateGhost(BuildingData data, bool pairExit = false)
    {
        if (data == null)
            return null;
        GameObject source = pairExit && data.pairExitPrefab != null
            ? data.pairExitPrefab
            : BuildingVisuals.SourceForGhost(data);
        if (source == null)
            return null;

        GameObject ghost = Instantiate(source);
        BuildingVisuals.PrepareGhostInstance(ghost);

        Conveyor belt = ghost.GetComponent<Conveyor>();
        if (belt == null && data.IsConveyor)
            belt = ghost.AddComponent<Conveyor>();
        if (belt != null)
            belt.PreparePreview(data);
        return ghost;
    }

    void PlaceGhost(GameObject ghost, Vector3 pos, float yaw, bool valid)
    {
        if (ghost == null)
            return;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        ghost.transform.SetPositionAndRotation(pos, rot);
        Conveyor belt = ghost.GetComponent<Conveyor>();
        if (belt != null)
            belt.Preview(pos, rot);
    }

    void TintPreview(bool allValid)
    {
        for (int i = 0; i < preview.Count; i++)
        {
            if (preview[i].ghost == null)
                continue;
            GhostTint.Apply(preview[i].ghost, allValid, builder.ghostValidMaterial, builder.ghostInvalidMaterial);
        }
    }

    void CancelPreview(bool keepSelection = false)
    {
        Conveyor.ClearWorldVisualOverrides();
        Conveyor.PreviewExits.Clear();
        GridOccupancy.Release(reserveToken);
        for (int i = 0; i < preview.Count; i++)
        {
            if (preview[i].ghost != null)
                Destroy(preview[i].ghost);
        }
        preview.Clear();

        if (moveActive)
        {
            for (int i = 0; i < moveRecords.Count; i++)
            {
                MoveRecord rec = moveRecords[i];
                if (rec.building == null)
                    continue;
                rec.building.transform.SetPositionAndRotation(rec.pos, Quaternion.Euler(0f, rec.yaw, 0f));
                RestoreRenderers(rec);
                rec.building.ReRegisterOnGrid();
                rec.building.OnRotated();
            }
        }

        moveRecords.Clear();
        moveIgnore.Clear();
        pasteActive = false;
        moveActive = false;
        if (!keepSelection)
            boxSelecting = false;
    }

    static void RestoreRenderers(MoveRecord rec)
    {
        if (rec.renderers == null)
            return;
        for (int i = 0; i < rec.renderers.Length; i++)
        {
            if (rec.renderers[i] != null)
                rec.renderers[i].enabled = i < rec.rendererEnabled.Length && rec.rendererEnabled[i];
        }
    }

    void ExitAll()
    {
        CancelPreview();
        selectionMode = false;
        selectedCells.Clear();
        selectedBuildings.Clear();
        HideSelectionVisuals();
    }

    void RefreshSelectionVisuals()
    {
        EnsureSelectMats();
        var cells = new List<Vector2Int>(selectedCells);
        if (boxSelecting && builder.TryGetAimCell(out Vector2Int end, out _))
        {
            int minX = Mathf.Min(boxStart.x, end.x);
            int maxX = Mathf.Max(boxStart.x, end.x);
            int minZ = Mathf.Min(boxStart.y, end.y);
            int maxZ = Mathf.Max(boxStart.y, end.y);
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector2Int c = new Vector2Int(x, z);
                    if (!cells.Contains(c))
                        cells.Add(c);
                }
            }
        }

        EnsureHighlightRoot();
        while (selectHighlights.Count < cells.Count)
            selectHighlights.Add(CreateHighlight());

        float y = builder.HasPlacementTarget
            ? builder.CurrentPlacementPosition.y + 0.04f
            : (GridSystem.Instance != null ? GridSystem.Instance.origin.y + 0.04f : 0.04f);
        float cell = GridFootprint.CellSize;

        for (int i = 0; i < selectHighlights.Count; i++)
        {
            Transform t = selectHighlights[i];
            if (t == null)
                continue;
            if (i >= cells.Count || !selectionMode)
            {
                t.gameObject.SetActive(false);
                continue;
            }

            t.gameObject.SetActive(true);
            Vector3 p = GridSystem.Instance != null
                ? GridSystem.Instance.GetCellCenter(cells[i], y)
                : new Vector3(cells[i].x * cell, y, cells[i].y * cell);
            t.position = p;
            t.localScale = new Vector3(cell / 10f * 0.9f, 1f, cell / 10f * 0.9f);
            MeshRenderer r = t.GetComponent<MeshRenderer>();
            if (r != null)
                r.sharedMaterial = boxSelecting ? boxMat : selectMat;
        }
    }

    void HideSelectionVisuals()
    {
        for (int i = 0; i < selectHighlights.Count; i++)
        {
            if (selectHighlights[i] != null)
                selectHighlights[i].gameObject.SetActive(false);
        }
        TrimHighlights();
    }

    void TrimHighlights()
    {
        for (int i = selectHighlights.Count - 1; i >= HighlightPoolKeep; i--)
        {
            if (selectHighlights[i] != null)
                Destroy(selectHighlights[i].gameObject);
            selectHighlights.RemoveAt(i);
        }

        for (int i = 0; i < selectHighlights.Count; i++)
        {
            if (selectHighlights[i] != null)
                selectHighlights[i].gameObject.SetActive(false);
        }
    }

    void EnsureHighlightRoot()
    {
        if (highlightRoot != null)
            return;
        GameObject root = new GameObject("SelectionHighlights");
        highlightRoot = root.transform;
    }

    void EnsureSelectMats()
    {
        if (selectMat != null)
            return;
        selectMat = CreateMat(new Color(0.35f, 0.95f, 0.75f, 0.38f));
        boxMat = CreateMat(new Color(0.95f, 0.9f, 0.35f, 0.32f));
    }

    static Material CreateMat(Color color)
    {
        Material m = RuntimeMaterials.Create(color);
        m.renderQueue = 3120;
        return m;
    }

    Transform CreateHighlight()
    {
        EnsureHighlightRoot();
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
        go.name = "SelectCell";
        go.transform.SetParent(highlightRoot, false);
        Destroy(go.GetComponent<Collider>());
        MeshRenderer r = go.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
    }

    void OnDestroy()
    {
        CancelPreview();
        for (int i = 0; i < selectHighlights.Count; i++)
        {
            if (selectHighlights[i] != null)
                Destroy(selectHighlights[i].gameObject);
        }
        if (highlightRoot != null)
            Destroy(highlightRoot.gameObject);
        if (selectMat != null) Destroy(selectMat);
        if (boxMat != null) Destroy(boxMat);
    }
}
