using UnityEngine;

[RequireComponent(typeof(Collider))]
public class SimpleMassDoor : MonoBehaviour
{
    public enum DoorMoveDirection
    {
        Up,
        Down,
        Left,
        Right,
        Forward,
        Backward,
        CustomLocalDirection,
        CustomWorldDirection
    }

    public enum DoorVisualStateMode
    {
        TargetOpen,
        FullyOpen,
        PartlyOpen
    }

    [Header("Door Movement")]
    [Tooltip("Which direction the door moves when opening.")]
    [SerializeField] private DoorMoveDirection moveDirection = DoorMoveDirection.Up;

    [Tooltip("Used when Move Direction is Custom Local Direction or Custom World Direction.")]
    [SerializeField] private Vector3 customDirection = Vector3.up;

    [Tooltip("How far the door moves from its closed position.")]
    [SerializeField] private float moveDistance = 3f;

    [Tooltip("How fast the door opens.")]
    [SerializeField] private float openSpeed = 4f;

    [Tooltip("How fast the door closes.")]
    [SerializeField] private float closeSpeed = 4f;

    [Tooltip("If true, the door starts open when the scene begins.")]
    [SerializeField] private bool startsOpen = false;

    [Tooltip("If true, the door stays open forever after opening once.")]
    [SerializeField] private bool stayOpenOnceOpened = false;

    [Header("Door Frame Visuals")]
    [Tooltip("Renderers that use the emissive door frame material.")]
    [SerializeField] private Renderer[] doorFrameRenderers;

    [Tooltip("The shader bool/float property used by the door frame material. Use the Shader Graph Reference name, often DoorOpen or _DoorOpen.")]
    [SerializeField] private string doorOpenShaderProperty = "DoorOpen";

    [Tooltip("If true, uses MaterialPropertyBlock so this door can change visuals without creating unique material instances.")]
    [SerializeField] private bool useMaterialPropertyBlock = true;

    [Tooltip("Controls when the door frame becomes green/open.")]
    [SerializeField] private DoorVisualStateMode visualStateMode = DoorVisualStateMode.TargetOpen;

    [Tooltip("Used by Partly Open mode. If Open Amount is above this, the frame is considered open.")]
    [Range(0f, 1f)]
    [SerializeField] private float partlyOpenVisualThreshold = 0.05f;

    [Header("Door Frame Lights")]
    [Tooltip("Realtime lights that should become green when open and red when closed.")]
    [SerializeField] private Light[] doorStatusLights;

    [SerializeField] private Color closedLightColor = Color.red;
    [SerializeField] private Color openLightColor = Color.green;

    [Tooltip("Light intensity when the door is closed.")]
    [SerializeField] private float closedLightIntensity = 2f;

    [Tooltip("Light intensity when the door is open.")]
    [SerializeField] private float openLightIntensity = 2f;

    [Tooltip("If true, light color smoothly blends between red and green while the door moves.")]
    [SerializeField] private bool blendLightColorByOpenAmount = false;

    [Header("Blocking / Safety")]
    [Tooltip("If true, the door will not close while the player or physics objects are in the way.")]
    [SerializeField] private bool blockClosingWhenObstructed = true;

    [Tooltip("Layers that can block the door from closing. Include Player and Pullable/physics object layers.")]
    [SerializeField] private LayerMask closingBlockerLayers = ~0;

    [Tooltip("If true, trigger colliders can block the door. Usually keep this false.")]
    [SerializeField] private bool triggersCanBlockClosing = false;

    [Tooltip("Extra padding around the door collider when checking if something blocks closing.")]
    [SerializeField] private float blockerCheckPadding = 0.03f;

    [Tooltip("How many small close-steps are tested per physics frame. Higher values catch blockers more reliably on fast doors.")]
    [SerializeField] private int closingSafetySteps = 4;

    [Tooltip("If true, the door also checks its current position while blocked. This keeps it locked until the blocker is actually gone.")]
    [SerializeField] private bool holdBlockedPositionUntilClear = true;

    [Header("Physics")]
    [Tooltip("If true, a kinematic Rigidbody is added/used so the door collider moves safely and cannot be pushed by physics objects.")]
    [SerializeField] private bool useKinematicRigidbody = true;

