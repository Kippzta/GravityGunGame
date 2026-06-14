#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

[CustomEditor(typeof(SplinePhysicsMover))]
public class SplinePhysicsMoverEditor : Editor
{
    private SplinePhysicsMover mover;

    private double lastEditorTime;
    private bool isPreviewing;
    private float previewDirection = 1f;

    private bool isCtrlDuplicatingKnot;
    private int duplicatedKnotIndex = -1;

    private int selectedKnotIndex = -1;

    private const int MinimumPointCount = 2;

    private void OnEnable()
    {
        mover = (SplinePhysicsMover)target;

        isPreviewing = true;
        previewDirection = 1f;
        lastEditorTime = EditorApplication.timeSinceStartup;

        EditorApplication.update += EditorUpdate;

        if (!Application.isPlaying && mover != null)
        {
            PreviewAtWithoutUndo(mover.StartT);
        }
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;

        if (!Application.isPlaying)
        {
            ResetPreview();
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (Application.isPlaying)
            return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Editor Auto Preview", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Selecting this object automatically previews the movement in Edit Mode.\n\n" +
            "Click a white spline point to select it.\n" +
            "Only the selected point shows the move gizmo.\n" +
            "Hold CTRL while moving the selected point to duplicate it.\n" +
            "Press Delete/Backspace while a point is selected to delete the point, not the platform.\n" +
            "Use the buttons below to set selected/all points to Linear, Auto, or Bezier.",
            MessageType.Info
        );

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Preview Start"))
        {
            previewDirection = 1f;
            PreviewAt(mover.StartT);
        }

        if (GUILayout.Button("Preview End"))
        {
            previewDirection = -1f;
            PreviewAt(1f);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Preview Closed"))
            PreviewAt(mover.ClosedT);

        if (GUILayout.Button("Preview Open"))
            PreviewAt(mover.OpenT);

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Reset Preview"))
        {
            previewDirection = 1f;
            PreviewAt(mover.StartT);
        }

        EditorGUILayout.Space();

        DrawPointDeleteControls();

        EditorGUILayout.Space();

        DrawTangentModeButtons();

        EditorGUILayout.Space();

        EditorGUILayout.LabelField(
            "Estimated Full Length",
            mover.EstimateFullSplineLength().ToString("0.00") + " units"
        );
    }

    private void DrawPointDeleteControls()
    {
        EditorGUILayout.LabelField("Spline Point Delete", EditorStyles.boldLabel);

        string selectedText = selectedKnotIndex >= 0
            ? "Selected Point: " + selectedKnotIndex
            : "Selected Point: None";

        EditorGUILayout.LabelField(selectedText);

        using (new EditorGUI.DisabledScope(!CanDeleteSelectedPoint()))
        {
            if (GUILayout.Button("Delete Selected Point"))
            {
                DeleteSelectedPoint();
            }
        }

        if (selectedKnotIndex >= 0 && !CanDeleteSelectedPoint())
        {
            EditorGUILayout.HelpBox(
                "You need at least 2 spline points, so this point cannot be deleted.",
                MessageType.Warning
            );
        }
    }

    private void DrawTangentModeButtons()
    {
        EditorGUILayout.LabelField("Spline Point Mode", EditorStyles.boldLabel);

        string selectedText = selectedKnotIndex >= 0
            ? "Selected Point: " + selectedKnotIndex
            : "Selected Point: None";

        EditorGUILayout.LabelField(selectedText);

        using (new EditorGUI.DisabledScope(selectedKnotIndex < 0))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Selected Linear"))
                SetSelectedKnotMode(TangentMode.Linear);

            if (GUILayout.Button("Selected Auto"))
                SetSelectedKnotMode(TangentMode.AutoSmooth);

            if (GUILayout.Button("Selected Bezier"))
                SetSelectedKnotMode(TangentMode.Broken);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("All Linear"))
            SetAllKnotModes(TangentMode.Linear);

        if (GUILayout.Button("All Auto"))
            SetAllKnotModes(TangentMode.AutoSmooth);

        if (GUILayout.Button("All Bezier"))
            SetAllKnotModes(TangentMode.Broken);

        EditorGUILayout.EndHorizontal();
    }

