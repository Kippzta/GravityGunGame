using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PullableObject : MonoBehaviour
{
    [Header("Pull Gun Settings")]
    [Tooltip("If enabled, this mass value is applied to the Rigidbody on Awake.")]
    [SerializeField] private bool overrideRigidbodyMass = false;

    [SerializeField] private float objectMass = 20f;

    [Header("Physics Quality")]
    [SerializeField] private bool useContinuousCollision = true;
    [SerializeField] private bool useInterpolation = true;

    private Rigidbody rb;

    public Rigidbody Rigidbody => rb;
    public float Mass => rb != null ? rb.mass : objectMass;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (overrideRigidbodyMass)
        {
            rb.mass = objectMass;
        }

        if (useContinuousCollision)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        if (useInterpolation)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
    }
}