using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RoadPainterPathSamplerTests
{
    private const float SegmentLength = 6.0008683f;
    private const float HalfWidth = 3.006042f;
    private const float SeamOffset = 1.501783f;

    private Assembly roadPainterAssembly;
    private Type prefabPartType;
    private Type generatorType;
    private MethodInfo getPartPlacements;
    private MethodInfo calculatePartBounds;
    private MethodInfo buildSamples;

    [SetUp]
    public void SetUp()
    {
        roadPainterAssembly = FindRoadPainterAssembly();
        prefabPartType = roadPainterAssembly.GetType("RoadPainterPrefabPart");
        generatorType = roadPainterAssembly.GetType("RoadPainterGenerator");
        getPartPlacements = generatorType.GetMethod("GetPartPlacements");
        calculatePartBounds = generatorType.GetMethod("CalculatePartBounds");
        buildSamples = generatorType.GetMethod("BuildSamples");

        Assert.That(prefabPartType, Is.Not.Null);
        Assert.That(generatorType, Is.Not.Null);
        Assert.That(getPartPlacements, Is.Not.Null);
        Assert.That(calculatePartBounds, Is.Not.Null);
        Assert.That(buildSamples, Is.Not.Null);
    }

    [Test]
    public void CompoundRoad01_UsesTwoUnscaledPartsWithOpposedDirections()
    {
        IList placements = GetRoad01Placements(Vector3.zero, Quaternion.identity);

        Assert.That(placements.Count, Is.EqualTo(2));
        AssertScaleIsPositiveAndOne(placements[0]);
        AssertScaleIsPositiveAndOne(placements[1]);
        Assert.That(GetRotation(placements[0]).eulerAngles.y, Is.EqualTo(0f).Within(0.001f));
        Assert.That(GetRotation(placements[1]).eulerAngles.y, Is.EqualTo(180f).Within(0.001f));
    }

    [Test]
    public void CompoundRoad01_CoversOneSharedLengthAndBothSidesOfCenterline()
    {
        IList placements = GetRoad01Placements(Vector3.zero, Quaternion.identity);
        Bounds halfMeshBounds = new Bounds(
            new Vector3(SegmentLength * 0.5f, 0.075f, -0.001238f),
            new Vector3(SegmentLength, 0.15f, HalfWidth)
        );

        Bounds first = (Bounds)calculatePartBounds.Invoke(null, new[] { (object)halfMeshBounds, placements[0] });
        Bounds second = (Bounds)calculatePartBounds.Invoke(null, new[] { (object)halfMeshBounds, placements[1] });
        Bounds combined = first;
        combined.Encapsulate(second);

        Assert.That(first.min.x, Is.EqualTo(second.min.x).Within(0.001f));
        Assert.That(first.max.x, Is.EqualTo(second.max.x).Within(0.001f));
        Assert.That(first.max.z, Is.EqualTo(0f).Within(0.001f));
        Assert.That(second.min.z, Is.EqualTo(0f).Within(0.001f));
        Assert.That(combined.size.z, Is.EqualTo(HalfWidth * 2f).Within(0.001f));
    }

    [Test]
    public void CompoundRoad01_TransformsOffsetsWithTheSegmentRotation()
    {
        Quaternion parentRotation = Quaternion.Euler(0f, 90f, 0f);
        Vector3 parentPosition = new Vector3(10f, 0f, 20f);
        IList placements = GetRoad01Placements(parentPosition, parentRotation);

        Vector3 expectedFirstPosition = parentPosition + parentRotation * new Vector3(0f, 0f, -SeamOffset);
        Vector3 expectedSecondPosition = parentPosition + parentRotation * new Vector3(SegmentLength, 0f, SeamOffset);

        Assert.That(GetPosition(placements[0]), Is.EqualTo(expectedFirstPosition));
        Assert.That(GetPosition(placements[1]), Is.EqualTo(expectedSecondPosition));
        AssertScaleIsPositiveAndOne(placements[0]);
        AssertScaleIsPositiveAndOne(placements[1]);
    }

    [Test]
    public void Sampling_DoesNotCreatePartialOrOverlappingSegments()
    {
        IList samples = (IList)buildSamples.Invoke(
            null,
            new object[]
            {
                new List<Vector3>
                {
                    Vector3.zero,
                    Vector3.right * (SegmentLength * 2.5f)
                },
                SegmentLength
            }
        );

        Assert.That(samples.Count, Is.EqualTo(2));
        Assert.That(GetField<Vector3>(samples[0], "Position"), Is.EqualTo(Vector3.zero));
        Assert.That(GetField<Vector3>(samples[1], "Position").x, Is.EqualTo(SegmentLength).Within(0.001f));
    }

    private static Assembly FindRoadPainterAssembly()
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetType("RoadPainterGenerator") != null)
            {
                return assembly;
            }
        }

        Assert.Fail("RoadPainterGenerator was not loaded from an editor assembly.");
        return null;
    }

    private IList GetRoad01Placements(Vector3 position, Quaternion rotation)
    {
        Type listType = typeof(List<>).MakeGenericType(prefabPartType);
        IList parts = (IList)Activator.CreateInstance(listType);
        parts.Add(CreatePart(new Vector3(0f, 0f, -SeamOffset), Vector3.zero));
        parts.Add(CreatePart(new Vector3(SegmentLength, 0f, SeamOffset), new Vector3(0f, 180f, 0f)));

        return (IList)getPartPlacements.Invoke(null, new object[] { parts, position, rotation });
    }

    private object CreatePart(Vector3 localPositionOffset, Vector3 localRotationOffset)
    {
        object part = Activator.CreateInstance(prefabPartType);
        prefabPartType.GetField("localPositionOffset").SetValue(part, localPositionOffset);
        prefabPartType.GetField("localRotationOffset").SetValue(part, localRotationOffset);
        return part;
    }

    private static void AssertScaleIsPositiveAndOne(object placement)
    {
        Vector3 scale = GetField<Vector3>(placement, "LocalScale");
        Assert.That(scale, Is.EqualTo(Vector3.one));
        Assert.That(scale.x, Is.GreaterThan(0f));
        Assert.That(scale.y, Is.GreaterThan(0f));
        Assert.That(scale.z, Is.GreaterThan(0f));
    }

    private static Vector3 GetPosition(object placement)
    {
        return GetField<Vector3>(placement, "WorldPosition");
    }

    private static Quaternion GetRotation(object placement)
    {
        return GetField<Quaternion>(placement, "WorldRotation");
    }

    private static T GetField<T>(object instance, string fieldName)
    {
        FieldInfo field = instance.GetType().GetField(fieldName);
        Assert.That(field, Is.Not.Null);
        return (T)field.GetValue(instance);
    }
}
