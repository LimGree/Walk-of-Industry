using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("References")]
    public PlayerInventory playerInventory;
    public PlayerBuilder playerBuilder;
    public ResearchSystem researchSystem;

    [Header("Game State")]
    public bool isPaused = false;

    public bool IsPaused => isPaused;

    InputSystem_Actions inputActions;
    IndustryPause pauseUi;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        inputActions = KeybindStore.Shared;
        if (GetComponent<InputHintUI>() == null)
            gameObject.AddComponent<InputHintUI>();
        if (GetComponent<WorldMapUI>() == null)
            gameObject.AddComponent<WorldMapUI>();
        if (GetComponent<PlayerWallet>() == null)
            gameObject.AddComponent<PlayerWallet>();
        if (GetComponent<ProductionStats>() == null)
            gameObject.AddComponent<ProductionStats>();
        if (GetComponent<AchievementSystem>() == null)
            gameObject.AddComponent<AchievementSystem>();
        if (GetComponent<PhotoMode>() == null)
            gameObject.AddComponent<PhotoMode>();
        if (GetComponent<BeltSpeedSystem>() == null)
            gameObject.AddComponent<BeltSpeedSystem>();
        if (GetComponent<MapMarkerSystem>() == null)
            gameObject.AddComponent<MapMarkerSystem>();
        if (GetComponent<MapExploration>() == null)
            gameObject.AddComponent<MapExploration>();
        if (GetComponent<WalletHud>() == null)
            gameObject.AddComponent<WalletHud>();
        if (GetComponent<MachineIdleHud>() == null)
            gameObject.AddComponent<MachineIdleHud>();
        if (GetComponent<BreakdownSystem>() == null)
            gameObject.AddComponent<BreakdownSystem>();
        if (GetComponent<DecorSystem>() == null)
            gameObject.AddComponent<DecorSystem>();
        if (GetComponent<RepairUI>() == null)
            gameObject.AddComponent<RepairUI>();
        if (GetComponent<DroneStationUI>() == null)
            gameObject.AddComponent<DroneStationUI>();
        if (GetComponent<SignEditorUI>() == null)
            gameObject.AddComponent<SignEditorUI>();
        if (GetComponent<BuildingPicker>() == null)
            gameObject.AddComponent<BuildingPicker>();
        if (GetComponent<SelectionActionsUI>() == null)
            gameObject.AddComponent<SelectionActionsUI>();
        if (GetComponent<BlueprintLibraryUI>() == null)
            gameObject.AddComponent<BlueprintLibraryUI>();
        if (GetComponent<CrosshairHud>() == null)
            gameObject.AddComponent<CrosshairHud>();
        if (GetComponent<DayNightCycle>() == null)
            gameObject.AddComponent<DayNightCycle>();
        if (GetComponent<WeatherCycle>() == null)
            gameObject.AddComponent<WeatherCycle>();
        if (GetComponent<TutorialSystem>() == null)
            gameObject.AddComponent<TutorialSystem>();
        if (GetComponent<TutorialUI>() == null)
            gameObject.AddComponent<TutorialUI>();
        if (GetComponent<GearHotbar>() == null)
            gameObject.AddComponent<GearHotbar>();
        if (GetComponent<PerkSystem>() == null)
            gameObject.AddComponent<PerkSystem>();
        if (GetComponent<GoalSystem>() == null)
            gameObject.AddComponent<GoalSystem>();
        if (GetComponent<GoalsUI>() == null)
            gameObject.AddComponent<GoalsUI>();
        if (GetComponent<ProductionMapUI>() == null)
            gameObject.AddComponent<ProductionMapUI>();
        GameAudio.Ensure();
        GameSettings.Apply();
    }

    void OnEnable()
    {
        inputActions.Player.Pause.performed += OnPausePerformed;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        if (inputActions != null)
            inputActions.Player.Pause.performed -= OnPausePerformed;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        GameSettings.Apply();
        GameSettings.ApplyTimeScale();
    }

    void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (LoadingScreen.IsLoading)
            return;
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (TutorialSystem.Instance != null && TutorialSystem.Instance.BlocksPause)
            return;

        if (UiModal.IsOpen)
        {
            UiModal.Hide();
            return;
        }

        if (WorldMapUI.Instance != null && WorldMapUI.Instance.MiniDragActive)
        {
            WorldMapUI.Instance.EndMiniDrag(false);
            return;
        }

        // Стек окон: Esc закрывает верхнее окно; пусто — пауза.
        if (UiStack.CloseTop())
            return;

        TogglePause();
    }

    public void TogglePause()
    {
        SetPaused(!isPaused);
    }

    public void SetPaused(bool paused)
    {
        // Во время загрузки мира пауза не включается (Esc, потеря фокуса, настройки).
        if (paused && LoadingScreen.IsLoading)
            return;
        isPaused = paused;
        Time.timeScale = paused ? 0f : GameSettings.PlaySpeed;
        AudioListener.pause = paused && GameSettings.PauseMutesWorld;
        if (paused)
            UiAudio.PlayPause();
        else
            UiAudio.PlayUnpause();
        GameAudio.SetPaused(paused);
        if (paused)
            UiStack.CloseAll();
        if (paused && PhotoMode.Instance != null)
            PhotoMode.Instance.Cancel();
        if (paused && BeltRide.Instance != null && BeltRide.Instance.IsRiding)
            BeltRide.Instance.Stop();

        if (pauseUi == null)
            pauseUi = new IndustryPause();
        pauseUi.Build(this);
        pauseUi.SetVisible(paused);

        RestoreGameplayFocus();
    }

    public void RestoreGameplayFocus()
    {
        bool uiOpen = (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
            || WorldOverlayGate.IsOpen;
        bool mapOpen = WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen;
        bool bagOpen = InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen;
        bool shopOpen = WalletHud.Instance != null && WalletHud.Instance.IsShopOpen;
        bool selectionOpen = SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen;
        bool researchOpen = ResearchUI.Instance != null && ResearchUI.Instance.IsOpen;
        bool buildOpen = BuildMenuUI.Instance != null && BuildMenuUI.Instance.IsOpen;
        bool libraryOpen = BlueprintLibraryUI.Instance != null && BlueprintLibraryUI.Instance.IsOpen;
        bool tutorialOpen = TutorialSystem.Instance != null && TutorialSystem.Instance.IsModal;
        bool modalOpen = UiModal.IsOpen;
        bool consoleOpen = DevConsole.IsOpen;
        bool menuOpen = UiStack.Any || uiOpen || mapOpen || bagOpen || shopOpen || selectionOpen || researchOpen || buildOpen || libraryOpen || tutorialOpen || modalOpen || consoleOpen;
        if (PhotoMode.IsActive)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            SetPlayerControl(false);
            return;
        }

        if (BeltRide.Instance != null && BeltRide.Instance.IsRiding)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.Locked;
            UnityEngine.Cursor.visible = false;
            PlayerMovement rideMove = Object.FindFirstObjectByType<PlayerMovement>();
            if (rideMove != null)
            {
                rideMove.canMove = false;
                rideMove.canLook = true;
            }
            return;
        }

        bool freeCursor = isPaused || menuOpen;

        UnityEngine.Cursor.lockState = freeCursor ? CursorLockMode.None : CursorLockMode.Locked;
        UnityEngine.Cursor.visible = freeCursor;
        SetPlayerControl(!isPaused && !menuOpen);
    }

    static void SetPlayerControl(bool enabled)
    {
        PlayerMovement movement = Object.FindFirstObjectByType<PlayerMovement>();
        if (movement == null)
            return;

        movement.canMove = enabled;
        movement.canLook = enabled;
    }

    public void PrepareLeaveGameplay()
    {
        if (SaveSystem.Instance != null && GameSettings.SaveOnQuit)
            SaveSystem.Instance.SaveGame();
        UiStack.Clear();
        isPaused = false;
        GameSettings.DevFrozen = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (ResearchSystem.Instance != null)
            Destroy(ResearchSystem.Instance.gameObject);
        WorldCatalog.ClearActive();
        Instance = null;
        Destroy(gameObject);
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
