using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class PlayerMovement : MonoBehaviour
{
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

    private bool isRunning;
    private bool jumpPressed;

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

        currentHeight = standingHeight;
        ApplyControllerHeight(currentHeight);
    }

    private void OnValidate()
    {
        ValidateSettings();
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

        if (isRunning)
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
                    IsCrouching =
                        !IsCrouching;
                }
            }
            else
            {
                IsCrouching =
                    Input.GetKey(
                        inputSettings.crouchKey
                    );
            }
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
            horizontalParameter,
            inputAxis.x
        );

        animator.SetFloat(
            verticalParameter,
            inputAxis.y
        );

        animator.SetBool(
            jumpParameter,
            !characterController.isGrounded
        );
    }

    // ==================================================
    // EXTERNAL ACCESS
    // ==================================================

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

        if (gravity > -0.01f)
            gravity = -9.81f;

        if (groundedYVelocity > 0f)
            groundedYVelocity = -2f;
    }
}