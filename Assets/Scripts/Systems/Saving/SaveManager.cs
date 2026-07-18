using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum SaveSlotState
{
    Empty,
    Valid,
    Recoverable,
    Corrupted
}

public readonly struct SaveSlotStatus
{
    public SaveSlotStatus(
        SaveSlotState state,
        string displayName,
        string sourcePath
    )
    {
        State = state;
        DisplayName = displayName ?? string.Empty;
        SourcePath = sourcePath ?? string.Empty;
        CanLoad =
            state == SaveSlotState.Valid ||
            state == SaveSlotState.Recoverable;
        CanDelete =
            state != SaveSlotState.Empty;
    }

    public SaveSlotState State { get; }
    public string DisplayName { get; }
    public string SourcePath { get; }
    public bool CanLoad { get; }
    public bool CanDelete { get; }
}

[DisallowMultipleComponent]
public sealed class SaveManager : MonoBehaviour
{
    // ==================================================
    // SINGLETON
    // ==================================================

    public static SaveManager Instance { get; private set; }

    // ==================================================
    // SETTINGS
    // ==================================================

    [Header("Scene")]
    [SerializeField]
    private string gameSceneName = "GameScene";

    [Header("Save Files")]
    [SerializeField]
    private string saveFolderName = "Saves";

    [SerializeField]
    private string saveFilePrefix = "save_slot_";

    [Header("Automatic Loading")]
    [SerializeField]
    private bool autoLoadCurrentSlotInGameScene = true;

    // ==================================================
    // CURRENT SAVE
    // ==================================================

    public SaveGameData CurrentSave { get; private set; }

#if UNITY_EDITOR
    private static string saveDirectoryPathOverrideForTests =
        string.Empty;

    public static void SetSaveDirectoryPathOverrideForTests(
        string path
    )
    {
        saveDirectoryPathOverrideForTests =
            string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim();
    }

    public static void ClearSaveDirectoryPathOverrideForTests()
    {
        saveDirectoryPathOverrideForTests = string.Empty;
    }
#endif

    public string SaveDirectoryPath
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrWhiteSpace(
                    saveDirectoryPathOverrideForTests))
            {
                return saveDirectoryPathOverrideForTests;
            }
