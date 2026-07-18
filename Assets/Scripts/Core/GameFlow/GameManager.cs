using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    MainMenu,
    CharacterCreation,
    Playing,
    Paused
}

public sealed class GameManager : MonoBehaviour
{
    // ==================================================
    // SINGLETON
    // ==================================================

    public static GameManager Instance { get; private set; }

    // ==================================================
    // SCENES
    // ==================================================

    [Header("Scene Names")]
    [SerializeField]
    private string mainMenuSceneName = "MainMenu";

    [SerializeField]
    private string characterCreationSceneName =
        "CharacterCreation";

    [SerializeField]
    private string gameSceneName = "GameScene";

    // ==================================================
    // SAVE SLOTS
    // ==================================================

    [Header("Save Slots")]
    [SerializeField, Min(1)]
    private int saveSlotCount = 3;

    public int SaveSlotCount => saveSlotCount;

    public int CurrentSlot { get; private set; } = -1;

    // ==================================================
    // GAME STATE
    // ==================================================

    public GameState CurrentState { get; private set; }

    // ==================================================
    // EVENTS
    // ==================================================

    public event Action<int> SaveSlotDeleted;

    // ==================================================
    // PLAYER PREFS
    // ==================================================

    private const string CurrentSlotKey =
        "CurrentSlot";

    private const string LegacySaveSlotKeyPrefix =
        "SaveSlot_";

    private static readonly string[] CharacterKeySuffixes =
    {
        "_Head",
        "_Face",
        "_Upper",
        "_Pants",
        "_Shoes",
        "_Skin"
    };

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

        saveSlotCount =
            Mathf.Max(1, saveSlotCount);

        CurrentSlot =
            PlayerPrefs.GetInt(
                CurrentSlotKey,
                -1
            );

        if (!IsValidSlotIndex(
                CurrentSlot,
                false))
        {
            CurrentSlot = -1;

            PlayerPrefs.DeleteKey(
                CurrentSlotKey
            );

            PlayerPrefs.Save();
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded +=
            OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -=
            OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        UpdateStateFromScene(
            SceneManager.GetActiveScene().name
        );
    }

    private void OnValidate()
    {
        saveSlotCount =
            Mathf.Max(1, saveSlotCount);
    }

    // ==================================================
    // SCENE STATE
    // ==================================================

    private void OnSceneLoaded(
        Scene scene,
        LoadSceneMode loadMode
    )
    {
        UpdateStateFromScene(scene.name);
    }

    private void UpdateStateFromScene(
        string sceneName
    )
    {
        if (string.Equals(
                sceneName,
                mainMenuSceneName,
                StringComparison.Ordinal))
        {
            CurrentState =
                GameState.MainMenu;

            return;
        }

        if (string.Equals(
                sceneName,
                characterCreationSceneName,
                StringComparison.Ordinal))
        {
            CurrentState =
                GameState.CharacterCreation;

            return;
        }

        if (string.Equals(
                sceneName,
                gameSceneName,
                StringComparison.Ordinal))
        {
            CurrentState =
                GameState.Playing;
        }
    }

    // ==================================================
    // START / CONTINUE
    // ==================================================

    public void StartNewGame(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return;

        if (SaveManager.Instance != null)
        {
            bool saveCreated =
                SaveManager.Instance.CreateNewSave(
                    slotIndex,
                    $"Save {slotIndex + 1}",
                    overwriteExisting: true
                );

            if (!saveCreated)
                return;
        }

        SetCurrentSlot(slotIndex);
        LoadCharacterCreation();
    }

