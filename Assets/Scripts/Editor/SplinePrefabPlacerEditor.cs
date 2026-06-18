#if UNITY_EDITOR

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

[CustomEditor(typeof(SplinePrefabPlacer))]
public class SplinePrefabPlacerEditor : Editor
{
    private const float InsertScreenDistance = 18f;
    private const int SegmentSamplesPerCurve = 20;

    private static readonly Dictionary<int, int> LastSplineSignatures = new Dictionary<int, int>();

    private SerializedProperty autoAssignSplineContainer;
    private SerializedProperty splineContainer;
    private SerializedProperty splineIndex;

    private SerializedProperty prefab;
    private SerializedProperty generatedParent;

    private SerializedProperty autoRegenerateInEditor;
    private SerializedProperty regenerateAfterSplineEdit;
    private SerializedProperty regenerateWhenSplineChanges;
    private SerializedProperty allowRegenerateFromOnValidate;

    private SerializedProperty placementMode;
    private SerializedProperty spacingMode;
    private SerializedProperty manualSpacing;
    private SerializedProperty autoSpacingMultiplier;
    private SerializedProperty autoDetectPrefabLengthAxis;
    private SerializedProperty prefabLengthAxis;

    private SerializedProperty stretchToConnect;
    private SerializedProperty stretchScaleClamp;

    private SerializedProperty fixPivotUsingBounds;
    private SerializedProperty additionalLocalOffset;

    private SerializedProperty alignToSplineTangent;
    private SerializedProperty prefabRotationOffsetEuler;

    private SerializedProperty alignToSurfaceNormal;
    private SerializedProperty snapToSurface;
    private SerializedProperty surfaceMask;
    private SerializedProperty surfaceRayHeight;
    private SerializedProperty surfaceRayDistance;
    private SerializedProperty fallbackUp;

    private SerializedProperty distanceSamples;

    private SerializedProperty generatedRootName;
    private SerializedProperty itemNamePrefix;

    private bool splineFoldout = true;
    private bool prefabFoldout = true;
    private bool autoRegenerateFoldout = true;
    private bool placementFoldout = true;
    private bool rotationFoldout = false;
    private bool surfaceFoldout = false;
    private bool advancedFoldout = false;
    private bool helpFoldout = false;

