using UnityEngine;
using UnityEngine.UI;

public class CharacterCarouselController : MonoBehaviour
{
    public Transform previewParent;
    public GameObject[] characterPrefabs;
    public float previewScale = 150f;

    public Button leftArrowButton;
    public Button rightArrowButton;
    public Button finishButton;

    private const string LegacySaveSlotKeyPrefix = "SaveSlot_";

    private GameObject currentPreview;
    private int currentIndex;
    private bool listenersRegistered;
    private bool isFinishing;

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

        if (!TryGetSelectedPrefab(
                out _))
        {
            UpdateButtonStates(false);
            return;
        }

        isFinishing = true;
        UpdateButtonStates(true);

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

        if (characterPrefabs == null ||
            characterPrefabs.Length == 0)
        {
            Debug.LogError(
                "CharacterCarouselController ist ungueltig: " +
                "characterPrefabs ist leer oder nicht gesetzt.",
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
            FindFirstValidPrefabIndex();

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
        selectedPrefab = null;

        if (previewParent == null ||
            characterPrefabs == null ||
            characterPrefabs.Length == 0)
        {
            return false;
        }

        if (currentIndex < 0 ||
            currentIndex >= characterPrefabs.Length)
        {
            return false;
        }

        selectedPrefab =
            characterPrefabs[currentIndex];

        if (selectedPrefab != null)
            return true;

        Debug.LogWarning(
            "CharacterCarouselController kann das ausgewaehlte " +
            $"Character-Prefab an Index {currentIndex} nicht laden, " +
            "weil der Eintrag leer ist.",
            gameObject
        );

        return false;
    }

    private int FindNextValidIndex(
        int direction
    )
    {
        if (characterPrefabs == null ||
            characterPrefabs.Length == 0)
        {
            return -1;
        }

        int step =
            direction < 0
                ? -1
                : 1;

        int index =
            currentIndex;

        for (int i = 0;
             i < characterPrefabs.Length;
             i++)
        {
            index =
                (index + step +
                 characterPrefabs.Length) %
                characterPrefabs.Length;

            if (characterPrefabs[index] != null)
                return index;
        }

        return -1;
    }

    private int FindFirstValidPrefabIndex()
    {
        if (characterPrefabs == null)
            return -1;

        for (int i = 0;
             i < characterPrefabs.Length;
             i++)
        {
            if (characterPrefabs[i] != null)
                return i;
        }

        return -1;
    }

    private int CountValidPrefabs()
    {
        if (characterPrefabs == null)
            return 0;

        int count = 0;

        for (int i = 0;
             i < characterPrefabs.Length;
             i++)
        {
            if (characterPrefabs[i] != null)
                count++;
        }

        return count;
    }

    private void UpdateButtonStates(
        bool hasValidConfiguration
    )
    {
        bool canNavigate =
            hasValidConfiguration &&
            !isFinishing &&
            CountValidPrefabs() > 1;

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
