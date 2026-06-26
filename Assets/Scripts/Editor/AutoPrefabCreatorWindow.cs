#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AutoPrefabCreatorWindow : EditorWindow
{
    private DefaultAsset outputFolder;

    private int prefabLayer = 0;

    private bool contributeGI = true;
    private bool occluderStatic = true;
    private bool batchingStatic = true;
    private bool navigationStatic = false;
    private bool occludeeStatic = true;
    private bool reflectionProbeStatic = true;

    private bool overwriteExistingPrefabs = true;
    private bool includeInactiveChildren = true;
    private bool useExactMaterialNameMatching = true;
    private bool removeMeshRenderersFromUCX = true;
    private bool removeMeshFiltersFromUCX = true;

    private Vector2 scroll;

    [MenuItem("Tools/Prefab Tools/Auto Prefab Creator")]
    public static void Open()
    {
        AutoPrefabCreatorWindow window = GetWindow<AutoPrefabCreatorWindow>();
        window.titleContent = new GUIContent("Auto Prefab Creator");
        window.minSize = new Vector2(430, 500);
        window.Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Auto Prefab Creator", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Select one or more FBX/model assets in the Project window, choose an output folder, then press Create Prefabs.",
            MessageType.Info
        );

        EditorGUILayout.Space(8);

        DrawOutputSettings();
        DrawPrefabSettings();
        DrawStaticSettings();
        DrawAdvancedSettings();
        DrawSelectedModelsPreview();

        EditorGUILayout.Space(12);

        using (new EditorGUI.DisabledScope(!CanCreatePrefabs()))
        {
            if (GUILayout.Button("Create Prefabs From Selected FBXes", GUILayout.Height(34)))
            {
                CreatePrefabsFromSelection();
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawOutputSettings()
    {
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);

        outputFolder = (DefaultAsset)EditorGUILayout.ObjectField(
            "Prefab Output Folder",
            outputFolder,
            typeof(DefaultAsset),
            false
        );

        if (GUILayout.Button("Use Currently Selected Folder"))
        {
            DefaultAsset selectedFolder = GetSelectedFolder();

            if (selectedFolder != null)
            {
                outputFolder = selectedFolder;
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "No Folder Selected",
                    "Select a folder in the Project window first.",
                    "OK"
                );
            }
        }

        if (outputFolder != null && !IsValidFolderAsset(outputFolder))
        {
            EditorGUILayout.HelpBox("The selected output asset is not a folder.", MessageType.Warning);
        }

        overwriteExistingPrefabs = EditorGUILayout.ToggleLeft("Overwrite Existing Prefabs", overwriteExistingPrefabs);

        EditorGUILayout.HelpBox(
            "Prefab names are created from the FBX name. Example: SM_Crate_01.fbx becomes PRE_Crate_01.prefab.",
            MessageType.None
        );
    }

    private void DrawPrefabSettings()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Prefab Settings", EditorStyles.boldLabel);

        prefabLayer = EditorGUILayout.LayerField("Prefab Layer", prefabLayer);
    }

    private void DrawStaticSettings()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Static Settings", EditorStyles.boldLabel);

        contributeGI = EditorGUILayout.ToggleLeft("Contribute GI", contributeGI);
        occluderStatic = EditorGUILayout.ToggleLeft("Occluder Static", occluderStatic);
        batchingStatic = EditorGUILayout.ToggleLeft("Batching Static", batchingStatic);
        navigationStatic = EditorGUILayout.ToggleLeft("Navigation Static", navigationStatic);
        occludeeStatic = EditorGUILayout.ToggleLeft("Occludee Static", occludeeStatic);
        reflectionProbeStatic = EditorGUILayout.ToggleLeft("Reflection Probe Static", reflectionProbeStatic);
    }

    private void DrawAdvancedSettings()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Advanced", EditorStyles.boldLabel);

        includeInactiveChildren = EditorGUILayout.ToggleLeft("Include Inactive Children", includeInactiveChildren);
        useExactMaterialNameMatching = EditorGUILayout.ToggleLeft("Exact Material Name Matching", useExactMaterialNameMatching);
        removeMeshRenderersFromUCX = EditorGUILayout.ToggleLeft("Remove Mesh Renderers From UCX Children", removeMeshRenderersFromUCX);
        removeMeshFiltersFromUCX = EditorGUILayout.ToggleLeft("Remove Mesh Filters From UCX Children", removeMeshFiltersFromUCX);

        EditorGUILayout.HelpBox(
            "UCX children are detected by name. Example: UCX_SM_Crate_01. They will become invisible collider children with BoxCollider only.",
            MessageType.None
        );
    }

    private void DrawSelectedModelsPreview()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Selected FBX / Model Assets", EditorStyles.boldLabel);

        List<string> selectedModelPaths = GetSelectedModelPaths();

        if (selectedModelPaths.Count == 0)
        {
            EditorGUILayout.HelpBox("No FBX/model assets selected.", MessageType.Warning);
            return;
        }

        foreach (string path in selectedModelPaths)
        {
            string sourceName = Path.GetFileNameWithoutExtension(path);
            string prefabName = ConvertToPrefabName(sourceName);

            EditorGUILayout.LabelField(sourceName + "  ->  " + prefabName + ".prefab");
        }
    }

    private bool CanCreatePrefabs()
    {
        if (outputFolder == null)
            return false;

        if (!IsValidFolderAsset(outputFolder))
            return false;

        return GetSelectedModelPaths().Count > 0;
    }

    private void CreatePrefabsFromSelection()
    {
        string outputFolderPath = AssetDatabase.GetAssetPath(outputFolder);

        if (string.IsNullOrEmpty(outputFolderPath) || !AssetDatabase.IsValidFolder(outputFolderPath))
        {
            EditorUtility.DisplayDialog("Invalid Output Folder", "Please select a valid folder inside Assets.", "OK");
            return;
        }

        List<string> selectedModelPaths = GetSelectedModelPaths();

        if (selectedModelPaths.Count == 0)
        {
            EditorUtility.DisplayDialog("No FBX Selected", "Select one or more FBX/model assets in the Project window.", "OK");
            return;
        }

        Dictionary<string, Material> unityMaterialsByName = BuildMaterialLookup();

        int createdCount = 0;
        int skippedCount = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (string modelPath in selectedModelPaths)
            {
                GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

                if (modelAsset == null)
                {
                    skippedCount++;
                    continue;
                }

                string sourceName = Path.GetFileNameWithoutExtension(modelPath);
                string prefabName = ConvertToPrefabName(sourceName);
                string prefabPath = outputFolderPath + "/" + prefabName + ".prefab";

                if (!overwriteExistingPrefabs && File.Exists(prefabPath))
                {
                    skippedCount++;
                    continue;
                }

                GameObject instance = null;

                try
                {
                    instance = CreateWorkingInstance(modelAsset);

                    if (instance == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    instance.name = prefabName;

                    SetLayerRecursively(instance, prefabLayer);
                    SetStaticFlagsRecursively(instance, BuildStaticFlags());

                    AssignMatchingUnityMaterials(instance, unityMaterialsByName);
                    ProcessUCXChildren(instance);

                    PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                    createdCount++;
                }
                finally
                {
                    if (instance != null)
                    {
                        DestroyImmediate(instance);
                    }
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog(
            "Prefab Creation Finished",
            "Created: " + createdCount + "\nSkipped: " + skippedCount,
            "OK"
        );
    }

    private GameObject CreateWorkingInstance(GameObject modelAsset)
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;

        if (instance != null)
            return instance;

        return Instantiate(modelAsset);
    }

    private string ConvertToPrefabName(string sourceName)
    {
        if (string.IsNullOrEmpty(sourceName))
            return "PRE_NewPrefab";

        if (sourceName.StartsWith("SM_", System.StringComparison.OrdinalIgnoreCase))
            return "PRE_" + sourceName.Substring(3);

        if (sourceName.StartsWith("PRE_", System.StringComparison.OrdinalIgnoreCase))
            return sourceName;

        return "PRE_" + sourceName;
    }

    private void AssignMatchingUnityMaterials(GameObject root, Dictionary<string, Material> unityMaterialsByName)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(includeInactiveChildren);

        foreach (Renderer renderer in renderers)
        {
            if (IsUCXObject(renderer.gameObject))
                continue;

            Material[] currentMaterials = renderer.sharedMaterials;

            for (int i = 0; i < currentMaterials.Length; i++)
            {
                Material importedMaterial = currentMaterials[i];

                if (importedMaterial == null)
                    continue;

                string importedName = CleanMaterialName(importedMaterial.name);

                if (unityMaterialsByName.TryGetValue(importedName, out Material replacementMaterial))
                {
                    currentMaterials[i] = replacementMaterial;
                }
            }

            renderer.sharedMaterials = currentMaterials;
        }
    }

    private Dictionary<string, Material> BuildMaterialLookup()
    {
        Dictionary<string, Material> lookup = new Dictionary<string, Material>();

        string[] materialGuids = AssetDatabase.FindAssets("t:Material");

        foreach (string guid in materialGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
                continue;

            string materialName = CleanMaterialName(material.name);

            if (!lookup.ContainsKey(materialName))
            {
                lookup.Add(materialName, material);
            }
        }

        return lookup;
    }

    private string CleanMaterialName(string materialName)
    {
        if (string.IsNullOrEmpty(materialName))
            return string.Empty;

        string cleaned = materialName.Trim();

        if (!useExactMaterialNameMatching)
        {
            cleaned = cleaned.Replace(" (Instance)", "");
            cleaned = cleaned.Replace("(Instance)", "");
        }

        return cleaned;
    }

    private void ProcessUCXChildren(GameObject root)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(includeInactiveChildren);

        foreach (Transform child in children)
        {
            GameObject childObject = child.gameObject;

            if (childObject == root)
                continue;

            if (!IsUCXObject(childObject))
                continue;

            Mesh mesh = GetMeshFromObject(childObject);

            BoxCollider boxCollider = childObject.GetComponent<BoxCollider>();

            if (boxCollider == null)
                boxCollider = childObject.AddComponent<BoxCollider>();

            if (mesh != null)
            {
                boxCollider.center = mesh.bounds.center;
                boxCollider.size = mesh.bounds.size;
            }

            RemoveUnwantedComponentsFromUCX(childObject, boxCollider);
        }
    }

    private Mesh GetMeshFromObject(GameObject obj)
    {
        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();

        if (meshFilter != null && meshFilter.sharedMesh != null)
            return meshFilter.sharedMesh;

        SkinnedMeshRenderer skinnedMeshRenderer = obj.GetComponent<SkinnedMeshRenderer>();

        if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
            return skinnedMeshRenderer.sharedMesh;

        return null;
    }

    private void RemoveUnwantedComponentsFromUCX(GameObject obj, BoxCollider boxCollider)
    {
        Component[] components = obj.GetComponents<Component>();

        foreach (Component component in components)
        {
            if (component == null)
                continue;

            if (component is Transform)
                continue;

            if (component == boxCollider)
                continue;

            if (!removeMeshRenderersFromUCX && component is Renderer)
                continue;

            if (!removeMeshFiltersFromUCX && component is MeshFilter)
                continue;

            DestroyImmediate(component);
        }

        if (obj.GetComponent<BoxCollider>() == null)
        {
            obj.AddComponent<BoxCollider>();
        }
    }

    private bool IsUCXObject(GameObject obj)
    {
        if (obj == null)
            return false;

        return obj.name.StartsWith("UCX", System.StringComparison.OrdinalIgnoreCase);
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        obj.layer = layer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void SetStaticFlagsRecursively(GameObject obj, StaticEditorFlags flags)
    {
        GameObjectUtility.SetStaticEditorFlags(obj, flags);

        foreach (Transform child in obj.transform)
        {
            SetStaticFlagsRecursively(child.gameObject, flags);
        }
    }

    private StaticEditorFlags BuildStaticFlags()
    {
        StaticEditorFlags flags = 0;

        if (contributeGI)
            flags |= StaticEditorFlags.ContributeGI;

        if (occluderStatic)
            flags |= StaticEditorFlags.OccluderStatic;

        if (batchingStatic)
            flags |= StaticEditorFlags.BatchingStatic;

        if (navigationStatic)
            flags |= StaticEditorFlags.NavigationStatic;

        if (occludeeStatic)
            flags |= StaticEditorFlags.OccludeeStatic;

        if (reflectionProbeStatic)
            flags |= StaticEditorFlags.ReflectionProbeStatic;

        return flags;
    }

    private List<string> GetSelectedModelPaths()
    {
        List<string> paths = new List<string>();

        foreach (Object selectedObject in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(selectedObject);

            if (string.IsNullOrEmpty(path))
                continue;

            if (AssetDatabase.IsValidFolder(path))
                continue;

            if (!IsModelAssetPath(path))
                continue;

            paths.Add(path);
        }

        return paths;
    }

    private bool IsModelAssetPath(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();

        return extension == ".fbx" ||
               extension == ".obj" ||
               extension == ".dae" ||
               extension == ".blend";
    }

    private DefaultAsset GetSelectedFolder()
    {
        foreach (Object selectedObject in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(selectedObject);

            if (string.IsNullOrEmpty(path))
                continue;

            if (AssetDatabase.IsValidFolder(path))
            {
                return selectedObject as DefaultAsset;
            }
        }

        return null;
    }

    private bool IsValidFolderAsset(DefaultAsset asset)
    {
        if (asset == null)
            return false;

        string path = AssetDatabase.GetAssetPath(asset);
        return AssetDatabase.IsValidFolder(path);
    }
}
#endif