    private void OnEnable()
    {
        autoAssignSplineContainer = serializedObject.FindProperty(nameof(SplinePrefabPlacer.autoAssignSplineContainer));
        splineContainer = serializedObject.FindProperty(nameof(SplinePrefabPlacer.splineContainer));
        splineIndex = serializedObject.FindProperty(nameof(SplinePrefabPlacer.splineIndex));

        prefab = serializedObject.FindProperty(nameof(SplinePrefabPlacer.prefab));
        generatedParent = serializedObject.FindProperty(nameof(SplinePrefabPlacer.generatedParent));

        autoRegenerateInEditor = serializedObject.FindProperty(nameof(SplinePrefabPlacer.autoRegenerateInEditor));
        regenerateAfterSplineEdit = serializedObject.FindProperty(nameof(SplinePrefabPlacer.regenerateAfterSplineEdit));
        regenerateWhenSplineChanges = serializedObject.FindProperty(nameof(SplinePrefabPlacer.regenerateWhenSplineChanges));
        allowRegenerateFromOnValidate = serializedObject.FindProperty(nameof(SplinePrefabPlacer.allowRegenerateFromOnValidate));

        placementMode = serializedObject.FindProperty(nameof(SplinePrefabPlacer.placementMode));
        spacingMode = serializedObject.FindProperty(nameof(SplinePrefabPlacer.spacingMode));
        manualSpacing = serializedObject.FindProperty(nameof(SplinePrefabPlacer.manualSpacing));
        autoSpacingMultiplier = serializedObject.FindProperty(nameof(SplinePrefabPlacer.autoSpacingMultiplier));
        autoDetectPrefabLengthAxis = serializedObject.FindProperty(nameof(SplinePrefabPlacer.autoDetectPrefabLengthAxis));
        prefabLengthAxis = serializedObject.FindProperty(nameof(SplinePrefabPlacer.prefabLengthAxis));

        stretchToConnect = serializedObject.FindProperty(nameof(SplinePrefabPlacer.stretchToConnect));
        stretchScaleClamp = serializedObject.FindProperty(nameof(SplinePrefabPlacer.stretchScaleClamp));

        fixPivotUsingBounds = serializedObject.FindProperty(nameof(SplinePrefabPlacer.fixPivotUsingBounds));
        additionalLocalOffset = serializedObject.FindProperty(nameof(SplinePrefabPlacer.additionalLocalOffset));

        alignToSplineTangent = serializedObject.FindProperty(nameof(SplinePrefabPlacer.alignToSplineTangent));
        prefabRotationOffsetEuler = serializedObject.FindProperty(nameof(SplinePrefabPlacer.prefabRotationOffsetEuler));

        alignToSurfaceNormal = serializedObject.FindProperty(nameof(SplinePrefabPlacer.alignToSurfaceNormal));
        snapToSurface = serializedObject.FindProperty(nameof(SplinePrefabPlacer.snapToSurface));
        surfaceMask = serializedObject.FindProperty(nameof(SplinePrefabPlacer.surfaceMask));
        surfaceRayHeight = serializedObject.FindProperty(nameof(SplinePrefabPlacer.surfaceRayHeight));
        surfaceRayDistance = serializedObject.FindProperty(nameof(SplinePrefabPlacer.surfaceRayDistance));
        fallbackUp = serializedObject.FindProperty(nameof(SplinePrefabPlacer.fallbackUp));

        distanceSamples = serializedObject.FindProperty(nameof(SplinePrefabPlacer.distanceSamples));

        generatedRootName = serializedObject.FindProperty(nameof(SplinePrefabPlacer.generatedRootName));
        itemNamePrefix = serializedObject.FindProperty(nameof(SplinePrefabPlacer.itemNamePrefix));

        EditorApplication.update += EditorUpdate;

        SplinePrefabPlacer placer = (SplinePrefabPlacer)target;
        if (placer != null)
        {
            placer.TryAutoAssignSplineContainer();
            LastSplineSignatures[placer.GetInstanceID()] = placer.GetSplineSignature();
        }
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;

        if (target != null)
        {
            LastSplineSignatures.Remove(target.GetInstanceID());
        }
    }

    private void EditorUpdate()
    {
        SplinePrefabPlacer placer = target as SplinePrefabPlacer;

        if (placer == null)
            return;

        if (Application.isPlaying)
            return;

        if (!placer.autoRegenerateInEditor)
            return;

        if (!placer.regenerateWhenSplineChanges)
            return;

        if (placer.prefab == null)
            return;

        placer.TryAutoAssignSplineContainer();

        if (placer.splineContainer == null)
            return;

        int id = placer.GetInstanceID();
        int currentSignature = placer.GetSplineSignature();

        if (!LastSplineSignatures.TryGetValue(id, out int previousSignature))
        {
            LastSplineSignatures[id] = currentSignature;
            return;
        }

        if (currentSignature == previousSignature)
            return;

        LastSplineSignatures[id] = currentSignature;

        placer.QueueRegenerate();
        SceneView.RepaintAll();
    }

    public override void OnInspectorGUI()
    {
        SplinePrefabPlacer placer = (SplinePrefabPlacer)target;

        serializedObject.Update();

        DrawTopButtons(placer);

        EditorGUILayout.Space(8);

        EditorGUI.BeginChangeCheck();

        DrawSplineSection(placer);
        DrawPrefabSection();
        DrawAutoRegenerateSection();
        DrawPlacementSection();
        DrawRotationSection();
        DrawSurfaceSection();
        DrawAdvancedSection();
        DrawHelpSection();

        bool changed = EditorGUI.EndChangeCheck();

        serializedObject.ApplyModifiedProperties();

        if (changed)
        {
            placer.TryAutoAssignSplineContainer();
            placer.ClampSettings();
            EditorUtility.SetDirty(placer);

            LastSplineSignatures[placer.GetInstanceID()] = placer.GetSplineSignature();

            if (!Application.isPlaying && placer.autoRegenerateInEditor && placer.allowRegenerateFromOnValidate)
            {
                placer.QueueRegenerate();
            }
        }
    }

