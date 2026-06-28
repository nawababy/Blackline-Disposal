using UnityEngine;
using UnityEngine.UI;

public class CharacterCustomizationController : MonoBehaviour
{
    [Header("Preview")]
    public Transform previewParent;

    [Header("Category Prefabs")]
    public GameObject[] heads;
    public GameObject[] faces;
    public GameObject[] uppers;
    public GameObject[] pants;
    public GameObject[] shoes;
    public Material[] skinTones;

    [Header("UI Buttons")]
    public Button headLeft, headRight;
    public Button faceLeft, faceRight;
    public Button upperLeft, upperRight;
    public Button pantsLeft, pantsRight;
    public Button shoesLeft, shoesRight;
    public Button skinLeft, skinRight;

    // Indices für Auswahl
    private int headIndex = 0;
    private int faceIndex = 0;
    private int upperIndex = 0;
    private int pantsIndex = 0;
    private int shoesIndex = 0;
    private int skinIndex = 0;

    // Aktuell instanzierte Teile
    private GameObject currentHead, currentFace, currentUpper, currentPants, currentShoes;

    void Start()
    {
        // Head Buttons
        headLeft.onClick.AddListener(() => ChangeCategory(ref headIndex, heads, -1));
        headRight.onClick.AddListener(() => ChangeCategory(ref headIndex, heads, 1));

        // Face Buttons
        faceLeft.onClick.AddListener(() => ChangeCategory(ref faceIndex, faces, -1));
        faceRight.onClick.AddListener(() => ChangeCategory(ref faceIndex, faces, 1));

        // Upper Buttons
        upperLeft.onClick.AddListener(() => ChangeCategory(ref upperIndex, uppers, -1));
        upperRight.onClick.AddListener(() => ChangeCategory(ref upperIndex, uppers, 1));

        // Pants Buttons
        pantsLeft.onClick.AddListener(() => ChangeCategory(ref pantsIndex, pants, -1));
        pantsRight.onClick.AddListener(() => ChangeCategory(ref pantsIndex, pants, 1));

        // Shoes Buttons
        shoesLeft.onClick.AddListener(() => ChangeCategory(ref shoesIndex, shoes, -1));
        shoesRight.onClick.AddListener(() => ChangeCategory(ref shoesIndex, shoes, 1));

        // Skin Buttons
        skinLeft.onClick.AddListener(() => ChangeSkin(-1));
        skinRight.onClick.AddListener(() => ChangeSkin(1));

        UpdatePreview();
    }

    void ChangeCategory(ref int index, GameObject[] options, int direction)
    {
        if (options.Length == 0) return;

        index += direction;
        if (index < 0) index = options.Length - 1;
        if (index >= options.Length) index = 0;

        UpdatePreview();
    }

    void ChangeSkin(int direction)
    {
        if (skinTones.Length == 0) return;

        skinIndex += direction;
        if (skinIndex < 0) skinIndex = skinTones.Length - 1;
        if (skinIndex >= skinTones.Length) skinIndex = 0;

        UpdatePreview();
    }

    void UpdatePreview()
    {
        // Alte Teile entfernen
        if (currentHead != null) Destroy(currentHead);
        if (currentFace != null) Destroy(currentFace);
        if (currentUpper != null) Destroy(currentUpper);
        if (currentPants != null) Destroy(currentPants);
        if (currentShoes != null) Destroy(currentShoes);

        // Neue Instanzen erzeugen
        if (heads.Length > 0)
            currentHead = Instantiate(heads[headIndex], previewParent.position, Quaternion.identity, previewParent);
        if (faces.Length > 0)
            currentFace = Instantiate(faces[faceIndex], previewParent.position, Quaternion.identity, previewParent);
        if (uppers.Length > 0)
            currentUpper = Instantiate(uppers[upperIndex], previewParent.position, Quaternion.identity, previewParent);
        if (pants.Length > 0)
            currentPants = Instantiate(pants[pantsIndex], previewParent.position, Quaternion.identity, previewParent);
        if (shoes.Length > 0)
            currentShoes = Instantiate(shoes[shoesIndex], previewParent.position, Quaternion.identity, previewParent);

        // Skin anwenden
        if (skinTones.Length > 0)
        {
            Renderer[] renderers = previewParent.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in renderers)
                r.material = skinTones[skinIndex];
        }

        // Rotation & Scale
        previewParent.localRotation = Quaternion.Euler(0, 180, 0);
        previewParent.localScale = Vector3.one * 150f;
    }

    // Speichern der Auswahl für den aktuellen BodyType/Slot
    public void SaveSelection(string slotKey)
    {
        PlayerPrefs.SetInt(slotKey + "_Head", headIndex);
        PlayerPrefs.SetInt(slotKey + "_Face", faceIndex);
        PlayerPrefs.SetInt(slotKey + "_Upper", upperIndex);
        PlayerPrefs.SetInt(slotKey + "_Pants", pantsIndex);
        PlayerPrefs.SetInt(slotKey + "_Shoes", shoesIndex);
        PlayerPrefs.SetInt(slotKey + "_Skin", skinIndex);
        PlayerPrefs.Save();
    }
}