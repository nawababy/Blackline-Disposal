using UnityEngine;

public sealed class CarryTrash : MonoBehaviour
{
    // ==================================================
    // PLAYER / INPUT
    // ==================================================

    [Header("Player / Input")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private InputSettings inputSettings;

    // ==================================================
    // CARRY SETTINGS
    // ==================================================

    [Header("Carry Settings")]
    [SerializeField, Min(0.1f)]
    private float pickupRange = 2f;

    [SerializeField, Min(0.1f)]
    private float carryDistance = 1.5f;

    [Tooltip(
        "Bestimmt, wie stark der Müllsack beim Loslassen " +
        "weggeschleudert wird."
    )]
    [SerializeField, Min(0f)]
    private float throwVelocityMultiplier = 1f;

    // ==================================================
    // CONTROL
    // ==================================================

    [Header("Control")]
    [SerializeField] private bool canCarry = true;

    // ==================================================
    // CURRENT STATE
    // ==================================================

    private bool isCarrying;

    private Trash carriedTrash;
    private Rigidbody carriedRigidbody;
    private Collider[] carriedColliders;

    private Vector3 lastCarryPosition;
    private Vector3 calculatedVelocity;

    // ==================================================
    // PUBLIC VALUES
    // ==================================================

    public bool IsCarrying => isCarrying;
    public bool CanCarry => canCarry;
    public Trash CarriedTrash => carriedTrash;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        FindReferences();

        if (inputSettings != null)
            inputSettings.EnsureLoaded();

        ValidateSettings();
    }

    private void OnEnable()
    {
        if (inputSettings != null)
            inputSettings.EnsureLoaded();
    }

    private void OnDisable()
    {
        /*
         * Falls das Script oder das Player-Objekt deaktiviert
         * wird, wird ein getragener Sack sauber freigegeben.
         *
         * Dabei wird absichtlich keine Wurfgeschwindigkeit
         * angewendet.
         */
        if (isCarrying)
            ReleaseTrash(false);
    }