    public void ContinueGame(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return;

        SaveSlotStatus jsonStatus =
            GetJsonSaveSlotStatus(slotIndex);

        if (jsonStatus.CanLoad)
        {
            SetCurrentSlot(slotIndex);
            LoadGameScene();
            return;
        }

        if (jsonStatus.State == SaveSlotState.Corrupted)
        {
            if (CurrentSlot == slotIndex)
            {
                CurrentSlot = -1;
                PlayerPrefs.DeleteKey(CurrentSlotKey);
                PlayerPrefs.Save();
            }

            Debug.LogWarning(
                $"Speicherplatz {slotIndex + 1} ist beschaedigt " +
                "und kann nicht fortgesetzt werden.",
                gameObject
            );

            return;
        }

        /*
         * Alte PlayerPrefs-Spielstände werden einmalig in
         * eine richtige JSON-Datei umgewandelt.
         */
        if (HasLegacySave(slotIndex))
        {
            SetCurrentSlot(slotIndex);
            MigrateLegacySave(slotIndex);
            LoadGameScene();
            return;
        }

        StartNewGame(slotIndex);
    }

    // ==================================================
    // SAVE SLOT INFORMATION
    // ==================================================

    public bool HasSaveForSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        SaveSlotStatus jsonStatus =
            GetJsonSaveSlotStatus(slotIndex);

