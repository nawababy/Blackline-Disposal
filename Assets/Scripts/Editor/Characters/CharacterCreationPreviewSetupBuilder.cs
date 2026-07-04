using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CharacterCreationPreviewSetupBuilder
{
    private const string MenuPath = "Tools/Blackline Disposal/Characters/Build Character Creation Preview Setup";
    private const string CharacterCreationScenePath = "Assets/Scenes/CharacterCreation.unity";
    private const string TargetControllerPath = "Assets/GameData/Characters/Animation/Blackline_CharacterCreation_Preview.controller";
    private const string SourceIdleClipPath = "Assets/ithappy/City_Characters/Animations/Animation_Mesh/Aminset_Basic.fbx";
    private const string SourceIdleClipName = "Idle_Breathing";
    private const string PreviewStateName = "PreviewIdle";
    private const string PreviewDragAreaName = "PreviewDragArea";
    private const string ModularAppearanceRootName = "ModularAppearanceRoot";
    private const string CharacterCreationCanvasName = "CharacterCreationUI";
    private const float DefaultRotationSpeed = 0.25f;

    private sealed class DragAreaSetupResult
    {
        public RectTransform RectTransform;
        public bool Created;
        public bool UsedFallbackRect;
        public int ExcludedSelectableCount;
    }

    [MenuItem(MenuPath)]
    public static void BuildFromMenu()
    {
        try
        {
            BuildCharacterCreationPreviewSetup();
        }
        catch (Exception exception)
        {
            Debug.LogError("[CharacterCreationPreviewSetupBuilder] " + exception.Message);
        }
    }

    public static void BuildCharacterCreationPreviewSetup()
    {
        ValidateTargetFolder();

        AnimationClip idleClip = LoadAndValidateIdleClip();
        AnimatorController controller = CreateOrUpdatePreviewController(idleClip);
        ValidatePreviewController(controller, idleClip);

        DragAreaSetupResult dragArea = UpdateCharacterCreationScene(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string rectDescription = dragArea.RectTransform == null
            ? "<missing>"
            : "anchorMin=" + dragArea.RectTransform.anchorMin +
                ", anchorMax=" + dragArea.RectTransform.anchorMax +
                ", offsetMin=" + dragArea.RectTransform.offsetMin +
                ", offsetMax=" + dragArea.RectTransform.offsetMax;

        Debug.Log(
            "[CharacterCreationPreviewSetupBuilder] Build complete. " +
            "Controller '" + TargetControllerPath + "' uses clip '" + SourceIdleClipName + "'. " +
            "PreviewDragArea " + (dragArea.Created ? "created" : "updated") +
            (dragArea.UsedFallbackRect ? " with fallback rect. " : " using existing preview rect. ") +
            "Rect: " + rectDescription + ". " +
            "Excluded Selectables: " + dragArea.ExcludedSelectableCount + "."
        );
    }

    private static void ValidateTargetFolder()
    {
        string folder = Path.GetDirectoryName(TargetControllerPath);
        if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder.Replace('\\', '/')))
        {
            throw new InvalidOperationException("Missing target folder for preview controller: '" + folder + "'.");
        }
    }

    private static AnimationClip LoadAndValidateIdleClip()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SourceIdleClipPath);
        if (assets == null || assets.Length == 0)
        {
            throw new InvalidOperationException("Could not load source animation asset '" + SourceIdleClipPath + "'.");
        }

        AnimationClip result = null;
        int matches = 0;
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || !string.Equals(clip.name, SourceIdleClipName, StringComparison.Ordinal))
            {
                continue;
            }

            result = clip;
            matches++;
        }

        if (matches != 1 || result == null)
        {
            throw new InvalidOperationException("Expected exactly one AnimationClip sub-asset named '" + SourceIdleClipName + "' in '" + SourceIdleClipPath + "' but found " + matches + ".");
        }

        ValidateClip(result, SourceIdleClipPath + "/" + SourceIdleClipName);
        return result;
    }

    private static AnimatorController CreateOrUpdatePreviewController(AnimationClip idleClip)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetControllerPath);
        if (controller == null)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TargetControllerPath) != null)
            {
                throw new InvalidOperationException("Target asset is not an AnimatorController: '" + TargetControllerPath + "'.");
            }

            controller = AnimatorController.CreateAnimatorControllerAtPath(TargetControllerPath);
        }

        if (controller == null)
        {
            throw new InvalidOperationException("Could not create preview AnimatorController at '" + TargetControllerPath + "'.");
        }

        RemoveAllParameters(controller);

        AnimatorControllerLayer layer = EnsureSingleBaseLayer(controller);
        AnimatorStateMachine stateMachine = layer.stateMachine;
        AnimatorState previewState = EnsureOnlyPreviewState(stateMachine);

        previewState.name = PreviewStateName;
        previewState.motion = idleClip;
        previewState.speed = 1f;
        previewState.writeDefaultValues = true;
        previewState.iKOnFeet = false;

        RemoveStateTransitions(previewState);
        stateMachine.defaultState = previewState;

        EditorUtility.SetDirty(previewState);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void RemoveAllParameters(AnimatorController controller)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = parameters.Length - 1; i >= 0; i--)
        {
            controller.RemoveParameter(parameters[i]);
        }
    }

    private static AnimatorControllerLayer EnsureSingleBaseLayer(AnimatorController controller)
    {
        AnimatorControllerLayer layer;
        if (controller.layers.Length > 0)
        {
            layer = controller.layers[0];
        }
        else
        {
            layer = new AnimatorControllerLayer();
        }

        if (layer.stateMachine == null)
        {
            AnimatorStateMachine stateMachine = new AnimatorStateMachine
            {
                name = "Base Layer"
            };

            AssetDatabase.AddObjectToAsset(stateMachine, controller);
            layer.stateMachine = stateMachine;
        }

        layer.name = "Base Layer";
        layer.defaultWeight = 1f;
        controller.layers = new[] { layer };
        return layer;
    }

    private static AnimatorState EnsureOnlyPreviewState(AnimatorStateMachine stateMachine)
    {
        ClearStateMachineTransitions(stateMachine);

        AnimatorState previewState = null;
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState state = states[i].state;
            if (state == null)
            {
                continue;
            }

            if (previewState == null && string.Equals(state.name, PreviewStateName, StringComparison.Ordinal))
            {
                previewState = state;
                continue;
            }

            stateMachine.RemoveState(state);
        }

        ChildAnimatorStateMachine[] childStateMachines = stateMachine.stateMachines;
        for (int i = 0; i < childStateMachines.Length; i++)
        {
            if (childStateMachines[i].stateMachine != null)
            {
                stateMachine.RemoveStateMachine(childStateMachines[i].stateMachine);
            }
        }

        if (previewState == null)
        {
            previewState = stateMachine.AddState(PreviewStateName, new Vector3(260f, 80f, 0f));
        }

        return previewState;
    }

    private static void ClearStateMachineTransitions(AnimatorStateMachine stateMachine)
    {
        AnimatorTransition[] entryTransitions = stateMachine.entryTransitions;
        for (int i = entryTransitions.Length - 1; i >= 0; i--)
        {
            stateMachine.RemoveEntryTransition(entryTransitions[i]);
        }

        AnimatorStateTransition[] anyStateTransitions = stateMachine.anyStateTransitions;
        for (int i = anyStateTransitions.Length - 1; i >= 0; i--)
        {
            stateMachine.RemoveAnyStateTransition(anyStateTransitions[i]);
        }
    }

    private static void RemoveStateTransitions(AnimatorState state)
    {
        AnimatorStateTransition[] transitions = state.transitions;
        for (int i = transitions.Length - 1; i >= 0; i--)
        {
            state.RemoveTransition(transitions[i]);
        }
    }

    private static DragAreaSetupResult UpdateCharacterCreationScene(AnimatorController controller)
    {
        EnsureNoUnsavedScenesWillBeLost();

        Scene scene = EditorSceneManager.OpenScene(CharacterCreationScenePath, OpenSceneMode.Single);

        CharacterAppearanceCreationController creationController = FindSingleObject<CharacterAppearanceCreationController>("CharacterAppearanceCreationController");
        CharacterAppearanceApplier applier = FindAssignedApplier(creationController);
        Transform previewRoot = FindSingleTransform(ModularAppearanceRootName);
        Canvas canvas = FindCharacterCreationCanvas();
        EnsureEventSystemExists();

        SetObjectReference(applier, "previewAnimatorController", controller);

        DragAreaSetupResult dragArea = CreateOrUpdatePreviewDragArea(canvas, previewRoot);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException("Could not save scene '" + CharacterCreationScenePath + "'.");
        }

        return dragArea;
    }

    private static CharacterAppearanceApplier FindAssignedApplier(CharacterAppearanceCreationController creationController)
    {
        SerializedObject serializedController = new SerializedObject(creationController);
        SerializedProperty applierProperty = RequireProperty(serializedController, "applier");
        CharacterAppearanceApplier applier = applierProperty.objectReferenceValue as CharacterAppearanceApplier;

        if (applier == null)
        {
            applier = creationController.GetComponent<CharacterAppearanceApplier>();
        }

        if (applier == null)
        {
            throw new InvalidOperationException("CharacterAppearanceCreationController has no assigned CharacterAppearanceApplier.");
        }

        return applier;
    }

    private static DragAreaSetupResult CreateOrUpdatePreviewDragArea(Canvas canvas, Transform previewRoot)
    {
        RectTransform sourceRect = FindSuitablePreviewRect(canvas.transform);
        RectTransform dragRect = FindNamedRectTransform(canvas.transform, PreviewDragAreaName);
        bool created = false;
        bool usedFallback = false;
        bool existingDragArea = dragRect != null;

        if (dragRect == null)
        {
            Transform parent = sourceRect != null && sourceRect.parent != null
                ? sourceRect.parent
                : canvas.transform;

            GameObject dragAreaObject = new GameObject(PreviewDragAreaName, typeof(RectTransform), typeof(Image), typeof(CharacterPreviewRotation));
            Undo.RegisterCreatedObjectUndo(dragAreaObject, "Create Preview Drag Area");
            dragAreaObject.transform.SetParent(parent, false);
            dragRect = dragAreaObject.GetComponent<RectTransform>();
            created = true;
        }

        if (!existingDragArea && sourceRect != null)
        {
            CopyRectTransform(sourceRect, dragRect);
        }
        else if (!existingDragArea)
        {
            ApplyFallbackRect(dragRect);
            usedFallback = true;
        }

        if (created)
        {
            dragRect.SetAsLastSibling();
        }

        Image image = dragRect.GetComponent<Image>();
        if (image == null)
        {
            Undo.AddComponent<Image>(dragRect.gameObject);
            image = dragRect.GetComponent<Image>();
        }

        if (!existingDragArea)
        {
            Undo.RecordObject(image, "Configure Preview Drag Area Image");
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = true;
            image.sprite = null;
            image.maskable = true;
            EditorUtility.SetDirty(image);
        }

        CharacterPreviewRotation rotation = dragRect.GetComponent<CharacterPreviewRotation>();
        bool addedRotation = false;
        if (rotation == null)
        {
            Undo.AddComponent<CharacterPreviewRotation>(dragRect.gameObject);
            rotation = dragRect.GetComponent<CharacterPreviewRotation>();
            addedRotation = true;
        }

        Selectable[] selectables = canvas.GetComponentsInChildren<Selectable>(true);
        RectTransform[] selectableRects = GetSelectableRects(selectables);

        SerializedObject rotationObject = new SerializedObject(rotation);
        if (!existingDragArea || addedRotation)
        {
            SetObjectReference(rotationObject, "previewRoot", previewRoot);
            SetObjectReference(rotationObject, "targetCanvas", canvas);
            SetFloat(rotationObject, "rotationSpeed", DefaultRotationSpeed);
            SetObjectArray(rotationObject, "excludedControls", selectables);
            SetObjectArray(rotationObject, "excludedUiAreas", selectableRects);
        }

        SetBool(rotationObject, "invertDragDirection", true);
        rotationObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(rotation);
        EditorUtility.SetDirty(dragRect);

        return new DragAreaSetupResult
        {
            RectTransform = dragRect,
            Created = created,
            UsedFallbackRect = usedFallback,
            ExcludedSelectableCount = selectables.Length
        };
    }

    private static RectTransform FindSuitablePreviewRect(Transform root)
    {
        string[] preferredNames =
        {
            "PreviewViewport",
            "CharacterPreview",
            "CharacterPreviewArea",
            "PreviewArea",
            "PreviewParent"
        };

        RectTransform result = null;
        int matches = 0;
        for (int nameIndex = 0; nameIndex < preferredNames.Length; nameIndex++)
        {
            RectTransform candidate = FindNamedRectTransform(root, preferredNames[nameIndex]);
            if (candidate == null || HasSelectableInChildren(candidate))
            {
                continue;
            }

            result = candidate;
            matches++;
        }

        return matches == 1 ? result : null;
    }

    private static bool HasSelectableInChildren(RectTransform root)
    {
        return root != null && root.GetComponentsInChildren<Selectable>(true).Length > 0;
    }

    private static RectTransform FindNamedRectTransform(Transform root, string objectName)
    {
        RectTransform[] rectTransforms = root.GetComponentsInChildren<RectTransform>(true);
        RectTransform result = null;
        int matches = 0;
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            if (!string.Equals(rectTransforms[i].name, objectName, StringComparison.Ordinal))
            {
                continue;
            }

            result = rectTransforms[i];
            matches++;
        }

        return matches == 1 ? result : null;
    }

    private static void CopyRectTransform(RectTransform source, RectTransform target)
    {
        Undo.RecordObject(target, "Configure Preview Drag Area RectTransform");
        target.anchorMin = source.anchorMin;
        target.anchorMax = source.anchorMax;
        target.anchoredPosition = source.anchoredPosition;
        target.sizeDelta = source.sizeDelta;
        target.pivot = source.pivot;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
        target.offsetMin = source.offsetMin;
        target.offsetMax = source.offsetMax;
    }

    private static void ApplyFallbackRect(RectTransform target)
    {
        Undo.RecordObject(target, "Configure Preview Drag Area RectTransform");
        target.anchorMin = new Vector2(0.25f, 0.08f);
        target.anchorMax = new Vector2(0.75f, 0.92f);
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
        target.pivot = new Vector2(0.5f, 0.5f);
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private static RectTransform[] GetSelectableRects(Selectable[] selectables)
    {
        RectTransform[] rects = new RectTransform[selectables.Length];
        for (int i = 0; i < selectables.Length; i++)
        {
            rects[i] = selectables[i] == null ? null : selectables[i].transform as RectTransform;
        }

        return rects;
    }

    private static T FindSingleObject<T>(string label) where T : UnityEngine.Object
    {
        T[] objects = UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (objects.Length != 1)
        {
            throw new InvalidOperationException("Expected exactly one " + label + " in '" + CharacterCreationScenePath + "' but found " + objects.Length + ".");
        }

        return objects[0];
    }

    private static Transform FindSingleTransform(string transformName)
    {
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Transform result = null;
        int matches = 0;
        for (int i = 0; i < transforms.Length; i++)
        {
            if (!string.Equals(transforms[i].name, transformName, StringComparison.Ordinal))
            {
                continue;
            }

            result = transforms[i];
            matches++;
        }

        if (matches != 1 || result == null)
        {
            throw new InvalidOperationException("Expected exactly one Transform named '" + transformName + "' in '" + CharacterCreationScenePath + "' but found " + matches + ".");
        }

        return result;
    }

    private static Canvas FindCharacterCreationCanvas()
    {
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        Canvas namedCanvas = null;
        int namedMatches = 0;
        Canvas activeCanvas = null;
        int activeMatches = 0;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null)
            {
                continue;
            }

            if (string.Equals(canvas.name, CharacterCreationCanvasName, StringComparison.Ordinal))
            {
                namedCanvas = canvas;
                namedMatches++;
            }

            if (canvas.gameObject.activeInHierarchy)
            {
                activeCanvas = canvas;
                activeMatches++;
            }
        }

        if (namedMatches == 1 && namedCanvas != null)
        {
            return namedCanvas;
        }

        if (activeMatches == 1 && activeCanvas != null)
        {
            return activeCanvas;
        }

        throw new InvalidOperationException("Could not find an unambiguous CharacterCreation Canvas. Named matches: " + namedMatches + ", active canvases: " + activeMatches + ".");
    }

    private static void EnsureEventSystemExists()
    {
        EventSystem[] eventSystems = UnityEngine.Object.FindObjectsByType<EventSystem>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        if (eventSystems.Length == 0)
        {
            throw new InvalidOperationException("CharacterCreation scene has no EventSystem.");
        }
    }

    private static void EnsureNoUnsavedScenesWillBeLost()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.isDirty)
            {
                continue;
            }

            if (Application.isBatchMode)
            {
                throw new InvalidOperationException("Cannot open " + CharacterCreationScenePath + " because scene '" + scene.path + "' has unsaved changes.");
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new InvalidOperationException("CharacterCreation preview setup cancelled because current scene changes were not saved.");
            }

            break;
        }
    }

    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serializedObject = new SerializedObject(target);
        SetObjectReference(serializedObject, propertyName, value);
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(target);
    }

    private static void SetObjectReference(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.objectReferenceValue = value;
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.floatValue = value;
    }

    private static void SetBool(SerializedObject serializedObject, string propertyName, bool value)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.boolValue = value;
    }

    private static void SetObjectArray(SerializedObject serializedObject, string propertyName, UnityEngine.Object[] values)
    {
        SerializedProperty property = RequireProperty(serializedObject, propertyName);
        property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }

    private static SerializedProperty RequireProperty(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException("Missing serialized field '" + propertyName + "' on " + serializedObject.targetObject.name + ".");
        }

        return property;
    }

    private static void ValidatePreviewController(AnimatorController controller, AnimationClip idleClip)
    {
        if (controller == null)
        {
            throw new InvalidOperationException("Preview controller validation failed because the controller is null.");
        }

        if (controller.parameters.Length != 0)
        {
            throw new InvalidOperationException("Preview controller validation failed: controller has parameters.");
        }

        if (controller.layers.Length != 1)
        {
            throw new InvalidOperationException("Preview controller validation failed: expected one Base Layer but found " + controller.layers.Length + ".");
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        if (stateMachine == null)
        {
            throw new InvalidOperationException("Preview controller validation failed: Base Layer has no state machine.");
        }

        if (stateMachine.states.Length != 1)
        {
            throw new InvalidOperationException("Preview controller validation failed: expected one state but found " + stateMachine.states.Length + ".");
        }

        AnimatorState state = stateMachine.states[0].state;
        if (state == null ||
            !string.Equals(state.name, PreviewStateName, StringComparison.Ordinal) ||
            state.motion != idleClip ||
            stateMachine.defaultState != state ||
            state.transitions.Length != 0)
        {
            throw new InvalidOperationException("Preview controller validation failed: PreviewIdle state is not configured correctly.");
        }

        if (stateMachine.entryTransitions.Length != 0 ||
            stateMachine.anyStateTransitions.Length != 0 ||
            stateMachine.stateMachines.Length != 0)
        {
            throw new InvalidOperationException("Preview controller validation failed: unexpected transitions or child state machines exist.");
        }
    }

    private static void ValidateClip(AnimationClip clip, string path)
    {
        if (clip == null)
        {
            throw new InvalidOperationException("Missing AnimationClip '" + path + "'.");
        }

        if (clip.legacy)
        {
            throw new InvalidOperationException("AnimationClip '" + path + "' is marked as Legacy.");
        }

        ValidateClipHasCurves(clip, path);
        ValidateClipHasHumanoidCurves(clip, path);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        if (!settings.loopTime)
        {
            throw new InvalidOperationException("AnimationClip '" + path + "' is not looped.");
        }
    }

    private static void ValidateClipHasCurves(AnimationClip clip, string path)
    {
        if (AnimationUtility.GetCurveBindings(clip).Length == 0 &&
            AnimationUtility.GetObjectReferenceCurveBindings(clip).Length == 0)
        {
            throw new InvalidOperationException("AnimationClip '" + path + "' has no animation curves.");
        }
    }

    private static void ValidateClipHasHumanoidCurves(AnimationClip clip, string path)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        for (int i = 0; i < bindings.Length; i++)
        {
            if (bindings[i].type == typeof(Animator) &&
                !string.IsNullOrEmpty(bindings[i].propertyName))
            {
                return;
            }
        }

        throw new InvalidOperationException("AnimationClip '" + path + "' has no humanoid Animator curves.");
    }
}
