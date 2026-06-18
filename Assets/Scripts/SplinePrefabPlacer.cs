using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class SplinePrefabPlacer : MonoBehaviour
{
    public enum SpacingMode
    {
        Manual,
        AutoFromPrefabBounds
    }

    public enum LengthAxis
    {
        X,
        Y,
        Z
    }

    public enum PlacementMode
    {
        Points,
        ConnectedSegments
    }

    [Header("Spline")]
    public bool autoAssignSplineContainer = true;
    public SplineContainer splineContainer;
    public int splineIndex = 0;

    [Header("Prefab")]
    public GameObject prefab;

    [Tooltip("Places generated objects under this transform. If empty, this GameObject is used.")]
    public Transform generatedParent;

    [Header("Auto Regenerate")]
    [Tooltip("Automatically rebuilds generated objects when settings change in the editor.")]
    public bool autoRegenerateInEditor = true;

    [Tooltip("Automatically regenerate after shift-click spline edits.")]
    public bool regenerateAfterSplineEdit = true;

    [Tooltip("Automatically regenerate when existing spline points, tangents, or spline transform are moved.")]
    public bool regenerateWhenSplineChanges = true;

    [Tooltip("Useful when changing many settings. When disabled, use the Regenerate button manually.")]
    public bool allowRegenerateFromOnValidate = true;

    [Header("Placement")]
    public PlacementMode placementMode = PlacementMode.ConnectedSegments;
    public SpacingMode spacingMode = SpacingMode.Manual;

    [Min(0.01f)]
    public float manualSpacing = 2f;

    [Tooltip("Multiplier used when spacing is calculated from prefab bounds.")]
    [Min(0.01f)]
    public float autoSpacingMultiplier = 1f;

    [Tooltip("When enabled, the largest local bounds axis of the prefab is used as the prefab length axis.")]
    public bool autoDetectPrefabLengthAxis = true;

    public LengthAxis prefabLengthAxis = LengthAxis.Z;

    [Header("Stretch / Compression")]
    [Tooltip("Scales each generated piece along its length axis so neighbouring pieces connect.")]
    public bool stretchToConnect = true;

    [Tooltip("Prevents extremely tiny or huge scale values when stretchToConnect is enabled.")]
    public Vector2 stretchScaleClamp = new Vector2(0.25f, 4f);

    [Header("Pivot Fix")]
    [Tooltip("Uses a wrapper object and offsets the prefab child so bounds are centered on the spline segment. This fixes prefabs stretching from an awkward pivot.")]
    public bool fixPivotUsingBounds = true;

    [Tooltip("Extra local-space offset applied to the prefab child after the automatic pivot fix.")]
    public Vector3 additionalLocalOffset;

    [Header("Rotation")]
    [Tooltip("Align generated pieces to the spline tangent.")]
    public bool alignToSplineTangent = true;

    [Tooltip("Extra rotation applied to the prefab child, useful when your prefab faces the wrong way.")]
    public Vector3 prefabRotationOffsetEuler;

    [Header("Surface Alignment")]
    public bool alignToSurfaceNormal = true;
    public bool snapToSurface = true;

    [Tooltip("Layers used when raycasting for surface normal.")]
    public LayerMask surfaceMask = ~0;

    [Tooltip("Raycast starts this far above the spline point.")]
    public float surfaceRayHeight = 10f;

    [Tooltip("Raycast length below the start point.")]
    public float surfaceRayDistance = 50f;

    [Tooltip("Fallback up direction when no surface is hit.")]
    public Vector3 fallbackUp = Vector3.up;

    [Header("Spline Sampling")]
    [Tooltip("Higher values improve distance accuracy along curved splines.")]
    [Range(32, 2048)]
    public int distanceSamples = 256;

    [Header("Generated Naming")]
    public string generatedRootName = "_Generated Spline Prefabs";
    public string itemNamePrefix = "Spline Piece";

    private const string GeneratedMarkerName = "__SplinePrefabPlacerGenerated";

#if UNITY_EDITOR
    private bool regenerationQueued;
#endif

    [Serializable]
    private struct DistanceSample
    {
        public float t;
        public float distance;
        public Vector3 position;
    }

    private void Reset()
    {
        TryAutoAssignSplineContainer();
        ClampSettings();
    }

    private void Awake()
    {
        TryAutoAssignSplineContainer();
    }

    private void OnEnable()
    {
        TryAutoAssignSplineContainer();
    }

    private void OnValidate()
    {
        TryAutoAssignSplineContainer();
        ClampSettings();

#if UNITY_EDITOR
        if (!Application.isPlaying && autoRegenerateInEditor && allowRegenerateFromOnValidate)
        {
            QueueRegenerate();
        }
#endif
    }

    public void TryAutoAssignSplineContainer()
    {
        if (!autoAssignSplineContainer)
            return;

        if (splineContainer != null)
            return;

        splineContainer = GetComponent<SplineContainer>();

        if (splineContainer != null)
            return;

        splineContainer = GetComponentInParent<SplineContainer>();

        if (splineContainer != null)
            return;

        splineContainer = GetComponentInChildren<SplineContainer>();
    }

    public void ClampSettings()
    {
        manualSpacing = Mathf.Max(0.01f, manualSpacing);
        autoSpacingMultiplier = Mathf.Max(0.01f, autoSpacingMultiplier);
        distanceSamples = Mathf.Clamp(distanceSamples, 32, 2048);

        surfaceRayHeight = Mathf.Max(0f, surfaceRayHeight);
        surfaceRayDistance = Mathf.Max(0.01f, surfaceRayDistance);

        if (stretchScaleClamp.x < 0.001f)
            stretchScaleClamp.x = 0.001f;

        if (stretchScaleClamp.y < stretchScaleClamp.x)
            stretchScaleClamp.y = stretchScaleClamp.x;

        if (fallbackUp.sqrMagnitude < 0.0001f)
            fallbackUp = Vector3.up;

        if (string.IsNullOrWhiteSpace(generatedRootName))
            generatedRootName = "_Generated Spline Prefabs";

        if (string.IsNullOrWhiteSpace(itemNamePrefix))
            itemNamePrefix = "Spline Piece";

        if (splineContainer != null && splineContainer.Splines != null && splineContainer.Splines.Count > 0)
            splineIndex = Mathf.Clamp(splineIndex, 0, splineContainer.Splines.Count - 1);
        else
            splineIndex = 0;
    }

    public int GetSplineSignature()
    {
        TryAutoAssignSplineContainer();

        unchecked
        {
            int hash = 17;

            if (splineContainer == null)
                return hash;

            Transform t = splineContainer.transform;

            hash = hash * 31 + t.position.GetHashCode();
            hash = hash * 31 + t.rotation.GetHashCode();
            hash = hash * 31 + t.lossyScale.GetHashCode();

            if (splineContainer.Splines == null)
                return hash;

            hash = hash * 31 + splineContainer.Splines.Count.GetHashCode();
            hash = hash * 31 + splineIndex.GetHashCode();

            if (splineContainer.Splines.Count == 0)
                return hash;

            int safeSplineIndex = Mathf.Clamp(splineIndex, 0, splineContainer.Splines.Count - 1);
            Spline spline = splineContainer.Splines[safeSplineIndex];

            hash = hash * 31 + spline.Count.GetHashCode();
            hash = hash * 31 + spline.Closed.GetHashCode();

            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];

                hash = hash * 31 + knot.Position.GetHashCode();
                hash = hash * 31 + knot.TangentIn.GetHashCode();
                hash = hash * 31 + knot.TangentOut.GetHashCode();
                hash = hash * 31 + knot.Rotation.GetHashCode();
            }

            return hash;
        }
    }