    private void DrawTopButtons(SplinePrefabPlacer placer)
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Spline Prefab Placer", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Regenerate", GUILayout.Height(28)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(placer.gameObject, "Regenerate Spline Prefabs");
                    placer.Regenerate();
                    LastSplineSignatures[placer.GetInstanceID()] = placer.GetSplineSignature();
                    EditorUtility.SetDirty(placer);
                }

                if (GUILayout.Button("Clear", GUILayout.Height(28)))
                {
                    Undo.RegisterFullObjectHierarchyUndo(placer.gameObject, "Clear Spline Prefabs");
                    placer.ClearGenerated();
                    LastSplineSignatures[placer.GetInstanceID()] = placer.GetSplineSignature();
                    EditorUtility.SetDirty(placer);
                }
            }

            if (placer.splineContainer == null)
            {
                EditorGUILayout.HelpBox("No SplineContainer assigned. Enable Auto Assign or assign one manually.", MessageType.Warning);

                if (GUILayout.Button("Find SplineContainer"))
                {
                    Undo.RecordObject(placer, "Find SplineContainer");
                    placer.splineContainer = null;
                    placer.TryAutoAssignSplineContainer();
                    LastSplineSignatures[placer.GetInstanceID()] = placer.GetSplineSignature();
                    EditorUtility.SetDirty(placer);
                }
            }

            if (placer.prefab == null)
            {
                EditorGUILayout.HelpBox("Assign a prefab before generating.", MessageType.Info);
            }
        }
    }

    private void DrawSplineSection(SplinePrefabPlacer placer)
    {
        splineFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(splineFoldout, "Spline");

        if (splineFoldout)
        {
            EditorGUILayout.PropertyField(autoAssignSplineContainer);

            using (new EditorGUI.DisabledScope(autoAssignSplineContainer.boolValue))
            {
                EditorGUILayout.PropertyField(splineContainer);
            }

            if (autoAssignSplineContainer.boolValue)
            {
                EditorGUILayout.HelpBox("SplineContainer is automatically found on this GameObject, then parent, then children.", MessageType.None);
            }

            EditorGUILayout.PropertyField(splineIndex);

            if (placer.splineContainer != null && placer.splineContainer.Splines != null)
            {
                EditorGUILayout.LabelField("Spline Count", placer.splineContainer.Splines.Count.ToString());
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPrefabSection()
    {
        prefabFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(prefabFoldout, "Prefab");

        if (prefabFoldout)
        {
            EditorGUILayout.PropertyField(prefab);
            EditorGUILayout.PropertyField(generatedParent);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(autoDetectPrefabLengthAxis);

            using (new EditorGUI.DisabledScope(autoDetectPrefabLengthAxis.boolValue))
            {
                EditorGUILayout.PropertyField(prefabLengthAxis);
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawAutoRegenerateSection()
    {
        autoRegenerateFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(autoRegenerateFoldout, "Auto Regenerate");

        if (autoRegenerateFoldout)
        {
            EditorGUILayout.PropertyField(autoRegenerateInEditor);
            EditorGUILayout.PropertyField(regenerateAfterSplineEdit);
            EditorGUILayout.PropertyField(regenerateWhenSplineChanges);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(allowRegenerateFromOnValidate);

            EditorGUILayout.HelpBox(
                "Auto Regenerate rebuilds the generated objects when settings change, when you Shift-click edit the spline, and when existing spline points are moved.",
                MessageType.None);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPlacementSection()
    {
        placementFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(placementFoldout, "Placement, Spacing & Stretch");

        if (placementFoldout)
        {
            EditorGUILayout.PropertyField(placementMode);
            EditorGUILayout.PropertyField(spacingMode);

            SplinePrefabPlacer.SpacingMode mode = (SplinePrefabPlacer.SpacingMode)spacingMode.enumValueIndex;

            if (mode == SplinePrefabPlacer.SpacingMode.Manual)
            {
                EditorGUILayout.PropertyField(manualSpacing);
            }
            else
            {
                EditorGUILayout.PropertyField(autoSpacingMultiplier);
            }

            EditorGUILayout.Space(6);

            EditorGUILayout.PropertyField(stretchToConnect);

            using (new EditorGUI.DisabledScope(!stretchToConnect.boolValue))
            {
                EditorGUILayout.PropertyField(stretchScaleClamp);
            }

            EditorGUILayout.Space(6);

            EditorGUILayout.PropertyField(fixPivotUsingBounds);
            EditorGUILayout.PropertyField(additionalLocalOffset);

            EditorGUILayout.HelpBox(
                "Connected Segments is best for fences. It divides the spline into pieces and optionally stretches each prefab so the fence connects cleanly.",
                MessageType.None);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawRotationSection()
    {
        rotationFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(rotationFoldout, "Rotation");

        if (rotationFoldout)
        {
            EditorGUILayout.PropertyField(alignToSplineTangent);
            EditorGUILayout.PropertyField(prefabRotationOffsetEuler);

            EditorGUILayout.HelpBox(
                "Use Prefab Rotation Offset if your prefab faces sideways or backwards. Common values are Y 90, Y -90, or Y 180.",
                MessageType.None);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawSurfaceSection()
    {
        surfaceFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(surfaceFoldout, "Surface Alignment");

        if (surfaceFoldout)
        {
            EditorGUILayout.PropertyField(alignToSurfaceNormal);
            EditorGUILayout.PropertyField(snapToSurface);
            EditorGUILayout.PropertyField(surfaceMask);
            EditorGUILayout.PropertyField(surfaceRayHeight);
            EditorGUILayout.PropertyField(surfaceRayDistance);
            EditorGUILayout.PropertyField(fallbackUp);

            EditorGUILayout.HelpBox(
                "Surface alignment raycasts downward from each placement point. The hit normal becomes the object's up direction.",
                MessageType.None);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawAdvancedSection()
    {
        advancedFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(advancedFoldout, "Advanced");

        if (advancedFoldout)
        {
            EditorGUILayout.PropertyField(distanceSamples);
            EditorGUILayout.PropertyField(generatedRootName);
            EditorGUILayout.PropertyField(itemNamePrefix);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawHelpSection()
    {
        helpFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(helpFoldout, "Scene View Editing Help");

        if (helpFoldout)
        {
            EditorGUILayout.HelpBox(
                "Shift + Left Click on ground: add a new spline point.\n\n" +
                "Shift + Left Click near an existing spline segment: insert a new spline point between two existing points.\n\n" +
                "Moving existing spline points now also auto-regenerates when Auto Regenerate and Regenerate When Spline Changes are enabled.",
                MessageType.Info);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void OnSceneGUI()
    {
        SplinePrefabPlacer placer = (SplinePrefabPlacer)target;

        if (placer == null)
            return;

        placer.TryAutoAssignSplineContainer();

        if (placer.splineContainer == null)
            return;

        if (placer.splineContainer.Splines == null || placer.splineContainer.Splines.Count == 0)
            return;

        Event e = Event.current;

        DrawSplineInsertPreview(placer);

        if (e.type != EventType.MouseDown)
            return;

        if (e.button != 0 || !e.shift)
            return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (!TryGetClickWorldPosition(placer, ray, out Vector3 clickedWorld))
            return;

        Undo.RecordObject(placer.splineContainer, "Edit Spline Point");

        bool inserted = TryInsertPointBetweenExistingKnots(placer, e.mousePosition, clickedWorld);

        if (!inserted)
            AddPointAtEnd(placer, clickedWorld);

        EditorUtility.SetDirty(placer.splineContainer);

        LastSplineSignatures[placer.GetInstanceID()] = placer.GetSplineSignature();

        if (!Application.isPlaying && placer.autoRegenerateInEditor && placer.regenerateAfterSplineEdit)
        {
            Undo.RegisterFullObjectHierarchyUndo(placer.gameObject, "Regenerate Spline Prefabs");
            placer.QueueRegenerate();
        }

        SceneView.RepaintAll();

        e.Use();
    }

    private static void DrawSplineInsertPreview(SplinePrefabPlacer placer)
    {
        Event e = Event.current;

        if (!e.shift)
            return;

        if (placer.splineContainer == null)
            return;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (!TryGetClickWorldPosition(placer, ray, out Vector3 clickedWorld))
            return;

        Handles.color = Color.yellow;
        Handles.SphereHandleCap(
            0,
            clickedWorld,
            Quaternion.identity,
            HandleUtility.GetHandleSize(clickedWorld) * 0.12f,
            EventType.Repaint);
    }

    private static bool TryGetClickWorldPosition(SplinePrefabPlacer placer, Ray ray, out Vector3 worldPosition)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, 10000f, placer.surfaceMask, QueryTriggerInteraction.Ignore))
        {
            worldPosition = hit.point;
            return true;
        }

        Plane plane = new Plane(Vector3.up, placer.transform.position);

        if (plane.Raycast(ray, out float distance))
        {
            worldPosition = ray.GetPoint(distance);
            return true;
        }

        worldPosition = default;
        return false;
    }

    private static void AddPointAtEnd(SplinePrefabPlacer placer, Vector3 worldPosition)
    {
        Spline spline = GetSpline(placer);

        if (spline == null)
            return;

        Vector3 local = placer.splineContainer.transform.InverseTransformPoint(worldPosition);
        BezierKnot knot = new BezierKnot((float3)local);

        spline.Add(knot);
    }

    private static bool TryInsertPointBetweenExistingKnots(
        SplinePrefabPlacer placer,
        Vector2 mousePosition,
        Vector3 clickedWorld)
    {
        Spline spline = GetSpline(placer);

        if (spline == null || spline.Count < 2)
            return false;

        int curveCount = spline.Closed ? spline.Count : spline.Count - 1;

        if (curveCount <= 0)
            return false;

        float bestScreenDistance = float.MaxValue;
        int bestInsertIndex = -1;

        for (int curve = 0; curve < curveCount; curve++)
        {
            for (int sample = 0; sample <= SegmentSamplesPerCurve; sample++)
            {
                float curveT = sample / (float)SegmentSamplesPerCurve;
                float globalT = CurveSampleToGlobalT(spline, curve, curveT);

                if (!placer.splineContainer.Evaluate(
                        placer.splineIndex,
                        Mathf.Clamp01(globalT),
                        out float3 pos,
                        out float3 tangent,
                        out float3 up))
                {
                    continue;
                }

                Vector2 screen = HandleUtility.WorldToGUIPoint((Vector3)pos);
                float screenDistance = Vector2.Distance(screen, mousePosition);

                if (screenDistance < bestScreenDistance)
                {
                    bestScreenDistance = screenDistance;
                    bestInsertIndex = curve + 1;
                }
            }
        }

        if (bestScreenDistance > InsertScreenDistance)
            return false;

        if (spline.Closed && bestInsertIndex >= spline.Count)
            bestInsertIndex = spline.Count;

        bestInsertIndex = Mathf.Clamp(bestInsertIndex, 0, spline.Count);

        Vector3 local = placer.splineContainer.transform.InverseTransformPoint(clickedWorld);
        BezierKnot knot = new BezierKnot((float3)local);

        spline.Insert(bestInsertIndex, knot);

        return true;
    }

    private static float CurveSampleToGlobalT(Spline spline, int curveIndex, float curveT)
    {
        int curveCount = spline.Closed ? spline.Count : spline.Count - 1;

        if (curveCount <= 0)
            return 0f;

        return Mathf.Clamp01((curveIndex + curveT) / curveCount);
    }

    private static Spline GetSpline(SplinePrefabPlacer placer)
    {
        if (placer == null || placer.splineContainer == null)
            return null;

        if (placer.splineContainer.Splines == null || placer.splineContainer.Splines.Count == 0)
            return null;

        placer.splineIndex = Mathf.Clamp(placer.splineIndex, 0, placer.splineContainer.Splines.Count - 1);

        return placer.splineContainer.Splines[placer.splineIndex];
    }
}

#endif