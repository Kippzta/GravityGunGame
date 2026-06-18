using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class CoopFirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera playerCamera;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float runSpeed = 9f;
    [SerializeField] private float runAccelerationTime = 0.75f;
    [SerializeField] private float runDecelerationTime = 0.35f;
    [SerializeField] private float acceleration = 45f;
    [SerializeField] private float groundDrag = 7f;
    [SerializeField] private float airControl = 0.25f;
    [SerializeField] private bool requireForwardInputToRun = true;
    [SerializeField] private bool disableRunningWhileHeavyTethered = true;
    [SerializeField] private bool resetRunBlendWhenNoInput = true;

    [Header("Jump / Gravity")]
    [SerializeField] private float jumpForce = 5.6f;
    [SerializeField] private float gravityMultiplier = 2.2f;
    [SerializeField] private float fallGravityMultiplier = 3.2f;
    [SerializeField] private float groundedStickForce = 20f;
    [SerializeField] private float maxFallSpeed = 35f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.18f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Tether Movement")]
    [SerializeField] private float tetherMoveForce = 45f;

    [Range(0f, 1f)]
    [SerializeField] private float tetherWalkMultiplier = 0.45f;

    [Range(0f, 1f)]
    [SerializeField] private float tetherAirControlMultiplier = 0.05f;

    [Range(0f, 1f)]
    [SerializeField] private float tetherAirMoveForceMultiplier = 0f;

    [Header("Air Momentum")]
    [SerializeField] private bool preserveAirMomentum = true;
    [SerializeField] private bool disableAirBrakingWhenNoInput = true;

    [Range(0f, 1f)]
    [SerializeField] private float airDirectionChangeStrength = 0.15f;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private PullGun pullGun;

    private float pitch;
    private bool grounded;
    private bool jumpRequested;
    private Vector2 moveInput;
    private bool runInputHeld;
    private bool wantsToRun;
    private bool isRunning;
    private float runBlend;
    private float currentMoveSpeed;

    public Rigidbody Rigidbody => rb;
    public Camera PlayerCamera => playerCamera;
    public bool IsGrounded => grounded;
    public Vector2 MoveInput => moveInput;
    public bool HasMoveInput => moveInput.sqrMagnitude > 0.001f;
    public bool WantsToRun => wantsToRun;
    public bool IsRunning => isRunning;
    public float RunBlend => runBlend;
    public float CurrentMoveSpeed => currentMoveSpeed;
    public float WalkSpeed => walkSpeed;
    public float RunSpeed => runSpeed;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        pullGun = GetComponent<PullGun>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        rb.freezeRotation = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        currentMoveSpeed = walkSpeed;
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        ReadLookInput();
        ReadMovementInput();
        UpdateRunState();
        ReadJumpInput();
        UpdateCursorLock();
    }

    private void FixedUpdate()
    {
        CheckGrounded();
        ApplyCustomGravity();
        ApplyMovement();

        if (jumpRequested)
        {
            Jump();
            jumpRequested = false;
        }

        ClampFallSpeed();
    }

    private void ReadLookInput()
    {
        if (Mouse.current == null)
        {
            return;
        }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        transform.Rotate(Vector3.up * mouseDelta.x * mouseSensitivity);

        pitch -= mouseDelta.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void ReadMovementInput()
    {
        moveInput = Vector2.zero;
        runInputHeld = false;

        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.aKey.isPressed) moveInput.x -= 1f;
        if (Keyboard.current.dKey.isPressed) moveInput.x += 1f;
        if (Keyboard.current.sKey.isPressed) moveInput.y -= 1f;
        if (Keyboard.current.wKey.isPressed) moveInput.y += 1f;

        runInputHeld =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
    }

    private void UpdateRunState()
    {
        bool hasInput = HasMoveInput;
        bool hasForwardInput = moveInput.y > 0.1f;
        bool heavyTethered = pullGun != null && pullGun.IsTetheredToHeavyTarget;

        wantsToRun = grounded && runInputHeld && hasInput;

        if (requireForwardInputToRun)
        {
            wantsToRun = wantsToRun && hasForwardInput;
        }

        if (disableRunningWhileHeavyTethered && heavyTethered)
        {
            wantsToRun = false;
        }

        if (!hasInput && resetRunBlendWhenNoInput)
        {
            wantsToRun = false;
        }

        float targetBlend = wantsToRun ? 1f : 0f;
        float blendTime = wantsToRun ? runAccelerationTime : runDecelerationTime;
        float blendSpeed = blendTime <= 0.001f ? 999f : 1f / blendTime;

        runBlend = Mathf.MoveTowards(runBlend, targetBlend, blendSpeed * Time.deltaTime);
        currentMoveSpeed = Mathf.Lerp(walkSpeed, runSpeed, runBlend);

        isRunning = runBlend > 0.05f && wantsToRun;
    }

    private void ReadJumpInput()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && grounded)
        {
            jumpRequested = true;
        }
    }

    private void UpdateCursorLock()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void ApplyCustomGravity()
    {
        float multiplier = rb.linearVelocity.y < 0f ? fallGravityMultiplier : gravityMultiplier;

        rb.AddForce(Physics.gravity * multiplier, ForceMode.Acceleration);

        if (grounded && rb.linearVelocity.y <= 0f)
        {
            rb.AddForce(Vector3.down * groundedStickForce, ForceMode.Acceleration);
        }
    }

    private void ApplyMovement()
    {
        Vector3 wishDirection = transform.right * moveInput.x + transform.forward * moveInput.y;
        bool heavyTethered = pullGun != null && pullGun.IsTetheredToHeavyTarget;

        if (grounded)
        {
            ApplyGroundMovement(wishDirection, heavyTethered);
        }
        else
        {
            ApplyAirMovement(wishDirection, heavyTethered);
        }
    }

    private void ApplyGroundMovement(Vector3 wishDirection, bool heavyTethered)
    {
        float speedMultiplier = heavyTethered ? tetherWalkMultiplier : 1f;

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 targetVelocity = wishDirection * currentMoveSpeed * speedMultiplier;
        Vector3 velocityChange = targetVelocity - horizontalVelocity;

        rb.AddForce(velocityChange * acceleration, ForceMode.Acceleration);

        if (!heavyTethered)
        {
            rb.AddForce(-horizontalVelocity * groundDrag, ForceMode.Acceleration);
        }
        else
        {
            rb.AddForce(wishDirection * tetherMoveForce, ForceMode.Acceleration);
        }
    }

    private void ApplyAirMovement(Vector3 wishDirection, bool heavyTethered)
    {
        if (!HasMoveInput && disableAirBrakingWhenNoInput)
        {
            return;
        }

        if (!HasMoveInput)
        {
            return;
        }

        float control = airControl;

        if (heavyTethered)
        {
            control *= tetherAirControlMultiplier;
        }

        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        if (preserveAirMomentum)
        {
            rb.AddForce(wishDirection * acceleration * control, ForceMode.Acceleration);

            Vector3 velocityInInputDirection = Vector3.Project(horizontalVelocity, wishDirection);
            Vector3 sidewaysVelocity = horizontalVelocity - velocityInInputDirection;

            if (sidewaysVelocity.sqrMagnitude > 0.001f)
            {
                rb.AddForce(
                    -sidewaysVelocity * acceleration * control * airDirectionChangeStrength,
                    ForceMode.Acceleration
                );
            }
        }
        else
        {
            Vector3 targetVelocity = wishDirection * currentMoveSpeed;
            Vector3 velocityChange = targetVelocity - horizontalVelocity;

            rb.AddForce(velocityChange * acceleration * control, ForceMode.Acceleration);
        }

        if (heavyTethered)
        {
            rb.AddForce(wishDirection * tetherMoveForce * tetherAirMoveForceMultiplier, ForceMode.Acceleration);
        }
    }

    private void Jump()
    {
        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;

        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    private void ClampFallSpeed()
    {
        if (rb.linearVelocity.y < -maxFallSpeed)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, -maxFallSpeed, rb.linearVelocity.z);
        }
    }

    private void CheckGrounded()
    {
        float radius = capsule.radius * 0.95f;
        float castDistance = capsule.height * 0.5f - radius + groundCheckDistance;
        Vector3 origin = transform.position + Vector3.up * 0.05f;

        grounded = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out _,
            castDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );
    }
}