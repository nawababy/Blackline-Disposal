using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class CharacterAppearanceDatabaseBuilder
{
    private const string MenuPath = "Tools/Blackline Disposal/Characters/Build Appearance Database";
    private const string AssetRoot = "Assets/ithappy/City_Characters";

    private static readonly BodyTypeSpec[] BodyTypes =
    {
        new BodyTypeSpec("adult_female", "Adult Female", "Adult_Female", "Assets/ithappy/City_Characters/Prefabs/Adult/Adult Female", "Assets/ithappy/City_Characters/Configs/BodyTypes/Adult/Female/Slots/Face.asset", "Assets/ithappy/City_Characters/Configs/BodyTypes/Adult/Female/Groups/Face.asset"),
        new BodyTypeSpec("adult_male", "Adult Male", "Adult_Male", "Assets/ithappy/City_Characters/Prefabs/Adult/Adult Male", "Assets/ithappy/City_Characters/Configs/BodyTypes/Adult/Male/Slots/Face.asset", "Assets/ithappy/City_Characters/Configs/BodyTypes/Adult/Male/Groups/Face.asset"),
        new BodyTypeSpec("plussize_female", "Plus-Size Female", "PlusSize_Female", "Assets/ithappy/City_Characters/Prefabs/Plus-Size/Plus-Size Female", "Assets/ithappy/City_Characters/Configs/BodyTypes/PlusSize/Female/Slots/Face.asset", "Assets/ithappy/City_Characters/Configs/BodyTypes/PlusSize/Female/Groups/Face.asset"),
        new BodyTypeSpec("plussize_male", "Plus-Size Male", "PlusSize_Male", "Assets/ithappy/City_Characters/Prefabs/Plus-Size/Plus-Size Male", "Assets/ithappy/City_Characters/Configs/BodyTypes/PlusSize/Male/Slots/Face.asset", "Assets/ithappy/City_Characters/Configs/BodyTypes/PlusSize/Male/Groups/Face.asset")
    };

    private sealed class BodyTypeSpec
    {
        public readonly string BodyTypeId;
        public readonly string DisplayName;
        public readonly string Prefix;
        public readonly string PrefabFolder;
        public readonly string FaceSlotConfigPath;
        public readonly string FaceGroupConfigPath;

        public BodyTypeSpec(string bodyTypeId, string displayName, string prefix, string prefabFolder, string faceSlotConfigPath, string faceGroupConfigPath)
        {
            BodyTypeId = bodyTypeId;
            DisplayName = displayName;
            Prefix = prefix;
            PrefabFolder = prefabFolder;
            FaceSlotConfigPath = faceSlotConfigPath;
            FaceGroupConfigPath = faceGroupConfigPath;
        }
    }

    private sealed class BodyTypeBuild
    {
        public readonly BodyTypeSpec Spec;
        public readonly List<BodyEntry> BodyEntries = new List<BodyEntry>();
        public readonly List<PartEntry> HairEntries = new List<PartEntry>();
        public readonly List<PartEntry> FaceEntries = new List<PartEntry>();
        public readonly List<PartEntry> UpperEntries = new List<PartEntry>();
        public readonly List<PartEntry> PantsEntries = new List<PartEntry>();
        public readonly List<PartEntry> ShoesEntries = new List<PartEntry>();
        public bool FaceIsSkinSpecific;

        public BodyTypeBuild(BodyTypeSpec spec)
        {
            Spec = spec;
        }
    }

    private sealed class BodyEntry
    {
        public string Id;
        public string BodyTypeId;
        public string SkinId;
        public string DisplayName;
        public GameObject Prefab;
        public string AssetPath;
    }

    private sealed class PartEntry
    {
        public string Id;
        public string BodyTypeId;
        public string SkinId;
        public string DisplayName;
        public CharacterAppearanceCategory Category;
        public GameObject Prefab;
        public string AssetPath;
        public string SourceName;
    }

    private sealed class BuildReport
    {
        public int BodyTypeCount;
        public int BodySkinCount;
        public int HairCount;
        public int FaceCount;
        public int UpperCount;
        public int PantsCount;
        public int ShoesCount;
        public readonly List<string> SkippedPrefabs = new List<string>();
        public readonly List<string> AmbiguousPrefabs = new List<string>();
        public readonly List<string> DuplicateIds = new List<string>();
        public readonly List<string> MissingDefaults = new List<string>();
        public readonly List<string> StructureNotes = new List<string>();
        public readonly List<string> FaceSkinNotes = new List<string>();
    }

    [MenuItem(MenuPath)]
    public static void BuildAppearanceDatabase()
    {
        CharacterAppearanceDatabase database = SelectDatabase();
        if (database == null)
        {
            return;
        }

        BuildReport report = new BuildReport();
        List<BodyTypeBuild> builds = AnalyzeAssetStructure(report);
        if (builds.Count == 0)
        {
            EditorUtility.DisplayDialog("Build Appearance Database", "No supported City_Characters prefabs were found.", "OK");
            Debug.LogWarning("[CharacterAppearanceDatabaseBuilder] No supported prefabs found under " + AssetRoot + ".");
            return;
        }

        if (HasExistingDatabaseContent(database))
        {
            bool replace = EditorUtility.DisplayDialog(
                "Replace Character Appearance Database?",
                "This will replace the generated BodyType, Body/Skin, Hair, Face, Upper, Pants and Shoes lists on the selected CharacterAppearanceDatabase. Existing manual entries in these lists will be overwritten.",
                "Replace",
                "Cancel");

            if (!replace)
            {
                Debug.Log("[CharacterAppearanceDatabaseBuilder] Build cancelled. Database was not modified.", database);
                return;
            }
        }

        Undo.RecordObject(database, "Build Character Appearance Database");
        SerializedObject serializedDatabase = new SerializedObject(database);
        serializedDatabase.Update();
        WriteDatabase(serializedDatabase, builds, report);
        serializedDatabase.ApplyModifiedProperties();

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string reportText = CreateReportText(report, database);
        Debug.Log(reportText, database);
        EditorUtility.DisplayDialog("Build Appearance Database", "Database build finished. See the Console for the full report.", "OK");
    }

    private static CharacterAppearanceDatabase SelectDatabase()
    {
        CharacterAppearanceDatabase selectedDatabase = Selection.activeObject as CharacterAppearanceDatabase;
        if (selectedDatabase != null)
        {
            return selectedDatabase;
        }

        string selectedPath = EditorUtility.OpenFilePanel("Select CharacterAppearanceDatabase", Application.dataPath, "asset");
        if (string.IsNullOrEmpty(selectedPath))
        {
            return null;
        }

        string projectPath = FileUtil.GetProjectRelativePath(selectedPath);
        CharacterAppearanceDatabase database = AssetDatabase.LoadAssetAtPath<CharacterAppearanceDatabase>(projectPath);
        if (database == null)
        {
            EditorUtility.DisplayDialog("Invalid Selection", "Please select an existing CharacterAppearanceDatabase asset.", "OK");
        }

        return database;
    }

    private static List<BodyTypeBuild> AnalyzeAssetStructure(BuildReport report)
    {
        List<BodyTypeBuild> builds = new List<BodyTypeBuild>();
        HashSet<string> knownIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < BodyTypes.Length; i++)
        {
            BodyTypeBuild build = AnalyzeBodyType(BodyTypes[i], report, knownIds);
            if (build != null)
            {
                builds.Add(build);
                report.BodyTypeCount++;
                report.BodySkinCount += build.BodyEntries.Count;
                report.HairCount += build.HairEntries.Count;
                report.FaceCount += build.FaceEntries.Count;
                report.UpperCount += build.UpperEntries.Count;
                report.PantsCount += build.PantsEntries.Count;
                report.ShoesCount += build.ShoesEntries.Count;
            }
        }

        return builds;
    }

    private static BodyTypeBuild AnalyzeBodyType(BodyTypeSpec spec, BuildReport report, HashSet<string> knownIds)
    {
        if (!AssetDatabase.IsValidFolder(spec.PrefabFolder))
        {
            report.AmbiguousPrefabs.Add(spec.BodyTypeId + ": missing folder " + spec.PrefabFolder);
            return null;
        }

        BodyTypeBuild build = new BodyTypeBuild(spec);
        report.StructureNotes.Add(spec.BodyTypeId + ": prefab folder " + spec.PrefabFolder);
        report.StructureNotes.Add(spec.BodyTypeId + ": face slot config " + GetConfigStatus(spec.FaceSlotConfigPath));
        report.StructureNotes.Add(spec.BodyTypeId + ": face group config " + GetConfigStatus(spec.FaceGroupConfigPath));

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { spec.PrefabFolder });
        List<string> prefabPaths = new List<string>();
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                prefabPaths.Add(prefabPath);
            }
        }

        prefabPaths.Sort(StringComparer.Ordinal);
        for (int i = 0; i < prefabPaths.Count; i++)
        {
            AnalyzePrefabPath(spec, build, prefabPaths[i], report, knownIds);
        }

        build.BodyEntries.Sort(CompareBodyEntries);
        SortPartEntries(build.HairEntries);
        SortPartEntries(build.FaceEntries);
        SortPartEntries(build.UpperEntries);
        SortPartEntries(build.PantsEntries);
        SortPartEntries(build.ShoesEntries);

        build.FaceIsSkinSpecific = DetectFaceSkinDependency(build, report);
        if (build.FaceIsSkinSpecific)
        {
            ApplySkinIdsToNumberedFaces(build);
        }

        AddMissingDefaultNotes(build, report);
        return build;
    }

    private static void AnalyzePrefabPath(BodyTypeSpec spec, BodyTypeBuild build, string prefabPath, BuildReport report, HashSet<string> knownIds)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            report.AmbiguousPrefabs.Add(prefabPath + " could not be loaded as GameObject prefab.");
            return;
        }

        string prefabName = Path.GetFileNameWithoutExtension(prefabPath);
        if (!prefabName.StartsWith(spec.Prefix + "_", StringComparison.Ordinal))
        {
            report.AmbiguousPrefabs.Add(prefabPath + " does not match expected prefix " + spec.Prefix + ".");
            return;
        }

        Match bodyMatch = Regex.Match(prefabName, "^" + Regex.Escape(spec.Prefix) + "_Body_([0-9]+)$");
        if (bodyMatch.Success)
        {
            string number = NormalizeNumber(bodyMatch.Groups[1].Value);
            BodyEntry entry = new BodyEntry
            {
                Id = spec.BodyTypeId + "_skin_" + number,
                BodyTypeId = spec.BodyTypeId,
                SkinId = "skin_" + number,
                DisplayName = spec.DisplayName + " Skin " + number,
                Prefab = prefab,
                AssetPath = prefabPath
            };
            AddBodyEntry(build, entry, report, knownIds);
            return;
        }

        Match hairMatch = Regex.Match(prefabName, "^" + Regex.Escape(spec.Prefix) + "_Hairstyle_([0-9]+)$");
        if (hairMatch.Success)
        {
            string number = NormalizeNumber(hairMatch.Groups[1].Value);
            AddPartEntry(build.HairEntries, CreatePartEntry(spec, CharacterAppearanceCategory.Hair, "hair_" + number, "Hair " + number, prefab, prefabPath, prefabName), report, knownIds);
            return;
        }

        Match faceMatch = Regex.Match(prefabName, "^" + Regex.Escape(spec.Prefix) + "_Face_(.+)$");
        if (faceMatch.Success)
        {
            string suffix = Slugify(faceMatch.Groups[1].Value);
            AddPartEntry(build.FaceEntries, CreatePartEntry(spec, CharacterAppearanceCategory.Face, "face_" + suffix, "Face " + ToDisplayText(suffix), prefab, prefabPath, prefabName), report, knownIds);
            return;
        }

        Match upperMatch = Regex.Match(prefabName, "^" + Regex.Escape(spec.Prefix) + "_Shirt_([0-9]+)$");
        if (upperMatch.Success)
        {
            string number = NormalizeNumber(upperMatch.Groups[1].Value);
            AddPartEntry(build.UpperEntries, CreatePartEntry(spec, CharacterAppearanceCategory.Upper, "upper_" + number, "Upper " + number, prefab, prefabPath, prefabName), report, knownIds);
            return;
        }

        Match pantsMatch = Regex.Match(prefabName, "^" + Regex.Escape(spec.Prefix) + "_Pants_([0-9]+)$");
        if (pantsMatch.Success)
        {
            string number = NormalizeNumber(pantsMatch.Groups[1].Value);
            AddPartEntry(build.PantsEntries, CreatePartEntry(spec, CharacterAppearanceCategory.Pants, "pants_" + number, "Pants " + number, prefab, prefabPath, prefabName), report, knownIds);
            return;
        }

        Match shoesMatch = Regex.Match(prefabName, "^" + Regex.Escape(spec.Prefix) + "_Shoes_([0-9]+)$");
        if (shoesMatch.Success)
        {
            string number = NormalizeNumber(shoesMatch.Groups[1].Value);
            AddPartEntry(build.ShoesEntries, CreatePartEntry(spec, CharacterAppearanceCategory.Shoes, "shoes_" + number, "Shoes " + number, prefab, prefabPath, prefabName), report, knownIds);
            return;
        }

        report.SkippedPrefabs.Add(prefabPath);
    }

    private static PartEntry CreatePartEntry(BodyTypeSpec spec, CharacterAppearanceCategory category, string idSuffix, string displaySuffix, GameObject prefab, string assetPath, string sourceName)
    {
        return new PartEntry
        {
            Id = spec.BodyTypeId + "_" + idSuffix,
            BodyTypeId = spec.BodyTypeId,
            SkinId = string.Empty,
            DisplayName = spec.DisplayName + " " + displaySuffix,
            Category = category,
            Prefab = prefab,
            AssetPath = assetPath,
            SourceName = sourceName
        };
    }

    private static void AddBodyEntry(BodyTypeBuild build, BodyEntry entry, BuildReport report, HashSet<string> knownIds)
    {
        if (!knownIds.Add(entry.Id))
        {
            report.DuplicateIds.Add(entry.Id + " from " + entry.AssetPath);
            return;
        }

        build.BodyEntries.Add(entry);
    }

    private static void AddPartEntry(List<PartEntry> entries, PartEntry entry, BuildReport report, HashSet<string> knownIds)
    {
        if (!knownIds.Add(entry.Id))
        {
            report.DuplicateIds.Add(entry.Id + " from " + entry.AssetPath);
            return;
        }

        entries.Add(entry);
    }

    private static bool DetectFaceSkinDependency(BodyTypeBuild build, BuildReport report)
    {
        bool hasFaceSlotConfig = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(build.Spec.FaceSlotConfigPath) != null;
        bool hasFaceGroupConfig = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(build.Spec.FaceGroupConfigPath) != null;
        bool numberedLikeSkin = build.FaceEntries.Count == build.BodyEntries.Count && build.FaceEntries.Count > 0;

        if (numberedLikeSkin)
        {
            for (int i = 0; i < build.FaceEntries.Count; i++)
            {
                if (!Regex.IsMatch(build.FaceEntries[i].SourceName, "_Face_[0-9]+$"))
                {
                    numberedLikeSkin = false;
                    break;
                }
            }
        }

        bool skinSpecific = hasFaceSlotConfig && hasFaceGroupConfig && numberedLikeSkin;
        report.FaceSkinNotes.Add(build.Spec.BodyTypeId + ": face config found=" + (hasFaceSlotConfig && hasFaceGroupConfig) + ", face count=" + build.FaceEntries.Count + ", skin count=" + build.BodyEntries.Count + ", skin-specific=" + skinSpecific);
        return skinSpecific;
    }

    private static void ApplySkinIdsToNumberedFaces(BodyTypeBuild build)
    {
        for (int i = 0; i < build.FaceEntries.Count; i++)
        {
            Match match = Regex.Match(build.FaceEntries[i].SourceName, "_Face_([0-9]+)$");
            if (match.Success)
            {
                build.FaceEntries[i].SkinId = "skin_" + NormalizeNumber(match.Groups[1].Value);
            }
        }
    }

    private static void AddMissingDefaultNotes(BodyTypeBuild build, BuildReport report)
    {
        AddMissingDefaultIfEmpty(build, report, "skin", GetDefaultSkinId(build));
        AddMissingDefaultIfEmpty(build, report, "hair", GetDefaultPartId(build.HairEntries, ""));
        AddMissingDefaultIfEmpty(build, report, "face", GetDefaultPartId(build.FaceEntries, "face_neutral"));
        AddMissingDefaultIfEmpty(build, report, "upper", GetDefaultPartId(build.UpperEntries, ""));
        AddMissingDefaultIfEmpty(build, report, "pants", GetDefaultPartId(build.PantsEntries, ""));
        AddMissingDefaultIfEmpty(build, report, "shoes", GetDefaultPartId(build.ShoesEntries, ""));
    }

    private static void AddMissingDefaultIfEmpty(BodyTypeBuild build, BuildReport report, string category, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            report.MissingDefaults.Add(build.Spec.BodyTypeId + " missing default " + category + ".");
        }
    }

    private static void WriteDatabase(SerializedObject serializedDatabase, List<BodyTypeBuild> builds, BuildReport report)
    {
        SerializedProperty defaultBodyTypeId = RequireProperty(serializedDatabase, "defaultBodyTypeId");
        SerializedProperty bodyTypeDefinitions = RequireProperty(serializedDatabase, "bodyTypeDefinitions");
        SerializedProperty bodyDefinitions = RequireProperty(serializedDatabase, "bodyDefinitions");
        SerializedProperty hairDefinitions = RequireProperty(serializedDatabase, "hairDefinitions");
        SerializedProperty faceDefinitions = RequireProperty(serializedDatabase, "faceDefinitions");
        SerializedProperty upperDefinitions = RequireProperty(serializedDatabase, "upperDefinitions");
        SerializedProperty pantsDefinitions = RequireProperty(serializedDatabase, "pantsDefinitions");
        SerializedProperty shoesDefinitions = RequireProperty(serializedDatabase, "shoesDefinitions");

        defaultBodyTypeId.stringValue = CharacterAppearanceDatabase.AdultFemaleBodyTypeId;
        ClearArray(bodyTypeDefinitions);
        ClearArray(bodyDefinitions);
        ClearArray(hairDefinitions);
        ClearArray(faceDefinitions);
        ClearArray(upperDefinitions);
        ClearArray(pantsDefinitions);
        ClearArray(shoesDefinitions);

        for (int i = 0; i < builds.Count; i++)
        {
            BodyTypeBuild build = builds[i];
            WriteBodyType(bodyTypeDefinitions, build);
            WriteBodyDefinitions(bodyDefinitions, build.BodyEntries);
            WritePartDefinitions(hairDefinitions, build.HairEntries);
            WritePartDefinitions(faceDefinitions, build.FaceEntries);
            WritePartDefinitions(upperDefinitions, build.UpperEntries);
            WritePartDefinitions(pantsDefinitions, build.PantsEntries);
            WritePartDefinitions(shoesDefinitions, build.ShoesEntries);
        }
    }

    private static void WriteBodyType(SerializedProperty array, BodyTypeBuild build)
    {
        SerializedProperty element = AddElement(array);
        SetString(element, "bodyTypeId", build.Spec.BodyTypeId);
        SetString(element, "displayName", build.Spec.DisplayName);
        SetString(element, "defaultSkinId", GetDefaultSkinId(build));
        SetString(element, "defaultHairId", GetDefaultPartId(build.HairEntries, ""));
        SetString(element, "defaultFaceId", GetDefaultPartId(build.FaceEntries, "face_neutral"));
        SetString(element, "defaultUpperId", GetDefaultPartId(build.UpperEntries, ""));
        SetString(element, "defaultPantsId", GetDefaultPartId(build.PantsEntries, ""));
        SetString(element, "defaultShoesId", GetDefaultPartId(build.ShoesEntries, ""));
    }

    private static void WriteBodyDefinitions(SerializedProperty array, List<BodyEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            BodyEntry entry = entries[i];
            SerializedProperty element = AddElement(array);
            SetString(element, "id", entry.Id);
            SetString(element, "bodyTypeId", entry.BodyTypeId);
            SetString(element, "skinId", entry.SkinId);
            SetString(element, "displayName", entry.DisplayName);
            SetObject(element, "prefab", entry.Prefab);
        }
    }

    private static void WritePartDefinitions(SerializedProperty array, List<PartEntry> entries)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            PartEntry entry = entries[i];
            SerializedProperty element = AddElement(array);
            SetString(element, "id", entry.Id);
            SetString(element, "displayName", entry.DisplayName);
            SetEnum(element, "category", entry.Category);
            SetString(element, "bodyTypeId", entry.BodyTypeId);
            SetString(element, "skinId", entry.SkinId);
            SetObject(element, "prefab", entry.Prefab);
        }
    }

    private static bool HasExistingDatabaseContent(CharacterAppearanceDatabase database)
    {
        SerializedObject serializedDatabase = new SerializedObject(database);
        return HasArrayContent(serializedDatabase, "bodyTypeDefinitions")
            || HasArrayContent(serializedDatabase, "bodyDefinitions")
            || HasArrayContent(serializedDatabase, "hairDefinitions")
            || HasArrayContent(serializedDatabase, "faceDefinitions")
            || HasArrayContent(serializedDatabase, "upperDefinitions")
            || HasArrayContent(serializedDatabase, "pantsDefinitions")
            || HasArrayContent(serializedDatabase, "shoesDefinitions");
    }

    private static bool HasArrayContent(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        return property != null && property.isArray && property.arraySize > 0;
    }

    private static SerializedProperty RequireProperty(SerializedObject serializedObject, string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
        {
            throw new InvalidOperationException("CharacterAppearanceDatabase is missing serialized field '" + propertyName + "'.");
        }

        return property;
    }

    private static SerializedProperty AddElement(SerializedProperty array)
    {
        int index = array.arraySize;
        array.arraySize++;
        return array.GetArrayElementAtIndex(index);
    }

    private static void ClearArray(SerializedProperty array)
    {
        array.ClearArray();
    }

    private static void SetString(SerializedProperty parent, string childName, string value)
    {
        SerializedProperty child = parent.FindPropertyRelative(childName);
        if (child == null)
        {
            throw new InvalidOperationException("Missing serialized child field '" + childName + "'.");
        }

        child.stringValue = value ?? string.Empty;
    }

    private static void SetEnum(SerializedProperty parent, string childName, CharacterAppearanceCategory value)
    {
        SerializedProperty child = parent.FindPropertyRelative(childName);
        if (child == null)
        {
            throw new InvalidOperationException("Missing serialized child field '" + childName + "'.");
        }

        child.enumValueIndex = (int)value;
    }

    private static void SetObject(SerializedProperty parent, string childName, UnityEngine.Object value)
    {
        SerializedProperty child = parent.FindPropertyRelative(childName);
        if (child == null)
        {
            throw new InvalidOperationException("Missing serialized child field '" + childName + "'.");
        }

        child.objectReferenceValue = value;
    }

    private static string GetDefaultSkinId(BodyTypeBuild build)
    {
        for (int i = 0; i < build.BodyEntries.Count; i++)
        {
            if (string.Equals(build.BodyEntries[i].SkinId, "skin_01", StringComparison.Ordinal))
            {
                return build.BodyEntries[i].SkinId;
            }
        }

        return build.BodyEntries.Count > 0 ? build.BodyEntries[0].SkinId : string.Empty;
    }

    private static string GetDefaultPartId(List<PartEntry> entries, string preferredSuffix)
    {
        if (!string.IsNullOrEmpty(preferredSuffix))
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Id.EndsWith("_" + preferredSuffix, StringComparison.Ordinal))
                {
                    return entries[i].Id;
                }
            }
        }

        return entries.Count > 0 ? entries[0].Id : string.Empty;
    }

    private static int CompareBodyEntries(BodyEntry left, BodyEntry right)
    {
        return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
    }

    private static void SortPartEntries(List<PartEntry> entries)
    {
        entries.Sort(ComparePartEntries);
    }

    private static int ComparePartEntries(PartEntry left, PartEntry right)
    {
        return string.Compare(left.Id, right.Id, StringComparison.Ordinal);
    }

    private static string CreateReportText(BuildReport report, CharacterAppearanceDatabase database)
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("[CharacterAppearanceDatabaseBuilder] Build report for " + AssetDatabase.GetAssetPath(database));
        builder.AppendLine("BodyTypes: " + report.BodyTypeCount);
        builder.AppendLine("Body/Skin: " + report.BodySkinCount);
        builder.AppendLine("Hair: " + report.HairCount);
        builder.AppendLine("Face: " + report.FaceCount);
        builder.AppendLine("Upper: " + report.UpperCount);
        builder.AppendLine("Pants: " + report.PantsCount);
        builder.AppendLine("Shoes: " + report.ShoesCount);
        AppendList(builder, "Structure", report.StructureNotes);
        AppendList(builder, "Face/Skin", report.FaceSkinNotes);
        AppendList(builder, "Skipped prefabs", report.SkippedPrefabs);
        AppendList(builder, "Ambiguous prefabs", report.AmbiguousPrefabs);
        AppendList(builder, "Duplicate IDs", report.DuplicateIds);
        AppendList(builder, "Missing defaults", report.MissingDefaults);
        return builder.ToString();
    }

    private static void AppendList(StringBuilder builder, string title, List<string> values)
    {
        builder.AppendLine(title + ": " + values.Count);
        for (int i = 0; i < values.Count; i++)
        {
            builder.AppendLine("  - " + values[i]);
        }
    }

    private static string GetConfigStatus(string assetPath)
    {
        UnityEngine.Object config = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
        return config == null ? "missing " + assetPath : "found " + assetPath;
    }

    private static string NormalizeNumber(string value)
    {
        int parsed;
        if (int.TryParse(value, out parsed))
        {
            return parsed.ToString("00");
        }

        return Slugify(value);
    }

    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string lower = value.Trim().ToLowerInvariant();
        string slug = Regex.Replace(lower, "[^a-z0-9]+", "_");
        slug = Regex.Replace(slug, "_+", "_");
        return slug.Trim('_');
    }

    private static string ToDisplayText(string slug)
    {
        if (string.IsNullOrEmpty(slug))
        {
            return string.Empty;
        }

        string[] parts = slug.Split('_');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 0)
            {
                continue;
            }

            parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
        }

        return string.Join(" ", parts);
    }
}