#endif

            return Path.Combine(
                Application.persistentDataPath,
                saveFolderName
            );
        }
    }

    private enum SaveCandidateKind
    {
        Main,
        Backup,
        Temporary,
        WriteTemporary
    }

    private readonly struct SaveCandidate
    {
        public SaveCandidate(
            SaveCandidateKind kind,
            string path,
            SaveGameData data,
            DateTime savedAtUtc
        )
        {
            Kind = kind;
            Path = path ?? string.Empty;
            Data = data;
            SavedAtUtc = savedAtUtc;
        }

        public SaveCandidateKind Kind { get; }
        public string Path { get; }
        public SaveGameData Data { get; }
        public DateTime SavedAtUtc { get; }
    }

    private int recoveryLoadedSlotIndex = -1;
    private string recoveryLoadedSourcePath = string.Empty;

    // ==================================================
    // WORLD TRASH DEFAULTS
    // ==================================================

    /*
     * Speichert den ursprünglichen Zustand aller
     * WorldTrashSaveObjects direkt nach dem Szenenladen.
     *
     * Dadurch kann später erkannt werden, welcher Müll
     * seitdem verarbeitet oder entfernt wurde.
     */
    private readonly Dictionary<string, WorldTrashSaveData>
        sceneWorldTrashDefaults =
            new Dictionary<string, WorldTrashSaveData>(
                StringComparer.Ordinal
            );

    // ==================================================
    // EVENTS
    // ==================================================

    public event Action<int> GameSaved;
    public event Action<int> GameLoaded;
    public event Action<int> SaveDeleted;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        EnsureSaveDirectoryExists();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ==================================================
    // SCENE LOADING
    // ==================================================

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode loadMode
    )
    {
        if (!string.Equals(
                scene.name,
                gameSceneName,
                StringComparison.Ordinal))
        {
            sceneWorldTrashDefaults.Clear();
            return;
        }

        int slotIndex =
            GameManager.Instance != null
                ? GameManager.Instance.CurrentSlot
                : -1;

        StartCoroutine(
            PrepareLoadedGameScene(slotIndex)
        );
    }

    private IEnumerator PrepareLoadedGameScene(
        int slotIndex
    )
    {
        /*
         * Einen Frame warten, damit alle Objekte
         * der GameScene vollständig erstellt wurden.
         */
        yield return null;

        CacheSceneWorldTrashDefaults();

        const int maximumWaitFrames = 120;

        for (int frame = 0;
             frame < maximumWaitFrames;
             frame++)
        {
            PlayerInventory inventory =
                FindFirstObjectByType<PlayerInventory>();

            if (inventory != null)
                break;

            yield return null;
        }

        /*
         * Noch einen zusätzlichen Frame warten,
         * damit Awake, OnEnable und Start fertig sind.
         */
        yield return null;

        if (!autoLoadCurrentSlotInGameScene)
            yield break;

        if (!IsValidSlotIndex(slotIndex))
            yield break;

        if (!HasSave(slotIndex))
            yield break;

        LoadGame(
            slotIndex,
            true
        );
    }

    // ==================================================
    // WORLD TRASH DEFAULT CACHE
    // ==================================================

    private void CacheSceneWorldTrashDefaults()
    {
        WorldTrashSaveObject[] worldTrashObjects =
            FindObjectsByType<WorldTrashSaveObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (!TryBuildWorldTrashLookup(
                worldTrashObjects,
                "Cache",
                out Dictionary<string, WorldTrashSaveObject>
                    worldTrashById))
        {
            Debug.LogError(
                "WorldTrash-Default-Cache wurde abgebrochen, " +
                "weil mindestens eine World Object ID fehlt " +
                "oder doppelt vergeben ist.",
                gameObject
            );

            return;
        }

        Dictionary<string, WorldTrashSaveData> defaults =
            new Dictionary<string, WorldTrashSaveData>(
                StringComparer.Ordinal
            );

        foreach (KeyValuePair<string, WorldTrashSaveObject> entry in
                 worldTrashById)
        {
            defaults.Add(
                entry.Key,
                entry.Value.CreateSaveData(true)
            );
        }

        sceneWorldTrashDefaults.Clear();

        foreach (KeyValuePair<string, WorldTrashSaveData> entry in
                 defaults)
        {
            sceneWorldTrashDefaults.Add(
                entry.Key,
                entry.Value
            );
        }

        Debug.Log(
            $"{sceneWorldTrashDefaults.Count} Müllobjekte " +
            "wurden für das Weltspeichern registriert.",
            gameObject
        );
    }

    // ==================================================
    // SAVE INFORMATION
    // ==================================================

    public SaveSlotStatus GetSaveSlotStatus(
        int slotIndex
    )
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            return new SaveSlotStatus(
                SaveSlotState.Empty,
                "Empty",
                string.Empty
            );
        }

        bool anyCandidateFileExists = false;

        List<SaveCandidate> validCandidates =
            new List<SaveCandidate>();

        TryAddValidCandidate(
            validCandidates,
            SaveCandidateKind.Main,
            GetSaveFilePath(slotIndex),
            slotIndex,
            ref anyCandidateFileExists
        );

        TryAddValidCandidate(
            validCandidates,
            SaveCandidateKind.Backup,
            GetBackupSaveFilePath(slotIndex),
            slotIndex,
            ref anyCandidateFileExists
        );

        TryAddValidCandidate(
            validCandidates,
            SaveCandidateKind.Temporary,
            GetTemporarySaveFilePath(slotIndex),
            slotIndex,
            ref anyCandidateFileExists
        );

        TryAddValidCandidate(
            validCandidates,
            SaveCandidateKind.WriteTemporary,
            GetWriteTemporarySaveFilePath(slotIndex),
            slotIndex,
            ref anyCandidateFileExists
        );

        if (!anyCandidateFileExists)
        {
            return new SaveSlotStatus(
                SaveSlotState.Empty,
                "Empty",
                string.Empty
            );
        }

        if (validCandidates.Count == 0)
        {
            return new SaveSlotStatus(
                SaveSlotState.Corrupted,
                "Corrupted Save",
                string.Empty
            );
        }

        bool hasValidMainSave =
            TryGetCandidate(
                validCandidates,
                SaveCandidateKind.Main,
                out SaveCandidate mainCandidate
            );

        if (hasValidMainSave)
        {
            SaveCandidate selectedCandidate =
                mainCandidate;

            bool hasNewerRecoveryCandidate = false;

            foreach (SaveCandidate candidate in validCandidates)
            {
                if (candidate.Kind == SaveCandidateKind.Main)
                    continue;

                if (candidate.SavedAtUtc <=
                    mainCandidate.SavedAtUtc)
                {
                    continue;
                }

                if (!hasNewerRecoveryCandidate ||
                    candidate.SavedAtUtc >
                    selectedCandidate.SavedAtUtc)
                {
                    selectedCandidate = candidate;
                    hasNewerRecoveryCandidate = true;
                }
            }

            if (!hasNewerRecoveryCandidate)
            {
                return CreateStatus(
                    SaveSlotState.Valid,
                    mainCandidate,
                    slotIndex
                );
            }

            return CreateStatus(
                SaveSlotState.Recoverable,
                selectedCandidate,
                slotIndex
            );
        }

        SaveCandidate newestCandidate =
            GetNewestCandidate(validCandidates);

        return CreateStatus(
            SaveSlotState.Recoverable,
            newestCandidate,
            slotIndex
        );
    }

    public bool HasSave(int slotIndex)
    {
        return GetSaveSlotStatus(slotIndex).CanLoad;
    }

    public bool HasAnySaveFiles(int slotIndex)
    {
        return GetSaveSlotStatus(slotIndex).CanDelete;
    }

    public string GetSaveName(int slotIndex)
    {
        SaveSlotStatus status =
            GetSaveSlotStatus(slotIndex);

        if (status.State == SaveSlotState.Empty)
            return "Empty";

        return string.IsNullOrWhiteSpace(status.DisplayName)
            ? $"Save {slotIndex + 1}"
            : status.DisplayName;
    }

    public SaveGameData GetSaveData(
        int slotIndex
    )
    {
        return ReadSaveFile(slotIndex);
    }

    // ==================================================
    // CREATE NEW SAVE
    // ==================================================

    public bool CreateNewSave(
        int slotIndex,
        string saveName,
        bool overwriteExisting = false
    )
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        SaveSlotStatus status =
            GetSaveSlotStatus(slotIndex);

        if ((status.State == SaveSlotState.Recoverable ||
             status.State == SaveSlotState.Corrupted) &&
            overwriteExisting)
        {
            Debug.LogError(
                $"Speicherplatz {slotIndex + 1} wird nicht ueberschrieben, " +
                "weil er Recovery- oder Corruption-Daten enthaelt. " +
                "Loesche den Slot zuerst bewusst.",
                gameObject
            );

            return false;
        }

        if (status.CanDelete &&
            !overwriteExisting)
        {
            Debug.LogWarning(
                $"Auf Speicherplatz {slotIndex + 1} " +
                "existiert bereits ein Spielstand oder eine Save-Datei.",
                gameObject
            );

            return false;
        }

        SaveGameData newSave =
            SaveGameData.CreateNew(
                slotIndex,
                saveName
            );

        bool wasWritten =
            WriteSaveFile(
                slotIndex,
                newSave
            );

        if (!wasWritten)
            return false;

        CurrentSave = newSave;
        ClearRecoveryLoadAuthorization(slotIndex);

        return true;
    }

    // ==================================================
    // SET PLAYER CHARACTER
    // ==================================================

    public bool SetPlayerCharacterId(
        int slotIndex,
        string characterId
    )
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            Debug.LogWarning(
                $"Ungueltiger Speicherplatz fuer Character-Auswahl: " +
                $"{slotIndex}",
                gameObject
            );

            return false;
        }

        string normalizedCharacterId =
            string.IsNullOrWhiteSpace(characterId)
                ? string.Empty
                : characterId.Trim();

        if (string.IsNullOrWhiteSpace(
                normalizedCharacterId))
        {
            Debug.LogWarning(
                $"Character-Auswahl fuer Slot {slotIndex + 1} " +
                "konnte nicht gespeichert werden, weil die " +
                "characterId leer ist.",
                gameObject
            );

            return false;
        }

        if (!CanWriteExistingSaveSlot(
                slotIndex,
                out SaveSlotStatus status))
        {
            return false;
        }

        SaveGameData saveData =
            ReadSaveFile(status, slotIndex, true);

        if (saveData == null)
        {
            Debug.LogError(
                $"Character-Auswahl fuer Slot {slotIndex + 1} " +
                "konnte nicht gespeichert werden, weil die " +
                "Save-Datei nicht gelesen werden konnte.",
                gameObject
            );

            return false;
        }

        EnsureSaveDataSectionsExist(
            saveData
        );

        saveData.player.characterId =
            normalizedCharacterId;

        bool wasWritten =
            WriteSaveFile(
                slotIndex,
                saveData
            );

        if (!wasWritten)
            return false;

        if (CurrentSave == null ||
            CurrentSave.slotIndex == slotIndex)
        {
            CurrentSave = saveData;
        }

        Debug.Log(
            $"Character-Auswahl '{normalizedCharacterId}' " +
            $"wurde fuer Slot {slotIndex + 1} gespeichert.",
            gameObject
        );

        return true;
    }


    // ==================================================
    // SET PLAYER APPEARANCE
    // ==================================================

    public bool SetPlayerAppearance(
        int slotIndex,
        CharacterAppearanceData appearance
    )
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            Debug.LogWarning(
                $"Ungueltiger Speicherplatz fuer Appearance-Auswahl: " +
                $"{slotIndex}",
                gameObject
            );

            return false;
        }

        if (appearance == null)
        {
            Debug.LogWarning(
                $"Appearance fuer Slot {slotIndex + 1} konnte nicht " +
                "gespeichert werden, weil die Daten fehlen.",
                gameObject
            );

            return false;
        }

        CharacterAppearanceData appearanceCopy =
            appearance.Clone();

        if (string.IsNullOrEmpty(appearanceCopy.bodyTypeId) ||
            string.IsNullOrEmpty(appearanceCopy.skinId))
        {
            Debug.LogWarning(
                $"Appearance fuer Slot {slotIndex + 1} konnte nicht " +
                "gespeichert werden, weil bodyTypeId oder skinId fehlen.",
                gameObject
            );

            return false;
        }

        if (!CanWriteExistingSaveSlot(
                slotIndex,
                out SaveSlotStatus status))
        {
            return false;
        }

        SaveGameData saveData =
            ReadSaveFile(status, slotIndex, true);

        if (saveData == null)
        {
            Debug.LogError(
                $"Appearance fuer Slot {slotIndex + 1} konnte nicht " +
                "gespeichert werden, weil die Save-Datei nicht gelesen " +
                "werden konnte.",
                gameObject
            );

            return false;
        }

        EnsureSaveDataSectionsExist(
            saveData
        );

        appearanceCopy.appearanceVersion =
            CharacterAppearanceData.CurrentVersion;

        saveData.player.appearance =
            appearanceCopy.Clone();

        saveData.saveVersion =
            SaveGameData.CurrentSaveVersion;

        saveData.UpdateLastSavedTime();

        bool wasWritten =
            WriteSaveFile(
                slotIndex,
                saveData
            );

        if (!wasWritten)
            return false;

        if (CurrentSave == null ||
            CurrentSave.slotIndex == slotIndex)
        {
            CurrentSave = saveData;
        }

        Debug.Log(
            $"Appearance wurde fuer Slot {slotIndex + 1} gespeichert.",
            gameObject
        );

        return true;
    }

    // ==================================================
    // SAVE GAME
    // ==================================================

    public bool SaveCurrentGame()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogWarning(
                "SaveManager findet keinen GameManager.",
                gameObject
            );

            return false;
        }

        return SaveGame(
            GameManager.Instance.CurrentSlot
        );
    }

    public bool SaveGame(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            Debug.LogWarning(
                $"Ungueltiger Speicherplatz: {slotIndex}",
                gameObject
            );

            return false;
        }

        SaveSlotStatus status =
            GetSaveSlotStatus(slotIndex);

        SaveGameData saveData;

        if (status.State == SaveSlotState.Empty)
        {
            saveData =
                SaveGameData.CreateNew(
                    slotIndex,
                    $"Save {slotIndex + 1}"
                );
        }
        else if (status.State == SaveSlotState.Valid)
        {
            saveData =
                ReadSaveFile(status, slotIndex, true);
        }
        else if (status.State == SaveSlotState.Recoverable)
        {
            if (!IsRecoverySaveAuthorized(
                    slotIndex,
                    status.SourcePath))
            {
                Debug.LogError(
                    $"Speichern von Slot {slotIndex + 1} wurde blockiert, " +
                    "weil der Slot Recovery-Daten enthaelt, aber in " +
                    "dieser Session nicht bewusst daraus geladen wurde.",
                    gameObject
                );

                return false;
            }

            saveData =
                CurrentSave != null &&
                CurrentSave.slotIndex == slotIndex
                    ? CurrentSave
                    : ReadSaveFile(status, slotIndex, true);
        }
        else
        {
            Debug.LogError(
                $"Speichern von Slot {slotIndex + 1} wurde blockiert, " +
                "weil der Slot beschaedigte Save-Dateien enthaelt.",
                gameObject
            );

            return false;
        }

        if (saveData == null)
        {
            Debug.LogError(
                $"Speichern von Slot {slotIndex + 1} wurde abgebrochen, " +
                "weil kein gueltiger Ausgangs-Save gelesen werden konnte.",
                gameObject
            );

            return false;
        }

        EnsureSaveDataSectionsExist(
            saveData
        );

        saveData.saveVersion = SaveGameData.CurrentSaveVersion;
        saveData.slotIndex = slotIndex;

        if (!CaptureCurrentSceneData(
                saveData))
        {
            return false;
        }

        saveData.UpdateLastSavedTime();

        bool wasRecoveryRepair =
            status.State == SaveSlotState.Recoverable &&
            IsRecoverySaveAuthorized(
                slotIndex,
                status.SourcePath
            );

        bool wasWritten =
            WriteSaveFile(
                slotIndex,
                saveData
            );

        if (!wasWritten)
            return false;

        CurrentSave = saveData;

        if (wasRecoveryRepair)
        {
            ClearRecoveryLoadAuthorization(slotIndex);

            Debug.Log(
                $"Recovery-Save fuer Slot {slotIndex + 1} wurde " +
                "erfolgreich als gueltiger Hauptsave gespeichert.",
                gameObject
            );
        }

        GameSaved?.Invoke(slotIndex);

        Debug.Log(
            $"Spielstand {slotIndex + 1} wurde gespeichert.\n" +
            GetSaveFilePath(slotIndex),
            gameObject
        );

        return true;
    }

    // ==================================================
    // CAPTURE CURRENT SCENE
    // ==================================================

    private bool CaptureCurrentSceneData(
        SaveGameData saveData
    )
    {
        if (saveData == null)
            return false;

        EnsureSaveDataSectionsExist(
            saveData
        );

        PlayerController playerController =
            FindFirstObjectByType<PlayerController>();

        PlayerInventory inventory =
            playerController != null
                ? playerController.Inventory
                : FindFirstObjectByType<PlayerInventory>();

        Transform playerTransform =
            playerController != null
                ? playerController.transform
                : inventory != null
                    ? inventory.transform
                    : null;

        CapturePlayerData(
            saveData,
            inventory,
            playerTransform
        );

        CaptureSharedBankData(
            saveData
        );

        CaptureFacilityData(
            saveData
        );

        return CaptureWorldTrashData(
            saveData
        );
    }

    // ==================================================
    // CAPTURE PLAYER
    // ==================================================

    private void CapturePlayerData(
        SaveGameData saveData,
        PlayerInventory inventory,
        Transform playerTransform
    )
    {
        if (inventory != null)
        {
            saveData.player.personalCash =
                inventory.GetCashForSave();

            saveData.player.selectedHotbarSlot =
                inventory.selectedSlot;

            CaptureHotbarData(
                inventory,
                saveData.player
            );
        }
        else
        {
            Debug.LogWarning(
                "Beim Speichern wurde kein PlayerInventory gefunden.",
                gameObject
            );
        }

        if (playerTransform != null)
        {
            saveData.player.position.Set(
                playerTransform.position
            );

            saveData.player.rotation.Set(
                playerTransform.eulerAngles
            );
        }
        else
        {
            Debug.LogWarning(
                "Beim Speichern wurde kein Player Transform gefunden.",
                gameObject
            );
        }
    }

    private void CaptureHotbarData(
        PlayerInventory inventory,
        PlayerSaveData playerData
    )
    {
        if (inventory == null ||
            playerData == null)
        {
            return;
        }

        if (playerData.hotbarSlots == null)
        {
            playerData.hotbarSlots =
                new List<HotbarSlotSaveData>();
        }

        playerData.hotbarSlots.Clear();

        for (int i = 0;
             i < inventory.SlotCount;
             i++)
        {
            HotbarItem item =
                inventory.GetItem(i);

            if (item == null ||
                !item.IsValid)
            {
                continue;
            }

            playerData.hotbarSlots.Add(
                new HotbarSlotSaveData
                {
                    slotIndex = i,
                    itemId = item.ItemId,
                    amount = 1
                }
            );
        }
    }

    // ==================================================
    // CAPTURE SHARED BANK
    // ==================================================

    private void CaptureSharedBankData(
        SaveGameData saveData
    )
    {
        SharedBankAccount bankAccount =
            SharedBankAccount.Instance;

        if (bankAccount == null)
        {
            bankAccount =
                FindFirstObjectByType<SharedBankAccount>();
        }

        if (bankAccount == null)
        {
            Debug.LogWarning(
                "Beim Speichern wurde kein SharedBankAccount gefunden.",
                gameObject
            );

            return;
        }

        saveData.sharedWorld.sharedBankBalance =
            bankAccount.GetBalanceForSave();
    }

    // ==================================================
    // CAPTURE FACILITIES
    // ==================================================

    private void CaptureFacilityData(
        SaveGameData saveData
    )
    {
        TrashFacility[] sceneFacilities =
            FindObjectsByType<TrashFacility>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (sceneFacilities == null ||
            sceneFacilities.Length == 0)
        {
            return;
        }

        if (!TryCollectSceneFacilitiesById(
                sceneFacilities,
                "Speichern",
                out Dictionary<string, TrashFacility> facilitiesById))
        {
            Debug.LogError(
                "Facility-Snapshot wurde nicht gespeichert, " +
                "weil mindestens eine Facility keine eindeutige facilityId besitzt.",
                gameObject
            );

            return;
        }

        if (facilitiesById.Count == 0)
            return;

        List<FacilitySaveData> capturedFacilities =
            new List<FacilitySaveData>();

        foreach (KeyValuePair<string, TrashFacility> entry in
                 facilitiesById)
        {
            if (entry.Value == null)
                continue;

            capturedFacilities.Add(
                entry.Value.CreateSaveData()
            );
        }

        capturedFacilities.Sort(
            (left, right) => string.Compare(
                left != null ? left.facilityId : string.Empty,
                right != null ? right.facilityId : string.Empty,
                StringComparison.Ordinal
            )
        );

        saveData.sharedWorld.facilities =
            capturedFacilities;

        saveData.sharedWorld.facilitiesSnapshotInitialized =
            true;
    }
    // ==================================================
    // CAPTURE WORLD TRASH
    // ==================================================

    private bool CaptureWorldTrashData(
        SaveGameData saveData
    )
    {
        string activeSceneName =
            SceneManager.GetActiveScene().name;

        WorldTrashSaveObject[] currentWorldTrash =
            FindObjectsByType<WorldTrashSaveObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (!TryBuildWorldTrashLookup(
                currentWorldTrash,
                "Capture",
                out Dictionary<string, WorldTrashSaveObject>
                    worldTrashById))
        {
            Debug.LogError(
                "WorldTrash-Snapshot wurde nicht gespeichert, " +
                "weil mindestens eine World Object ID fehlt " +
                "oder doppelt vergeben ist.",
                gameObject
            );

            return false;
        }

        Dictionary<string, WorldTrashSaveData> savedRecords =
            new Dictionary<string, WorldTrashSaveData>(
                StringComparer.Ordinal
            );

        /*
         * Bereits vorhandene Records übernehmen.
         * Das ist später auch bei mehreren Szenen wichtig.
         */
        if (saveData.sharedWorld.worldTrashObjects != null)
        {
            foreach (WorldTrashSaveData existingRecord in
                     saveData.sharedWorld.worldTrashObjects)
            {
                if (existingRecord == null ||
                    string.IsNullOrWhiteSpace(
                        existingRecord.worldObjectId))
                {
                    continue;
                }

                savedRecords[existingRecord.worldObjectId] =
                    existingRecord;
            }
        }

        HashSet<string> currentWorldObjectIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        /*
         * Alle Müllobjekte speichern,
         * die aktuell noch existieren.
         */
        foreach (KeyValuePair<string, WorldTrashSaveObject> entry in
                 worldTrashById)
        {
            WorldTrashSaveObject worldTrash =
                entry.Value;

            string worldObjectId =
                entry.Key;

            currentWorldObjectIds.Add(
                worldObjectId
            );

            savedRecords[worldObjectId] =
                worldTrash.CreateSaveData(true);
        }

        /*
         * Alles, was beim Szenenstart existierte,
         * jetzt aber fehlt, wurde verarbeitet oder entfernt.
         */
        foreach (KeyValuePair<string, WorldTrashSaveData>
                 defaultEntry in sceneWorldTrashDefaults)
        {
            string worldObjectId =
                defaultEntry.Key;

            WorldTrashSaveData defaultData =
                defaultEntry.Value;

            if (defaultData == null ||
                !string.Equals(
                    defaultData.sceneName,
                    activeSceneName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (currentWorldObjectIds.Contains(
                    worldObjectId))
            {
                continue;
            }

            savedRecords[worldObjectId] =
                new WorldTrashSaveData
                {
                    worldObjectId =
                        worldObjectId,

                    trashTypeId =
                        defaultData.trashTypeId,

                    sceneName =
                        defaultData.sceneName,

                    exists =
                        false,

                    position =
                        defaultData.position,

                    rotation =
                        defaultData.rotation
                };

            Debug.Log(
                $"Müllobjekt wurde als entfernt gespeichert: " +
                $"{worldObjectId}",
                gameObject
            );
        }

        saveData.sharedWorld.worldTrashObjects =
            new List<WorldTrashSaveData>(
                savedRecords.Values
            );

        saveData.sharedWorld
            .worldTrashSnapshotInitialized = true;

        return true;
    }

    // ==================================================
    // LOAD GAME
    // ==================================================

    public bool LoadGame(
        int slotIndex,
        bool applyToCurrentScene
    )
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        SaveSlotStatus status =
            GetSaveSlotStatus(slotIndex);

        if (!status.CanLoad)
        {
            Debug.LogError(
                $"Spielstand {slotIndex + 1} konnte nicht geladen werden. " +
                $"Status: {status.State}.\n" +
                GetSaveCandidateFailureSummary(slotIndex),
                gameObject
            );

            ClearRecoveryLoadAuthorization(slotIndex);
            return false;
        }

        SaveGameData saveData =
            ReadSaveFile(status, slotIndex, true);

        if (saveData == null)
        {
            Debug.LogWarning(
                $"Spielstand {slotIndex + 1} " +
                "konnte nicht geladen werden.",
                gameObject
            );

            ClearRecoveryLoadAuthorization(slotIndex);
            return false;
        }

        EnsureSaveDataSectionsExist(
            saveData
        );

        CurrentSave = saveData;

        if (status.State == SaveSlotState.Recoverable)
        {
            recoveryLoadedSlotIndex = slotIndex;
            recoveryLoadedSourcePath = status.SourcePath;

            Debug.Log(
                $"Spielstand {slotIndex + 1} wurde aus einem " +
                $"Recovery-Kandidaten geladen:\n{status.SourcePath}",
                gameObject
            );
        }
        else
        {
            ClearRecoveryLoadAuthorization(slotIndex);
        }

        if (applyToCurrentScene)
        {
            ApplySaveDataToCurrentScene(
                saveData
            );
        }

        GameLoaded?.Invoke(slotIndex);

        Debug.Log(
            $"Spielstand {slotIndex + 1} wurde geladen.",
            gameObject
        );

        return true;
    }

    // ==================================================
    // APPLY SAVE DATA
    // ==================================================

    private void ApplySaveDataToCurrentScene(
        SaveGameData saveData
    )
    {
        if (saveData == null)
            return;

        PlayerController playerController =
            FindFirstObjectByType<PlayerController>();

        PlayerInventory inventory =
            playerController != null
                ? playerController.Inventory
                : FindFirstObjectByType<PlayerInventory>();

        Transform playerTransform =
            playerController != null
                ? playerController.transform
                : inventory != null
                    ? inventory.transform
                    : null;

        ApplyPlayerData(
            saveData,
            inventory,
            playerTransform
        );

        ApplySharedBankData(
            saveData
        );

        ApplyFacilityData(
            saveData
        );

        ApplyWorldTrashData(
            saveData
        );
    }

    // ==================================================
    // APPLY PLAYER
    // ==================================================

    private void ApplyPlayerData(
        SaveGameData saveData,
        PlayerInventory inventory,
        Transform playerTransform
    )
    {
        if (inventory != null)
        {
            inventory.LoadCash(
                saveData.player.personalCash
            );

            ApplyHotbarData(
                saveData,
                inventory
            );

            inventory.SetSelectedSlot(
                saveData.player.selectedHotbarSlot
            );
        }
        else
        {
            Debug.LogWarning(
                "Beim Laden wurde kein PlayerInventory gefunden.",
                gameObject
            );
        }

        if (playerTransform != null)
        {
            ApplyPlayerTransform(
                playerTransform,
                saveData.player.position.ToVector3(),
                saveData.player.rotation.ToVector3()
            );
        }
        else
        {
            Debug.LogWarning(
                "Beim Laden wurde kein Player Transform gefunden.",
                gameObject
            );
        }
    }

    private void ApplyHotbarData(
        SaveGameData saveData,
        PlayerInventory inventory
    )
    {
        if (saveData == null ||
            saveData.player == null ||
            inventory == null)
        {
            return;
        }

        if (saveData.saveVersion < 2)
            return;

        List<HotbarSlotSaveData> savedHotbarSlots =
            saveData.player.hotbarSlots;

        if (savedHotbarSlots == null)
        {
            Debug.LogWarning(
                $"Spielstand {saveData.slotIndex + 1} enthält keine " +
                "Hotbar-Daten. Die aktuelle Hotbar bleibt unverändert.",
                gameObject
            );

            return;
        }

        inventory.ClearHotbarForLoad();

        HashSet<int> restoredSlotIndices =
            new HashSet<int>();

        foreach (HotbarSlotSaveData savedSlot in
                 savedHotbarSlots)
        {
            if (savedSlot == null)
            {
                Debug.LogWarning(
                    $"Spielstand {saveData.slotIndex + 1} enthält " +
                    "einen leeren Hotbar-Eintrag.",
                    gameObject
                );

                continue;
            }

            if (savedSlot.slotIndex < 0 ||
                savedSlot.slotIndex >= inventory.SlotCount)
            {
                Debug.LogWarning(
                    $"Hotbar-Slot {savedSlot.slotIndex} aus " +
                    $"Spielstand {saveData.slotIndex + 1} ist ungültig " +
                    "und wird übersprungen.",
                    gameObject
                );

                continue;
            }

            if (savedSlot.amount <= 0)
            {
                Debug.LogWarning(
                    $"Hotbar-Slot {savedSlot.slotIndex} aus " +
                    $"Spielstand {saveData.slotIndex + 1} hat eine " +
                    $"ungültige Menge: {savedSlot.amount}.",
                    gameObject
                );

                continue;
            }

            if (restoredSlotIndices.Contains(
                    savedSlot.slotIndex))
            {
                Debug.LogWarning(
                    $"Hotbar-Slot {savedSlot.slotIndex} kommt in " +
                    $"Spielstand {saveData.slotIndex + 1} mehrfach vor. " +
                    "Der erste gültige Eintrag bleibt erhalten.",
                    gameObject
                );

                continue;
            }

            bool wasLoaded =
                inventory.TryLoadKnownItemIntoSlot(
                    savedSlot.slotIndex,
                    savedSlot.itemId
                );

            if (!wasLoaded)
                continue;

            restoredSlotIndices.Add(
                savedSlot.slotIndex
            );
        }

        inventory.NotifyHotbarLoadCompleted();
    }

    private void ApplyPlayerTransform(
        Transform playerTransform,
        Vector3 savedPosition,
        Vector3 savedRotation
    )
    {
        if (playerTransform == null)
            return;

        CharacterController characterController =
            playerTransform.GetComponent<CharacterController>();

        bool controllerWasEnabled =
            characterController != null &&
            characterController.enabled;

        if (controllerWasEnabled)
            characterController.enabled = false;

        playerTransform.SetPositionAndRotation(
            savedPosition,
            Quaternion.Euler(savedRotation)
        );

        if (controllerWasEnabled)
            characterController.enabled = true;
    }

    // ==================================================
    // APPLY SHARED BANK
    // ==================================================

    private void ApplySharedBankData(
        SaveGameData saveData
    )
    {
        SharedBankAccount bankAccount =
            SharedBankAccount.Instance;

        if (bankAccount == null)
        {
            bankAccount =
                FindFirstObjectByType<SharedBankAccount>();
        }

        if (bankAccount == null)
        {
            Debug.LogWarning(
                "Beim Laden wurde kein SharedBankAccount gefunden.",
                gameObject
            );

            return;
        }

        bankAccount.LoadBalance(
            saveData.sharedWorld.sharedBankBalance
        );
    }

    // ==================================================
    // APPLY FACILITIES
    // ==================================================

    private void ApplyFacilityData(
        SaveGameData saveData
    )
    {
        if (!saveData.sharedWorld.facilitiesSnapshotInitialized)
            return;

        List<FacilitySaveData> savedFacilities =
            saveData.sharedWorld.facilities;

        if (savedFacilities == null)
            return;

        TrashFacility[] sceneFacilities =
            FindObjectsByType<TrashFacility>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (!TryCollectSceneFacilitiesById(
                sceneFacilities,
                "Laden",
                out Dictionary<string, TrashFacility> facilitiesById))
        {
            Debug.LogError(
                "Facility-Snapshot wurde nicht angewendet, " +
                "weil mindestens eine Scene-Facility keine eindeutige facilityId besitzt.",
                gameObject
            );

            return;
        }

        Dictionary<string, FacilitySaveData> recordsById =
            new Dictionary<string, FacilitySaveData>(
                StringComparer.Ordinal
            );

        foreach (FacilitySaveData savedFacility in
                 savedFacilities)
        {
            if (savedFacility == null)
                continue;

            string facilityId =
                string.IsNullOrWhiteSpace(savedFacility.facilityId)
                    ? string.Empty
                    : savedFacility.facilityId.Trim();

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                Debug.LogWarning(
                    $"Spielstand {saveData.slotIndex + 1} enthält " +
                    "einen Facility-Eintrag ohne facilityId.",
                    gameObject
                );

                continue;
            }

            if (recordsById.ContainsKey(facilityId))
            {
                Debug.LogWarning(
                    $"Spielstand {saveData.slotIndex + 1} enthält " +
                    $"die Facility-ID '{facilityId}' mehrfach. " +
                    "Der erste Eintrag bleibt erhalten.",
                    gameObject
                );

                continue;
            }

            recordsById.Add(
                facilityId,
                savedFacility
            );
        }

        foreach (KeyValuePair<string, FacilitySaveData> entry in
                 recordsById)
        {
            if (facilitiesById.ContainsKey(entry.Key))
                continue;

            Debug.LogWarning(
                $"Spielstand {saveData.slotIndex + 1} enthält " +
                $"Facility-ID '{entry.Key}', aber in der aktuellen Szene " +
                "wurde keine passende TrashFacility gefunden.",
                gameObject
            );
        }

        foreach (KeyValuePair<string, TrashFacility> entry in
                 facilitiesById)
        {
            if (entry.Value == null)
                continue;

            if (!recordsById.TryGetValue(
                    entry.Key,
                    out FacilitySaveData savedFacility))
            {
                continue;
            }

            entry.Value.ApplySaveData(
                savedFacility
            );
        }
    }

    private bool TryCollectSceneFacilitiesById(
        TrashFacility[] sceneFacilities,
        string operationName,
        out Dictionary<string, TrashFacility> facilitiesById
    )
    {
        facilitiesById =
            new Dictionary<string, TrashFacility>(
                StringComparer.Ordinal
            );

        bool allFacilitiesValid =
            true;

        if (sceneFacilities == null)
            return true;

        foreach (TrashFacility facility in
                 sceneFacilities)
        {
            if (facility == null)
                continue;

            string facilityId =
                facility.FacilityId;

            if (string.IsNullOrWhiteSpace(facilityId))
            {
                Debug.LogError(
                    $"Facility-{operationName} abgebrochen: " +
                    "Eine TrashFacility besitzt keine gültige facilityId.",
                    facility
                );

                allFacilitiesValid = false;
                continue;
            }

            if (facilitiesById.ContainsKey(facilityId))
            {
                Debug.LogError(
                    $"Facility-{operationName} abgebrochen: " +
                    $"Die facilityId '{facilityId}' kommt mehrfach vor.",
                    facility
                );

                allFacilitiesValid = false;
                continue;
            }

            facilitiesById.Add(
                facilityId,
                facility
            );
        }

        return allFacilitiesValid;
    }
    // ==================================================
    // APPLY WORLD TRASH
    // ==================================================

    private void ApplyWorldTrashData(
        SaveGameData saveData
    )
    {
        if (!saveData.sharedWorld
                .worldTrashSnapshotInitialized)
        {
            /*
             * Neuer oder alter Spielstand ohne
             * Müll-Snapshot. Szenenmüll bleibt unverändert.
             */
            return;
        }

        if (saveData.sharedWorld.worldTrashObjects == null)
            return;

        string activeSceneName =
            SceneManager.GetActiveScene().name;

        WorldTrashSaveObject[] currentWorldTrash =
            FindObjectsByType<WorldTrashSaveObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (!TryBuildWorldTrashLookup(
                currentWorldTrash,
                "Apply",
                out Dictionary<string, WorldTrashSaveObject>
                    worldTrashById))
        {
            Debug.LogError(
                "WorldTrash-Daten wurden nicht angewendet, " +
                "weil mindestens eine World Object ID fehlt " +
                "oder doppelt vergeben ist.",
                gameObject
            );

            return;
        }

        Dictionary<string, WorldTrashSaveData> records =
            new Dictionary<string, WorldTrashSaveData>(
                StringComparer.Ordinal
            );

        foreach (WorldTrashSaveData record in
                 saveData.sharedWorld.worldTrashObjects)
        {
            if (record == null ||
                string.IsNullOrWhiteSpace(
                    record.worldObjectId))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(
                    record.sceneName) &&
                !string.Equals(
                    record.sceneName,
                    activeSceneName,
                    StringComparison.Ordinal))
            {
                continue;
            }

            records[record.worldObjectId] =
                record;
        }

        foreach (KeyValuePair<string, WorldTrashSaveObject> entry in
                 worldTrashById)
        {
            WorldTrashSaveObject worldTrash =
                entry.Value;

            if (!records.TryGetValue(
                    entry.Key,
                    out WorldTrashSaveData record))
            {
                /*
                 * Müllobjekt wurde möglicherweise erst
                 * später durch ein Update hinzugefügt.
                 */
                continue;
            }

            worldTrash.ApplySaveData(
                record
            );

            if (!record.exists)
            {
                Debug.Log(
                    $"Gespeichertes Müllobjekt wurde entfernt: " +
                    $"{record.worldObjectId}",
                    gameObject
                );
            }
        }
    }

    private bool TryBuildWorldTrashLookup(
        WorldTrashSaveObject[] worldObjects,
        string operationName,
        out Dictionary<string, WorldTrashSaveObject> lookup
    )
    {
        lookup =
            new Dictionary<string, WorldTrashSaveObject>(
                StringComparer.Ordinal
            );

        bool hasInvalidIds = false;

        if (worldObjects == null)
            return true;

        HashSet<string> reportedDuplicateIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        foreach (WorldTrashSaveObject worldObject in
                 worldObjects)
        {
            if (worldObject == null)
                continue;

            string worldObjectId =
                worldObject.WorldObjectId;

            if (worldObjectId == null)
            {
                Debug.LogError(
                    $"WorldTrash-ID fehlt während '{operationName}'.\n" +
                    $"Objekt: {worldObject.name}\n" +
                    "Jedes WorldTrashSaveObject braucht eine " +
                    "stabile, eindeutige World Object ID.",
                    worldObject
                );

                hasInvalidIds = true;
                continue;
            }

            if (worldObjectId.Length == 0)
            {
                Debug.LogError(
                    $"WorldTrash-ID ist leer während '{operationName}'.\n" +
                    $"Objekt: {worldObject.name}\n" +
                    "Jedes WorldTrashSaveObject braucht eine " +
                    "stabile, eindeutige World Object ID.",
                    worldObject
                );

                hasInvalidIds = true;
                continue;
            }

            if (string.IsNullOrWhiteSpace(worldObjectId))
            {
                Debug.LogError(
                    $"WorldTrash-ID besteht nur aus Whitespace " +
                    $"während '{operationName}'.\n" +
                    $"Objekt: {worldObject.name}\n" +
                    "Jedes WorldTrashSaveObject braucht eine " +
                    "stabile, eindeutige World Object ID.",
                    worldObject
                );

                hasInvalidIds = true;
                continue;
            }

            if (lookup.TryGetValue(
                    worldObjectId,
                    out WorldTrashSaveObject existingWorldObject))
            {
                if (reportedDuplicateIds.Add(worldObjectId))
                {
                    Debug.LogError(
                        $"Doppelte WorldTrash-ID während '{operationName}'.\n" +
                        $"worldObjectId: {worldObjectId}\n" +
                        $"Objekt 1: {existingWorldObject.name}\n" +
                        $"Objekt 2: {worldObject.name}\n" +
                        "WorldTrash-IDs müssen in der gespeicherten " +
                        "Welt global eindeutig sein.",
                        worldObject
                    );
                }

                hasInvalidIds = true;
                continue;
            }

            lookup.Add(
                worldObjectId,
                worldObject
            );
        }

        if (!hasInvalidIds)
            return true;

        lookup.Clear();
        return false;
    }

    // ==================================================
    // DELETE SAVE
    // ==================================================

    public bool DeleteSave(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        string saveFilePath =
            GetSaveFilePath(slotIndex);

        string backupFilePath =
            GetBackupSaveFilePath(slotIndex);

        string temporaryFilePath =
            GetTemporarySaveFilePath(slotIndex);

        string writeTemporaryFilePath =
            GetWriteTemporarySaveFilePath(slotIndex);

        bool saveExisted =
            File.Exists(saveFilePath) ||
            File.Exists(backupFilePath) ||
            File.Exists(temporaryFilePath) ||
            File.Exists(writeTemporaryFilePath);

        try
        {
            DeleteFileIfExists(saveFilePath);
            DeleteFileIfExists(backupFilePath);
            DeleteFileIfExists(temporaryFilePath);
            DeleteFileIfExists(writeTemporaryFilePath);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Spielstand {slotIndex + 1} konnte nicht gelöscht werden.\n" +
                exception,
                gameObject
            );

            return false;
        }

        if (CurrentSave != null &&
            CurrentSave.slotIndex == slotIndex)
        {
            CurrentSave = null;
        }

        ClearRecoveryLoadAuthorization(slotIndex);

        SaveDeleted?.Invoke(slotIndex);

        return saveExisted;
    }

    // ==================================================
    // READ SAVE FILE
    // ==================================================

    private SaveGameData ReadSaveFile(
        int slotIndex
    )
    {
        SaveSlotStatus status =
            GetSaveSlotStatus(slotIndex);

        return ReadSaveFile(
            status,
            slotIndex,
            false
        );
    }

    private SaveGameData ReadSaveFile(
        SaveSlotStatus status,
        int slotIndex,
        bool logFailure
    )
    {
        if (!status.CanLoad ||
            string.IsNullOrWhiteSpace(status.SourcePath))
        {
            return null;
        }

        if (TryReadAndValidateSaveCandidate(
                status.SourcePath,
                slotIndex,
                out SaveGameData saveData,
                out string failureReason))
        {
            return saveData;
        }

        if (logFailure)
        {
            Debug.LogError(
                $"Spielstand {slotIndex + 1} konnte nicht aus " +
                $"'{status.SourcePath}' gelesen werden.\n" +
                failureReason,
                gameObject
            );
        }

        return null;
    }

    // ==================================================
    // WRITE SAVE FILE
    // ==================================================

    private bool WriteSaveFile(
        int slotIndex,
        SaveGameData saveData
    )
    {
        if (!IsValidSlotIndex(slotIndex) ||
            saveData == null)
        {
            return false;
        }

        EnsureSaveDirectoryExists();

        string saveFilePath =
            GetSaveFilePath(slotIndex);

        string backupFilePath =
            GetBackupSaveFilePath(slotIndex);

        string temporaryFilePath =
            GetTemporarySaveFilePath(slotIndex);

        string writeTemporaryFilePath =
            GetWriteTemporarySaveFilePath(slotIndex);

        SaveSlotStatus existingStatus =
            GetSaveSlotStatus(slotIndex);

        bool writeTemporaryFileValidated = false;

        try
        {
            if (!PreserveRecoveryCandidateBeforeWrite(
                    slotIndex,
                    existingStatus,
                    backupFilePath))
            {
                return false;
            }

            if (File.Exists(writeTemporaryFilePath))
                File.Delete(writeTemporaryFilePath);

            string json =
                JsonUtility.ToJson(
                    saveData,
                    true
                );

            File.WriteAllText(
                writeTemporaryFilePath,
                json
            );

            if (!ValidateTemporarySaveFile(
                    slotIndex,
                    writeTemporaryFilePath))
            {
                DeleteTemporarySaveFileBestEffort(
                    slotIndex,
                    writeTemporaryFilePath
                );

                return false;
            }

            writeTemporaryFileValidated = true;

            bool mainSaveIsValid =
                TryReadAndValidateSaveCandidate(
                    saveFilePath,
                    slotIndex,
                    out _,
                    out _
                );

            if (File.Exists(saveFilePath))
            {
                if (mainSaveIsValid)
                {
                    DeleteBackupBeforeReplace(
                        slotIndex,
                        backupFilePath
                    );

                    File.Replace(
                        writeTemporaryFilePath,
                        saveFilePath,
                        backupFilePath
                    );
                }
                else
                {
                    File.Replace(
                        writeTemporaryFilePath,
                        saveFilePath,
                        null
                    );
                }
            }
            else
            {
                File.Move(
                    writeTemporaryFilePath,
                    saveFilePath
                );
            }

            if (!TryReadAndValidateSaveCandidate(
                    saveFilePath,
                    slotIndex,
                    out _,
                    out string finalValidationFailure))
            {
                Debug.LogError(
                    $"Spielstand {slotIndex + 1} wurde geschrieben, " +
                    "konnte danach aber nicht validiert werden.\n" +
                    finalValidationFailure,
                    gameObject
                );

                return false;
            }

            if (File.Exists(writeTemporaryFilePath))
            {
                DeleteTemporarySaveFileBestEffort(
                    slotIndex,
                    writeTemporaryFilePath
                );
            }

            if (existingStatus.State == SaveSlotState.Recoverable &&
                File.Exists(temporaryFilePath))
            {
                DeleteTemporarySaveFileBestEffort(
                    slotIndex,
                    temporaryFilePath
                );
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Spielstand {slotIndex + 1} konnte nicht sicher gespeichert werden.\n" +
                $"Ziel: {saveFilePath}\n" +
                $"Temporäre Datei: {writeTemporaryFilePath}\n" +
                exception,
                gameObject
            );

            if (!writeTemporaryFileValidated)
            {
                DeleteTemporarySaveFileBestEffort(
                    slotIndex,
                    writeTemporaryFilePath
                );
            }

            return false;
        }
    }

    private bool ValidateTemporarySaveFile(
        int expectedSlotIndex,
        string temporaryFilePath
    )
    {
        if (TryReadAndValidateSaveCandidate(
                temporaryFilePath,
                expectedSlotIndex,
                out _,
                out string failureReason))
        {
            return true;
        }

        Debug.LogError(
            $"Temporäre Save-Datei für Slot {expectedSlotIndex + 1} " +
            "konnte nicht validiert werden.\n" +
            $"{temporaryFilePath}\n" +
            failureReason,
            gameObject
        );

        return false;
    }

    private void DeleteTemporarySaveFileBestEffort(
        int slotIndex,
        string temporaryFilePath
    )
    {
        try
        {
            if (File.Exists(temporaryFilePath))
                File.Delete(temporaryFilePath);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"Temporäre Save-Datei für Slot {slotIndex + 1} " +
                $"konnte nicht entfernt werden.\n" +
                $"{temporaryFilePath}\n" +
                exception,
                gameObject
            );
        }
    }

    private bool TryReadAndValidateSaveCandidate(
        string path,
        int expectedSlotIndex,
        out SaveGameData data,
        out string failureReason
    )
    {
        data = null;
        failureReason = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            failureReason = "Dateipfad fehlt.";
            return false;
        }

        if (!File.Exists(path))
        {
            failureReason = "Datei existiert nicht.";
            return false;
        }

        try
        {
            FileInfo fileInfo =
                new FileInfo(path);

            if (fileInfo.Length <= 0L)
            {
                failureReason = "Datei ist leer.";
                return false;
            }

            string json =
                File.ReadAllText(path);

            if (string.IsNullOrWhiteSpace(json))
            {
                failureReason = "JSON-Inhalt ist leer.";
                return false;
            }

            if (!ContainsJsonField(json, "saveVersion") ||
                !ContainsJsonField(json, "slotIndex"))
            {
                failureReason =
                    "Pflichtfelder saveVersion oder slotIndex fehlen.";
                return false;
            }

            SaveGameData saveData =
                JsonUtility.FromJson<SaveGameData>(
                    json
                );

            if (saveData == null)
            {
                failureReason =
                    "JSON konnte nicht als SaveGameData gelesen werden.";
                return false;
            }

            if (saveData.saveVersion <= 0)
            {
                failureReason =
                    $"Ungueltige Save-Version: {saveData.saveVersion}.";
                return false;
            }

            if (saveData.saveVersion >
                SaveGameData.CurrentSaveVersion)
            {
                failureReason =
                    $"Save-Version {saveData.saveVersion} ist neuer " +
                    $"als unterstuetzt ({SaveGameData.CurrentSaveVersion}).";
                return false;
            }

            if (saveData.slotIndex != expectedSlotIndex)
            {
                failureReason =
                    $"Slot-Mismatch. Erwartet: {expectedSlotIndex}, " +
                    $"gefunden: {saveData.slotIndex}.";
                return false;
            }

            EnsureSaveDataSectionsExist(saveData);

            if (saveData.player == null ||
                saveData.sharedWorld == null ||
                saveData.player.position == null ||
                saveData.player.rotation == null ||
                saveData.player.hotbarSlots == null ||
                saveData.player.appearance == null ||
                saveData.sharedWorld.facilities == null ||
                saveData.sharedWorld.worldTrashObjects == null)
            {
                failureReason =
                    "Save enthaelt nicht alle notwendigen Bereiche.";
                return false;
            }

            data = saveData;
            return true;
        }
        catch (Exception exception)
        {
            failureReason = exception.ToString();
            return false;
        }
    }

    private bool ContainsJsonField(
        string json,
        string fieldName
    )
    {
        return json.IndexOf(
            "\"" + fieldName + "\"",
            StringComparison.Ordinal
        ) >= 0;
    }

    private void TryAddValidCandidate(
        List<SaveCandidate> validCandidates,
        SaveCandidateKind kind,
        string path,
        int slotIndex,
        ref bool anyCandidateFileExists
    )
    {
        if (!File.Exists(path))
            return;

        anyCandidateFileExists = true;

        if (!TryReadAndValidateSaveCandidate(
                path,
                slotIndex,
                out SaveGameData saveData,
                out _))
        {
            return;
        }

        validCandidates.Add(
            new SaveCandidate(
                kind,
                path,
                saveData,
                GetSaveTimestampUtc(saveData, path)
            )
        );
    }

    private SaveSlotStatus CreateStatus(
        SaveSlotState state,
        SaveCandidate candidate,
        int slotIndex
    )
    {
        return new SaveSlotStatus(
            state,
            GetCandidateDisplayName(
                candidate.Data,
                slotIndex
            ),
            candidate.Path
        );
    }

    private string GetCandidateDisplayName(
        SaveGameData saveData,
        int slotIndex
    )
    {
        if (saveData != null &&
            !string.IsNullOrWhiteSpace(saveData.saveName))
        {
            return saveData.saveName.Trim();
        }

        return $"Save {slotIndex + 1}";
    }

    private DateTime GetSaveTimestampUtc(
        SaveGameData saveData,
        string path
    )
    {
        if (saveData != null &&
            !string.IsNullOrWhiteSpace(saveData.lastSavedUtc) &&
            DateTime.TryParse(
                saveData.lastSavedUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime parsedTime))
        {
            return parsedTime.ToUniversalTime();
        }

        return File.GetLastWriteTimeUtc(path);
    }

    private bool TryGetCandidate(
        List<SaveCandidate> candidates,
        SaveCandidateKind kind,
        out SaveCandidate candidate
    )
    {
        foreach (SaveCandidate currentCandidate in candidates)
        {
            if (currentCandidate.Kind != kind)
                continue;

            candidate = currentCandidate;
            return true;
        }

        candidate = default;
        return false;
    }

    private SaveCandidate GetNewestCandidate(
        List<SaveCandidate> candidates
    )
    {
        SaveCandidate selectedCandidate =
            candidates[0];

        for (int i = 1;
             i < candidates.Count;
             i++)
        {
            if (candidates[i].SavedAtUtc >
                selectedCandidate.SavedAtUtc)
            {
                selectedCandidate = candidates[i];
            }
        }

        return selectedCandidate;
    }

    private string GetSaveCandidateFailureSummary(
        int slotIndex
    )
    {
        List<string> failureLines =
            new List<string>();

        AppendCandidateFailure(
            failureLines,
            "Main",
            GetSaveFilePath(slotIndex),
            slotIndex
        );

        AppendCandidateFailure(
            failureLines,
            "Backup",
            GetBackupSaveFilePath(slotIndex),
            slotIndex
        );

        AppendCandidateFailure(
            failureLines,
            "Temporary",
            GetTemporarySaveFilePath(slotIndex),
            slotIndex
        );

        AppendCandidateFailure(
            failureLines,
            "WriteTemporary",
            GetWriteTemporarySaveFilePath(slotIndex),
            slotIndex
        );

        return failureLines.Count == 0
            ? "Keine Save-Kandidatendatei vorhanden."
            : string.Join("\n", failureLines);
    }

    private void AppendCandidateFailure(
        List<string> failureLines,
        string label,
        string path,
        int slotIndex
    )
    {
        if (!File.Exists(path))
            return;

        if (TryReadAndValidateSaveCandidate(
                path,
                slotIndex,
                out _,
                out string failureReason))
        {
            return;
        }

        failureLines.Add(
            label + ": " + failureReason + " (" + path + ")"
        );
    }

    private bool PreserveRecoveryCandidateBeforeWrite(
        int slotIndex,
        SaveSlotStatus existingStatus,
        string backupFilePath
    )
    {
        if (existingStatus.State != SaveSlotState.Recoverable)
            return true;

        if (string.IsNullOrWhiteSpace(existingStatus.SourcePath))
            return true;

        if (string.Equals(
                existingStatus.SourcePath,
                backupFilePath,
                StringComparison.Ordinal))
        {
            return true;
        }

        if (!TryReadAndValidateSaveCandidate(
                existingStatus.SourcePath,
                slotIndex,
                out _,
                out string failureReason))
        {
            Debug.LogError(
                $"Recovery-Kandidat fuer Slot {slotIndex + 1} " +
                "konnte vor dem Speichern nicht als Backup erhalten werden.\n" +
                failureReason,
                gameObject
            );

            return false;
        }

        try
        {
            if (File.Exists(backupFilePath))
                File.Delete(backupFilePath);

            File.Copy(
                existingStatus.SourcePath,
                backupFilePath
            );

            if (!TryReadAndValidateSaveCandidate(
                    backupFilePath,
                    slotIndex,
                    out _,
                    out string backupFailureReason))
            {
                Debug.LogError(
                    $"Backup fuer Slot {slotIndex + 1} wurde erstellt, " +
                    "ist aber nicht gueltig.\n" +
                    backupFailureReason,
                    gameObject
                );

                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Recovery-Kandidat fuer Slot {slotIndex + 1} " +
                "konnte nicht als Backup erhalten werden.\n" +
                exception,
                gameObject
            );

            return false;
        }
    }

    private void DeleteBackupBeforeReplace(
        int slotIndex,
        string backupFilePath
    )
    {
        if (!File.Exists(backupFilePath))
            return;

        try
        {
            File.Delete(backupFilePath);
        }
        catch (Exception exception)
        {
            throw new IOException(
                $"Backup-Datei fuer Slot {slotIndex + 1} " +
                "konnte vor File.Replace nicht entfernt werden.",
                exception
            );
        }
    }

    private void DeleteFileIfExists(
        string path
    )
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private bool CanWriteExistingSaveSlot(
        int slotIndex,
        out SaveSlotStatus status
    )
    {
        status =
            GetSaveSlotStatus(slotIndex);

        if (status.State == SaveSlotState.Empty)
        {
            Debug.LogError(
                $"Slot {slotIndex + 1} kann nicht aktualisiert werden, " +
                "weil keine bestehende Save-Datei vorhanden ist.",
                gameObject
            );

            return false;
        }

        if (status.State == SaveSlotState.Corrupted)
        {
            Debug.LogError(
                $"Slot {slotIndex + 1} kann nicht aktualisiert werden, " +
                "weil der Save beschaedigt ist.",
                gameObject
            );

            return false;
        }

        if (status.State == SaveSlotState.Recoverable &&
            !IsRecoverySaveAuthorized(slotIndex, status.SourcePath))
        {
            Debug.LogError(
                $"Slot {slotIndex + 1} kann nicht aktualisiert werden, " +
                "weil zuerst bewusst aus dem Recovery-Kandidaten " +
                "geladen werden muss.",
                gameObject
            );

            return false;
        }

        return true;
    }

    private bool IsRecoverySaveAuthorized(
        int slotIndex,
        string sourcePath
    )
    {
        return recoveryLoadedSlotIndex == slotIndex &&
               !string.IsNullOrWhiteSpace(recoveryLoadedSourcePath) &&
               string.Equals(
                   recoveryLoadedSourcePath,
                   sourcePath,
                   StringComparison.Ordinal
               );
    }

    public void ClearRecoveryLoadAuthorizationIfSlotChanges(
        int slotIndex
    )
    {
        if (recoveryLoadedSlotIndex >= 0 &&
            recoveryLoadedSlotIndex != slotIndex)
        {
            ClearRecoveryLoadAuthorization();
        }
    }

    private void ClearRecoveryLoadAuthorization(
        int slotIndex
    )
    {
        if (recoveryLoadedSlotIndex != slotIndex)
            return;

        ClearRecoveryLoadAuthorization();
    }

    private void ClearRecoveryLoadAuthorization()
    {
        recoveryLoadedSlotIndex = -1;
        recoveryLoadedSourcePath = string.Empty;
    }

    // ==================================================
    // DATA VALIDATION
    // ==================================================

    private void EnsureSaveDataSectionsExist(
        SaveGameData saveData
    )
    {
        if (saveData == null)
            return;

        if (saveData.player == null)
            saveData.player = new PlayerSaveData();

        if (saveData.sharedWorld == null)
            saveData.sharedWorld = new SharedWorldSaveData();

        if (saveData.player.position == null)
        {
            saveData.player.position =
                new SerializableVector3();
        }

        if (saveData.player.rotation == null)
        {
            saveData.player.rotation =
                new SerializableVector3();
        }

        if (saveData.player.hotbarSlots == null)
        {
            saveData.player.hotbarSlots =
                new List<HotbarSlotSaveData>();
        }

        if (saveData.player.appearance == null)
        {
            saveData.player.appearance =
                new CharacterAppearanceData();
        }

        if (saveData.sharedWorld.facilities == null)
        {
            saveData.sharedWorld.facilities =
                new List<FacilitySaveData>();
        }

        if (saveData.sharedWorld.worldTrashObjects == null)
        {
            saveData.sharedWorld.worldTrashObjects =
                new List<WorldTrashSaveData>();
        }
    }

    // ==================================================
    // FILE PATHS
    // ==================================================

    private void EnsureSaveDirectoryExists()
    {
        if (!Directory.Exists(
                SaveDirectoryPath))
        {
            Directory.CreateDirectory(
                SaveDirectoryPath
            );
        }
    }

    private string GetSaveFilePath(
        int slotIndex
    )
    {
        return Path.Combine(
            SaveDirectoryPath,
            saveFilePrefix +
            slotIndex +
            ".json"
        );
    }

    private string GetBackupSaveFilePath(
        int slotIndex
    )
    {
        return GetSaveFilePath(slotIndex) +
               ".bak";
    }

    private string GetTemporarySaveFilePath(
        int slotIndex
    )
    {
        return GetSaveFilePath(slotIndex) +
               ".tmp";
    }

    private string GetWriteTemporarySaveFilePath(
        int slotIndex
    )
    {
        return GetSaveFilePath(slotIndex) +
               ".write.tmp";
    }

    private bool IsValidSlotIndex(
        int slotIndex
    )
    {
        if (slotIndex < 0)
            return false;

        if (GameManager.Instance == null)
            return true;

        return slotIndex <
               GameManager.Instance.SaveSlotCount;
    }
}
