using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class EscapeMenuController : MonoBehaviour
{
    // ==================================================
    // MENU REFERENCES
    // ==================================================

    [Header("Menu References")]
    [SerializeField]
    private GameObject menuPanel;

    [SerializeField]
    private GameObject settingsPanel;

    [SerializeField]
    private Image blurOverlay;

    // ==================================================
    // GAMEPLAY HUD
    // ==================================================

    [Header("Gameplay HUD")]
    [Tooltip(
        "Das gemeinsame Root-Objekt für Hotbar, Geldanzeige, " +
        "Kompass und Crosshair."
    )]
    [SerializeField]
    private GameObject gameplayHudRoot;

    [SerializeField]
    private Image crosshairImage;

    // ==================================================
    // LOCAL PLAYER
    // ==================================================

    [Header("Local Player")]
    [SerializeField]
    private PlayerMovement playerMovement;

    [SerializeField]
    private PlayerLook playerLook;

    [SerializeField]
    private CarryTrash carryTrash;

    [SerializeField]
    private HotbarController hotbarController;

    [SerializeField]
    private Transform playerTransform;

    // ==================================================
    // INPUT
    // ==================================================

    [Header("Input")]
    [SerializeField]
    private InputSettings inputSettings;

    [SerializeField]
    private KeyCode fallbackToggleKey =
        KeyCode.Escape;

    // ==================================================
    // I'M STUCK
    // ==================================================

    [Header("I'm Stuck")]
    [SerializeField]
    private Transform stuckRespawnPoint;

    [SerializeField]
    private Vector3 fallbackStuckPosition =
        new Vector3(0f, 1f, 0f);

    // ==================================================
    // EXIT
    // ==================================================

    [Header("Exit")]
    [SerializeField]
    private string mainMenuSceneName =
        "MainMenu";

    // ==================================================
    // STATE
    // ==================================================

    private bool isMenuOpen;

    public bool IsMenuOpen => isMenuOpen;

    private KeyCode ToggleKey =>
        inputSettings != null
            ? inputSettings.pauseKey
            : fallbackToggleKey;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Start()
    {
        CloseMenu();
    }

    private void Update()
    {
        if (!Input.GetKeyDown(ToggleKey))
            return;

        /*
         * Wenn die Settings geöffnet sind,
         * führt Escape zurück zum Pause-Menü.
         */
        if (settingsPanel != null &&
            settingsPanel.activeSelf)
        {
            OnSettingsBackButton();
            return;
        }

        ToggleMenu();
    }

    // ==================================================
    // OPEN / CLOSE
    // ==================================================

    public void ToggleMenu()
    {
        if (isMenuOpen)
            CloseMenu();
        else
            OpenMenu();
    }

    public void OpenMenu()
    {
        isMenuOpen = true;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        ApplyMenuState();
    }

    public void CloseMenu()
    {
        isMenuOpen = false;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        ApplyMenuState();
    }

    private void ApplyMenuState()
    {
        if (menuPanel != null)
            menuPanel.SetActive(isMenuOpen);

        if (blurOverlay != null)
            blurOverlay.enabled = isMenuOpen;

        /*
         * Versteckt Hotbar, Geld, Bank, Kompass und
         * Crosshair während Pause und Settings.
         */
        if (gameplayHudRoot != null)
            gameplayHudRoot.SetActive(!isMenuOpen);

        if (crosshairImage != null)
            crosshairImage.enabled = !isMenuOpen;

        if (playerMovement != null)
        {
            playerMovement.SetInputEnabled(
                !isMenuOpen
            );
        }

        if (playerLook != null)
        {
            playerLook.SetLookEnabled(
                !isMenuOpen
            );
        }

        if (carryTrash != null)
        {
            carryTrash.SetCarryEnabled(
                !isMenuOpen
            );
        }

        if (hotbarController != null)
        {
            hotbarController.SetControlEnabled(
                !isMenuOpen
            );
        }

        UpdateGameState();
        ApplyCursorState();
    }

    private void UpdateGameState()
    {
        if (GameManager.Instance == null)
            return;

        GameManager.Instance.SetPausedState(
            isMenuOpen
        );
    }

    private void ApplyCursorState()
    {
        Cursor.lockState =
            isMenuOpen
                ? CursorLockMode.None
                : CursorLockMode.Locked;

        Cursor.visible =
            isMenuOpen;
    }

    private void OnApplicationFocus(
        bool hasFocus
    )
    {
        if (hasFocus)
            ApplyCursorState();
    }

    // ==================================================
    // BUTTONS
    // ==================================================

    public void OnResumeButton()
    {
        CloseMenu();
    }

    public void OnSettingsButton()
    {
        if (!isMenuOpen ||
            settingsPanel == null)
        {
            return;
        }

        settingsPanel.SetActive(true);
    }

    public void OnSettingsBackButton()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    // ==================================================
    // SAVE AND EXIT
    // ==================================================

    public void OnSaveAndExitButton()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError(
                "Save & Exit nicht möglich: " +
                "Es wurde kein SaveManager gefunden.",
                gameObject
            );

            return;
        }

        bool saveSuccessful =
            SaveManager.Instance.SaveCurrentGame();

        if (!saveSuccessful)
        {
            Debug.LogError(
                "Das Spiel konnte nicht gespeichert werden. " +
                "Die GameScene wird deshalb nicht verlassen.",
                gameObject
            );

            return;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPausedState(false);
            GameManager.Instance.LoadMainMenu();
            return;
        }

        if (string.IsNullOrWhiteSpace(
                mainMenuSceneName))
        {
            Debug.LogWarning(
                "Keine Main-Menu-Szene eingetragen.",
                gameObject
            );

            return;
        }

        SceneManager.LoadScene(
            mainMenuSceneName
        );
    }

    public void OnQuitGameButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ==================================================
    // I'M STUCK
    // ==================================================

    public void OnImStuckButton()
    {
        if (playerTransform == null)
        {
            Debug.LogWarning(
                "Beim EscapeMenuController fehlt " +
                "der Player Transform.",
                gameObject
            );

            return;
        }

        Vector3 targetPosition =
            stuckRespawnPoint != null
                ? stuckRespawnPoint.position
                : fallbackStuckPosition;

        Quaternion targetRotation =
            stuckRespawnPoint != null
                ? stuckRespawnPoint.rotation
                : playerTransform.rotation;

        CharacterController characterController =
            playerTransform.GetComponent<CharacterController>();

        bool controllerWasEnabled =
            characterController != null &&
            characterController.enabled;

        if (controllerWasEnabled)
            characterController.enabled = false;

        playerTransform.SetPositionAndRotation(
            targetPosition,
            targetRotation
        );

        if (controllerWasEnabled)
            characterController.enabled = true;

        CloseMenu();
    }
}