using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PullGun : MonoBehaviour
{
    private enum PullMode
    {
        None,
        Extending,
        PullObjectToPlayer,
        StaticAnchor,
        DynamicHeavyAnchor
    }

    [Header("References")]
    [Tooltip("The player camera used for aiming. If empty, the script searches for a child Camera.")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("Point in front of the camera that light objects are pulled toward. If empty, one is generated.")]
    [SerializeField] private Transform holdPoint;

    [Header("Layers")]
    [Tooltip("Everything the tether ray can hit. Include Pullable, NotPullable, walls, floor, ceiling, etc.")]
    [SerializeField] private LayerMask aimBlockingLayers = ~0;

    [Tooltip("Only these layers can be attached to by the tether.")]
    [SerializeField] private LayerMask pullableLayers = ~0;

    [Header("Tether Extension")]
    [Tooltip("If true, the tether shoots outward over time. If false, it attaches instantly.")]
    [SerializeField] private bool useExtendingTether = true;

    [Tooltip("Maximum distance the tether can shoot.")]
    [SerializeField] private float maxTetherLength = 40f;

    [Tooltip("How fast the tether extends while holding left mouse.")]
    [SerializeField] private float tetherExtendSpeed = 55f;

    [Tooltip("Starting visible length when the tether begins firing.")]
    [SerializeField] private float initialTetherLength = 0.5f;

    [Tooltip("If true, the tether follows your aim while extending. If false, it locks to the fired direction.")]
    [SerializeField] private bool tetherFollowsAimWhileExtending = true;

    [Tooltip("If true, non-pullable objects stop the tether.")]
    [SerializeField] private bool nonPullableObjectsBlockTether = true;

    [Header("Rope Length")]
    [Tooltip("Minimum possible rope length after attaching.")]
    [SerializeField] private float minRopeLength = 1.5f;

    [Tooltip("Default distance in front of the camera where light objects hover when not tethered.")]
    [SerializeField] private float idleHoldPointDistance = 4f;

    [Tooltip("If true, the rope length is automatically shortened after attaching to create a better swing arc.")]
    [SerializeField] private bool autoAdjustRopeForSwing = true;

    [Tooltip("The desired swing rope length as a percentage of the hit distance. Lower values create a tighter/faster swing.")]
    [Range(0.35f, 1f)]
    [SerializeField] private float swingRopeLengthMultiplier = 0.72f;

    [Tooltip("Extra meters removed from the rope after applying the multiplier. Higher values create a stronger pull into the swing.")]
    [SerializeField] private float swingRopeShortenAmount = 1.5f;

    [Tooltip("How fast the rope tightens from the hit distance to the chosen swing length.")]
    [SerializeField] private float ropeLengthAdjustSpeed = 18f;

    [Tooltip("If true, light objects also use the auto swing rope length. Usually false feels better for carried objects.")]
    [SerializeField] private bool autoAdjustLightObjectRope = false;

    [Tooltip("If true, the player can never move farther than the current rope length.")]
    [SerializeField] private bool useNoStretchConstraint = true;

    [Tooltip("Small tolerance before the no-stretch correction happens.")]
    [SerializeField] private float constraintTolerance = 0.01f;

    [Tooltip("If true, only outward velocity is removed at the rope limit. This preserves swing momentum.")]
    [SerializeField] private bool removeOnlyOutwardVelocity = true;

    [Header("Smooth Rope Catch")]
    [Tooltip("Starts slowing outward movement this far before the rope reaches full length. Prevents a sudden hard stop.")]
    [SerializeField] private float catchDampingDistance = 1.25f;

    [Tooltip("How strongly outward velocity is reduced while approaching the rope limit.")]
    [SerializeField] private float catchDampingStrength = 7f;

    [Header("Player / Mass")]
    [Tooltip("Mass value used when comparing the player against objects. Objects below this are treated as light.")]
    [SerializeField] private float playerMass = 80f;

    [Header("Rope Pull Assist")]
    [Tooltip("Pull acceleration toward the anchor while the rope is tightening to the chosen swing length.")]
    [SerializeField] private float tighteningPullAcceleration = 38f;

    [Tooltip("Pull acceleration toward the anchor once the rope is near full length.")]
    [SerializeField] private float ropeTensionAcceleration = 12f;

    [Tooltip("How close to full rope length the player must be before tension assist activates.")]
    [SerializeField] private float tensionAssistActivationDistance = 0.65f;

    [Tooltip("Maximum upward acceleration from rope pull assist.")]
    [SerializeField] private float maxUpwardTetherAcceleration = 18f;

    [Tooltip("Maximum total acceleration from rope pull assist.")]
    [SerializeField] private float maxPlayerTetherAcceleration = 85f;

    [Tooltip("Maximum player velocity while tethered.")]
    [SerializeField] private float maxPlayerTetherVelocity = 32f;

    [Header("Light Object Pull")]
    [Tooltip("Spring force used to pull light objects toward the HoldPoint.")]
    [SerializeField] private float holdPointSpring = 180f;

    [Tooltip("Damping used to reduce wobble on light objects.")]
    [SerializeField] private float lightObjectDamping = 18f;

    [Tooltip("How much camera motion influences light objects while held.")]
    [SerializeField] private float objectSwingInfluence = 18f;

    [Tooltip("Maximum velocity for light objects while pulled.")]
    [SerializeField] private float maxObjectVelocity = 30f;

    [Header("Dynamic Heavy Anchor")]
    [Tooltip("How much force is transferred back into a heavy dynamic object.")]
    [SerializeField] private float heavyObjectReactionMultiplier = 0.35f;

    [Tooltip("How much player swing force is transferred into a heavy object.")]
    [SerializeField] private float heavyObjectSwingTransfer = 0.35f;

    [Tooltip("Extra force to help loosen/move heavy objects while swinging.")]
    [SerializeField] private float heavyObjectLoosenForce = 10f;

    [Tooltip("Extra torque applied to heavy objects when pulled off-center.")]
    [SerializeField] private float heavyObjectTorqueMultiplier = 2f;

    [Tooltip("Maximum velocity for heavy objects while tethered.")]
    [SerializeField] private float maxHeavyObjectVelocity = 10f;

    [Tooltip("Controls how strongly mass affects force transfer to heavy objects.")]
    [SerializeField] private float massAnchorPower = 1.4f;

    [Tooltip("Minimum reaction share that can affect a heavy object.")]
    [Range(0f, 1f)]
    [SerializeField] private float minHeavyObjectReactionShare = 0.05f;

    [Header("Swing Control")]
    [Tooltip("Player control influence while grounded and tethered.")]
    [SerializeField] private float groundedPlayerSwingInfluence = 16f;

    [Tooltip("Airborne swing pumping force.")]
    [SerializeField] private float airborneSwingPumpForce = 24f;

    [Tooltip("Small direct airborne steering while swinging.")]
    [Range(0f, 1f)]
    [SerializeField] private float airborneDirectControl = 0.04f;

    [Tooltip("Minimum tangential speed before timing-based swing pumping is used.")]
    [SerializeField] private float minSwingSpeedForPumpTiming = 1.25f;

    [Tooltip("Brake applied when airborne input goes against swing direction.")]
    [SerializeField] private float wrongDirectionAirBrake = 2f;

    [Header("Release")]
    [Tooltip("Velocity multiplier applied to light objects when released.")]
    [SerializeField] private float lightObjectReleaseVelocityMultiplier = 1.15f;

    [Tooltip("Forward impulse applied to light objects when released.")]
    [SerializeField] private float lightObjectThrowImpulse = 1.5f;

    [Header("Debug Visuals")]
    [Tooltip("Master toggle for all PullGun debug visuals.")]
    [SerializeField] private bool debugEnabled = true;

    [Tooltip("Shows debug lines and markers in Game view using LineRenderers.")]
    [SerializeField] private bool drawGameViewDebug = true;

    [Tooltip("Shows debug gizmos in Scene view.")]
    [SerializeField] private bool drawSceneGizmos = true;

    [Tooltip("Shows a debug panel in Game view.")]
    [SerializeField] private bool showDebugPanel = true;

    [Tooltip("Shows the aim line.")]
    [SerializeField] private bool showAimLine = true;

    [Tooltip("Shows the active tether line.")]
    [SerializeField] private bool showTetherLine = true;

    [Tooltip("Shows the chosen rope length guide.")]
    [SerializeField] private bool showRopeLengthGuide = true;

    [Tooltip("Shows force direction line.")]
    [SerializeField] private bool showForceLine = true;

    [Tooltip("Size of debug marker spheres.")]
    [SerializeField] private float debugMarkerSize = 0.18f;

    [Tooltip("Width of debug lines.")]
    [SerializeField] private float debugLineWidth = 0.04f;

    [Header("Debug Colors")]
    [SerializeField] private Color aimColor = Color.white;
    [SerializeField] private Color extendingColor = Color.cyan;
    [SerializeField] private Color lightObjectColor = Color.green;
    [SerializeField] private Color staticAnchorColor = Color.yellow;
    [SerializeField] private Color heavyAnchorColor = new Color(1f, 0.45f, 0f);
    [SerializeField] private Color blockedColor = Color.gray;
    [SerializeField] private Color ropeLengthColor = Color.magenta;
    [SerializeField] private Color forceColor = Color.red;

    private Rigidbody playerRb;
    private CoopFirstPersonController controller;

    private PullMode currentMode = PullMode.None;

    private Rigidbody targetRb;
    private Collider targetCollider;

    private Transform staticAnchorTransform;
    private Vector3 staticAnchorPoint;
    private Vector3 localStaticAnchorPoint;
    private Vector3 localGrabPoint;

    private float currentRopeLength;
    private float targetRopeLength;
    private float attachRopeLength;

    private float currentExtendingLength;
    private Vector3 extendingStartPoint;
    private Vector3 extendingDirection;
    private Vector3 extendingEndPoint;

    private Vector3 previousHoldPointPosition;
    private Quaternion previousCameraRotation;

    private RaycastHit latestAimHit;
    private bool hasLatestAimHit;

    private Vector3 lastPlayerTetherAcceleration;
    private Vector3 lastPlayerTetherForce;
    private Vector3 lastObjectTetherForce;
    private Vector3 lastSwingForce;
    private float lastLengthError;
    private float lastCorrectedAmount;
    private float lastOutwardSpeedRemoved;

    private LineRenderer aimLineRenderer;
    private LineRenderer tetherLineRenderer;
    private LineRenderer ropeLengthLineRenderer;
    private LineRenderer forceLineRenderer;

    private GameObject aimMarker;
    private GameObject grabMarker;
    private GameObject holdMarker;
    private GameObject playerMarker;

    private Material debugMaterial;

    public bool IsTethered =>
        currentMode == PullMode.PullObjectToPlayer ||
        currentMode == PullMode.StaticAnchor ||
        currentMode == PullMode.DynamicHeavyAnchor;

    public bool IsExtendingTether => currentMode == PullMode.Extending;

    public bool IsTetherVisuallyActive => currentMode != PullMode.None;

    public bool IsTetheredToHeavyTarget =>
        currentMode == PullMode.StaticAnchor ||
        currentMode == PullMode.DynamicHeavyAnchor;

    public float CurrentRopeLength => currentRopeLength;
    public float TargetRopeLength => targetRopeLength;
    public float AttachRopeLength => attachRopeLength;

    private void Awake()
    {
        playerRb = GetComponent<Rigidbody>();
        controller = GetComponent<CoopFirstPersonController>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        currentRopeLength = Mathf.Clamp(idleHoldPointDistance, minRopeLength, maxTetherLength);
        targetRopeLength = currentRopeLength;
        attachRopeLength = currentRopeLength;

        if (holdPoint == null && playerCamera != null)
        {
            GameObject generatedHoldPoint = new GameObject("Generated Hold Point");
            generatedHoldPoint.transform.SetParent(playerCamera.transform);
            generatedHoldPoint.transform.localPosition = Vector3.forward * idleHoldPointDistance;
            generatedHoldPoint.transform.localRotation = Quaternion.identity;
            holdPoint = generatedHoldPoint.transform;
        }

        CreateDebugVisualsIfNeeded();
    }

    private void Start()
    {
        ResetPreviousFrameData();
    }

    private void Update()
    {
        HandleInput();
        UpdateRopeLengthAdjustment();
        UpdateHoldPointPosition();
        UpdateAimDebug();
        UpdateDebugVisuals();
    }

    private void FixedUpdate()
    {
        ResetFrameDebugValues();

        switch (currentMode)
        {
            case PullMode.PullObjectToPlayer:
                SimulateLightObjectPull();
                break;

            case PullMode.StaticAnchor:
                SimulateStaticAnchorPull();
                break;

            case PullMode.DynamicHeavyAnchor:
                SimulateDynamicHeavyPull();
                break;
        }

        StorePreviousFrameData();
    }

    private void HandleInput()
    {
        if (Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (useExtendingTether)
            {
                StartExtendingTether();
            }
            else
            {
                TryInstantAttach();
            }
        }

        if (currentMode == PullMode.Extending && Mouse.current.leftButton.isPressed)
        {
            UpdateExtendingTether();
        }

        if (Mouse.current.leftButton.wasReleasedThisFrame || Mouse.current.rightButton.wasPressedThisFrame)
        {
            ReleasePull();
        }
    }

    private void StartExtendingTether()
    {
        if (playerCamera == null)
        {
            return;
        }

        ClearTarget();

        currentMode = PullMode.Extending;
        currentExtendingLength = Mathf.Max(0.01f, initialTetherLength);

        extendingStartPoint = playerCamera.transform.position;
        extendingDirection = playerCamera.transform.forward;
        extendingEndPoint = extendingStartPoint + extendingDirection * currentExtendingLength;
    }

    private void UpdateExtendingTether()
    {
        if (playerCamera == null)
        {
            ReleasePull();
            return;
        }

        currentExtendingLength += tetherExtendSpeed * Time.deltaTime;
        currentExtendingLength = Mathf.Clamp(currentExtendingLength, initialTetherLength, maxTetherLength);

        if (tetherFollowsAimWhileExtending)
        {
            extendingStartPoint = playerCamera.transform.position;
            extendingDirection = playerCamera.transform.forward;
        }

        Ray ray = new Ray(extendingStartPoint, extendingDirection);

        if (Physics.Raycast(ray, out RaycastHit hit, currentExtendingLength, aimBlockingLayers, QueryTriggerInteraction.Ignore))
        {
            extendingEndPoint = hit.point;

            if (IsHitPullableByLayer(hit))
            {
                AttachToHit(hit);
                return;
            }

            if (nonPullableObjectsBlockTether)
            {
                return;
            }
        }

        extendingEndPoint = extendingStartPoint + extendingDirection * currentExtendingLength;
    }

    private void TryInstantAttach()
    {
        if (playerCamera == null)
        {
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (!Physics.Raycast(ray, out RaycastHit hit, maxTetherLength, aimBlockingLayers, QueryTriggerInteraction.Ignore))
        {
            return;
        }

        if (!IsHitPullableByLayer(hit))
        {
            Debug.Log(
                $"Pull gun blocked by non-pullable object: {hit.collider.name}, layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)}",
                hit.collider
            );

            return;
        }

        AttachToHit(hit);
    }

    private void AttachToHit(RaycastHit hit)
    {
        targetCollider = hit.collider;
        targetRb = hit.rigidbody;

        float hitDistance = Vector3.Distance(GetPlayerTetherPoint(), hit.point);

        attachRopeLength = Mathf.Clamp(hitDistance, minRopeLength, maxTetherLength);
        currentRopeLength = attachRopeLength;

        bool staticOrKinematic = targetRb == null || targetRb.isKinematic;
        bool lightObject = !staticOrKinematic && targetRb.mass < playerMass;

        if (autoAdjustRopeForSwing && (!lightObject || autoAdjustLightObjectRope))
        {
            targetRopeLength = CalculateAutoSwingRopeLength(attachRopeLength);
        }
        else
        {
            targetRopeLength = attachRopeLength;
        }

        if (staticOrKinematic)
        {
            StartStaticAnchor(hit);
        }
        else if (lightObject)
        {
            StartLightObject(hit);
        }
        else
        {
            StartDynamicHeavyAnchor(hit);
        }

        UpdateHoldPointPosition();
        ResetPreviousFrameData();
    }

    private float CalculateAutoSwingRopeLength(float hitDistance)
    {
        float desiredLength = hitDistance * swingRopeLengthMultiplier - swingRopeShortenAmount;
        return Mathf.Clamp(desiredLength, minRopeLength, hitDistance);
    }

    private void UpdateRopeLengthAdjustment()
    {
        if (!IsTethered)
        {
            return;
        }

        if (currentRopeLength <= targetRopeLength)
        {
            currentRopeLength = targetRopeLength;
            return;
        }

        currentRopeLength = Mathf.MoveTowards(
            currentRopeLength,
            targetRopeLength,
            ropeLengthAdjustSpeed * Time.deltaTime
        );
    }

    private void StartLightObject(RaycastHit hit)
    {
        currentMode = PullMode.PullObjectToPlayer;

        targetRb = hit.rigidbody;
        targetCollider = hit.collider;
        staticAnchorTransform = null;
        localGrabPoint = targetRb.transform.InverseTransformPoint(hit.point);

        PrepareTargetRigidbody(targetRb);
    }

    private void StartStaticAnchor(RaycastHit hit)
    {
        currentMode = PullMode.StaticAnchor;

        targetRb = hit.rigidbody;
        targetCollider = hit.collider;

        staticAnchorTransform = targetRb != null ? targetRb.transform : hit.collider.transform;
        staticAnchorPoint = hit.point;

        localStaticAnchorPoint = staticAnchorTransform != null
            ? staticAnchorTransform.InverseTransformPoint(hit.point)
            : hit.point;
    }

    private void StartDynamicHeavyAnchor(RaycastHit hit)
    {
        currentMode = PullMode.DynamicHeavyAnchor;

        targetRb = hit.rigidbody;
        targetCollider = hit.collider;
        staticAnchorTransform = null;
        localGrabPoint = targetRb.transform.InverseTransformPoint(hit.point);

        PrepareTargetRigidbody(targetRb);
        targetRb.WakeUp();
    }

    private void PrepareTargetRigidbody(Rigidbody body)
    {
        if (body == null)
        {
            return;
        }

        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode =
            body.mass > 200f
                ? CollisionDetectionMode.ContinuousSpeculative
                : CollisionDetectionMode.ContinuousDynamic;
    }

    private void UpdateHoldPointPosition()
    {
        if (holdPoint == null || playerCamera == null)
        {
            return;
        }

        float holdDistance = IsTethered ? currentRopeLength : idleHoldPointDistance;

        holdPoint.position = playerCamera.transform.position + playerCamera.transform.forward * holdDistance;
        holdPoint.rotation = playerCamera.transform.rotation;
    }

    private void SimulateLightObjectPull()
    {
        if (targetRb == null || holdPoint == null)
        {
            ReleasePull();
            return;
        }

        Vector3 grabPoint = targetRb.transform.TransformPoint(localGrabPoint);
        Vector3 toHoldPoint = holdPoint.position - grabPoint;
        float distance = toHoldPoint.magnitude;

        if (distance <= 0.001f)
        {
            return;
        }

        Vector3 direction = toHoldPoint / distance;
        Vector3 pointVelocity = targetRb.GetPointVelocity(grabPoint);
        Vector3 holdVelocity = GetHoldPointVelocity();
        Vector3 relativeVelocity = holdVelocity - pointVelocity;

        Vector3 springForce = direction * distance * holdPointSpring;
        Vector3 dampingForce = Vector3.Project(relativeVelocity, direction) * lightObjectDamping;
        Vector3 swingForce = GetCameraSwingForce() * objectSwingInfluence;

        targetRb.AddForceAtPosition(springForce + dampingForce + swingForce, grabPoint, ForceMode.Force);

        LimitRigidbodyVelocity(targetRb, maxObjectVelocity);
    }

    private void SimulateStaticAnchorPull()
    {
        Vector3 anchorPoint = GetCurrentStaticAnchorPoint();

        ApplySmoothCatchDamping(anchorPoint);
        ApplyOneWayNoStretchConstraint(anchorPoint);

        Vector3 tensionForce = ApplyPlayerPullAssist(anchorPoint);
        lastPlayerTetherForce = tensionForce;

        Vector3 swingForce = GetMomentumBasedPlayerSwingForce(anchorPoint);
        playerRb.AddForce(swingForce, ForceMode.Acceleration);
        lastSwingForce = swingForce;

        LimitRigidbodyVelocity(playerRb, maxPlayerTetherVelocity);
    }

    private void SimulateDynamicHeavyPull()
    {
        if (targetRb == null)
        {
            ReleasePull();
            return;
        }

        Vector3 grabPoint = targetRb.transform.TransformPoint(localGrabPoint);

        ApplySmoothCatchDamping(grabPoint);
        ApplyOneWayNoStretchConstraint(grabPoint);

        Vector3 tensionForce = ApplyPlayerPullAssist(grabPoint);
        Vector3 objectReactionForce = GetDynamicObjectReactionForce(-tensionForce);

        targetRb.AddForceAtPosition(objectReactionForce, grabPoint, ForceMode.Force);
        ApplyExtraHeavyObjectTorque(grabPoint, objectReactionForce);

        Vector3 swingForce = GetMomentumBasedPlayerSwingForce(grabPoint);
        playerRb.AddForce(swingForce, ForceMode.Acceleration);

        ApplySwingTransferToHeavyObject(grabPoint, swingForce);
        ApplyHeavyObjectLoosenForce(grabPoint);

        lastPlayerTetherForce = tensionForce;
        lastObjectTetherForce += objectReactionForce;
        lastSwingForce = swingForce;

        LimitRigidbodyVelocity(playerRb, maxPlayerTetherVelocity);
        LimitRigidbodyVelocity(targetRb, maxHeavyObjectVelocity);
    }

    private void ApplySmoothCatchDamping(Vector3 anchorPoint)
    {
        if (catchDampingDistance <= 0f || catchDampingStrength <= 0f)
        {
            return;
        }

        Vector3 playerPoint = GetPlayerTetherPoint();
        Vector3 anchorToPlayer = playerPoint - anchorPoint;
        float distance = anchorToPlayer.magnitude;

        if (distance <= 0.001f)
        {
            return;
        }

        float catchStartDistance = currentRopeLength - catchDampingDistance;

        if (distance < catchStartDistance)
        {
            return;
        }

        Vector3 anchorToPlayerDirection = anchorToPlayer / distance;
        Vector3 velocity = playerRb.linearVelocity;

        float outwardSpeed = Vector3.Dot(velocity, anchorToPlayerDirection);

        if (outwardSpeed <= 0f)
        {
            return;
        }

        float catch01 = Mathf.InverseLerp(catchStartDistance, currentRopeLength, distance);
        float dampingFactor = 1f - Mathf.Exp(-catchDampingStrength * catch01 * Time.fixedDeltaTime);

        Vector3 velocityChange = -anchorToPlayerDirection * outwardSpeed * dampingFactor;

        playerRb.AddForce(velocityChange, ForceMode.VelocityChange);

        lastOutwardSpeedRemoved += outwardSpeed * dampingFactor;
    }

    private void ApplyOneWayNoStretchConstraint(Vector3 anchorPoint)
    {
        if (!useNoStretchConstraint)
        {
            return;
        }

        Vector3 playerPoint = GetPlayerTetherPoint();
        Vector3 anchorToPlayer = playerPoint - anchorPoint;
        float distance = anchorToPlayer.magnitude;

        if (distance <= 0.001f)
        {
            return;
        }

        float lengthError = distance - currentRopeLength;
        lastLengthError = lengthError;

        if (lengthError <= constraintTolerance)
        {
            return;
        }

        Vector3 anchorToPlayerDirection = anchorToPlayer / distance;
        Vector3 desiredPlayerPoint = anchorPoint + anchorToPlayerDirection * currentRopeLength;
        Vector3 correction = desiredPlayerPoint - playerPoint;

        lastCorrectedAmount = correction.magnitude;

        playerRb.position += correction;

        if (removeOnlyOutwardVelocity)
        {
            RemoveOutwardVelocity(anchorToPlayerDirection);
        }

        Physics.SyncTransforms();
    }

    private void RemoveOutwardVelocity(Vector3 anchorToPlayerDirection)
    {
        Vector3 velocity = playerRb.linearVelocity;
        float outwardSpeed = Vector3.Dot(velocity, anchorToPlayerDirection);

        if (outwardSpeed > 0f)
        {
            playerRb.linearVelocity = velocity - anchorToPlayerDirection * outwardSpeed;
            lastOutwardSpeedRemoved += outwardSpeed;
        }
    }

    private Vector3 ApplyPlayerPullAssist(Vector3 anchorPoint)
    {
        Vector3 playerPoint = GetPlayerTetherPoint();
        Vector3 playerToAnchor = anchorPoint - playerPoint;
        float distance = playerToAnchor.magnitude;

        if (distance <= 0.001f)
        {
            return Vector3.zero;
        }

        bool ropeStillTightening = currentRopeLength > targetRopeLength + 0.02f;
        float distanceFromLimit = currentRopeLength - distance;

        if (!ropeStillTightening && distanceFromLimit > tensionAssistActivationDistance)
        {
            return Vector3.zero;
        }

        float assistStrength = ropeStillTightening
            ? tighteningPullAcceleration
            : ropeTensionAcceleration;

        if (assistStrength <= 0f)
        {
            return Vector3.zero;
        }

        float assist01;

        if (ropeStillTightening)
        {
            assist01 = Mathf.Clamp01((currentRopeLength - targetRopeLength) / Mathf.Max(0.001f, attachRopeLength - targetRopeLength));
        }
        else
        {
            assist01 = 1f - Mathf.Clamp01(distanceFromLimit / Mathf.Max(0.001f, tensionAssistActivationDistance));
        }

        Vector3 direction = playerToAnchor / distance;
        Vector3 acceleration = direction * assistStrength * assist01;
        acceleration = ClampTetherAcceleration(acceleration);

        playerRb.AddForce(acceleration, ForceMode.Acceleration);

        lastPlayerTetherAcceleration = acceleration;

        return acceleration * Mathf.Max(Mathf.Max(playerRb.mass, playerMass), 0.001f);
    }

    private Vector3 ClampTetherAcceleration(Vector3 acceleration)
    {
        Vector3 horizontal = new Vector3(acceleration.x, 0f, acceleration.z);

        if (horizontal.magnitude > maxPlayerTetherAcceleration)
        {
            horizontal = horizontal.normalized * maxPlayerTetherAcceleration;
        }

        float vertical = Mathf.Clamp(
            acceleration.y,
            -maxPlayerTetherAcceleration,
            maxUpwardTetherAcceleration
        );

        Vector3 clamped = horizontal + Vector3.up * vertical;

        if (clamped.magnitude > maxPlayerTetherAcceleration)
        {
            clamped = clamped.normalized * maxPlayerTetherAcceleration;
        }

        return clamped;
    }

    private Vector3 GetDynamicObjectReactionForce(Vector3 rawReactionForce)
    {
        if (targetRb == null)
        {
            return Vector3.zero;
        }

        float objectMass = Mathf.Max(targetRb.mass, 0.001f);
        float playerMassValue = Mathf.Max(Mathf.Max(playerRb.mass, playerMass), 0.001f);

        float massRatio = playerMassValue / objectMass;
        float reactionShare = Mathf.Pow(massRatio, massAnchorPower);
        reactionShare = Mathf.Clamp(reactionShare, minHeavyObjectReactionShare, 1f);

        return rawReactionForce * reactionShare * heavyObjectReactionMultiplier;
    }

    private void ApplySwingTransferToHeavyObject(Vector3 grabPoint, Vector3 playerSwingForce)
    {
        if (targetRb == null || playerSwingForce.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Vector3 transferForce =
            -playerSwingForce *
            Mathf.Max(Mathf.Max(playerRb.mass, playerMass), 0.001f) *
            heavyObjectSwingTransfer *
            GetHeavyObjectReactionShare();

        targetRb.AddForceAtPosition(transferForce, grabPoint, ForceMode.Force);
        ApplyExtraHeavyObjectTorque(grabPoint, transferForce);

        lastObjectTetherForce += transferForce;
    }

    private void ApplyHeavyObjectLoosenForce(Vector3 grabPoint)
    {
        if (targetRb == null)
        {
            return;
        }

        Vector3 horizontalVelocity = new Vector3(
            playerRb.linearVelocity.x,
            0f,
            playerRb.linearVelocity.z
        );

        float swingSpeed = horizontalVelocity.magnitude;

        if (swingSpeed < 1f)
        {
            return;
        }

        Vector3 loosenForce =
            horizontalVelocity.normalized *
            heavyObjectLoosenForce *
            Mathf.Max(Mathf.Max(playerRb.mass, playerMass), 0.001f) *
            GetHeavyObjectReactionShare();

        targetRb.AddForceAtPosition(loosenForce, grabPoint, ForceMode.Force);
        ApplyExtraHeavyObjectTorque(grabPoint, loosenForce);

        lastObjectTetherForce += loosenForce;
    }

    private float GetHeavyObjectReactionShare()
    {
        if (targetRb == null)
        {
            return 0f;
        }

        float objectMass = Mathf.Max(targetRb.mass, 0.001f);
        float playerMassValue = Mathf.Max(Mathf.Max(playerRb.mass, playerMass), 0.001f);

        float massRatio = playerMassValue / objectMass;
        float share = Mathf.Pow(massRatio, massAnchorPower);

        return Mathf.Clamp(share, minHeavyObjectReactionShare, 1f);
    }

    private void ApplyExtraHeavyObjectTorque(Vector3 grabPoint, Vector3 appliedForce)
    {
        if (targetRb == null)
        {
            return;
        }

        Vector3 leverArm = grabPoint - targetRb.worldCenterOfMass;
        Vector3 torque = Vector3.Cross(leverArm, appliedForce) * heavyObjectTorqueMultiplier;

        targetRb.AddTorque(torque, ForceMode.Force);
    }

    private Vector3 GetMomentumBasedPlayerSwingForce(Vector3 anchorPoint)
    {
        Vector3 playerPoint = GetPlayerTetherPoint();
        Vector3 toPlayer = playerPoint - anchorPoint;

        if (toPlayer.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        Vector3 tetherDirection = toPlayer.normalized;
        Vector3 inputDirection = GetWorldInputDirection();

        Vector3 tangentialInput = Vector3.ProjectOnPlane(inputDirection, tetherDirection);

        if (tangentialInput.sqrMagnitude < 0.001f)
        {
            return Vector3.zero;
        }

        tangentialInput.Normalize();

        bool grounded = controller != null && controller.IsGrounded;

        if (grounded)
        {
            return tangentialInput * groundedPlayerSwingInfluence;
        }

        Vector3 tangentialVelocity = Vector3.ProjectOnPlane(playerRb.linearVelocity, tetherDirection);
        float tangentialSpeed = tangentialVelocity.magnitude;

        Vector3 weakDirectControl = tangentialInput * airborneSwingPumpForce * airborneDirectControl;

        if (tangentialSpeed < minSwingSpeedForPumpTiming)
        {
            return weakDirectControl;
        }

        Vector3 swingDirection = tangentialVelocity.normalized;
        float alignment = Vector3.Dot(tangentialInput, swingDirection);

        if (alignment > 0f)
        {
            return weakDirectControl + tangentialInput * airborneSwingPumpForce * alignment;
        }

        return weakDirectControl - swingDirection * wrongDirectionAirBrake * Mathf.Abs(alignment);
    }

    private Vector3 GetWorldInputDirection()
    {
        Vector2 input = Vector2.zero;

        if (controller != null)
        {
            input = controller.MoveInput;
        }
        else if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
        }

        input = Vector2.ClampMagnitude(input, 1f);

        return Vector3.ClampMagnitude(
            transform.right * input.x + transform.forward * input.y,
            1f
        );
    }

    private Vector3 GetPlayerTetherPoint()
    {
        return playerCamera != null ? playerCamera.transform.position : playerRb.worldCenterOfMass;
    }

    private Vector3 GetCurrentStaticAnchorPoint()
    {
        if (staticAnchorTransform != null)
        {
            return staticAnchorTransform.TransformPoint(localStaticAnchorPoint);
        }

        return staticAnchorPoint;
    }

    private Vector3 GetCurrentGrabPoint()
    {
        switch (currentMode)
        {
            case PullMode.Extending:
                return extendingEndPoint;

            case PullMode.PullObjectToPlayer:
            case PullMode.DynamicHeavyAnchor:
                return targetRb != null
                    ? targetRb.transform.TransformPoint(localGrabPoint)
                    : GetPlayerTetherPoint();

            case PullMode.StaticAnchor:
                return GetCurrentStaticAnchorPoint();

            default:
                return playerCamera != null
                    ? playerCamera.transform.position + playerCamera.transform.forward * maxTetherLength
                    : transform.position;
        }
    }

    public Vector3 GetVisualTetherStartPoint()
    {
        return GetPlayerTetherPoint();
    }

    public Vector3 GetVisualTetherEndPoint()
    {
        return GetCurrentGrabPoint();
    }

    private Vector3 GetHoldPointVelocity()
    {
        if (Time.fixedDeltaTime <= 0f || holdPoint == null)
        {
            return Vector3.zero;
        }

        return (holdPoint.position - previousHoldPointPosition) / Time.fixedDeltaTime;
    }

    private Vector3 GetCameraSwingForce()
    {
        if (playerCamera == null)
        {
            return Vector3.zero;
        }

        Quaternion deltaRotation =
            playerCamera.transform.rotation *
            Quaternion.Inverse(previousCameraRotation);

        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f)
        {
            angle -= 360f;
        }

        if (axis == Vector3.zero || float.IsNaN(axis.x))
        {
            return Vector3.zero;
        }

        return Vector3.Cross(axis.normalized, playerCamera.transform.forward) * angle;
    }

    private void LimitRigidbodyVelocity(Rigidbody body, float maxVelocity)
    {
        if (body == null)
        {
            return;
        }

        if (body.linearVelocity.magnitude > maxVelocity)
        {
            body.linearVelocity = body.linearVelocity.normalized * maxVelocity;
        }
    }

    private bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private bool IsTransformOrParentInLayerMask(Transform startTransform, LayerMask mask)
    {
        Transform current = startTransform;

        while (current != null)
        {
            if (IsLayerInMask(current.gameObject.layer, mask))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private bool IsHitPullableByLayer(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        if (IsLayerInMask(hit.collider.gameObject.layer, pullableLayers))
        {
            return true;
        }

        Rigidbody attachedRigidbody = hit.collider.attachedRigidbody;

        if (attachedRigidbody != null)
        {
            if (IsLayerInMask(attachedRigidbody.gameObject.layer, pullableLayers))
            {
                return true;
            }

            if (IsTransformOrParentInLayerMask(attachedRigidbody.transform, pullableLayers))
            {
                return true;
            }
        }

        return IsTransformOrParentInLayerMask(hit.collider.transform, pullableLayers);
    }

    public void ReleasePull()
    {
        if (currentMode == PullMode.PullObjectToPlayer && targetRb != null && playerCamera != null)
        {
            targetRb.linearVelocity *= lightObjectReleaseVelocityMultiplier;
            targetRb.AddForce(playerCamera.transform.forward * lightObjectThrowImpulse, ForceMode.VelocityChange);
        }

        currentMode = PullMode.None;
        ClearTarget();

        currentExtendingLength = 0f;
        currentRopeLength = Mathf.Clamp(idleHoldPointDistance, minRopeLength, maxTetherLength);
        targetRopeLength = currentRopeLength;
        attachRopeLength = currentRopeLength;
    }

    private void ClearTarget()
    {
        targetRb = null;
        targetCollider = null;
        staticAnchorTransform = null;
        staticAnchorPoint = Vector3.zero;
        localStaticAnchorPoint = Vector3.zero;
        localGrabPoint = Vector3.zero;
    }

    private void ResetFrameDebugValues()
    {
        lastPlayerTetherAcceleration = Vector3.zero;
        lastPlayerTetherForce = Vector3.zero;
        lastObjectTetherForce = Vector3.zero;
        lastSwingForce = Vector3.zero;
        lastLengthError = 0f;
        lastCorrectedAmount = 0f;
        lastOutwardSpeedRemoved = 0f;
    }

    private void StorePreviousFrameData()
    {
        if (holdPoint != null)
        {
            previousHoldPointPosition = holdPoint.position;
        }

        if (playerCamera != null)
        {
            previousCameraRotation = playerCamera.transform.rotation;
        }
    }

    private void ResetPreviousFrameData()
    {
        StorePreviousFrameData();
    }

    private void UpdateAimDebug()
    {
        hasLatestAimHit = false;

        if (playerCamera == null || currentMode == PullMode.Extending)
        {
            return;
        }

        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        hasLatestAimHit = Physics.Raycast(
            ray,
            out latestAimHit,
            maxTetherLength,
            aimBlockingLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    private void CreateDebugVisualsIfNeeded()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (debugMaterial == null)
        {
            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            debugMaterial = new Material(shader);
        }

        if (aimLineRenderer == null) aimLineRenderer = CreateDebugLine("PullGun Aim Debug Line");
        if (tetherLineRenderer == null) tetherLineRenderer = CreateDebugLine("PullGun Tether Debug Line");
        if (ropeLengthLineRenderer == null) ropeLengthLineRenderer = CreateDebugLine("PullGun Rope Length Debug Line");
        if (forceLineRenderer == null) forceLineRenderer = CreateDebugLine("PullGun Force Debug Line");

        if (aimMarker == null) aimMarker = CreateDebugMarker("PullGun Aim Marker");
        if (grabMarker == null) grabMarker = CreateDebugMarker("PullGun Grab Marker");
        if (holdMarker == null) holdMarker = CreateDebugMarker("PullGun Hold Marker");
        if (playerMarker == null) playerMarker = CreateDebugMarker("PullGun Player Marker");
    }

    private LineRenderer CreateDebugLine(string objectName)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.startWidth = debugLineWidth;
        line.endWidth = debugLineWidth;
        line.material = debugMaterial;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.enabled = false;

        return line;
    }

    private GameObject CreateDebugMarker(string objectName)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = objectName;
        marker.transform.SetParent(transform);
        marker.transform.localScale = Vector3.one * debugMarkerSize;

        Collider markerCollider = marker.GetComponent<Collider>();

        if (markerCollider != null)
        {
            Destroy(markerCollider);
        }

        marker.SetActive(false);

        return marker;
    }

    private void UpdateDebugVisuals()
    {
        if (!debugEnabled || !drawGameViewDebug)
        {
            SetAllDebugRenderersEnabled(false);
            return;
        }

        CreateDebugVisualsIfNeeded();

        UpdateAimLineDebug();
        UpdateTetherLineDebug();
        UpdateRopeLengthLineDebug();
        UpdateForceLineDebug();
        UpdateDebugMarkers();
    }

    private void UpdateAimLineDebug()
    {
        if (aimLineRenderer == null)
        {
            return;
        }

        if (!showAimLine || playerCamera == null)
        {
            aimLineRenderer.enabled = false;
            return;
        }

        Vector3 start = GetPlayerTetherPoint();
        Vector3 end;

        if (currentMode == PullMode.Extending)
        {
            end = extendingEndPoint;
        }
        else if (hasLatestAimHit)
        {
            end = latestAimHit.point;
        }
        else
        {
            end = start + playerCamera.transform.forward * maxTetherLength;
        }

        SetLine(aimLineRenderer, start, end, GetCurrentDebugColor(), debugLineWidth);
    }

    private void UpdateTetherLineDebug()
    {
        if (tetherLineRenderer == null)
        {
            return;
        }

        if (!showTetherLine || currentMode == PullMode.None)
        {
            tetherLineRenderer.enabled = false;
            return;
        }

        Vector3 start =
            currentMode == PullMode.PullObjectToPlayer && holdPoint != null
                ? holdPoint.position
                : GetPlayerTetherPoint();

        Vector3 end = GetCurrentGrabPoint();

        SetLine(tetherLineRenderer, start, end, GetCurrentDebugColor(), debugLineWidth * 1.6f);
    }

    private void UpdateRopeLengthLineDebug()
    {
        if (ropeLengthLineRenderer == null)
        {
            return;
        }

        if (!showRopeLengthGuide || !IsTethered)
        {
            ropeLengthLineRenderer.enabled = false;
            return;
        }

        Vector3 start;
        Vector3 end;

        if (currentMode == PullMode.PullObjectToPlayer)
        {
            start = GetPlayerTetherPoint();
            end = holdPoint != null ? holdPoint.position : GetPlayerTetherPoint();
        }
        else
        {
            Vector3 anchorPoint = GetCurrentGrabPoint();
            Vector3 toPlayer = GetPlayerTetherPoint() - anchorPoint;

            if (toPlayer.sqrMagnitude <= 0.001f)
            {
                ropeLengthLineRenderer.enabled = false;
                return;
            }

            start = anchorPoint;
            end = anchorPoint + toPlayer.normalized * currentRopeLength;
        }

        SetLine(ropeLengthLineRenderer, start, end, ropeLengthColor, debugLineWidth * 0.75f);
    }

    private void UpdateForceLineDebug()
    {
        if (forceLineRenderer == null)
        {
            return;
        }

        if (!showForceLine || !IsTethered)
        {
            forceLineRenderer.enabled = false;
            return;
        }

        Vector3 start = GetPlayerTetherPoint();
        Vector3 direction = lastPlayerTetherAcceleration;

        if (direction.sqrMagnitude <= 0.001f)
        {
            forceLineRenderer.enabled = false;
            return;
        }

        Vector3 end = start + direction.normalized * 1.5f;

        SetLine(forceLineRenderer, start, end, forceColor, debugLineWidth);
    }

    private void UpdateDebugMarkers()
    {
        SetMarker(playerMarker, GetPlayerTetherPoint(), playerMarker != null, Color.blue);
        SetMarker(holdMarker, holdPoint != null ? holdPoint.position : Vector3.zero, holdPoint != null, Color.cyan);

        bool showGrab = currentMode != PullMode.None;
        SetMarker(grabMarker, GetCurrentGrabPoint(), showGrab, GetCurrentDebugColor());

        bool showAim = hasLatestAimHit && currentMode == PullMode.None;
        SetMarker(aimMarker, showAim ? latestAimHit.point : Vector3.zero, showAim, GetCurrentDebugColor());
    }

    private void SetLine(LineRenderer line, Vector3 start, Vector3 end, Color color, float width)
    {
        line.enabled = true;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void SetMarker(GameObject marker, Vector3 position, bool active, Color color)
    {
        if (marker == null)
        {
            return;
        }

        marker.SetActive(active);

        if (!active)
        {
            return;
        }

        marker.transform.position = position;
        marker.transform.localScale = Vector3.one * debugMarkerSize;

        Renderer markerRenderer = marker.GetComponent<Renderer>();

        if (markerRenderer != null)
        {
            markerRenderer.material.color = color;
        }
    }

    private void SetAllDebugRenderersEnabled(bool enabled)
    {
        if (aimLineRenderer != null) aimLineRenderer.enabled = enabled;
        if (tetherLineRenderer != null) tetherLineRenderer.enabled = enabled;
        if (ropeLengthLineRenderer != null) ropeLengthLineRenderer.enabled = enabled;
        if (forceLineRenderer != null) forceLineRenderer.enabled = enabled;

        if (aimMarker != null) aimMarker.SetActive(enabled);
        if (grabMarker != null) grabMarker.SetActive(enabled);
        if (holdMarker != null) holdMarker.SetActive(enabled);
        if (playerMarker != null) playerMarker.SetActive(enabled);
    }

    private Color GetCurrentDebugColor()
    {
        if (currentMode == PullMode.Extending)
        {
            return extendingColor;
        }

        if (currentMode == PullMode.PullObjectToPlayer)
        {
            return lightObjectColor;
        }

        if (currentMode == PullMode.StaticAnchor)
        {
            return staticAnchorColor;
        }

        if (currentMode == PullMode.DynamicHeavyAnchor)
        {
            return heavyAnchorColor;
        }

        if (hasLatestAimHit && !IsHitPullableByLayer(latestAimHit))
        {
            return blockedColor;
        }

        return aimColor;
    }

    private void OnGUI()
    {
        if (!debugEnabled || !showDebugPanel)
        {
            return;
        }

        string targetName = targetCollider != null ? targetCollider.name : "None";

        string text =
            $"Pull Gun Debug\n" +
            $"Mode: {currentMode}\n" +
            $"Target: {targetName}\n" +
            $"Attach Rope Length: {attachRopeLength:F2}\n" +
            $"Current Rope Length: {currentRopeLength:F2}\n" +
            $"Target Rope Length: {targetRopeLength:F2}\n" +
            $"Length Error: {lastLengthError:F3}\n" +
            $"Corrected Amount: {lastCorrectedAmount:F3}\n" +
            $"Outward Speed Removed: {lastOutwardSpeedRemoved:F2}\n" +
            $"Auto Swing Length: {autoAdjustRopeForSwing}\n" +
            $"Extending Length: {currentExtendingLength:F2}\n" +
            $"Player Accel: {lastPlayerTetherAcceleration.magnitude:F1}\n" +
            $"Player Force: {lastPlayerTetherForce.magnitude:F0}\n" +
            $"Object Force: {lastObjectTetherForce.magnitude:F0}\n" +
            $"Swing Force: {lastSwingForce.magnitude:F1}";

        GUI.Box(new Rect(20f, 20f, 430f, 345f), text);
    }

    private void OnDrawGizmos()
    {
        if (!debugEnabled || !drawSceneGizmos)
        {
            return;
        }

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerCamera == null)
        {
            return;
        }

        DrawAimGizmo();
        DrawTetherGizmo();
        DrawRopeLengthGizmo();
        DrawForceGizmo();
    }

    private void DrawAimGizmo()
    {
        if (!showAimLine)
        {
            return;
        }

        Vector3 start = playerCamera.transform.position;
        Vector3 end;

        if (Application.isPlaying && currentMode == PullMode.Extending)
        {
            end = extendingEndPoint;
            Gizmos.color = extendingColor;
        }
        else
        {
            end = start + playerCamera.transform.forward * maxTetherLength;
            Gizmos.color = aimColor;
        }

        Gizmos.DrawLine(start, end);
    }

    private void DrawTetherGizmo()
    {
        if (!Application.isPlaying || !showTetherLine || currentMode == PullMode.None)
        {
            return;
        }

        Gizmos.color = GetCurrentDebugColor();

        Vector3 start =
            currentMode == PullMode.PullObjectToPlayer && holdPoint != null
                ? holdPoint.position
                : GetPlayerTetherPoint();

        Vector3 end = GetCurrentGrabPoint();

        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(end, debugMarkerSize);
    }

    private void DrawRopeLengthGizmo()
    {
        if (!Application.isPlaying || !showRopeLengthGuide || !IsTethered)
        {
            return;
        }

        Gizmos.color = ropeLengthColor;

        if (currentMode == PullMode.PullObjectToPlayer)
        {
            Gizmos.DrawWireSphere(GetPlayerTetherPoint(), currentRopeLength);
        }
        else
        {
            Gizmos.DrawWireSphere(GetCurrentGrabPoint(), currentRopeLength);
        }
    }

    private void DrawForceGizmo()
    {
        if (!Application.isPlaying || !showForceLine || !IsTethered)
        {
            return;
        }

        if (lastPlayerTetherAcceleration.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Gizmos.color = forceColor;

        Vector3 start = GetPlayerTetherPoint();
        Vector3 end = start + lastPlayerTetherAcceleration.normalized * 1.5f;

        Gizmos.DrawLine(start, end);
    }
}