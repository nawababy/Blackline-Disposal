using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerInteractionFeedbackController : MonoBehaviour
{
    [Header("Local Player")]
    [SerializeField]
    private Camera playerCamera;

    [SerializeField]
    private CarryTrash carryTrash;

    [SerializeField]
    private InputSettings inputSettings;

    [Header("UI")]
    [SerializeField]
    private InteractionPromptUI promptUI;

    [Header("Prompt Position")]
    [SerializeField, Min(0f)]
    private float pickupWorldVerticalOffset = 0.15f;

    [SerializeField]
    private Vector2 pickupScreenOffset =
        new Vector2(0f, 24f);

    [SerializeField]
    private Vector2 facilityScreenOffset =
        new Vector2(0f, 24f);

    [Header("Facility")]
    [SerializeField, Min(0.1f)]
    private float facilityViewRange = 2f;

    private Trash cachedTrashTarget;
    private Renderer[] cachedTrashRenderers;
    private Vector3 cachedTrashLocalAnchor;
    private bool hasCachedTrashAnchor;

    private void Awake()
    {
        ResolveLocalReferences();

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
        ClearCachedTrashTarget();

        if (promptUI != null)
            promptUI.HidePrompt();
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    private void Update()
    {
        if (!CanShowInteractionFeedback())
        {
            ClearCachedTrashTarget();
            HidePrompt();
            return;
        }

        if (carryTrash.TryGetCurrentPickupTarget(
                out CarryTrash.PickupTargetResult
                    pickupTarget) &&
            pickupTarget.CanPickup)
        {
            if (!TryGetPickupScreenPosition(
                    pickupTarget,
                    out Vector2 screenPosition) ||
                !promptUI.SetScreenPosition(
                    screenPosition))
            {
                HidePrompt();
                return;
            }

            promptUI.ShowPickupPrompt(
                inputSettings.pickupKey,
                inputSettings.pickupHold
            );

            return;
        }

        ClearCachedTrashTarget();

        if (TryGetViewedProcessingFacility(
                out TrashFacility facility,
                out RaycastHit facilityHit) &&
            TryGetScreenPosition(
                facilityHit.point,
                facilityScreenOffset,
                out Vector2 facilityScreenPosition) &&
            promptUI.SetScreenPosition(
                facilityScreenPosition))
        {
            promptUI.ShowFacilityProcessingTime(
                facility.ProcessingTimeRemaining
            );

            return;
        }

        HidePrompt();
    }

    private bool TryGetViewedProcessingFacility(
        out TrashFacility facility,
        out RaycastHit hit
    )
    {
        facility = null;
        hit = default;

        if (playerCamera == null)
            return false;

        bool hasHit =
            Physics.Raycast(
                playerCamera.transform.position,
                playerCamera.transform.forward,
                out hit,
                facilityViewRange,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore
            );

        if (!hasHit ||
            hit.distance > facilityViewRange)
        {
            return false;
        }

        facility =
            hit.collider.GetComponentInParent<
                TrashFacility>();

        return facility != null &&
               facility.isActiveAndEnabled &&
               facility.IsProcessing &&
               facility.ProcessingTimeRemaining > 0f;
    }

    private bool TryGetPickupScreenPosition(
        CarryTrash.PickupTargetResult pickupTarget,
        out Vector2 screenPosition
    )
    {
        screenPosition = default;

        Trash trash =
            pickupTarget.Trash;

        if (trash == null ||
            !trash.isActiveAndEnabled)
        {
            ClearCachedTrashTarget();
            return false;
        }

        CacheTrashTarget(trash);

        Vector3 worldPosition;

        if (hasCachedTrashAnchor)
        {
            worldPosition =
                trash.transform.TransformPoint(
                    cachedTrashLocalAnchor
                );
        }
        else if (pickupTarget.Collider != null)
        {
            worldPosition =
                pickupTarget.RaycastHit.point;
        }
        else
        {
            worldPosition =
                trash.transform.position;
        }

        worldPosition +=
            Vector3.up *
            pickupWorldVerticalOffset;

        return TryGetScreenPosition(
            worldPosition,
            pickupScreenOffset,
            out screenPosition
        );
    }

    private void CacheTrashTarget(Trash trash)
    {
        if (cachedTrashTarget == trash)
            return;

        ClearCachedTrashTarget();

        if (trash == null)
            return;

        cachedTrashTarget = trash;
        cachedTrashRenderers =
            trash.GetComponentsInChildren<Renderer>(
                true
            );

        Bounds combinedBounds = default;
        bool hasBounds = false;

        foreach (Renderer targetRenderer in
                 cachedTrashRenderers)
        {
            if (targetRenderer == null ||
                !targetRenderer.enabled ||
                !targetRenderer.gameObject
                    .activeInHierarchy)
            {
                continue;
            }

            if (!hasBounds)
            {
                combinedBounds =
                    targetRenderer.bounds;

                hasBounds = true;
                continue;
            }

            combinedBounds.Encapsulate(
                targetRenderer.bounds
            );
        }

        if (!hasBounds)
            return;

        Vector3 upperCenter =
            combinedBounds.center;

        upperCenter.y =
            combinedBounds.max.y;

        cachedTrashLocalAnchor =
            trash.transform.InverseTransformPoint(
                upperCenter
            );

        hasCachedTrashAnchor = true;
    }

    private void ClearCachedTrashTarget()
    {
        cachedTrashTarget = null;
        cachedTrashRenderers = null;
        cachedTrashLocalAnchor = Vector3.zero;
        hasCachedTrashAnchor = false;
    }

    private bool TryGetScreenPosition(
        Vector3 worldPosition,
        Vector2 screenOffset,
        out Vector2 screenPosition
    )
    {
        screenPosition = default;

        if (playerCamera == null)
            return false;

        Vector3 projectedPosition =
            playerCamera.WorldToScreenPoint(
                worldPosition
            );

        if (projectedPosition.z <= 0f)
            return false;

        screenPosition =
            new Vector2(
                projectedPosition.x +
                    screenOffset.x,
                projectedPosition.y +
                    screenOffset.y
            );

        return playerCamera.pixelRect.Contains(
            screenPosition
        );
    }

    private bool CanShowInteractionFeedback()
    {
        return playerCamera != null &&
               carryTrash != null &&
               carryTrash.isActiveAndEnabled &&
               carryTrash.CanCarry &&
               inputSettings != null &&
               promptUI != null &&
               promptUI.isActiveAndEnabled;
    }

    private void ResolveLocalReferences()
    {
        if (carryTrash == null)
            carryTrash = GetComponent<CarryTrash>();

        if (playerCamera != null)
            return;

        playerCamera =
            GetComponentInChildren<Camera>(true);
    }

    private void HidePrompt()
    {
        if (promptUI != null)
            promptUI.HidePrompt();
    }

    private void ValidateSettings()
    {
        facilityViewRange =
            Mathf.Max(
                0.1f,
                facilityViewRange
            );

        pickupWorldVerticalOffset =
            Mathf.Max(
                0f,
                pickupWorldVerticalOffset
            );
    }
}
