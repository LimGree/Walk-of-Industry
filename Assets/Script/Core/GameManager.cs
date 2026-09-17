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
        if (GetComponent<BeltSpeedSystem>() == null)
            gameObject.AddComponent<BeltSpeedSystem>();
        if (GetComponent<MapMarkerSystem>() == null)
            gameObject.AddComponent<MapMarkerSystem>();
        if (GetComponent<MapExploration>() == null)
            gameObject.AddComponent<MapExploration>();
        if (GetComponent<WalletHud>() == null)
            gameObject.AddComponent<WalletHud>();
        if (GetComponent<SelectionActionsUI>() == null)
            gameObject.AddComponent<SelectionActionsUI>();
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
    }

    void OnPausePerformed(InputAction.CallbackContext context)
    {
        if (KeybindStore.BlocksGameplayInput)
            return;
        if (TutorialSystem.Instance != null && TutorialSystem.Instance.BlocksPause)
            return;

        if (UiModal.IsOpen)
        {
            UiModal.Hide();
            return;
        }

        if (InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen)
        {
            InventoryUI.Instance.SetBagOpen(false);
            return;
        }

        if (WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
        {
            WalletHud.Instance.SetShopOpen(false);
            return;
        }

        if (SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen)
        {
            SelectionActionsUI.Instance.Toggle();
            return;
        }

        if (MachineUI.Instance != null && MachineUI.Instance.IsOpen)
        {
            MachineUI.Instance.Close();
            return;
        }

        if (ResearchUI.Instance != null && ResearchUI.Instance.IsOpen)
        {
            ResearchUI.Instance.Close();
            return;
        }

        if (BuildMenuUI.Instance != null && BuildMenuUI.Instance.IsOpen)
        {
            BuildMenuUI.Instance.CloseMenu(true);
            return;
        }

        if (WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen)
        {
            WorldMapUI.Instance.SetOpen(false);
            return;
        }

        TogglePause();
    }

    public void TogglePause()
    {
        SetPaused(!isPaused);
    }

    public void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        if (paused)
            UiAudio.PlayPause();
        else
            UiAudio.PlayUnpause();
        GameAudio.SetPaused(paused);
        if (paused && WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen)
            WorldMapUI.Instance.SetOpen(false);
        if (paused && InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen)
            InventoryUI.Instance.SetBagOpen(false);
        if (paused && WalletHud.Instance != null && WalletHud.Instance.IsShopOpen)
            WalletHud.Instance.SetShopOpen(false);
        if (paused && ResearchUI.Instance != null && ResearchUI.Instance.IsOpen)
            ResearchUI.Instance.Close();
        if (paused && BuildMenuUI.Instance != null && BuildMenuUI.Instance.IsOpen)
            BuildMenuUI.Instance.CloseMenu(false);

        if (pauseUi == null)
            pauseUi = new IndustryPause();
        pauseUi.Build(this);
        pauseUi.SetVisible(paused);

        RestoreGameplayFocus();
    }

    public void RestoreGameplayFocus()
    {
        bool uiOpen = MachineUI.Instance != null && MachineUI.Instance.IsOpen;
        bool mapOpen = WorldMapUI.Instance != null && WorldMapUI.Instance.IsOpen;
        bool bagOpen = InventoryUI.Instance != null && InventoryUI.Instance.IsBagOpen;
        bool shopOpen = WalletHud.Instance != null && WalletHud.Instance.IsShopOpen;
        bool selectionOpen = SelectionActionsUI.Instance != null && SelectionActionsUI.Instance.IsOpen;
        bool researchOpen = ResearchUI.Instance != null && ResearchUI.Instance.IsOpen;
        bool buildOpen = BuildMenuUI.Instance != null && BuildMenuUI.Instance.IsOpen;
        bool tutorialOpen = TutorialSystem.Instance != null && TutorialSystem.Instance.IsModal;
        bool menuOpen = uiOpen || mapOpen || bagOpen || shopOpen || selectionOpen || researchOpen || buildOpen || tutorialOpen;
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
        if (SaveSystem.Instance != null)
            SaveSystem.Instance.SaveGame();
        isPaused = false;
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
