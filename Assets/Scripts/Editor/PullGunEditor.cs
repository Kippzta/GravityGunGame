using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PullGun))]
public class PullGunEditor : Editor
{
    private enum ViewMode
    {
        Simple,
        Advanced
    }

    private static ViewMode viewMode = ViewMode.Simple;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);

        viewMode = (ViewMode)GUILayout.Toolbar(
            (int)viewMode,
            new[] { "Simple", "Advanced" },
            GUILayout.Height(28)
        );

        EditorGUILayout.Space(8);

        if (viewMode == ViewMode.Simple)
        {
            DrawSimpleInspector();
        }
        else
        {
            DrawDefaultInspector();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawSimpleInspector()
    {
        DrawPresetButtons();

        EditorGUILayout.Space(10);

        DrawSection("References");
        DrawProperty("playerCamera", "Player Camera");
        DrawProperty("holdPoint", "Hold Point");

        DrawSection("Layers");
        DrawProperty("aimBlockingLayers", "Aim Blocking Layers");
        DrawProperty("pullableLayers", "Pullable Layers");

        DrawSection("Tether Shooting");
        DrawProperty("useExtendingTether", "Use Extending Tether");
        DrawProperty("maxTetherLength", "Max Tether Length");
        DrawProperty("tetherExtendSpeed", "Tether Extend Speed");
        DrawProperty("initialTetherLength", "Initial Tether Length");
        DrawProperty("tetherFollowsAimWhileExtending", "Tether Follows Aim");
        DrawProperty("nonPullableObjectsBlockTether", "Non-Pullable Objects Block Tether");

        DrawSection("Swing Feel");
        DrawProperty("autoAdjustRopeForSwing", "Auto Adjust Rope For Swing");
        DrawProperty("swingRopeLengthMultiplier", "Swing Rope Length");
        DrawProperty("swingRopeShortenAmount", "Extra Rope Shortening");
        DrawProperty("ropeLengthAdjustSpeed", "Rope Tighten Speed");
        DrawProperty("tighteningPullAcceleration", "Tightening Pull Strength");
        DrawProperty("ropeTensionAcceleration", "Rope Tension Strength");
        DrawProperty("catchDampingDistance", "Catch Smooth Distance");
        DrawProperty("catchDampingStrength", "Catch Smooth Strength");

        DrawSection("Swing Control");
        DrawProperty("airborneSwingPumpForce", "Air Swing Pump Force");
        DrawProperty("airborneDirectControl", "Air Direct Control");
        DrawProperty("groundedPlayerSwingInfluence", "Grounded Swing Control");
        DrawProperty("wrongDirectionAirBrake", "Wrong Direction Brake");

        DrawSection("Safety Limits");
        DrawProperty("minRopeLength", "Min Rope Length");
        DrawProperty("useNoStretchConstraint", "Prevent Rope Stretch");
        DrawProperty("constraintTolerance", "Stretch Tolerance");
        DrawProperty("removeOnlyOutwardVelocity", "Remove Only Outward Velocity");
        DrawProperty("maxPlayerTetherVelocity", "Max Player Tether Velocity");
        DrawProperty("maxPlayerTetherAcceleration", "Max Player Tether Acceleration");
        DrawProperty("maxUpwardTetherAcceleration", "Max Upward Tether Acceleration");

        DrawSection("Debug");
        DrawProperty("debugEnabled", "Debug Enabled");
        DrawProperty("drawGameViewDebug", "Game View Debug");
        DrawProperty("drawSceneGizmos", "Scene Gizmos");
        DrawProperty("showDebugPanel", "Debug Panel");

        EditorGUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "Switch to Advanced to see every raw physics/debug value. The Simple view only shows the values you will usually tune.",
            MessageType.Info
        );
    }

    private void DrawPresetButtons()
    {
        DrawSection("Presets");

        EditorGUILayout.LabelField(
            "Click a preset, then tweak the simple values below.",
            EditorStyles.wordWrappedMiniLabel
        );

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Balanced"))
            {
                ApplyBalancedPreset();
            }

            if (GUILayout.Button("Arcade"))
            {
                ApplyArcadePreset();
            }

            if (GUILayout.Button("Smooth"))
            {
                ApplySmoothSwingPreset();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Tight"))
            {
                ApplyTightSwingPreset();
            }

            if (GUILayout.Button("Heavy"))
            {
                ApplyHeavySwingPreset();
            }
        }
    }

    private void ApplyBalancedPreset()
    {
        SetBool("autoAdjustRopeForSwing", true);
        SetFloat("swingRopeLengthMultiplier", 0.72f);
        SetFloat("swingRopeShortenAmount", 1.5f);
        SetFloat("ropeLengthAdjustSpeed", 18f);

        SetFloat("tighteningPullAcceleration", 38f);
        SetFloat("ropeTensionAcceleration", 12f);

        SetFloat("catchDampingDistance", 1.25f);
        SetFloat("catchDampingStrength", 7f);

        SetFloat("airborneSwingPumpForce", 24f);
        SetFloat("airborneDirectControl", 0.04f);
        SetFloat("groundedPlayerSwingInfluence", 16f);
        SetFloat("wrongDirectionAirBrake", 2f);

        SetFloat("maxPlayerTetherVelocity", 32f);
        SetFloat("maxPlayerTetherAcceleration", 85f);
        SetFloat("maxUpwardTetherAcceleration", 18f);

        SetBool("useNoStretchConstraint", true);
        SetBool("removeOnlyOutwardVelocity", true);
        SetFloat("constraintTolerance", 0.01f);
    }

    private void ApplyArcadePreset()
    {
        SetBool("autoAdjustRopeForSwing", true);
        SetFloat("swingRopeLengthMultiplier", 0.62f);
        SetFloat("swingRopeShortenAmount", 2.2f);
        SetFloat("ropeLengthAdjustSpeed", 26f);

        SetFloat("tighteningPullAcceleration", 55f);
        SetFloat("ropeTensionAcceleration", 20f);

        SetFloat("catchDampingDistance", 1.0f);
        SetFloat("catchDampingStrength", 8f);

        SetFloat("airborneSwingPumpForce", 34f);
        SetFloat("airborneDirectControl", 0.08f);
        SetFloat("groundedPlayerSwingInfluence", 22f);
        SetFloat("wrongDirectionAirBrake", 1.5f);

        SetFloat("maxPlayerTetherVelocity", 38f);
        SetFloat("maxPlayerTetherAcceleration", 110f);
        SetFloat("maxUpwardTetherAcceleration", 24f);

        SetBool("useNoStretchConstraint", true);
        SetBool("removeOnlyOutwardVelocity", true);
        SetFloat("constraintTolerance", 0.015f);
    }

    private void ApplySmoothSwingPreset()
    {
        SetBool("autoAdjustRopeForSwing", true);
        SetFloat("swingRopeLengthMultiplier", 0.78f);
        SetFloat("swingRopeShortenAmount", 1.0f);
        SetFloat("ropeLengthAdjustSpeed", 12f);

        SetFloat("tighteningPullAcceleration", 26f);
        SetFloat("ropeTensionAcceleration", 8f);

        SetFloat("catchDampingDistance", 1.75f);
        SetFloat("catchDampingStrength", 5f);

        SetFloat("airborneSwingPumpForce", 20f);
        SetFloat("airborneDirectControl", 0.035f);
        SetFloat("groundedPlayerSwingInfluence", 13f);
        SetFloat("wrongDirectionAirBrake", 2.5f);

        SetFloat("maxPlayerTetherVelocity", 30f);
        SetFloat("maxPlayerTetherAcceleration", 70f);
        SetFloat("maxUpwardTetherAcceleration", 14f);

        SetBool("useNoStretchConstraint", true);
        SetBool("removeOnlyOutwardVelocity", true);
        SetFloat("constraintTolerance", 0.015f);
    }

    private void ApplyTightSwingPreset()
    {
        SetBool("autoAdjustRopeForSwing", true);
        SetFloat("swingRopeLengthMultiplier", 0.55f);
        SetFloat("swingRopeShortenAmount", 2.8f);
        SetFloat("ropeLengthAdjustSpeed", 32f);

        SetFloat("tighteningPullAcceleration", 70f);
        SetFloat("ropeTensionAcceleration", 24f);

        SetFloat("catchDampingDistance", 0.75f);
        SetFloat("catchDampingStrength", 10f);

        SetFloat("airborneSwingPumpForce", 30f);
        SetFloat("airborneDirectControl", 0.05f);
        SetFloat("groundedPlayerSwingInfluence", 20f);
        SetFloat("wrongDirectionAirBrake", 2f);

        SetFloat("maxPlayerTetherVelocity", 34f);
        SetFloat("maxPlayerTetherAcceleration", 125f);
        SetFloat("maxUpwardTetherAcceleration", 22f);

        SetBool("useNoStretchConstraint", true);
        SetBool("removeOnlyOutwardVelocity", true);
        SetFloat("constraintTolerance", 0.008f);
    }

    private void ApplyHeavySwingPreset()
    {
        SetBool("autoAdjustRopeForSwing", true);
        SetFloat("swingRopeLengthMultiplier", 0.85f);
        SetFloat("swingRopeShortenAmount", 0.5f);
        SetFloat("ropeLengthAdjustSpeed", 8f);

        SetFloat("tighteningPullAcceleration", 18f);
        SetFloat("ropeTensionAcceleration", 6f);

        SetFloat("catchDampingDistance", 2.0f);
        SetFloat("catchDampingStrength", 4f);

        SetFloat("airborneSwingPumpForce", 14f);
        SetFloat("airborneDirectControl", 0.02f);
        SetFloat("groundedPlayerSwingInfluence", 10f);
        SetFloat("wrongDirectionAirBrake", 3f);

        SetFloat("maxPlayerTetherVelocity", 26f);
        SetFloat("maxPlayerTetherAcceleration", 55f);
        SetFloat("maxUpwardTetherAcceleration", 10f);

        SetBool("useNoStretchConstraint", true);
        SetBool("removeOnlyOutwardVelocity", true);
        SetFloat("constraintTolerance", 0.02f);
    }

    private void DrawSection(string title)
    {
        EditorGUILayout.Space(8);

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12
        };

        EditorGUILayout.LabelField(title, style);

        Rect rect = GUILayoutUtility.GetRect(1f, 1f);
        EditorGUI.DrawRect(rect, new Color(0.25f, 0.25f, 0.25f, 1f));
    }

    private void DrawProperty(string propertyName, string label)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label));
    }

    private void SetFloat(string propertyName, float value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            return;
        }

        property.floatValue = value;
    }

    private void SetBool(string propertyName, bool value)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            return;
        }

        property.boolValue = value;
    }
}