        return jsonStatus.CanDelete ||
               HasLegacySave(slotIndex);
    }

    public bool CanDeleteSaveForSlot(
        int slotIndex
    )
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        SaveSlotStatus jsonStatus =
            GetJsonSaveSlotStatus(slotIndex);

        return jsonStatus.CanDelete ||
               HasLegacySave(slotIndex);
    }

    public string GetSaveNameForSlot(
        int slotIndex
    )
    {
        if (!IsValidSlotIndex(slotIndex))
            return "Empty";

        SaveSlotStatus jsonStatus =
            GetJsonSaveSlotStatus(slotIndex);

        if (jsonStatus.State == SaveSlotState.Valid)
        {
            return string.IsNullOrWhiteSpace(jsonStatus.DisplayName)
                ? $"Save {slotIndex + 1}"
                : jsonStatus.DisplayName;
        }

        if (jsonStatus.State == SaveSlotState.Recoverable)
        {
            string displayName =
                string.IsNullOrWhiteSpace(jsonStatus.DisplayName)
                    ? "Recovery available"
                    : jsonStatus.DisplayName;

            return string.Equals(
                    displayName,
                    "Recovery available",
                    StringComparison.Ordinal)
                ? displayName
                : displayName + " (Recovery available)";
        }

        if (jsonStatus.State == SaveSlotState.Corrupted)
            return "Corrupted Save";

        if (HasLegacySave(slotIndex))
        {
            return PlayerPrefs.GetString(
                GetLegacySaveKey(slotIndex),
                $"Save {slotIndex + 1}"
            );
        }

        return "Empty";
    }

    private SaveSlotStatus GetJsonSaveSlotStatus(
        int slotIndex
    )
    {
        if (SaveManager.Instance == null)
        {
            return new SaveSlotStatus(
                SaveSlotState.Empty,
                "Empty",
                string.Empty
            );
        }

        return SaveManager.Instance.GetSaveSlotStatus(
            slotIndex
        );
    }

    private bool HasLegacySave(int slotIndex)
    {
        string legacyKey =
            GetLegacySaveKey(slotIndex);

        if (!PlayerPrefs.HasKey(legacyKey))
            return false;

        string legacySaveName =
            PlayerPrefs.GetString(
                legacyKey,
                "Empty"
            );

        return
            !string.IsNullOrWhiteSpace(
                legacySaveName) &&
            !string.Equals(
                legacySaveName,
                "Empty",
                StringComparison.OrdinalIgnoreCase
            );
    }

    private void MigrateLegacySave(
        int slotIndex
    )
    {
        if (SaveManager.Instance == null)
            return;

        string legacySaveName =
            PlayerPrefs.GetString(
                GetLegacySaveKey(slotIndex),
                $"Save {slotIndex + 1}"
            );

        /*
         * Falls früher nur die Charakter-Arraynummer
         * gespeichert wurde, verwenden wir einen
         * verständlicheren Spielstandnamen.
         */
        if (int.TryParse(
                legacySaveName,
                out _))
        {
            legacySaveName =
                $"Save {slotIndex + 1}";
        }

        SaveManager.Instance.CreateNewSave(
            slotIndex,
            legacySaveName,
            overwriteExisting: false
        );
    }

    // ==================================================
    // CURRENT SLOT
    // ==================================================

    public void SetCurrentSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return;

        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.ClearRecoveryLoadAuthorizationIfSlotChanges(
                slotIndex
            );
        }

        CurrentSlot = slotIndex;

        PlayerPrefs.SetInt(
            CurrentSlotKey,
            CurrentSlot
        );

        PlayerPrefs.Save();
    }

    // ==================================================
    // DELETE SAVE
    // ==================================================

    public bool DeleteSaveForSlot(
        int slotIndex
    )
    {
        if (!IsValidSlotIndex(slotIndex))
            return false;

        bool jsonSaveDeleted = false;

        if (SaveManager.Instance != null)
        {
            jsonSaveDeleted =
                SaveManager.Instance.DeleteSave(
                    slotIndex
                );
        }

        string legacySaveKey =
            GetLegacySaveKey(slotIndex);

        bool legacySaveExisted =
            PlayerPrefs.HasKey(
                legacySaveKey
            );

        PlayerPrefs.DeleteKey(
            legacySaveKey
        );

        DeleteCharacterKeys(
            legacySaveKey
        );

        if (CurrentSlot == slotIndex)
        {
            CurrentSlot = -1;

            PlayerPrefs.DeleteKey(
                CurrentSlotKey
            );
        }

        PlayerPrefs.Save();

        bool saveExisted =
            jsonSaveDeleted ||
            legacySaveExisted;

        SaveSlotDeleted?.Invoke(
            slotIndex
        );

        Debug.Log(
            saveExisted
                ? $"Speicherplatz {slotIndex + 1} wurde gelöscht."
                : $"Speicherplatz {slotIndex + 1} war bereits leer.",
            gameObject
        );

        return saveExisted;
    }

    private void DeleteCharacterKeys(
        string saveKey
    )
    {
        for (int i = 0;
             i < CharacterKeySuffixes.Length;
             i++)
        {
            PlayerPrefs.DeleteKey(
                saveKey +
                CharacterKeySuffixes[i]
            );
        }
    }

    // ==================================================
    // SCENE LOADING
    // ==================================================

    public void LoadMainMenu()
    {
        LoadScene(mainMenuSceneName);
    }

    public void LoadCharacterCreation()
    {
        LoadScene(
            characterCreationSceneName
        );
    }

    public void LoadGameScene()
    {
        LoadScene(gameSceneName);
    }

    // ==================================================
    // PAUSE STATE
    // ==================================================

    public void SetPausedState(bool paused)
    {
        CurrentState =
            paused
                ? GameState.Paused
                : GameState.Playing;
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private string GetLegacySaveKey(
        int slotIndex
    )
    {
        return
            LegacySaveSlotKeyPrefix +
            slotIndex;
    }

    private bool IsValidSlotIndex(
        int slotIndex,
        bool showWarning = true
    )
    {
        bool isValid =
            slotIndex >= 0 &&
            slotIndex < saveSlotCount;

        if (!isValid &&
            showWarning)
        {
            Debug.LogWarning(
                $"Ungültiger Speicherplatz: {slotIndex}. " +
                $"Erlaubt sind Werte von 0 bis " +
                $"{saveSlotCount - 1}.",
                gameObject
            );
        }

        return isValid;
    }

    private void LoadScene(
        string sceneName
    )
    {
        if (string.IsNullOrWhiteSpace(
                sceneName))
        {
            Debug.LogWarning(
                "Es wurde kein Szenenname angegeben.",
                gameObject
            );

            return;
        }

        SceneManager.LoadScene(
            sceneName
        );
    }
}
