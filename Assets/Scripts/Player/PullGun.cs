using UnityEngine;
using UnityEngine.InputSystem;

// [RequireComponent(typeof(Rigidbody))]
public class PullGun : MonoBehaviour
{
    private enum PullState
    {
        Idle,
        Extending,
        Blocked,
        SwingAnchor,
        PullingObject
    }

    [Header("References")]
    [Tooltip("Camera used for aiming. If empty, the script finds a child Camera.")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("Where the tether visually starts. If empty, the camera position is used.")]
    [SerializeField] private Transform muzzlePoint;

    [Tooltip("Where pulled objects are pulled toward. If empty, one is generated in front of the camera.")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] public CoopFirstPersonController controller;

    [Header("Layers")]
    [Tooltip("Everything the tether can hit. Include walls, ground, pullable objects, and blockers.")]
    [SerializeField] private LayerMask aimBlockingLayers = ~0;

    [Tooltip("Only objects/surfaces on these layers can be attached to.")]
    [SerializeField] private LayerMask pullableLayers = ~0;

    [Header("Input")]
    [Tooltip("Hold left mouse to fire/hold tether. Release to detach.")]
    [SerializeField] private bool holdToUse = true;

    [Tooltip("Right mouse releases the tether.")]
    [SerializeField] private bool rightClickReleases = true;

    [Header("Tether Extension")]
    [Tooltip("Maximum distance the tether can extend.")]
    [SerializeField] private float maxTetherDistance = 35f;

    [Tooltip("How fast the tether shoots outward.")]
    [SerializeField] private float tetherExtendSpeed = 65f;

    [Tooltip("Initial visible tether length when firing.")]
    [SerializeField] private float initialTetherLength = 0.35f;

    [Tooltip("If true, the tether follows your aim while extending. If false, it fires in the original direction.")]
    [SerializeField] private bool tetherFollowsAimWhileExtending = false;

    [Tooltip("If true, hitting a non-pullable object stops the tether until released.")]
    [SerializeField] private bool nonPullableObjectsBlockTether = true;

    [Header("Fire Cooldown")]
    [Tooltip("Small cooldown to stop click-spam climbing.")]
    [SerializeField] private float fireCooldown = 0.2f;

    [Tooltip("Small cooldown after releasing.")]
    [SerializeField] private float releaseCooldown = 0.15f;

    [Header("Swing Rope")]
    [Tooltip("Small allowed stretch before the rope limit reacts.")]
    [SerializeField] private float ropeTolerance = 0.12f;

    [Tooltip("If true, outward velocity away from the anchor is removed when the rope is tight.")]
    [SerializeField] private bool removeOutwardVelocityAtLimit = true;

    [Tooltip("Maximum player speed while swinging.")]
    [SerializeField] private float maxSwingSpeed = 28f;

    [Tooltip("Maximum outward speed allowed immediately when attaching. This prevents backward-jump slingshots.")]
    [SerializeField] private float maxOutwardSpeedOnAttach = 0.5f;

    [Tooltip("Maximum total player speed immediately after attaching.")]
    [SerializeField] private float maxTotalSpeedOnAttach = 16f;

    [Tooltip("How strongly the rope corrects extra distance. Lower values are smoother. Set to 0 to disable positional correction.")]
    [SerializeField] private float ropeCorrectionStrength = 4f;

    [Tooltip("Maximum distance the rope can correct per physics frame. Lower values reduce camera shake. Set to 0 to only remove outward velocity.")]
    [SerializeField] private float maxRopeCorrectionPerFrame = 0.02f;

    [Tooltip("If true, uses Rigidbody.MovePosition for rope correction.")]
    [SerializeField] private bool useSmoothRopeCorrection = true;

    [Header("Swing Control")]
    [Tooltip("Air control while swinging. This force is tangential only, never along the rope.")]
    [SerializeField] private float airborneSwingControl = 18f;

