using UnityEngine;
using UnityEngine.UI;

public class CharacterCarouselController : MonoBehaviour
{
    public Transform previewParent;
    public GameObject[] characterPrefabs;
    public float previewScale = 150f;

    [SerializeField]
    private CharacterDatabase characterDatabase;

    public Button leftArrowButton;
    public Button rightArrowButton;
    public Button finishButton;

    private const string LegacySaveSlotKeyPrefix = "SaveSlot_";

    private GameObject currentPreview;
    private int currentIndex;
    private bool listenersRegistered;
    private bool isFinishing;

    private bool UsesCharacterDatabase =>
        characterDatabase != null;

    private void OnEnable()
    {
        RegisterButtonListeners();
        RefreshControllerState(true);
    }

    private void OnDisable()
    {
        UnregisterButtonListeners();
    }

    private void OnDestroy()
    {
        UnregisterButtonListeners();
    }

    public void ShowPreviousCharacter()
    {
        ChangeCharacter(-1);
    }

    public void ShowNextCharacter()
    {
        ChangeCharacter(1);
    }

    public void FinishCreation()
    {
        if (isFinishing)
            return;

        if (!RefreshControllerState(false))
            return;

        GameManager gameManager =
            GameManager.Instance;

        if (gameManager == null)
        {
            Debug.LogError(
                "CharacterCarouselController kann die Auswahl nicht " +
                "abschliessen, weil kein GameManager aktiv ist.",
                gameObject
            );

            return;
        }

        int slotIndex =
            gameManager.CurrentSlot;

        if (slotIndex < 0 ||
            slotIndex >= gameManager.SaveSlotCount)
        {
            Debug.LogError(
                "CharacterCarouselController kann die Auswahl nicht " +
                "speichern, weil kein gueltiger Save-Slot aktiv ist. " +
                $"CurrentSlot: {slotIndex}, SaveSlotCount: " +
                $"{gameManager.SaveSlotCount}.",
                gameObject
            );

            return;
        }

        if (!TryGetSelectedCharacterId(
                out string selectedCharacterId))
        {
            UpdateButtonStates(true);
            return;
        }

        SaveManager saveManager =
            SaveManager.Instance;

        if (saveManager == null)
        {
            Debug.LogError(
                "CharacterCarouselController kann die Auswahl nicht " +
                "speichern, weil kein SaveManager aktiv ist.",
                gameObject
            );

            return;
        }

        isFinishing = true;
        UpdateButtonStates(true);

        bool wasSaved =
            saveManager.SetPlayerCharacterId(
                slotIndex,
                selectedCharacterId
            );

        if (!wasSaved)
        {
            Debug.LogError(
                "CharacterCarouselController konnte die gewaehlte " +
                $"characterId '{selectedCharacterId}' nicht in Slot " +
                $"{slotIndex + 1} speichern.",
                gameObject
            );

            isFinishing = false;
            RefreshControllerState(false);
            return;
        }

        PlayerPrefs.SetString(
            LegacySaveSlotKeyPrefix + slotIndex,
            currentIndex.ToString()
        );

        PlayerPrefs.Save();

        gameManager.LoadGameScene();
    }

    private void RegisterButtonListeners()
    {
        if (listenersRegistered)
            return;

        if (leftArrowButton != null)
        {
            leftArrowButton.onClick.AddListener(
                ShowPreviousCharacter
            );
        }

        if (rightArrowButton != null)
        {
            rightArrowButton.onClick.AddListener(
                ShowNextCharacter
            );
        }

        if (finishButton != null)
        {
            finishButton.onClick.AddListener(
                FinishCreation
            );
        }

        listenersRegistered = true;
    }

    private void UnregisterButtonListeners()
    {
        if (!listenersRegistered)
            return;

        if (leftArrowButton != null)
        {
            leftArrowButton.onClick.RemoveListener(
                ShowPreviousCharacter
            );
        }

        if (rightArrowButton != null)
        {
            rightArrowButton.onClick.RemoveListener(
                ShowNextCharacter
            );
        }

        if (finishButton != null)
        {
            finishButton.onClick.RemoveListener(
                FinishCreation
            );
        }

        listenersRegistered = false;
    }

    private void ChangeCharacter(
        int direction
    )
    {
        if (isFinishing)
            return;

        if (!RefreshControllerState(false))
            return;

        int nextIndex =
            FindNextValidIndex(direction);

        if (nextIndex < 0)
        {
            Debug.LogError(
                "CharacterCarouselController findet kein gueltiges " +
                "Character-Prefab zum Wechseln.",
                gameObject
            );

            UpdateButtonStates(false);
            return;
        }

        if (nextIndex == currentIndex)
            return;

        currentIndex = nextIndex;

        UpdatePreview();
        UpdateButtonStates(true);
    }

