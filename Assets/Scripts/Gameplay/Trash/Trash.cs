using UnityEngine;

[DisallowMultipleComponent]
public sealed class Trash : MonoBehaviour
{
    // ==================================================
    // IDENTIFICATION
    // ==================================================

    [Header("Trash Identification")]
    [Tooltip(
        "Eindeutige und dauerhafte ID dieses Mülltyps. " +
        "Diese ID darf nach Veröffentlichung nicht mehr geändert werden."
    )]
    [SerializeField]
    private string trashId = "trash.default";

    [Tooltip("Anzeigename des Müllobjekts.")]
    [SerializeField]
    private string trashName = "Trash Bag";

    [SerializeField]
    private TrashClass trashClass = TrashClass.A;

    // ==================================================
    // VALUE
    // ==================================================

    [Header("Value")]
    [SerializeField, Min(0)]
    private int baseValue = 50;

    // ==================================================
    // ILLEGAL TRASH
    // ==================================================

    [Header("Illegal")]
    [SerializeField]
    private bool isIllegal;

    [Tooltip(
        "Heat, das später beim Verarbeiten oder Entdecken " +
        "dieses Mülltyps entstehen kann."
    )]
    [SerializeField, Min(0)]
    private int heatAmount;

    // ==================================================
    // PUBLIC VALUES
    // ==================================================

    public string TrashId => trashId;
    public string TrashName => trashName;
    public TrashClass TrashClass => trashClass;
    public int BaseValue => baseValue;
    public bool IsIllegal => isIllegal;
    public int HeatAmount => heatAmount;

    public bool HasValidId =>
        !string.IsNullOrWhiteSpace(trashId);

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        ValidateValues();
    }

    private void OnValidate()
    {
        ValidateValues();
    }

    // ==================================================
    // VALIDATION
    // ==================================================

    private void ValidateValues()
    {
        baseValue = Mathf.Max(0, baseValue);
        heatAmount = Mathf.Max(0, heatAmount);

        if (trashName == null)
            trashName = string.Empty;

        if (trashId == null)
            trashId = string.Empty;

        trashName = trashName.Trim();
        trashId = trashId.Trim();

        if (!Application.isPlaying &&
            string.IsNullOrWhiteSpace(trashId))
        {
            Debug.LogWarning(
                $"Das Müllobjekt '{name}' besitzt keine Trash ID.",
                gameObject
            );
        }
    }
}