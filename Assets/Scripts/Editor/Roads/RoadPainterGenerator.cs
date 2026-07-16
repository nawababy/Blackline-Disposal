using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public struct RoadPainterSegmentSample
{
    public Vector3 Position;
    public Vector3 Tangent;

    public RoadPainterSegmentSample(Vector3 position, Vector3 tangent)
    {
        Position = position;
        Tangent = tangent;
    }
}

public struct RoadPainterSegmentPose
{
    public Vector3 Position;
    public Quaternion Rotation;

    public RoadPainterSegmentPose(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }
}

public struct RoadPainterPartPlacement
{
    public RoadPainterPrefabPart Part;
    public Vector3 WorldPosition;
    public Quaternion WorldRotation;
    public Vector3 LocalScale;

    public RoadPainterPartPlacement(RoadPainterPrefabPart part, Vector3 worldPosition, Quaternion worldRotation)
    {
        Part = part;
        WorldPosition = worldPosition;
        WorldRotation = worldRotation;
        LocalScale = Vector3.one;
    }
}

public static class RoadPainterGenerator
{
    private const float MinimumPointDistance = 0.05f;
    private const string GeneratedContainerName = "RoadPainterGenerated";

    public static List<RoadPainterSegmentSample> BuildSamples(IReadOnlyList<Vector3> sourcePoints, float segmentPitch)
    {
        List<Vector3> points = RemoveNearDuplicatePoints(sourcePoints);
        List<RoadPainterSegmentSample> samples = new List<RoadPainterSegmentSample>();
        if (points.Count < 2 || segmentPitch <= 0f)
        {
            return samples;
        }

        List<float> cumulativeLengths = new List<float>(points.Count) { 0f };
        float totalLength = 0f;
        for (int i = 1; i < points.Count; i++)
        {
            totalLength += Vector3.Distance(points[i - 1], points[i]);
            cumulativeLengths.Add(totalLength);
        }

        for (float distance = 0f; distance + segmentPitch <= totalLength + 0.0001f; distance += segmentPitch)
        {
            Vector3 position;
            Vector3 tangent;
            if (TryGetPointAndTangent(points, cumulativeLengths, distance, out position, out tangent))
            {
                samples.Add(new RoadPainterSegmentSample(position, tangent));
            }
        }

        return samples;
    }

    public static List<RoadPainterPartPlacement> GetPartPlacements(
        IReadOnlyList<RoadPainterPrefabPart> parts,
        Vector3 parentPosition,
        Quaternion parentRotation)
    {
        List<RoadPainterPartPlacement> placements = new List<RoadPainterPartPlacement>();
        if (parts == null)
        {
            return placements;
        }

        for (int i = 0; i < parts.Count; i++)
        {
            RoadPainterPrefabPart part = parts[i];
            if (part == null)
            {
                continue;
            }

            Vector3 worldPosition = parentPosition + parentRotation * part.localPositionOffset;
            Quaternion worldRotation = parentRotation * Quaternion.Euler(part.localRotationOffset);
            placements.Add(new RoadPainterPartPlacement(part, worldPosition, worldRotation));
        }

        return placements;
    }

