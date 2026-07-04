#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class VertexPaintEditorTool : EditorWindow
{
    private enum PaintMode
    {
        BrushPaint,
        FaceFill
    }

    private enum ChannelMode
    {
        RGBA,
        Red,
        Green,
        Blue,
        Alpha
    }

    private enum FalloffMode
    {
        Smooth,
        Linear,
        Hard
    }

    private PaintMode paintMode = PaintMode.BrushPaint;
    private ChannelMode channelMode = ChannelMode.RGBA;
    private FalloffMode falloffMode = FalloffMode.Smooth;

    private Color paintColor = Color.red;

    private float brushRadius = 1.0f;
    private float brushStrength = 0.5f;
    private float normalAngleLimit = 75.0f;

    private bool enablePainting = true;
    private bool ignoreBackfaces = true;
    private bool useNormalAngleLimit = true;
    private bool showBrushPreview = true;
    private bool autoMakeInstancePaintable = true;

    private MeshFilter currentMeshFilter;
    private VertexPaintInstanceData currentPaintData;

    private RaycastHitData lastHit;
    private bool hasHit;

    [MenuItem("Tools/Vertex Painter/Scene Instance Vertex Painter")]
    public static void OpenWindow()
    {
        VertexPaintEditorTool window = GetWindow<VertexPaintEditorTool>();
        window.titleContent = new GUIContent("Vertex Painter");
        window.Show();
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += OnSelectionChanged;
        OnSelectionChanged();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Selection.selectionChanged -= OnSelectionChanged;
    }

    private void OnSelectionChanged()
    {
        currentMeshFilter = null;
        currentPaintData = null;

        if (Selection.activeGameObject != null)
        {
            currentMeshFilter = Selection.activeGameObject.GetComponent<MeshFilter>();
            currentPaintData = Selection.activeGameObject.GetComponent<VertexPaintInstanceData>();
        }

        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Scene Instance Vertex Painter", EditorStyles.boldLabel);

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Selected Mesh", EditorStyles.boldLabel);

            currentMeshFilter = (MeshFilter)EditorGUILayout.ObjectField("Mesh Filter", currentMeshFilter, typeof(MeshFilter), true);

            if (currentMeshFilter != null)
            {
                currentPaintData = currentMeshFilter.GetComponent<VertexPaintInstanceData>();
            }

            GUI.enabled = currentMeshFilter != null && currentMeshFilter.sharedMesh != null;

            if (GUILayout.Button("Make Scene Instance Paintable"))
            {
                EnsureSceneInstancePaintable(true);
            }

            if (GUILayout.Button("Save Paint To This Scene Instance"))
            {
                SavePaintToInstance();
            }

            if (GUILayout.Button("Rebuild Instance Mesh From Stored Paint"))
            {
                RebuildInstanceMesh();
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("Revert MeshFilter To Original Mesh"))
            {
                RevertToOriginalMesh();
            }

            if (GUILayout.Button("Clear Stored Paint And Revert"))
            {
                ClearStoredPaintAndRevert();
            }

            GUI.enabled = true;

            if (currentMeshFilter == null || currentMeshFilter.sharedMesh == null)
            {
                EditorGUILayout.HelpBox("Select a GameObject with a MeshFilter.", MessageType.Info);
            }
            else
            {
                DrawMeshStatus();
            }
        }

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Paint Settings", EditorStyles.boldLabel);

            enablePainting = EditorGUILayout.Toggle("Enable Painting", enablePainting);
            autoMakeInstancePaintable = EditorGUILayout.Toggle("Auto Make Paintable", autoMakeInstancePaintable);

            paintMode = (PaintMode)EditorGUILayout.EnumPopup("Paint Mode", paintMode);
            channelMode = (ChannelMode)EditorGUILayout.EnumPopup("Channel Mode", channelMode);
            falloffMode = (FalloffMode)EditorGUILayout.EnumPopup("Falloff", falloffMode);

            paintColor = EditorGUILayout.ColorField("Paint Color", paintColor);

            brushRadius = EditorGUILayout.Slider("Brush Radius", brushRadius, 0.01f, 20.0f);
            brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0.01f, 1.0f);

            ignoreBackfaces = EditorGUILayout.Toggle("Ignore Backfaces", ignoreBackfaces);
            useNormalAngleLimit = EditorGUILayout.Toggle("Use Normal Angle Limit", useNormalAngleLimit);

            if (useNormalAngleLimit)
            {
                normalAngleLimit = EditorGUILayout.Slider("Normal Angle Limit", normalAngleLimit, 1.0f, 180.0f);
            }

            showBrushPreview = EditorGUILayout.Toggle("Show Brush Preview", showBrushPreview);
        }

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Fill / Clear", EditorStyles.boldLabel);

            GUI.enabled = currentMeshFilter != null && currentMeshFilter.sharedMesh != null;

            if (GUILayout.Button("Fill Whole Mesh With Paint Color"))
            {
                if (EnsureSceneInstancePaintable(false))
                {
                    FillWholeMesh(paintColor);
                }
            }

            if (GUILayout.Button("Clear Whole Mesh"))
            {
                if (EnsureSceneInstancePaintable(false))
                {
                    FillWholeMesh(Color.clear);
                }
            }

            GUI.enabled = true;
        }

        EditorGUILayout.Space(6);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Scene Controls", EditorStyles.boldLabel);

            EditorGUILayout.HelpBox(
                "Left Mouse: Paint\n" +
                "Shift + Left Mouse: Erase\n" +
                "Ctrl + Left Mouse: Smooth\n" +
                "F: Fill face under cursor\n" +
                "P: Pick color under cursor\n\n" +
                "Paint is stored on VertexPaintInstanceData on this scene object.\n" +
                "The original FBX mesh is not modified.",
                MessageType.Info
            );
        }
    }

    private void DrawMeshStatus()
    {
        if (currentMeshFilter == null)
        {
            return;
        }

        Mesh mesh = currentMeshFilter.sharedMesh;

        if (mesh == null)
        {
            return;
        }

        string meshPath = AssetDatabase.GetAssetPath(mesh);

        if (currentPaintData == null)
        {
            EditorGUILayout.HelpBox(
                "This object is not scene-instance paintable yet. Press 'Make Scene Instance Paintable'.",
                MessageType.Warning
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Scene instance paint data found.\n" +
                "Stored Vertex Count: " + currentPaintData.StoredVertexCount + "\n" +
                "Original Mesh: " + (currentPaintData.OriginalMesh != null ? currentPaintData.OriginalMesh.name : "None"),
                MessageType.Info
            );
        }

        if (!string.IsNullOrEmpty(meshPath) && meshPath.ToLower().EndsWith(".fbx"))
        {
            EditorGUILayout.HelpBox(
                "The current MeshFilter is still using an FBX mesh. Painting will automatically create a temporary instance mesh and store colors on this scene object.",
                MessageType.Info
            );
        }
        else if (string.IsNullOrEmpty(meshPath))
        {
            EditorGUILayout.HelpBox(
                "The current MeshFilter is using a generated scene instance mesh. This is expected.",
                MessageType.None
            );
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Current mesh asset path: " + meshPath,
                MessageType.None
            );
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!enablePainting)
        {
            return;
        }

        if (currentMeshFilter == null || currentMeshFilter.sharedMesh == null)
        {
            return;
        }

        Event e = Event.current;

        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        hasHit = RaycastMesh(currentMeshFilter, ray, out lastHit);

        if (hasHit && showBrushPreview)
        {
            DrawBrushPreview(lastHit);
        }

        if (hasHit)
        {
            HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
        }

        if (e.type == EventType.KeyDown && hasHit)
        {
            if (e.keyCode == KeyCode.F)
            {
                if (EnsureSceneInstancePaintable(false))
                {
                    FillFace(lastHit.triangleIndex, paintColor);
                }

                e.Use();
            }

            if (e.keyCode == KeyCode.P)
            {
                if (EnsureSceneInstancePaintable(false))
                {
                    paintColor = PickColor(lastHit);
                    Repaint();
                }

                e.Use();
            }
        }

        bool leftMouseHeld =
            e.button == 0 &&
            (e.type == EventType.MouseDown || e.type == EventType.MouseDrag);

        if (!leftMouseHeld || !hasHit)
        {
            return;
        }

        if (!autoMakeInstancePaintable && currentPaintData == null)
        {
            return;
        }

        if (!EnsureSceneInstancePaintable(false))
        {
            return;
        }

        if (paintMode == PaintMode.FaceFill)
        {
            FillFace(lastHit.triangleIndex, e.shift ? Color.clear : paintColor);
            e.Use();
            return;
        }

        if (e.control || e.command)
        {
            SmoothPaint(lastHit);
            e.Use();
            return;
        }

        if (e.shift)
        {
            BrushPaint(lastHit, Color.clear, true);
            e.Use();
            return;
        }

        BrushPaint(lastHit, paintColor, false);
        e.Use();
    }

    private bool EnsureSceneInstancePaintable(bool forceMessage)
    {
        if (currentMeshFilter == null || currentMeshFilter.sharedMesh == null)
        {
            if (forceMessage)
            {
                Debug.LogWarning("Vertex Painter: Select a GameObject with a MeshFilter.");
            }

            return false;
        }

        currentPaintData = currentMeshFilter.GetComponent<VertexPaintInstanceData>();

        if (currentPaintData == null)
        {
            currentPaintData = Undo.AddComponent<VertexPaintInstanceData>(currentMeshFilter.gameObject);
        }

        Undo.RecordObject(currentPaintData, "Make Scene Instance Paintable");
        Undo.RecordObject(currentMeshFilter, "Assign Scene Instance Vertex Paint Mesh");

        Mesh instanceMesh = currentPaintData.GetOrCreateInstanceMesh(currentMeshFilter, Color.clear);

        if (instanceMesh == null)
        {
            if (forceMessage)
            {
                Debug.LogWarning("Vertex Painter: Could not create instance mesh.");
            }

            return false;
        }

        EditorUtility.SetDirty(currentPaintData);
        EditorUtility.SetDirty(currentMeshFilter);
        EditorUtility.SetDirty(currentMeshFilter.gameObject);

        return true;
    }

    private void SavePaintToInstance()
    {
        if (currentMeshFilter == null || currentPaintData == null || currentMeshFilter.sharedMesh == null)
        {
            return;
        }

        currentPaintData.StoreColors(currentMeshFilter.sharedMesh.colors32);

        EditorUtility.SetDirty(currentPaintData);
        EditorUtility.SetDirty(currentMeshFilter.gameObject);

        Debug.Log("Vertex Painter: Saved vertex colors to scene instance data on " + currentMeshFilter.gameObject.name);
    }

    private void RebuildInstanceMesh()
    {
        if (currentMeshFilter == null || currentPaintData == null)
        {
            return;
        }

        Undo.RecordObject(currentMeshFilter, "Rebuild Vertex Paint Instance Mesh");
        currentPaintData.ApplyToMeshFilter(currentMeshFilter);

        EditorUtility.SetDirty(currentMeshFilter);
        SceneView.RepaintAll();
    }

    private void RevertToOriginalMesh()
    {
        if (currentPaintData == null)
        {
            return;
        }

        Undo.RecordObject(currentPaintData, "Revert To Original Mesh");

        if (currentMeshFilter != null)
        {
            Undo.RecordObject(currentMeshFilter, "Revert To Original Mesh");
        }

        currentPaintData.RevertToOriginalMesh();

        if (currentMeshFilter != null)
        {
            EditorUtility.SetDirty(currentMeshFilter);
        }

        SceneView.RepaintAll();
    }

    private void ClearStoredPaintAndRevert()
    {
        if (currentPaintData == null)
        {
            return;
        }

        Undo.RecordObject(currentPaintData, "Clear Stored Vertex Paint");

        if (currentMeshFilter != null)
        {
            Undo.RecordObject(currentMeshFilter, "Clear Stored Vertex Paint");
        }

        currentPaintData.ResetPaintDataAndRevert();

        if (currentMeshFilter != null)
        {
            EditorUtility.SetDirty(currentMeshFilter);
        }

        SceneView.RepaintAll();
    }

    private void FillWholeMesh(Color color)
    {
        Mesh mesh = GetEditableMesh();

        if (mesh == null)
        {
            return;
        }

        EnsureColorArray(mesh, Color.clear);

        Undo.RecordObject(currentPaintData, "Fill Stored Vertex Colors");

        Color32[] colors = mesh.colors32;

        bool erase = color == Color.clear;

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = ApplyChannel(colors[i], color, 1.0f, erase);
        }

        mesh.colors32 = colors;
        CommitColorsToInstance(mesh);
    }

    private void FillFace(int triangleIndex, Color color)
    {
        Mesh mesh = GetEditableMesh();

        if (mesh == null)
        {
            return;
        }

        EnsureColorArray(mesh, Color.clear);

        int[] triangles = mesh.triangles;
        int baseIndex = triangleIndex * 3;

        if (baseIndex < 0 || baseIndex + 2 >= triangles.Length)
        {
            return;
        }

        Undo.RecordObject(currentPaintData, "Fill Vertex Paint Face");

        Color32[] colors = mesh.colors32;

        bool erase = color == Color.clear;

        colors[triangles[baseIndex + 0]] = ApplyChannel(colors[triangles[baseIndex + 0]], color, 1.0f, erase);
        colors[triangles[baseIndex + 1]] = ApplyChannel(colors[triangles[baseIndex + 1]], color, 1.0f, erase);
        colors[triangles[baseIndex + 2]] = ApplyChannel(colors[triangles[baseIndex + 2]], color, 1.0f, erase);

        mesh.colors32 = colors;
        CommitColorsToInstance(mesh);
    }

    private void BrushPaint(RaycastHitData hit, Color targetColor, bool erase)
    {
        Mesh mesh = GetEditableMesh();

        if (mesh == null)
        {
            return;
        }

        EnsureColorArray(mesh, Color.clear);

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Color32[] colors = mesh.colors32;

        Transform t = currentMeshFilter.transform;

        Undo.RecordObject(currentPaintData, erase ? "Erase Vertex Paint" : "Brush Vertex Paint");

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = t.TransformPoint(vertices[i]);
            float distance = Vector3.Distance(worldPos, hit.point);

            if (distance > brushRadius)
            {
                continue;
            }

            if (useNormalAngleLimit && normals != null && normals.Length == vertices.Length)
            {
                Vector3 worldNormal = t.TransformDirection(normals[i]).normalized;
                float angle = Vector3.Angle(hit.normal, worldNormal);

                if (angle > normalAngleLimit)
                {
                    continue;
                }
            }

            float falloff = EvaluateFalloff(distance / brushRadius);
            float strength = brushStrength * falloff;

            colors[i] = ApplyChannel(colors[i], targetColor, strength, erase);
        }

        mesh.colors32 = colors;
        CommitColorsToInstance(mesh);
    }

    private void SmoothPaint(RaycastHitData hit)
    {
        Mesh mesh = GetEditableMesh();

        if (mesh == null)
        {
            return;
        }

        EnsureColorArray(mesh, Color.clear);

        Vector3[] vertices = mesh.vertices;
        Color32[] colors = mesh.colors32;
        Color32[] originalColors = mesh.colors32;

        Transform t = currentMeshFilter.transform;

        Undo.RecordObject(currentPaintData, "Smooth Vertex Paint");

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = t.TransformPoint(vertices[i]);
            float distance = Vector3.Distance(worldPos, hit.point);

            if (distance > brushRadius)
            {
                continue;
            }

            Color average = AverageNearbyColor(vertices, originalColors, i, brushRadius, t);

            float falloff = EvaluateFalloff(distance / brushRadius);
            float strength = brushStrength * falloff;

            colors[i] = Color32.Lerp(colors[i], average, strength);
        }

        mesh.colors32 = colors;
        CommitColorsToInstance(mesh);
    }

    private Color AverageNearbyColor(Vector3[] vertices, Color32[] colors, int centerIndex, float radius, Transform t)
    {
        Vector3 centerWorld = t.TransformPoint(vertices[centerIndex]);

        Color sum = Color.clear;
        int count = 0;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = t.TransformPoint(vertices[i]);
            float distance = Vector3.Distance(centerWorld, worldPos);

            if (distance <= radius)
            {
                sum += (Color)colors[i];
                count++;
            }
        }

        if (count <= 0)
        {
            return colors[centerIndex];
        }

        return sum / count;
    }

    private Color PickColor(RaycastHitData hit)
    {
        Mesh mesh = GetEditableMesh();

        if (mesh == null)
        {
            return paintColor;
        }

        EnsureColorArray(mesh, Color.clear);

        int[] triangles = mesh.triangles;
        Color32[] colors = mesh.colors32;

        int baseIndex = hit.triangleIndex * 3;

        if (baseIndex < 0 || baseIndex + 2 >= triangles.Length)
        {
            return paintColor;
        }

        Color c0 = colors[triangles[baseIndex + 0]];
        Color c1 = colors[triangles[baseIndex + 1]];
        Color c2 = colors[triangles[baseIndex + 2]];

        return (c0 + c1 + c2) / 3.0f;
    }

    private Mesh GetEditableMesh()
    {
        if (currentMeshFilter == null || currentPaintData == null)
        {
            return null;
        }

        Mesh mesh = currentMeshFilter.sharedMesh;

        if (mesh == null)
        {
            mesh = currentPaintData.GetOrCreateInstanceMesh(currentMeshFilter, Color.clear);
        }

        return mesh;
    }

    private void CommitColorsToInstance(Mesh mesh)
    {
        if (mesh == null || currentPaintData == null)
        {
            return;
        }

        mesh.UploadMeshData(false);
        currentPaintData.StoreColors(mesh.colors32);

        EditorUtility.SetDirty(currentPaintData);
        EditorUtility.SetDirty(currentMeshFilter);
        EditorUtility.SetDirty(currentMeshFilter.gameObject);

        SceneView.RepaintAll();
    }

    private void EnsureColorArray(Mesh mesh, Color defaultColor)
    {
        if (mesh == null)
        {
            return;
        }

        if (mesh.colors32 != null && mesh.colors32.Length == mesh.vertexCount)
        {
            return;
        }

        Color32[] colors = new Color32[mesh.vertexCount];

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] = defaultColor;
        }

        mesh.colors32 = colors;
    }

    private Color32 ApplyChannel(Color32 currentColor32, Color targetColor, float strength, bool erase)
    {
        Color current = currentColor32;

        if (erase)
        {
            switch (channelMode)
            {
                case ChannelMode.RGBA:
                    targetColor = Color.clear;
                    break;

                case ChannelMode.Red:
                    targetColor = new Color(0, current.g, current.b, current.a);
                    break;

                case ChannelMode.Green:
                    targetColor = new Color(current.r, 0, current.b, current.a);
                    break;

                case ChannelMode.Blue:
                    targetColor = new Color(current.r, current.g, 0, current.a);
                    break;

                case ChannelMode.Alpha:
                    targetColor = new Color(current.r, current.g, current.b, 0);
                    break;
            }
        }
        else
        {
            switch (channelMode)
            {
                case ChannelMode.RGBA:
                    break;

                case ChannelMode.Red:
                    targetColor = new Color(targetColor.r, current.g, current.b, current.a);
                    break;

                case ChannelMode.Green:
                    targetColor = new Color(current.r, targetColor.g, current.b, current.a);
                    break;

                case ChannelMode.Blue:
                    targetColor = new Color(current.r, current.g, targetColor.b, current.a);
                    break;

                case ChannelMode.Alpha:
                    targetColor = new Color(current.r, current.g, current.b, targetColor.a);
                    break;
            }
        }

        Color result = Color.Lerp(current, targetColor, Mathf.Clamp01(strength));
        return result;
    }

    private float EvaluateFalloff(float normalizedDistance)
    {
        normalizedDistance = Mathf.Clamp01(normalizedDistance);

        switch (falloffMode)
        {
            case FalloffMode.Hard:
                return 1.0f;

            case FalloffMode.Linear:
                return 1.0f - normalizedDistance;

            case FalloffMode.Smooth:
                float t = 1.0f - normalizedDistance;
                return t * t * (3.0f - 2.0f * t);

            default:
                return 1.0f - normalizedDistance;
        }
    }

    private void DrawBrushPreview(RaycastHitData hit)
    {
        Handles.color = new Color(paintColor.r, paintColor.g, paintColor.b, 0.9f);
        Handles.DrawWireDisc(hit.point, hit.normal, brushRadius);

        Handles.color = Color.white;
        Handles.DrawLine(hit.point, hit.point + hit.normal * Mathf.Max(0.25f, brushRadius * 0.25f));
    }

    private bool RaycastMesh(MeshFilter meshFilter, Ray ray, out RaycastHitData hitData)
    {
        hitData = default;

        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            return false;
        }

        Mesh mesh = meshFilter.sharedMesh;

        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Transform transform = meshFilter.transform;

        float closestDistance = float.MaxValue;
        bool foundHit = false;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = transform.TransformPoint(vertices[triangles[i + 0]]);
            Vector3 v1 = transform.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 v2 = transform.TransformPoint(vertices[triangles[i + 2]]);

            Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

            if (ignoreBackfaces)
            {
                float facing = Vector3.Dot(ray.direction, normal);

                if (facing >= 0.0f)
                {
                    continue;
                }
            }

            if (RayTriangleIntersection(ray, v0, v1, v2, out float distance, out Vector3 point))
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    foundHit = true;

                    hitData = new RaycastHitData
                    {
                        point = point,
                        normal = normal,
                        distance = distance,
                        triangleIndex = i / 3
                    };
                }
            }
        }

        return foundHit;
    }

    private bool RayTriangleIntersection(
        Ray ray,
        Vector3 v0,
        Vector3 v1,
        Vector3 v2,
        out float distance,
        out Vector3 point)
    {
        distance = 0.0f;
        point = Vector3.zero;

        const float epsilon = 0.0000001f;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;

        Vector3 h = Vector3.Cross(ray.direction, edge2);
        float a = Vector3.Dot(edge1, h);

        if (a > -epsilon && a < epsilon)
        {
            return false;
        }

        float f = 1.0f / a;
        Vector3 s = ray.origin - v0;
        float u = f * Vector3.Dot(s, h);

        if (u < 0.0f || u > 1.0f)
        {
            return false;
        }

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(ray.direction, q);

        if (v < 0.0f || u + v > 1.0f)
        {
            return false;
        }

        float t = f * Vector3.Dot(edge2, q);

        if (t > epsilon)
        {
            distance = t;
            point = ray.origin + ray.direction * t;
            return true;
        }

        return false;
    }

    private struct RaycastHitData
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
        public int triangleIndex;
    }
}
#endif