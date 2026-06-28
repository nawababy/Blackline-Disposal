using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class DeleteSaveConfirmationUI : MonoBehaviour
{
    [Header("Confirmation Window")]
    [SerializeField] private GameObject confirmationPanel;

    [Header("Texts")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text messageText;

    [Header("Buttons")]
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button confirmDeleteButton;

    [Header("Events")]
    [Tooltip(
        "Wird ausgelöst, nachdem ein Speicherplatz gelöscht wurde. " +
        "Hier kann später die Save-Slot-Anzeige aktualisiert werden."
    )]
    [SerializeField]
    private UnityEvent onSaveDeleted =
        new UnityEvent();

    private int pendingSlotIndex = -1;

    public bool IsOpen =>
        confirmationPanel != null &&
        confirmationPanel.activeSelf;

    private void Awake()
    {
        if (cancelButton != null)
            cancelButton.onClick.AddListener(CancelDelete);

        if (confirmDeleteButton != null)
            confirmDeleteButton.onClick.AddListener(ConfirmDelete);

        CloseWindow();
    }

    private void OnDestroy()
    {
        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(CancelDelete);

        if (confirmDeleteButton != null)
            confirmDeleteButton.onClick.RemoveListener(ConfirmDelete);
    }

    private void Update()
    {
        if (!IsOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
            CancelDelete();
    }

    public void OpenForSlot(int slotIndex)
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning(
                "DeleteSaveConfirmationUI findet keinen GameManager."
            );

            return;
        }

        if (!GameManager.Instance.HasSaveForSlot(slotIndex))
        {
            Debug.LogWarning(
                $"Speicherplatz {slotIndex} ist bereits leer."
            );

            CloseWindow();
            return;
        }

        pendingSlotIndex = slotIndex;

        string saveName =
            GameManager.Instance.GetSaveNameForSlot(slotIndex);

        if (titleText != null)
            titleText.text = "DELETE SAVE";

        if (messageText != null)
        {
            messageText.text =
                $"Are you sure you want to permanently delete " +
                $"\"{saveName}\"?\n\n" +
                "This action cannot be undone.";
        }

        if (confirmationPanel != null)
            confirmationPanel.SetActive(true);
    }

    public void CancelDelete()
    {
        CloseWindow();
    }

    public void ConfirmDelete()
    {
        if (pendingSlotIndex < 0)
        {
            Debug.LogWarning(
                "Es wurde kein Speicherplatz zum Löschen ausgewählt."
            );

            CloseWindow();
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogWarning(
                "DeleteSaveConfirmationUI findet keinen GameManager."
            );

            CloseWindow();
            return;
        }

        int slotToDelete = pendingSlotIndex;

        bool wasDeleted =
            GameManager.Instance.DeleteSaveForSlot(slotToDelete);

        CloseWindow();

        if (wasDeleted)
            onSaveDeleted.Invoke();
    }

    private void CloseWindow()
    {
        pendingSlotIndex = -1;

        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
    }
}