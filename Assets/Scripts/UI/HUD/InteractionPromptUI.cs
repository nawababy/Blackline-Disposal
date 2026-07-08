using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InteractionPromptUI : MonoBehaviour
{
    private enum PromptType
    {
        None,
        Pickup,
        Facility
    }

    [Header("Text")]
    [SerializeField]
    private TMP_Text promptText;

    [SerializeField]
    private RectTransform promptRectTransform;

    [Header("Canvas")]
    [SerializeField]
    private Canvas canvas;

    private RectTransform canvasRectTransform;
    private Camera canvasCamera;

    private PromptType currentPromptType;
    private KeyCode currentPickupKey = KeyCode.None;
    private bool currentPickupHold;
    private int currentFacilitySeconds = -1;

    private void Awake()
    {
        CacheReferences();
        HidePrompt();
    }

    private void OnEnable()
    {
        CacheCanvasCamera();
    }

    private void OnDisable()
    {
        HidePrompt();
    }

    public void ShowPickupPrompt(
        KeyCode pickupKey,
        bool pickupHold
    )
    {
        if (promptText == null)
            return;

        if (currentPromptType ==
                PromptType.Pickup &&
            currentPickupKey == pickupKey &&
            currentPickupHold == pickupHold)
        {
            return;
        }

        currentPromptType =
            PromptType.Pickup;

        currentPickupKey =
            pickupKey;

        currentPickupHold =
            pickupHold;

        currentFacilitySeconds = -1;

        string actionText =
            pickupHold
                ? "Carry"
                : "Pick up";

        promptText.text =
            $"[{FormatKeyName(pickupKey)}] " +
            actionText;

        promptText.enabled = true;
    }

    public bool SetScreenPosition(
        Vector2 screenPosition
    )
    {
        if (promptRectTransform == null ||
            canvasRectTransform == null)
        {
            return false;
        }

        if (!RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    canvasRectTransform,
                    screenPosition,
                    canvasCamera,
                    out Vector2 localPosition
                ))
        {
            return false;
        }

        promptRectTransform.position =
            canvasRectTransform.TransformPoint(
                localPosition
            );

        return true;
    }

    public void ShowFacilityProcessingTime(
        float processingTimeRemaining
    )
    {
        int remainingSeconds =
            Mathf.CeilToInt(
                processingTimeRemaining
            );

        if (remainingSeconds <= 0)
        {
            HidePrompt();
            return;
        }

        if (promptText == null)
            return;

        if (currentPromptType ==
                PromptType.Facility &&
            currentFacilitySeconds ==
                remainingSeconds)
        {
            return;
        }

        currentPromptType =
            PromptType.Facility;

        currentPickupKey =
            KeyCode.None;

        currentPickupHold = false;
        currentFacilitySeconds =
            remainingSeconds;

        promptText.text =
            $"Processing: " +
            FormatDuration(remainingSeconds);

        promptText.enabled = true;
    }

    public void HidePrompt()
    {
        if (currentPromptType ==
                PromptType.None &&
            (promptText == null ||
             !promptText.enabled))
        {
            return;
        }

        currentPromptType =
            PromptType.None;

        currentPickupKey =
            KeyCode.None;

        currentPickupHold = false;
        currentFacilitySeconds = -1;

        if (promptText == null)
            return;

        promptText.text = string.Empty;
        promptText.enabled = false;
    }

    private void CacheReferences()
    {
        if (promptRectTransform == null &&
            promptText != null)
        {
            promptRectTransform =
                promptText.GetComponent<
                    RectTransform>();
        }

        if (canvas == null &&
            promptRectTransform != null)
        {
            canvas =
                promptRectTransform
                    .GetComponentInParent<Canvas>();
        }

        canvasRectTransform =
            canvas != null
                ? canvas.transform as RectTransform
                : null;

        CacheCanvasCamera();
    }

    private void CacheCanvasCamera()
    {
        canvasCamera =
            canvas != null &&
            canvas.renderMode !=
                RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
    }

    private static string FormatDuration(
        int totalSeconds
    )
    {
        if (totalSeconds < 60)
            return $"{totalSeconds}s";

        int seconds =
            totalSeconds % 60;

        int totalMinutes =
            totalSeconds / 60;

        if (totalMinutes < 60)
        {
            return
                $"{totalMinutes}:{seconds:00}";
        }

        int hours =
            totalMinutes / 60;

        int minutes =
            totalMinutes % 60;

        return
            $"{hours}:{minutes:00}:{seconds:00}";
    }

    private static string FormatKeyName(
        KeyCode key
    )
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
                return "Mouse 1";

            case KeyCode.Mouse1:
                return "Mouse 2";

            case KeyCode.Mouse2:
                return "Mouse 3";

            case KeyCode.Mouse3:
                return "Mouse 4";

            case KeyCode.Mouse4:
                return "Mouse 5";

            case KeyCode.Mouse5:
                return "Mouse 6";

            case KeyCode.Mouse6:
                return "Mouse 7";

            case KeyCode.None:
                return "Unbound";
        }

        string keyName =
            key.ToString();

        if (keyName.StartsWith("Alpha"))
            return keyName.Replace("Alpha", "");

        if (keyName.StartsWith("Keypad"))
        {
            return
                keyName.Replace(
                    "Keypad",
                    "Num "
                );
        }

        return keyName;
    }
}
