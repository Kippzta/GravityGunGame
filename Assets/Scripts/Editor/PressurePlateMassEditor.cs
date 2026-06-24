using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PressurePlateMass))]
public class PressurePlateMassEditor : Editor
{
    private SerializedProperty doorsProperty;

    private bool isPickingDoor;
    private static PressurePlateMassEditor activePicker;

    private void OnEnable()
    {
        doorsProperty = serializedObject.FindProperty("doors");
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;

        if (activePicker == this)
        {
            activePicker = null;
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDoorPickerSection();

        EditorGUILayout.Space(8);
        DrawDefaultInspectorExceptDoors();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawDoorPickerSection()
    {
        EditorGUILayout.LabelField("Door Picker", EditorStyles.boldLabel);

        if (doorsProperty == null)
        {
            EditorGUILayout.HelpBox(
                "Could not find a serialized field named 'doors' on PressurePlateMass. Make sure your PressurePlateMass script has: [SerializeField] private SimpleMassDoor[] doors;",
                MessageType.Error
            );

            return;
        }

        EditorGUILayout.PropertyField(doorsProperty, new GUIContent("Assigned Doors"), true);

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            GUI.backgroundColor = isPickingDoor ? Color.green : Color.white;

            if (GUILayout.Button(isPickingDoor ? "Picking Door... Click Scene Object" : "Pick Door From Scene", GUILayout.Height(28)))
            {
                TogglePicking();
            }

            GUI.backgroundColor = Color.white;

            if (GUILayout.Button("Clear Doors", GUILayout.Height(28)))
            {
                Undo.RecordObject(target, "Clear Pressure Plate Doors");
                doorsProperty.ClearArray();
                serializedObject.ApplyModifiedProperties();
            }
        }

        EditorGUILayout.HelpBox(
            "Click 'Pick Door From Scene', then click a door mesh/object in the Scene view. The picker will search the clicked object, its parents, and its children for SimpleMassDoor.",
            MessageType.Info
        );
    }

    private void DrawDefaultInspectorExceptDoors()
    {
        SerializedProperty property = serializedObject.GetIterator();

        bool enterChildren = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (property.name == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(property, true);
                }

                continue;
            }

            if (property.name == "doors")
            {
                continue;
            }

            EditorGUILayout.PropertyField(property, true);
        }
    }

    private void TogglePicking()
    {
        isPickingDoor = !isPickingDoor;

        if (isPickingDoor)
        {
            activePicker = this;
            SceneView.RepaintAll();
        }
        else if (activePicker == this)
        {
            activePicker = null;
            SceneView.RepaintAll();
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isPickingDoor || activePicker != this)
        {
            return;
        }

        Event currentEvent = Event.current;

        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(12f, 12f, 330f, 70f), EditorStyles.helpBox);
        GUILayout.Label("Pressure Plate Door Picker", EditorStyles.boldLabel);
        GUILayout.Label("Click a door object in the Scene view. Press Esc to cancel.");
        GUILayout.EndArea();

        Handles.EndGUI();

        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape)
        {
            isPickingDoor = false;
            activePicker = null;
            currentEvent.Use();
            SceneView.RepaintAll();
            return;
        }

        HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));

        if (currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
        {
            return;
        }

        GameObject pickedObject = HandleUtility.PickGameObject(currentEvent.mousePosition, false);

        if (pickedObject == null)
        {
            Debug.Log("PressurePlateMass picker did not hit a GameObject.");
            currentEvent.Use();
            return;
        }

        SimpleMassDoor pickedDoor = FindDoorOnPickedObject(pickedObject);

        if (pickedDoor == null)
        {
            Debug.LogWarning(
                $"Picked object '{pickedObject.name}' does not have a SimpleMassDoor on itself, its parents, or its children.",
                pickedObject
            );

            currentEvent.Use();
            return;
        }

        AddDoor(pickedDoor);

        isPickingDoor = false;
        activePicker = null;

        currentEvent.Use();
        SceneView.RepaintAll();
    }

    private SimpleMassDoor FindDoorOnPickedObject(GameObject pickedObject)
    {
        SimpleMassDoor door = pickedObject.GetComponent<SimpleMassDoor>();

        if (door != null)
        {
            return door;
        }

        door = pickedObject.GetComponentInParent<SimpleMassDoor>();

        if (door != null)
        {
            return door;
        }

        door = pickedObject.GetComponentInChildren<SimpleMassDoor>();

        return door;
    }

    private void AddDoor(SimpleMassDoor door)
    {
        if (door == null || doorsProperty == null)
        {
            return;
        }

        serializedObject.Update();

        for (int i = 0; i < doorsProperty.arraySize; i++)
        {
            SerializedProperty existingDoor = doorsProperty.GetArrayElementAtIndex(i);

            if (existingDoor.objectReferenceValue == door)
            {
                Debug.Log($"Door '{door.name}' is already assigned.", door);
                return;
            }
        }

        Undo.RecordObject(target, "Add Door To Pressure Plate");

        int newIndex = doorsProperty.arraySize;
        doorsProperty.InsertArrayElementAtIndex(newIndex);
        doorsProperty.GetArrayElementAtIndex(newIndex).objectReferenceValue = door;

        serializedObject.ApplyModifiedProperties();

        EditorUtility.SetDirty(target);

        Debug.Log($"Assigned door '{door.name}' to pressure plate '{target.name}'.", door);
    }
}