    private bool RefreshControllerState(
        bool updatePreview
    )
    {
        bool hasValidConfiguration =
            ValidateConfiguration();

        UpdateButtonStates(
            hasValidConfiguration
        );

        if (!hasValidConfiguration)
        {
            DestroyCurrentPreview();
            return false;
        }

        if (updatePreview)
            UpdatePreview();

        return true;
    }

    private bool ValidateConfiguration()
    {
        bool isValid = true;

        if (previewParent == null)
        {
            Debug.LogError(
                "CharacterCarouselController ist ungueltig: " +
                "previewParent fehlt.",
                gameObject
            );

            isValid = false;
        }

        if (leftArrowButton == null)
        {
            Debug.LogError(
                "CharacterCarouselController ist ungueltig: " +
                "leftArrowButton fehlt.",
                gameObject
            );

            isValid = false;
        }

        if (rightArrowButton == null)
        {
            Debug.LogError(
                "CharacterCarouselController ist ungueltig: " +
                "rightArrowButton fehlt.",
                gameObject
            );

            isValid = false;
        }

        if (finishButton == null)
        {
            Debug.LogError(
                "CharacterCarouselController ist ungueltig: " +
                "finishButton fehlt.",
                gameObject
            );

            isValid = false;
        }

        if (!isValid)
            return false;

        if (UsesCharacterDatabase)
            return ValidateDatabaseConfiguration();

        return ValidateLegacyPrefabConfiguration();
    }

    private bool ValidateDatabaseConfiguration()
    {
        if (!characterDatabase.ValidateDatabase(gameObject))
            return false;

        if (currentIndex < 0 ||
            currentIndex >= characterDatabase.Count)
        {
            Debug.LogWarning(
                "CharacterCarouselController hatte einen ungueltigen " +
                $"currentIndex: {currentIndex}. Der Index wird korrigiert.",
                gameObject
            );

            currentIndex =
                Mathf.Clamp(
                    currentIndex,
                    0,
                    characterDatabase.Count - 1
                );
        }

        if (characterDatabase.TryGetDefinitionAt(
                currentIndex,
                out _))
        {
            return true;
        }

        int validIndex =
            FindFirstValidCharacterIndex();

        if (validIndex < 0)
        {
            Debug.LogError(
                "CharacterCarouselController ist ungueltig: " +
                "CharacterDatabase enthaelt keinen gueltigen " +
                "Character-Eintrag.",
                gameObject
            );

            return false;
        }

        Debug.LogWarning(
            "CharacterCarouselController ueberspringt einen " +
            $"ungueltigen CharacterDatabase-Eintrag an Index {currentIndex}.",
            gameObject
        );

        currentIndex = validIndex;

        return true;
    }

    private bool ValidateLegacyPrefabConfiguration()
    {
        if (characterPrefabs == null ||
            characterPrefabs.Length == 0)
        {
            Debug.LogError(
                "CharacterCarouselController ist ungueltig: " +
                "characterPrefabs ist leer oder nicht gesetzt.",
                gameObject
            );

            return false;
        }

        if (currentIndex < 0 ||
            currentIndex >= characterPrefabs.Length)
        {
            Debug.LogWarning(
                "CharacterCarouselController hatte einen ungueltigen " +
                $"currentIndex: {currentIndex}. Der Index wird korrigiert.",
                gameObject
            );

            currentIndex =
                Mathf.Clamp(
                    currentIndex,
                    0,
                    characterPrefabs.Length - 1
                );
        }

        if (characterPrefabs[currentIndex] != null)
            return true;

        int validIndex =
            FindFirstValidCharacterIndex();

        if (validIndex < 0)
        {
            Debug.LogError(
                "CharacterCarouselController ist ungueltig: " +
                "characterPrefabs enthaelt kein gueltiges Prefab.",
                gameObject
            );

            return false;
        }

        Debug.LogWarning(
            "CharacterCarouselController ueberspringt ein leeres " +
            $"Character-Prefab an Index {currentIndex}.",
            gameObject
        );

        currentIndex = validIndex;

        return true;
    }

    private void UpdatePreview()
    {
        if (!TryGetSelectedPrefab(
                out GameObject prefabToSpawn))
        {
            DestroyCurrentPreview();
            return;
        }

        DestroyCurrentPreview();

        currentPreview =
            Instantiate(
                prefabToSpawn,
                previewParent
            );

        currentPreview.transform.localPosition =
            Vector3.zero;

        currentPreview.transform.localRotation =
            Quaternion.Euler(
                0f,
                180f,
                0f
            );

        currentPreview.transform.localScale =
            Vector3.one * previewScale;
    }

    private bool TryGetSelectedPrefab(
        out GameObject selectedPrefab
    )
    {
        return TryGetPrefabAt(
            currentIndex,
            true,
            out selectedPrefab
        );
    }

