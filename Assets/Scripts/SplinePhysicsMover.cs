using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

public class SplinePhysicsMover : MonoBehaviour
{
    public enum MoverMode
    {
        Door,
        PingPongPlatform,
        OneShotPlatform
    }

    public enum SpeedMode
    {
        MetersPerSecond,
        Normalized
    }

    [Header("Target")]
    [SerializeField] private Rigidbody targetRigidbody;

    [Header("Spline")]
    [SerializeField] private SplineContainer spline;
    [SerializeField] private int splineIndex = 0;

    [Header("Scene Editing")]
    [SerializeField] private bool showSplineEditHandles = true;
    [SerializeField] private bool showSplinePreviewLine = true;

    [Range(0f, 1f)]
    [SerializeField] private float editorPreviewT = 0f;

    [Header("Mode")]
    [SerializeField] private MoverMode mode = MoverMode.PingPongPlatform;

    [Header("Movement")]
    [SerializeField] private SpeedMode speedMode = SpeedMode.MetersPerSecond;

    [Tooltip("MetersPerSecond = Unity units per second. Normalized = full spline per second.")]
    [SerializeField] private float moveSpeed = 2f;

    [Range(0f, 1f)]
    [SerializeField] private float startT = 0f;

    [SerializeField] private bool playOnStart = true;

    [Header("Door Settings")]
    [SerializeField] private bool startsOpen = false;

    [Range(0f, 1f)]
    [SerializeField] private float closedT = 0f;

    [Range(0f, 1f)]
    [SerializeField] private float openT = 1f;

    [Header("Platform Settings")]
    [SerializeField] private bool startForward = true;
    [SerializeField] private float waitAtEnds = 0f;

    [Header("Rotation")]
    [SerializeField] private bool rotateAlongSpline = false;

    [Tooltip("Use this if your platform/door model faces the wrong way on the spline.")]
    [SerializeField] private Vector3 rotationOffsetEuler;

    [Header("Physics")]
    [SerializeField] private bool setKinematicOnAwake = true;
    [SerializeField] private RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;

    [Header("Length Sampling")]
    [Tooltip("Higher values make meters-per-second speed more accurate on curved splines.")]
    [SerializeField] private int lengthSamples = 64;

    private float currentT;
    private float targetT;
    private float direction;
    private float waitTimer;

    private bool isMoving;
    private bool isOpen;

    public bool IsOpen => isOpen;
    public bool IsMoving => isMoving;

    public float CurrentT => Application.isPlaying ? currentT : editorPreviewT;

    public SplineContainer Spline => spline;
    public int SplineIndex => splineIndex;

    public bool ShowSplineEditHandles => showSplineEditHandles;
    public bool ShowSplinePreviewLine => showSplinePreviewLine;

    public float EditorPreviewT => editorPreviewT;
    public float MoveSpeed => moveSpeed;
    public SpeedMode CurrentSpeedMode => speedMode;

    public float StartT => startT;
    public float ClosedT => closedT;
    public float OpenT => openT;

    public bool RotateAlongSpline => rotateAlongSpline;

    public Rigidbody TargetRigidbody => targetRigidbody;

    public Transform TargetTransform
    {
        get
        {
            if (targetRigidbody != null)
                return targetRigidbody.transform;

            return transform;
        }
    }

    private void Reset()
    {
        AutoFindReferences();
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);
        waitAtEnds = Mathf.Max(0f, waitAtEnds);
        lengthSamples = Mathf.Max(4, lengthSamples);