    [Tooltip("Rigidbody interpolation used if Use Kinematic Rigidbody is enabled.")]
    [SerializeField] private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;

    [Tooltip("Collision detection mode used if Use Kinematic Rigidbody is enabled.")]
    [SerializeField] private CollisionDetectionMode collisionDetection = CollisionDetectionMode.ContinuousSpeculative;

    [Header("Audio Optional")]
    [Tooltip("Optional sound played when the door starts opening.")]
    [SerializeField] private AudioSource openSound;

    [Tooltip("Optional sound played when the door starts closing.")]
    [SerializeField] private AudioSource closeSound;

    [Tooltip("Optional sound played once when the door tries to close but is blocked.")]
    [SerializeField] private AudioSource blockedSound;

    [Header("Debug")]
    [Tooltip("Draws the closed and open positions in the Scene view.")]
    [SerializeField] private bool drawGizmos = true;

    [Tooltip("Shows door debug information in the Game view.")]
    [SerializeField] private bool showDebugPanel = false;

    private Rigidbody doorRigidbody;
    private Collider[] doorColliders;

    private Vector3 closedPosition;
    private Vector3 openPosition;

    private bool targetOpen;
    private bool isOpen;
    private bool hasOpenedOnce;
    private bool wasMoving;
    private bool wasBlocked;

    private bool lastVisualOpenState;
    private MaterialPropertyBlock materialPropertyBlock;

    public bool IsOpen => isOpen;
    public bool TargetOpen => targetOpen;
    public bool IsBlocked { get; private set; }
    public float OpenAmount { get; private set; }

    private void Reset()
    {
        Collider doorCollider = GetComponent<Collider>();
        doorCollider.isTrigger = false;

        useKinematicRigidbody = true;
        blockClosingWhenObstructed = true;
        blockerCheckPadding = 0.03f;
        closingSafetySteps = 4;
        holdBlockedPositionUntilClear = true;

        closedLightColor = Color.red;
        openLightColor = Color.green;
        closedLightIntensity = 2f;
        openLightIntensity = 2f;
        doorOpenShaderProperty = "DoorOpen";
    }

    private void Awake()
    {
        CacheDoorColliders();
        SetupRigidbody();

        if (useMaterialPropertyBlock)
        {
            materialPropertyBlock = new MaterialPropertyBlock();
        }

        closedPosition = transform.position;
        openPosition = closedPosition + GetWorldMoveDirection() * moveDistance;

        targetOpen = startsOpen;
        isOpen = startsOpen;
        OpenAmount = startsOpen ? 1f : 0f;

        Vector3 startPosition = startsOpen ? openPosition : closedPosition;
        MoveDoorToPosition(startPosition);

        lastVisualOpenState = GetVisualOpenState();
        ApplyDoorVisuals(lastVisualOpenState, true);
    }

    private void FixedUpdate()
    {
        UpdateDoorMovement();
        UpdateDoorVisuals();
    }

    private void CacheDoorColliders()
    {
        doorColliders = GetComponentsInChildren<Collider>();

        foreach (Collider doorCollider in doorColliders)
        {
            if (doorCollider == null)
            {
                continue;
            }

            doorCollider.isTrigger = false;
        }
    }

    private void SetupRigidbody()
    {
        if (!useKinematicRigidbody)
        {
            return;
        }

        doorRigidbody = GetComponent<Rigidbody>();

        if (doorRigidbody == null)
        {
            doorRigidbody = gameObject.AddComponent<Rigidbody>();
        }

        doorRigidbody.isKinematic = true;
        doorRigidbody.useGravity = false;
        doorRigidbody.interpolation = interpolation;
        doorRigidbody.collisionDetectionMode = collisionDetection;
    }

