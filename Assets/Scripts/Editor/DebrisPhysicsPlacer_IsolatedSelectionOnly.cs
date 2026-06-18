#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Selection-only editor physics preview for Unity 6.
///
/// Important design rule:
/// This tool NEVER calls Physics.Simulate() on the active/default scene.
/// It creates a separate local PhysicsScene, simulates only clones there,
/// and copies results back only when Bake is pressed.
/// </summary>
public class DebrisPhysicsPlacerSelectionOnly : EditorWindow
{
    [Header("Simulation")]
    [SerializeField] private float fixedStep = 1f / 60f;
    [SerializeField] private float maxPreviewSeconds = 10f;
    [SerializeField] private bool runContinuously = true;
    [SerializeField] private bool hideOriginalsWhilePreviewing = true;

    [Header("Temporary Rigidbody Settings")]
    [SerializeField] private bool addRigidbodiesToSelectedIfMissing = true;
    [SerializeField] private bool forceSelectedRigidbodiesDynamic = true;
    [SerializeField] private bool useGravity = true;
    [SerializeField] private float linearDamping = 0.2f;
    [SerializeField] private float angularDamping = 0.5f;

    [Header("Collision Environment")]
    [SerializeField] private bool cloneNonSelectedCollidersAsStatic = true;
    [SerializeField] private bool includeTriggerColliders = false;

    private readonly List<PreviewItem> previewItems = new ();
    private readonly List<RendererState> hiddenOriginalRenderers = new ();

    private Scene previewScene;
    private PhysicsScene previewPhysicsScene;
    private bool isPreviewing;
    private bool isPaused;
    private double lastEditorTime;
    private float accumulatedTime;
    private float simulatedTime;

    [MenuItem("Tools/Debris Physics Placer/Selection Only Isolated Simulator")]
    public static void ShowWindow()
    {
        GetWindow<DebrisPhysicsPlacerSelectionOnly>("Debris Selection Sim");
    }

    private void OnEnable()
    {
        EditorApplication.update += EditorUpdate;
        SceneView.duringSceneGui += DuringSceneGUI;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
        SceneView.duringSceneGui -= DuringSceneGUI;
        CancelAndCleanup();
    }

    private void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "This version simulates only temporary clones in an Editor preview PhysicsScene. " +
            "It does not call Physics.Simulate on your real scene, and it does not move real objects until Bake.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(isPreviewing))
        {
            fixedStep = Mathf.Max(0.001f, EditorGUILayout.FloatField("Fixed Step", fixedStep));
            maxPreviewSeconds = Mathf.Max(0.1f, EditorGUILayout.FloatField("Max Preview Seconds", maxPreviewSeconds));
            runContinuously = EditorGUILayout.Toggle("Run Continuously", runContinuously);
            hideOriginalsWhilePreviewing = EditorGUILayout.Toggle("Hide Originals While Previewing", hideOriginalsWhilePreviewing);

            EditorGUILayout.Space();
            addRigidbodiesToSelectedIfMissing = EditorGUILayout.Toggle("Add Rigidbody If Missing", addRigidbodiesToSelectedIfMissing);
            forceSelectedRigidbodiesDynamic = EditorGUILayout.Toggle("Force Selected Dynamic", forceSelectedRigidbodiesDynamic);
            useGravity = EditorGUILayout.Toggle("Use Gravity", useGravity);
            linearDamping = Mathf.Max(0f, EditorGUILayout.FloatField("Linear Damping", linearDamping));
            angularDamping = Mathf.Max(0f, EditorGUILayout.FloatField("Angular Damping", angularDamping));

            EditorGUILayout.Space();
            cloneNonSelectedCollidersAsStatic = EditorGUILayout.Toggle("Use Scene Colliders As Static", cloneNonSelectedCollidersAsStatic);
            includeTriggerColliders = EditorGUILayout.Toggle("Include Trigger Colliders", includeTriggerColliders);
        }

        EditorGUILayout.Space();

        if (!isPreviewing)
        {
            if (GUILayout.Button("Start Selection-Only Preview", GUILayout.Height(32)))
                StartPreview();
        }
        else
        {
            EditorGUILayout.LabelField("Preview Time", simulatedTime.ToString("0.00") + " / " + maxPreviewSeconds.ToString("0.00") + " sec");

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(isPaused ? "Resume" : "Pause"))
                    isPaused = !isPaused;

                if (GUILayout.Button("Step Once"))
                    StepPreview(fixedStep);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bake / Keep Result", GUILayout.Height(30)))
                    BakeAndCleanup();