    [Tooltip("Ground control while tethered.")]
    [SerializeField] private float groundedSwingControl = 10f;

    [Tooltip("How much direct input is allowed while airborne. Lower keeps swinging more natural.")]
    [Range(0f, 1f)]
    [SerializeField] private float airborneDirectControl = 0.08f;

    [Header("Object Pulling")]
    [Tooltip("Distance in front of the camera where objects are pulled.")]
    [SerializeField] private float objectHoldDistance = 4f;

    [Tooltip("Spring strength for pulling objects toward the Hold Point. Heavier objects move less because this uses normal Force.")]
    [SerializeField] private float objectSpringStrength = 220f;

    [Tooltip("Damping applied to pulled objects to reduce wobble.")]
    [SerializeField] private float objectDamping = 22f;

    [Tooltip("Maximum total force applied to objects. Lower values make heavy objects much harder to lift.")]
    [SerializeField] private float maxObjectPullForce = 2200f;

    [Tooltip("Maximum object velocity while being pulled.")]
    [SerializeField] private float maxPulledObjectVelocity = 18f;

    [Tooltip("If true, pulled objects keep gravity while being pulled.")]
    [SerializeField] private bool objectKeepsGravity = true;

    [Tooltip("Optional throw impulse when releasing pulled objects.")]
    [SerializeField] private float releaseThrowImpulse = 0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugPanel = true;
    [SerializeField] private bool drawGizmos = true;

    private Rigidbody playerRb;

    private PullState state = PullState.Idle;

    private Rigidbody targetRb;
    private Collider targetCollider;

    private Transform anchorTransform;
    private Vector3 staticAnchorWorldPoint;
    private Vector3 localAnchorPoint;
    private Vector3 localObjectGrabPoint;

    private Vector3 fireStartPoint;
    private Vector3 fireDirection;
    private Vector3 tetherEndPoint;

    private float currentTetherLength;
    private float ropeLength;
    private float nextAllowedFireTime;

    private Vector3 previousHoldPointPosition;

    private float debugCurrentMass;
    private float debugCurrentDistance;
    private float debugRopeError;
    private float debugOutwardSpeedRemoved;
    private float debugCorrectionAmount;
    private Vector3 debugObjectForce;
    private Vector3 debugSwingForce;

    public bool IsTethered => state == PullState.SwingAnchor || state == PullState.PullingObject;
    public bool IsExtendingTether => state == PullState.Extending;
    public bool IsTetherVisuallyActive => state != PullState.Idle;
    public bool IsTetheredToHeavyTarget => state == PullState.SwingAnchor;

    public float CurrentRopeLength => ropeLength;
    public float TargetRopeLength => ropeLength;
    public float AttachRopeLength => ropeLength;

    private void Awake()
    {
        // 1. Först hittar vi kontrollern
        controller = GetComponentInParent<CoopFirstPersonController>();

        // 2. Sedan använder vi kontrollern för att hitta spelarens Rigidbody
        if (controller != null)
        {
            playerRb = controller.GetComponent<Rigidbody>();
            Debug.Log("Sving-kontrollern och dess Rigidbody hittades!");
        }
        else
        {
            Debug.LogError("Hittade ingen CoopFirstPersonController!");
        }
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (holdPoint == null && playerCamera != null)
        {
            GameObject holdPointObject = new GameObject("Generated PullGun Hold Point");
            holdPointObject.transform.SetParent(playerCamera.transform);
            holdPointObject.transform.localPosition = Vector3.forward * objectHoldDistance;
            holdPointObject.transform.localRotation = Quaternion.identity;
            holdPoint = holdPointObject.transform;
        }

        ropeLength = objectHoldDistance;
        tetherEndPoint = GetTetherStartPoint();
    }

    private void Start()
    {
        if (holdPoint != null)
        {
            previousHoldPointPosition = holdPoint.position;
        }
    }

    private void OnDisable()
    {
        ReleasePull();
    }


