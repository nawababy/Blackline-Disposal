using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class CharacterAppearanceApplier : MonoBehaviour
{
    [SerializeField] private CharacterAppearanceDatabase database;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private string managedRootName = "AppliedAppearance";

    private GameObject currentAppearanceRoot;

    private enum ApplyMode
    {
        Preview,
        Runtime
    }

    private sealed class BoneLookup
    {
        private readonly Dictionary<string, Transform> bonesByPath =
            new Dictionary<string, Transform>(StringComparer.Ordinal);

        private readonly Dictionary<string, Transform> bonesByName =
            new Dictionary<string, Transform>(StringComparer.Ordinal);

        public BoneLookup(Transform root)
        {
            Root = root;
            AddBone(root, string.Empty);
        }

        public Transform Root { get; private set; }

        public bool TryGetBone(Transform sourceRoot, Transform sourceBone, out Transform targetBone)
        {
            targetBone = null;

            if (sourceBone == null)
            {
                return false;
            }

            if (sourceRoot != null && sourceBone == sourceRoot)
            {
                targetBone = Root;
                return targetBone != null;
            }

            string relativePath = GetRelativePath(sourceRoot, sourceBone);
            if (!string.IsNullOrEmpty(relativePath) && bonesByPath.TryGetValue(relativePath, out targetBone))
            {
                return true;
            }

            return bonesByName.TryGetValue(sourceBone.name, out targetBone);
        }

        private void AddBone(Transform bone, string path)
        {
            if (bone == null)
            {
                return;
            }

            if (!bonesByPath.ContainsKey(path))
            {
                bonesByPath.Add(path, bone);
            }

            if (!bonesByName.ContainsKey(bone.name))
            {
                bonesByName.Add(bone.name, bone);
            }

            for (int i = 0; i < bone.childCount; i++)
            {
                Transform child = bone.GetChild(i);
                string childPath = string.IsNullOrEmpty(path) ? child.name : path + "/" + child.name;
                AddBone(child, childPath);
            }
        }
    }

    public void SetDatabase(CharacterAppearanceDatabase newDatabase)
    {
        database = newDatabase;
    }

    public void SetVisualRoot(Transform newVisualRoot)
    {
        visualRoot = newVisualRoot;
    }

    public bool ApplyAppearance(CharacterAppearanceData appearanceData)
    {
        Animator ignoredAnimator;
        return ApplyAppearanceInternal(
            appearanceData,
            ApplyMode.Preview,
            null,
            out ignoredAnimator
        );
    }

    public bool ApplyRuntimeAppearance(
        CharacterAppearanceData appearanceData,
        RuntimeAnimatorController runtimeAnimatorController,
        out Animator runtimeAnimator
    )
    {
        return ApplyAppearanceInternal(
            appearanceData,
            ApplyMode.Runtime,
            runtimeAnimatorController,
            out runtimeAnimator
        );
    }

    public void ClearAppearance()
    {
        if (currentAppearanceRoot == null)
        {
            return;
        }

        DestroyGameObject(currentAppearanceRoot);
        currentAppearanceRoot = null;
    }

    private bool ApplyAppearanceInternal(
        CharacterAppearanceData appearanceData,
        ApplyMode mode,
        RuntimeAnimatorController runtimeAnimatorController,
        out Animator runtimeAnimator
    )
    {
        runtimeAnimator = null;

        if (database == null)
        {
            Debug.LogError("[CharacterAppearanceApplier] Cannot apply appearance because no CharacterAppearanceDatabase is assigned.", this);
            return false;
        }

        if (visualRoot == null)
        {
            Debug.LogError("[CharacterAppearanceApplier] Cannot apply appearance because no visualRoot is assigned.", this);
            return false;
        }

        database.ValidateDatabase(database);

        CharacterAppearanceData resolvedAppearance = new CharacterAppearanceData();
        bool resolved = mode == ApplyMode.Runtime
            ? database.ResolveRuntimeAppearance(appearanceData, resolvedAppearance, this)
            : database.ResolveAppearance(appearanceData, resolvedAppearance, this);

        if (!resolved)
        {
            return false;
        }

        CharacterAppearanceDatabase.BodyDefinition bodyDefinition;
        if (!database.TryGetBodyDefinition(resolvedAppearance.bodyTypeId, resolvedAppearance.skinId, out bodyDefinition))
        {
            Debug.LogError("[CharacterAppearanceApplier] Cannot resolve body prefab for BodyType '" + resolvedAppearance.bodyTypeId + "' and Skin '" + resolvedAppearance.skinId + "'.", this);
            return false;
        }

        GameObject newAppearanceRoot = new GameObject(string.IsNullOrWhiteSpace(managedRootName) ? "AppliedAppearance" : managedRootName.Trim());
        newAppearanceRoot.transform.SetParent(visualRoot, false);
        ResetLocalTransform(newAppearanceRoot.transform);

        try
        {
            if (mode == ApplyMode.Runtime)
            {
                if (!ApplyRuntimeParts(bodyDefinition, resolvedAppearance, runtimeAnimatorController, newAppearanceRoot.transform, out runtimeAnimator))
                {
                    DestroyGameObject(newAppearanceRoot);
                    return false;
                }
            }
            else
            {
                ApplyPreviewParts(bodyDefinition, resolvedAppearance, newAppearanceRoot.transform);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError("[CharacterAppearanceApplier] Failed to apply appearance: " + exception.Message, this);
            DestroyGameObject(newAppearanceRoot);
            runtimeAnimator = null;
            return false;
        }

        ClearAppearance();
        currentAppearanceRoot = newAppearanceRoot;
        return true;
    }

    private void ApplyPreviewParts(
        CharacterAppearanceDatabase.BodyDefinition bodyDefinition,
        CharacterAppearanceData resolvedAppearance,
        Transform parent
    )
    {
        InstantiatePreviewBodyDefinition(bodyDefinition, parent);
        InstantiatePreviewPartDefinition(CharacterAppearanceCategory.Hair, resolvedAppearance.hairId, resolvedAppearance, parent);
        InstantiatePreviewPartDefinition(CharacterAppearanceCategory.Face, resolvedAppearance.faceId, resolvedAppearance, parent);
        InstantiateOptionalPreviewPartDefinition(CharacterAppearanceCategory.Hat, resolvedAppearance.hatId, resolvedAppearance, parent);
        InstantiateOptionalPreviewPartDefinition(CharacterAppearanceCategory.Glasses, resolvedAppearance.glassesId, resolvedAppearance, parent);
        InstantiateOptionalPreviewPartDefinition(CharacterAppearanceCategory.Gloves, resolvedAppearance.glovesId, resolvedAppearance, parent);

        if (!string.IsNullOrEmpty(resolvedAppearance.fullBodyId))
        {
            InstantiateOptionalPreviewPartDefinition(CharacterAppearanceCategory.FullBody, resolvedAppearance.fullBodyId, resolvedAppearance, parent);
        }
        else
        {
            InstantiatePreviewPartDefinition(CharacterAppearanceCategory.Upper, resolvedAppearance.upperId, resolvedAppearance, parent);
            InstantiatePreviewPartDefinition(CharacterAppearanceCategory.Pants, resolvedAppearance.pantsId, resolvedAppearance, parent);
        }

        InstantiatePreviewPartDefinition(CharacterAppearanceCategory.Shoes, resolvedAppearance.shoesId, resolvedAppearance, parent);
    }

    private bool ApplyRuntimeParts(
        CharacterAppearanceDatabase.BodyDefinition bodyDefinition,
        CharacterAppearanceData resolvedAppearance,
        RuntimeAnimatorController runtimeAnimatorController,
        Transform parent,
        out Animator runtimeAnimator
    )
    {
        runtimeAnimator = null;

        GameObject bodyInstance = Instantiate(bodyDefinition.Prefab, parent);
        bodyInstance.name = bodyDefinition.Id;
        ResetLocalTransform(bodyInstance.transform);

        Transform fallbackSkeletonRoot = FindRendererSkeletonRoot(bodyInstance);
        Avatar bodyAvatar = FindExistingRuntimeAvatar(bodyInstance);
        runtimeAnimator = PrepareRuntimeBodyAnimator(bodyInstance, resolvedAppearance.bodyTypeId, bodyAvatar, runtimeAnimatorController);
        if (runtimeAnimator == null)
        {
            return false;
        }

        Transform targetSkeletonRoot = FindSkeletonRoot(bodyInstance, runtimeAnimator, fallbackSkeletonRoot);
        if (targetSkeletonRoot == null)
        {
            Debug.LogError("[CharacterAppearanceApplier] Body prefab '" + bodyDefinition.Prefab.name + "' for BodyType '" + resolvedAppearance.bodyTypeId + "' has no usable skeleton root.", this);
            return false;
        }

        BoneLookup targetBones = new BoneLookup(targetSkeletonRoot);

        InstantiateRuntimePartDefinition(CharacterAppearanceCategory.Hair, resolvedAppearance.hairId, resolvedAppearance, targetBones, parent, false);
        InstantiateRuntimePartDefinition(CharacterAppearanceCategory.Face, resolvedAppearance.faceId, resolvedAppearance, targetBones, parent, false);
        InstantiateRuntimePartDefinition(CharacterAppearanceCategory.Hat, resolvedAppearance.hatId, resolvedAppearance, targetBones, parent, true);
        InstantiateRuntimePartDefinition(CharacterAppearanceCategory.Glasses, resolvedAppearance.glassesId, resolvedAppearance, targetBones, parent, true);
        InstantiateRuntimePartDefinition(CharacterAppearanceCategory.Gloves, resolvedAppearance.glovesId, resolvedAppearance, targetBones, parent, true);

        if (!string.IsNullOrEmpty(resolvedAppearance.fullBodyId))
        {
            InstantiateRuntimePartDefinition(CharacterAppearanceCategory.FullBody, resolvedAppearance.fullBodyId, resolvedAppearance, targetBones, parent, true);
        }
        else
        {
            InstantiateRuntimePartDefinition(CharacterAppearanceCategory.Upper, resolvedAppearance.upperId, resolvedAppearance, targetBones, parent, false);
            InstantiateRuntimePartDefinition(CharacterAppearanceCategory.Pants, resolvedAppearance.pantsId, resolvedAppearance, targetBones, parent, false);
        }

        InstantiateRuntimePartDefinition(CharacterAppearanceCategory.Shoes, resolvedAppearance.shoesId, resolvedAppearance, targetBones, parent, false);
        return runtimeAnimator != null;
    }

    private void InstantiatePreviewBodyDefinition(CharacterAppearanceDatabase.BodyDefinition definition, Transform parent)
    {
        if (definition == null || definition.Prefab == null)
        {
            throw new InvalidOperationException("Body definition has no prefab.");
        }

        GameObject instance = Instantiate(definition.Prefab, parent);
        instance.name = definition.Id;
        ResetLocalTransform(instance.transform);
        RemoveDisallowedComponents(instance, null);
    }

    private void InstantiatePreviewPartDefinition(CharacterAppearanceCategory category, string id, CharacterAppearanceData appearanceData, Transform parent)
    {
        CharacterAppearanceDatabase.AppearanceDefinition definition;
        if (!database.TryGetDefinition(category, appearanceData.bodyTypeId, appearanceData.skinId, id, out definition))
        {
            throw new InvalidOperationException("Could not resolve definition '" + id + "' for BodyType '" + appearanceData.bodyTypeId + "' category " + category + ".");
        }

        GameObject instance = Instantiate(definition.Prefab, parent);
        instance.name = definition.Id;
        ResetLocalTransform(instance.transform);
        RemoveDisallowedComponents(instance, null);
    }

    private void InstantiateOptionalPreviewPartDefinition(CharacterAppearanceCategory category, string id, CharacterAppearanceData appearanceData, Transform parent)
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        InstantiatePreviewPartDefinition(category, id, appearanceData, parent);
    }

    private void InstantiateRuntimePartDefinition(
        CharacterAppearanceCategory category,
        string id,
        CharacterAppearanceData appearanceData,
        BoneLookup targetBones,
        Transform parent,
        bool optional
    )
    {
        if (string.IsNullOrEmpty(id))
        {
            return;
        }

        CharacterAppearanceDatabase.AppearanceDefinition definition;
        if (!database.TryGetDefinition(category, appearanceData.bodyTypeId, appearanceData.skinId, id, out definition))
        {
            Debug.LogWarning("[CharacterAppearanceApplier] Runtime part '" + id + "' for BodyType '" + appearanceData.bodyTypeId + "' category " + category + " could not be resolved.", this);
            return;
        }

        GameObject instance = Instantiate(definition.Prefab, parent);
        instance.name = definition.Id;
        ResetLocalTransform(instance.transform);

        if (!BindSkinnedMeshRenderers(instance, category, definition.Id, appearanceData.bodyTypeId, definition.Prefab, targetBones))
        {
            string severity = optional ? "optional" : "required";
            Debug.LogWarning("[CharacterAppearanceApplier] Skipped " + severity + " runtime part '" + definition.Id + "' for BodyType '" + appearanceData.bodyTypeId + "' category " + category + ".", this);
            DestroyGameObject(instance);
            return;
        }

        RemoveDisallowedComponents(instance, null);
    }

    private Animator PrepareRuntimeBodyAnimator(GameObject bodyInstance, string bodyTypeId, Avatar bodyAvatar, RuntimeAnimatorController runtimeAnimatorController)
    {
        Animator[] animators = bodyInstance.GetComponentsInChildren<Animator>(true);
        Animator runtimeAnimator = SelectRuntimeAnimator(bodyInstance, animators);
        Avatar runtimeAvatar;
        if (!TryResolveRuntimeAvatar(bodyTypeId, bodyAvatar, out runtimeAvatar))
        {
            return null;
        }

        RemoveDisallowedComponents(bodyInstance, runtimeAnimator);

        runtimeAnimator.avatar = runtimeAvatar;

        if (runtimeAnimatorController != null)
        {
            runtimeAnimator.runtimeAnimatorController = runtimeAnimatorController;
        }
        else
        {
            Debug.LogWarning("[CharacterAppearanceApplier] Runtime body has no AnimatorController assigned.", this);
        }

        runtimeAnimator.applyRootMotion = false;
        return runtimeAnimator;
    }

    private Animator SelectRuntimeAnimator(GameObject bodyInstance, Animator[] animators)
    {
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animatorCandidate = animators[i];
            if (animatorCandidate != null && IsUsableHumanoidAvatar(animatorCandidate.avatar))
            {
                return animatorCandidate;
            }
        }

        return animators.Length > 0 ? animators[0] : bodyInstance.AddComponent<Animator>();
    }

    private Avatar FindExistingRuntimeAvatar(GameObject bodyInstance)
    {
        Animator[] animators = bodyInstance.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animatorCandidate = animators[i];
            if (animatorCandidate != null && IsUsableHumanoidAvatar(animatorCandidate.avatar))
            {
                return animatorCandidate.avatar;
            }
        }

        return null;
    }

    private bool TryResolveRuntimeAvatar(string bodyTypeId, Avatar bodyAvatar, out Avatar runtimeAvatar)
    {
        runtimeAvatar = null;

        if (IsUsableHumanoidAvatar(bodyAvatar))
        {
            runtimeAvatar = bodyAvatar;
            return true;
        }

        Avatar databaseAvatar;
        if (database.TryGetRuntimeAvatar(bodyTypeId, out databaseAvatar) && IsUsableHumanoidAvatar(databaseAvatar))
        {
            runtimeAvatar = databaseAvatar;
            return true;
        }

        Debug.LogError("[CharacterAppearanceApplier] Cannot animate runtime BodyType '" + bodyTypeId + "' because no valid Humanoid runtimeAvatar is assigned. Expected source: " + GetExpectedRuntimeAvatarSourcePath(bodyTypeId) + ".", this);
        return false;
    }

    private static bool IsUsableHumanoidAvatar(Avatar avatar)
    {
        return avatar != null && avatar.isValid && avatar.isHuman;
    }

    private Transform FindSkeletonRoot(GameObject bodyInstance, Animator runtimeAnimator, Transform fallbackSkeletonRoot)
    {
        if (runtimeAnimator != null && runtimeAnimator.avatar != null && runtimeAnimator.avatar.isHuman)
        {
            Transform hips = runtimeAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null)
            {
                return hips;
            }
        }

        if (fallbackSkeletonRoot != null)
        {
            return fallbackSkeletonRoot;
        }

        return FindRendererSkeletonRoot(bodyInstance);
    }

    private Transform FindRendererSkeletonRoot(GameObject bodyInstance)
    {
        SkinnedMeshRenderer[] renderers = bodyInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].rootBone != null)
            {
                return renderers[i].rootBone;
            }
        }

        return null;
    }

    private static string GetExpectedRuntimeAvatarSourcePath(string bodyTypeId)
    {
        string normalizedBodyTypeId = CharacterAppearanceData.NormalizeId(bodyTypeId);
        switch (normalizedBodyTypeId)
        {
            case CharacterAppearanceDatabase.AdultFemaleBodyTypeId:
            case CharacterAppearanceDatabase.AdultMaleBodyTypeId:
                return "Assets/ithappy/City_Characters/Meshes/Basic_Character_Adult.fbx";
            case CharacterAppearanceDatabase.PlusSizeFemaleBodyTypeId:
            case CharacterAppearanceDatabase.PlusSizeMaleBodyTypeId:
                return "Assets/ithappy/City_Characters/Meshes/Basic_Characters_Plus-size.fbx";
            case CharacterAppearanceDatabase.ChildFemaleBodyTypeId:
            case CharacterAppearanceDatabase.ChildMaleBodyTypeId:
                return "Assets/ithappy/City_Characters/Meshes/Basic_Characters_Child.fbx";
            case CharacterAppearanceDatabase.TeenFemaleBodyTypeId:
            case CharacterAppearanceDatabase.TeenMaleBodyTypeId:
                return "Assets/ithappy/City_Characters/Meshes/Basic_Characters_Teen.fbx";
            case CharacterAppearanceDatabase.SeniorFemaleBodyTypeId:
            case CharacterAppearanceDatabase.SeniorMaleBodyTypeId:
                return "Assets/ithappy/City_Characters/Meshes/Basic_Characters_Senior.fbx";
            case CharacterAppearanceDatabase.PumpedMaleBodyTypeId:
                return "Assets/ithappy/City_Characters/Meshes/Basic_Characters_Pumped.fbx";
            default:
                return "CharacterAppearanceDatabase BodyType runtimeAvatar";
        }
    }

    private bool BindSkinnedMeshRenderers(
        GameObject instance,
        CharacterAppearanceCategory category,
        string id,
        string bodyTypeId,
        GameObject sourcePrefab,
        BoneLookup targetBones
    )
    {
        SkinnedMeshRenderer[] renderers = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length == 0)
        {
            Debug.LogWarning("[CharacterAppearanceApplier] Runtime part '" + id + "' for BodyType '" + bodyTypeId + "' category " + category + " has no SkinnedMeshRenderer. Prefab: " + GetPrefabName(sourcePrefab), this);
            return false;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (!TryBindRenderer(renderers[i], category, id, bodyTypeId, sourcePrefab, targetBones))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryBindRenderer(
        SkinnedMeshRenderer renderer,
        CharacterAppearanceCategory category,
        string id,
        string bodyTypeId,
        GameObject sourcePrefab,
        BoneLookup targetBones
    )
    {
        if (renderer == null)
        {
            return false;
        }

        Transform sourceRoot = renderer.rootBone;
        if (sourceRoot == null)
        {
            Debug.LogWarning("[CharacterAppearanceApplier] Renderer '" + renderer.name + "' in '" + id + "' has no rootBone. BodyType: " + bodyTypeId + ", category: " + category + ", prefab: " + GetPrefabName(sourcePrefab), this);
            return false;
        }

        Transform[] sourceBones = renderer.bones;
        if (sourceBones == null || sourceBones.Length == 0)
        {
            Debug.LogWarning("[CharacterAppearanceApplier] Renderer '" + renderer.name + "' in '" + id + "' has no bones. BodyType: " + bodyTypeId + ", category: " + category + ", prefab: " + GetPrefabName(sourcePrefab), this);
            return false;
        }

        Transform[] reboundBones = new Transform[sourceBones.Length];
        for (int i = 0; i < sourceBones.Length; i++)
        {
            Transform sourceBone = sourceBones[i];
            Transform targetBone;
            if (!targetBones.TryGetBone(sourceRoot, sourceBone, out targetBone))
            {
                string boneName = sourceBone == null ? "<null>" : sourceBone.name;
                Debug.LogWarning("[CharacterAppearanceApplier] Missing target bone '" + boneName + "' while binding '" + id + "'. BodyType: " + bodyTypeId + ", category: " + category + ", prefab: " + GetPrefabName(sourcePrefab), this);
                return false;
            }

            reboundBones[i] = targetBone;
        }

        Transform targetRootBone;
        if (!targetBones.TryGetBone(sourceRoot, sourceRoot, out targetRootBone))
        {
            targetRootBone = targetBones.Root;
        }

        renderer.rootBone = targetRootBone;
        renderer.bones = reboundBones;
        return true;
    }

    private static string GetRelativePath(Transform root, Transform bone)
    {
        if (root == null || bone == null || bone == root || !bone.IsChildOf(root))
        {
            return string.Empty;
        }

        List<string> pathParts = new List<string>();
        Transform current = bone;
        while (current != null && current != root)
        {
            pathParts.Add(current.name);
            current = current.parent;
        }

        pathParts.Reverse();
        return string.Join("/", pathParts.ToArray());
    }

    private static string GetPrefabName(GameObject prefab)
    {
        return prefab == null ? "<missing>" : prefab.name;
    }

    private static void ResetLocalTransform(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private void RemoveDisallowedComponents(GameObject instance, Animator protectedAnimator)
    {
        Component[] components = instance.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component is Transform)
            {
                continue;
            }

            if (component == protectedAnimator)
            {
                continue;
            }

            if (IsDisallowedComponent(component))
            {
                Behaviour behaviour = component as Behaviour;
                if (behaviour != null)
                {
                    behaviour.enabled = false;
                }

                DestroyComponent(component);
            }
        }
    }

    private static bool IsDisallowedComponent(Component component)
    {
        Type componentType = component.GetType();
        string typeName = componentType.Name;
        string namespaceName = componentType.Namespace;

        return component is Animator
            || component is Camera
            || component is AudioListener
            || component is CharacterController
            || component is Rigidbody
            || component is Collider
            || string.Equals(typeName, "PlayerInput", StringComparison.Ordinal)
            || (!string.IsNullOrEmpty(namespaceName) && namespaceName.StartsWith("UnityEngine.InputSystem", StringComparison.Ordinal));
    }

    private static void DestroyComponent(Component component)
    {
        if (Application.isPlaying)
        {
            Destroy(component);
        }
        else
        {
            DestroyImmediate(component);
        }
    }

    private static void DestroyGameObject(GameObject target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }
}
