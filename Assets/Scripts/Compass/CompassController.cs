using UnityEngine;

public sealed class CompassController : MonoBehaviour
{
    // ==================================================
    // REFERENCES
    // ==================================================

    [Header("References")]
    [SerializeField] private Transform player;

    [Tooltip(
        "Optional. Wird momentan nur als Referenz gespeichert. " +
        "Die Marker selbst werden einzeln bewegt."
    )]
    [SerializeField] private RectTransform compassBar;

    // ==================================================
    // COMPASS SETTINGS
    // ==================================================

    [Header("Compass Settings")]
    [SerializeField, Min(1f)]
    private float compassWidth = 750f;

    [SerializeField, Min(0f)]
    private float minimumRotationChange = 0.01f;

    // ==================================================
    // CARDINAL MARKERS
    // ==================================================

    [Header("Cardinal Markers")]
    [SerializeField] private RectTransform markerN;
    [SerializeField] private RectTransform markerE;
    [SerializeField] private RectTransform markerS;
    [SerializeField] private RectTransform markerW;

    // ==================================================
    // PRIVATE STATE
    // ==================================================

    private float halfWidth;
    private float lastYaw;
    private bool hasInitialized;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        RecalculateWidth();
    }

    private void Start()
    {
        RefreshCompassImmediately();
    }

    private void OnValidate()
    {
        compassWidth = Mathf.Max(1f, compassWidth);
        minimumRotationChange =
            Mathf.Max(0f, minimumRotationChange);

        RecalculateWidth();
    }

    private void Update()
    {
        if (player == null)
            return;

        float playerYaw =
            NormalizeAngle(player.eulerAngles.y);

        if (hasInitialized)
        {
            float rotationDifference =
                Mathf.Abs(
                    Mathf.DeltaAngle(
                        lastYaw,
                        playerYaw
                    )
                );

            if (rotationDifference <
                minimumRotationChange)
            {
                return;
            }
        }

        UpdateCompass(playerYaw);
    }

    // ==================================================
    // COMPASS UPDATE
    // ==================================================

    public void RefreshCompassImmediately()
    {
        RecalculateWidth();

        if (player == null)
        {
            hasInitialized = false;
            return;
        }

        float playerYaw =
            NormalizeAngle(player.eulerAngles.y);

        UpdateCompass(playerYaw);
    }

    private void UpdateCompass(float playerYaw)
    {
        UpdateCardinalMarker(
            markerN,
            0f,
            playerYaw
        );

        UpdateCardinalMarker(
            markerE,
            90f,
            playerYaw
        );

        UpdateCardinalMarker(
            markerS,
            180f,
            playerYaw
        );

        UpdateCardinalMarker(
            markerW,
            270f,
            playerYaw
        );

        lastYaw = playerYaw;
        hasInitialized = true;
    }

    private void UpdateCardinalMarker(
        RectTransform marker,
        float markerAngle,
        float playerYaw
    )
    {
        if (marker == null)
            return;

        float relativeAngle =
            Mathf.DeltaAngle(
                playerYaw,
                markerAngle
            );

        float xPosition =
            relativeAngle / 180f *
            halfWidth;

        Vector2 anchoredPosition =
            marker.anchoredPosition;

        anchoredPosition.x = xPosition;

        marker.anchoredPosition =
            anchoredPosition;
    }

    // ==================================================
    // REFERENCES
    // ==================================================

    public void SetPlayer(Transform newPlayer)
    {
        player = newPlayer;
        RefreshCompassImmediately();
    }

    // ==================================================
    // HELPERS
    // ==================================================

    private void RecalculateWidth()
    {
        halfWidth =
            Mathf.Max(1f, compassWidth) /
            2f;
    }

    private float NormalizeAngle(float angle)
    {
        return Mathf.Repeat(angle, 360f);
    }
}