    private void Update()
    {
        if (!enabled) return;

        HandleInput();
        UpdateHoldPoint();
    }

    private void FixedUpdate()
    {
        if (!enabled) return;

        ResetDebugValues();

        switch (state)
        {
            case PullState.SwingAnchor:
                SimulateSwingAnchor();
                break;

            case PullState.PullingObject:
                SimulateObjectPull();
                break;
        }

        if (holdPoint != null)
        {
            previousHoldPointPosition = holdPoint.position;
        }
    }

    private void HandleInput()
    {
        if (!enabled) return;

        if (Mouse.current == null)
        {
            return;
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (state == PullState.Idle && Time.time >= nextAllowedFireTime)
            {
                StartTether();
            }
        }

        if (state == PullState.Extending && Mouse.current.leftButton.isPressed)
        {
            UpdateExtendingTether();
        }

        if (holdToUse && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            ReleasePull();
        }

        if (rightClickReleases && Mouse.current.rightButton.wasPressedThisFrame)
        {
            ReleasePull();
        }
    }


    private void StartTether()
    {
        if (playerCamera == null)
        {
            return;
        }

        ClearTarget();

        state = PullState.Extending;

        fireStartPoint = GetTetherStartPoint();
        fireDirection = playerCamera.transform.forward.normalized;

        currentTetherLength = Mathf.Max(0.01f, initialTetherLength);
        tetherEndPoint = fireStartPoint + fireDirection * currentTetherLength;

        nextAllowedFireTime = Time.time + fireCooldown;
    }

    private void UpdateExtendingTether()
    {
        if (playerCamera == null)
        {
            ReleasePull();
            return;
        }

        if (tetherFollowsAimWhileExtending)
        {
            fireStartPoint = GetTetherStartPoint();
            fireDirection = playerCamera.transform.forward.normalized;
        }

        currentTetherLength += tetherExtendSpeed * Time.deltaTime;
        currentTetherLength = Mathf.Clamp(currentTetherLength, initialTetherLength, maxTetherDistance);

        Ray ray = new Ray(fireStartPoint, fireDirection);

        if (Physics.Raycast(ray, out RaycastHit hit, currentTetherLength, aimBlockingLayers, QueryTriggerInteraction.Ignore))
        {
            tetherEndPoint = hit.point;

            if (IsHitPullable(hit))
            {
                AttachToHit(hit);
                return;
            }

            if (nonPullableObjectsBlockTether)
            {
                state = PullState.Blocked;
                return;
            }
        }
        else
        {
            tetherEndPoint = fireStartPoint + fireDirection * currentTetherLength;
        }

        if (currentTetherLength >= maxTetherDistance)
        {
            state = PullState.Blocked;
        }
    }

    private void AttachToHit(RaycastHit hit)
    {
        targetCollider = hit.collider;
        targetRb = hit.rigidbody;

        bool hasDynamicRigidbody = targetRb != null && !targetRb.isKinematic;

        if (hasDynamicRigidbody)
        {
            StartObjectPull(hit);
        }
        else
        {
            StartSwingAnchor(hit);
        }
    }

    private void StartSwingAnchor(RaycastHit hit)
    {
        state = PullState.SwingAnchor;

        targetRb = hit.rigidbody;
        targetCollider = hit.collider;

        anchorTransform = targetRb != null ? targetRb.transform : hit.collider.transform;
        staticAnchorWorldPoint = hit.point;

        localAnchorPoint = anchorTransform != null
            ? anchorTransform.InverseTransformPoint(hit.point)
            : hit.point;

        ropeLength = Vector3.Distance(GetPlayerPhysicsPoint(), hit.point);
        ropeLength = Mathf.Clamp(ropeLength, 0.1f, maxTetherDistance);

        tetherEndPoint = hit.point;

        RemoveSlingshotVelocityOnAttach(hit.point);
    }

