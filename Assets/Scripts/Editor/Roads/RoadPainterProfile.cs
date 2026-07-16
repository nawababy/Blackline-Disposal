using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public enum RoadPainterForwardAxis
{
    PositiveX,
    NegativeX,
    PositiveZ,
    NegativeZ
}

[Serializable]
public sealed class RoadPainterPrefabPart
{
    public GameObject prefab;
    public Vector3 localPositionOffset;
    public Vector3 localRotationOffset;
}

[CreateAssetMenu(menuName = "Blackline Disposal/Road Painter/Profile", fileName = "RoadPainterProfile")]
public sealed class RoadPainterProfile : ScriptableObject
{
    public const string Road01PrefabPath = "Assets/LowPolyMegapolis/Prefabs/Road/Road_01.prefab";
    public const float Road01SegmentLength = 6.0008683f;
    public const float Road01HalfWidth = 3.006042f;
    public const float Road01SeamOffset = 1.501783f;

    [SerializeField] private string profileId = "road_profile";
    [SerializeField] private string displayName = "Road Profile";
    [SerializeField] private List<RoadPainterPrefabPart> segmentParts = new List<RoadPainterPrefabPart>();
    [SerializeField] private float segmentLength = 6f;
    [SerializeField] private float segmentSpacing;
    [SerializeField] private float roadWidth = 3f;
    [SerializeField] private RoadPainterForwardAxis forwardAxis = RoadPainterForwardAxis.PositiveX;
    [SerializeField] private float yOffset;
    [SerializeField, Range(0f, 45f)] private float maximumSlopeDegrees = 10f;
    [SerializeField, Range(0f, 90f)] private float angleSnapDegrees = 15f;
    [SerializeField] private float gridSnapSize;
    [SerializeField] private bool disableSecondaryLodColliders = true;

    public string ProfileId { get { return profileId; } }
    public string DisplayName { get { return displayName; } }
    public IReadOnlyList<RoadPainterPrefabPart> SegmentParts { get { return segmentParts; } }
    public float SegmentLength { get { return segmentLength; } }
    public float SegmentSpacing { get { return segmentSpacing; } }
    public float SegmentPitch { get { return segmentLength + segmentSpacing; } }
    public float RoadWidth { get { return roadWidth; } }
    public RoadPainterForwardAxis ForwardAxis { get { return forwardAxis; } }
    public float YOffset { get { return yOffset; } }
    public float MaximumSlopeDegrees { get { return maximumSlopeDegrees; } }
    public float AngleSnapDegrees { get { return angleSnapDegrees; } }
    public float GridSnapSize { get { return gridSnapSize; } }
    public bool DisableSecondaryLodColliders { get { return disableSecondaryLodColliders; } }

    public Vector3 GetForwardAxisVector()
    {
        switch (forwardAxis)
        {
            case RoadPainterForwardAxis.PositiveX:
                return Vector3.right;
            case RoadPainterForwardAxis.NegativeX:
                return Vector3.left;
            case RoadPainterForwardAxis.PositiveZ:
                return Vector3.forward;
            case RoadPainterForwardAxis.NegativeZ:
                return Vector3.back;
            default:
                return Vector3.right;
        }
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            error = "Road Painter profile ID is empty.";
            return false;
        }

        if (segmentLength <= 0f)
        {
            error = "Road Painter segment length must be greater than zero.";
            return false;
        }

        if (SegmentPitch <= 0f)
        {
            error = "Road Painter segment pitch must be greater than zero.";
            return false;
        }

        if (roadWidth <= 0f)
        {
            error = "Road Painter road width must be greater than zero.";
            return false;
        }

        if (segmentParts == null || segmentParts.Count == 0)
        {
            error = "Road Painter profile has no segment parts.";
            return false;
        }

        for (int i = 0; i < segmentParts.Count; i++)
        {
            if (segmentParts[i] == null || segmentParts[i].prefab == null)
            {
                error = "Road Painter profile part " + i + " has no prefab.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    public static RoadPainterProfile CreateLowPolyMegapolisRoad01()
    {
        GameObject roadPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Road01PrefabPath);
        if (roadPrefab == null)
        {
            Debug.LogError("[RoadPainterProfile] Could not load Road_01 at '" + Road01PrefabPath + "'.");
            return null;
        }

        RoadPainterProfile profile = CreateInstance<RoadPainterProfile>();
        profile.profileId = "low_poly_megapolis_road_01";
        profile.displayName = "LowPoly Megapolis Road 01";
        profile.segmentLength = Road01SegmentLength;
        profile.segmentSpacing = 0f;
        profile.roadWidth = Road01HalfWidth * 2f;
        profile.forwardAxis = RoadPainterForwardAxis.PositiveX;
        profile.yOffset = 0f;
        profile.maximumSlopeDegrees = 10f;
        profile.angleSnapDegrees = 15f;
        profile.gridSnapSize = 0f;
        profile.disableSecondaryLodColliders = true;
        profile.segmentParts = new List<RoadPainterPrefabPart>
        {
            new RoadPainterPrefabPart
            {
                prefab = roadPrefab,
                localPositionOffset = new Vector3(0f, 0f, -Road01SeamOffset),
                localRotationOffset = Vector3.zero
            },
            new RoadPainterPrefabPart
            {
                prefab = roadPrefab,
                localPositionOffset = new Vector3(Road01SegmentLength, 0f, Road01SeamOffset),
                localRotationOffset = new Vector3(0f, 180f, 0f)
            }
        };

        return profile;
    }
}
