using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(menuName = "Blackline Disposal/Road Painter/Path", fileName = "RoadPainterPath")]
public sealed class RoadPainterPath : ScriptableObject
{
    [SerializeField] private string pathId;
    [SerializeField] private RoadPainterProfile profile;
    [SerializeField] private List<Vector3> localControlPoints = new List<Vector3>();
    [SerializeField] private string roadRootGlobalObjectId;
    [SerializeField] private string terrainGlobalObjectId;
    [SerializeField] private string generatedRootGlobalObjectId;
    [SerializeField] private LayerMask terrainLayerMask = ~0;
    [SerializeField] private bool useTerrainRaycast = true;
    [SerializeField] private bool useFixedHeight;
    [SerializeField] private float fixedHeight;

    public string PathId { get { return pathId; } }
    public RoadPainterProfile Profile { get { return profile; } }
    public IReadOnlyList<Vector3> LocalControlPoints { get { return localControlPoints; } }
    public int ControlPointCount { get { return localControlPoints.Count; } }
    public LayerMask TerrainLayerMask { get { return terrainLayerMask; } }
    public bool UseTerrainRaycast { get { return useTerrainRaycast; } }
    public bool UseFixedHeight { get { return useFixedHeight; } }
    public float FixedHeight { get { return fixedHeight; } }

    public void Configure(RoadPainterProfile newProfile, Transform roadRoot, Terrain terrain, LayerMask layerMask)
    {
        profile = newProfile;
        roadRootGlobalObjectId = GetGlobalObjectId(roadRoot);
        terrainGlobalObjectId = GetGlobalObjectId(terrain);
        terrainLayerMask = layerMask;
        EnsurePathId();
        EditorUtility.SetDirty(this);
    }

    public void SetTerrainOptions(bool raycastEnabled, bool fixedHeightEnabled, float newFixedHeight, LayerMask layerMask)
    {
        useTerrainRaycast = raycastEnabled;
        useFixedHeight = fixedHeightEnabled;
        fixedHeight = newFixedHeight;
        terrainLayerMask = layerMask;
        EditorUtility.SetDirty(this);
    }

    public bool TryResolveRoadRoot(out Transform roadRoot)
    {
        roadRoot = ResolveObject<Transform>(roadRootGlobalObjectId);
        return roadRoot != null;
    }

    public bool TryResolveTerrain(out Terrain terrain)
    {
        terrain = ResolveObject<Terrain>(terrainGlobalObjectId);
        return terrain != null;
    }

    public bool TryResolveGeneratedRoot(out Transform generatedRoot)
    {
        generatedRoot = ResolveObject<Transform>(generatedRootGlobalObjectId);
        return generatedRoot != null;
    }

    public List<Vector3> GetWorldControlPoints()
    {
        Transform roadRoot;
        if (!TryResolveRoadRoot(out roadRoot))
        {
            return new List<Vector3>();
        }

        List<Vector3> worldPoints = new List<Vector3>(localControlPoints.Count);
        for (int i = 0; i < localControlPoints.Count; i++)
        {
            worldPoints.Add(roadRoot.TransformPoint(localControlPoints[i]));
        }

        return worldPoints;
    }

    public void AddWorldControlPoint(Vector3 worldPosition)
    {
        Transform roadRoot;
        if (!TryResolveRoadRoot(out roadRoot))
        {
            throw new InvalidOperationException("Road Painter path has no valid road root.");
        }

        localControlPoints.Add(roadRoot.InverseTransformPoint(worldPosition));
        EditorUtility.SetDirty(this);
    }

    public void SetWorldControlPoint(int index, Vector3 worldPosition)
    {
        Transform roadRoot;
        if (!TryResolveRoadRoot(out roadRoot))
        {
            throw new InvalidOperationException("Road Painter path has no valid road root.");
        }

        if (index < 0 || index >= localControlPoints.Count)
        {
            throw new ArgumentOutOfRangeException("index");
        }

        localControlPoints[index] = roadRoot.InverseTransformPoint(worldPosition);
        EditorUtility.SetDirty(this);
    }

    public void RemoveControlPointAt(int index)
    {
        if (index < 0 || index >= localControlPoints.Count)
        {
            return;
        }

        localControlPoints.RemoveAt(index);
        EditorUtility.SetDirty(this);
    }

    public void RemoveControlPointsFrom(int startIndex)
    {
        startIndex = Mathf.Clamp(startIndex, 0, localControlPoints.Count);
        if (startIndex == localControlPoints.Count)
        {
            return;
        }

        localControlPoints.RemoveRange(startIndex, localControlPoints.Count - startIndex);
        EditorUtility.SetDirty(this);
    }

    public void SetGeneratedRoot(Transform generatedRoot)
    {
        generatedRootGlobalObjectId = GetGlobalObjectId(generatedRoot);
        EditorUtility.SetDirty(this);
    }

    private void EnsurePathId()
    {
        if (!string.IsNullOrWhiteSpace(pathId))
        {
            return;
        }

        pathId = "road_path_" + Guid.NewGuid().ToString("N");
    }

    private static string GetGlobalObjectId(UnityEngine.Object value)
    {
        return value == null ? string.Empty : GlobalObjectId.GetGlobalObjectIdSlow(value).ToString();
    }

    private static T ResolveObject<T>(string globalObjectId) where T : UnityEngine.Object
    {
        if (string.IsNullOrEmpty(globalObjectId))
        {
            return null;
        }

        GlobalObjectId identifier;
        if (!GlobalObjectId.TryParse(globalObjectId, out identifier))
        {
            return null;
        }

        return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(identifier) as T;
    }
}
