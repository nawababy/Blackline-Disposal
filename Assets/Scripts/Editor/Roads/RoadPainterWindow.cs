using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public sealed class RoadPainterWindow : EditorWindow
{
    private const int MaximumPreviewSegments = 128;

    private enum SceneMode
    {
        Paint,
        Edit,
        Erase,
        Select
    }

    private RoadPainterPath path;
    private RoadPainterProfile profile;
    private Transform roadRoot;
    private Terrain terrain;
    private LayerMask terrainLayerMask = ~0;
    private SceneMode sceneMode;
    private bool livePreview = true;
    private bool isPainting;
    private int strokeStartPointCount;
    private RoadPainterPath synchronizedPath;

    [MenuItem("Tools/Blackline Disposal/Road Painter")]
    public static void Open()
    {
        GetWindow<RoadPainterWindow>("Road Painter");
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGui;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Road Painter", EditorStyles.boldLabel);
        path = (RoadPainterPath)EditorGUILayout.ObjectField("Road Path", path, typeof(RoadPainterPath), false);
        if (path != synchronizedPath)
        {
            synchronizedPath = path;
            SynchronizeFieldsFromPath();
        }

        profile = (RoadPainterProfile)EditorGUILayout.ObjectField("Road Profile", profile, typeof(RoadPainterProfile), false);
        roadRoot = (Transform)EditorGUILayout.ObjectField("Parent Root", roadRoot, typeof(Transform), true);
        terrain = (Terrain)EditorGUILayout.ObjectField("Terrain", terrain, typeof(Terrain), true);
        terrainLayerMask = EditorGUILayout.MaskField("Terrain LayerMask", terrainLayerMask, InternalEditorUtility.layers);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Road 01 Profile"))
            {
                CreateDefaultProfile();
            }

            if (GUILayout.Button("Create Road Path"))
            {
                CreateRoadPath();
            }
        }

        if (path != null)
        {
            if (profile != null && roadRoot != null)
            {
                if (GUILayout.Button("Apply Path Bindings"))
                {
                    Undo.RecordObject(path, "Configure Road Path");
                    path.Configure(profile, roadRoot, terrain, terrainLayerMask);
                    SceneView.RepaintAll();
                }
            }

            EditorGUILayout.Space();
            sceneMode = (SceneMode)GUILayout.Toolbar((int)sceneMode, new[] { "Paint", "Edit", "Erase", "Select" });
            livePreview = EditorGUILayout.Toggle("Live Preview", livePreview);

            RoadPainterProfile activeProfile = path.Profile;
            if (activeProfile != null)
            {
                EditorGUILayout.LabelField("Segment Parts", activeProfile.SegmentParts.Count.ToString());
                EditorGUILayout.LabelField("Segment Length", activeProfile.SegmentLength.ToString("0.####") + " m");
                EditorGUILayout.LabelField("Road Width", activeProfile.RoadWidth.ToString("0.###") + " m");
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate"))
                {
                    GeneratePath();
                }

                if (GUILayout.Button("Regenerate"))
                {
                    GeneratePath();
                }

                if (GUILayout.Button("Clear Generated"))
                {
                    RoadPainterGenerator.ClearGenerated(path);
                }
            }

            using (new EditorGUI.DisabledScope(true))
            {
                GUILayout.Button("Convert Existing Selection (Version 2)");
            }
        }

        EditorGUILayout.HelpBox(
            "Paint adds control points to the selected Road Path. Enter generates the path; Escape removes points added during the current stroke. Generated prefabs stay under the selected parent root.",
            MessageType.Info
        );
    }

    private void OnSceneGui(SceneView sceneView)
    {
        if (path == null || path.Profile == null || !path.TryResolveRoadRoot(out Transform resolvedRoot))
        {
            return;
        }

        Event currentEvent = Event.current;
        DrawPathHandles();
        if (livePreview)
        {
            DrawLivePreview();
        }

        switch (sceneMode)
        {
            case SceneMode.Paint:
                HandlePaint(currentEvent);
                break;
            case SceneMode.Edit:
                HandleEdit();
                break;
            case SceneMode.Erase:
                HandleErase();
                break;
        }
    }

    private void HandlePaint(Event currentEvent)
    {
        if (currentEvent.alt)
        {
            return;
        }

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (currentEvent.type == EventType.Layout)
        {
            HandleUtility.AddDefaultControl(controlId);
        }

        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Escape && isPainting)
        {
            Undo.RecordObject(path, "Cancel Road Paint Stroke");
            path.RemoveControlPointsFrom(strokeStartPointCount);
            isPainting = false;
            currentEvent.Use();
            SceneView.RepaintAll();
            return;
        }

        if (currentEvent.type == EventType.KeyDown && currentEvent.keyCode == KeyCode.Return && isPainting)
        {
            isPainting = false;
            GeneratePath();
            currentEvent.Use();
            return;
        }

        Vector3 worldPoint;
        if (!TryGetPaintPoint(currentEvent.mousePosition, out worldPoint))
        {
            return;
        }

        Handles.color = Color.yellow;
        Handles.DrawWireDisc(worldPoint, Vector3.up, 0.35f);

        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
        {
            isPainting = true;
            strokeStartPointCount = path.ControlPointCount;
            AddPaintPoint(worldPoint, "Paint Road Point");
            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0 && isPainting)
        {
            List<Vector3> points = path.GetWorldControlPoints();
            if (points.Count == 0 || Vector3.Distance(points[points.Count - 1], worldPoint) >= Mathf.Max(0.25f, path.Profile.SegmentLength * 0.25f))
            {
                AddPaintPoint(worldPoint, "Extend Road Paint Stroke");
            }

            currentEvent.Use();
            return;
        }

        if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0 && isPainting)
        {
            currentEvent.Use();
        }
    }

    private void HandleEdit()
    {
        List<Vector3> points = path.GetWorldControlPoints();
        for (int i = 0; i < points.Count; i++)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 updated = Handles.PositionHandle(points[i], Quaternion.identity);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(path, "Move Road Control Point");
                path.SetWorldControlPoint(i, ApplySnapping(updated));
            }
        }
    }

    private void HandleErase()
    {
        List<Vector3> points = path.GetWorldControlPoints();
        for (int i = points.Count - 1; i >= 0; i--)
        {
            if (!Handles.Button(points[i], Quaternion.identity, 0.2f, 0.2f, Handles.DotHandleCap))
            {
                continue;
            }

            Undo.RecordObject(path, "Erase Road Control Point");
            path.RemoveControlPointAt(i);
            SceneView.RepaintAll();
            break;
        }
    }

    private void DrawPathHandles()
    {
        List<Vector3> points = path.GetWorldControlPoints();
        if (points.Count == 0)
        {
            return;
        }

        Handles.color = Color.cyan;
        for (int i = 0; i < points.Count; i++)
        {
            Handles.SphereHandleCap(0, points[i], Quaternion.identity, 0.18f, EventType.Repaint);
            if (i > 0)
            {
                Handles.DrawLine(points[i - 1], points[i]);
            }
        }
    }

    private void DrawLivePreview()
    {
        RoadPainterProfile activeProfile = path.Profile;
        List<RoadPainterSegmentSample> samples = RoadPainterGenerator.BuildSamples(path.GetWorldControlPoints(), activeProfile.SegmentPitch);
        if (samples.Count == 0)
        {
            return;
        }

        int stride = Mathf.Max(1, Mathf.CeilToInt(samples.Count / (float)MaximumPreviewSegments));
        for (int i = 0; i < samples.Count; i += stride)
        {
            RoadPainterSegmentPose pose;
            if (!RoadPainterGenerator.TryGetSegmentPose(path, activeProfile, samples[i], out pose))
            {
                continue;
            }

            Handles.matrix = Matrix4x4.TRS(
                pose.Position + pose.Rotation * Vector3.right * (activeProfile.SegmentLength * 0.5f),
                pose.Rotation,
                Vector3.one
            );
            Handles.color = new Color(0.2f, 1f, 0.7f, 0.75f);
            Handles.DrawWireCube(Vector3.zero, new Vector3(activeProfile.SegmentLength, 0.03f, activeProfile.RoadWidth));

            List<RoadPainterPartPlacement> placements = RoadPainterGenerator.GetPartPlacements(activeProfile.SegmentParts, pose.Position, pose.Rotation);
            for (int partIndex = 0; partIndex < placements.Count; partIndex++)
            {
                RoadPainterPartPlacement placement = placements[partIndex];
                Handles.matrix = Matrix4x4.identity;
                Handles.color = partIndex == 0 ? Color.green : Color.magenta;
                Handles.ArrowHandleCap(0, placement.WorldPosition, placement.WorldRotation, 0.65f, EventType.Repaint);
            }
        }

        Handles.matrix = Matrix4x4.identity;
    }

    private bool TryGetPaintPoint(Vector2 mousePosition, out Vector3 worldPoint)
    {
        worldPoint = Vector3.zero;
        if (!path.TryResolveTerrain(out Terrain resolvedTerrain) || resolvedTerrain.terrainData == null)
        {
            return false;
        }

        Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
        TerrainCollider terrainCollider = resolvedTerrain.GetComponent<TerrainCollider>();
        RaycastHit[] hits = Physics.RaycastAll(ray, 5000f, path.TerrainLayerMask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].collider != terrainCollider)
            {
                continue;
            }

            worldPoint = ApplySnapping(hits[i].point);
            return true;
        }

        Plane terrainPlane = new Plane(Vector3.up, resolvedTerrain.transform.position);
        float enter;
        if (!terrainPlane.Raycast(ray, out enter))
        {
            return false;
        }

        Vector3 planePoint = ray.GetPoint(enter);
        planePoint.y = resolvedTerrain.SampleHeight(planePoint) + resolvedTerrain.transform.position.y;
        worldPoint = ApplySnapping(planePoint);
        return true;
    }

    private Vector3 ApplySnapping(Vector3 worldPoint)
    {
        if (!path.TryResolveRoadRoot(out Transform resolvedRoot))
        {
            return worldPoint;
        }

        Vector3 localPoint = resolvedRoot.InverseTransformPoint(worldPoint);
        localPoint.y = 0f;
        RoadPainterProfile activeProfile = path.Profile;
        List<Vector3> existingPoints = path.GetWorldControlPoints();
        if (activeProfile.AngleSnapDegrees > 0f && existingPoints.Count > 0)
        {
            Vector3 previousLocal = resolvedRoot.InverseTransformPoint(existingPoints[existingPoints.Count - 1]);
            Vector3 delta = localPoint - previousLocal;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(delta.z, delta.x) * Mathf.Rad2Deg;
                float snappedAngle = Mathf.Round(angle / activeProfile.AngleSnapDegrees) * activeProfile.AngleSnapDegrees;
                float distance = delta.magnitude;
                float radians = snappedAngle * Mathf.Deg2Rad;
                localPoint = previousLocal + new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * distance;
            }
        }

        if (activeProfile.GridSnapSize > 0f)
        {
            float grid = activeProfile.GridSnapSize;
            localPoint.x = Mathf.Round(localPoint.x / grid) * grid;
            localPoint.z = Mathf.Round(localPoint.z / grid) * grid;
        }

        return resolvedRoot.TransformPoint(localPoint);
    }

    private void AddPaintPoint(Vector3 worldPoint, string undoName)
    {
        Undo.RecordObject(path, undoName);
        path.AddWorldControlPoint(ApplySnapping(worldPoint));
        SceneView.RepaintAll();
    }

    private void GeneratePath()
    {
        string error;
        if (!RoadPainterGenerator.Generate(path, out error))
        {
            Debug.LogError("[RoadPainterWindow] " + error, path);
        }

        SceneView.RepaintAll();
    }

    private void SynchronizeFieldsFromPath()
    {
        if (path.Profile != null)
        {
            profile = path.Profile;
        }

        Transform resolvedRoot;
        if (path.TryResolveRoadRoot(out resolvedRoot))
        {
            roadRoot = resolvedRoot;
        }

        Terrain resolvedTerrain;
        if (path.TryResolveTerrain(out resolvedTerrain))
        {
            terrain = resolvedTerrain;
        }

        terrainLayerMask = path.TerrainLayerMask;
    }

    private void CreateDefaultProfile()
    {
        string pathName = EditorUtility.SaveFilePanelInProject(
            "Create LowPoly Megapolis Road 01 Profile",
            "LowPolyMegapolisRoad01",
            "asset",
            "Choose a project location for the Road Painter profile."
        );
        if (string.IsNullOrEmpty(pathName))
        {
            return;
        }

        RoadPainterProfile createdProfile = RoadPainterProfile.CreateLowPolyMegapolisRoad01();
        if (createdProfile == null)
        {
            return;
        }

        AssetDatabase.CreateAsset(createdProfile, pathName);
        AssetDatabase.SaveAssets();
        profile = createdProfile;
        Selection.activeObject = createdProfile;
    }

    private void CreateRoadPath()
    {
        if (profile == null || roadRoot == null)
        {
            Debug.LogError("[RoadPainterWindow] Assign a Road Profile and Parent Root before creating a Road Path.");
            return;
        }

        string pathName = EditorUtility.SaveFilePanelInProject(
            "Create Road Painter Path",
            "RoadPainterPath",
            "asset",
            "Choose a project location for the editable Road Painter path."
        );
        if (string.IsNullOrEmpty(pathName))
        {
            return;
        }

        RoadPainterPath createdPath = CreateInstance<RoadPainterPath>();
        createdPath.Configure(profile, roadRoot, terrain, terrainLayerMask);
        AssetDatabase.CreateAsset(createdPath, pathName);
        AssetDatabase.SaveAssets();
        path = createdPath;
        Selection.activeObject = createdPath;
    }
}
