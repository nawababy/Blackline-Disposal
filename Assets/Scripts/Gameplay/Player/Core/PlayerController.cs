using UnityEngine;

public sealed class PlayerController : MonoBehaviour
{
    // ==================================================
    // REFERENCES
    // ==================================================

    [Header("Player References")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private PlayerLook playerLook;
    [SerializeField] private PlayerInventory inventory;

    // ==================================================
    // PUBLIC ACCESS
    // ==================================================

    public PlayerInventory Inventory => inventory;

    /*
     * Das Geld liegt nicht mehr zusätzlich im PlayerController.
     * PlayerInventory ist momentan die einzige Geldquelle.
     */
    public int Cash =>
        inventory != null
            ? inventory.cash
            : 0;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        /*
         * Läuft auch im Editor und versucht fehlende
         * Referenzen automatisch zu finden.
         */
        FindReferences();
    }

    // ==================================================
    // REFERENCES
    // ==================================================

    private void FindReferences()
    {
        if (movement == null)
            movement = GetComponent<PlayerMovement>();

        if (playerLook == null)
            playerLook = GetComponent<PlayerLook>();

        if (inventory == null)
            inventory = GetComponent<PlayerInventory>();
    }

    // ==================================================
    // MONEY
    // ==================================================

    /*
     * Diese Methoden bleiben vorerst erhalten,
     * damit bestehende Scripts oder UnityEvents
     * nicht plötzlich kaputtgehen.
     *
     * Sie verändern aber nur noch PlayerInventory.
     */

    public void AddCash(int amount)
    {
        if (inventory == null)
        {
            LogMissingInventory();
            return;
        }

        inventory.AddCash(amount);
    }

    public bool TrySpendCash(int amount)
    {
        if (inventory == null)
        {
            LogMissingInventory();
            return false;
        }

        return inventory.TrySpendCash(amount);
    }

    public void SetCash(int amount)
    {
        if (inventory == null)
        {
            LogMissingInventory();
            return;
        }

        inventory.SetCash(amount);
    }

    // ==================================================
    // LOCAL PLAYER CONTROL
    // ==================================================

    public void SetLocalControl(bool value)
    {
        if (movement != null)
            movement.SetInputEnabled(value);

        if (playerLook != null)
        {
            playerLook.SetLookEnabled(value);
            playerLook.SetCameraActive(value);
        }
    }

    // ==================================================
    // SAVING PLACEHOLDERS
    // ==================================================

    public void SaveGame()
    {
        /*
         * Das spätere Speichern wird nicht mehr direkt
         * vom PlayerController erledigt.
         *
         * Dafür erstellen wir später einen zentralen
         * SaveManager.
         */
    }

    public void LoadGame()
    {
        /*
         * Das spätere Laden wird ebenfalls vom
         * zentralen SaveManager übernommen.
         */
    }

    // ==================================================
    // WARNINGS
    // ==================================================

    private void LogMissingInventory()
    {
        Debug.LogWarning(
            "PlayerController findet kein PlayerInventory. " +
            "Füge PlayerInventory dem Player-Objekt hinzu " +
            "oder trage es im Inspector ein.",
            gameObject
        );
    }
}