        AutoFindReferences();
    }

    private void Awake()
    {
        AutoFindReferences();

        if (targetRigidbody != null)
        {
            if (setKinematicOnAwake)
                targetRigidbody.isKinematic = true;

            targetRigidbody.interpolation = interpolation;
        }

        direction = startForward ? 1f : -1f;

        if (mode == MoverMode.Door)
        {
            isOpen = startsOpen;
            currentT = isOpen ? openT : closedT;
            targetT = currentT;
            isMoving = false;
        }
        else
        {
            currentT = Mathf.Clamp01(startT);
            targetT = startForward ? 1f : 0f;
            isMoving = playOnStart;
        }

        MoveToSplinePositionRuntime(currentT);
    }

    private void FixedUpdate()
    {
        if (spline == null)
            return;

        switch (mode)
        {
            case MoverMode.Door:
                UpdateDoor();
                break;

            case MoverMode.PingPongPlatform:
                UpdatePingPongPlatform();
                break;

            case MoverMode.OneShotPlatform:
                UpdateOneShotPlatform();
                break;
        }

        MoveToSplinePositionRuntime(currentT);
    }

    private void UpdateDoor()
    {
        if (!isMoving)
            return;

        currentT = Mathf.MoveTowards(
            currentT,
            targetT,
            GetDeltaT(Time.fixedDeltaTime, currentT, targetT)
        );

        if (Mathf.Approximately(currentT, targetT))
        {
            currentT = targetT;
            isMoving = false;
        }
    }

    private void UpdatePingPongPlatform()
    {
        if (!isMoving)
            return;

        if (waitTimer > 0f)
        {
            waitTimer -= Time.fixedDeltaTime;
            return;
        }

        float targetEnd = direction > 0f ? 1f : 0f;

        currentT += direction * GetDeltaT(Time.fixedDeltaTime, currentT, targetEnd);
        currentT = Mathf.Clamp01(currentT);

        if (currentT >= 1f)
        {
            currentT = 1f;
            direction = -1f;
            waitTimer = waitAtEnds;
        }
        else if (currentT <= 0f)
        {
            currentT = 0f;
            direction = 1f;
            waitTimer = waitAtEnds;
        }
    }

    private void UpdateOneShotPlatform()
    {
        if (!isMoving)
            return;

        currentT = Mathf.MoveTowards(
            currentT,
            targetT,
            GetDeltaT(Time.fixedDeltaTime, currentT, targetT)
        );

        if (Mathf.Approximately(currentT, targetT))
        {
            currentT = targetT;
            isMoving = false;
        }
    }

    public float GetDeltaT(float deltaTime, float fromT, float toT)
    {
        if (moveSpeed <= 0f)
            return 0f;

        if (speedMode == SpeedMode.Normalized)
            return moveSpeed * deltaTime;

        float length = EstimateSplineLengthBetween(fromT, toT);

        if (length <= 0.0001f)
            return 0f;

        return (moveSpeed / length) * Mathf.Abs(toT - fromT) * deltaTime;
    }

    public float GetEditorDeltaT(float deltaTime, float fromT, float toT)
    {
        return GetDeltaT(deltaTime, fromT, toT);
    }

    public float EstimateFullSplineLength()
    {
        return EstimateSplineLengthBetween(0f, 1f);
    }

    public float EstimateSplineLengthBetween(float fromT, float toT)
    {
        if (spline == null)
            return 0f;

        fromT = Mathf.Clamp01(fromT);
        toT = Mathf.Clamp01(toT);

        if (Mathf.Approximately(fromT, toT))
            return 0f;

        float start = Mathf.Min(fromT, toT);
        float end = Mathf.Max(fromT, toT);

        int samples = Mathf.Max(4, lengthSamples);
        float length = 0f;

        Vector3 previous = GetSplineWorldPosition(start);

        for (int i = 1; i <= samples; i++)
        {
            float lerp = i / (float)samples;
            float t = Mathf.Lerp(start, end, lerp);

            Vector3 current = GetSplineWorldPosition(t);
            length += Vector3.Distance(previous, current);

            previous = current;
        }

        return length;
    }

    private void MoveToSplinePositionRuntime(float t)
    {
        if (!TryGetSplinePose(t, out Vector3 position, out Quaternion rotation))
            return;

        if (targetRigidbody != null)
        {
            targetRigidbody.MovePosition(position);

            if (rotateAlongSpline)
                targetRigidbody.MoveRotation(rotation);
        }
        else
        {
            TargetTransform.position = position;

            if (rotateAlongSpline)
                TargetTransform.rotation = rotation;
        }
    }

    private void MoveToSplinePositionTransform(float t)
    {
        if (!TryGetSplinePose(t, out Vector3 position, out Quaternion rotation))
            return;

        TargetTransform.position = position;

        if (rotateAlongSpline)
            TargetTransform.rotation = rotation;
    }

    public void PreviewInEditor(float t)
    {
        if (Application.isPlaying)
            return;

        editorPreviewT = Mathf.Clamp01(t);
        MoveToSplinePositionTransform(editorPreviewT);
    }

    public void PreviewStart()
    {
        PreviewInEditor(0f);
    }

    public void PreviewEnd()
    {
        PreviewInEditor(1f);
    }

    public void PreviewClosed()
    {
        PreviewInEditor(closedT);
    }

    public void PreviewOpen()
    {
        PreviewInEditor(openT);
    }

    public bool TryGetSplinePose(float t, out Vector3 position, out Quaternion rotation)
    {
        position = transform.position;
        rotation = transform.rotation;

        if (spline == null)
            return false;

        bool valid = spline.Evaluate(
            splineIndex,
            Mathf.Clamp01(t),
            out float3 splinePosition,
            out float3 tangent,
            out float3 upVector
        );

        if (!valid)
            return false;

        position = (Vector3)splinePosition;
        rotation = TargetTransform.rotation;

        if (rotateAlongSpline && math.lengthsq(tangent) > 0.0001f)
        {
            Quaternion splineRotation = Quaternion.LookRotation(
                math.normalize(tangent),
                math.normalize(upVector)
            );

            rotation = splineRotation * Quaternion.Euler(rotationOffsetEuler);
        }

        return true;
    }

    public Vector3 GetSplineWorldPosition(float t)
    {
        if (TryGetSplinePose(t, out Vector3 position, out Quaternion rotation))
            return position;

        return transform.position;
    }

    private void AutoFindReferences()
    {
        if (spline == null)
            spline = GetComponentInChildren<SplineContainer>();

        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponentInChildren<Rigidbody>();

            if (targetRigidbody == null)
                targetRigidbody = GetComponent<Rigidbody>();
        }
    }

    // -------------------------
    // Door API
    // -------------------------

    public void Open()
    {
        mode = MoverMode.Door;
        isOpen = true;
        targetT = openT;
        isMoving = true;
    }

    public void Close()
    {
        mode = MoverMode.Door;
        isOpen = false;
        targetT = closedT;
        isMoving = true;
    }

    public void Toggle()
    {
        if (isOpen)
            Close();
        else
            Open();
    }

    public void SetOpen(bool open)
    {
        if (open)
            Open();
        else
            Close();
    }

    // -------------------------
    // Platform API
    // -------------------------

    public void StartPlatform()
    {
        isMoving = true;
    }

    public void StopPlatform()
    {
        isMoving = false;
    }

    public void TogglePlatformMoving()
    {
        isMoving = !isMoving;
    }

    public void ReverseDirection()
    {
        direction *= -1f;
    }

    public void MoveToStart()
    {
        mode = MoverMode.OneShotPlatform;
        targetT = 0f;
        isMoving = true;
    }

    public void MoveToEnd()
    {
        mode = MoverMode.OneShotPlatform;
        targetT = 1f;
        isMoving = true;
    }

    public void MoveTo(float normalizedT)
    {
        mode = MoverMode.OneShotPlatform;
        targetT = Mathf.Clamp01(normalizedT);
        isMoving = true;
    }
}