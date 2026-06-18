using UnityEditor;
using UnityEngine;

public class DropToFloorTool : EditorWindow
{
    private LayerMask groundLayers = ~0;
    private float rayStartHeight = 1000f;
    private float surfaceOffset = 0f;

    private bool alignToNormal = true;
    private bool preserveYaw = true;

    private bool randomizeRotationOnDrop = false;
    private Vector2 randomYRotationRange = new Vector2(0f, 360f);

    [MenuItem("Tools/Placement/Drop To Floor")]
    public static void ShowWindow()
    {
        GetWindow<DropToFloorTool>("Drop To Floor");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Drop Selected Objects To Floor", EditorStyles.boldLabel);

        groundLayers = LayerMaskField("Ground Layers", groundLayers);
        rayStartHeight = EditorGUILayout.FloatField("Ray Start Height", rayStartHeight);
        surfaceOffset = EditorGUILayout.FloatField("Surface Offset", surfaceOffset);

        EditorGUILayout.Space();

        alignToNormal = EditorGUILayout.Toggle("Align To Surface Normal", alignToNormal);
        preserveYaw = EditorGUILayout.Toggle("Preserve Facing Direction", preserveYaw);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Random Rotation", EditorStyles.boldLabel);

        randomYRotationRange = EditorGUILayout.Vector2Field(
            "Random Y Rotation Range",
            randomYRotationRange
        );

        randomizeRotationOnDrop = EditorGUILayout.Toggle(
            "Randomize Rotation On Drop",
            randomizeRotationOnDrop
        );

        using (new EditorGUI.DisabledScope(Selection.transforms.Length == 0))
        {
            if (GUILayout.Button("Randomize Selected Rotation"))
            {
                RandomizeSelectedRotations();
            }
        }

        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(Selection.transforms.Length == 0))
        {
            if (GUILayout.Button("Drop Selected Objects"))
            {
                DropSelectedObjects();
            }
        }

        EditorGUILayout.HelpBox(
            "Select one or more GameObjects, then click Drop Selected Objects. " +
            "The tool raycasts downward and places each object on the first collider hit. " +
            "Random rotation is applied around the surface normal when aligning to slopes.",
            MessageType.Info
        );
    }

    private void DropSelectedObjects()
    {
        foreach (Transform selected in Selection.transforms)
        {
            DropObject(selected);
        }
    }

    private void DropObject(Transform target)
    {
        Undo.RecordObject(target, "Drop To Floor");

        Vector3 rayOrigin = target.position + Vector3.up * rayStartHeight;
        Ray ray = new Ray(rayOrigin, Vector3.down);

        float rayDistance = rayStartHeight * 2f;

        if (Physics.Raycast(ray, out RaycastHit hit, rayDistance, groundLayers))
        {
            float yaw = target.eulerAngles.y;

            if (randomizeRotationOnDrop)
            {
                yaw = GetRandomYRotation();
            }

            Vector3 newPosition = hit.point + hit.normal * surfaceOffset;
            target.position = newPosition;

            if (alignToNormal)
            {
                AlignRotationToNormal(target, hit.normal, yaw);
            }
            else if (randomizeRotationOnDrop)
            {
                target.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            EditorUtility.SetDirty(target);
        }
        else
        {
            Debug.LogWarning($"No floor found below {target.name}", target);
        }
    }

    private void RandomizeSelectedRotations()
    {
        foreach (Transform selected in Selection.transforms)
        {
            Undo.RecordObject(selected, "Randomize Rotation");

            float yaw = GetRandomYRotation();

            if (alignToNormal)
            {
                Vector3 up = selected.up;
                AlignRotationToNormal(selected, up, yaw);
            }
            else
            {
                selected.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            EditorUtility.SetDirty(selected);
        }
    }

    private float GetRandomYRotation()
    {
        float min = Mathf.Min(randomYRotationRange.x, randomYRotationRange.y);
        float max = Mathf.Max(randomYRotationRange.x, randomYRotationRange.y);

        return Random.Range(min, max);
    }

    private void AlignRotationToNormal(Transform target, Vector3 normal, float yaw)
    {
        if (preserveYaw)
        {
            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 forward = Vector3.ProjectOnPlane(
                yawRotation * Vector3.forward,
                normal
            ).normalized;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.ProjectOnPlane(
                    yawRotation * Vector3.right,
                    normal
                ).normalized;
            }

            target.rotation = Quaternion.LookRotation(forward, normal);
        }
        else
        {
            Quaternion normalAlignment = Quaternion.FromToRotation(Vector3.up, normal);
            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);

            target.rotation = normalAlignment * yawRotation;
        }
    }

    private static LayerMask LayerMaskField(string label, LayerMask selected)
    {
        string[] layerNames = new string[32];
        int[] layerNumbers = new int[32];

        int layerCount = 0;

        for (int i = 0; i < 32; i++)
        {
            string layerName = LayerMask.LayerToName(i);

            if (!string.IsNullOrEmpty(layerName))
            {
                layerNames[layerCount] = layerName;
                layerNumbers[layerCount] = i;
                layerCount++;
            }
        }

        string[] displayedNames = new string[layerCount];
        int[] displayedNumbers = new int[layerCount];

        for (int i = 0; i < layerCount; i++)
        {
            displayedNames[i] = layerNames[i];
            displayedNumbers[i] = layerNumbers[i];
        }

        int maskWithoutEmpty = 0;

        for (int i = 0; i < displayedNumbers.Length; i++)
        {
            if (((1 << displayedNumbers[i]) & selected.value) != 0)
            {
                maskWithoutEmpty |= 1 << i;
            }
        }

        maskWithoutEmpty = EditorGUILayout.MaskField(
            label,
            maskWithoutEmpty,
            displayedNames
        );

        int finalMask = 0;

        for (int i = 0; i < displayedNumbers.Length; i++)
        {
            if ((maskWithoutEmpty & (1 << i)) != 0)
            {
                finalMask |= 1 << displayedNumbers[i];
            }
        }

        selected.value = finalMask;
        return selected;
    }
}