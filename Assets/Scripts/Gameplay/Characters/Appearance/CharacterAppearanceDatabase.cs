using System;
using System.Collections.Generic;
using UnityEngine;

public enum CharacterAppearanceCategory
{
    Body,
    Hair,
    Face,
    Upper,
    Pants,
    Shoes
}

[CreateAssetMenu(fileName = "CharacterAppearanceDatabase", menuName = "Game/Characters/Character Appearance Database")]
public sealed class CharacterAppearanceDatabase : ScriptableObject
{
    public const string AdultFemaleCompatibilityKey = "adult_female";

    [Serializable]
    public sealed class AppearanceDefinition
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private CharacterAppearanceCategory category;
        [SerializeField] private string compatibilityKey = AdultFemaleCompatibilityKey;
        [SerializeField] private GameObject prefab;

        public string Id
        {
            get { return CharacterAppearanceData.NormalizeId(id); }
        }

        public string DisplayName
        {
            get { return string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim(); }
        }

        public CharacterAppearanceCategory Category
        {
            get { return category; }
        }

        public string CompatibilityKey
        {
            get { return CharacterAppearanceData.NormalizeId(compatibilityKey); }
        }

        public GameObject Prefab
        {
            get { return prefab; }
        }
    }

    [Header("Defaults")]
    [SerializeField] private string defaultBodyId = string.Empty;
    [SerializeField] private string defaultHairId = string.Empty;
    [SerializeField] private string defaultFaceId = string.Empty;
    [SerializeField] private string defaultUpperId = string.Empty;
    [SerializeField] private string defaultPantsId = string.Empty;
    [SerializeField] private string defaultShoesId = string.Empty;

    [Header("Adult Female Definitions")]
    [SerializeField] private List<AppearanceDefinition> bodyDefinitions = new List<AppearanceDefinition>();
    [SerializeField] private List<AppearanceDefinition> hairDefinitions = new List<AppearanceDefinition>();
    [SerializeField] private List<AppearanceDefinition> faceDefinitions = new List<AppearanceDefinition>();
    [SerializeField] private List<AppearanceDefinition> upperDefinitions = new List<AppearanceDefinition>();
    [SerializeField] private List<AppearanceDefinition> pantsDefinitions = new List<AppearanceDefinition>();
    [SerializeField] private List<AppearanceDefinition> shoesDefinitions = new List<AppearanceDefinition>();

    public string GetDefaultId(CharacterAppearanceCategory category)
    {
        switch (category)
        {
            case CharacterAppearanceCategory.Body:
                return CharacterAppearanceData.NormalizeId(defaultBodyId);
            case CharacterAppearanceCategory.Hair:
                return CharacterAppearanceData.NormalizeId(defaultHairId);
            case CharacterAppearanceCategory.Face:
                return CharacterAppearanceData.NormalizeId(defaultFaceId);
            case CharacterAppearanceCategory.Upper:
                return CharacterAppearanceData.NormalizeId(defaultUpperId);
            case CharacterAppearanceCategory.Pants:
                return CharacterAppearanceData.NormalizeId(defaultPantsId);
            case CharacterAppearanceCategory.Shoes:
                return CharacterAppearanceData.NormalizeId(defaultShoesId);
            default:
                return string.Empty;
        }
    }

    public int GetDefinitionCount(CharacterAppearanceCategory category)
    {
        return GetDefinitions(category).Count;
    }

    public bool TryGetDefinitionAtIndex(CharacterAppearanceCategory category, int index, out AppearanceDefinition definition)
    {
        definition = null;
        List<AppearanceDefinition> definitions = GetDefinitions(category);
        if (index < 0 || index >= definitions.Count)
        {
            return false;
        }

        AppearanceDefinition candidate = definitions[index];
        if (!IsDefinitionUsable(candidate, category))
        {
            return false;
        }

        definition = candidate;
        return true;
    }

    public int GetCompatibleDefinitionCount(CharacterAppearanceCategory category)
    {
        List<AppearanceDefinition> definitions = GetDefinitions(category);
        int count = 0;

        for (int i = 0; i < definitions.Count; i++)
        {
            if (IsDefinitionUsable(definitions[i], category))
            {
                count++;
            }
        }

        return count;
    }

    public void GetCompatibleDefinitions(CharacterAppearanceCategory category, List<AppearanceDefinition> results)
    {
        if (results == null)
        {
            Debug.LogError("[CharacterAppearanceDatabase] Cannot fill compatible definitions because the result list is null.", this);
            return;
        }

        results.Clear();

        List<AppearanceDefinition> definitions = GetDefinitions(category);
        for (int i = 0; i < definitions.Count; i++)
        {
            AppearanceDefinition definition = definitions[i];
            if (IsDefinitionUsable(definition, category))
            {
                results.Add(definition);
            }
        }
    }

    public bool TryGetDefinition(CharacterAppearanceCategory category, string id, out AppearanceDefinition definition)
    {
        definition = null;
        string normalizedId = CharacterAppearanceData.NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
        {
            return false;
        }

        List<AppearanceDefinition> definitions = GetDefinitions(category);
        for (int i = 0; i < definitions.Count; i++)
        {
            AppearanceDefinition candidate = definitions[i];
            if (IsDefinitionUsable(candidate, category) && string.Equals(candidate.Id, normalizedId, StringComparison.Ordinal))
            {
                definition = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetDefaultDefinition(CharacterAppearanceCategory category, out AppearanceDefinition definition)
    {
        return TryGetDefinition(category, GetDefaultId(category), out definition);
    }

    public bool TryGetNextDefinition(CharacterAppearanceCategory category, string currentId, int direction, out AppearanceDefinition definition)
    {
        definition = null;

        List<AppearanceDefinition> definitions = GetDefinitions(category);
        int definitionCount = definitions.Count;
        if (definitionCount == 0)
        {
            return false;
        }

        int step = direction < 0 ? -1 : 1;
        int currentIndex = FindDefinitionIndex(category, currentId);
        if (currentIndex < 0)
        {
            currentIndex = step > 0 ? -1 : definitionCount;
        }

        for (int i = 0; i < definitionCount; i++)
        {
            int nextIndex = Mod(currentIndex + step, definitionCount);
            AppearanceDefinition candidate = definitions[nextIndex];
            if (IsDefinitionUsable(candidate, category))
            {
                definition = candidate;
                return true;
            }

            currentIndex = nextIndex;
        }

        return false;
    }

    public bool ValidateDatabase(UnityEngine.Object logContext = null)
    {
        bool isValid = true;
        HashSet<string> knownIds = new HashSet<string>(StringComparer.Ordinal);

        isValid &= ValidateDefinitions(CharacterAppearanceCategory.Body, knownIds, logContext);
        isValid &= ValidateDefinitions(CharacterAppearanceCategory.Hair, knownIds, logContext);
        isValid &= ValidateDefinitions(CharacterAppearanceCategory.Face, knownIds, logContext);
        isValid &= ValidateDefinitions(CharacterAppearanceCategory.Upper, knownIds, logContext);
        isValid &= ValidateDefinitions(CharacterAppearanceCategory.Pants, knownIds, logContext);
        isValid &= ValidateDefinitions(CharacterAppearanceCategory.Shoes, knownIds, logContext);

        isValid &= ValidateDefault(CharacterAppearanceCategory.Body, logContext);
        isValid &= ValidateDefault(CharacterAppearanceCategory.Hair, logContext);
        isValid &= ValidateDefault(CharacterAppearanceCategory.Face, logContext);
        isValid &= ValidateDefault(CharacterAppearanceCategory.Upper, logContext);
        isValid &= ValidateDefault(CharacterAppearanceCategory.Pants, logContext);
        isValid &= ValidateDefault(CharacterAppearanceCategory.Shoes, logContext);

        return isValid;
    }

    private void OnValidate()
    {
        ValidateDatabase(this);
    }

    private bool ValidateDefinitions(CharacterAppearanceCategory expectedCategory, HashSet<string> knownIds, UnityEngine.Object logContext)
    {
        bool isValid = true;
        List<AppearanceDefinition> definitions = GetDefinitions(expectedCategory);

        for (int i = 0; i < definitions.Count; i++)
        {
            AppearanceDefinition definition = definitions[i];
            string location = expectedCategory + " entry " + i;

            if (definition == null)
            {
                LogError("[CharacterAppearanceDatabase] " + location + " is null.", logContext);
                isValid = false;
                continue;
            }

            if (definition.Category != expectedCategory)
            {
                LogError("[CharacterAppearanceDatabase] " + location + " has category " + definition.Category + " but is stored in " + expectedCategory + ".", logContext);
                isValid = false;
            }

            if (string.IsNullOrEmpty(definition.Id))
            {
                LogError("[CharacterAppearanceDatabase] " + location + " has an empty id.", logContext);
                isValid = false;
            }
            else if (!knownIds.Add(definition.Id))
            {
                LogError("[CharacterAppearanceDatabase] Duplicate appearance id '" + definition.Id + "' found at " + location + ".", logContext);
                isValid = false;
            }

            if (!string.Equals(definition.CompatibilityKey, AdultFemaleCompatibilityKey, StringComparison.Ordinal))
            {
                LogError("[CharacterAppearanceDatabase] " + location + " uses compatibility key '" + definition.CompatibilityKey + "'. V1 only supports '" + AdultFemaleCompatibilityKey + "'.", logContext);
                isValid = false;
            }

            if (definition.Prefab == null)
            {
                LogError("[CharacterAppearanceDatabase] " + location + " with id '" + definition.Id + "' has no prefab assigned.", logContext);
                isValid = false;
            }
        }

        return isValid;
    }

    private bool ValidateDefault(CharacterAppearanceCategory category, UnityEngine.Object logContext)
    {
        string defaultId = GetDefaultId(category);
        if (string.IsNullOrEmpty(defaultId))
        {
            LogError("[CharacterAppearanceDatabase] Missing default id for category " + category + ".", logContext);
            return false;
        }

        AppearanceDefinition definition;
        if (!TryGetDefinition(category, defaultId, out definition))
        {
            LogError("[CharacterAppearanceDatabase] Default id '" + defaultId + "' for category " + category + " does not resolve to a valid Adult Female definition.", logContext);
            return false;
        }

        return true;
    }

    private int FindDefinitionIndex(CharacterAppearanceCategory category, string id)
    {
        string normalizedId = CharacterAppearanceData.NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId))
        {
            return -1;
        }

        List<AppearanceDefinition> definitions = GetDefinitions(category);
        for (int i = 0; i < definitions.Count; i++)
        {
            AppearanceDefinition definition = definitions[i];
            if (IsDefinitionUsable(definition, category) && string.Equals(definition.Id, normalizedId, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private List<AppearanceDefinition> GetDefinitions(CharacterAppearanceCategory category)
    {
        switch (category)
        {
            case CharacterAppearanceCategory.Body:
                return bodyDefinitions;
            case CharacterAppearanceCategory.Hair:
                return hairDefinitions;
            case CharacterAppearanceCategory.Face:
                return faceDefinitions;
            case CharacterAppearanceCategory.Upper:
                return upperDefinitions;
            case CharacterAppearanceCategory.Pants:
                return pantsDefinitions;
            case CharacterAppearanceCategory.Shoes:
                return shoesDefinitions;
            default:
                return bodyDefinitions;
        }
    }

    private static bool IsDefinitionUsable(AppearanceDefinition definition, CharacterAppearanceCategory expectedCategory)
    {
        return definition != null
            && definition.Category == expectedCategory
            && !string.IsNullOrEmpty(definition.Id)
            && definition.Prefab != null
            && string.Equals(definition.CompatibilityKey, AdultFemaleCompatibilityKey, StringComparison.Ordinal);
    }

    private static int Mod(int value, int length)
    {
        return ((value % length) + length) % length;
    }

    private static void LogError(string message, UnityEngine.Object context)
    {
        if (context != null)
        {
            Debug.LogError(message, context);
        }
        else
        {
            Debug.LogError(message);
        }
    }
}