    private bool CanDeleteSelectedPoint()
    {
        Spline spline = GetEditableSpline(out SplineContainer container);

        if (spline == null)
            return false;

        if (selectedKnotIndex < 0 || selectedKnotIndex >= spline.Count)
            return false;

        return spline.Count > MinimumPointCount;
    }

    private void DeleteSelectedPoint()
    {
        Spline spline = GetEditableSpline(out SplineContainer container);

        if (spline == null)
            return;

        if (!CanDeleteSelectedPoint())
            return;

        Undo.RecordObject(container, "Delete Spline Point");

        spline.RemoveAt(selectedKnotIndex);

        selectedKnotIndex = Mathf.Clamp(selectedKnotIndex, 0, spline.Count - 1);
        isCtrlDuplicatingKnot = false;
        duplicatedKnotIndex = -1;

        EditorUtility.SetDirty(container);

        mover.PreviewInEditor(mover.EditorPreviewT);

        Repaint();
        SceneView.RepaintAll();
    }

    private void SetSelectedKnotMode(TangentMode mode)
    {
        Spline spline = GetEditableSpline(out SplineContainer container);

        if (spline == null)
            return;

        if (selectedKnotIndex < 0 || selectedKnotIndex >= spline.Count)
            return;

        Undo.RecordObject(container, "Set Selected Spline Point Mode");

        spline.SetTangentMode(selectedKnotIndex, mode);

        EditorUtility.SetDirty(container);
        mover.PreviewInEditor(mover.EditorPreviewT);
        SceneView.RepaintAll();
    }

    private void SetAllKnotModes(TangentMode mode)
    {
        Spline spline = GetEditableSpline(out SplineContainer container);

        if (spline == null)
            return;

        Undo.RecordObject(container, "Set All Spline Point Modes");

        for (int i = 0; i < spline.Count; i++)
        {
            spline.SetTangentMode(i, mode);
        }

        EditorUtility.SetDirty(container);
        mover.PreviewInEditor(mover.EditorPreviewT);
        SceneView.RepaintAll();
    }

    private Spline GetEditableSpline(out SplineContainer container)
    {
        container = null;

        if (mover == null)
            return null;

        container = mover.Spline;

        if (container == null)
            return null;

        int splineIndex = mover.SplineIndex;

        if (splineIndex < 0 || splineIndex >= container.Splines.Count)
            return null;

        return container.Splines[splineIndex];
    }

