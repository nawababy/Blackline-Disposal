using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAppearanceController : MonoBehaviour
{
    [SerializeField] private CharacterAppearanceDatabase database;
    [SerializeField] private CharacterAppearanceApplier applier;
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private RuntimeAnimatorController runtimeAnimatorController;

    private SaveManager subscribedSaveManager;
    private int lastAppliedSlotIndex = int.MinValue;
    private string lastAppliedFingerprint = string.Empty;

    private void Awake()
    {
        if (applier == null)
        {
            applier = GetComponent<CharacterAppearanceApplier>();
        }

        if (playerMovement == null)
        {
            playerMovement = GetComponent<PlayerMovement>();
        }
    }

    private void OnEnable()
    {
        SubscribeToSaveManager();
        TryApplyCurrentSave("OnEnable");
    }

    private void Start()
    {
        SubscribeToSaveManager();
        TryApplyCurrentSave("Start");
    }

    private void OnDisable()
    {
        UnsubscribeFromSaveManager();
    }

    public bool ApplyCurrentSave()
    {
        return TryApplyCurrentSave("ManualRefresh");
    }

    private void HandleGameLoaded(int slotIndex)
    {
        TryApplyCurrentSave("GameLoaded");
    }

    private void SubscribeToSaveManager()
    {
        SaveManager saveManager = SaveManager.Instance;
        if (saveManager == null || saveManager == subscribedSaveManager)
        {
            return;
        }

        UnsubscribeFromSaveManager();
        subscribedSaveManager = saveManager;
        subscribedSaveManager.GameLoaded += HandleGameLoaded;
    }

    private void UnsubscribeFromSaveManager()
    {
        if (subscribedSaveManager == null)
        {
            return;
        }

        subscribedSaveManager.GameLoaded -= HandleGameLoaded;
        subscribedSaveManager = null;
    }

    private bool TryApplyCurrentSave(string reason)
    {
        if (database == null)
        {
            Debug.LogError("[PlayerAppearanceController] Cannot apply player appearance because no CharacterAppearanceDatabase is assigned.", this);
            return false;
        }

        if (applier == null)
        {
            Debug.LogError("[PlayerAppearanceController] Cannot apply player appearance because no CharacterAppearanceApplier is assigned.", this);
            return false;
        }

        if (playerMovement == null)
        {
            Debug.LogError("[PlayerAppearanceController] Cannot apply player appearance because no PlayerMovement is assigned.", this);
            return false;
        }

        SubscribeToSaveManager();

        SaveManager saveManager = SaveManager.Instance;
        if (saveManager == null)
        {
            Debug.LogWarning("[PlayerAppearanceController] Cannot apply player appearance because no SaveManager exists yet. Reason: " + reason, this);
            return false;
        }

        SaveGameData saveData = saveManager.CurrentSave;
        if (saveData == null)
        {
            return false;
        }

        int currentSlot = GameManager.Instance != null ? GameManager.Instance.CurrentSlot : saveData.slotIndex;
        if (currentSlot >= 0 && saveData.slotIndex != currentSlot)
        {
            return false;
        }

        CharacterAppearanceData appearance = saveData.player != null ? saveData.player.appearance : null;
        if (appearance == null)
        {
            Debug.LogWarning("[PlayerAppearanceController] Save Slot " + (saveData.slotIndex + 1) + " has no modular appearance. Using database default.", this);
            appearance = new CharacterAppearanceData();
        }

        string fingerprint = BuildFingerprint(saveData.slotIndex, appearance);
        if (lastAppliedSlotIndex == saveData.slotIndex && string.Equals(lastAppliedFingerprint, fingerprint, System.StringComparison.Ordinal))
        {
            return true;
        }

        applier.SetDatabase(database);

        Animator runtimeAnimator;
        if (!applier.ApplyRuntimeAppearance(appearance, runtimeAnimatorController, out runtimeAnimator))
        {
            Debug.LogError("[PlayerAppearanceController] Failed to apply saved player appearance for Slot " + (saveData.slotIndex + 1) + ".", this);
            return false;
        }

        playerMovement.SetAnimator(runtimeAnimator);
        lastAppliedSlotIndex = saveData.slotIndex;
        lastAppliedFingerprint = fingerprint;
        return true;
    }

    private static string BuildFingerprint(int slotIndex, CharacterAppearanceData appearance)
    {
        if (appearance == null)
        {
            return slotIndex + "|<null>";
        }

        return slotIndex + "|" +
            appearance.appearanceVersion + "|" +
            appearance.bodyTypeId + "|" +
            appearance.skinId + "|" +
            appearance.hairId + "|" +
            appearance.faceId + "|" +
            appearance.upperId + "|" +
            appearance.pantsId + "|" +
            appearance.shoesId + "|" +
            appearance.hatId + "|" +
            appearance.glassesId + "|" +
            appearance.glovesId + "|" +
            appearance.fullBodyId;
    }
}