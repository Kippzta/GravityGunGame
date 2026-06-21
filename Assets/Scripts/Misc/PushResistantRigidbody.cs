using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class ControlledPushableObject : MonoBehaviour
{
    [Header("Player Detection")]
    [Tooltip("Layers that count as the player.")]
    [SerializeField] private LayerMask playerLayers;

    [Tooltip("If true, also detects CoopFirstPersonController on the colliding object.")]
    [SerializeField] private bool detectPlayerController = true;

    [Header("Push Feel")]
    [Tooltip("Maximum horizontal speed the player can push this object at.")]
    [SerializeField] private float maxPlayerPushSpeed = 0.8f;

    [Tooltip("How much horizontal speed is allowed instantly when the player starts pushing.")]
    [SerializeField] private float initialPushSpeed = 0.15f;

    [Tooltip("How quickly the object ramps from initial push speed to max push speed while the player keeps pushing.")]
    [SerializeField] private float pushRampSpeed = 1.2f;

    [Tooltip("How quickly push permission fades after the player stops touching the object.")]
    [SerializeField] private float pushMemoryFadeSpeed = 4f;

    [Tooltip("How much horizontal velocity caused by player contact is resisted.")]
    [Range(0f, 1f)]
    [SerializeField] private float playerPushResistance = 0.75f;

    [Header("General Weight")]
    [Tooltip("Optional mass override. Set to 0 to keep the Rigidbody mass.")]
    [SerializeField] private float overrideMass = 120f;

    [Tooltip("Extra damping while grounded.")]
    [SerializeField] private float groundedLinearDamping = 2f;

    [Tooltip("Damping while airborne.")]
    [SerializeField] private float airborneLinearDamping = 0.2f;

    [Tooltip("Angular damping.")]
    [SerializeField] private float angularDamping = 5f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayers = ~0;
    [SerializeField] private float groundCheckExtraDistance = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool showDebugPanel = false;

    private Rigidbody rb;
    private Collider objectCollider;

    private bool touchingPlayer;
    private bool grounded;

    private float allowedPushSpeed;
    private Vector3 lastPlayerPushDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        objectCollider = GetComponent<Collider>();

        if (overrideMass > 0f)
        {
            rb.mass = overrideMass;
        }

        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.angularDamping = angularDamping;
    }

    private void FixedUpdate()
    {
        grounded = IsGrounded();

        rb.linearDamping = grounded ? groundedLinearDamping : airborneLinearDamping;
        rb.angularDamping = angularDamping;

        if (touchingPlayer)
        {
            allowedPushSpeed = Mathf.MoveTowards(
                allowedPushSpeed,
                maxPlayerPushSpeed,
                pushRampSpeed * Time.fixedDeltaTime
            );

            LimitPlayerPushVelocity();
        }
        else
        {
            allowedPushSpeed = Mathf.MoveTowards(
                allowedPushSpeed,
                0f,
                pushMemoryFadeSpeed * Time.fixedDeltaTime
            );
        }

        touchingPlayer = false;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (!IsPlayerCollision(collision))
        {
            return;
        }

        touchingPlayer = true;

        Vector3 pushDirection = GetPushDirectionFromCollision(collision);

        if (pushDirection.sqrMagnitude > 0.001f)
        {
            lastPlayerPushDirection = pushDirection.normalized;
        }

        if (allowedPushSpeed <= 0.01f)
        {
            allowedPushSpeed = initialPushSpeed;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsPlayerCollision(collision))
        {
            return;
        }

        touchingPlayer = true;

        Vector3 pushDirection = GetPushDirectionFromCollision(collision);

        if (pushDirection.sqrMagnitude > 0.001f)
        {
            lastPlayerPushDirection = pushDirection.normalized;
        }

        allowedPushSpeed = Mathf.Max(allowedPushSpeed, initialPushSpeed);

        LimitPlayerPushVelocity();
    }

    private bool IsPlayerCollision(Collision collision)
    {
        if (collision == null || collision.collider == null)
        {
            return false;
        }

        if (IsLayerInMask(collision.collider.gameObject.layer, playerLayers))
        {
            return true;
        }

        if (collision.rigidbody != null && IsLayerInMask(collision.rigidbody.gameObject.layer, playerLayers))
        {
            return true;
        }

        if (detectPlayerController)
        {
            if (collision.collider.GetComponentInParent<CoopFirstPersonController>() != null)
            {
                return true;
            }

            if (collision.rigidbody != null &&
                collision.rigidbody.GetComponentInParent<CoopFirstPersonController>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private Vector3 GetPushDirectionFromCollision(Collision collision)
    {
        Vector3 averageNormal = Vector3.zero;

        for (int i = 0; i < collision.contactCount; i++)
        {
            averageNormal += collision.GetContact(i).normal;
        }

        if (averageNormal.sqrMagnitude <= 0.001f)
        {
            return Vector3.zero;
        }

        averageNormal.Normalize();

        Vector3 pushDirection = -averageNormal;
        pushDirection.y = 0f;

        return pushDirection;
    }

    private void LimitPlayerPushVelocity()
    {
        Vector3 velocity = rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(
            velocity.x,
            0f,
            velocity.z
        );

        if (horizontalVelocity.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Vector3 pushDirection = lastPlayerPushDirection.sqrMagnitude > 0.001f
            ? lastPlayerPushDirection.normalized
            : horizontalVelocity.normalized;

        float speedAlongPush = Vector3.Dot(horizontalVelocity, pushDirection);
        Vector3 sidewaysVelocity = horizontalVelocity - pushDirection * speedAlongPush;

        float allowedSpeed = Mathf.Clamp(
            allowedPushSpeed,
            0f,
            maxPlayerPushSpeed
        );

        if (speedAlongPush > allowedSpeed)
        {
            speedAlongPush = Mathf.Lerp(
                speedAlongPush,
                allowedSpeed,
                playerPushResistance
            );
        }

        sidewaysVelocity = Vector3.Lerp(
            sidewaysVelocity,
            Vector3.zero,
            playerPushResistance
        );

        Vector3 limitedHorizontalVelocity =
            pushDirection * Mathf.Max(0f, speedAlongPush) +
            sidewaysVelocity;

        rb.linearVelocity = new Vector3(
            limitedHorizontalVelocity.x,
            velocity.y,
            limitedHorizontalVelocity.z
        );
    }

    private bool IsGrounded()
    {
        Bounds bounds = objectCollider.bounds;

        Vector3 origin = bounds.center;
        float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.8f;
        float distance = bounds.extents.y + groundCheckExtraDistance;

        return Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out _,
            distance,
            groundLayers,
            QueryTriggerInteraction.Ignore
        );
    }

    private bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void OnGUI()
    {
        if (!showDebugPanel)
        {
            return;
        }

        Vector3 horizontalVelocity = new Vector3(
            rb.linearVelocity.x,
            0f,
            rb.linearVelocity.z
        );

        string text =
            $"Controlled Pushable\n" +
            $"Name: {name}\n" +
            $"Mass: {rb.mass:F1}\n" +
            $"Grounded: {grounded}\n" +
            $"Touching Player: {touchingPlayer}\n" +
            $"Allowed Push Speed: {allowedPushSpeed:F2}\n" +
            $"Horizontal Speed: {horizontalVelocity.magnitude:F2}";

        GUI.Box(new Rect(20f, 610f, 300f, 155f), text);
    }
}