    private void EditorUpdate()
    {
        if (!isPreviewing)
            return;

        if (Application.isPlaying)
            return;

        if (mover == null)
            return;

        if (Selection.activeGameObject != mover.gameObject)
        {
            ResetPreview();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(now - lastEditorTime);
        lastEditorTime = now;

        deltaTime = Mathf.Clamp(deltaTime, 0f, 0.05f);

        float currentT = mover.EditorPreviewT;
        float targetT = previewDirection > 0f ? 1f : mover.StartT;

        currentT += previewDirection * mover.GetEditorDeltaT(deltaTime, currentT, targetT);
        currentT = Mathf.Clamp01(currentT);

        if (previewDirection > 0f && currentT >= 1f)
        {
            currentT = 1f;
            previewDirection = -1f;
        }
        else if (previewDirection < 0f && currentT <= mover.StartT)
        {
            currentT = mover.StartT;
            previewDirection = 1f;
        }

        PreviewAtWithoutUndo(currentT);
    }

    private void ResetPreview()
    {
        isPreviewing = false;
        previewDirection = 1f;
        isCtrlDuplicatingKnot = false;
        duplicatedKnotIndex = -1;
        selectedKnotIndex = -1;

        if (mover != null && !Application.isPlaying)
        {
            PreviewAtWithoutUndo(mover.StartT);
        }

        SceneView.RepaintAll();
    }

    private void PreviewAt(float t)
    {
        if (mover == null)
            return;

        Undo.RecordObject(mover, "Preview Spline Mover");

        Transform targetTransform = mover.TargetTransform;

        if (targetTransform != null)
            Undo.RecordObject(targetTransform, "Move Preview Target");

        mover.PreviewInEditor(t);

        EditorUtility.SetDirty(mover);

        if (targetTransform != null)
            EditorUtility.SetDirty(targetTransform);

        SceneView.RepaintAll();
    }

    private void PreviewAtWithoutUndo(float t)
    {
        if (mover == null)
            return;

        Transform targetTransform = mover.TargetTransform;

        mover.PreviewInEditor(t);

        EditorUtility.SetDirty(mover);

        if (targetTransform != null)
            EditorUtility.SetDirty(targetTransform);

        SceneView.RepaintAll();
    }

    private void OnSceneGUI()
    {
        if (mover == null)
            return;

        if (mover.Spline == null)
            return;

        Event currentEvent = Event.current;

        HandleDeleteShortcut(currentEvent);

        if (currentEvent.type == EventType.MouseUp || currentEvent.type == EventType.Ignore)
        {
            isCtrlDuplicatingKnot = false;
            duplicatedKnotIndex = -1;
        }

        DrawPreviewPositionAndRotation();

        if (mover.ShowSplinePreviewLine)
            DrawSplinePreviewLine();

        if (mover.ShowSplineEditHandles)
            DrawEditableSplineKnots();
    }

    private void HandleDeleteShortcut(Event currentEvent)
    {
        if (currentEvent == null)
            return;

        if (currentEvent.type != EventType.KeyDown)
            return;

        bool isDeleteKey =
            currentEvent.keyCode == KeyCode.Delete ||
            currentEvent.keyCode == KeyCode.Backspace;

        if (!isDeleteKey)
            return;

        if (selectedKnotIndex < 0)
            return;

        if (CanDeleteSelectedPoint())
        {
            DeleteSelectedPoint();
        }

        currentEvent.Use();
    }

    private void DrawPreviewPositionAndRotation()
    {
        if (!mover.TryGetSplinePose(
                mover.EditorPreviewT,
                out Vector3 previewPosition,
                out Quaternion previewRotation))
        {
            return;
        }

        float size = HandleUtility.GetHandleSize(previewPosition) * 0.18f;

        Handles.color = Color.yellow;
        Handles.SphereHandleCap(
            0,
            previewPosition,
            Quaternion.identity,
            size,
            EventType.Repaint
        );

        Handles.Label(
            previewPosition + Vector3.up * size * 1.5f,
            $"Preview {mover.EditorPreviewT:0.00}"
        );

        if (!mover.RotateAlongSpline)
            return;

        float arrowLength = HandleUtility.GetHandleSize(previewPosition) * 0.75f;

        Handles.color = Color.yellow;
        Handles.ArrowHandleCap(
            0,
            previewPosition,
            previewRotation,
            arrowLength,
            EventType.Repaint
        );

        Handles.color = Color.green;
        Handles.DrawLine(
            previewPosition,
            previewPosition + previewRotation * Vector3.up * arrowLength * 0.65f,
            3f
        );
    }

    private void DrawSplinePreviewLine()
    {
        Handles.color = Color.cyan;

        const int samples = 64;

        Vector3 previous = mover.GetSplineWorldPosition(0f);

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 current = mover.GetSplineWorldPosition(t);

            Handles.DrawLine(previous, current, 3f);

            previous = current;
        }

        DrawPointLabel(0f, "Start", Color.green);
        DrawPointLabel(1f, "End", Color.red);
        DrawPointLabel(mover.ClosedT, "Closed", Color.blue);
        DrawPointLabel(mover.OpenT, "Open", Color.yellow);
    }