    private void OnDestroy()
    {
        if (isCarrying)
            ReleaseTrash(false);
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    private void Update()
    {
        if (!canCarry ||
            playerCamera == null ||
            inputSettings == null)
        {
            return;
        }

        HandleCarryInput();
    }

    private void LateUpdate()
    {
        if (isCarrying)
            UpdateCarriedTrashPosition();
    }

    // ==================================================
    // REFERENCES
    // ==================================================

    private void FindReferences()
    {
        if (playerCamera != null)
            return;

        Camera cameraComponent =
            GetComponentInChildren<Camera>(true);

        if (cameraComponent != null)
            playerCamera = cameraComponent.transform;
    }

    public void SetPlayerCamera(
        Transform newPlayerCamera
    )
    {
        playerCamera = newPlayerCamera;
    }

    public void SetInputSettings(
        InputSettings newInputSettings
    )
    {
        inputSettings = newInputSettings;

        if (inputSettings != null)
            inputSettings.EnsureLoaded();
    }

    // ==================================================
    // INPUT
    // ==================================================

    private void HandleCarryInput()
    {
        if (inputSettings.pickupHold)
        {
            /*
             * Hold-Modus:
             * Taste drücken = aufnehmen
             * Taste loslassen = werfen/fallen lassen
             */
            if (Input.GetKeyDown(inputSettings.pickupKey))
                TryPickup();

            if (Input.GetKeyUp(inputSettings.pickupKey))
                DropTrash();

            return;
        }

        /*
         * Toggle-Modus:
         * Einmal drücken = aufnehmen
         * Erneut drücken = werfen/fallen lassen
         */
        if (!Input.GetKeyDown(inputSettings.pickupKey))
            return;

        if (isCarrying)
            DropTrash();
        else
            TryPickup();
    }

    // ==================================================
    // PICKUP
    // ==================================================

    public bool TryPickup()
    {
        if (!canCarry ||
            isCarrying ||
            playerCamera == null)
        {
            return false;
        }

        bool hasHit =
            Physics.Raycast(
                playerCamera.position,
                playerCamera.forward,
                out RaycastHit hit,
                pickupRange,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );

        if (!hasHit)
            return false;

        Trash trash =
            hit.collider.GetComponentInParent<Trash>();

        if (trash == null)
            return false;

        return TryPickupTrash(trash);
    }

    public bool TryPickupTrash(Trash trash)
    {
        if (!canCarry ||
            isCarrying ||
            trash == null ||
            playerCamera == null)
        {
            return false;
        }

        Rigidbody trashRigidbody =
            trash.GetComponent<Rigidbody>();

        if (trashRigidbody == null)
        {
            trashRigidbody =
                trash.GetComponentInChildren<Rigidbody>();
        }

        if (trashRigidbody == null)
        {
            Debug.LogWarning(
                $"Das Müllobjekt '{trash.name}' besitzt " +
                "keinen Rigidbody.",
                trash
            );

            return false;
        }

        carriedTrash = trash;
        carriedRigidbody = trashRigidbody;

        carriedColliders =
            carriedTrash.GetComponentsInChildren<Collider>(
                true
            );

        /*
         * Alte Bewegung entfernen, bevor der Rigidbody
         * kinematisch gemacht wird.
         */
        carriedRigidbody.linearVelocity =
            Vector3.zero;

        carriedRigidbody.angularVelocity =
            Vector3.zero;

        carriedRigidbody.useGravity = false;
        carriedRigidbody.isKinematic = true;

        SetCarriedCollidersEnabled(false);

        isCarrying = true;

        lastCarryPosition =
            GetCarryPosition();

        calculatedVelocity =
            Vector3.zero;

        UpdateCarriedTrashPosition();

        return true;
    }

    // ==================================================
    // CARRY MOVEMENT
    // ==================================================

    private void UpdateCarriedTrashPosition()
    {
        if (carriedTrash == null ||
            carriedRigidbody == null ||
            playerCamera == null)
        {
            ClearCarryState();
            return;
        }

        Vector3 targetPosition =
            GetCarryPosition();

        float safeDeltaTime =
            Mathf.Max(
                Time.deltaTime,
                0.0001f
            );

        /*
         * Die Bewegung der Kamera wird als spätere
         * Wurfgeschwindigkeit gespeichert.
         *
         * Diese Geschwindigkeit wird absichtlich nicht
         * begrenzt, damit schnelle Kamerabewegungen den
         * Sack stark wegschleudern können.
         */
        calculatedVelocity =
            (targetPosition - lastCarryPosition) /
            safeDeltaTime;

        lastCarryPosition =
            targetPosition;

        /*
         * Direkte Positionierung wie im ursprünglichen
         * Script. Dadurch bleibt der Sack ohne weiches
         * Hinterherziehen direkt vor der Kamera.
         */
        carriedTrash.transform.SetPositionAndRotation(
            targetPosition,
            Quaternion.LookRotation(
                playerCamera.forward,
                Vector3.up
            )
        );
    }

    private Vector3 GetCarryPosition()
    {
        if (playerCamera == null)
            return transform.position;

        return
            playerCamera.position +
            playerCamera.forward *
            carryDistance;
    }

    // ==================================================
    // DROP / THROW
    // ==================================================

    public void DropTrash()
    {
        ReleaseTrash(true);
    }

    public void DropTrashWithoutThrow()
    {
        ReleaseTrash(false);
    }

    private void ReleaseTrash(
        bool applyThrowVelocity
    )
    {
        if (!isCarrying ||
            carriedTrash == null ||
            carriedRigidbody == null)
        {
            ClearCarryState();
            return;
        }

        SetCarriedCollidersEnabled(true);

        carriedRigidbody.isKinematic = false;
        carriedRigidbody.useGravity = true;

        /*
         * Keine Geschwindigkeitsbegrenzung.
         * Dadurch bleibt das starke Wegschleudern erhalten.
         */
        carriedRigidbody.linearVelocity =
            applyThrowVelocity
                ? calculatedVelocity *
                  throwVelocityMultiplier
                : Vector3.zero;

        carriedRigidbody.angularVelocity =
            Vector3.zero;

        ClearCarryState();
    }

    // ==================================================
    // COLLIDERS
    // ==================================================

    private void SetCarriedCollidersEnabled(
        bool enabled
    )
    {
        if (carriedColliders == null)
            return;

        foreach (Collider trashCollider in carriedColliders)
        {
            if (trashCollider != null)
                trashCollider.enabled = enabled;
        }
    }

    // ==================================================
    // STATE
    // ==================================================

    private void ClearCarryState()
    {
        carriedTrash = null;
        carriedRigidbody = null;
        carriedColliders = null;

        isCarrying = false;

        lastCarryPosition =
            Vector3.zero;

        calculatedVelocity =
            Vector3.zero;
    }

    // ==================================================
    // CONTROL
    // ==================================================

    public void SetCarryEnabled(bool value)
    {
        canCarry = value;

        /*
         * Beim Öffnen des Pause-Menüs wird der Sack wie
         * vorher mit der zuletzt berechneten Geschwindigkeit
         * losgelassen.
         */
        if (!canCarry && isCarrying)
            DropTrash();
    }

    // ==================================================
    // VALIDATION
    // ==================================================

    private void ValidateSettings()
    {
        pickupRange =
            Mathf.Max(
                0.1f,
                pickupRange
            );

        carryDistance =
            Mathf.Max(
                0.1f,
                carryDistance
            );

        throwVelocityMultiplier =
            Mathf.Max(
                0f,
                throwVelocityMultiplier
            );
    }
}