    private void UpdateDoorMovement()
    {
        if (stayOpenOnceOpened && hasOpenedOnce)
        {
            targetOpen = true;
        }

        float targetAmount = targetOpen ? 1f : 0f;
        float speed = targetOpen ? openSpeed : closeSpeed;

        float previousAmount = OpenAmount;
        float candidateAmount = Mathf.MoveTowards(
            OpenAmount,
            targetAmount,
            speed * Time.fixedDeltaTime
        );

        IsBlocked = false;

        bool tryingToClose = candidateAmount < OpenAmount;

        if (blockClosingWhenObstructed && tryingToClose)
        {
            candidateAmount = GetSafeClosingAmount(OpenAmount, candidateAmount);

            if (Mathf.Approximately(candidateAmount, OpenAmount))
            {
                IsBlocked = true;
            }
        }

        OpenAmount = candidateAmount;

        Vector3 targetPosition = Vector3.Lerp(closedPosition, openPosition, OpenAmount);
        MoveDoorToPosition(targetPosition);

        bool moving = !Mathf.Approximately(OpenAmount, targetAmount) && !IsBlocked;

        if (moving && !wasMoving)
        {
            if (targetOpen)
            {
                PlayOpenSound();
            }
            else
            {
                PlayCloseSound();
            }
        }

        if (IsBlocked && !wasBlocked)
        {
            PlayBlockedSound();
        }

        wasMoving = moving;
        wasBlocked = IsBlocked;

        isOpen = OpenAmount >= 0.99f;

        if (targetOpen && OpenAmount > previousAmount)
        {
            hasOpenedOnce = true;
        }
    }

    private void UpdateDoorVisuals()
    {
        bool visualOpenState = GetVisualOpenState();

        if (visualOpenState != lastVisualOpenState || blendLightColorByOpenAmount)
        {
            ApplyDoorVisuals(visualOpenState, false);
            lastVisualOpenState = visualOpenState;
        }
    }

    private bool GetVisualOpenState()
    {
        switch (visualStateMode)
        {
            case DoorVisualStateMode.TargetOpen:
                return targetOpen;

            case DoorVisualStateMode.FullyOpen:
                return isOpen;

            case DoorVisualStateMode.PartlyOpen:
                return OpenAmount >= partlyOpenVisualThreshold;

            default:
                return targetOpen;
        }
    }

    private void ApplyDoorVisuals(bool open, bool force)
    {
        ApplyDoorFrameShaderBool(open);
        ApplyDoorLights(open);
    }