    private void DrawPointLabel(float t, string label, Color color)
    {
        Vector3 position = mover.GetSplineWorldPosition(t);
        float size = HandleUtility.GetHandleSize(position) * 0.12f;

        Handles.color = color;
        Handles.SphereHandleCap(
            0,
            position,
            Quaternion.identity,
            size,
            EventType.Repaint
        );

        Handles.Label(position + Vector3.up * size * 1.5f, label);
    }

    private void DrawEditableSplineKnots()
    {
        Spline spline = GetEditableSpline(out SplineContainer container);

        if (spline == null)
            return;

        Transform containerTransform = container.transform;

        Event currentEvent = Event.current;
        bool ctrlHeld = currentEvent.control || currentEvent.command;

        for (int i = 0; i < spline.Count; i++)
        {
            BezierKnot knot = spline[i];

            Vector3 localPosition = knot.Position;
            Vector3 worldPosition = containerTransform.TransformPoint(localPosition);

            float size = HandleUtility.GetHandleSize(worldPosition) * 0.16f;

            Color knotColor = Color.white;

            if (i == selectedKnotIndex)
                knotColor = Color.yellow;
            else if (ctrlHeld)
                knotColor = Color.magenta;

            Handles.color = knotColor;

            if (Handles.Button(
                    worldPosition,
                    Quaternion.identity,
                    size,
                    size,
                    Handles.SphereHandleCap))
            {
                selectedKnotIndex = i;
                Repaint();
                SceneView.RepaintAll();
            }

            string label = i == selectedKnotIndex
                ? $"Selected Point {i}"
                : ctrlHeld
                    ? $"CTRL click, then drag to duplicate Point {i}"
                    : $"Point {i}";

            Handles.Label(
                worldPosition + Vector3.up * size * 1.5f,
                label
            );

            if (i != selectedKnotIndex)
                continue;

            EditorGUI.BeginChangeCheck();

            Vector3 newWorldPosition = Handles.PositionHandle(
                worldPosition,
                Quaternion.identity
            );

            if (EditorGUI.EndChangeCheck())
            {
                Vector3 newLocalPosition = containerTransform.InverseTransformPoint(newWorldPosition);

                if (ctrlHeld && !isCtrlDuplicatingKnot)
                {
                    Undo.RecordObject(container, "Duplicate Spline Point");

                    BezierKnot duplicatedKnot = knot;

                    duplicatedKnot.Position = new float3(
                        newLocalPosition.x,
                        newLocalPosition.y,
                        newLocalPosition.z
                    );

                    int insertIndex = Mathf.Clamp(i + 1, 0, spline.Count);
                    spline.Insert(insertIndex, duplicatedKnot);

                    selectedKnotIndex = insertIndex;
                    isCtrlDuplicatingKnot = true;
                    duplicatedKnotIndex = insertIndex;

                    EditorUtility.SetDirty(container);
                    mover.PreviewInEditor(mover.EditorPreviewT);
                    SceneView.RepaintAll();

                    return;
                }

                if (isCtrlDuplicatingKnot && duplicatedKnotIndex >= 0 && duplicatedKnotIndex < spline.Count)
                {
                    Undo.RecordObject(container, "Move Duplicated Spline Point");

                    BezierKnot duplicatedKnot = spline[duplicatedKnotIndex];

                    duplicatedKnot.Position = new float3(
                        newLocalPosition.x,
                        newLocalPosition.y,
                        newLocalPosition.z
                    );

                    spline[duplicatedKnotIndex] = duplicatedKnot;

                    selectedKnotIndex = duplicatedKnotIndex;

                    EditorUtility.SetDirty(container);
                    mover.PreviewInEditor(mover.EditorPreviewT);
                    SceneView.RepaintAll();

                    return;
                }

                Undo.RecordObject(container, "Move Spline Point");

                knot.Position = new float3(
                    newLocalPosition.x,
                    newLocalPosition.y,
                    newLocalPosition.z
                );

                spline[i] = knot;

                EditorUtility.SetDirty(container);
                mover.PreviewInEditor(mover.EditorPreviewT);
                SceneView.RepaintAll();
            }
        }
    }
}
#endif