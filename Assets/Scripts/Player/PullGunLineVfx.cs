using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PullGunLineVFX : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The PullGun script this line should follow.")]
    [SerializeField] private PullGun pullGun;

    [Tooltip("Optional muzzle/start point. If empty, the line starts from PullGun.GetVisualTetherStartPoint().")]
    [SerializeField] private Transform muzzlePoint;

    [Header("Line Shape")]
    [Tooltip("How many points the line uses. 2 is perfectly straight. Higher values allow sag/noise.")]
    [SerializeField] private int linePoints = 16;

    [Tooltip("Adds a slight sag to the rope. Keep 0 for a perfectly straight laser-like tether.")]
    [SerializeField] private float ropeSag = 0.05f;

    [Tooltip("Adds subtle wave/noise to the rope while active.")]
    [SerializeField] private float ropeWaveAmount = 0.025f;

    [Tooltip("Speed of the rope wave animation.")]
    [SerializeField] private float ropeWaveSpeed = 12f;

    [Header("Width")]
    [Tooltip("Line width while tether is active.")]
    [SerializeField] private float activeWidth = 0.045f;

    [Tooltip("How quickly the line appears/disappears.")]
    [SerializeField] private float visibilityLerpSpeed = 20f;

    [Header("Material Optional")]
    [Tooltip("Optional material assigned to the LineRenderer at runtime.")]
    [SerializeField] private Material lineMaterial;

    [Tooltip("Color of the tether line.")]
    [SerializeField] private Color lineColor = Color.cyan;

    private LineRenderer lineRenderer;
    private float visibleAmount;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();

        if (pullGun == null)
        {
            pullGun = GetComponentInParent<PullGun>();
        }

        lineRenderer.useWorldSpace = true;
        lineRenderer.positionCount = Mathf.Max(2, linePoints);
        lineRenderer.enabled = false;

        lineRenderer.startWidth = activeWidth;
        lineRenderer.endWidth = activeWidth;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;

        if (lineMaterial != null)
        {
            lineRenderer.material = lineMaterial;
        }
    }

    private void LateUpdate()
    {
        if (pullGun == null)
        {
            HideLineImmediately();
            return;
        }

        bool shouldShow = pullGun.IsTetherVisuallyActive;

        float targetVisibleAmount = shouldShow ? 1f : 0f;

        visibleAmount = Mathf.Lerp(
            visibleAmount,
            targetVisibleAmount,
            1f - Mathf.Exp(-visibilityLerpSpeed * Time.deltaTime)
        );

        if (visibleAmount <= 0.001f)
        {
            lineRenderer.enabled = false;
            return;
        }

        lineRenderer.enabled = true;

        Vector3 start = muzzlePoint != null
            ? muzzlePoint.position
            : pullGun.GetVisualTetherStartPoint();

        Vector3 end = pullGun.GetVisualTetherEndPoint();

        UpdateLinePoints(start, end);

        float width = activeWidth * visibleAmount;
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width;

        Color visibleColor = lineColor;
        visibleColor.a *= visibleAmount;

        lineRenderer.startColor = visibleColor;
        lineRenderer.endColor = visibleColor;
    }

    private void UpdateLinePoints(Vector3 start, Vector3 end)
    {
        int points = Mathf.Max(2, linePoints);

        if (lineRenderer.positionCount != points)
        {
            lineRenderer.positionCount = points;
        }

        Vector3 direction = end - start;
        Vector3 side = Vector3.Cross(direction.normalized, Vector3.up);

        if (side.sqrMagnitude < 0.001f)
        {
            side = Vector3.Cross(direction.normalized, Vector3.right);
        }

        side.Normalize();

        for (int i = 0; i < points; i++)
        {
            float t = i / (float)(points - 1);

            Vector3 point = Vector3.Lerp(start, end, t);

            float sagCurve = Mathf.Sin(t * Mathf.PI);
            point += Vector3.down * ropeSag * sagCurve;

            float wave = Mathf.Sin(Time.time * ropeWaveSpeed + t * Mathf.PI * 4f);
            point += side * wave * ropeWaveAmount * sagCurve;

            lineRenderer.SetPosition(i, point);
        }
    }

    private void HideLineImmediately()
    {
        visibleAmount = 0f;

        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }
}