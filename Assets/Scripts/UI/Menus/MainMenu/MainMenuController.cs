using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class MainMenuController : MonoBehaviour
{
    // ==================================================
    // PANELS
    // ==================================================

    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject saveSelectPanel;
    [SerializeField] private GameObject optionsPanel;

    // ==================================================
    // SAVE SLOTS
    // ==================================================

    [Header("Save Slot Buttons")]
    [Tooltip("Slot 1 = Element 0, Slot 2 = Element 1 usw.")]
    [SerializeField]
    private Button[] saveSlotButtons = new Button[3];

    [Header("Save Slot Name Texts")]
    [Tooltip(
        "Hier exakt die Texte eintragen, die den Namen " +
        "oder Empty anzeigen sollen."
    )]
    [SerializeField]
    private TMP_Text[] saveSlotNameTexts =
        new TMP_Text[3];

    [Header("Save Slot Display")]
    [SerializeField]
    private string emptySlotText = "Empty";

    private bool isSubscribedToGameManager;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void OnEnable()
    {
        TrySubscribeToGameManager();
    }

    private void Start()
    {
        ShowMainPanel();

        TrySubscribeToGameManager();
        UpdateSaveSlotButtons();
        CheckGameManager();
    }

    private void OnDisable()
    {
        UnsubscribeFromGameManager();
    }

    private void OnDestroy()
    {
        UnsubscribeFromGameManager();
    }

    // ==================================================
    // MAIN MENU
    // ==================================================

    public void OnPlayPressed()
    {
        SetPanelState(
            mainActive: false,
            saveSelectActive: true,
            optionsActive: false
        );

        UpdateSaveSlotButtons();
    }

    public void OnOptionsPressed()
    {
        if (optionsPanel == null)
        {
            Debug.LogWarning(
                "Es wurde noch kein Options Panel zugewiesen.",
                gameObject
            );

            return;
        }

        SetPanelState(
            mainActive: false,
            saveSelectActive: false,
            optionsActive: true
        );
    }

    public void OnQuitPressed()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ==================================================
    // SAVE SLOTS
    // ==================================================

    public void LoadSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return;

        if (GameManager.Instance == null)
        {
            Debug.LogError(
                "Es wurde kein aktiver GameManager gefunden.",
                gameObject
            );

            return;
        }

        GameManager.Instance.ContinueGame(slotIndex);
    }

    public void UpdateSaveSlotButtons()
    {
        if (saveSlotNameTexts == null ||
            saveSlotNameTexts.Length == 0)
        {
            Debug.LogWarning(
                "Es wurden keine Save Slot Name Texts eingetragen.",
                gameObject
            );

            return;
        }

        for (int i = 0; i < saveSlotNameTexts.Length; i++)
        {
            TMP_Text slotNameText =
                saveSlotNameTexts[i];

            if (slotNameText == null)
            {
                Debug.LogWarning(
                    $"Der Name-Text für Speicherplatz {i} fehlt.",
                    gameObject
                );

                continue;
            }

            bool hasSave =
                GameManager.Instance != null &&
                GameManager.Instance.HasSaveForSlot(i);

            slotNameText.text =
                hasSave
                    ? GameManager.Instance.GetSaveNameForSlot(i)
                    : emptySlotText;
        }
    }

    private bool IsValidSlotIndex(int slotIndex)
    {
        if (slotIndex < 0)
        {
            Debug.LogWarning(
                "Ungültiger Save-Slot: " + slotIndex,
                gameObject
            );

            return false;
        }

        if (saveSlotButtons != null &&
            saveSlotButtons.Length > 0 &&
            slotIndex >= saveSlotButtons.Length)
        {
            Debug.LogWarning(
                $"Der Save-Slot {slotIndex} existiert nicht.",
                gameObject
            );

            return false;
        }

        return true;
    }

    private void TrySubscribeToGameManager()
    {
        if (isSubscribedToGameManager)
            return;

        if (GameManager.Instance == null)
            return;

        GameManager.Instance.SaveSlotDeleted +=
            OnSaveSlotDeleted;

        isSubscribedToGameManager = true;
    }

    private void UnsubscribeFromGameManager()
    {
        if (!isSubscribedToGameManager)
            return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SaveSlotDeleted -=
                OnSaveSlotDeleted;
        }

        isSubscribedToGameManager = false;
    }

    private void OnSaveSlotDeleted(int deletedSlotIndex)
    {
        UpdateSaveSlotButtons();
    }

    // ==================================================
    // PANEL NAVIGATION
    // ==================================================

    public void BackToMain()
    {
        ShowMainPanel();
    }

    private void ShowMainPanel()
    {
        SetPanelState(
            mainActive: true,
            saveSelectActive: false,
            optionsActive: false
        );
    }

    private void SetPanelState(
        bool mainActive,
        bool saveSelectActive,
        bool optionsActive
    )
    {
        if (mainPanel != null)
            mainPanel.SetActive(mainActive);

        if (saveSelectPanel != null)
            saveSelectPanel.SetActive(saveSelectActive);

        if (optionsPanel != null)
            optionsPanel.SetActive(optionsActive);
    }

    // ==================================================
    // VALIDATION
    // ==================================================

    private void CheckGameManager()
    {
        if (GameManager.Instance != null)
            return;

        Debug.LogWarning(
            "In der MainMenu-Szene wurde kein aktiver " +
            "GameManager gefunden.",
            gameObject
        );
    }
}