                if (GUILayout.Button("Cancel / Restore", GUILayout.Height(30)))
                    CancelAndCleanup();
            }
        }
    }

    private void StartPreview()
    {
        GameObject[] selectedObjects = Selection.gameObjects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            Debug.LogWarning("Select debris objects first.");
            return;
        }

        CancelAndCleanup();

        previewScene = EditorSceneManager.NewPreviewScene();

        previewPhysicsScene = previewScene.GetPhysicsScene();
        if (!previewPhysicsScene.IsValid())
        {
            Debug.LogError("Could not create a valid local PhysicsScene.");
            CancelAndCleanup();
            return;
        }

        HashSet<Transform> selectedRoots = new ();
        foreach (GameObject selected in selectedObjects)
        {
            if (selected != null)
                selectedRoots.Add(selected.transform);
        }

        if (cloneNonSelectedCollidersAsStatic)
            CloneSceneCollidersAsStatic(selectedRoots);

        foreach (GameObject original in selectedObjects)
        {
            if (original == null)
                continue;

            GameObject clone = Instantiate(original);
            clone.name = original.name + " [PHYSICS PREVIEW CLONE]";
            SceneManager.MoveGameObjectToScene(clone, previewScene);

            clone.transform.SetPositionAndRotation(original.transform.position, original.transform.rotation);
            clone.transform.localScale = original.transform.lossyScale;

            DisableScriptsOnClone(clone);
            PrepareSelectedCloneForPhysics(clone);

            previewItems.Add(new PreviewItem
            {
                original = original,
                clone = clone,
                startPosition = original.transform.position,
                startRotation = original.transform.rotation,
                startScale = original.transform.localScale
            });
        }

        if (hideOriginalsWhilePreviewing)
            HideOriginalSelectedRenderers();

        isPreviewing = true;
        isPaused = !runContinuously;
        simulatedTime = 0f;
        accumulatedTime = 0f;
        lastEditorTime = EditorApplication.timeSinceStartup;

        SceneView.RepaintAll();
    }

    private void EditorUpdate()
    {
        if (!isPreviewing || isPaused)
            return;

        double now = EditorApplication.timeSinceStartup;
        float delta = Mathf.Clamp((float)(now - lastEditorTime), 0f, 0.1f);
        lastEditorTime = now;

        accumulatedTime += delta;
        while (accumulatedTime >= fixedStep && simulatedTime < maxPreviewSeconds)
        {
            StepPreview(fixedStep);
            accumulatedTime -= fixedStep;
        }

        if (simulatedTime >= maxPreviewSeconds)
            isPaused = true;
    }

    private void StepPreview(float dt)
    {
        if (!isPreviewing || !previewPhysicsScene.IsValid())
            return;

        previewPhysicsScene.Simulate(dt);
        simulatedTime += dt;
        SceneView.RepaintAll();
        Repaint();
    }

    private void DuringSceneGUI(SceneView sceneView)
    {
        if (!isPreviewing)
            return;

        Event current = Event.current;
        if (current == null || current.type != EventType.Repaint)
            return;

        foreach (PreviewItem item in previewItems)
        {
            if (item.clone == null)
                continue;

            DrawCloneMeshes(item.clone);
        }
    }

    private static void DrawCloneMeshes(GameObject cloneRoot)
    {
        MeshFilter[] meshFilters = cloneRoot.GetComponentsInChildren<MeshFilter>(true);
        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
                continue;

            MeshRenderer renderer = meshFilter.GetComponent<MeshRenderer>();
            if (renderer == null || !renderer.enabled)
                continue;

            Material[] materials = renderer.sharedMaterials;
            int subMeshCount = Mathf.Max(1, meshFilter.sharedMesh.subMeshCount);
            for (int subMesh = 0; subMesh < subMeshCount; subMesh++)
            {
                Material material = materials != null && materials.Length > 0
                    ? materials[Mathf.Min(subMesh, materials.Length - 1)]
                    : null;

                if (material != null && material.SetPass(0))
                    Graphics.DrawMeshNow(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix, subMesh);
            }
        }
    }

    private void BakeAndCleanup()
    {
        if (!isPreviewing)
            return;

        Undo.SetCurrentGroupName("Bake Debris Physics Preview");
        int group = Undo.GetCurrentGroup();

        foreach (PreviewItem item in previewItems)
        {
            if (item.original == null || item.clone == null)
                continue;

            Undo.RecordObject(item.original.transform, "Bake Debris Physics Preview");
            item.original.transform.SetPositionAndRotation(item.clone.transform.position, item.clone.transform.rotation);
            EditorUtility.SetDirty(item.original);
        }

        RestoreOriginalSelectedRenderers();
        CleanupPreviewScene();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Undo.CollapseUndoOperations(group);
        SceneView.RepaintAll();
    }

    private void CancelAndCleanup()
    {
        RestoreOriginalSelectedRenderers();
        CleanupPreviewScene();
        SceneView.RepaintAll();
    }

    private void CleanupPreviewScene()
    {
        isPreviewing = false;
        isPaused = false;
        previewItems.Clear();
        hiddenOriginalRenderers.Clear();

        if (previewScene.IsValid())
        {
            if (EditorSceneManager.IsPreviewScene(previewScene))
                EditorSceneManager.ClosePreviewScene(previewScene);
            else
                EditorSceneManager.CloseScene(previewScene, true);
        }

        previewScene = default;
        previewPhysicsScene = default;
    }

    private void CloneSceneCollidersAsStatic(HashSet<Transform> selectedRoots)
    {
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (Collider sourceCollider in colliders)
        {
            if (sourceCollider == null || !sourceCollider.enabled)
                continue;

            if (!includeTriggerColliders && sourceCollider.isTrigger)
                continue;

            if (IsInsideSelectedHierarchy(sourceCollider.transform, selectedRoots))
                continue;

            GameObject staticClone = new GameObject(sourceCollider.gameObject.name + " [STATIC COLLIDER PREVIEW]");
            staticClone.hideFlags = HideFlags.HideAndDontSave;
            SceneManager.MoveGameObjectToScene(staticClone, previewScene);

            staticClone.transform.SetPositionAndRotation(sourceCollider.transform.position, sourceCollider.transform.rotation);
            staticClone.transform.localScale = sourceCollider.transform.lossyScale;

            Type colliderType = sourceCollider.GetType();
            Collider clonedCollider = staticClone.AddComponent(colliderType) as Collider;
            if (clonedCollider == null)
            {
                DestroyImmediate(staticClone);
                continue;
            }

            EditorUtility.CopySerialized(sourceCollider, clonedCollider);
            clonedCollider.enabled = true;
            clonedCollider.isTrigger = sourceCollider.isTrigger;

            // No Rigidbody is added here on purpose.
            // This makes every non-selected scene collider static in the preview physics scene.
        }
    }

    private static bool IsInsideSelectedHierarchy(Transform transform, HashSet<Transform> selectedRoots)
    {
        Transform current = transform;
        while (current != null)
        {
            if (selectedRoots.Contains(current))
                return true;
            current = current.parent;
        }
        return false;
    }

    private void PrepareSelectedCloneForPhysics(GameObject clone)
    {
        Rigidbody[] rigidbodies = clone.GetComponentsInChildren<Rigidbody>(true);

        if (rigidbodies.Length == 0 && addRigidbodiesToSelectedIfMissing)
        {
            Rigidbody rb = clone.AddComponent<Rigidbody>();
            ConfigurePreviewRigidbody(rb);
            return;
        }

        foreach (Rigidbody rb in rigidbodies)
        {
            ConfigurePreviewRigidbody(rb);
        }
    }

    private void ConfigurePreviewRigidbody(Rigidbody rb)
    {
        if (rb == null)
            return;

        if (forceSelectedRigidbodiesDynamic)
            rb.isKinematic = false;

        rb.useGravity = useGravity;
        rb.linearDamping = linearDamping;
        rb.angularDamping = angularDamping;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.Sleep();
        rb.WakeUp();
    }

    private static void DisableScriptsOnClone(GameObject cloneRoot)
    {
        MonoBehaviour[] behaviours = cloneRoot.GetComponentsInChildren<MonoBehaviour>(true);
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
                behaviour.enabled = false;
        }
    }

    private void HideOriginalSelectedRenderers()
    {
        foreach (PreviewItem item in previewItems)
        {
            if (item.original == null)
                continue;

            Renderer[] renderers = item.original.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                    continue;

                hiddenOriginalRenderers.Add(new RendererState
                {
                    renderer = renderer,
                    wasEnabled = renderer.enabled
                });

                renderer.enabled = false;
            }
        }
    }

    private void RestoreOriginalSelectedRenderers()
    {
        foreach (RendererState state in hiddenOriginalRenderers)
        {
            if (state.renderer != null)
                state.renderer.enabled = state.wasEnabled;
        }
    }

    private struct PreviewItem
    {
        public GameObject original;
        public GameObject clone;
        public Vector3 startPosition;
        public Quaternion startRotation;
        public Vector3 startScale;
    }

    private struct RendererState
    {
        public Renderer renderer;
        public bool wasEnabled;
    }
}
#endif
