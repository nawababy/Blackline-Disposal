using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PlayerMovementAnimatorControllerBuilder
{
    private const string MenuPath = "Tools/Blackline Disposal/Characters/Build Player Crouch Setup";

    private const string TargetFolder = "Assets/GameData/Characters/Animation";
    private const string GameScenePath = "Assets/Scenes/GameScene.unity";
    private const string SourceControllerPath = "Assets/ithappy/City_Characters/Animations/Animation_Controllers/Character_Movement.controller";
    private const string TargetControllerPath = TargetFolder + "/Blackline_Player_Movement.controller";

    private const string CrouchParameter = "IsCrouching";
    private const string HorizontalParameter = "Hor";
    private const string VerticalParameter = "Vert";
    private const string StateParameter = "State";
    private const string JumpParameter = "IsJump";

    private const string MovementStateName = "Movement";
    private const string JumpStateName = "Jump";
    private const string CrouchStateName = "Crouch";
    private const string CrouchBlendTreeName = "Crouch Blend Tree";

    private const float TransitionDuration = 0.1f;
    private const float CrouchIdleLength = 1f;

    private static readonly ClipSpec[] CrouchWalkClips =
    {
        new ClipSpec(
            "Assets/ithappy/City_Characters/Animations/Other_Animations/Crouch_Walk_Forward.anim",
            TargetFolder + "/Blackline_Crouch_Walk_Forward.anim",
            "Blackline_Crouch_Walk_Forward",
            new Vector2(0f, 1f)),
        new ClipSpec(
            "Assets/ithappy/City_Characters/Animations/Other_Animations/Crouch_Walk_Backward.anim",
            TargetFolder + "/Blackline_Crouch_Walk_Backward.anim",
            "Blackline_Crouch_Walk_Backward",
            new Vector2(0f, -1f)),
        new ClipSpec(
            "Assets/ithappy/City_Characters/Animations/Other_Animations/Crouch_Walk_Left.anim",
            TargetFolder + "/Blackline_Crouch_Walk_Left.anim",
            "Blackline_Crouch_Walk_Left",
            new Vector2(-1f, 0f)),
        new ClipSpec(
            "Assets/ithappy/City_Characters/Animations/Other_Animations/Crouch_Walk_Right.anim",
            TargetFolder + "/Blackline_Crouch_Walk_Right.anim",
            "Blackline_Crouch_Walk_Right",
            new Vector2(1f, 0f))
    };

    private static readonly ClipSpec CrouchIdleClip =
        new ClipSpec(
            string.Empty,
            TargetFolder + "/Blackline_Crouch_Idle.anim",
            "Blackline_Crouch_Idle",
            Vector2.zero);

    private sealed class ClipSpec
    {
        public readonly string SourcePath;
        public readonly string TargetPath;
        public readonly string TargetName;
        public readonly Vector2 BlendPosition;

        public ClipSpec(string sourcePath, string targetPath, string targetName, Vector2 blendPosition)
        {
            SourcePath = sourcePath;
            TargetPath = targetPath;
            TargetName = targetName;
            BlendPosition = blendPosition;
        }
    }

    private sealed class PlayerMovementLayer
    {
        public readonly AnimatorControllerLayer Layer;
        public readonly AnimatorState MovementState;
        public readonly AnimatorState JumpState;

        public PlayerMovementLayer(AnimatorControllerLayer layer, AnimatorState movementState, AnimatorState jumpState)
        {
            Layer = layer;
            MovementState = movementState;
            JumpState = jumpState;
        }
    }

    [MenuItem(MenuPath)]
    public static void BuildFromMenu()
    {
        try
        {
            BuildAndAssignForGameScene();
        }
        catch (Exception exception)
        {
            Debug.LogError("[PlayerMovementAnimatorControllerBuilder] " + exception.Message);
        }
    }

    public static void BuildAndAssignForGameScene()
    {
        EnsureTargetFolder();
        ValidateSources();

        AnimationClip forwardClip = null;
        AnimationClip backwardClip = null;
        AnimationClip leftClip = null;
        AnimationClip rightClip = null;

        for (int i = 0; i < CrouchWalkClips.Length; i++)
        {
            AnimationClip copiedClip = CreateOrUpdateCrouchWalkClip(CrouchWalkClips[i]);
            if (CrouchWalkClips[i].TargetName.EndsWith("Forward", StringComparison.Ordinal))
            {
                forwardClip = copiedClip;
            }
            else if (CrouchWalkClips[i].TargetName.EndsWith("Backward", StringComparison.Ordinal))
            {
                backwardClip = copiedClip;
            }
            else if (CrouchWalkClips[i].TargetName.EndsWith("Left", StringComparison.Ordinal))
            {
                leftClip = copiedClip;
            }
            else if (CrouchWalkClips[i].TargetName.EndsWith("Right", StringComparison.Ordinal))
            {
                rightClip = copiedClip;
            }
        }

        AnimationClip idleClip =
            CreateOrUpdateCrouchIdleClip(forwardClip);

        AnimatorController controller =
            CreateOrUpdateAnimatorController(
                idleClip,
                forwardClip,
                backwardClip,
                leftClip,
                rightClip
            );

        UpdateGameScene(controller);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[PlayerMovementAnimatorControllerBuilder] Build complete. Created or updated player crouch clips, controller and GameScene assignment."
        );
    }

    private static void EnsureTargetFolder()
    {
        EnsureFolder("Assets/GameData");
        EnsureFolder("Assets/GameData/Characters");
        EnsureFolder(TargetFolder);
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string parent = Path.GetDirectoryName(folder);
        string name = Path.GetFileName(folder);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            throw new InvalidOperationException("Invalid folder path '" + folder + "'.");
        }

        string guid = AssetDatabase.CreateFolder(parent.Replace('\\', '/'), name);
        if (string.IsNullOrEmpty(guid))
        {
            throw new InvalidOperationException("Could not create folder '" + folder + "'.");
        }
    }

    private static void ValidateSources()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(SourceControllerPath) == null)
        {
            throw new InvalidOperationException("Missing source controller '" + SourceControllerPath + "'.");
        }

        for (int i = 0; i < CrouchWalkClips.Length; i++)
        {
            ClipSpec spec = CrouchWalkClips[i];
            AnimationClip sourceClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(spec.SourcePath);

            if (sourceClip == null)
            {
                throw new InvalidOperationException("Missing source AnimationClip '" + spec.SourcePath + "'.");
            }

            ValidateClipHasCurves(sourceClip, spec.SourcePath);
            ValidateClipHasHumanoidCurves(sourceClip, spec.SourcePath);
        }
    }

    private static AnimationClip CreateOrUpdateCrouchWalkClip(ClipSpec spec)
    {
        AnimationClip sourceClip =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(spec.SourcePath);

        if (sourceClip == null)
        {
            throw new InvalidOperationException("Missing source AnimationClip '" + spec.SourcePath + "'.");
        }

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(spec.TargetPath) == null)
        {
            if (!AssetDatabase.CopyAsset(spec.SourcePath, spec.TargetPath))
            {
                throw new InvalidOperationException("Could not copy '" + spec.SourcePath + "' to '" + spec.TargetPath + "'.");
            }
        }

        AnimationClip targetClip =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(spec.TargetPath);

        if (targetClip == null)
        {
            throw new InvalidOperationException("Target asset is not an AnimationClip: '" + spec.TargetPath + "'.");
        }

        EditorUtility.CopySerialized(sourceClip, targetClip);
        targetClip.name = spec.TargetName;
        targetClip.legacy = false;
        SetLoopTime(targetClip, true);

        EditorUtility.SetDirty(targetClip);
        ValidateClip(targetClip, spec.TargetPath, true);
        return targetClip;
    }

    private static AnimationClip CreateOrUpdateCrouchIdleClip(AnimationClip sourceClip)
    {
        if (sourceClip == null)
        {
            throw new InvalidOperationException("Cannot create crouch idle because the forward crouch walk clip is missing.");
        }

        ValidateClipHasCurves(sourceClip, CrouchWalkClips[0].TargetPath);
        ValidateClipHasHumanoidCurves(sourceClip, CrouchWalkClips[0].TargetPath);

        AnimationClip generatedClip = new AnimationClip
        {
            name = CrouchIdleClip.TargetName,
            frameRate = sourceClip.frameRate,
            legacy = false
        };

        int floatCurveCount = CopyStaticFloatCurves(sourceClip, generatedClip);
        CopyStaticObjectReferenceCurves(sourceClip, generatedClip);

        if (floatCurveCount == 0)
        {
            throw new InvalidOperationException("Could not generate '" + CrouchIdleClip.TargetPath + "' because the source clip has no usable float curves.");
        }

        SetLoopTime(generatedClip, true);

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(CrouchIdleClip.TargetPath) == null)
        {
            AssetDatabase.CreateAsset(generatedClip, CrouchIdleClip.TargetPath);
        }
        else
        {
            AnimationClip existingClip =
                AssetDatabase.LoadAssetAtPath<AnimationClip>(CrouchIdleClip.TargetPath);

            if (existingClip == null)
            {
                throw new InvalidOperationException("Target asset is not an AnimationClip: '" + CrouchIdleClip.TargetPath + "'.");
            }

            EditorUtility.CopySerialized(generatedClip, existingClip);
            existingClip.name = CrouchIdleClip.TargetName;
            SetLoopTime(existingClip, true);
            EditorUtility.SetDirty(existingClip);
        }

        AnimationClip targetClip =
            AssetDatabase.LoadAssetAtPath<AnimationClip>(CrouchIdleClip.TargetPath);

        ValidateClip(targetClip, CrouchIdleClip.TargetPath, true);
        return targetClip;
    }

    private static int CopyStaticFloatCurves(AnimationClip sourceClip, AnimationClip targetClip)
    {
        EditorCurveBinding[] bindings =
            AnimationUtility.GetCurveBindings(sourceClip);

        int copiedCount = 0;
        for (int i = 0; i < bindings.Length; i++)
        {
            AnimationCurve sourceCurve =
                AnimationUtility.GetEditorCurve(sourceClip, bindings[i]);

            if (sourceCurve == null || sourceCurve.length == 0)
            {
                continue;
            }

            float value = sourceCurve.Evaluate(0f);
            AnimationCurve targetCurve =
                AnimationCurve.Constant(0f, CrouchIdleLength, value);

            AnimationUtility.SetEditorCurve(targetClip, bindings[i], targetCurve);
            copiedCount++;
        }

        return copiedCount;
    }

    private static void CopyStaticObjectReferenceCurves(AnimationClip sourceClip, AnimationClip targetClip)
    {
        EditorCurveBinding[] bindings =
            AnimationUtility.GetObjectReferenceCurveBindings(sourceClip);

        for (int i = 0; i < bindings.Length; i++)
        {
            ObjectReferenceKeyframe[] sourceKeyframes =
                AnimationUtility.GetObjectReferenceCurve(sourceClip, bindings[i]);

            UnityEngine.Object value = null;
            for (int keyIndex = 0; keyIndex < sourceKeyframes.Length; keyIndex++)
            {
                if (sourceKeyframes[keyIndex].time >= 0f)
                {
                    value = sourceKeyframes[keyIndex].value;
                    break;
                }
            }

            if (value == null)
            {
                continue;
            }

            ObjectReferenceKeyframe[] targetKeyframes =
            {
                new ObjectReferenceKeyframe
                {
                    time = 0f,
                    value = value
                },
                new ObjectReferenceKeyframe
                {
                    time = CrouchIdleLength,
                    value = value
                }
            };

            AnimationUtility.SetObjectReferenceCurve(targetClip, bindings[i], targetKeyframes);
        }
    }

    private static AnimatorController CreateOrUpdateAnimatorController(
        AnimationClip idleClip,
        AnimationClip forwardClip,
        AnimationClip backwardClip,
        AnimationClip leftClip,
        AnimationClip rightClip
    )
    {
        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TargetControllerPath) == null)
        {
            if (!AssetDatabase.CopyAsset(SourceControllerPath, TargetControllerPath))
            {
                throw new InvalidOperationException("Could not copy source controller to '" + TargetControllerPath + "'.");
            }
        }

        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(TargetControllerPath);

        if (controller == null)
        {
            throw new InvalidOperationException("Target asset is not an AnimatorController: '" + TargetControllerPath + "'.");
        }

        EnsureParameter(controller, HorizontalParameter, AnimatorControllerParameterType.Float);
        EnsureParameter(controller, VerticalParameter, AnimatorControllerParameterType.Float);
        EnsureParameter(controller, StateParameter, AnimatorControllerParameterType.Float);
        EnsureParameter(controller, JumpParameter, AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, CrouchParameter, AnimatorControllerParameterType.Bool);

        PlayerMovementLayer movementLayer =
            FindPlayerMovementLayer(controller);

        AnimatorState crouchState =
            EnsureCrouchState(controller, movementLayer);

        ConfigureCrouchBlendTree(
            controller,
            crouchState,
            idleClip,
            forwardClip,
            backwardClip,
            leftClip,
            rightClip
        );

        EnsureTransition(
            movementLayer.MovementState,
            crouchState,
            new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.If,
                    parameter = CrouchParameter,
                    threshold = 0f
                }
            });

        EnsureTransition(
            crouchState,
            movementLayer.MovementState,
            new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.IfNot,
                    parameter = CrouchParameter,
                    threshold = 0f
                }
            });

        EnsureTransition(
            movementLayer.JumpState,
            crouchState,
            new[]
            {
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.IfNot,
                    parameter = JumpParameter,
                    threshold = 0f
                },
                new AnimatorCondition
                {
                    mode = AnimatorConditionMode.If,
                    parameter = CrouchParameter,
                    threshold = 0f
                }
            });

        EditorUtility.SetDirty(controller);
        ValidateController(controller);
        return controller;
    }

    private static void EnsureParameter(AnimatorController controller, string parameterName, AnimatorControllerParameterType parameterType)
    {
        AnimatorControllerParameter[] parameters =
            controller.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (!string.Equals(parameters[i].name, parameterName, StringComparison.Ordinal))
            {
                continue;
            }

            if (parameters[i].type != parameterType)
            {
                throw new InvalidOperationException("Animator parameter '" + parameterName + "' has type " + parameters[i].type + " but expected " + parameterType + ".");
            }

            return;
        }

        controller.AddParameter(parameterName, parameterType);
    }

    private static PlayerMovementLayer FindPlayerMovementLayer(AnimatorController controller)
    {
        PlayerMovementLayer result = null;
        AnimatorControllerLayer[] layers = controller.layers;

        for (int i = 0; i < layers.Length; i++)
        {
            AnimatorState movementState =
                FindState(layers[i].stateMachine, MovementStateName);

            AnimatorState jumpState =
                FindState(layers[i].stateMachine, JumpStateName);

            if (movementState == null || jumpState == null)
            {
                continue;
            }

            if (result != null)
            {
                throw new InvalidOperationException("More than one Animator layer contains Movement and Jump states.");
            }

            result = new PlayerMovementLayer(layers[i], movementState, jumpState);
        }

        if (result == null)
        {
            throw new InvalidOperationException("No Animator layer contains both Movement and Jump states.");
        }

        return result;
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
    {
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (string.Equals(states[i].state.name, stateName, StringComparison.Ordinal))
            {
                return states[i].state;
            }
        }

        ChildAnimatorStateMachine[] childStateMachines =
            stateMachine.stateMachines;

        for (int i = 0; i < childStateMachines.Length; i++)
        {
            AnimatorState state =
                FindState(childStateMachines[i].stateMachine, stateName);

            if (state != null)
            {
                return state;
            }
        }

        return null;
    }

    private static AnimatorState EnsureCrouchState(AnimatorController controller, PlayerMovementLayer movementLayer)
    {
        AnimatorStateMachine stateMachine =
            movementLayer.Layer.stateMachine;

        AnimatorState crouchState =
            FindState(stateMachine, CrouchStateName);

        if (crouchState == null)
        {
            crouchState =
                stateMachine.AddState(CrouchStateName, new Vector3(520f, 60f, 0f));
        }

        crouchState.writeDefaultValues =
            movementLayer.MovementState.writeDefaultValues;

        EditorUtility.SetDirty(crouchState);
        EditorUtility.SetDirty(stateMachine);
        EditorUtility.SetDirty(controller);
        return crouchState;
    }

    private static void ConfigureCrouchBlendTree(
        AnimatorController controller,
        AnimatorState crouchState,
        AnimationClip idleClip,
        AnimationClip forwardClip,
        AnimationClip backwardClip,
        AnimationClip leftClip,
        AnimationClip rightClip
    )
    {
        BlendTree oldTree = crouchState.motion as BlendTree;
        if (oldTree != null)
        {
            crouchState.motion = null;
            UnityEngine.Object.DestroyImmediate(oldTree, true);
        }

        BlendTree blendTree = new BlendTree
        {
            name = CrouchBlendTreeName,
            blendType = BlendTreeType.FreeformDirectional2D,
            blendParameter = HorizontalParameter,
            blendParameterY = VerticalParameter,
            useAutomaticThresholds = false
        };

        AssetDatabase.AddObjectToAsset(blendTree, controller);
        crouchState.motion = blendTree;

        blendTree.AddChild(idleClip, Vector2.zero);
        blendTree.AddChild(forwardClip, new Vector2(0f, 1f));
        blendTree.AddChild(backwardClip, new Vector2(0f, -1f));
        blendTree.AddChild(leftClip, new Vector2(-1f, 0f));
        blendTree.AddChild(rightClip, new Vector2(1f, 0f));

        EditorUtility.SetDirty(blendTree);
        EditorUtility.SetDirty(crouchState);
    }

    private static void EnsureTransition(AnimatorState sourceState, AnimatorState destinationState, AnimatorCondition[] conditions)
    {
        AnimatorStateTransition[] transitions =
            sourceState.transitions;

        for (int i = transitions.Length - 1; i >= 0; i--)
        {
            if (transitions[i].destinationState == destinationState)
            {
                sourceState.RemoveTransition(transitions[i]);
            }
        }

        AnimatorStateTransition transition =
            sourceState.AddTransition(destinationState);

        transition.hasExitTime = false;
        transition.duration = TransitionDuration;
        transition.hasFixedDuration = true;

        for (int i = 0; i < conditions.Length; i++)
        {
            transition.AddCondition(
                conditions[i].mode,
                conditions[i].threshold,
                conditions[i].parameter
            );
        }

        EditorUtility.SetDirty(sourceState);
        EditorUtility.SetDirty(transition);
    }

    private static void ValidateController(AnimatorController controller)
    {
        EnsureParameterExists(controller, HorizontalParameter);
        EnsureParameterExists(controller, VerticalParameter);
        EnsureParameterExists(controller, StateParameter);
        EnsureParameterExists(controller, JumpParameter);
        EnsureParameterExists(controller, CrouchParameter);

        PlayerMovementLayer layer =
            FindPlayerMovementLayer(controller);

        AnimatorState crouchState =
            FindState(layer.Layer.stateMachine, CrouchStateName);

        if (crouchState == null)
        {
            throw new InvalidOperationException("Controller validation failed: missing Crouch state.");
        }

        BlendTree blendTree = crouchState.motion as BlendTree;
        if (blendTree == null || blendTree.children.Length != 5)
        {
            throw new InvalidOperationException("Controller validation failed: Crouch state has no complete BlendTree.");
        }
    }

    private static void EnsureParameterExists(AnimatorController controller, string parameterName)
    {
        AnimatorControllerParameter[] parameters =
            controller.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            if (string.Equals(parameters[i].name, parameterName, StringComparison.Ordinal))
            {
                return;
            }
        }

        throw new InvalidOperationException("Controller validation failed: missing parameter '" + parameterName + "'.");
    }

    private static void UpdateGameScene(AnimatorController controller)
    {
        EnsureNoUnsavedScenesWillBeLost();

        Scene scene =
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

        PlayerAppearanceController[] appearanceControllers =
            UnityEngine.Object.FindObjectsByType<PlayerAppearanceController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (appearanceControllers.Length != 1)
        {
            throw new InvalidOperationException("Expected exactly one PlayerAppearanceController in " + GameScenePath + " but found " + appearanceControllers.Length + ".");
        }

        GameObject playerObject =
            appearanceControllers[0].gameObject;

        PlayerMovement[] movementComponents =
            playerObject.GetComponents<PlayerMovement>();

        PlayerLook[] lookComponents =
            playerObject.GetComponents<PlayerLook>();

        if (movementComponents.Length != 1)
        {
            throw new InvalidOperationException("Expected exactly one PlayerMovement on '" + playerObject.name + "' but found " + movementComponents.Length + ".");
        }

        if (lookComponents.Length != 1)
        {
            throw new InvalidOperationException("Expected exactly one PlayerLook on '" + playerObject.name + "' but found " + lookComponents.Length + ".");
        }

        SetObjectReference(appearanceControllers[0], "runtimeAnimatorController", controller);

        SerializedObject movementObject =
            new SerializedObject(movementComponents[0]);

        SetLayerMask(movementObject, "standUpBlockerMask", ~0);
        SetFloat(movementObject, "standUpPadding", 0.02f);
        SetFloat(movementObject, "standUpRetryInterval", 0.1f);
        SetString(movementObject, "crouchParameter", CrouchParameter);
        movementObject.ApplyModifiedProperties();

        SerializedObject lookObject =
            new SerializedObject(lookComponents[0]);

        SetFloat(lookObject, "standingCameraHeight", 1.6f);
        SetFloat(lookObject, "crouchingCameraHeight", 1f);
        SetFloat(lookObject, "cameraHeightTransitionSpeed", 10f);
        lookObject.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            throw new InvalidOperationException("Could not save scene '" + GameScenePath + "'.");
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
                throw new InvalidOperationException("Cannot open " + GameScenePath + " because scene '" + scene.path + "' has unsaved changes.");
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new InvalidOperationException("GameScene update cancelled because current scene changes were not saved.");
            }

            break;
        }
    }

    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serializedObject =
            new SerializedObject(target);

        SerializedProperty property =
            RequireProperty(serializedObject, propertyName);

        property.objectReferenceValue = value;
        serializedObject.ApplyModifiedProperties();
    }

    private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
    {
        SerializedProperty property =
            RequireProperty(serializedObject, propertyName);

        property.floatValue = value;
    }

    private static void SetString(SerializedObject serializedObject, string propertyName, string value)
    {
        SerializedProperty property =
            RequireProperty(serializedObject, propertyName);

        property.stringValue = value;
    }

    private static void SetLayerMask(SerializedObject serializedObject, string propertyName, int value)
    {
        SerializedProperty property =
            RequireProperty(serializedObject, propertyName);

        property.intValue = value;
    }

    private static SerializedProperty RequireProperty(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property =
            serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            throw new InvalidOperationException("Missing serialized field '" + propertyName + "' on " + serializedObject.targetObject.name + ".");
        }

        return property;
    }

    private static void ValidateClip(AnimationClip clip, string path, bool mustLoop)
    {
        if (clip == null)
        {
            throw new InvalidOperationException("Missing AnimationClip '" + path + "'.");
        }

        ValidateClipHasCurves(clip, path);
        ValidateClipHasHumanoidCurves(clip, path);

        AnimationClipSettings settings =
            AnimationUtility.GetAnimationClipSettings(clip);

        if (mustLoop && !settings.loopTime)
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
        EditorCurveBinding[] bindings =
            AnimationUtility.GetCurveBindings(clip);

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

    private static void SetLoopTime(AnimationClip clip, bool loopTime)
    {
        AnimationClipSettings settings =
            AnimationUtility.GetAnimationClipSettings(clip);

        settings.loopTime = loopTime;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }
}