    private void ApplyDoorFrameShaderBool(bool open)
    {
        if (doorFrameRenderers == null || doorFrameRenderers.Length == 0)
        {
            return;
        }

        float openValue = open ? 1f : 0f;

        for (int i = 0; i < doorFrameRenderers.Length; i++)
        {
            Renderer doorFrameRenderer = doorFrameRenderers[i];

            if (doorFrameRenderer == null)
            {
                continue;
            }

            if (useMaterialPropertyBlock)
            {
                doorFrameRenderer.GetPropertyBlock(materialPropertyBlock);
                materialPropertyBlock.SetFloat(doorOpenShaderProperty, openValue);
                doorFrameRenderer.SetPropertyBlock(materialPropertyBlock);
            }
            else
            {
                Material[] materials = doorFrameRenderer.materials;

                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];

                    if (material == null)
                    {
                        continue;
                    }

                    material.SetFloat(doorOpenShaderProperty, openValue);
                }
            }
        }
    }

    private void ApplyDoorLights(bool open)
    {
        if (doorStatusLights == null || doorStatusLights.Length == 0)
        {
            return;
        }

        Color lightColor;
        float lightIntensity;

        if (blendLightColorByOpenAmount)
        {
            lightColor = Color.Lerp(closedLightColor, openLightColor, OpenAmount);
            lightIntensity = Mathf.Lerp(closedLightIntensity, openLightIntensity, OpenAmount);
        }
        else
        {
            lightColor = open ? openLightColor : closedLightColor;
            lightIntensity = open ? openLightIntensity : closedLightIntensity;
        }

        for (int i = 0; i < doorStatusLights.Length; i++)
        {
            Light statusLight = doorStatusLights[i];

            if (statusLight == null)
            {
                continue;
            }

            statusLight.color = lightColor;
            statusLight.intensity = lightIntensity;
        }
    }

    private float GetSafeClosingAmount(float fromAmount, float requestedAmount)
    {
        if (holdBlockedPositionUntilClear)
        {
            Vector3 currentPosition = Vector3.Lerp(closedPosition, openPosition, fromAmount);

            if (WouldDoorOverlapBlockerAtPosition(currentPosition))
            {
                return fromAmount;
            }
        }

        int steps = Mathf.Max(1, closingSafetySteps);
        float safeAmount = fromAmount;

        for (int i = 1; i <= steps; i++)
        {
            float testAmount = Mathf.Lerp(fromAmount, requestedAmount, i / (float)steps);
            Vector3 testPosition = Vector3.Lerp(closedPosition, openPosition, testAmount);

            if (WouldDoorOverlapBlockerAtPosition(testPosition))
            {
                return safeAmount;
            }

            safeAmount = testAmount;
        }

        return requestedAmount;
    }

    private bool WouldDoorOverlapBlockerAtPosition(Vector3 candidateDoorPosition)
    {
        if (doorColliders == null || doorColliders.Length == 0)
        {
            CacheDoorColliders();
        }

        Vector3 moveDelta = candidateDoorPosition - transform.position;

        QueryTriggerInteraction queryTriggerInteraction = triggersCanBlockClosing
            ? QueryTriggerInteraction.Collide
            : QueryTriggerInteraction.Ignore;

        foreach (Collider doorCollider in doorColliders)
        {
            if (doorCollider == null)
            {
                continue;
            }

            if (doorCollider.isTrigger)
            {
                continue;
            }

            Collider[] overlaps = GetOverlapsForDoorCollider(
                doorCollider,
                moveDelta,
                queryTriggerInteraction
            );

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider hit = overlaps[i];

                if (hit == null)
                {
                    continue;
                }

                if (IsOwnCollider(hit))
                {
                    continue;
                }

                if (hit.attachedRigidbody == doorRigidbody)
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private Collider[] GetOverlapsForDoorCollider(
        Collider doorCollider,
        Vector3 moveDelta,
        QueryTriggerInteraction queryTriggerInteraction
    )
    {
        if (doorCollider is BoxCollider box)
        {
            Vector3 center = box.transform.TransformPoint(box.center) + moveDelta;
            Vector3 halfExtents = Vector3.Scale(box.size * 0.5f, AbsVector(box.transform.lossyScale));
            halfExtents += Vector3.one * blockerCheckPadding;

            return Physics.OverlapBox(
                center,
                halfExtents,
                box.transform.rotation,
                closingBlockerLayers,
                queryTriggerInteraction
            );
        }

        if (doorCollider is SphereCollider sphere)
        {
            Vector3 center = sphere.transform.TransformPoint(sphere.center) + moveDelta;
            float radius = sphere.radius * GetLargestScaleAxis(sphere.transform.lossyScale);
            radius += blockerCheckPadding;

            return Physics.OverlapSphere(
                center,
                radius,
                closingBlockerLayers,
                queryTriggerInteraction
            );
        }

        if (doorCollider is CapsuleCollider capsule)
        {
            GetCapsuleWorldPoints(
                capsule,
                moveDelta,
                out Vector3 pointA,
                out Vector3 pointB,
                out float radius
            );

            radius += blockerCheckPadding;

            return Physics.OverlapCapsule(
                pointA,
                pointB,
                radius,
                closingBlockerLayers,
                queryTriggerInteraction
            );
        }

        Bounds bounds = doorCollider.bounds;
        bounds.center += moveDelta;
        bounds.Expand(blockerCheckPadding * 2f);

        return Physics.OverlapBox(
            bounds.center,
            bounds.extents,
            Quaternion.identity,
            closingBlockerLayers,
            queryTriggerInteraction
        );
    }

    private void GetCapsuleWorldPoints(
        CapsuleCollider capsule,
        Vector3 moveDelta,
        out Vector3 pointA,
        out Vector3 pointB,
        out float radius
    )
    {
        Transform capsuleTransform = capsule.transform;

        Vector3 center = capsuleTransform.TransformPoint(capsule.center) + moveDelta;
        Vector3 scale = AbsVector(capsuleTransform.lossyScale);

        Vector3 axis;
        float heightScale;
        float radiusScale;

        switch (capsule.direction)
        {
            case 0:
                axis = capsuleTransform.right;
                heightScale = scale.x;
                radiusScale = Mathf.Max(scale.y, scale.z);
                break;

            case 1:
                axis = capsuleTransform.up;
                heightScale = scale.y;
                radiusScale = Mathf.Max(scale.x, scale.z);
                break;

            default:
                axis = capsuleTransform.forward;
                heightScale = scale.z;
                radiusScale = Mathf.Max(scale.x, scale.y);
                break;
        }

        radius = capsule.radius * radiusScale;
        float height = Mathf.Max(capsule.height * heightScale, radius * 2f);
        float halfLine = Mathf.Max(0f, height * 0.5f - radius);

        pointA = center + axis * halfLine;
        pointB = center - axis * halfLine;
    }

    private bool IsOwnCollider(Collider other)
    {
        if (other.transform == transform)
        {
            return true;
        }

        if (other.transform.IsChildOf(transform))
        {
            return true;
        }

        return false;
    }

    private void MoveDoorToPosition(Vector3 position)
    {
        if (doorRigidbody != null && doorRigidbody.isKinematic)
        {
            doorRigidbody.MovePosition(position);
        }
        else
        {
            transform.position = position;
        }
    }

    private Vector3 GetWorldMoveDirection()
    {
        Vector3 direction;

        switch (moveDirection)
        {
            case DoorMoveDirection.Up:
                direction = Vector3.up;
                break;

            case DoorMoveDirection.Down:
                direction = Vector3.down;
                break;

            case DoorMoveDirection.Left:
                direction = -transform.right;
                break;

            case DoorMoveDirection.Right:
                direction = transform.right;
                break;

            case DoorMoveDirection.Forward:
                direction = transform.forward;
                break;

            case DoorMoveDirection.Backward:
                direction = -transform.forward;
                break;

            case DoorMoveDirection.CustomLocalDirection:
                direction = transform.TransformDirection(customDirection);
                break;

            case DoorMoveDirection.CustomWorldDirection:
                direction = customDirection;
                break;

            default:
                direction = Vector3.up;
                break;
        }

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return Vector3.up;
        }

        return direction.normalized;
    }

    public void Open()
    {
        targetOpen = true;
        UpdateDoorVisuals();
    }

    public void Close()
    {
        if (stayOpenOnceOpened && hasOpenedOnce)
        {
            return;
        }

        targetOpen = false;
        UpdateDoorVisuals();
    }

    public void SetOpen(bool open)
    {
        if (open)
        {
            Open();
        }
        else
        {
            Close();
        }
    }

    public void Toggle()
    {
        SetOpen(!targetOpen);
    }

    private Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z)
        );
    }

    private float GetLargestScaleAxis(Vector3 scale)
    {
        scale = AbsVector(scale);
        return Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
    }

    private void PlayOpenSound()
    {
        if (openSound != null)
        {
            openSound.Play();
        }
    }

    private void PlayCloseSound()
    {
        if (closeSound != null)
        {
            closeSound.Play();
        }
    }

    private void PlayBlockedSound()
    {
        if (blockedSound != null)
        {
            blockedSound.Play();
        }
    }

    private void OnGUI()
    {
        if (!showDebugPanel)
        {
            return;
        }

        string text =
            $"Simple Mass Door\n" +
            $"Name: {name}\n" +
            $"Open Amount: {OpenAmount:P0}\n" +
            $"Target Open: {targetOpen}\n" +
            $"Is Open: {isOpen}\n" +
            $"Blocked: {IsBlocked}\n" +
            $"Visual Open: {GetVisualOpenState()}";

        GUI.Box(new Rect(350f, 380f, 280f, 160f), text);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        Vector3 previewClosed = Application.isPlaying ? closedPosition : transform.position;
        Vector3 previewOpen = previewClosed + GetWorldMoveDirection() * moveDistance;

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(previewClosed, Vector3.one * 0.25f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(previewOpen, Vector3.one * 0.25f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(previewClosed, previewOpen);
    }
}