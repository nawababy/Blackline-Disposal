using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ControlsSettingsUI : MonoBehaviour
{
    // ==================================================
    // INPUT SETTINGS
    // ==================================================

    [Header("Input Settings")]
    [SerializeField] private InputSettings inputSettings;

    // ==================================================
    // KEY BINDINGS
    // ==================================================

    [Header("Crouch Key")]
    [SerializeField] private Button crouchButton;
    [SerializeField] private TMP_Text crouchKeyText;

    [Header("Interact Key")]
    [SerializeField] private Button interactButton;
    [SerializeField] private TMP_Text interactKeyText;

    [Header("Pickup Key")]
    [SerializeField] private Button pickupButton;
    [SerializeField] private TMP_Text pickupKeyText;

    // ==================================================
    // HOLD OPTIONS
    // ==================================================

    [Header("Hold Options")]
    [Tooltip("Aktiviert: Die Crouch-Taste muss gehalten werden.")]
    [SerializeField] private Toggle holdToCrouchToggle;

    [Tooltip("Aktiviert: Die Pickup-Taste muss gehalten werden.")]
    [SerializeField] private Toggle holdToPickupToggle;

    // ==================================================
    // RESET
    // ==================================================

    [Header("Reset")]
    [SerializeField] private Button resetControlsButton;

    // ==================================================
    // REBINDING
    // ==================================================

    [Header("Rebinding")]
    [SerializeField]
    private string waitingForKeyText =
        "Press a key...";

    private bool isWaitingForKey;
    private InputBindingId waitingBinding;
    private int rebindStartedFrame;
    private bool listenersRegistered;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        RegisterListeners();
    }

    private void OnEnable()
    {
        if (inputSettings == null)
        {
            Debug.LogWarning(
                "ControlsSettingsUI hat keine InputSettings-Datei.",
                gameObject
            );

            return;
        }

        inputSettings.EnsureLoaded();

        inputSettings.Changed -= RefreshUI;
        inputSettings.Changed += RefreshUI;

        isWaitingForKey = false;

        RefreshUI();
        Canvas.ForceUpdateCanvases();
    }

    private void OnDisable()
    {
        if (inputSettings != null)
            inputSettings.Changed -= RefreshUI;

        isWaitingForKey = false;
    }

    private void OnDestroy()
    {
        RemoveListeners();

        if (inputSettings != null)
            inputSettings.Changed -= RefreshUI;
    }

    private void Update()
    {
        if (!isWaitingForKey)
            return;

        // Verhindert, dass der Klick auf den Button
        // direkt als neue Taste erkannt wird.
        if (Time.frameCount <= rebindStartedFrame)
            return;

        foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
        {
            if (key == KeyCode.None)
                continue;

            if (!Input.GetKeyDown(key))
                continue;

            // Escape bricht die Neubelegung ab.
            if (key == KeyCode.Escape)
            {
                CancelRebind();
                return;
            }

            inputSettings.SetKey(
                waitingBinding,
                key
            );

            isWaitingForKey = false;

            RefreshUI();
            Canvas.ForceUpdateCanvases();

            return;
        }
    }

    // ==================================================
    // LISTENERS
    // ==================================================

    private void RegisterListeners()
    {
        if (listenersRegistered)
            return;

        if (crouchButton != null)
        {
            crouchButton.onClick.AddListener(
                OnCrouchButtonPressed
            );
        }

        if (interactButton != null)
        {
            interactButton.onClick.AddListener(
                OnInteractButtonPressed
            );
        }

        if (pickupButton != null)
        {
            pickupButton.onClick.AddListener(
                OnPickupButtonPressed
            );
        }

        if (holdToCrouchToggle != null)
        {
            holdToCrouchToggle.onValueChanged.AddListener(
                OnHoldToCrouchChanged
            );
        }

        if (holdToPickupToggle != null)
        {
            holdToPickupToggle.onValueChanged.AddListener(
                OnHoldToPickupChanged
            );
        }

        if (resetControlsButton != null)
        {
            resetControlsButton.onClick.AddListener(
                OnResetControlsPressed
            );
        }

        listenersRegistered = true;
    }

    private void RemoveListeners()
    {
        if (!listenersRegistered)
            return;

        if (crouchButton != null)
        {
            crouchButton.onClick.RemoveListener(
                OnCrouchButtonPressed
            );
        }

        if (interactButton != null)
        {
            interactButton.onClick.RemoveListener(
                OnInteractButtonPressed
            );
        }

        if (pickupButton != null)
        {
            pickupButton.onClick.RemoveListener(
                OnPickupButtonPressed
            );
        }

        if (holdToCrouchToggle != null)
        {
            holdToCrouchToggle.onValueChanged.RemoveListener(
                OnHoldToCrouchChanged
            );
        }

        if (holdToPickupToggle != null)
        {
            holdToPickupToggle.onValueChanged.RemoveListener(
                OnHoldToPickupChanged
            );
        }

        if (resetControlsButton != null)
        {
            resetControlsButton.onClick.RemoveListener(
                OnResetControlsPressed
            );
        }

        listenersRegistered = false;
    }

    // ==================================================
    // UI REFRESH
    // ==================================================

    public void RefreshUI()
    {
        if (inputSettings == null)
            return;

        SetKeyText(
            crouchKeyText,
            inputSettings.crouchKey
        );

        SetKeyText(
            interactKeyText,
            inputSettings.interactKey
        );

        SetKeyText(
            pickupKeyText,
            inputSettings.pickupKey
        );

        /*
         * crouchToggle = true:
         * einmal drücken zum Ducken.
         *
         * crouchToggle = false:
         * Taste muss gehalten werden.
         *
         * Deshalb wird der Wert für "Hold to Crouch"
         * hier umgedreht.
         */
        if (holdToCrouchToggle != null)
        {
            holdToCrouchToggle.SetIsOnWithoutNotify(
                !inputSettings.crouchToggle
            );
        }

        if (holdToPickupToggle != null)
        {
            holdToPickupToggle.SetIsOnWithoutNotify(
                inputSettings.pickupHold
            );
        }

        if (isWaitingForKey)
        {
            TMP_Text selectedText =
                GetKeyText(waitingBinding);

            if (selectedText != null)
                selectedText.text = waitingForKeyText;
        }
    }

    // ==================================================
    // REBINDING
    // ==================================================

    private void OnCrouchButtonPressed()
    {
        BeginRebind(InputBindingId.Crouch);
    }

    private void OnInteractButtonPressed()
    {
        BeginRebind(InputBindingId.Interact);
    }

    private void OnPickupButtonPressed()
    {
        BeginRebind(InputBindingId.Pickup);
    }

    private void BeginRebind(InputBindingId binding)
    {
        if (inputSettings == null)
            return;

        waitingBinding = binding;
        isWaitingForKey = true;
        rebindStartedFrame = Time.frameCount;

        RefreshUI();
        Canvas.ForceUpdateCanvases();
    }

    private void CancelRebind()
    {
        isWaitingForKey = false;

        RefreshUI();
        Canvas.ForceUpdateCanvases();
    }

    // ==================================================
    // HOLD OPTIONS
    // ==================================================

    private void OnHoldToCrouchChanged(bool holdEnabled)
    {
        if (inputSettings == null)
            return;

        inputSettings.SetCrouchToggle(
            !holdEnabled
        );
    }

    private void OnHoldToPickupChanged(bool holdEnabled)
    {
        if (inputSettings == null)
            return;

        inputSettings.SetPickupHold(
            holdEnabled
        );
    }

    // ==================================================
    // RESET
    // ==================================================

    private void OnResetControlsPressed()
    {
        if (inputSettings == null)
            return;

        isWaitingForKey = false;

        inputSettings.ResetToDefaults();

        RefreshUI();
        Canvas.ForceUpdateCanvases();
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private TMP_Text GetKeyText(InputBindingId binding)
    {
        switch (binding)
        {
            case InputBindingId.Crouch:
                return crouchKeyText;

            case InputBindingId.Interact:
                return interactKeyText;

            case InputBindingId.Pickup:
                return pickupKeyText;

            default:
                return null;
        }
    }

    private void SetKeyText(
        TMP_Text textField,
        KeyCode key
    )
    {
        if (textField == null)
            return;

        textField.text = FormatKeyName(key);
    }

    private string FormatKeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.LeftShift:
                return "Left Shift";

            case KeyCode.RightShift:
                return "Right Shift";

            case KeyCode.LeftControl:
                return "Left Ctrl";

            case KeyCode.RightControl:
                return "Right Ctrl";

            case KeyCode.LeftAlt:
                return "Left Alt";

            case KeyCode.RightAlt:
                return "Right Alt";

            case KeyCode.Return:
                return "Enter";

            case KeyCode.Escape:
                return "Escape";

            case KeyCode.Space:
                return "Space";

            case KeyCode.Mouse0:
                return "Left Mouse";

            case KeyCode.Mouse1:
                return "Right Mouse";

            case KeyCode.Mouse2:
                return "Middle Mouse";

            case KeyCode.None:
                return "Unbound";
        }

        string keyName = key.ToString();

        if (keyName.StartsWith("Alpha"))
            return keyName.Replace("Alpha", "");

        if (keyName.StartsWith("Keypad"))
            return keyName.Replace("Keypad", "Num ");

        return keyName;
    }
}