using System;
using UnityEngine;

public sealed class CharacterAppearanceApplier : MonoBehaviour
{
    [SerializeField] private CharacterAppearanceDatabase database;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private string managedRootName = "AppliedAppearance";

    private GameObject currentAppearanceRoot;

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
        if (!database.ResolveAppearance(appearanceData, resolvedAppearance, this))
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
            InstantiateBodyDefinition(bodyDefinition, newAppearanceRoot.transform);
            InstantiatePartDefinition(CharacterAppearanceCategory.Hair, resolvedAppearance.hairId, resolvedAppearance, newAppearanceRoot.transform);
            InstantiatePartDefinition(CharacterAppearanceCategory.Face, resolvedAppearance.faceId, resolvedAppearance, newAppearanceRoot.transform);
            InstantiatePartDefinition(CharacterAppearanceCategory.Upper, resolvedAppearance.upperId, resolvedAppearance, newAppearanceRoot.transform);
            InstantiatePartDefinition(CharacterAppearanceCategory.Pants, resolvedAppearance.pantsId, resolvedAppearance, newAppearanceRoot.transform);
            InstantiatePartDefinition(CharacterAppearanceCategory.Shoes, resolvedAppearance.shoesId, resolvedAppearance, newAppearanceRoot.transform);
        }
        catch (Exception exception)
        {
            Debug.LogError("[CharacterAppearanceApplier] Failed to apply appearance: " + exception.Message, this);
            DestroyGameObject(newAppearanceRoot);
            return false;
        }

        ClearAppearance();
        currentAppearanceRoot = newAppearanceRoot;
        return true;
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

    private void InstantiateBodyDefinition(CharacterAppearanceDatabase.BodyDefinition definition, Transform parent)
    {
        if (definition == null || definition.Prefab == null)
        {
            throw new InvalidOperationException("Body definition has no prefab.");
        }

        GameObject instance = Instantiate(definition.Prefab, parent);
        instance.name = definition.Id;
        ResetLocalTransform(instance.transform);
        RemoveDisallowedComponents(instance);
    }

    private void InstantiatePartDefinition(CharacterAppearanceCategory category, string id, CharacterAppearanceData appearanceData, Transform parent)
    {
        CharacterAppearanceDatabase.AppearanceDefinition definition;
        if (!database.TryGetDefinition(category, appearanceData.bodyTypeId, appearanceData.skinId, id, out definition))
        {
            throw new InvalidOperationException("Could not resolve definition '" + id + "' for BodyType '" + appearanceData.bodyTypeId + "' category " + category + ".");
        }

        GameObject instance = Instantiate(definition.Prefab, parent);
        instance.name = definition.Id;
        ResetLocalTransform(instance.transform);
        RemoveDisallowedComponents(instance);
    }

    private static void ResetLocalTransform(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;
    }

    private void RemoveDisallowedComponents(GameObject instance)
    {
        Component[] components = instance.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component is Transform)
            {
                continue;
            }

            if (IsDisallowedComponent(component))
            {
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
