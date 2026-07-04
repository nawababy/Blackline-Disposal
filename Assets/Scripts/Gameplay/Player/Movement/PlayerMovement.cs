using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMovement : MonoBehaviour
{
    private const float WalkAnimationState = 0f;
    private const float RunAnimationState = 1f;

    // ==================================================
    // MOVEMENT SETTINGS
    // ==================================================

    [Header("Speed")]
    [SerializeField, Min(0f)]
    private float walkSpeed = 4f;

    [SerializeField, Min(1f)]
    private float sprintMultiplier = 1.5f;

    [SerializeField, Range(0.1f, 1f)]
    private float crouchSpeedMultiplier = 0.5f;

    // ==================================================
    // JUMP AND GRAVITY
    // ==================================================

    [Header("Jump & Gravity")]
    [SerializeField, Min(0f)]
    private float jumpHeight = 1.5f;

    [SerializeField]
    private float gravity = -9.81f;

    [SerializeField]
    private float groundedYVelocity = -2f;

    // ==================================================
    // CROUCH
    // ==================================================

    [Header("Crouch")]
    [SerializeField, Min(0.1f)]
    private float standingHeight = 2f;

    [SerializeField, Min(0.1f)]
    private float crouchingHeight = 1f;

    [SerializeField, Min(0f)]
    private float crouchTransitionSpeed = 10f;

    [SerializeField]
    private LayerMask standUpBlockerMask = ~0;

    [SerializeField, Min(0f)]
    private float standUpPadding = 0.02f;

    [SerializeField, Min(0f)]
    private float standUpRetryInterval = 0.1f;

    // ==================================================
    // INPUT
    // ==================================================

    [Header("Input")]
    [SerializeField]
    private InputSettings inputSettings;

    [Tooltip(
        "Nur der lokale Spieler darf Input lesen. " +
        "Wird außerdem beim Öffnen des Pause-Menüs deaktiviert."
    )]
    [SerializeField]
    private bool canReadInput = true;

    // ==================================================
    // ANIMATION
    // ==================================================

    [Header("Animation")]
    [SerializeField]
    private Animator animator;

    [SerializeField]
    private string horizontalParameter = "Hor";

    [SerializeField]
    private string verticalParameter = "Vert";

    [SerializeField]
    private string jumpParameter = "IsJump";

    [SerializeField]
    private string stateParameter = "State";

    [SerializeField]
    private string crouchParameter = "IsCrouching";

    // ==================================================
    // PUBLIC STATE
    // ==================================================

    public bool IsCrouching { get; private set; }

    public bool CanReadInput => canReadInput;

    // ==================================================
    // PRIVATE STATE
    // ==================================================

    private CharacterController characterController;

    private Vector3 verticalVelocity;
    private Vector2 inputAxis;

    private float currentHeight;
    private float nextStandUpCheckTime;

    private bool isRunning;
    private bool jumpPressed;
    private bool wantsToCrouch;
    private bool standUpBufferWarningLogged;

    private int horizontalParameterHash;
    private int verticalParameterHash;
    private int jumpParameterHash;
    private int stateParameterHash;
    private int crouchParameterHash;

    private readonly Collider[] standUpResults =
        new Collider[16];

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================

    private void Awake()
    {
        characterController =
            GetComponent<CharacterController>();

        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>();
        }

        if (inputSettings != null)
            inputSettings.EnsureLoaded();

        ValidateSettings();
        CacheAnimatorHashes();

        currentHeight = standingHeight;
        wantsToCrouch = IsCrouching;
        ApplyControllerHeight(currentHeight);
    }

    private void OnValidate()
    {
        ValidateSettings();
        CacheAnimatorHashes();
    }

    private void Update()
    {
        if (canReadInput)
            GatherInput();

        /*
         * HandleCrouch läuft auch bei deaktiviertem Input weiter,
         * damit eine bereits begonnene Höhenanimation sauber
         * abgeschlossen werden kann.
         *
         * Neue Tastatureingaben werden innerhalb der Methode
         * aber nur gelesen, wenn canReadInput true ist.
         */
        HandleCrouch();

        /*
         * Bewegung läuft weiterhin, damit Gravitation auch
         * bei geöffnetem Pause-Menü funktioniert.
         *
         * Horizontale Eingaben werden beim Deaktivieren gelöscht.
         */
        HandleMovement();

        UpdateAnimator();

        jumpPressed = false;
    }

    // ==================================================
    // INPUT
    // ==================================================

    private void GatherInput()
    {
        inputAxis = new Vector2(
            Input.GetAxisRaw("Horizontal"),
            Input.GetAxisRaw("Vertical")
        );

        if (inputSettings == null)
        {
            isRunning = false;
            jumpPressed = false;
            return;
        }

        isRunning =
            Input.GetKey(inputSettings.sprintKey);

        if (Input.GetKeyDown(inputSettings.jumpKey))
            jumpPressed = true;
    }

    // ==================================================
    // MOVEMENT
    // ==================================================

    private void HandleMovement()
    {
        if (characterController == null ||
            !characterController.enabled)
        {
            return;
        }

        Vector3 moveDirection =
            transform.right * inputAxis.x +
            transform.forward * inputAxis.y;

        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();

        float finalSpeed = walkSpeed;

        if (isRunning && !IsCrouching)
            finalSpeed *= sprintMultiplier;

        if (IsCrouching)
            finalSpeed *= crouchSpeedMultiplier;

        Vector3 horizontalMovement =
            moveDirection * finalSpeed;

        if (characterController.isGrounded &&
            verticalVelocity.y < 0f)
        {
            verticalVelocity.y =
                groundedYVelocity;
        }

        if (jumpPressed &&
            characterController.isGrounded &&
            !IsCrouching)
        {
            verticalVelocity.y =
                Mathf.Sqrt(
                    jumpHeight * -2f * gravity
                );
        }

        verticalVelocity.y +=
            gravity * Time.deltaTime;

        Vector3 finalMovement =
            horizontalMovement;

        finalMovement.y =
            verticalVelocity.y;

        characterController.Move(
            finalMovement * Time.deltaTime
        );
    }

    // ==================================================
    // CROUCH
    // ==================================================

    private void HandleCrouch()
    {
        /*
         * Wichtig:
         * Crouch-Tasten werden ausschließlich gelesen,
         * wenn der Spieler gerade Input lesen darf.
         */
        if (canReadInput && inputSettings != null)
        {
            if (inputSettings.crouchToggle)
            {
                if (Input.GetKeyDown(
                        inputSettings.crouchKey))
                {
                    wantsToCrouch =
                        !wantsToCrouch;
                }
            }
            else
            {
                wantsToCrouch =
                    Input.GetKey(
                        inputSettings.crouchKey
                    );
            }
        }

        if (wantsToCrouch)
        {
            IsCrouching = true;
        }
        else if (IsCrouching &&
                 ShouldRetryStandUp() &&
                 CanStandUp())
        {
            IsCrouching = false;
        }

        float targetHeight =
            IsCrouching
                ? crouchingHeight
                : standingHeight;

        if (Mathf.Approximately(
                currentHeight,
                targetHeight))
        {
            return;
        }

        currentHeight =
            Mathf.MoveTowards(
                currentHeight,
                targetHeight,
                crouchTransitionSpeed *
                Time.deltaTime
            );

        ApplyControllerHeight(currentHeight);
    }

    private void ApplyControllerHeight(float height)
    {
        if (characterController == null)
            return;

        characterController.height = height;

        characterController.center =
            Vector3.up * (height * 0.5f);
    }

    private bool ShouldRetryStandUp()
    {
        if (standUpRetryInterval <= 0f)
            return true;

        if (Time.time < nextStandUpCheckTime)
            return false;

        nextStandUpCheckTime =
            Time.time + standUpRetryInterval;

        return true;
    }

    private bool CanStandUp()
    {
        if (characterController == null)
            return true;

        if (standingHeight <= currentHeight + standUpPadding)
            return true;

        float radius = GetScaledControllerRadius();
        float verticalScale = Mathf.Abs(transform.lossyScale.y);

        float scaledCurrentHeight =
            Mathf.Max(currentHeight * verticalScale, radius * 2f);

        float scaledStandingHeight =
            Mathf.Max(standingHeight * verticalScale, radius * 2f);

        Vector3 currentCenter =
            GetWorldControllerCenter(currentHeight);

        Vector3 standingCenter =
            GetWorldControllerCenter(standingHeight);

        Vector3 up = transform.up;

        float currentHalfLine =
            Mathf.Max(0f, (scaledCurrentHeight * 0.5f) - radius);

        float standingHalfLine =
            Mathf.Max(0f, (scaledStandingHeight * 0.5f) - radius);

        Vector3 currentTop =
            currentCenter + up * currentHalfLine;

        Vector3 standingTop =
            standingCenter + up * standingHalfLine;

        float capsuleRadius =
            Mathf.Max(0.001f, radius - standUpPadding);

        Vector3 bottom =
            currentTop + up * capsuleRadius;

        Vector3 top =
            standingTop - up * capsuleRadius;

        if (Vector3.Dot(top - bottom, up) < 0f)
            top = bottom;

        int hitCount =
            Physics.OverlapCapsuleNonAlloc(
                bottom,
                top,
                capsuleRadius,
                standUpResults,
                standUpBlockerMask,
                QueryTriggerInteraction.Ignore
            );

        if (hitCount >= standUpResults.Length)
        {
            if (!standUpBufferWarningLogged)
            {
                Debug.LogWarning(
                    "PlayerMovement stand-up check reached the collider buffer limit. Staying crouched.",
                    this
                );

                standUpBufferWarningLogged = true;
            }

            ClearStandUpResults(hitCount);
            return false;
        }

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = standUpResults[i];
            if (hit == null ||
                hit == characterController ||
                hit.transform == transform ||
                hit.transform.IsChildOf(transform))
            {
                continue;
            }

            ClearStandUpResults(hitCount);
            return false;
        }

        standUpBufferWarningLogged = false;
        ClearStandUpResults(hitCount);
        return true;
    }

    private Vector3 GetWorldControllerCenter(float height)
    {
        Vector3 localCenter =
            characterController != null
                ? characterController.center
                : Vector3.up * (currentHeight * 0.5f);

        localCenter.y +=
            (height - currentHeight) * 0.5f;

        return transform.TransformPoint(localCenter);
    }

    private float GetScaledControllerRadius()
    {
        float horizontalScale =
            Mathf.Max(
                Mathf.Abs(transform.lossyScale.x),
                Mathf.Abs(transform.lossyScale.z)
            );

        return Mathf.Max(
            0.001f,
            characterController.radius * horizontalScale
        );
    }

    private void ClearStandUpResults(int hitCount)
    {
        int clearCount =
            Mathf.Min(hitCount, standUpResults.Length);

        for (int i = 0; i < clearCount; i++)
            standUpResults[i] = null;
    }

    // ==================================================
    // ANIMATION
    // ==================================================

    private void UpdateAnimator()
    {
        if (animator == null ||
            characterController == null)
        {
            return;
        }

        animator.SetFloat(
            horizontalParameterHash,
            inputAxis.x
        );

        animator.SetFloat(
            verticalParameterHash,
            inputAxis.y
        );

        animator.SetBool(
            jumpParameterHash,
            !characterController.isGrounded
        );

        animator.SetFloat(
            stateParameterHash,
            IsSprintMovementActive()
                ? RunAnimationState
                : WalkAnimationState
        );

        animator.SetBool(
            crouchParameterHash,
            IsCrouching
        );
    }

    private bool IsSprintMovementActive()
    {
        return !IsCrouching &&
               isRunning &&
               inputAxis.sqrMagnitude > 0.0001f;
    }

    // ==================================================
    // EXTERNAL ACCESS
    // ==================================================

    public void SetAnimator(Animator newAnimator)
    {
        if (animator == newAnimator)
            return;

        animator = newAnimator;

        if (animator == null)
        {
            Debug.LogWarning(
                "PlayerMovement received no Animator. Movement continues without animation.",
                this
            );
        }
    }

    public float GetCenterY()
    {
        if (characterController == null)
            return currentHeight * 0.5f;

        return characterController.center.y;
    }

    public void SetInputEnabled(bool value)
    {
        if (canReadInput == value)
            return;

        canReadInput = value;

        if (canReadInput)
            return;

        /*
         * Verhindert, dass der Spieler nach dem Öffnen
         * des Pause-Menüs weiterläuft oder sprintet.
         */
        inputAxis = Vector2.zero;
        isRunning = false;
        jumpPressed = false;

        /*
         * IsCrouching wird absichtlich nicht zurückgesetzt.
         * Öffnet der Spieler das Menü im Ducken, bleibt
         * sein Charakter währenddessen geduckt.
         */
    }

    // ==================================================
    // VALIDATION
    // ==================================================

    private void ValidateSettings()
    {
        standingHeight =
            Mathf.Max(0.1f, standingHeight);

        crouchingHeight =
            Mathf.Clamp(
                crouchingHeight,
                0.1f,
                standingHeight
            );

        walkSpeed =
            Mathf.Max(0f, walkSpeed);

        sprintMultiplier =
            Mathf.Max(1f, sprintMultiplier);

        crouchSpeedMultiplier =
            Mathf.Clamp(
                crouchSpeedMultiplier,
                0.1f,
                1f
            );

        jumpHeight =
            Mathf.Max(0f, jumpHeight);

        crouchTransitionSpeed =
            Mathf.Max(
                0f,
                crouchTransitionSpeed
            );

        standUpPadding =
            Mathf.Max(0f, standUpPadding);

        standUpRetryInterval =
            Mathf.Max(0f, standUpRetryInterval);

        if (gravity > -0.01f)
            gravity = -9.81f;

        if (groundedYVelocity > 0f)
            groundedYVelocity = -2f;
    }

    private void CacheAnimatorHashes()
    {
        horizontalParameterHash =
            Animator.StringToHash(horizontalParameter);

        verticalParameterHash =
            Animator.StringToHash(verticalParameter);

        jumpParameterHash =
            Animator.StringToHash(jumpParameter);

        stateParameterHash =
            Animator.StringToHash(stateParameter);

        crouchParameterHash =
            Animator.StringToHash(crouchParameter);
    }
}
