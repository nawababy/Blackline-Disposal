using UnityEngine;

public sealed class PlayerLook : MonoBehaviour
{
    [Header("Fallback Settings")]
    [Tooltip("Wird verwendet, wenn kein SettingsManager vorhanden ist.")]
    [SerializeField, Range(0.1f, 10f)]
    private float fallbackSensitivity = 2f;

    [SerializeField, Range(60f, 100f)]
    private float fallbackFieldOfView = 60f;

    [Header("Mouse Settings")]
    [SerializeField, Range(1f, 89f)]
    private float maxPitch = 85f;

    [Header("Camera")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PlayerMovement movementScript;

    [SerializeField, Min(0f)]
    private float cameraSmoothSpeed = 10f;

    [Tooltip(
        "Zusätzliche Höhe über der Mitte des CharacterControllers."
    )]
    [SerializeField]
    private float eyeHeight = 0.6f;

    [Header("Control")]
    [SerializeField] private bool canLook = true;

    private SettingsManager connectedSettingsManager;
    private AudioListener cachedAudioListener;

    private float sensitivity;
    private float fieldOfView;
    private float pitch;
    private float currentCameraHeight;

    private Vector3 cameraBaseLocalPosition;

    public float Sensitivity => sensitivity;
    public float FieldOfView => fieldOfView;
    public bool CanLook => canLook;

    private void Awake()
    {
        FindRequiredReferences();
        CacheCameraValues();

        sensitivity = fallbackSensitivity;
        fieldOfView = fallbackFieldOfView;
    }

    private void OnEnable()
    {
        TryConnectToSettingsManager();
    }

    private void Start()
    {
        TryConnectToSettingsManager();
        ApplyFieldOfView();
        SetCursorLocked(true);
    }

    private void OnDisable()
    {
        DisconnectFromSettingsManager();
    }

    private void OnDestroy()
    {
        DisconnectFromSettingsManager();
    }

    private void Update()
    {
        /*
         * Falls die GameScene direkt gestartet wurde und der
         * SettingsManager erst etwas später verfügbar ist,
         * wird die Verbindung automatisch nachgeholt.
         */
        if (connectedSettingsManager == null)
            TryConnectToSettingsManager();

        if (canLook)
            HandleMouseLook();

        SmoothCameraHeight();
    }

    // ==================================================
    // REFERENCES
    // ==================================================

    private void FindRequiredReferences()
    {
        if (movementScript == null)
        {
            movementScript =
                GetComponent<PlayerMovement>();
        }

        if (playerCamera == null)
        {
            playerCamera =
                GetComponentInChildren<Camera>(true);
        }

        if (playerCamera != null)
        {
            cachedAudioListener =
                playerCamera.GetComponent<AudioListener>();
        }
    }

    private void CacheCameraValues()
    {
        if (playerCamera == null)
        {
            cameraBaseLocalPosition = Vector3.zero;
            currentCameraHeight = eyeHeight;
            return;
        }

        cameraBaseLocalPosition =
            playerCamera.transform.localPosition;

        currentCameraHeight =
            GetTargetCameraHeight();

        SetCameraHeight(currentCameraHeight);
    }

    // ==================================================
    // SETTINGS MANAGER
    // ==================================================

    private void TryConnectToSettingsManager()
    {
        SettingsManager manager =
            SettingsManager.Instance;

        if (manager == null)
        {
            sensitivity = fallbackSensitivity;
            fieldOfView = fallbackFieldOfView;

            ApplyFieldOfView();
            return;
        }

        if (connectedSettingsManager == manager)
            return;

        DisconnectFromSettingsManager();

        connectedSettingsManager = manager;

        sensitivity =
            connectedSettingsManager.Sensitivity;

        fieldOfView =
            connectedSettingsManager.FieldOfView;

        connectedSettingsManager.SensitivityChanged +=
            OnSensitivityChanged;

        connectedSettingsManager.FieldOfViewChanged +=
            OnFieldOfViewChanged;

        ApplyFieldOfView();
    }

    private void DisconnectFromSettingsManager()
    {
        if (connectedSettingsManager == null)
            return;

        connectedSettingsManager.SensitivityChanged -=
            OnSensitivityChanged;

        connectedSettingsManager.FieldOfViewChanged -=
            OnFieldOfViewChanged;

        connectedSettingsManager = null;
    }

    private void OnSensitivityChanged(
        float newSensitivity
    )
    {
        sensitivity = newSensitivity;
    }

    private void OnFieldOfViewChanged(
        float newFieldOfView
    )
    {
        fieldOfView = newFieldOfView;

        ApplyFieldOfView();
    }

    // ==================================================
    // MOUSE LOOK
    // ==================================================

    private void HandleMouseLook()
    {
        float mouseX =
            Input.GetAxisRaw("Mouse X") *
            sensitivity;

        float mouseY =
            Input.GetAxisRaw("Mouse Y") *
            sensitivity;

        pitch = Mathf.Clamp(
            pitch - mouseY,
            -maxPitch,
            maxPitch
        );

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation =
                Quaternion.Euler(
                    pitch,
                    0f,
                    0f
                );
        }

        transform.Rotate(
            Vector3.up * mouseX,
            Space.Self
        );
    }

    // ==================================================
    // CAMERA HEIGHT
    // ==================================================

    private void SmoothCameraHeight()
    {
        if (playerCamera == null ||
            movementScript == null)
        {
            return;
        }

        float targetHeight =
            GetTargetCameraHeight();

        currentCameraHeight = Mathf.Lerp(
            currentCameraHeight,
            targetHeight,
            Time.deltaTime * cameraSmoothSpeed
        );

        SetCameraHeight(currentCameraHeight);
    }

    private float GetTargetCameraHeight()
    {
        if (movementScript == null)
            return eyeHeight;

        return movementScript.GetCenterY() +
               eyeHeight;
    }

    private void SetCameraHeight(float height)
    {
        if (playerCamera == null)
            return;

        Vector3 newPosition =
            cameraBaseLocalPosition;

        newPosition.y = height;

        playerCamera.transform.localPosition =
            newPosition;
    }

    // ==================================================
    // FIELD OF VIEW
    // ==================================================

    private void ApplyFieldOfView()
    {
        if (playerCamera == null)
            return;

        playerCamera.fieldOfView =
            Mathf.Clamp(
                fieldOfView,
                60f,
                100f
            );
    }

    // ==================================================
    // EXTERNAL CONTROL
    // ==================================================

    public void SetLookEnabled(bool value)
    {
        canLook = value;
    }

    public void SetCameraActive(bool value)
    {
        if (playerCamera == null)
            return;

        playerCamera.gameObject.SetActive(value);

        if (cachedAudioListener != null)
        {
            cachedAudioListener.enabled = value;
        }
    }

    public void SetCursorLocked(bool value)
    {
        Cursor.lockState = value
            ? CursorLockMode.Locked
            : CursorLockMode.None;

        Cursor.visible = !value;
    }
}