    private void StartObjectPull(RaycastHit hit)
    {
        state = PullState.PullingObject;

        targetRb = hit.rigidbody;
        targetCollider = hit.collider;

        anchorTransform = null;
        localObjectGrabPoint = targetRb.transform.InverseTransformPoint(hit.point);

        targetRb.useGravity = objectKeepsGravity;
        targetRb.interpolation = RigidbodyInterpolation.Interpolate;
        targetRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        targetRb.WakeUp();

        ropeLength = Vector3.Distance(GetTetherStartPoint(), hit.point);
        ropeLength = Mathf.Clamp(ropeLength, 0.1f, maxTetherDistance);

        tetherEndPoint = hit.point;
        debugCurrentMass = targetRb.mass;
    }

    private void SimulateSwingAnchor()
    {
        Vector3 anchorPoint = GetCurrentAnchorPoint();
        tetherEndPoint = anchorPoint;

        ApplyNonElasticRopeLimit(anchorPoint);
        ApplyTangentialSwingControl(anchorPoint);
        LimitPlayerSwingSpeed();
    }

    private void ApplyNonElasticRopeLimit(Vector3 anchorPoint)
    {
        Vector3 playerPoint = GetPlayerPhysicsPoint();
        Vector3 anchorToPlayer = playerPoint - anchorPoint;

        float distance = anchorToPlayer.magnitude;
        debugCurrentDistance = distance;

        if (distance <= 0.001f)
        {
            return;
        }

        float error = distance - ropeLength;
        debugRopeError = error;

        if (error <= ropeTolerance)
        {
            return;
        }

        Vector3 anchorToPlayerDirection = anchorToPlayer / distance;

        if (removeOutwardVelocityAtLimit)
        {
            RemoveOutwardVelocity(anchorToPlayerDirection);
        }

        ApplySmallRopePositionCorrection(anchorToPlayerDirection, error);
    }

    private void RemoveOutwardVelocity(Vector3 anchorToPlayerDirection)
    {
        Vector3 velocity = playerRb.linearVelocity;
        float outwardSpeed = Vector3.Dot(velocity, anchorToPlayerDirection);

        if (outwardSpeed <= 0f)
        {
            return;
        }

        playerRb.linearVelocity = velocity - anchorToPlayerDirection * outwardSpeed;
        debugOutwardSpeedRemoved = outwardSpeed;
    }

    private void ApplySmallRopePositionCorrection(Vector3 anchorToPlayerDirection, float error)
    {
        if (ropeCorrectionStrength <= 0f || maxRopeCorrectionPerFrame <= 0f)
        {
            return;
        }

        float correctionDistance = error * ropeCorrectionStrength * Time.fixedDeltaTime;
        correctionDistance = Mathf.Min(correctionDistance, maxRopeCorrectionPerFrame);

        if (correctionDistance <= 0f)
        {
            return;
        }

        Vector3 correction = -anchorToPlayerDirection * correctionDistance;

        if (useSmoothRopeCorrection)
        {
            playerRb.MovePosition(playerRb.position + correction);
        }
        else
        {
            playerRb.position += correction;
        }

        debugCorrectionAmount = correctionDistance;
    }

