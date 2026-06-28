using UnityEngine;
using UnityEngine.UI;

public sealed class SaveSlotDeleteButtonUI : MonoBehaviour
{
    [Header("Save Slot")]
    [Tooltip("Erster Slot = 0, zweiter Slot = 1, dritter Slot = 2")]
    [SerializeField] private int slotIndex;

    [Header("Delete Button")]
    [SerializeField] private Button deleteButton;

    [Header("Confirmation Window")]
    [SerializeField]
    private DeleteSaveConfirmationUI deleteConfirmationUI;

    private bool isSubscribed;

    private void Awake()
    {
        if (deleteButton != null)
        {
            deleteButton.onClick.AddListener(
                OnDeleteButtonClicked
            );
        }
    }

    private void OnEnable()
    {
        TrySubscribeToGameManager();
        RefreshDeleteButton();
    }

    private void Start()
    {
        TrySubscribeToGameManager();
        RefreshDeleteButton();
    }

    private void OnDisable()
    {
        UnsubscribeFromGameManager();
    }

    private void OnDestroy()
    {
        if (deleteButton != null)
        {
            deleteButton.onClick.RemoveListener(
                OnDeleteButtonClicked
            );
        }

        UnsubscribeFromGameManager();
    }

    private void TrySubscribeToGameManager()
    {
        if (isSubscribed)
            return;

        if (GameManager.Instance == null)
            return;

        GameManager.Instance.SaveSlotDeleted +=
            OnSaveSlotDeleted;

        isSubscribed = true;
    }

    private void UnsubscribeFromGameManager()
    {
        if (!isSubscribed)
            return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SaveSlotDeleted -=
                OnSaveSlotDeleted;
        }

        isSubscribed = false;
    }

    public void RefreshDeleteButton()
    {
        if (deleteButton == null)
            return;

        bool hasSave =
            GameManager.Instance != null &&
            GameManager.Instance.HasSaveForSlot(slotIndex);

        deleteButton.gameObject.SetActive(hasSave);
    }

    private void OnDeleteButtonClicked()
    {
        if (GameManager.Instance == null)
            return;

        if (!GameManager.Instance.HasSaveForSlot(slotIndex))
        {
            RefreshDeleteButton();
            return;
        }

        if (deleteConfirmationUI == null)
        {
            Debug.LogWarning(
                "Delete Confirmation UI wurde nicht eingetragen.",
                gameObject
            );

            return;
        }

        deleteConfirmationUI.OpenForSlot(slotIndex);
    }

    private void OnSaveSlotDeleted(int deletedSlotIndex)
    {
        if (deletedSlotIndex != slotIndex)
            return;

        RefreshDeleteButton();
    }
}