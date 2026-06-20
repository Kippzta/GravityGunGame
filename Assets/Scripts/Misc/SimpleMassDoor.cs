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

    [Header("Blocking / Safety")]
    [Tooltip("If true, the door will not close while the player or physics objects are in the doorway.")]
    [SerializeField] private bool blockClosingWhenObstructed = true;

    [Tooltip("Layers that can block the door from closing. Include Player and Pullable/physics object layers.")]
    [SerializeField] private LayerMask closingBlockerLayers = ~0;

    [Tooltip("If true, trigger colliders can block the door. Usually keep this false.")]
    [SerializeField] private bool triggersCanBlockClosing = false;

    [Tooltip("Extra padding around the door collider when checking if something blocks closing.")]
    [SerializeField] private float blockerCheckPadding = 0.03f;

    [Tooltip("If true, the door opens slightly when blocked while closing. This helps avoid squeezing objects.")]
    [SerializeField] private bool pushOpenSlightlyWhenBlocked = true;

    [Tooltip("How much the door opens back up per second when blocked while closing.")]
    [SerializeField] private float blockedOpenBackSpeed = 1f;

    [Header("Physics")]
    [Tooltip("If true, a kinematic Rigidbody is added/used so the door collider moves safely through physics.")]
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

    [Tooltip("Optional sound played when the door tries to close but is blocked.")]
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

    public bool IsOpen => isOpen;
    public bool TargetOpen => targetOpen;
    public bool IsBlocked { get; private set; }
    public float OpenAmount { get; private set; }

    private void Reset()
    {
        Collider doorCollider = GetComponent<Collider>();
        doorCollider.isTrigger = false;
    }

    private void Awake()
    {
        doorColliders = GetComponentsInChildren<Collider>();

        SetupRigidbody();

        closedPosition = transform.position;
        openPosition = closedPosition + GetWorldMoveDirection() * moveDistance;

        targetOpen = startsOpen;
        isOpen = startsOpen;
        OpenAmount = startsOpen ? 1f : 0f;

        Vector3 startPosition = startsOpen ? openPosition : closedPosition;
        MoveDoorToPosition(startPosition);
    }

    private void FixedUpdate()
    {
        UpdateDoorMovement();
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
            Vector3 candidatePosition = Vector3.Lerp(closedPosition, openPosition, candidateAmount);

            if (WouldDoorOverlapBlockerAtPosition(candidatePosition))
            {
                IsBlocked = true;
                candidateAmount = OpenAmount;

                if (pushOpenSlightlyWhenBlocked)
                {
                    candidateAmount = Mathf.MoveTowards(
                        OpenAmount,
                        1f,
                        blockedOpenBackSpeed * Time.fixedDeltaTime
                    );
                }

                if (!wasBlocked)
                {
                    PlayBlockedSound();
                }
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

        wasMoving = moving;
        wasBlocked = IsBlocked;

        isOpen = OpenAmount >= 0.99f;

        if (targetOpen && OpenAmount > previousAmount)
        {
            hasOpenedOnce = true;
        }
    }

    private bool WouldDoorOverlapBlockerAtPosition(Vector3 candidateDoorPosition)
    {
        if (doorColliders == null || doorColliders.Length == 0)
        {
            doorColliders = GetComponentsInChildren<Collider>();
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
    }

    public void Close()
    {
        if (stayOpenOnceOpened && hasOpenedOnce)
        {
            return;
        }

        targetOpen = false;
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
            $"Blocked: {IsBlocked}";

        GUI.Box(new Rect(350f, 380f, 260f, 140f), text);
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