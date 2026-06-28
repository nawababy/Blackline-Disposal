using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class CharacterCarouselController : MonoBehaviour
{
    public Transform previewParent;
    public GameObject[] characterPrefabs; // alle Prefabs
    public float previewScale = 150f;

    public Button leftArrowButton;
    public Button rightArrowButton;
    public Button finishButton;

    private GameObject currentPreview;
    private int currentIndex = 0;

    void Start()
    {
        leftArrowButton.onClick.AddListener(() => ChangeCharacter(-1));
        rightArrowButton.onClick.AddListener(() => ChangeCharacter(1));
        finishButton.onClick.AddListener(FinishCreation);

        UpdatePreview();
    }

    void ChangeCharacter(int direction)
    {
        currentIndex += direction;

        if (currentIndex < 0) currentIndex = characterPrefabs.Length - 1;
        if (currentIndex >= characterPrefabs.Length) currentIndex = 0;

        UpdatePreview();
    }

    void UpdatePreview()
    {
        // ALLE Kinder vom PreviewParent löschen
        foreach (Transform child in previewParent)
        {
            Destroy(child.gameObject);
        }

        // Neues Prefab instanziieren
        GameObject prefabToSpawn = characterPrefabs[currentIndex];
        if (prefabToSpawn != null)
        {
            currentPreview = Instantiate(prefabToSpawn, previewParent);
            currentPreview.transform.localPosition = Vector3.zero;
            currentPreview.transform.localRotation = Quaternion.Euler(0, 180, 0);
            currentPreview.transform.localScale = Vector3.one * previewScale;
        }
    }

    void FinishCreation()
    {
        int slotIndex = PlayerPrefs.GetInt("CurrentSlot", 0);
        PlayerPrefs.SetString("SaveSlot_" + slotIndex, currentIndex.ToString());
        PlayerPrefs.Save();

        SceneManager.LoadScene("GameScene");
    }
}