    private void RemoveSlingshotVelocityOnAttach(Vector3 anchorPoint)
    {
        Vector3 playerPoint = GetPlayerPhysicsPoint();
        Vector3 anchorToPlayer = playerPoint - anchorPoint;

        if (anchorToPlayer.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Vector3 anchorToPlayerDirection = anchorToPlayer.normalized;

        Vector3 velocity = playerRb.linearVelocity;
        float outwardSpeed = Vector3.Dot(velocity, anchorToPlayerDirection);

        if (outwardSpeed > maxOutwardSpeedOnAttach)
        {
            float excessSpeed = outwardSpeed - maxOutwardSpeedOnAttach;
            velocity -= anchorToPlayerDirection * excessSpeed;
            debugOutwardSpeedRemoved = excessSpeed;
        }

        if (velocity.magnitude > maxTotalSpeedOnAttach)
        {
            velocity = velocity.normalized * maxTotalSpeedOnAttach;
        }

        playerRb.linearVelocity = velocity;
    }

    private void ApplyTangentialSwingControl(Vector3 anchorPoint)
    {
        Vector3 playerPoint = GetPlayerPhysicsPoint();
        Vector3 anchorToPlayer = playerPoint - anchorPoint;

        if (anchorToPlayer.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Vector3 ropeDirection = anchorToPlayer.normalized;
        Vector3 inputDirection = GetWorldInputDirection();

        if (inputDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Vector3 tangentialDirection = Vector3.ProjectOnPlane(inputDirection, ropeDirection);

        if (tangentialDirection.sqrMagnitude <= 0.001f)
        {
            return;
        }

        tangentialDirection.Normalize();

        bool grounded = controller != null && controller.IsGrounded;
        float control = grounded ? groundedSwingControl : airborneSwingControl;

        Vector3 force = tangentialDirection * control;

        if (!grounded)
        {
            force *= airborneDirectControl;
        }

        playerRb.AddForce(force, ForceMode.Acceleration);
        debugSwingForce = force;
    }

    private void LimitPlayerSwingSpeed()
    {
        if (playerRb.linearVelocity.magnitude > maxSwingSpeed)
        {
            playerRb.linearVelocity = playerRb.linearVelocity.normalized * maxSwingSpeed;
        }
    }

    private void SimulateObjectPull()
    {
        if (targetRb == null || holdPoint == null)
        {
            ReleasePull();
            return;
        }

        Vector3 grabPoint = targetRb.transform.TransformPoint(localObjectGrabPoint);
        tetherEndPoint = grabPoint;

        Vector3 toHoldPoint = holdPoint.position - grabPoint;
        float distance = toHoldPoint.magnitude;

        if (distance <= 0.001f)
        {
            return;
        }

        Vector3 direction = toHoldPoint / distance;

        Vector3 pointVelocity = targetRb.GetPointVelocity(grabPoint);
        Vector3 holdVelocity = GetHoldPointVelocity();
        Vector3 relativeVelocity = pointVelocity - holdVelocity;

        Vector3 springForce = direction * distance * objectSpringStrength;
        Vector3 dampingForce = -relativeVelocity * objectDamping;

        Vector3 totalForce = springForce + dampingForce;

        if (totalForce.magnitude > maxObjectPullForce)
        {
            totalForce = totalForce.normalized * maxObjectPullForce;
        }

        targetRb.AddForceAtPosition(totalForce, grabPoint, ForceMode.Force);

        if (targetRb.linearVelocity.magnitude > maxPulledObjectVelocity)
        {
            targetRb.linearVelocity = targetRb.linearVelocity.normalized * maxPulledObjectVelocity;
        }

        debugObjectForce = totalForce;
        debugCurrentMass = targetRb.mass;
        debugCurrentDistance = distance;
    }

    private void UpdateHoldPoint()
    {
        if (holdPoint == null || playerCamera == null)
        {
            return;
        }

        holdPoint.position = playerCamera.transform.position + playerCamera.transform.forward * objectHoldDistance;
        holdPoint.rotation = playerCamera.transform.rotation;
    }

    private Vector3 GetHoldPointVelocity()
    {
        if (holdPoint == null || Time.fixedDeltaTime <= 0f)
        {
            return Vector3.zero;
        }

        return (holdPoint.position - previousHoldPointPosition) / Time.fixedDeltaTime;
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

        Vector3 right = transform.right;
        Vector3 forward = transform.forward;

        right.y = 0f;
        forward.y = 0f;

        right.Normalize();
        forward.Normalize();

        return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
    }

    private Vector3 GetCurrentAnchorPoint()
    {
        if (anchorTransform != null)
        {
            return anchorTransform.TransformPoint(localAnchorPoint);
        }

        return staticAnchorWorldPoint;
    }

    private Vector3 GetPlayerPhysicsPoint()
    {
        return playerRb.worldCenterOfMass;
    }

    private Vector3 GetTetherStartPoint()
    {
        if (muzzlePoint != null)
        {
            return muzzlePoint.position;
        }

        if (playerCamera != null)
        {
            return playerCamera.transform.position;
        }

        return transform.position;
    }

    public Vector3 GetVisualTetherStartPoint()
    {
        return GetTetherStartPoint();
    }

    public Vector3 GetVisualTetherEndPoint()
    {
        return tetherEndPoint;
    }

    private bool IsHitPullable(RaycastHit hit)
    {
        if (hit.collider == null)
        {
            return false;
        }

        if (IsLayerInMask(hit.collider.gameObject.layer, pullableLayers))
        {
            return true;
        }

        Rigidbody attachedBody = hit.collider.attachedRigidbody;

        if (attachedBody != null)
        {
            if (IsLayerInMask(attachedBody.gameObject.layer, pullableLayers))
            {
                return true;
            }

            if (IsTransformOrParentInLayerMask(attachedBody.transform, pullableLayers))
            {
                return true;
            }
        }

        return IsTransformOrParentInLayerMask(hit.collider.transform, pullableLayers);
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

    public void ReleasePull()
    {
        if (state == PullState.PullingObject && targetRb != null && playerCamera != null && releaseThrowImpulse > 0f)
        {
            targetRb.AddForce(playerCamera.transform.forward * releaseThrowImpulse, ForceMode.VelocityChange);
        }

        state = PullState.Idle;
        ClearTarget();

        currentTetherLength = 0f;
        tetherEndPoint = GetTetherStartPoint();

        nextAllowedFireTime = Mathf.Max(nextAllowedFireTime, Time.time + releaseCooldown);
    }

    private void ClearTarget()
    {
        targetRb = null;
        targetCollider = null;

        anchorTransform = null;
        staticAnchorWorldPoint = Vector3.zero;
        localAnchorPoint = Vector3.zero;
        localObjectGrabPoint = Vector3.zero;

        ropeLength = objectHoldDistance;
    }

    private void ResetDebugValues()
    {
        debugCurrentMass = targetRb != null ? targetRb.mass : 0f;
        debugCurrentDistance = 0f;
        debugRopeError = 0f;
        debugOutwardSpeedRemoved = 0f;
        debugCorrectionAmount = 0f;
        debugObjectForce = Vector3.zero;
        debugSwingForce = Vector3.zero;
    }

    private void OnGUI()
    {
        if (!showDebugPanel || !enabled)
        {
            return;
        }

        string targetName = targetCollider != null ? targetCollider.name : "None";
        float cooldown = Mathf.Max(0f, nextAllowedFireTime - Time.time);

        string text =
            $"Clean PullGun\n" +
            $"State: {state}\n" +
            $"Target: {targetName}\n" +
            $"Rope Length: {ropeLength:F2}\n" +
            $"Tether Length: {currentTetherLength:F2}\n" +
            $"Distance: {debugCurrentDistance:F2}\n" +
            $"Rope Error: {debugRopeError:F3}\n" +
            $"Outward Removed: {debugOutwardSpeedRemoved:F2}\n" +
            $"Correction: {debugCorrectionAmount:F3}\n" +
            $"Object Mass: {debugCurrentMass:F1}\n" +
            $"Object Force: {debugObjectForce.magnitude:F0}\n" +
            $"Swing Force: {debugSwingForce.magnitude:F1}\n" +
            $"Cooldown: {cooldown:F2}";

        GUI.Box(new Rect(20f, 20f, 380f, 295f), text);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        if (!Application.isPlaying)
        {
            return;
        }

        if (!IsTetherVisuallyActive)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(GetVisualTetherStartPoint(), GetVisualTetherEndPoint());
        Gizmos.DrawSphere(GetVisualTetherEndPoint(), 0.12f);
    }
}