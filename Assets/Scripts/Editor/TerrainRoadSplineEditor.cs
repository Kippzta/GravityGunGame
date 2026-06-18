// Copyright (c) Cortopia Studios. All rights reserved.
// This unpublished material is proprietary to Cortopia Studios.
// The methods and techniques described herein are considered trade secrets
// and/or confidential. Reproduction or distribution, in whole or in part, is
// forbidden except by express written permission of Cortopia Studios.

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace Cortopia.Scripts.Terrain.Editor
{
    [CustomEditor(typeof(TerrainRoadSpline))]
    public sealed class TerrainRoadSplineEditor : UnityEditor.Editor
    {
        private List<TerrainRoadSpline.RoadSample> _cachedPreviewSamples;
        private int _cachedPreviewVersion = -1;
        private int _cachedSplineHash = -1;
        private double _nextSplineHashCheckTime;

        private TerrainRoadSpline Tool => (TerrainRoadSpline) this.target;

        private void OnSceneGUI()
        {
            this.Tool.SyncWidthData();

            this.DrawRoadPreview();
            this.DrawWidthHandles();
            this.HandleShiftClickCreation();
        }

        public override void OnInspectorGUI()
        {
            this.serializedObject.Update();

            this.DrawDefaultInspector();

            EditorGUILayout.Space(12);

            using (new EditorGUI.DisabledScope(!this.Tool.terrain))
            {
                if (GUILayout.Button("Generate Terrain Roads", GUILayout.Height(34)))
                {
                    if (this.Tool.terrain && this.Tool.terrain.terrainData)
                    {
                        Undo.RegisterCompleteObjectUndo(this.Tool.terrain.terrainData, "Generate Terrain Roads");
                    }

                    this.Tool.GenerateTerrainRoads();
                }
            }

            EditorGUILayout.HelpBox(
                "Scene controls:\n" + "� Shift + Left Click on terrain: add point to current spline.\n" + "� Ctrl + Shift + Left Click on terrain: start a new spline.\n" +
                "� Alt + Shift + Left Click on terrain: insert point between nearest spline points.\n" +
                "� Drag blue width handles beside spline knots to change width.\n" + "� Terrain is only changed when Generate Terrain Roads is clicked.\n\n" +
                "Performance tips:\n" + "� Increase Preview Sample Spacing while editing.\n" + "� Disable Draw Preview Fill for large road networks.\n" +
                "� Keep Draw Width Labels disabled unless needed.\n" + "� Increase Width Handle Step if you have many knots.", MessageType.Info);

            this.serializedObject.ApplyModifiedProperties();

            if (GUI.changed)
            {
                this.Tool.SyncWidthData();
                this.Tool.MarkPreviewDirty();
                EditorUtility.SetDirty(this.Tool);
            }
        }

        private List<TerrainRoadSpline.RoadSample> GetCachedPreviewSamples()
        {
            bool needsRebuild = this._cachedPreviewSamples == null || this._cachedPreviewVersion != this.Tool.EditorPreviewVersion;

            bool shouldCheckSplineHash = this.ShouldCheckSplineHash();

            int splineHash = this._cachedSplineHash;

            if (shouldCheckSplineHash || needsRebuild)
            {
                splineHash = this.CalculateSplineHash();
            }

            if (splineHash != this._cachedSplineHash)
            {
                needsRebuild = true;
            }

            if (needsRebuild)
            {
                this._cachedPreviewSamples = this.Tool.BuildSamples(this.Tool.previewSampleSpacing);
                this._cachedPreviewVersion = this.Tool.EditorPreviewVersion;
                this._cachedSplineHash = splineHash;
            }

            return this._cachedPreviewSamples;
        }

        private bool ShouldCheckSplineHash()
        {
            double now = EditorApplication.timeSinceStartup;

            if (now < this._nextSplineHashCheckTime)
            {
                return false;
            }

            this._nextSplineHashCheckTime = now + Mathf.Max(0.05f, this.Tool.splineChangeCheckInterval);
            return true;
        }

        private int CalculateSplineHash()
        {
            SplineContainer container = this.Tool.Container;

            if (container == null)
            {
                return 0;
            }

            unchecked
            {
                int hash = 17;

                Transform t = container.transform;

                hash = hash * 31 + container.Splines.Count;

                hash = hash * 31 + Mathf.RoundToInt(this.Tool.previewSampleSpacing * 100f);
                hash = hash * 31 + Mathf.RoundToInt(this.Tool.defaultWidth * 100f);

                hash = hash * 31 + Mathf.RoundToInt(t.position.x * 100f);
                hash = hash * 31 + Mathf.RoundToInt(t.position.y * 100f);
                hash = hash * 31 + Mathf.RoundToInt(t.position.z * 100f);

                hash = hash * 31 + Mathf.RoundToInt(t.eulerAngles.x * 100f);
                hash = hash * 31 + Mathf.RoundToInt(t.eulerAngles.y * 100f);
                hash = hash * 31 + Mathf.RoundToInt(t.eulerAngles.z * 100f);

                hash = hash * 31 + Mathf.RoundToInt(t.lossyScale.x * 100f);
                hash = hash * 31 + Mathf.RoundToInt(t.lossyScale.y * 100f);
                hash = hash * 31 + Mathf.RoundToInt(t.lossyScale.z * 100f);

                for (int s = 0; s < container.Splines.Count; s++)
                {
                    Spline spline = container.Splines[s];

                    hash = hash * 31 + spline.Count;

                    for (int k = 0; k < spline.Count; k++)
                    {
                        BezierKnot knot = spline[k];

                        hash = hash * 31 + Mathf.RoundToInt(knot.Position.x * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(knot.Position.y * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(knot.Position.z * 100f);

                        hash = hash * 31 + Mathf.RoundToInt(knot.TangentIn.x * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(knot.TangentIn.y * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(knot.TangentIn.z * 100f);

                        hash = hash * 31 + Mathf.RoundToInt(knot.TangentOut.x * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(knot.TangentOut.y * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(knot.TangentOut.z * 100f);

                        hash = hash * 31 + Mathf.RoundToInt(knot.Rotation.value.x * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(knot.Rotation.value.y * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(knot.Rotation.value.z * 100f);
                        hash = hash * 31 + Mathf.RoundToInt(knot.Rotation.value.w * 100f);

                        hash = hash * 31 + Mathf.RoundToInt(this.Tool.GetKnotWidth(s, k) * 100f);
                    }
                }

                return hash;
            }
        }

        private bool IsSceneVisible(Vector3 worldPosition)
        {
            var sceneView = SceneView.currentDrawingSceneView;
            if (!sceneView || !sceneView.camera)
            {
                return true;
            }

            Camera camera = sceneView.camera;

            if (this.Tool.sceneVisibilityDistance > 0f)
            {
                float sqrDistance = (camera.transform.position - worldPosition).sqrMagnitude;

                float maxDistance = this.Tool.sceneVisibilityDistance;

                if (sqrDistance > maxDistance * maxDistance)
                {
                    return false;
                }
            }

            if (this.Tool.useSceneCameraFrustumCulling)
            {
                Vector3 viewport = camera.WorldToViewportPoint(worldPosition);

                if (viewport.z <= 0f)
                {
                    return false;
                }

                const float padding = 0.15f;

                if (viewport.x < -padding || viewport.x > 1f + padding)
                {
                    return false;
                }

                if (viewport.y < -padding || viewport.y > 1f + padding)
                {
                    return false;
                }
            }

            return true;
        }

        private void DrawRoadPreview()
        {
            if (!this.Tool.drawPreview)
            {
                return;
            }

            Event e = Event.current;

            if (this.Tool.hidePreviewWhileDragging && e != null && e.type == EventType.MouseDrag)
            {
                return;
            }

            var samples = this.GetCachedPreviewSamples();

            if (samples.Count < 2)
            {
                return;
            }

            Handles.zTest = CompareFunction.Always;

            for (int i = 0; i < samples.Count - 1; i++)
            {
                TerrainRoadSpline.RoadSample a = samples[i];
                TerrainRoadSpline.RoadSample b = samples[i + 1];

                if (a.SplineIndex != b.SplineIndex)
                {
                    continue;
                }

                Vector3 segmentCenter = (a.Position + b.Position) * 0.5f;

                if (!this.IsSceneVisible(segmentCenter))
                {
                    continue;
                }

                Vector3 aRight = Vector3.Cross(Vector3.up, a.Tangent).normalized;
                Vector3 bRight = Vector3.Cross(Vector3.up, b.Tangent).normalized;

                if (aRight.sqrMagnitude < 0.0001f)
                {
                    aRight = Vector3.right;
                }

                if (bRight.sqrMagnitude < 0.0001f)
                {
                    bRight = Vector3.right;
                }

                Vector3 aL = a.Position - aRight * (a.Width * 0.5f);
                Vector3 aR = a.Position + aRight * (a.Width * 0.5f);
                Vector3 bL = b.Position - bRight * (b.Width * 0.5f);
                Vector3 bR = b.Position + bRight * (b.Width * 0.5f);

                if (this.Tool.drawPreviewFill)
                {
                    Handles.color = this.Tool.previewFillColor;
                    Handles.DrawAAConvexPolygon(aL, bL, bR, aR);
                }

                Handles.color = this.Tool.previewEdgeColor;
                Handles.DrawAAPolyLine(5f, aL, bL);
                Handles.DrawAAPolyLine(5f, aR, bR);
            }
        }

        private void DrawWidthHandles()
        {
            SplineContainer container = this.Tool.Container;
            if (!container)
            {
                return;
            }

            Handles.color = this.Tool.widthHandleColor;

            int handleStep = Mathf.Max(1, this.Tool.widthHandleStep);

            for (int s = 0; s < container.Splines.Count; s++)
            {
                Spline spline = container.Splines[s];

                for (int k = 0; k < spline.Count; k++)
                {
                    if (handleStep > 1 && k % handleStep != 0)
                    {
                        continue;
                    }

                    BezierKnot knot = spline[k];

                    var localPos = new Vector3(knot.Position.x, knot.Position.y, knot.Position.z);

                    Vector3 worldPos = container.transform.TransformPoint(localPos);

                    if (!this.IsSceneVisible(worldPos))
                    {
                        continue;
                    }

                    Vector3 tangent = this.GetKnotTangentWorld(container, s, k);
                    Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;

                    if (right.sqrMagnitude < 0.0001f)
                    {
                        right = Vector3.right;
                    }

                    float width = this.Tool.GetKnotWidth(s, k);
                    Vector3 handlePos = worldPos + right * (width * 0.5f);

                    float size = HandleUtility.GetHandleSize(handlePos) * 0.12f;

                    EditorGUI.BeginChangeCheck();

                    Vector3 newHandlePos = Handles.Slider(handlePos, right, size, Handles.SphereHandleCap, 0f);

                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(this.Tool, "Change Road Width");

                        float newWidth = Vector3.Dot(newHandlePos - worldPos, right) * 2f;
                        this.Tool.SetKnotWidth(s, k, Mathf.Max(0.1f, newWidth));

                        this.Tool.MarkPreviewDirty();

                        EditorUtility.SetDirty(this.Tool);
                        SceneView.RepaintAll();
                    }

                    if (this.Tool.drawWidthLabels)
                    {
                        Handles.Label(handlePos + Vector3.up * size, $"W {this.Tool.GetKnotWidth(s, k):0.0}");
                    }
                }
            }
        }

        private Vector3 GetKnotTangentWorld(SplineContainer container, int splineIndex, int knotIndex)
        {
            Spline spline = container.Splines[splineIndex];

            if (spline.Count <= 1)
            {
                return container.transform.forward;
            }

            float t = knotIndex / Mathf.Max(1f, spline.Count - 1f);

            if (container.Evaluate(splineIndex, t, out _, out float3 tangent, out _))
            {
                var worldTangent = new Vector3(tangent.x, tangent.y, tangent.z);

                if (worldTangent.sqrMagnitude > 0.0001f)
                {
                    return worldTangent.normalized;
                }
            }

            return container.transform.forward;
        }

        private void HandleShiftClickCreation()
        {
            Event e = Event.current;

            if (!e.shift || e.type != EventType.MouseDown || e.button != 0)
            {
                return;
            }

            if (!this.Tool.terrain)
            {
                return;
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            if (!Physics.Raycast(ray, out RaycastHit hit, 100000f))
            {
                return;
            }

            var hitTerrain = hit.collider.GetComponent<UnityEngine.Terrain>();

            if (hitTerrain != this.Tool.terrain)
            {
                return;
            }

            Undo.RecordObject(this.Tool.Container, "Edit Road Spline Point");
            Undo.RecordObject(this.Tool, "Edit Road Width");

            if (e.alt)
            {
                this.InsertPointBetweenNearestKnots(hit.point);
            }
            else
            {
                this.AddPoint(hit.point, e.control);
            }

            EditorUtility.SetDirty(this.Tool.Container);
            EditorUtility.SetDirty(this.Tool);

            e.Use();
            SceneView.RepaintAll();
        }

        private void AddPoint(Vector3 worldPoint, bool forceNewSpline)
        {
            SplineContainer container = this.Tool.Container;

            var splines = new List<Spline>(container.Splines);

            bool createNewSpline = splines.Count == 0 || forceNewSpline;

            if (createNewSpline)
            {
                splines.Add(new Spline());
                container.Splines = splines;
                this.Tool.SyncWidthData();
            }

            int splineIndex = Mathf.Max(0, container.Splines.Count - 1);
            Spline spline = container.Splines[splineIndex];

            Vector3 local = container.transform.InverseTransformPoint(worldPoint);

            var knot = new BezierKnot(new float3(local.x, local.y, local.z));

            spline.Add(knot, TangentMode.AutoSmooth);

            this.Tool.SyncWidthData();
            this.Tool.SetKnotWidth(splineIndex, spline.Count - 1, this.Tool.defaultWidth);
            this.Tool.MarkPreviewDirty();
        }

        private void InsertPointBetweenNearestKnots(Vector3 worldPoint)
        {
            SplineContainer container = this.Tool.Container;

            if (!container)
            {
                return;
            }

            if (!this.TryFindNearestKnotSegment(worldPoint, out int splineIndex, out int insertIndex, out float segmentT))
            {
                this.AddPoint(worldPoint, false);
                return;
            }

            Spline spline = container.Splines[splineIndex];

            Vector3 local = container.transform.InverseTransformPoint(worldPoint);

            var knot = new BezierKnot(new float3(local.x, local.y, local.z));

            float widthA = this.Tool.GetKnotWidth(splineIndex, insertIndex - 1);
            float widthB = this.Tool.GetKnotWidth(splineIndex, insertIndex);

            float newWidth = Mathf.Lerp(widthA, widthB, segmentT);

            spline.Insert(insertIndex, knot, TangentMode.AutoSmooth);

            this.Tool.InsertKnotWidth(splineIndex, insertIndex, newWidth);
            this.Tool.SyncWidthData();
            this.Tool.MarkPreviewDirty();
        }

        private bool TryFindNearestKnotSegment(Vector3 worldPoint, out int bestSplineIndex, out int bestInsertIndex, out float bestSegmentT)
        {
            bestSplineIndex = -1;
            bestInsertIndex = -1;
            bestSegmentT = 0f;

            SplineContainer container = this.Tool.Container;
            if (!container)
            {
                return false;
            }

            var pointXZ = new Vector2(worldPoint.x, worldPoint.z);

            float bestDistanceSq = float.MaxValue;

            for (int s = 0; s < container.Splines.Count; s++)
            {
                Spline spline = container.Splines[s];

                if (spline.Count < 2)
                {
                    continue;
                }

                for (int k = 0; k < spline.Count - 1; k++)
                {
                    Vector3 a = KnotWorldPosition(container, spline[k]);
                    Vector3 b = KnotWorldPosition(container, spline[k + 1]);

                    var aXZ = new Vector2(a.x, a.z);
                    var bXZ = new Vector2(b.x, b.z);

                    float t = ClosestPoint01OnSegment(pointXZ, aXZ, bXZ);
                    Vector2 closest = Vector2.Lerp(aXZ, bXZ, t);

                    float distanceSq = (pointXZ - closest).sqrMagnitude;

                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        bestSplineIndex = s;
                        bestInsertIndex = k + 1;
                        bestSegmentT = t;
                    }
                }
            }

            return bestSplineIndex >= 0 && bestInsertIndex >= 0;
        }

        private static Vector3 KnotWorldPosition(SplineContainer container, BezierKnot knot)
        {
            var local = new Vector3(knot.Position.x, knot.Position.y, knot.Position.z);

            return container.transform.TransformPoint(local);
        }

        private static float ClosestPoint01OnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float dot = Vector2.Dot(ab, ab);
            if (dot < 0.000001f)
            {
                return 0f;
            }

            return Mathf.Clamp01(Vector2.Dot(p - a, ab) / dot);
        }
    }
}