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
        if (!ResolveAppearance(appearanceData, resolvedAppearance))
        {
            return false;
        }

        GameObject newAppearanceRoot = new GameObject(string.IsNullOrWhiteSpace(managedRootName) ? "AppliedAppearance" : managedRootName.Trim());
        newAppearanceRoot.transform.SetParent(visualRoot, false);
        newAppearanceRoot.transform.localPosition = Vector3.zero;
        newAppearanceRoot.transform.localRotation = Quaternion.identity;
        newAppearanceRoot.transform.localScale = Vector3.one;

        try
        {
            InstantiateDefinition(CharacterAppearanceCategory.Body, resolvedAppearance.bodyId, newAppearanceRoot.transform);
            InstantiateDefinition(CharacterAppearanceCategory.Hair, resolvedAppearance.hairId, newAppearanceRoot.transform);
            InstantiateDefinition(CharacterAppearanceCategory.Face, resolvedAppearance.faceId, newAppearanceRoot.transform);
            InstantiateDefinition(CharacterAppearanceCategory.Upper, resolvedAppearance.upperId, newAppearanceRoot.transform);
            InstantiateDefinition(CharacterAppearanceCategory.Pants, resolvedAppearance.pantsId, newAppearanceRoot.transform);
            InstantiateDefinition(CharacterAppearanceCategory.Shoes, resolvedAppearance.shoesId, newAppearanceRoot.transform);
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

    private bool ResolveAppearance(CharacterAppearanceData source, CharacterAppearanceData resolvedAppearance)
    {
        if (source == null)
        {
            Debug.LogWarning("[CharacterAppearanceApplier] Appearance data is null. Falling back to database defaults.", this);
        }

        resolvedAppearance.appearanceVersion = CharacterAppearanceData.CurrentVersion;

        bool resolvedAllCategories = true;
        resolvedAllCategories &= ResolveCategory(source, CharacterAppearanceCategory.Body, resolvedAppearance);
        resolvedAllCategories &= ResolveCategory(source, CharacterAppearanceCategory.Hair, resolvedAppearance);
        resolvedAllCategories &= ResolveCategory(source, CharacterAppearanceCategory.Face, resolvedAppearance);
        resolvedAllCategories &= ResolveCategory(source, CharacterAppearanceCategory.Upper, resolvedAppearance);
        resolvedAllCategories &= ResolveCategory(source, CharacterAppearanceCategory.Pants, resolvedAppearance);
        resolvedAllCategories &= ResolveCategory(source, CharacterAppearanceCategory.Shoes, resolvedAppearance);

        return resolvedAllCategories;
    }

    private bool ResolveCategory(CharacterAppearanceData source, CharacterAppearanceCategory category, CharacterAppearanceData resolvedAppearance)
    {
        string requestedId = source == null ? string.Empty : source.GetId(category);
        CharacterAppearanceDatabase.AppearanceDefinition definition;

        if (database.TryGetDefinition(category, requestedId, out definition))
        {
            resolvedAppearance.SetId(category, definition.Id);
            return true;
        }

        if (!string.IsNullOrEmpty(CharacterAppearanceData.NormalizeId(requestedId)))
        {
            Debug.LogWarning("[CharacterAppearanceApplier] Appearance id '" + requestedId + "' is invalid for category " + category + ". Falling back to the configured default.", this);
        }

        if (database.TryGetDefaultDefinition(category, out definition))
        {
            resolvedAppearance.SetId(category, definition.Id);
            return true;
        }

        Debug.LogError("[CharacterAppearanceApplier] Cannot resolve a valid default for category " + category + ".", this);
        return false;
    }

    private void InstantiateDefinition(CharacterAppearanceCategory category, string id, Transform parent)
    {
        CharacterAppearanceDatabase.AppearanceDefinition definition;
        if (!database.TryGetDefinition(category, id, out definition))
        {
            throw new InvalidOperationException("Could not resolve definition '" + id + "' for category " + category + ".");
        }

        GameObject instance = Instantiate(definition.Prefab, parent);
        instance.name = definition.Id;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        RemoveDisallowedComponents(instance);
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