    private bool TryGetSelectedCharacterId(
        out string selectedCharacterId
    )
    {
        selectedCharacterId = string.Empty;

        if (!UsesCharacterDatabase)
        {
            Debug.LogError(
                "CharacterCarouselController kann die Auswahl nicht " +
                "speichern, weil keine CharacterDatabase zugewiesen ist. " +
                "Das Legacy-characterPrefabs-Array ist nur ein " +
                "Preview-Fallback und darf keine characterId erzeugen.",
                gameObject
            );

            return false;
        }

        if (!characterDatabase.ValidateDatabase(gameObject))
            return false;

        if (!characterDatabase.TryGetDefinitionAt(
                currentIndex,
                out CharacterDatabase.CharacterDefinition definition))
        {
            Debug.LogError(
                "CharacterCarouselController kann die Auswahl nicht " +
                $"speichern, weil der Datenbankeintrag an Index " +
                $"{currentIndex} ungueltig ist.",
                gameObject
            );

            return false;
        }

        selectedCharacterId =
            definition.CharacterId;

        if (!string.IsNullOrWhiteSpace(
                selectedCharacterId))
        {
            return true;
        }

        Debug.LogError(
            "CharacterCarouselController kann die Auswahl nicht " +
            "speichern, weil die characterId leer ist.",
            gameObject
        );

        return false;
    }

    private bool TryGetPrefabAt(
        int index,
        bool logWarning,
        out GameObject prefab
    )
    {
        prefab = null;

        if (UsesCharacterDatabase)
        {
            if (characterDatabase == null ||
                index < 0 ||
                index >= characterDatabase.Count)
            {
                return false;
            }

            if (characterDatabase.TryGetDefinitionAt(
                    index,
                    out CharacterDatabase.CharacterDefinition definition))
            {
                prefab = definition.CharacterPrefab;
                return prefab != null;
            }

            if (logWarning)
            {
                Debug.LogWarning(
                    "CharacterCarouselController kann den " +
                    $"CharacterDatabase-Eintrag an Index {index} " +
                    "nicht fuer die Preview laden.",
                    gameObject
                );
            }

            return false;
        }

        if (characterPrefabs == null ||
            index < 0 ||
            index >= characterPrefabs.Length)
        {
            return false;
        }

        prefab =
            characterPrefabs[index];

        if (prefab != null)
            return true;

        if (logWarning)
        {
            Debug.LogWarning(
                "CharacterCarouselController kann das ausgewaehlte " +
                $"Character-Prefab an Index {index} nicht laden, " +
                "weil der Eintrag leer ist.",
                gameObject
            );
        }

        return false;
    }

    private int FindNextValidIndex(
        int direction
    )
    {
        int count =
            GetCharacterEntryCount();

        if (count <= 0)
            return -1;

        int step =
            direction < 0
                ? -1
                : 1;

        int index =
            currentIndex;

        for (int i = 0;
             i < count;
             i++)
        {
            index =
                (index + step + count) %
                count;

            if (TryGetPrefabAt(
                    index,
                    false,
                    out _))
            {
                return index;
            }
        }

        return -1;
    }

    private int FindFirstValidCharacterIndex()
    {
        int count =
            GetCharacterEntryCount();

        for (int i = 0;
             i < count;
             i++)
        {
            if (TryGetPrefabAt(
                    i,
                    false,
                    out _))
            {
                return i;
            }
        }

        return -1;
    }

    private int CountValidCharacters()
    {
        int count =
            GetCharacterEntryCount();

        int validCount = 0;

        for (int i = 0;
             i < count;
             i++)
        {
            if (TryGetPrefabAt(
                    i,
                    false,
                    out _))
            {
                validCount++;
            }
        }

        return validCount;
    }

    private int GetCharacterEntryCount()
    {
        if (UsesCharacterDatabase)
            return characterDatabase.Count;

        return characterPrefabs != null
            ? characterPrefabs.Length
            : 0;
    }

    private void UpdateButtonStates(
        bool hasValidConfiguration
    )
    {
        bool canNavigate =
            hasValidConfiguration &&
            !isFinishing &&
            CountValidCharacters() > 1;

        bool canFinish =
            hasValidConfiguration &&
            !isFinishing;

        if (leftArrowButton != null)
        {
            leftArrowButton.interactable =
                canNavigate;
        }

        if (rightArrowButton != null)
        {
            rightArrowButton.interactable =
                canNavigate;
        }

        if (finishButton != null)
        {
            finishButton.interactable =
                canFinish;
        }
    }

    private void DestroyCurrentPreview()
    {
        if (currentPreview == null)
            return;

        Destroy(currentPreview);
        currentPreview = null;
    }
}
