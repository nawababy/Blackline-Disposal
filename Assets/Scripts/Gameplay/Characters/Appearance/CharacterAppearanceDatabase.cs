using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
    public const string AdultFemaleBodyTypeId = "adult_female";
    public const string AdultMaleBodyTypeId = "adult_male";
    public const string PlusSizeFemaleBodyTypeId = "plussize_female";
    public const string PlusSizeMaleBodyTypeId = "plussize_male";

    [Serializable]
    public sealed class BodyTypeDefinition
    {
        [SerializeField] private string bodyTypeId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private string defaultSkinId = string.Empty;
        [SerializeField] private string defaultHairId = string.Empty;
        [SerializeField] private string defaultFaceId = string.Empty;
        [SerializeField] private string defaultUpperId = string.Empty;
        [SerializeField] private string defaultPantsId = string.Empty;
        [SerializeField] private string defaultShoesId = string.Empty;

        public string BodyTypeId { get { return CharacterAppearanceData.NormalizeId(bodyTypeId); } }
        public string DisplayName { get { return string.IsNullOrWhiteSpace(displayName) ? BodyTypeId : displayName.Trim(); } }
        public string DefaultSkinId { get { return CharacterAppearanceData.NormalizeId(defaultSkinId); } }
        public string GetDefaultId(CharacterAppearanceCategory category)
        {
            switch (category)
            {
                case CharacterAppearanceCategory.Hair: return CharacterAppearanceData.NormalizeId(defaultHairId);
                case CharacterAppearanceCategory.Face: return CharacterAppearanceData.NormalizeId(defaultFaceId);
                case CharacterAppearanceCategory.Upper: return CharacterAppearanceData.NormalizeId(defaultUpperId);
                case CharacterAppearanceCategory.Pants: return CharacterAppearanceData.NormalizeId(defaultPantsId);
                case CharacterAppearanceCategory.Shoes: return CharacterAppearanceData.NormalizeId(defaultShoesId);
                default: return string.Empty;
            }
        }
    }

    [Serializable]
    public sealed class BodyDefinition
    {
        [SerializeField] private string id = string.Empty;
        [FormerlySerializedAs("compatibilityKey")]
        [SerializeField] private string bodyTypeId = string.Empty;
        [SerializeField] private string skinId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private GameObject prefab;

        public string Id { get { return CharacterAppearanceData.NormalizeId(id); } }
        public string BodyTypeId { get { return CharacterAppearanceData.NormalizeId(bodyTypeId); } }
        public string SkinId { get { return CharacterAppearanceData.NormalizeId(skinId); } }
        public string DisplayName { get { return string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim(); } }
        public GameObject Prefab { get { return prefab; } }
    }

    [Serializable]
    public sealed class AppearanceDefinition
    {
        [SerializeField] private string id = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private CharacterAppearanceCategory category;
        [FormerlySerializedAs("compatibilityKey")]
        [SerializeField] private string bodyTypeId = AdultFemaleBodyTypeId;
        [SerializeField] private string skinId = string.Empty;
        [SerializeField] private GameObject prefab;

        public string Id { get { return CharacterAppearanceData.NormalizeId(id); } }
        public string DisplayName { get { return string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim(); } }
        public CharacterAppearanceCategory Category { get { return category; } }
        public string BodyTypeId { get { return CharacterAppearanceData.NormalizeId(bodyTypeId); } }
        public string SkinId { get { return CharacterAppearanceData.NormalizeId(skinId); } }
        public bool IsSkinSpecific { get { return !string.IsNullOrEmpty(SkinId); } }
        public GameObject Prefab { get { return prefab; } }
    }

    [Header("Body Types")]
    [SerializeField] private string defaultBodyTypeId = AdultFemaleBodyTypeId;
    [SerializeField] private List<BodyTypeDefinition> bodyTypeDefinitions = new List<BodyTypeDefinition>();

    [Header("Body Skin Variants")]
    [FormerlySerializedAs("bodyDefinitions")]
    [SerializeField] private List<BodyDefinition> bodyDefinitions = new List<BodyDefinition>();

    [Header("Modular Parts")]
    [SerializeField] private List<AppearanceDefinition> hairDefinitions = new List<AppearanceDefinition>();
    [SerializeField] private List<AppearanceDefinition> faceDefinitions = new List<AppearanceDefinition>();
    [SerializeField] private List<AppearanceDefinition> upperDefinitions = new List<AppearanceDefinition>();
    [SerializeField] private List<AppearanceDefinition> pantsDefinitions = new List<AppearanceDefinition>();
    [SerializeField] private List<AppearanceDefinition> shoesDefinitions = new List<AppearanceDefinition>();

    public int GetBodyTypeCount() { return bodyTypeDefinitions.Count; }
    public string GetDefaultBodyTypeId() { return CharacterAppearanceData.NormalizeId(defaultBodyTypeId); }
    public string GetDefaultSkinId(string bodyTypeId) { BodyTypeDefinition bodyType; return TryGetBodyType(bodyTypeId, out bodyType) ? bodyType.DefaultSkinId : string.Empty; }
    public string GetDefaultId(string bodyTypeId, CharacterAppearanceCategory category) { BodyTypeDefinition bodyType; return TryGetBodyType(bodyTypeId, out bodyType) ? bodyType.GetDefaultId(category) : string.Empty; }

    public void GetBodyTypes(List<string> results)
    {
        if (results == null) { Debug.LogError("[CharacterAppearanceDatabase] Result list is null.", this); return; }
        results.Clear();
        for (int i = 0; i < bodyTypeDefinitions.Count; i++)
        {
            BodyTypeDefinition bodyType = bodyTypeDefinitions[i];
            if (bodyType != null && !string.IsNullOrEmpty(bodyType.BodyTypeId)) { results.Add(bodyType.BodyTypeId); }
        }
    }

    public bool TryGetBodyType(string bodyTypeId, out BodyTypeDefinition definition)
    {
        definition = null;
        string normalizedBodyTypeId = CharacterAppearanceData.NormalizeId(bodyTypeId);
        if (string.IsNullOrEmpty(normalizedBodyTypeId)) { return false; }
        for (int i = 0; i < bodyTypeDefinitions.Count; i++)
        {
            BodyTypeDefinition candidate = bodyTypeDefinitions[i];
            if (candidate != null && string.Equals(candidate.BodyTypeId, normalizedBodyTypeId, StringComparison.Ordinal)) { definition = candidate; return true; }
        }
        return false;
    }

    public bool TryGetBodyTypeAtIndex(int index, out BodyTypeDefinition definition)
    {
        definition = null;
        if (index < 0 || index >= bodyTypeDefinitions.Count) { return false; }
        BodyTypeDefinition candidate = bodyTypeDefinitions[index];
        if (candidate == null || string.IsNullOrEmpty(candidate.BodyTypeId)) { return false; }
        definition = candidate;
        return true;
    }

    public bool TryGetNextBodyType(string currentBodyTypeId, int direction, out BodyTypeDefinition definition)
    {
        definition = null;
        if (bodyTypeDefinitions.Count == 0) { return false; }
        int step = direction < 0 ? -1 : 1;
        int currentIndex = FindBodyTypeIndex(currentBodyTypeId);
        if (currentIndex < 0) { currentIndex = step > 0 ? -1 : bodyTypeDefinitions.Count; }
        for (int i = 0; i < bodyTypeDefinitions.Count; i++)
        {
            int nextIndex = Mod(currentIndex + step, bodyTypeDefinitions.Count);
            BodyTypeDefinition candidate = bodyTypeDefinitions[nextIndex];
            if (candidate != null && !string.IsNullOrEmpty(candidate.BodyTypeId)) { definition = candidate; return true; }
            currentIndex = nextIndex;
        }
        return false;
    }

    public bool TryGetBodyDefinition(string bodyTypeId, string skinId, out BodyDefinition definition)
    {
        definition = null;
        string normalizedBodyTypeId = CharacterAppearanceData.NormalizeId(bodyTypeId);
        string normalizedSkinId = CharacterAppearanceData.NormalizeId(skinId);
        if (string.IsNullOrEmpty(normalizedBodyTypeId) || string.IsNullOrEmpty(normalizedSkinId)) { return false; }
        for (int i = 0; i < bodyDefinitions.Count; i++)
        {
            BodyDefinition candidate = bodyDefinitions[i];
            if (candidate != null && candidate.Prefab != null && string.Equals(candidate.BodyTypeId, normalizedBodyTypeId, StringComparison.Ordinal) && string.Equals(candidate.SkinId, normalizedSkinId, StringComparison.Ordinal)) { definition = candidate; return true; }
        }
        return false;
    }

    public bool TryGetBodyDefinitionById(string id, out BodyDefinition definition)
    {
        definition = null;
        string normalizedId = CharacterAppearanceData.NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId)) { return false; }
        for (int i = 0; i < bodyDefinitions.Count; i++)
        {
            BodyDefinition candidate = bodyDefinitions[i];
            if (candidate != null && candidate.Prefab != null && string.Equals(candidate.Id, normalizedId, StringComparison.Ordinal)) { definition = candidate; return true; }
        }
        return false;
    }

    public bool TryGetDefaultBodyDefinition(string bodyTypeId, out BodyDefinition definition)
    {
        return TryGetBodyDefinition(bodyTypeId, GetDefaultSkinId(bodyTypeId), out definition);
    }

    public int GetSkinCount(string bodyTypeId)
    {
        int count = 0;
        string normalizedBodyTypeId = CharacterAppearanceData.NormalizeId(bodyTypeId);
        for (int i = 0; i < bodyDefinitions.Count; i++)
        {
            BodyDefinition candidate = bodyDefinitions[i];
            if (candidate != null && candidate.Prefab != null && string.Equals(candidate.BodyTypeId, normalizedBodyTypeId, StringComparison.Ordinal) && !string.IsNullOrEmpty(candidate.SkinId)) { count++; }
        }
        return count;
    }

    public void GetSkinIds(string bodyTypeId, List<string> results)
    {
        if (results == null) { Debug.LogError("[CharacterAppearanceDatabase] Result list is null.", this); return; }
        results.Clear();
        string normalizedBodyTypeId = CharacterAppearanceData.NormalizeId(bodyTypeId);
        for (int i = 0; i < bodyDefinitions.Count; i++)
        {
            BodyDefinition candidate = bodyDefinitions[i];
            if (candidate != null && candidate.Prefab != null && string.Equals(candidate.BodyTypeId, normalizedBodyTypeId, StringComparison.Ordinal) && !string.IsNullOrEmpty(candidate.SkinId)) { results.Add(candidate.SkinId); }
        }
    }

    public bool TryGetNextSkin(string bodyTypeId, string currentSkinId, int direction, out BodyDefinition definition)
    {
        definition = null;
        string normalizedBodyTypeId = CharacterAppearanceData.NormalizeId(bodyTypeId);
        if (string.IsNullOrEmpty(normalizedBodyTypeId) || bodyDefinitions.Count == 0) { return false; }
        int step = direction < 0 ? -1 : 1;
        int currentIndex = FindBodyDefinitionIndex(normalizedBodyTypeId, currentSkinId);
        if (currentIndex < 0) { currentIndex = step > 0 ? -1 : bodyDefinitions.Count; }
        for (int i = 0; i < bodyDefinitions.Count; i++)
        {
            int nextIndex = Mod(currentIndex + step, bodyDefinitions.Count);
            BodyDefinition candidate = bodyDefinitions[nextIndex];
            if (candidate != null && candidate.Prefab != null && string.Equals(candidate.BodyTypeId, normalizedBodyTypeId, StringComparison.Ordinal) && !string.IsNullOrEmpty(candidate.SkinId)) { definition = candidate; return true; }
            currentIndex = nextIndex;
        }
        return false;
    }

    public int GetDefinitionCount(CharacterAppearanceCategory category) { return GetDefinitions(category).Count; }

    public bool TryGetDefinitionAtIndex(CharacterAppearanceCategory category, int index, out AppearanceDefinition definition)
    {
        definition = null;
        List<AppearanceDefinition> definitions = GetDefinitions(category);
        if (index < 0 || index >= definitions.Count) { return false; }
        AppearanceDefinition candidate = definitions[index];
        if (!IsDefinitionUsable(candidate, category)) { return false; }
        definition = candidate;
        return true;
    }

    public int GetCompatibleDefinitionCount(CharacterAppearanceCategory category, string bodyTypeId, string skinId)
    {
        int count = 0;
        List<AppearanceDefinition> definitions = GetDefinitions(category);
        for (int i = 0; i < definitions.Count; i++)
        {
            if (IsDefinitionCompatible(definitions[i], category, bodyTypeId, skinId)) { count++; }
        }
        return count;
    }

    public int GetCompatibleDefinitionCount(CharacterAppearanceCategory category)
    {
        string bodyTypeId = GetDefaultBodyTypeId();
        return GetCompatibleDefinitionCount(category, bodyTypeId, GetDefaultSkinId(bodyTypeId));
    }

    public void GetCompatibleDefinitions(CharacterAppearanceCategory category, string bodyTypeId, string skinId, List<AppearanceDefinition> results)
    {
        if (results == null) { Debug.LogError("[CharacterAppearanceDatabase] Result list is null.", this); return; }
        results.Clear();
        List<AppearanceDefinition> definitions = GetDefinitions(category);
        for (int i = 0; i < definitions.Count; i++)
        {
            AppearanceDefinition definition = definitions[i];
            if (IsDefinitionCompatible(definition, category, bodyTypeId, skinId)) { results.Add(definition); }
        }
    }

    public void GetCompatibleDefinitions(CharacterAppearanceCategory category, List<AppearanceDefinition> results)
    {
        string bodyTypeId = GetDefaultBodyTypeId();
        GetCompatibleDefinitions(category, bodyTypeId, GetDefaultSkinId(bodyTypeId), results);
    }

    public bool TryGetDefinition(CharacterAppearanceCategory category, string bodyTypeId, string skinId, string id, out AppearanceDefinition definition)
    {
        definition = null;
        string normalizedId = CharacterAppearanceData.NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId)) { return false; }
        List<AppearanceDefinition> definitions = GetDefinitions(category);
        for (int i = 0; i < definitions.Count; i++)
        {
            AppearanceDefinition candidate = definitions[i];
            if (IsDefinitionCompatible(candidate, category, bodyTypeId, skinId) && string.Equals(candidate.Id, normalizedId, StringComparison.Ordinal)) { definition = candidate; return true; }
        }
        return false;
    }

    public bool TryGetDefinition(CharacterAppearanceCategory category, string id, out AppearanceDefinition definition)
    {
        string bodyTypeId = GetDefaultBodyTypeId();
        return TryGetDefinition(category, bodyTypeId, GetDefaultSkinId(bodyTypeId), id, out definition);
    }

    public bool TryGetDefaultDefinition(string bodyTypeId, string skinId, CharacterAppearanceCategory category, out AppearanceDefinition definition)
    {
        definition = null;
        BodyTypeDefinition bodyType;
        return TryGetBodyType(bodyTypeId, out bodyType) && TryGetDefinition(category, bodyType.BodyTypeId, skinId, bodyType.GetDefaultId(category), out definition);
    }

    public bool TryGetDefaultDefinition(CharacterAppearanceCategory category, out AppearanceDefinition definition)
    {
        string bodyTypeId = GetDefaultBodyTypeId();
        return TryGetDefaultDefinition(bodyTypeId, GetDefaultSkinId(bodyTypeId), category, out definition);
    }

    public bool TryGetNextDefinition(CharacterAppearanceCategory category, string bodyTypeId, string skinId, string currentId, int direction, out AppearanceDefinition definition)
    {
        definition = null;
        List<AppearanceDefinition> definitions = GetDefinitions(category);
        if (definitions.Count == 0) { return false; }
        int step = direction < 0 ? -1 : 1;
        int currentIndex = FindDefinitionIndex(category, bodyTypeId, skinId, currentId);
        if (currentIndex < 0) { currentIndex = step > 0 ? -1 : definitions.Count; }
        for (int i = 0; i < definitions.Count; i++)
        {
            int nextIndex = Mod(currentIndex + step, definitions.Count);
            AppearanceDefinition candidate = definitions[nextIndex];
            if (IsDefinitionCompatible(candidate, category, bodyTypeId, skinId)) { definition = candidate; return true; }
            currentIndex = nextIndex;
        }
        return false;
    }

    public bool TryGetNextDefinition(CharacterAppearanceCategory category, string currentId, int direction, out AppearanceDefinition definition)
    {
        string bodyTypeId = GetDefaultBodyTypeId();
        return TryGetNextDefinition(category, bodyTypeId, GetDefaultSkinId(bodyTypeId), currentId, direction, out definition);
    }

    public bool ResolveAppearance(CharacterAppearanceData source, CharacterAppearanceData resolvedAppearance, UnityEngine.Object logContext = null)
    {
        if (resolvedAppearance == null) { LogError("[CharacterAppearanceDatabase] Cannot resolve appearance because target data is null.", logContext); return false; }
        string requestedBodyTypeId = source == null ? string.Empty : CharacterAppearanceData.NormalizeId(source.bodyTypeId);
        string requestedSkinId = source == null ? string.Empty : CharacterAppearanceData.NormalizeId(source.skinId);
        BodyDefinition legacyBodyDefinition;
        if (!TryGetBodyType(requestedBodyTypeId, out _) && TryGetBodyDefinitionById(requestedBodyTypeId, out legacyBodyDefinition))
        {
            requestedBodyTypeId = legacyBodyDefinition.BodyTypeId;
            requestedSkinId = legacyBodyDefinition.SkinId;
        }

        BodyTypeDefinition bodyType;
        if (!TryGetBodyType(requestedBodyTypeId, out bodyType))
        {
            string fallbackBodyTypeId = GetDefaultBodyTypeId();
            if (!TryGetBodyType(fallbackBodyTypeId, out bodyType) && !TryGetBodyTypeAtIndex(0, out bodyType))
            {
                LogError("[CharacterAppearanceDatabase] Cannot resolve appearance because no valid body type is configured.", logContext);
                return false;
            }
            if (!string.IsNullOrEmpty(requestedBodyTypeId)) { LogWarning("[CharacterAppearanceDatabase] BodyType '" + requestedBodyTypeId + "' is invalid. Falling back to '" + bodyType.BodyTypeId + "'.", logContext); }
        }

        BodyDefinition bodyDefinition;
        if (!TryGetBodyDefinition(bodyType.BodyTypeId, requestedSkinId, out bodyDefinition))
        {
            if (!string.IsNullOrEmpty(requestedSkinId)) { LogWarning("[CharacterAppearanceDatabase] Skin '" + requestedSkinId + "' is invalid for BodyType '" + bodyType.BodyTypeId + "'. Falling back to default.", logContext); }
            if (!TryGetDefaultBodyDefinition(bodyType.BodyTypeId, out bodyDefinition) && !TryGetNextSkin(bodyType.BodyTypeId, string.Empty, 1, out bodyDefinition))
            {
                LogError("[CharacterAppearanceDatabase] Cannot resolve a valid skin for BodyType '" + bodyType.BodyTypeId + "'.", logContext);
                return false;
            }
        }

        CharacterAppearanceData sourceSnapshot = source == null ? null : source.Clone();
        resolvedAppearance.appearanceVersion = CharacterAppearanceData.CurrentVersion;
        resolvedAppearance.bodyTypeId = bodyType.BodyTypeId;
        resolvedAppearance.skinId = bodyDefinition.SkinId;
        bool ok = true;
        ok &= ResolvePart(sourceSnapshot, bodyType.BodyTypeId, bodyDefinition.SkinId, CharacterAppearanceCategory.Hair, resolvedAppearance, logContext);
        ok &= ResolvePart(sourceSnapshot, bodyType.BodyTypeId, bodyDefinition.SkinId, CharacterAppearanceCategory.Face, resolvedAppearance, logContext);
        ok &= ResolvePart(sourceSnapshot, bodyType.BodyTypeId, bodyDefinition.SkinId, CharacterAppearanceCategory.Upper, resolvedAppearance, logContext);
        ok &= ResolvePart(sourceSnapshot, bodyType.BodyTypeId, bodyDefinition.SkinId, CharacterAppearanceCategory.Pants, resolvedAppearance, logContext);
        ok &= ResolvePart(sourceSnapshot, bodyType.BodyTypeId, bodyDefinition.SkinId, CharacterAppearanceCategory.Shoes, resolvedAppearance, logContext);
        return ok;
    }

    private bool ResolvePart(CharacterAppearanceData source, string bodyTypeId, string skinId, CharacterAppearanceCategory category, CharacterAppearanceData resolvedAppearance, UnityEngine.Object logContext)
    {
        string requestedId = source == null ? string.Empty : source.GetId(category);
        AppearanceDefinition definition;
        if (TryGetDefinition(category, bodyTypeId, skinId, requestedId, out definition)) { resolvedAppearance.SetId(category, definition.Id); return true; }
        if (!string.IsNullOrEmpty(requestedId)) { LogWarning("[CharacterAppearanceDatabase] " + category + " id '" + requestedId + "' is invalid for BodyType '" + bodyTypeId + "' and Skin '" + skinId + "'. Falling back to default.", logContext); }
        if (TryGetDefaultDefinition(bodyTypeId, skinId, category, out definition)) { resolvedAppearance.SetId(category, definition.Id); return true; }
        LogError("[CharacterAppearanceDatabase] Cannot resolve a valid default for BodyType '" + bodyTypeId + "' category " + category + ".", logContext);
        resolvedAppearance.SetId(category, string.Empty);
        return false;
    }

    public bool ValidateDatabase(UnityEngine.Object logContext = null)
    {
        bool valid = true;
        HashSet<string> bodyTypeIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> definitionIds = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> bodySkinKeys = new HashSet<string>(StringComparer.Ordinal);
        valid &= ValidateBodyTypes(bodyTypeIds, logContext);
        valid &= ValidateBodyDefinitions(bodyTypeIds, definitionIds, bodySkinKeys, logContext);
        valid &= ValidateDefinitions(CharacterAppearanceCategory.Hair, bodyTypeIds, definitionIds, bodySkinKeys, logContext);
        valid &= ValidateDefinitions(CharacterAppearanceCategory.Face, bodyTypeIds, definitionIds, bodySkinKeys, logContext);
        valid &= ValidateDefinitions(CharacterAppearanceCategory.Upper, bodyTypeIds, definitionIds, bodySkinKeys, logContext);
        valid &= ValidateDefinitions(CharacterAppearanceCategory.Pants, bodyTypeIds, definitionIds, bodySkinKeys, logContext);
        valid &= ValidateDefinitions(CharacterAppearanceCategory.Shoes, bodyTypeIds, definitionIds, bodySkinKeys, logContext);
        valid &= ValidateDefaults(logContext);
        return valid;
    }

    private void OnValidate()
    {
        ValidateDatabase(this);
    }

    private bool ValidateBodyTypes(HashSet<string> bodyTypeIds, UnityEngine.Object logContext)
    {
        bool valid = true;
        for (int i = 0; i < bodyTypeDefinitions.Count; i++)
        {
            BodyTypeDefinition bodyType = bodyTypeDefinitions[i];
            string location = "BodyType entry " + i;
            if (bodyType == null) { LogError("[CharacterAppearanceDatabase] " + location + " is null.", logContext); valid = false; continue; }
            if (string.IsNullOrEmpty(bodyType.BodyTypeId)) { LogError("[CharacterAppearanceDatabase] " + location + " has an empty bodyTypeId.", logContext); valid = false; }
            else if (!bodyTypeIds.Add(bodyType.BodyTypeId)) { LogError("[CharacterAppearanceDatabase] Duplicate bodyTypeId '" + bodyType.BodyTypeId + "'.", logContext); valid = false; }
        }
        if (string.IsNullOrEmpty(GetDefaultBodyTypeId()) || !bodyTypeIds.Contains(GetDefaultBodyTypeId()))
        {
            LogError("[CharacterAppearanceDatabase] Default BodyType '" + GetDefaultBodyTypeId() + "' is missing or invalid.", logContext);
            valid = false;
        }
        return valid;
    }

    private bool ValidateBodyDefinitions(HashSet<string> bodyTypeIds, HashSet<string> definitionIds, HashSet<string> bodySkinKeys, UnityEngine.Object logContext)
    {
        bool valid = true;
        for (int i = 0; i < bodyDefinitions.Count; i++)
        {
            BodyDefinition definition = bodyDefinitions[i];
            string location = "Body Skin entry " + i;
            if (definition == null) { LogError("[CharacterAppearanceDatabase] " + location + " is null.", logContext); valid = false; continue; }
            valid &= ValidateDefinitionId(definition.Id, location, definitionIds, logContext);
            if (string.IsNullOrEmpty(definition.BodyTypeId) || !bodyTypeIds.Contains(definition.BodyTypeId)) { LogError("[CharacterAppearanceDatabase] " + location + " has invalid bodyTypeId '" + definition.BodyTypeId + "'.", logContext); valid = false; }
            if (string.IsNullOrEmpty(definition.SkinId)) { LogError("[CharacterAppearanceDatabase] " + location + " has an empty skinId.", logContext); valid = false; }
            else if (!bodySkinKeys.Add(GetBodySkinKey(definition.BodyTypeId, definition.SkinId))) { LogError("[CharacterAppearanceDatabase] Duplicate skin '" + definition.SkinId + "' for BodyType '" + definition.BodyTypeId + "'.", logContext); valid = false; }
            if (definition.Prefab == null) { LogError("[CharacterAppearanceDatabase] " + location + " with id '" + definition.Id + "' has no prefab assigned.", logContext); valid = false; }
        }
        return valid;
    }

    private bool ValidateDefinitions(CharacterAppearanceCategory category, HashSet<string> bodyTypeIds, HashSet<string> definitionIds, HashSet<string> bodySkinKeys, UnityEngine.Object logContext)
    {
        bool valid = true;
        List<AppearanceDefinition> definitions = GetDefinitions(category);
        for (int i = 0; i < definitions.Count; i++)
        {
            AppearanceDefinition definition = definitions[i];
            string location = category + " entry " + i;
            if (definition == null) { LogError("[CharacterAppearanceDatabase] " + location + " is null.", logContext); valid = false; continue; }
            valid &= ValidateDefinitionId(definition.Id, location, definitionIds, logContext);
            if (definition.Category != category) { LogError("[CharacterAppearanceDatabase] " + location + " has category " + definition.Category + " but is stored in " + category + ".", logContext); valid = false; }
            if (string.IsNullOrEmpty(definition.BodyTypeId) || !bodyTypeIds.Contains(definition.BodyTypeId)) { LogError("[CharacterAppearanceDatabase] " + location + " has invalid bodyTypeId '" + definition.BodyTypeId + "'.", logContext); valid = false; }
            if (!string.IsNullOrEmpty(definition.SkinId) && !bodySkinKeys.Contains(GetBodySkinKey(definition.BodyTypeId, definition.SkinId))) { LogError("[CharacterAppearanceDatabase] " + location + " references unknown skin '" + definition.SkinId + "' for BodyType '" + definition.BodyTypeId + "'.", logContext); valid = false; }
            if (definition.Prefab == null) { LogError("[CharacterAppearanceDatabase] " + location + " with id '" + definition.Id + "' has no prefab assigned.", logContext); valid = false; }
        }
        return valid;
    }

    private bool ValidateDefaults(UnityEngine.Object logContext)
    {
        bool valid = true;
        for (int i = 0; i < bodyTypeDefinitions.Count; i++)
        {
            BodyTypeDefinition bodyType = bodyTypeDefinitions[i];
            if (bodyType == null || string.IsNullOrEmpty(bodyType.BodyTypeId)) { continue; }
            BodyDefinition bodyDefinition;
            if (!TryGetBodyDefinition(bodyType.BodyTypeId, bodyType.DefaultSkinId, out bodyDefinition)) { LogError("[CharacterAppearanceDatabase] Default skin '" + bodyType.DefaultSkinId + "' is invalid for BodyType '" + bodyType.BodyTypeId + "'.", logContext); valid = false; }
            valid &= ValidatePartDefault(bodyType, bodyType.DefaultSkinId, CharacterAppearanceCategory.Hair, logContext);
            valid &= ValidatePartDefault(bodyType, bodyType.DefaultSkinId, CharacterAppearanceCategory.Face, logContext);
            valid &= ValidatePartDefault(bodyType, bodyType.DefaultSkinId, CharacterAppearanceCategory.Upper, logContext);
            valid &= ValidatePartDefault(bodyType, bodyType.DefaultSkinId, CharacterAppearanceCategory.Pants, logContext);
            valid &= ValidatePartDefault(bodyType, bodyType.DefaultSkinId, CharacterAppearanceCategory.Shoes, logContext);
        }
        return valid;
    }

    private bool ValidatePartDefault(BodyTypeDefinition bodyType, string skinId, CharacterAppearanceCategory category, UnityEngine.Object logContext)
    {
        AppearanceDefinition definition;
        if (TryGetDefinition(category, bodyType.BodyTypeId, skinId, bodyType.GetDefaultId(category), out definition)) { return true; }
        LogError("[CharacterAppearanceDatabase] Default " + category + " id '" + bodyType.GetDefaultId(category) + "' is invalid for BodyType '" + bodyType.BodyTypeId + "'.", logContext);
        return false;
    }

    private bool ValidateDefinitionId(string id, string location, HashSet<string> definitionIds, UnityEngine.Object logContext)
    {
        if (string.IsNullOrEmpty(id)) { LogError("[CharacterAppearanceDatabase] " + location + " has an empty id.", logContext); return false; }
        if (!definitionIds.Add(id)) { LogError("[CharacterAppearanceDatabase] Duplicate definition id '" + id + "' found at " + location + ".", logContext); return false; }
        return true;
    }

    private int FindBodyTypeIndex(string bodyTypeId)
    {
        string normalizedBodyTypeId = CharacterAppearanceData.NormalizeId(bodyTypeId);
        for (int i = 0; i < bodyTypeDefinitions.Count; i++)
        {
            BodyTypeDefinition definition = bodyTypeDefinitions[i];
            if (definition != null && string.Equals(definition.BodyTypeId, normalizedBodyTypeId, StringComparison.Ordinal)) { return i; }
        }
        return -1;
    }

    private int FindBodyDefinitionIndex(string bodyTypeId, string skinId)
    {
        string normalizedBodyTypeId = CharacterAppearanceData.NormalizeId(bodyTypeId);
        string normalizedSkinId = CharacterAppearanceData.NormalizeId(skinId);
        for (int i = 0; i < bodyDefinitions.Count; i++)
        {
            BodyDefinition definition = bodyDefinitions[i];
            if (definition != null && string.Equals(definition.BodyTypeId, normalizedBodyTypeId, StringComparison.Ordinal) && string.Equals(definition.SkinId, normalizedSkinId, StringComparison.Ordinal)) { return i; }
        }
        return -1;
    }

    private int FindDefinitionIndex(CharacterAppearanceCategory category, string bodyTypeId, string skinId, string id)
    {
        string normalizedId = CharacterAppearanceData.NormalizeId(id);
        if (string.IsNullOrEmpty(normalizedId)) { return -1; }
        List<AppearanceDefinition> definitions = GetDefinitions(category);
        for (int i = 0; i < definitions.Count; i++)
        {
            AppearanceDefinition definition = definitions[i];
            if (IsDefinitionCompatible(definition, category, bodyTypeId, skinId) && string.Equals(definition.Id, normalizedId, StringComparison.Ordinal)) { return i; }
        }
        return -1;
    }

    private List<AppearanceDefinition> GetDefinitions(CharacterAppearanceCategory category)
    {
        switch (category)
        {
            case CharacterAppearanceCategory.Hair: return hairDefinitions;
            case CharacterAppearanceCategory.Face: return faceDefinitions;
            case CharacterAppearanceCategory.Upper: return upperDefinitions;
            case CharacterAppearanceCategory.Pants: return pantsDefinitions;
            case CharacterAppearanceCategory.Shoes: return shoesDefinitions;
            default: return hairDefinitions;
        }
    }

    private static bool IsDefinitionUsable(AppearanceDefinition definition, CharacterAppearanceCategory category)
    {
        return definition != null && definition.Category == category && !string.IsNullOrEmpty(definition.Id) && !string.IsNullOrEmpty(definition.BodyTypeId) && definition.Prefab != null;
    }

    private static bool IsDefinitionCompatible(AppearanceDefinition definition, CharacterAppearanceCategory category, string bodyTypeId, string skinId)
    {
        if (!IsDefinitionUsable(definition, category)) { return false; }
        string normalizedBodyTypeId = CharacterAppearanceData.NormalizeId(bodyTypeId);
        string normalizedSkinId = CharacterAppearanceData.NormalizeId(skinId);
        return string.Equals(definition.BodyTypeId, normalizedBodyTypeId, StringComparison.Ordinal) && (string.IsNullOrEmpty(definition.SkinId) || string.Equals(definition.SkinId, normalizedSkinId, StringComparison.Ordinal));
    }

    private static string GetBodySkinKey(string bodyTypeId, string skinId)
    {
        return CharacterAppearanceData.NormalizeId(bodyTypeId) + "|" + CharacterAppearanceData.NormalizeId(skinId);
    }

    private static int Mod(int value, int length)
    {
        return ((value % length) + length) % length;
    }

    private static void LogWarning(string message, UnityEngine.Object context)
    {
        if (context != null) { Debug.LogWarning(message, context); }
        else { Debug.LogWarning(message); }
    }

    private static void LogError(string message, UnityEngine.Object context)
    {
        if (context != null) { Debug.LogError(message, context); }
        else { Debug.LogError(message); }
    }
}