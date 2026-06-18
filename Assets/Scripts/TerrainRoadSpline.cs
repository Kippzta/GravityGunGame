// Copyright (c) Cortopia Studios. All rights reserved.
// This unpublished material is proprietary to Cortopia Studios.
// The methods and techniques described herein are considered trade secrets
// and/or confidential. Reproduction or distribution, in whole or in part, is
// forbidden except by express written permission of Cortopia Studios.

using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Cortopia.Scripts.Terrain
{
    [RequireComponent(typeof(SplineContainer))]
    public sealed class TerrainRoadSpline : MonoBehaviour
    {
        [Header("Target")]
        public UnityEngine.Terrain terrain;

        [Header("Road Shape")]
        public float defaultWidth = 6f;

        [Tooltip("Soft falloff area outside the road width.")]
        public float shoulderWidth = 3f;

        [Tooltip("Distance in meters between spline samples used for actual terrain generation. Lower is more accurate but slower.")]
        public float sampleSpacing = 3f;

        [Tooltip("Extra vertical offset applied to the flattened road surface.")]
        public float heightOffset;

        [Tooltip("If true, terrain is flattened to the spline Y height. If false, it only smooths terrain around the road.")]
        public bool flattenToSplineHeight = true;

        [Range(0f, 1f)]
        public float strength = 1f;

        [Header("Intersection Blending")]
        [Tooltip("Blends overlapping road heights at intersections. Usually this should stay enabled.")]
        public bool blendIntersections = true;

        [Header("Viewport Preview")]
        public bool drawPreview = true;

        [Tooltip("Hide preview road and width handles farther than this distance from the Scene view camera. Set to 0 to disable distance culling.")]
        public float sceneVisibilityDistance = 150f;

        [Tooltip("Green transparent road fill shown in Scene view.")]
        public Color previewFillColor = new(0f, 1f, 0.15f, 0.35f);

        [Tooltip("Bright green road edges shown in Scene view.")]
        public Color previewEdgeColor = new(0f, 1f, 0.05f, 1f);

        public Color widthHandleColor = new(0.1f, 0.75f, 1f, 1f);

        [Header("Editor Performance")]
        [Tooltip("Distance in meters between spline samples used only for the Scene view preview. Higher is faster while editing.")]
        public float previewSampleSpacing = 8f;

        [Tooltip("Hides the green preview fill/edges while dragging in Scene view.")]
        public bool hidePreviewWhileDragging = true;

        [Tooltip("Draws the transparent filled part of the road preview. Disable for better editor performance.")]
        public bool drawPreviewFill = true;

        [Tooltip("Draw width text labels near width handles. Disable for better editor performance.")]
        public bool drawWidthLabels;

        [Tooltip("Draw every Nth width handle. Use 1 to draw all width handles.")]
        public int widthHandleStep = 1;

        [Tooltip("Also hide preview and handles when they are outside the Scene camera view.")]
        public bool useSceneCameraFrustumCulling = true;

        [Tooltip("How often the editor checks whether Unity's built-in spline tools changed the spline. Higher values are faster but update the preview less often.")]
        public float splineChangeCheckInterval = 0.25f;

        [SerializeField]
        private List<SplineWidthData> widthData = new();

        [SerializeField]
        [HideInInspector]
        private int editorPreviewVersion;

        private SplineContainer _container;

        public int EditorPreviewVersion => this.editorPreviewVersion;

        public SplineContainer Container
        {
            get
            {
                if (!this._container)
                {
                    this._container = this.GetComponent<SplineContainer>();
                }

                return this._container;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            this.defaultWidth = Mathf.Max(0.1f, this.defaultWidth);
            this.shoulderWidth = Mathf.Max(0f, this.shoulderWidth);
            this.sampleSpacing = Mathf.Max(0.25f, this.sampleSpacing);
            this.previewSampleSpacing = Mathf.Max(0.5f, this.previewSampleSpacing);
            this.sceneVisibilityDistance = Mathf.Max(0f, this.sceneVisibilityDistance);
            this.widthHandleStep = Mathf.Max(1, this.widthHandleStep);
            this.splineChangeCheckInterval = Mathf.Max(0.05f, this.splineChangeCheckInterval);

            if (!Application.isPlaying)
            {
                this.SyncWidthData();
                this.MarkPreviewDirty();
            }
        }
#endif

        public void MarkPreviewDirty()
        {
            this.editorPreviewVersion++;
        }

        public void SyncWidthData()
        {
            SplineContainer container = this.Container;
            if (!container)
            {
                return;
            }

            bool changed = false;

            while (this.widthData.Count < container.Splines.Count)
            {
                this.widthData.Add(new SplineWidthData());
                changed = true;
            }

            while (this.widthData.Count > container.Splines.Count)
            {
                this.widthData.RemoveAt(this.widthData.Count - 1);
                changed = true;
            }

            for (int s = 0; s < container.Splines.Count; s++)
            {
                Spline spline = container.Splines[s];
                SplineWidthData data = this.widthData[s];

                while (data.widths.Count < spline.Count)
                {
                    data.widths.Add(this.defaultWidth);
                    changed = true;
                }

                while (data.widths.Count > spline.Count)
                {
                    data.widths.RemoveAt(data.widths.Count - 1);
                    changed = true;
                }
            }

            if (changed)
            {
                this.MarkPreviewDirty();
            }
        }

        public float GetKnotWidth(int splineIndex, int knotIndex)
        {
            if (splineIndex < 0 || splineIndex >= this.widthData.Count)
            {
                return this.defaultWidth;
            }

            SplineWidthData data = this.widthData[splineIndex];

            if (knotIndex < 0 || knotIndex >= data.widths.Count)
            {
                return this.defaultWidth;
            }

            return Mathf.Max(0.1f, data.widths[knotIndex]);
        }

        public void SetKnotWidth(int splineIndex, int knotIndex, float width)
        {
            if (splineIndex < 0 || splineIndex >= this.widthData.Count)
            {
                return;
            }

            SplineWidthData data = this.widthData[splineIndex];

            if (knotIndex < 0 || knotIndex >= data.widths.Count)
            {
                return;
            }

            data.widths[knotIndex] = Mathf.Max(0.1f, width);
        }

        public void InsertKnotWidth(int splineIndex, int knotIndex, float width)
        {
            this.SyncWidthData();

            if (splineIndex < 0 || splineIndex >= this.widthData.Count)
            {
                return;
            }

            SplineWidthData data = this.widthData[splineIndex];

            knotIndex = Mathf.Clamp(knotIndex, 0, data.widths.Count);

            data.widths.Insert(knotIndex, Mathf.Max(0.1f, width));

            this.MarkPreviewDirty();
        }

        public float EvaluateWidth(int splineIndex, float t)
        {
            Spline spline = this.Container.Splines[splineIndex];
            int count = spline.Count;

            if (count == 0)
            {
                return this.defaultWidth;
            }

            if (count == 1)
            {
                return this.GetKnotWidth(splineIndex, 0);
            }

            float scaled = Mathf.Clamp01(t) * (count - 1);
            int i0 = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, count - 1);
            int i1 = Mathf.Clamp(i0 + 1, 0, count - 1);
            float u = scaled - i0;

            return Mathf.Lerp(this.GetKnotWidth(splineIndex, i0), this.GetKnotWidth(splineIndex, i1), u);
        }

        public List<RoadSample> BuildSamples()
        {
            return this.BuildSamples(this.sampleSpacing);
        }

        public List<RoadSample> BuildSamples(float spacing)
        {
            this.SyncWidthData();

            spacing = Mathf.Max(0.25f, spacing);

            var samples = new List<RoadSample>();
            SplineContainer container = this.Container;
            if (!container)
            {
                return samples;
            }

            for (int s = 0; s < container.Splines.Count; s++)
            {
                Spline spline = container.Splines[s];

                if (spline.Count < 2)
                {
                    continue;
                }

                float length = Mathf.Max(container.CalculateLength(s), spacing);
                int steps = Mathf.Max(2, Mathf.CeilToInt(length / spacing));

                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float) steps;

                    if (!container.Evaluate(s, t, out float3 p, out float3 tangent, out _))
                    {
                        continue;
                    }

                    var worldPos = new Vector3(p.x, p.y, p.z);
                    var worldTangent = new Vector3(tangent.x, tangent.y, tangent.z);

                    if (worldTangent.sqrMagnitude < 0.0001f)
                    {
                        worldTangent = Vector3.forward;
                    }

                    samples.Add(new RoadSample
                    {
                        SplineIndex = s,
                        T = t,
                        Position = worldPos,
                        Tangent = worldTangent.normalized,
                        Width = this.EvaluateWidth(s, t)
                    });
                }
            }

            return samples;
        }

        public void GenerateTerrainRoads()
        {
            if (!this.terrain)
            {
                Debug.LogWarning("No Terrain assigned.", this);
                return;
            }

            TerrainData data = this.terrain.terrainData;
            if (!data)
            {
                Debug.LogWarning("Assigned Terrain has no TerrainData.", this);
                return;
            }

            var roadSamples = this.BuildSamples(this.sampleSpacing);

            if (roadSamples.Count < 2)
            {
                Debug.LogWarning("No usable spline road samples found.", this);
                return;
            }

            Vector3 terrainPos = this.terrain.transform.position;
            Vector3 terrainSize = data.size;

            int heightRes = data.heightmapResolution;

            float[,] heights = data.GetHeights(0, 0, heightRes, heightRes);
            float[,] original = (float[,]) heights.Clone();

            float[,] maxInfluence = new float[heightRes, heightRes];
            float[,] accumulatedTargetHeight = new float[heightRes, heightRes];
            float[,] accumulatedWeight = new float[heightRes, heightRes];

            for (int i = 0; i < roadSamples.Count - 1; i++)
            {
                RoadSample a = roadSamples[i];
                RoadSample b = roadSamples[i + 1];

                if (a.SplineIndex != b.SplineIndex)
                {
                    continue;
                }

                this.StampRoadSegment(a, b, terrainPos, terrainSize, heightRes, original, maxInfluence, accumulatedTargetHeight, accumulatedWeight);
            }

            for (int y = 0; y < heightRes; y++)
            {
                for (int x = 0; x < heightRes; x++)
                {
                    float weight = accumulatedWeight[y, x];

                    if (weight <= 0f)
                    {
                        continue;
                    }

                    float influence = Mathf.Clamp01(maxInfluence[y, x]);

                    float targetHeight;

                    if (this.blendIntersections)
                    {
                        targetHeight = accumulatedTargetHeight[y, x] / weight;
                    }
                    else
                    {
                        targetHeight = accumulatedTargetHeight[y, x];
                    }

                    heights[y, x] = Mathf.Lerp(original[y, x], targetHeight, influence);
                }
            }

#if UNITY_EDITOR
            data.SetHeightsDelayLOD(0, 0, heights);
            data.SyncHeightmap();
            EditorUtility.SetDirty(data);
#else
            data.SetHeights(0, 0, heights);
#endif
        }

        private void StampRoadSegment(
            RoadSample a, RoadSample b, Vector3 terrainPos, Vector3 terrainSize, int heightRes, float[,] original, float[,] maxInfluence, float[,] accumulatedTargetHeight,
            float[,] accumulatedWeight)
        {
            var aXZ = new Vector2(a.Position.x, a.Position.z);
            var bXZ = new Vector2(b.Position.x, b.Position.z);

            float maxWidth = Mathf.Max(a.Width, b.Width);
            float outerDistance = maxWidth * 0.5f + this.shoulderWidth;

            float minWorldX = Mathf.Min(a.Position.x, b.Position.x) - outerDistance;
            float maxWorldX = Mathf.Max(a.Position.x, b.Position.x) + outerDistance;
            float minWorldZ = Mathf.Min(a.Position.z, b.Position.z) - outerDistance;
            float maxWorldZ = Mathf.Max(a.Position.z, b.Position.z) + outerDistance;

            int minX = WorldToHeightmapX(minWorldX, terrainPos, terrainSize, heightRes);
            int maxX = WorldToHeightmapX(maxWorldX, terrainPos, terrainSize, heightRes);
            int minY = WorldToHeightmapZ(minWorldZ, terrainPos, terrainSize, heightRes);
            int maxY = WorldToHeightmapZ(maxWorldZ, terrainPos, terrainSize, heightRes);

            Vector2 segment = bXZ - aXZ;
            float segmentLengthSq = Vector2.Dot(segment, segment);

            if (segmentLengthSq < 0.0001f)
            {
                return;
            }

            for (int y = minY; y <= maxY; y++)
            {
                float normZ = y / (float) (heightRes - 1);
                float worldZ = terrainPos.z + normZ * terrainSize.z;

                for (int x = minX; x <= maxX; x++)
                {
                    float normX = x / (float) (heightRes - 1);
                    float worldX = terrainPos.x + normX * terrainSize.x;

                    var pointXZ = new Vector2(worldX, worldZ);

                    float segmentT = Mathf.Clamp01(Vector2.Dot(pointXZ - aXZ, segment) / segmentLengthSq);

                    Vector2 closest = Vector2.Lerp(aXZ, bXZ, segmentT);
                    float distance = Vector2.Distance(pointXZ, closest);

                    float width = Mathf.Lerp(a.Width, b.Width, segmentT);
                    float halfWidth = width * 0.5f;
                    float outer = halfWidth + this.shoulderWidth;

                    if (distance > outer)
                    {
                        continue;
                    }

                    float influence;

                    if (distance <= halfWidth)
                    {
                        influence = 1f;
                    }
                    else
                    {
                        float shoulderT = Mathf.InverseLerp(outer, halfWidth, distance);
                        influence = Smooth01(shoulderT);
                    }

                    influence *= this.strength;

                    if (influence <= 0f)
                    {
                        continue;
                    }

                    float targetHeight01;

                    if (this.flattenToSplineHeight)
                    {
                        float roadWorldY = Mathf.Lerp(a.Position.y, b.Position.y, segmentT) + this.heightOffset;

                        targetHeight01 = Mathf.InverseLerp(terrainPos.y, terrainPos.y + terrainSize.y, roadWorldY);
                    }
                    else
                    {
                        targetHeight01 = SmoothNeighbourHeight(original, x, y);
                    }

                    targetHeight01 = Mathf.Clamp01(targetHeight01);

                    if (this.blendIntersections)
                    {
                        maxInfluence[y, x] = Mathf.Max(maxInfluence[y, x], influence);
                        accumulatedTargetHeight[y, x] += targetHeight01 * influence;
                        accumulatedWeight[y, x] += influence;
                    }
                    else
                    {
                        if (influence <= maxInfluence[y, x])
                        {
                            continue;
                        }

                        maxInfluence[y, x] = influence;
                        accumulatedTargetHeight[y, x] = targetHeight01;
                        accumulatedWeight[y, x] = 1f;
                    }
                }
            }
        }

        private static int WorldToHeightmapX(float worldX, Vector3 terrainPos, Vector3 terrainSize, int heightRes)
        {
            float normalized = Mathf.InverseLerp(terrainPos.x, terrainPos.x + terrainSize.x, worldX);

            return Mathf.Clamp(Mathf.RoundToInt(normalized * (heightRes - 1)), 0, heightRes - 1);
        }

        private static int WorldToHeightmapZ(float worldZ, Vector3 terrainPos, Vector3 terrainSize, int heightRes)
        {
            float normalized = Mathf.InverseLerp(terrainPos.z, terrainPos.z + terrainSize.z, worldZ);

            return Mathf.Clamp(Mathf.RoundToInt(normalized * (heightRes - 1)), 0, heightRes - 1);
        }

        private static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float SmoothNeighbourHeight(float[,] heights, int x, int y)
        {
            int h = heights.GetLength(0);
            int w = heights.GetLength(1);

            float sum = 0f;
            int count = 0;

            for (int yy = -1; yy <= 1; yy++)
            {
                for (int xx = -1; xx <= 1; xx++)
                {
                    int nx = Mathf.Clamp(x + xx, 0, w - 1);
                    int ny = Mathf.Clamp(y + yy, 0, h - 1);

                    sum += heights[ny, nx];
                    count++;
                }
            }

            return sum / count;
        }

        [Serializable]
        public sealed class SplineWidthData
        {
            public List<float> widths = new();
        }

        public struct RoadSample
        {
            public int SplineIndex;
            public float T;
            public Vector3 Position;
            public Vector3 Tangent;
            public float Width;
        }
    }
}