using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class CharacterPreviewRotation : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, ICanvasRaycastFilter
{
    [SerializeField] private Transform previewRoot;
    [SerializeField] private float rotationSpeed = 0.25f;
    [SerializeField] private bool invertDragDirection;
    [SerializeField] private Canvas targetCanvas;
    [SerializeField] private Selectable[] excludedControls = new Selectable[0];
    [SerializeField] private RectTransform[] excludedUiAreas = new RectTransform[0];

    private RectTransform rectTransform;
    private bool isDragging;
    private int activePointerId = int.MinValue;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnDisable()
    {
        isDragging = false;
        activePointerId = int.MinValue;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        CacheReferences();
        if (!IsRaycastLocationValid(eventData.position, eventData.pressEventCamera))
        {
            return;
        }

        isDragging = true;
        activePointerId = eventData.pointerId;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || eventData == null || eventData.pointerId != activePointerId || previewRoot == null)
        {
            return;
        }

        float canvasScale = targetCanvas != null && targetCanvas.scaleFactor > 0f
            ? targetCanvas.scaleFactor
            : 1f;

        float direction = invertDragDirection ? -1f : 1f;
        float deltaY = eventData.delta.x / canvasScale * rotationSpeed * direction;
        Vector3 eulerAngles = previewRoot.localEulerAngles;

        previewRoot.localRotation = Quaternion.Euler(0f, eulerAngles.y + deltaY, 0f);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData == null || eventData.pointerId != activePointerId)
        {
            return;
        }

        isDragging = false;
        activePointerId = int.MinValue;
    }

    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        CacheReferences();

        if (rectTransform == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPoint, eventCamera))
        {
            return false;
        }

        return !IsPointerOverExcludedArea(screenPoint, eventCamera);
    }

    private void CacheReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (targetCanvas == null)
        {
            targetCanvas = GetComponentInParent<Canvas>();
        }
    }

    private bool IsPointerOverExcludedArea(Vector2 screenPoint, Camera eventCamera)
    {
        if (excludedControls != null)
        {
            for (int i = 0; i < excludedControls.Length; i++)
            {
                Selectable selectable = excludedControls[i];
                if (selectable == null || !selectable.isActiveAndEnabled)
                {
                    continue;
                }

                RectTransform selectableRect = selectable.transform as RectTransform;
                if (IsActiveRectUnderPointer(selectableRect, screenPoint, eventCamera))
                {
                    return true;
                }
            }
        }

        if (excludedUiAreas != null)
        {
            for (int i = 0; i < excludedUiAreas.Length; i++)
            {
                if (IsActiveRectUnderPointer(excludedUiAreas[i], screenPoint, eventCamera))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsActiveRectUnderPointer(RectTransform candidate, Vector2 screenPoint, Camera eventCamera)
    {
        return candidate != null &&
            candidate.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(candidate, screenPoint, eventCamera);
    }
}
