using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class TrashFacility : MonoBehaviour
{
    // ==================================================
    // SAVE ID
    // ==================================================

    [Header("Save ID")]
    [SerializeField]
    private string facilityId = string.Empty;

    // ==================================================
    // ACCEPTED TRASH
    // ==================================================

    [Header("Accepted Trash Types")]
    [SerializeField]
    private List<TrashClass> acceptedTrash =
        new List<TrashClass>();

    // ==================================================
    // FACILITY LEVELS
    // ==================================================

    [Header("Facility Levels")]
    [Tooltip(
        "Level 1 = Element 0, Level 2 = Element 1 usw. " +
        "Der Upgrade Cost eines Levels ist der Preis, " +
        "um dieses Level freizuschalten."
    )]
    [SerializeField]
    private FacilityLevelDefinition[] levels =
        new FacilityLevelDefinition[3];

    [Header("Current Level")]
    [SerializeField, Min(0)]
    private int currentLevelIndex;

    // ==================================================
    // REFERENCES
    // ==================================================

    [Header("References")]
    [SerializeField]
    private PlayerInventory playerInventory;

    // ==================================================
    // CURRENT STATE
    // ==================================================

    private bool isProcessing;
    private float processingTimeRemaining;
    private float processingBaseValue;
    private float processingPayoutValue;
    private Coroutine processingRoutine;

    public string FacilityId =>
        string.IsNullOrWhiteSpace(facilityId)
            ? string.Empty
            : facilityId.Trim();

    public bool HasValidFacilityId =>
        !string.IsNullOrWhiteSpace(FacilityId);

    public bool IsProcessing => isProcessing;

    public int CurrentLevelIndex =>
        currentLevelIndex;

    public int CurrentLevelNumber =>
        currentLevelIndex + 1;

    public float ProcessingTimeRemaining =>
        processingTimeRemaining;

    public bool CanUpgrade =>
        levels != null &&
        levels.Length > 0 &&
        currentLevelIndex < levels.Length - 1;

    public FacilityLevelDefinition CurrentLevel =>
        GetCurrentLevel();

    public int NextUpgradeCost
    {
        get
        {
            if (!CanUpgrade)
                return 0;

            FacilityLevelDefinition nextLevel =
                levels[currentLevelIndex + 1];

            if (nextLevel == null)
                return 0;

            return Mathf.Max(
                0,
                nextLevel.upgradeCost
            );
        }
    }

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        CreateDefaultLevelsIfNeeded();
        ValidateCurrentLevel();
        ValidateFacilityId();
    }

    private void OnEnable()
    {
        ResumeProcessingIfNeeded();
    }

    private void OnDisable()
    {
        StopProcessingRoutine();
    }

    private void OnDestroy()
    {
        StopProcessingRoutine();
    }

    private void OnValidate()
    {
        CreateDefaultLevelsIfNeeded();
        ValidateCurrentLevel();
    }

    // ==================================================
    // TRASH ACCEPTANCE
    // ==================================================

    private void OnTriggerEnter(Collider other)
    {
        /*
         * Während der Verarbeitung wird kein zweiter
         * Müllsack angenommen oder zerstört.
         */
        if (isProcessing)
            return;

        if (other == null)
            return;

        Trash trash =
            other.GetComponentInParent<Trash>();

        TryAcceptTrash(trash);
    }

    public bool TryAcceptTrash(Trash trash)
    {
        if (isProcessing)
            return false;

        if (trash == null)
            return false;

        if (!IsTrashAccepted(trash.TrashClass))
            return false;

        FacilityLevelDefinition level =
            GetCurrentLevel();

        if (level == null)
        {
            Debug.LogWarning(
                "TrashFacility besitzt kein gültiges Facility-Level.",
                gameObject
            );

            return false;
        }

        float safeMultiplier =
            Mathf.Max(0f, level.valueMultiplier);

        processingBaseValue =
            Mathf.Max(0f, trash.BaseValue);

        processingPayoutValue =
            processingBaseValue * safeMultiplier;

        processingTimeRemaining =
            Mathf.Max(0.1f, level.processTime);

        /*
         * Sofort sperren, damit auch bei mehreren
         * Collidern nicht zwei Säcke im selben Frame
         * angenommen werden können.
         */
        isProcessing = true;

        Destroy(trash.gameObject);

        StartProcessingRoutine();

        return true;
    }

    private bool IsTrashAccepted(
        TrashClass trashClass
    )
    {
        return acceptedTrash != null &&
               acceptedTrash.Contains(trashClass);
    }

    // ==================================================
    // PROCESSING
    // ==================================================

    private IEnumerator ProcessTrash()
    {
        while (processingTimeRemaining > 0f)
        {
            processingTimeRemaining =
                Mathf.Max(
                    0f,
                    processingTimeRemaining -
                    Time.deltaTime
                );

            yield return null;
        }

        CompleteProcessing();
    }

    private IEnumerator CompleteProcessingNextFrame()
    {
        yield return null;

        CompleteProcessing();
    }

    private void StartProcessingRoutine()
    {
        StopProcessingRoutine();

        processingRoutine =
            StartCoroutine(ProcessTrash());
    }

    private void ResumeProcessingIfNeeded()
    {
        if (!isProcessing ||
            processingRoutine != null)
        {
            return;
        }

        processingRoutine =
            processingTimeRemaining > 0f
                ? StartCoroutine(ProcessTrash())
                : StartCoroutine(CompleteProcessingNextFrame());
    }

    private void StopProcessingRoutine()
    {
        if (processingRoutine == null)
            return;

        StopCoroutine(processingRoutine);
        processingRoutine = null;
    }

    private void CompleteProcessing()
    {
        float payoutToApply =
            Mathf.Max(0f, processingPayoutValue);

        SetIdleProcessingState();

        if (payoutToApply <= 0f)
            return;

        if (playerInventory != null)
        {
            playerInventory.AddCash(payoutToApply);
        }
        else
        {
            Debug.LogWarning(
                "TrashFacility konnte kein Geld auszahlen, " +
                "weil kein PlayerInventory eingetragen ist.",
                gameObject
            );
        }
    }

    private void SetIdleProcessingState()
    {
        processingRoutine = null;

        processingTimeRemaining = 0f;
        processingBaseValue = 0f;
        processingPayoutValue = 0f;
        isProcessing = false;
    }

    // ==================================================
    // SAVE DATA
    // ==================================================

    public FacilitySaveData CreateSaveData()
    {
        return new FacilitySaveData
        {
            facilityId = FacilityId,
            levelIndex = currentLevelIndex,
            isProcessing = isProcessing,
            processingTimeRemaining = isProcessing
                ? Mathf.Max(0f, processingTimeRemaining)
                : 0f,
            processingBaseValue = isProcessing
                ? Mathf.Max(0f, processingBaseValue)
                : 0f,
            processingPayoutValue = isProcessing
                ? Mathf.Max(0f, processingPayoutValue)
                : 0f
        };
    }

    public void ApplySaveData(
        FacilitySaveData saveData
    )
    {
        if (saveData == null)
            return;

        StopProcessingRoutine();
        ValidateCurrentLevel();

        if (!string.Equals(
                FacilityId,
                string.IsNullOrWhiteSpace(saveData.facilityId)
                    ? string.Empty
                    : saveData.facilityId.Trim(),
                StringComparison.Ordinal))
        {
            Debug.LogWarning(
                "Facility-Speicherdaten passen nicht zur Facility-ID. " +
                $"Scene-ID: '{FacilityId}', Save-ID: '{saveData.facilityId}'.",
                gameObject
            );

            return;
        }

        int loadedLevelIndex =
            saveData.levelIndex;

        currentLevelIndex =
            ClampLevelIndex(loadedLevelIndex);

        if (loadedLevelIndex != currentLevelIndex)
        {
            Debug.LogWarning(
                $"Facility '{FacilityId}' enthält ein ungültiges Level " +
                $"{loadedLevelIndex}. Verwendet wird Level {currentLevelIndex}.",
                gameObject
            );
        }

        if (!saveData.isProcessing)
        {
            SetIdleProcessingState();
            return;
        }

        float loadedBaseValue =
            Mathf.Max(0f, saveData.processingBaseValue);

        float loadedPayoutValue =
            Mathf.Max(0f, saveData.processingPayoutValue);

        if (loadedBaseValue <= 0f ||
            loadedPayoutValue <= 0f)
        {
            Debug.LogWarning(
                $"Facility '{FacilityId}' enthält unvollständige " +
                "Verarbeitungsdaten und wird als idle geladen.",
                gameObject
            );

            SetIdleProcessingState();
            return;
        }

        processingBaseValue =
            loadedBaseValue;

        processingPayoutValue =
            loadedPayoutValue;

        processingTimeRemaining =
            Mathf.Max(
                0f,
                saveData.processingTimeRemaining
            );

        isProcessing = true;

        ResumeProcessingIfNeeded();
    }

    private int ClampLevelIndex(
        int levelIndex
    )
    {
        if (levels == null ||
            levels.Length == 0)
        {
            return 0;
        }

        return Mathf.Clamp(
            levelIndex,
            0,
            levels.Length - 1
        );
    }

    // ==================================================
    // UPGRADES
    // ==================================================

    /*
     * Kann weiterhin direkt mit einem Unity-Button
     * verbunden werden.
     */
    public void Upgrade()
    {
        TryUpgrade();
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade)
        {
            Debug.Log(
                "Die Facility hat bereits das höchste Level.",
                gameObject
            );

            return false;
        }

        if (playerInventory == null)
        {
            Debug.LogWarning(
                "Facility-Upgrade nicht möglich: " +
                "PlayerInventory fehlt.",
                gameObject
            );

            return false;
        }

        int upgradeCost =
            NextUpgradeCost;

        if (upgradeCost > 0 &&
            !playerInventory.TrySpendCash(upgradeCost))
        {
            Debug.Log(
                $"Nicht genug Bargeld. " +
                $"Das Upgrade kostet ${upgradeCost:N0}.",
                gameObject
            );

            return false;
        }

        currentLevelIndex++;

        Debug.Log(
            $"Facility wurde auf Level " +
            $"{CurrentLevelNumber} verbessert.",
            gameObject
        );

        return true;
    }

    // ==================================================
    // REFERENCES
    // ==================================================

    public void SetPlayerInventory(
        PlayerInventory newInventory
    )
    {
        playerInventory = newInventory;
    }

    // ==================================================
    // LEVEL HELPERS
    // ==================================================

    private FacilityLevelDefinition GetCurrentLevel()
    {
        if (levels == null ||
            levels.Length == 0)
        {
            return null;
        }

        ValidateCurrentLevel();

        return levels[currentLevelIndex];
    }

    private void ValidateCurrentLevel()
    {
        currentLevelIndex =
            ClampLevelIndex(currentLevelIndex);
    }

    private void ValidateFacilityId()
    {
        if (HasValidFacilityId)
            return;

        Debug.LogWarning(
            "TrashFacility besitzt keine gültige facilityId. " +
            "Diese Facility wird nicht gespeichert oder geladen, " +
            "bis im Inspector eine stabile ID gesetzt ist.",
            gameObject
        );
    }

    private void CreateDefaultLevelsIfNeeded()
    {
        if (levels == null ||
            levels.Length < 3)
        {
            Array.Resize(ref levels, 3);
        }

        if (levels[0] == null)
        {
            levels[0] =
                new FacilityLevelDefinition
                {
                    levelName = "Level 1",
                    processTime = 300f,
                    valueMultiplier = 1f,
                    upgradeCost = 0
                };
        }

        if (levels[1] == null)
        {
            levels[1] =
                new FacilityLevelDefinition
                {
                    levelName = "Level 2",
                    processTime = 150f,
                    valueMultiplier = 2.5f,
                    upgradeCost = 1000
                };
        }

        if (levels[2] == null)
        {
            levels[2] =
                new FacilityLevelDefinition
                {
                    levelName = "Level 3",
                    processTime = 60f,
                    valueMultiplier = 3.5f,
                    upgradeCost = 5000
                };
        }
    }
}