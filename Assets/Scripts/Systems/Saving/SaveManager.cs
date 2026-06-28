using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    public string SaveDirectoryPath =>
        Path.Combine(
            Application.persistentDataPath,
            saveFolderName
        );

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

        DontDestroyOnLoad(gameObject);

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
        sceneWorldTrashDefaults.Clear();

        WorldTrashSaveObject[] worldTrashObjects =
            FindObjectsByType<WorldTrashSaveObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (WorldTrashSaveObject worldTrash in
                 worldTrashObjects)
        {
            if (worldTrash == null ||
                !worldTrash.HasValidWorldObjectId)
            {
                continue;
            }

            string worldObjectId =
                worldTrash.WorldObjectId;

            if (sceneWorldTrashDefaults.ContainsKey(
                    worldObjectId))
            {
                Debug.LogError(
                    $"Doppelte World Object ID gefunden: " +
                    $"{worldObjectId}",
                    worldTrash
                );

                continue;
            }

            sceneWorldTrashDefaults.Add(
                worldObjectId,
                worldTrash.CreateSaveData(true)
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

    public bool HasSave(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        return File.Exists(
            GetSaveFilePath(slotIndex)
        );
    }

    public string GetSaveName(int slotIndex)
    {
        SaveGameData saveData =
            ReadSaveFile(slotIndex);

        if (saveData == null ||
            string.IsNullOrWhiteSpace(
                saveData.saveName))
        {
            return "Empty";
        }

        return saveData.saveName;
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

        if (HasSave(slotIndex) &&
            !overwriteExisting)
        {
            Debug.LogWarning(
                $"Auf Speicherplatz {slotIndex + 1} " +
                "existiert bereits ein Spielstand.",
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
                $"Ungültiger Speicherplatz: {slotIndex}",
                gameObject
            );

            return false;
        }

        SaveGameData saveData =
            ReadSaveFile(slotIndex);

        if (saveData == null)
        {
            saveData =
                SaveGameData.CreateNew(
                    slotIndex,
                    $"Save {slotIndex + 1}"
                );
        }

        EnsureSaveDataSectionsExist(
            saveData
        );

        saveData.saveVersion = 2;
        saveData.slotIndex = slotIndex;

        CaptureCurrentSceneData(
            saveData
        );

        saveData.UpdateLastSavedTime();

        bool wasWritten =
            WriteSaveFile(
                slotIndex,
                saveData
            );

        if (!wasWritten)
            return false;

        CurrentSave = saveData;

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

    private void CaptureCurrentSceneData(
        SaveGameData saveData
    )
    {
        if (saveData == null)
            return;

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

        CaptureWorldTrashData(
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
    // CAPTURE WORLD TRASH
    // ==================================================

    private void CaptureWorldTrashData(
        SaveGameData saveData
    )
    {
        if (saveData.sharedWorld.worldTrashObjects == null)
        {
            saveData.sharedWorld.worldTrashObjects =
                new List<WorldTrashSaveData>();
        }

        string activeSceneName =
            SceneManager.GetActiveScene().name;

        Dictionary<string, WorldTrashSaveData> savedRecords =
            new Dictionary<string, WorldTrashSaveData>(
                StringComparer.Ordinal
            );

        /*
         * Bereits vorhandene Records übernehmen.
         * Das ist später auch bei mehreren Szenen wichtig.
         */
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

        HashSet<string> currentWorldObjectIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        WorldTrashSaveObject[] currentWorldTrash =
            FindObjectsByType<WorldTrashSaveObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        /*
         * Alle Müllobjekte speichern,
         * die aktuell noch existieren.
         */
        foreach (WorldTrashSaveObject worldTrash in
                 currentWorldTrash)
        {
            if (worldTrash == null ||
                !worldTrash.HasValidWorldObjectId)
            {
                continue;
            }

            /*
             * Deaktivierte Müllobjekte gelten als entfernt.
             */
            if (!worldTrash.gameObject.activeInHierarchy)
                continue;

            string worldObjectId =
                worldTrash.WorldObjectId;

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

        SaveGameData saveData =
            ReadSaveFile(slotIndex);

        if (saveData == null)
        {
            Debug.LogWarning(
                $"Spielstand {slotIndex + 1} " +
                "konnte nicht geladen werden.",
                gameObject
            );

            return false;
        }

        EnsureSaveDataSectionsExist(
            saveData
        );

        CurrentSave = saveData;

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

        WorldTrashSaveObject[] currentWorldTrash =
            FindObjectsByType<WorldTrashSaveObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (WorldTrashSaveObject worldTrash in
                 currentWorldTrash)
        {
            if (worldTrash == null ||
                !worldTrash.HasValidWorldObjectId)
            {
                continue;
            }

            if (!records.TryGetValue(
                    worldTrash.WorldObjectId,
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

    // ==================================================
    // DELETE SAVE
    // ==================================================

    public bool DeleteSave(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        string saveFilePath =
            GetSaveFilePath(slotIndex);

        string temporaryFilePath =
            GetTemporarySaveFilePath(slotIndex);

        bool saveExisted =
            File.Exists(saveFilePath);

        try
        {
            if (File.Exists(saveFilePath))
                File.Delete(saveFilePath);

            if (File.Exists(temporaryFilePath))
                File.Delete(temporaryFilePath);
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
        if (!IsValidSlotIndex(slotIndex))
            return null;

        string saveFilePath =
            GetSaveFilePath(slotIndex);

        if (!File.Exists(saveFilePath))
            return null;

        try
        {
            string json =
                File.ReadAllText(
                    saveFilePath
                );

            if (string.IsNullOrWhiteSpace(json))
                return null;

            SaveGameData saveData =
                JsonUtility.FromJson<SaveGameData>(
                    json
                );

            if (saveData == null)
                return null;

            EnsureSaveDataSectionsExist(
                saveData
            );

            return saveData;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Spielstand {slotIndex + 1} konnte nicht gelesen werden.\n" +
                exception,
                gameObject
            );

            return null;
        }
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

        string temporaryFilePath =
            GetTemporarySaveFilePath(slotIndex);

        bool temporaryFileValidated = false;

        try
        {
            if (File.Exists(temporaryFilePath))
                File.Delete(temporaryFilePath);

            string json =
                JsonUtility.ToJson(
                    saveData,
                    true
                );

            File.WriteAllText(
                temporaryFilePath,
                json
            );

            if (!ValidateTemporarySaveFile(
                    slotIndex,
                    temporaryFilePath))
            {
                DeleteTemporarySaveFileBestEffort(
                    slotIndex,
                    temporaryFilePath
                );

                return false;
            }

            temporaryFileValidated = true;

            if (File.Exists(saveFilePath))
            {
                File.Replace(
                    temporaryFilePath,
                    saveFilePath,
                    null
                );
            }
            else
            {
                File.Move(
                    temporaryFilePath,
                    saveFilePath
                );
            }

            if (File.Exists(temporaryFilePath))
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
                $"Temporäre Datei: {temporaryFilePath}\n" +
                exception,
                gameObject
            );

            if (!temporaryFileValidated)
            {
                DeleteTemporarySaveFileBestEffort(
                    slotIndex,
                    temporaryFilePath
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
        if (!File.Exists(temporaryFilePath))
        {
            Debug.LogError(
                $"Temporäre Save-Datei für Slot {expectedSlotIndex + 1} " +
                $"wurde nicht erstellt.\n{temporaryFilePath}",
                gameObject
            );

            return false;
        }

        FileInfo temporaryFileInfo =
            new FileInfo(temporaryFilePath);

        if (temporaryFileInfo.Length <= 0L)
        {
            Debug.LogError(
                $"Temporäre Save-Datei für Slot {expectedSlotIndex + 1} " +
                $"ist leer.\n{temporaryFilePath}",
                gameObject
            );

            return false;
        }

        try
        {
            string json =
                File.ReadAllText(
                    temporaryFilePath
                );

            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogError(
                    $"Temporäre Save-Datei für Slot {expectedSlotIndex + 1} " +
                    $"enthält keinen gültigen JSON-Inhalt.\n{temporaryFilePath}",
                    gameObject
                );

                return false;
            }

            SaveGameData temporarySaveData =
                JsonUtility.FromJson<SaveGameData>(
                    json
                );

            if (temporarySaveData == null)
            {
                Debug.LogError(
                    $"Temporäre Save-Datei für Slot {expectedSlotIndex + 1} " +
                    $"konnte nicht als SaveGameData gelesen werden.\n" +
                    temporaryFilePath,
                    gameObject
                );

                return false;
            }

            if (temporarySaveData.player == null ||
                temporarySaveData.sharedWorld == null ||
                temporarySaveData.player.position == null ||
                temporarySaveData.player.rotation == null ||
                temporarySaveData.player.hotbarSlots == null ||
                temporarySaveData.sharedWorld.facilities == null ||
                temporarySaveData.sharedWorld.worldTrashObjects == null)
            {
                Debug.LogError(
                    $"Temporäre Save-Datei für Slot {expectedSlotIndex + 1} " +
                    $"enthält nicht alle notwendigen Save-Bereiche.\n" +
                    temporaryFilePath,
                    gameObject
                );

                return false;
            }

            if (temporarySaveData.slotIndex !=
                expectedSlotIndex)
            {
                Debug.LogError(
                    $"Temporäre Save-Datei hat den falschen Slot. " +
                    $"Erwartet: {expectedSlotIndex}, " +
                    $"gefunden: {temporarySaveData.slotIndex}.\n" +
                    temporaryFilePath,
                    gameObject
                );

                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Temporäre Save-Datei für Slot {expectedSlotIndex + 1} " +
                $"konnte nicht validiert werden.\n" +
                $"{temporaryFilePath}\n" +
                exception,
                gameObject
            );

            return false;
        }
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

    private string GetTemporarySaveFilePath(
        int slotIndex
    )
    {
        return GetSaveFilePath(slotIndex) +
               ".tmp";
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