    public static Bounds CalculatePartBounds(Bounds localBounds, RoadPainterPartPlacement placement)
    {
        Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        Vector3 center = localBounds.center;
        Vector3 extents = localBounds.extents;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 localCorner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    Vector3 worldCorner = placement.WorldPosition + placement.WorldRotation * Vector3.Scale(localCorner, placement.LocalScale);
                    minimum = Vector3.Min(minimum, worldCorner);
                    maximum = Vector3.Max(maximum, worldCorner);
                }
            }
        }

        Bounds bounds = new Bounds();
        bounds.SetMinMax(minimum, maximum);
        return bounds;
    }

    public static bool Generate(RoadPainterPath path, out string error)
    {
        error = string.Empty;

        if (path == null)
        {
            error = "Road Painter path is null.";
            return false;
        }

        RoadPainterProfile profile = path.Profile;
        if (profile == null)
        {
            error = "Road Painter path has no profile.";
            return false;
        }

        if (!profile.TryValidate(out error))
        {
            if (string.IsNullOrEmpty(error))
            {
                error = "Road Painter profile validation failed.";
            }

            return false;
        }

        Transform roadRoot;
        if (!path.TryResolveRoadRoot(out roadRoot))
        {
            error = "Road Painter path has no valid road root.";
            return false;
        }

        List<Vector3> worldPoints = path.GetWorldControlPoints();
        List<RoadPainterSegmentSample> samples = BuildSamples(worldPoints, profile.SegmentPitch);
        if (samples.Count == 0)
        {
            error = "Road Painter path needs at least one full segment length between its control points.";
            return false;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Generate Road Path");
        Undo.RecordObject(path, "Generate Road Path");

        Transform generatedRoot = EnsureGeneratedRoot(path, roadRoot);
        ClearGeneratedChildren(generatedRoot);

        int skippedSegments = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            RoadPainterSegmentPose pose;
            if (!TryGetSegmentPose(path, profile, samples[i], out pose))
            {
                skippedSegments++;
                continue;
            }

            CreateCompoundSegment(generatedRoot, profile, pose, i + 1);
        }

        EditorSceneManager.MarkSceneDirty(roadRoot.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);

        if (skippedSegments > 0)
        {
            Debug.LogWarning("[RoadPainterGenerator] Skipped " + skippedSegments + " steep or unresolved segment(s) for path '" + path.name + "'.", path);
        }

        error = string.Empty;
        return true;
    }

    public static void ClearGenerated(RoadPainterPath path)
    {
        if (path == null)
        {
            return;
        }

        Transform generatedRoot;
        if (!path.TryResolveGeneratedRoot(out generatedRoot))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Clear Generated Road Path");
        ClearGeneratedChildren(generatedRoot);
        EditorSceneManager.MarkSceneDirty(generatedRoot.gameObject.scene);
        Undo.CollapseUndoOperations(undoGroup);
    }

    public static bool TryGetSegmentPose(
        RoadPainterPath path,
        RoadPainterProfile profile,
        RoadPainterSegmentSample sample,
        out RoadPainterSegmentPose pose)
    {
        pose = new RoadPainterSegmentPose();
        Vector3 flatTangent = Vector3.ProjectOnPlane(sample.Tangent, Vector3.up);
        if (flatTangent.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        flatTangent.Normalize();
        float height;
        if (!TryGetGroundHeight(path, sample.Position, out height))
        {
            return false;
        }

        if (!path.UseFixedHeight && IsTooSteep(path, profile, sample.Position, flatTangent))
        {
            return false;
        }

        Quaternion rotation = Quaternion.FromToRotation(profile.GetForwardAxisVector(), flatTangent);
        pose = new RoadPainterSegmentPose(
            new Vector3(sample.Position.x, height + profile.YOffset, sample.Position.z),
            rotation
        );
        return true;
    }

    private static List<Vector3> RemoveNearDuplicatePoints(IReadOnlyList<Vector3> sourcePoints)
    {
        List<Vector3> result = new List<Vector3>();
        if (sourcePoints == null)
        {
            return result;
        }

        for (int i = 0; i < sourcePoints.Count; i++)
        {
            if (result.Count == 0 || Vector3.Distance(result[result.Count - 1], sourcePoints[i]) >= MinimumPointDistance)
            {
                result.Add(sourcePoints[i]);
            }
        }

        return result;
    }

    private static bool TryGetPointAndTangent(
        IReadOnlyList<Vector3> points,
        IReadOnlyList<float> cumulativeLengths,
        float distance,
        out Vector3 position,
        out Vector3 tangent)
    {
        position = Vector3.zero;
        tangent = Vector3.zero;
        for (int i = 1; i < points.Count; i++)
        {
            if (distance > cumulativeLengths[i] + 0.0001f)
            {
                continue;
            }

            Vector3 segment = points[i] - points[i - 1];
            float length = segment.magnitude;
            if (length < MinimumPointDistance)
            {
                continue;
            }

            float fraction = Mathf.Clamp01((distance - cumulativeLengths[i - 1]) / length);
            position = Vector3.Lerp(points[i - 1], points[i], fraction);
            tangent = segment.normalized;
            return true;
        }

        return false;
    }

    private static bool TryGetGroundHeight(RoadPainterPath path, Vector3 worldPosition, out float height)
    {
        if (path.UseFixedHeight)
        {
            height = path.FixedHeight;
            return true;
        }

        Terrain terrain;
        if (!path.TryResolveTerrain(out terrain) || terrain.terrainData == null)
        {
            height = 0f;
            return false;
        }

        if (path.UseTerrainRaycast)
        {
            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            float rayStart = terrain.transform.position.y + terrain.terrainData.size.y + 1000f;
            Ray ray = new Ray(new Vector3(worldPosition.x, rayStart, worldPosition.z), Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, rayStart - terrain.transform.position.y + 2000f, path.TerrainLayerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == terrainCollider)
                {
                    height = hits[i].point.y;
                    return true;
                }
            }
        }

        height = terrain.SampleHeight(worldPosition) + terrain.transform.position.y;
        return true;
    }

    private static bool IsTooSteep(RoadPainterPath path, RoadPainterProfile profile, Vector3 start, Vector3 tangent)
    {
        float startHeight;
        float endHeight;
        if (!TryGetGroundHeight(path, start, out startHeight) ||
            !TryGetGroundHeight(path, start + tangent * profile.SegmentLength, out endHeight))
        {
            return true;
        }

        float slopeDegrees = Mathf.Atan2(Mathf.Abs(endHeight - startHeight), profile.SegmentLength) * Mathf.Rad2Deg;
        return slopeDegrees > profile.MaximumSlopeDegrees;
    }

    private static Transform EnsureGeneratedRoot(RoadPainterPath path, Transform roadRoot)
    {
        Transform generatedRoot;
        if (path.TryResolveGeneratedRoot(out generatedRoot) && generatedRoot.parent != null)
        {
            return generatedRoot;
        }

        Transform container = FindOrCreateChild(roadRoot, GeneratedContainerName, "Create Road Painter Generated Root");
        GameObject pathRootObject = new GameObject("RoadPath_" + path.PathId);
        Undo.RegisterCreatedObjectUndo(pathRootObject, "Create Generated Road Path");
        Undo.SetTransformParent(pathRootObject.transform, container, "Parent Generated Road Path");
        pathRootObject.transform.localPosition = Vector3.zero;
        pathRootObject.transform.localRotation = Quaternion.identity;
        pathRootObject.transform.localScale = Vector3.one;
        path.SetGeneratedRoot(pathRootObject.transform);
        return pathRootObject.transform;
    }

    private static Transform FindOrCreateChild(Transform parent, string childName, string undoName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (string.Equals(child.name, childName, StringComparison.Ordinal))
            {
                return child;
            }
        }

        GameObject childObject = new GameObject(childName);
        Undo.RegisterCreatedObjectUndo(childObject, undoName);
        Undo.SetTransformParent(childObject.transform, parent, undoName);
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject.transform;
    }

    private static void ClearGeneratedChildren(Transform generatedRoot)
    {
        for (int i = generatedRoot.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(generatedRoot.GetChild(i).gameObject);
        }
    }

    private static void CreateCompoundSegment(
        Transform generatedRoot,
        RoadPainterProfile profile,
        RoadPainterSegmentPose pose,
        int segmentNumber)
    {
        GameObject segmentObject = new GameObject("RoadSegment_" + segmentNumber.ToString("D4"));
        Undo.RegisterCreatedObjectUndo(segmentObject, "Generate Road Segment");
        Undo.SetTransformParent(segmentObject.transform, generatedRoot, "Parent Road Segment");
        segmentObject.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
        segmentObject.transform.localScale = Vector3.one;

        IReadOnlyList<RoadPainterPrefabPart> parts = profile.SegmentParts;
        for (int i = 0; i < parts.Count; i++)
        {
            RoadPainterPrefabPart definition = parts[i];
            GameObject partObject = PrefabUtility.InstantiatePrefab(definition.prefab, segmentObject.scene) as GameObject;
            if (partObject == null)
            {
                throw new InvalidOperationException("Could not instantiate Road Painter prefab part '" + definition.prefab.name + "'.");
            }

            Undo.RegisterCreatedObjectUndo(partObject, "Generate Road Segment Part");
            Undo.SetTransformParent(partObject.transform, segmentObject.transform, "Parent Road Segment Part");
            partObject.name = i == 0 ? "RoadHalf_A" : i == 1 ? "RoadHalf_B" : "RoadPart_" + (i + 1).ToString("D2");
            partObject.transform.localPosition = definition.localPositionOffset;
            partObject.transform.localRotation = Quaternion.Euler(definition.localRotationOffset);
            partObject.transform.localScale = Vector3.one;

            if (profile.DisableSecondaryLodColliders)
            {
                DisableSecondaryLodColliders(partObject);
            }
        }
    }

    private static void DisableSecondaryLodColliders(GameObject partObject)
    {
        LODGroup[] lodGroups = partObject.GetComponentsInChildren<LODGroup>(true);
        for (int groupIndex = 0; groupIndex < lodGroups.Length; groupIndex++)
        {
            LOD[] lods = lodGroups[groupIndex].GetLODs();
            if (lods.Length < 2)
            {
                continue;
            }

            HashSet<Collider> primaryColliders = new HashSet<Collider>();
            AddRendererColliders(lods[0].renderers, primaryColliders);
            if (primaryColliders.Count == 0)
            {
                continue;
            }

            for (int lodIndex = 1; lodIndex < lods.Length; lodIndex++)
            {
                Renderer[] renderers = lods[lodIndex].renderers;
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    Renderer renderer = renderers[rendererIndex];
                    if (renderer == null)
                    {
                        continue;
                    }

                    Collider[] colliders = renderer.GetComponents<Collider>();
                    for (int colliderIndex = 0; colliderIndex < colliders.Length; colliderIndex++)
                    {
                        Collider collider = colliders[colliderIndex];
                        if (collider == null || primaryColliders.Contains(collider))
                        {
                            continue;
                        }

                        Undo.RecordObject(collider, "Disable Secondary LOD Collider");
                        collider.enabled = false;
                    }
                }
            }
        }
    }

    private static void AddRendererColliders(Renderer[] renderers, ISet<Collider> colliders)
    {
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Collider[] rendererColliders = renderer.GetComponents<Collider>();
            for (int colliderIndex = 0; colliderIndex < rendererColliders.Length; colliderIndex++)
            {
                if (rendererColliders[colliderIndex] != null)
                {
                    colliders.Add(rendererColliders[colliderIndex]);
                }
            }
        }
    }
}
