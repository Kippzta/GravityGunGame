using UnityEngine;

public class FirstPersonCameraAnimator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CoopFirstPersonController controller;
    [SerializeField] private PullGun pullGun;
    [SerializeField] private Rigidbody playerRigidbody;

    [Header("General")]
    [SerializeField] private bool animationsEnabled = true;
    [SerializeField] private float positionFollowSpeed = 16f;
    [SerializeField] private float rotationFollowSpeed = 16f;

    [Header("Walking / Running Bob")]
    [SerializeField] private bool enableMovementBob = true;
    [SerializeField] private bool movementBobOnlyGrounded = true;
    [SerializeField] private bool disableMovementBobWhileHeavyTethered = true;
    [SerializeField] private float walkBobFrequency = 9f;
    [SerializeField] private float runBobFrequency = 13f;
    [SerializeField] private float walkVerticalBobAmount = 0.045f;
    [SerializeField] private float runVerticalBobAmount = 0.075f;
    [SerializeField] private float walkSideBobAmount = 0.025f;
    [SerializeField] private float runSideBobAmount = 0.045f;
    [SerializeField] private float walkRollAmount = 0.6f;
    [SerializeField] private float runRollAmount = 1.2f;
    [SerializeField] private float walkPitchAmount = 0.45f;
    [SerializeField] private float runPitchAmount = 0.9f;
    [SerializeField] private float movementBobBlendSpeed = 10f;

    [Header("Jump")]
    [SerializeField] private bool enableJumpAnimation = true;
    [SerializeField] private float jumpTriggerVelocity = 1.5f;
    [SerializeField] private float jumpDipAmount = 0.08f;
    [SerializeField] private float jumpPitchAmount = -2f;
    [SerializeField] private float jumpAnimationDuration = 0.22f;

    [Header("Landing")]
    [SerializeField] private bool enableLandingAnimation = true;
    [SerializeField] private float minimumLandingSpeed = 2f;
    [SerializeField] private float hardLandingSpeed = 14f;
    [SerializeField] private float maxLandingDipAmount = 0.18f;
    [SerializeField] private float maxLandingPitchAmount = 4f;
    [SerializeField] private float landingAnimationDuration = 0.32f;

    [Header("Wall Impact")]
    [SerializeField] private bool enableWallImpactAnimation = true;
    [SerializeField] private bool wallImpactOnlyWhileTethered = true;
    [SerializeField] private float minimumWallImpactSpeed = 5f;
    [SerializeField] private float hardWallImpactSpeed = 18f;

    [Range(0f, 1f)]
    [SerializeField] private float maxWallNormalY = 0.45f;

    [SerializeField] private float maxWallImpactKickAmount = 0.16f;
    [SerializeField] private float maxWallImpactPitchAmount = 5f;
    [SerializeField] private float maxWallImpactRollAmount = 7f;
    [SerializeField] private float wallImpactAnimationDuration = 0.28f;
    [SerializeField] private float wallImpactCooldown = 0.18f;

    [Header("Swing")]
    [SerializeField] private bool enableSwingAnimation = true;
    [SerializeField] private bool swingOnlyOnHeavyTethers = true;
    [SerializeField] private float maxSwingRoll = 7f;
    [SerializeField] private float maxSwingSideOffset = 0.08f;
    [SerializeField] private float maxSwingBackOffset = 0.06f;
    [SerializeField] private float swingSpeedForMaxAnimation = 18f;
    [SerializeField] private float swingFollowSpeed = 7f;
    [SerializeField] private float swingWaveAmount = 0.04f;
    [SerializeField] private float swingWaveSpeed = 7f;

    [Header("Jitter Safety")]
    [Tooltip("If true, camera animation is applied from the controller's current base camera rotation each frame instead of accumulating on top of itself.")]
    [SerializeField] private bool useStableBaseRotation = true;

    [Tooltip("If true, disables swing camera animation while grounded. This can reduce shake when tethered and touching floors/walls.")]
    [SerializeField] private bool disableSwingAnimationWhileGrounded = false;

    [Tooltip("If true, clears camera animation offsets when animations are disabled.")]
    [SerializeField] private bool resetOffsetsWhenDisabled = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugValues = false;

    private Vector3 originalCameraLocalPosition;

    private Vector3 currentPositionOffset;
    private Vector3 targetPositionOffset;
    private Vector3 currentRotationOffset;
    private Vector3 targetRotationOffset;

    private bool wasGrounded;
    private float previousVerticalVelocity;

    private float movementBobTimer;
    private float movementBobWeight;

    private float jumpTimer;
    private float landingTimer;
    private float wallImpactTimer;
    private float wallImpactCooldownTimer;

    private float jumpStrength;
    private float landingStrength;
    private float wallImpactStrength;

    private Vector3 wallImpactLocalDirection;

    private float smoothedSwingSide;
    private float smoothedSwingSpeed;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<CoopFirstPersonController>();
        }

        if (pullGun == null)
        {
            pullGun = GetComponent<PullGun>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody>();
        }

        if (cameraTransform == null)
        {
            Camera childCamera = GetComponentInChildren<Camera>();

            if (childCamera != null)
            {
                cameraTransform = childCamera.transform;
            }
        }

        if (cameraTransform != null)
        {
            originalCameraLocalPosition = cameraTransform.localPosition;
        }

        if (controller != null)
        {
            wasGrounded = controller.IsGrounded;
        }

        if (playerRigidbody != null)
        {
            previousVerticalVelocity = playerRigidbody.linearVelocity.y;
        }
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
        {
            return;
        }

        Quaternion baseLocalRotation = cameraTransform.localRotation;

        if (!animationsEnabled)
        {
            if (resetOffsetsWhenDisabled)
            {
                currentPositionOffset = Vector3.zero;
                targetPositionOffset = Vector3.zero;
                currentRotationOffset = Vector3.zero;
                targetRotationOffset = Vector3.zero;
            }

            cameraTransform.localPosition = originalCameraLocalPosition;
            cameraTransform.localRotation = baseLocalRotation;
            return;
        }

        if (wallImpactCooldownTimer > 0f)
        {
            wallImpactCooldownTimer -= Time.deltaTime;
        }

        targetPositionOffset = Vector3.zero;
        targetRotationOffset = Vector3.zero;

        DetectJumpAndLanding();

        ApplyMovementBob();
        ApplyJumpAnimation();
        ApplyLandingAnimation();
        ApplyWallImpactAnimation();
        ApplySwingAnimation();

        currentPositionOffset = SmoothVector(currentPositionOffset, targetPositionOffset, positionFollowSpeed);
        currentRotationOffset = SmoothVector(currentRotationOffset, targetRotationOffset, rotationFollowSpeed);

        cameraTransform.localPosition = originalCameraLocalPosition + currentPositionOffset;

        Quaternion additiveRotation = Quaternion.Euler(
            currentRotationOffset.x,
            currentRotationOffset.y,
            currentRotationOffset.z
        );

        if (useStableBaseRotation)
        {
            cameraTransform.localRotation = baseLocalRotation * additiveRotation;
        }
        else
        {
            cameraTransform.localRotation = cameraTransform.localRotation * additiveRotation;
        }

        if (controller != null)
        {
            wasGrounded = controller.IsGrounded;
        }

        if (playerRigidbody != null)
        {
            previousVerticalVelocity = playerRigidbody.linearVelocity.y;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryTriggerWallImpact(collision);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryTriggerWallImpact(collision);
    }

    private void DetectJumpAndLanding()
    {
        if (controller == null || playerRigidbody == null)
        {
            return;
        }

        bool isGrounded = controller.IsGrounded;
        float verticalVelocity = playerRigidbody.linearVelocity.y;

        if (enableJumpAnimation)
        {
            bool justLeftGround = wasGrounded && !isGrounded;

            if (justLeftGround && verticalVelocity > jumpTriggerVelocity)
            {
                jumpTimer = jumpAnimationDuration;
                jumpStrength = Mathf.Clamp01(verticalVelocity / 8f);
            }
        }

        if (enableLandingAnimation)
        {
            bool justLanded = !wasGrounded && isGrounded;

            if (justLanded)
            {
                float landingSpeed = Mathf.Abs(Mathf.Min(previousVerticalVelocity, 0f));

                if (landingSpeed >= minimumLandingSpeed)
                {
                    landingTimer = landingAnimationDuration;
                    landingStrength = Mathf.InverseLerp(minimumLandingSpeed, hardLandingSpeed, landingSpeed);
                }
            }
        }
    }

    private void ApplyMovementBob()
    {
        if (!enableMovementBob || controller == null || playerRigidbody == null)
        {
            movementBobWeight = SmoothFloat(movementBobWeight, 0f, movementBobBlendSpeed);
            return;
        }

        bool heavyTethered = pullGun != null && pullGun.IsTetheredToHeavyTarget;

        bool canBob =
            controller.HasMoveInput &&
            (!movementBobOnlyGrounded || controller.IsGrounded);

        if (disableMovementBobWhileHeavyTethered && heavyTethered)
        {
            canBob = false;
        }

        float targetWeight = canBob ? 1f : 0f;
        movementBobWeight = SmoothFloat(movementBobWeight, targetWeight, movementBobBlendSpeed);

        if (movementBobWeight <= 0.001f)
        {
            return;
        }

        float runBlend = controller.RunBlend;

        float frequency = Mathf.Lerp(walkBobFrequency, runBobFrequency, runBlend);
        float verticalAmount = Mathf.Lerp(walkVerticalBobAmount, runVerticalBobAmount, runBlend);
        float sideAmount = Mathf.Lerp(walkSideBobAmount, runSideBobAmount, runBlend);
        float rollAmount = Mathf.Lerp(walkRollAmount, runRollAmount, runBlend);
        float pitchAmount = Mathf.Lerp(walkPitchAmount, runPitchAmount, runBlend);

        float horizontalSpeed = new Vector3(
            playerRigidbody.linearVelocity.x,
            0f,
            playerRigidbody.linearVelocity.z
        ).magnitude;

        float speed01 = Mathf.Clamp01(horizontalSpeed / Mathf.Max(0.01f, controller.RunSpeed));

        movementBobTimer += Time.deltaTime * frequency * Mathf.Lerp(0.65f, 1.2f, speed01);

        float verticalBob = Mathf.Abs(Mathf.Sin(movementBobTimer)) * verticalAmount;
        float sideBob = Mathf.Sin(movementBobTimer) * sideAmount;
        float rollBob = Mathf.Sin(movementBobTimer) * rollAmount;
        float pitchBob = Mathf.Cos(movementBobTimer * 2f) * pitchAmount;

        targetPositionOffset += Vector3.up * verticalBob * movementBobWeight;
        targetPositionOffset += Vector3.right * sideBob * movementBobWeight;

        targetRotationOffset.z += -rollBob * movementBobWeight;
        targetRotationOffset.x += pitchBob * movementBobWeight;
    }

    private void ApplyJumpAnimation()
    {
        if (!enableJumpAnimation || jumpTimer <= 0f)
        {
            return;
        }

        jumpTimer -= Time.deltaTime;

        float t = 1f - Mathf.Clamp01(jumpTimer / jumpAnimationDuration);
        float curve = Mathf.Sin(t * Mathf.PI);
        float fade = 1f - t;
        float strength = jumpStrength * curve * fade;

        targetPositionOffset += Vector3.down * jumpDipAmount * strength;
        targetRotationOffset.x += jumpPitchAmount * strength;
    }

    private void ApplyLandingAnimation()
    {
        if (!enableLandingAnimation || landingTimer <= 0f)
        {
            return;
        }

        landingTimer -= Time.deltaTime;

        float t = 1f - Mathf.Clamp01(landingTimer / landingAnimationDuration);
        float dipCurve = Mathf.Sin(t * Mathf.PI);
        float pitchCurve = Mathf.Sin(t * Mathf.PI * 1.4f) * (1f - t);

        targetPositionOffset += Vector3.down * maxLandingDipAmount * landingStrength * dipCurve;
        targetRotationOffset.x += maxLandingPitchAmount * landingStrength * pitchCurve;
    }

    private void TryTriggerWallImpact(Collision collision)
    {
        if (!enableWallImpactAnimation || playerRigidbody == null || cameraTransform == null)
        {
            return;
        }

        if (wallImpactCooldownTimer > 0f)
        {
            return;
        }

        if (wallImpactOnlyWhileTethered && (pullGun == null || !pullGun.IsTethered))
        {
            return;
        }

        if (!TryGetWallNormal(collision, out Vector3 wallNormal))
        {
            return;
        }

        float impactSpeed = Vector3.Dot(playerRigidbody.linearVelocity, -wallNormal);

        if (impactSpeed < minimumWallImpactSpeed)
        {
            return;
        }

        wallImpactTimer = wallImpactAnimationDuration;
        wallImpactCooldownTimer = wallImpactCooldown;
        wallImpactStrength = Mathf.InverseLerp(minimumWallImpactSpeed, hardWallImpactSpeed, impactSpeed);
        wallImpactLocalDirection = cameraTransform.InverseTransformDirection(wallNormal.normalized);
    }

    private bool TryGetWallNormal(Collision collision, out Vector3 wallNormal)
    {
        wallNormal = Vector3.zero;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint contact = collision.GetContact(i);

            if (Mathf.Abs(contact.normal.y) <= maxWallNormalY)
            {
                wallNormal = contact.normal;
                return true;
            }
        }

        return false;
    }

    private void ApplyWallImpactAnimation()
    {
        if (!enableWallImpactAnimation || wallImpactTimer <= 0f)
        {
            return;
        }

        wallImpactTimer -= Time.deltaTime;

        float t = 1f - Mathf.Clamp01(wallImpactTimer / wallImpactAnimationDuration);
        float kickCurve = Mathf.Sin(t * Mathf.PI);
        float rotateCurve = Mathf.Sin(t * Mathf.PI * 1.25f) * (1f - t * 0.25f);

        Vector3 localKick =
            wallImpactLocalDirection.normalized *
            maxWallImpactKickAmount *
            wallImpactStrength *
            kickCurve;

        float pitch =
            -Mathf.Abs(wallImpactLocalDirection.z) *
            maxWallImpactPitchAmount *
            wallImpactStrength *
            rotateCurve;

        float roll =
            -wallImpactLocalDirection.x *
            maxWallImpactRollAmount *
            wallImpactStrength *
            rotateCurve;

        targetPositionOffset += localKick;
        targetRotationOffset.x += pitch;
        targetRotationOffset.z += roll;
    }

    private void ApplySwingAnimation()
    {
        if (!enableSwingAnimation || playerRigidbody == null || pullGun == null || cameraTransform == null)
        {
            return;
        }

        bool shouldSwingAnimate =
            swingOnlyOnHeavyTethers
                ? pullGun.IsTetheredToHeavyTarget
                : pullGun.IsTethered;

        if (disableSwingAnimationWhileGrounded && controller != null && controller.IsGrounded)
        {
            shouldSwingAnimate = false;
        }

        if (!shouldSwingAnimate)
        {
            smoothedSwingSide = SmoothFloat(smoothedSwingSide, 0f, swingFollowSpeed);
            smoothedSwingSpeed = SmoothFloat(smoothedSwingSpeed, 0f, swingFollowSpeed);
            return;
        }

        Vector3 velocity = playerRigidbody.linearVelocity;
        Vector3 localVelocity = cameraTransform.InverseTransformDirection(velocity);

        float targetSide = Mathf.Clamp(localVelocity.x / swingSpeedForMaxAnimation, -1f, 1f);
        float targetSpeed = Mathf.Clamp01(velocity.magnitude / swingSpeedForMaxAnimation);

        smoothedSwingSide = SmoothFloat(smoothedSwingSide, targetSide, swingFollowSpeed);
        smoothedSwingSpeed = SmoothFloat(smoothedSwingSpeed, targetSpeed, swingFollowSpeed);

        float wave = Mathf.Sin(Time.time * swingWaveSpeed) * swingWaveAmount * smoothedSwingSpeed;

        targetRotationOffset.z += -smoothedSwingSide * maxSwingRoll;
        targetPositionOffset += Vector3.right * smoothedSwingSide * maxSwingSideOffset;
        targetPositionOffset += Vector3.back * smoothedSwingSpeed * maxSwingBackOffset;
        targetPositionOffset += Vector3.up * wave;
    }

    private float SmoothFloat(float current, float target, float speed)
    {
        return Mathf.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
    }

    private Vector3 SmoothVector(Vector3 current, Vector3 target, float speed)
    {
        return Vector3.Lerp(current, target, 1f - Mathf.Exp(-speed * Time.deltaTime));
    }

    private void OnGUI()
    {
        if (!showDebugValues)
        {
            return;
        }

        string text =
            $"Camera Animator\n" +
            $"Grounded: {(controller != null && controller.IsGrounded)}\n" +
            $"Run Blend: {(controller != null ? controller.RunBlend : 0f):F2}\n" +
            $"Bob Weight: {movementBobWeight:F2}\n" +
            $"Jump: {jumpTimer:F2}\n" +
            $"Landing: {landingTimer:F2}\n" +
            $"Wall Impact: {wallImpactTimer:F2}\n" +
            $"Swing Speed: {smoothedSwingSpeed:F2}\n" +
            $"Rotation Offset: {currentRotationOffset}";

        GUI.Box(new Rect(20f, 270f, 330f, 235f), text);
    }
}