#if UNITY_EDITOR
    public void QueueRegenerate()
    {
        if (regenerationQueued)
            return;

        regenerationQueued = true;

        EditorApplication.delayCall += () =>
        {
            regenerationQueued = false;

            if (this == null)
                return;

            if (Application.isPlaying)
                return;

            if (!isActiveAndEnabled)
                return;

            if (prefab == null)
                return;

            Regenerate();
        };
    }
#endif

    public void Regenerate()
    {
        TryAutoAssignSplineContainer();
        ClampSettings();

        ClearGenerated();

        if (splineContainer == null || prefab == null)
            return;

        if (splineContainer.Splines == null || splineContainer.Splines.Count == 0)
            return;

        splineIndex = Mathf.Clamp(splineIndex, 0, splineContainer.Splines.Count - 1);

        List<DistanceSample> table = BuildDistanceTable();
        if (table.Count < 2)
            return;

        float length = table[table.Count - 1].distance;
        if (length <= 0.001f)
            return;

        BoundsInfo bounds = CalculatePrefabBounds(prefab);
        LengthAxis effectiveAxis = autoDetectPrefabLengthAxis ? bounds.longestAxis : prefabLengthAxis;

        float prefabLength = GetAxisSize(bounds.size, effectiveAxis);
        if (prefabLength <= 0.001f)
            prefabLength = manualSpacing;

        float spacing = spacingMode == SpacingMode.Manual
            ? Mathf.Max(0.01f, manualSpacing)
            : Mathf.Max(0.01f, prefabLength * autoSpacingMultiplier);

        Transform root = GetOrCreateGeneratedRoot();

        if (placementMode == PlacementMode.ConnectedSegments)
        {
            GenerateConnectedSegments(root, table, length, spacing, prefabLength, bounds, effectiveAxis);
        }
        else
        {
            GeneratePoints(root, table, length, spacing, bounds, effectiveAxis);
        }
    }

    public void ClearGenerated()
    {
        Transform parent = generatedParent != null ? generatedParent : transform;

        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);

            if (child.name == generatedRootName || child.name.Contains(GeneratedMarkerName))
            {
                DestroyObject(child.gameObject);
            }
        }
    }

    private Transform GetOrCreateGeneratedRoot()
    {
        Transform parent = generatedParent != null ? generatedParent : transform;

        GameObject root = new GameObject(generatedRootName);
        root.name = $"{generatedRootName} {GeneratedMarkerName}";
        root.transform.SetParent(parent, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        return root.transform;
    }

    private void GenerateConnectedSegments(
        Transform root,
        List<DistanceSample> table,
        float totalLength,
        float desiredSpacing,
        float prefabLength,
        BoundsInfo bounds,
        LengthAxis axis)
    {
        int segmentCount = Mathf.Max(1, Mathf.RoundToInt(totalLength / desiredSpacing));
        float segmentLength = totalLength / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            float d0 = i * segmentLength;
            float d1 = (i + 1) * segmentLength;
            float dm = (d0 + d1) * 0.5f;

            float tMid = DistanceToT(table, dm);
            float tA = DistanceToT(table, d0);
            float tB = DistanceToT(table, d1);

            Vector3 posA = EvaluatePosition(tA);
            Vector3 posB = EvaluatePosition(tB);
            Vector3 posMid = EvaluatePosition(tMid);

            Vector3 forward = (posB - posA).normalized;
            if (forward.sqrMagnitude < 0.0001f)
                forward = EvaluateTangent(tMid);

            PlacementPose pose = BuildPose(posMid, forward, tMid);

            float axisScale = 1f;
            if (stretchToConnect && prefabLength > 0.001f)
            {
                axisScale = segmentLength / prefabLength;
                axisScale = Mathf.Clamp(axisScale, stretchScaleClamp.x, stretchScaleClamp.y);
            }

            Vector3 localScale = Vector3.one;
            SetAxisValue(ref localScale, axis, axisScale);

            CreatePiece(root, i, pose.position, pose.rotation, bounds, axis, localScale);
        }
    }

    private void GeneratePoints(
        Transform root,
        List<DistanceSample> table,
        float totalLength,
        float spacing,
        BoundsInfo bounds,
        LengthAxis axis)
    {
        int count = Mathf.Max(1, Mathf.FloorToInt(totalLength / spacing) + 1);

        for (int i = 0; i < count; i++)
        {
            float distance = Mathf.Min(i * spacing, totalLength);
            float t = DistanceToT(table, distance);

            Vector3 position = EvaluatePosition(t);
            Vector3 forward = EvaluateTangent(t);

            PlacementPose pose = BuildPose(position, forward, t);
            CreatePiece(root, i, pose.position, pose.rotation, bounds, axis, Vector3.one);
        }
    }

    private void CreatePiece(
        Transform root,
        int index,
        Vector3 worldPosition,
        Quaternion worldRotation,
        BoundsInfo bounds,
        LengthAxis axis,
        Vector3 childScale)
    {
        GameObject wrapper = new GameObject($"{itemNamePrefix} {index:000}");
        wrapper.transform.SetParent(root, true);
        wrapper.transform.SetPositionAndRotation(worldPosition, worldRotation);
        wrapper.transform.localScale = Vector3.one;

        GameObject instance = InstantiatePrefab(prefab);
        instance.name = prefab.name;
        instance.transform.SetParent(wrapper.transform, false);

        Quaternion axisCorrection = Quaternion.FromToRotation(GetAxisVector(axis), Vector3.forward);
        Quaternion offsetRotation = Quaternion.Euler(prefabRotationOffsetEuler);

        instance.transform.localRotation = axisCorrection * offsetRotation;
        instance.transform.localScale = childScale;

        Vector3 localOffset = additionalLocalOffset;

        if (fixPivotUsingBounds)
        {
            Vector3 scaledCenter = Vector3.Scale(bounds.center, childScale);
            Vector3 correctedCenter = instance.transform.localRotation * scaledCenter;
            localOffset -= correctedCenter;
        }

        instance.transform.localPosition = localOffset;
    }

    private PlacementPose BuildPose(Vector3 position, Vector3 forward, float t)
    {
        Vector3 up = fallbackUp.sqrMagnitude > 0.001f ? fallbackUp.normalized : Vector3.up;

        if (!alignToSplineTangent)
            forward = transform.forward;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        if (TryGetSurface(position, out RaycastHit hit))
        {
            if (snapToSurface)
                position = hit.point;

            if (alignToSurfaceNormal)
                up = hit.normal;
        }
        else if (alignToSurfaceNormal)
        {
            Vector3 splineUp = EvaluateUp(t);
            if (splineUp.sqrMagnitude > 0.0001f)
                up = splineUp.normalized;
        }

        forward = Vector3.ProjectOnPlane(forward, up).normalized;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.Cross(up, Vector3.right).normalized;

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.Cross(up, Vector3.forward).normalized;

        return new PlacementPose
        {
            position = position,
            rotation = Quaternion.LookRotation(forward, up)
        };
    }

    private bool TryGetSurface(Vector3 position, out RaycastHit hit)
    {
        Vector3 up = fallbackUp.sqrMagnitude > 0.001f ? fallbackUp.normalized : Vector3.up;
        Vector3 rayStart = position + up * surfaceRayHeight;
        return Physics.Raycast(rayStart, -up, out hit, surfaceRayHeight + surfaceRayDistance, surfaceMask, QueryTriggerInteraction.Ignore);
    }

    private List<DistanceSample> BuildDistanceTable()
    {
        List<DistanceSample> samples = new List<DistanceSample>(distanceSamples + 1);

        Vector3 previous = EvaluatePosition(0f);
        float distance = 0f;

        samples.Add(new DistanceSample
        {
            t = 0f,
            distance = 0f,
            position = previous
        });

        for (int i = 1; i <= distanceSamples; i++)
        {
            float t = i / (float)distanceSamples;
            Vector3 current = EvaluatePosition(t);
            distance += Vector3.Distance(previous, current);

            samples.Add(new DistanceSample
            {
                t = t,
                distance = distance,
                position = current
            });

            previous = current;
        }

        return samples;
    }

    private float DistanceToT(List<DistanceSample> table, float distance)
    {
        if (distance <= 0f)
            return 0f;

        float total = table[table.Count - 1].distance;
        if (distance >= total)
            return 1f;

        int low = 0;
        int high = table.Count - 1;

        while (low < high)
        {
            int mid = (low + high) / 2;

            if (table[mid].distance < distance)
                low = mid + 1;
            else
                high = mid;
        }

        DistanceSample b = table[low];
        DistanceSample a = table[Mathf.Max(0, low - 1)];

        float range = b.distance - a.distance;
        if (range <= 0.0001f)
            return b.t;

        float lerp = (distance - a.distance) / range;
        return Mathf.Lerp(a.t, b.t, lerp);
    }

    private Vector3 EvaluatePosition(float t)
    {
        if (splineContainer != null && splineContainer.Evaluate(splineIndex, Mathf.Clamp01(t), out float3 pos, out float3 tangent, out float3 up))
            return pos;

        return transform.position;
    }

    private Vector3 EvaluateTangent(float t)
    {
        if (splineContainer != null && splineContainer.Evaluate(splineIndex, Mathf.Clamp01(t), out float3 pos, out float3 tangent, out float3 up))
        {
            Vector3 v = tangent;
            if (v.sqrMagnitude > 0.0001f)
                return v.normalized;
        }

        return transform.forward;
    }

    private Vector3 EvaluateUp(float t)
    {
        if (splineContainer != null && splineContainer.Evaluate(splineIndex, Mathf.Clamp01(t), out float3 pos, out float3 tangent, out float3 up))
        {
            Vector3 v = up;
            if (v.sqrMagnitude > 0.0001f)
                return v.normalized;
        }

        return Vector3.up;
    }

    private static GameObject InstantiatePrefab(GameObject source)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance != null)
                return instance;
        }
#endif

        return Instantiate(source);
    }

    private static void DestroyObject(GameObject obj)
    {
        if (obj == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
#else
        Destroy(obj);
#endif
    }

    private static Vector3 GetAxisVector(LengthAxis axis)
    {
        switch (axis)
        {
            case LengthAxis.X:
                return Vector3.right;

            case LengthAxis.Y:
                return Vector3.up;

            default:
                return Vector3.forward;
        }
    }

    private static float GetAxisSize(Vector3 size, LengthAxis axis)
    {
        switch (axis)
        {
            case LengthAxis.X:
                return size.x;

            case LengthAxis.Y:
                return size.y;

            default:
                return size.z;
        }
    }

    private static void SetAxisValue(ref Vector3 value, LengthAxis axis, float axisValue)
    {
        switch (axis)
        {
            case LengthAxis.X:
                value.x = axisValue;
                break;

            case LengthAxis.Y:
                value.y = axisValue;
                break;

            default:
                value.z = axisValue;
                break;
        }
    }

    private struct PlacementPose
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    private struct BoundsInfo
    {
        public Vector3 center;
        public Vector3 size;
        public LengthAxis longestAxis;
    }

    private static BoundsInfo CalculatePrefabBounds(GameObject root)
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one);

        MeshFilter[] meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter mf in meshFilters)
        {
            if (mf.sharedMesh == null)
                continue;

            EncapsulateLocalBounds(root.transform, mf.transform, mf.sharedMesh.bounds, ref bounds, ref hasBounds);
        }

        SkinnedMeshRenderer[] skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (SkinnedMeshRenderer smr in skinned)
        {
            if (smr.sharedMesh == null)
                continue;

            EncapsulateLocalBounds(root.transform, smr.transform, smr.sharedMesh.bounds, ref bounds, ref hasBounds);
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (Collider col in colliders)
        {
            Bounds localApprox = new Bounds(root.transform.InverseTransformPoint(col.bounds.center), col.bounds.size);

            if (!hasBounds)
            {
                bounds = localApprox;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(localApprox);
            }
        }

        if (!hasBounds)
            bounds = new Bounds(Vector3.zero, Vector3.one);

        Vector3 size = bounds.size;

        LengthAxis longest = LengthAxis.Z;

        if (size.x >= size.y && size.x >= size.z)
            longest = LengthAxis.X;
        else if (size.y >= size.x && size.y >= size.z)
            longest = LengthAxis.Y;

        return new BoundsInfo
        {
            center = bounds.center,
            size = size,
            longestAxis = longest
        };
    }

    private static void EncapsulateLocalBounds(
        Transform root,
        Transform child,
        Bounds childLocalBounds,
        ref Bounds result,
        ref bool hasBounds)
    {
        Matrix4x4 childToRoot = root.worldToLocalMatrix * child.localToWorldMatrix;

        Vector3 min = childLocalBounds.min;
        Vector3 max = childLocalBounds.max;

        Vector3[] corners =
        {
            new Vector3(min.x, min.y, min.z),
            new Vector3(min.x, min.y, max.z),
            new Vector3(min.x, max.y, min.z),
            new Vector3(min.x, max.y, max.z),
            new Vector3(max.x, min.y, min.z),
            new Vector3(max.x, min.y, max.z),
            new Vector3(max.x, max.y, min.z),
            new Vector3(max.x, max.y, max.z)
        };

        foreach (Vector3 corner in corners)
        {
            Vector3 p = childToRoot.MultiplyPoint3x4(corner);

            if (!hasBounds)
            {
                result = new Bounds(p, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(p);
            }
        }
    }
}