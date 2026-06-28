using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterDatabase",
    menuName = "Game/Characters/Character Database")]
public sealed class CharacterDatabase : ScriptableObject
{
    [Serializable]
    public sealed class CharacterDefinition
    {
        [SerializeField]
        private string characterId = string.Empty;

        [SerializeField]
        private string displayName = string.Empty;

        [SerializeField]
        private GameObject characterPrefab;

        public string CharacterId =>
            NormalizeCharacterId(characterId);

        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName)
                ? CharacterId
                : displayName.Trim();

        public GameObject CharacterPrefab =>
            characterPrefab;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(CharacterId) &&
            characterPrefab != null;
    }

    [SerializeField]
    private List<CharacterDefinition> characters =
        new List<CharacterDefinition>();

    [SerializeField]
    private string defaultCharacterId = string.Empty;

    public int Count =>
        characters != null
            ? characters.Count
            : 0;

    public string DefaultCharacterId =>
        NormalizeCharacterId(defaultCharacterId);

    public bool TryGetDefinitionAt(
        int index,
        out CharacterDefinition definition
    )
    {
        definition = null;

        if (characters == null ||
            index < 0 ||
            index >= characters.Count)
        {
            return false;
        }

        CharacterDefinition candidate =
            characters[index];

        if (candidate == null ||
            !candidate.IsValid)
        {
            return false;
        }

        definition = candidate;
        return true;
    }

    public bool TryGetDefinition(
        string characterId,
        out CharacterDefinition definition
    )
    {
        definition = null;

        string normalizedCharacterId =
            NormalizeCharacterId(characterId);

        if (string.IsNullOrWhiteSpace(
                normalizedCharacterId) ||
            characters == null)
        {
            return false;
        }

        for (int i = 0;
             i < characters.Count;
             i++)
        {
            CharacterDefinition candidate =
                characters[i];

            if (candidate == null ||
                !candidate.IsValid)
            {
                continue;
            }

            if (!string.Equals(
                    candidate.CharacterId,
                    normalizedCharacterId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            if (definition != null)
            {
                Debug.LogError(
                    $"CharacterDatabase enthaelt die characterId " +
                    $"'{normalizedCharacterId}' mehrfach.",
                    this
                );

                definition = null;
                return false;
            }

            definition = candidate;
        }

        return definition != null;
    }

    public bool IsDefaultCharacterIdValid()
    {
        return TryGetDefinition(
            DefaultCharacterId,
            out _);
    }

    public bool ValidateDatabase(
        UnityEngine.Object logContext = null
    )
    {
        UnityEngine.Object context =
            logContext != null
                ? logContext
                : this;

        bool isValid = true;

        if (characters == null ||
            characters.Count == 0)
        {
            Debug.LogError(
                "CharacterDatabase ist ungueltig: " +
                "characters ist leer oder nicht gesetzt.",
                context
            );

            return false;
        }

        HashSet<string> usedIds =
            new HashSet<string>(
                StringComparer.Ordinal
            );

        bool hasValidDefinition = false;

        for (int i = 0;
             i < characters.Count;
             i++)
        {
            CharacterDefinition definition =
                characters[i];

            if (definition == null)
            {
                Debug.LogError(
                    $"CharacterDatabase enthaelt einen leeren " +
                    $"Eintrag an Index {i}.",
                    context
                );

                isValid = false;
                continue;
            }

            string id =
                definition.CharacterId;

            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogError(
                    $"CharacterDatabase-Eintrag {i} hat keine " +
                    "stabile characterId.",
                    context
                );

                isValid = false;
            }
            else if (!usedIds.Add(id))
            {
                Debug.LogError(
                    $"CharacterDatabase enthaelt die characterId " +
                    $"'{id}' mehrfach.",
                    context
                );

                isValid = false;
            }

            if (definition.CharacterPrefab == null)
            {
                Debug.LogError(
                    $"CharacterDatabase-Eintrag {i} mit characterId " +
                    $"'{id}' hat kein Character-Prefab.",
                    context
                );

                isValid = false;
            }

            if (definition.IsValid)
                hasValidDefinition = true;
        }

        if (!hasValidDefinition)
        {
            Debug.LogError(
                "CharacterDatabase enthaelt keine gueltige " +
                "Character-Definition.",
                context
            );

            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(DefaultCharacterId))
        {
            Debug.LogError(
                "CharacterDatabase ist ungueltig: " +
                "defaultCharacterId fehlt.",
                context
            );

            isValid = false;
        }
        else if (!TryGetDefinition(
                     DefaultCharacterId,
                     out _))
        {
            Debug.LogError(
                "CharacterDatabase ist ungueltig: " +
                $"defaultCharacterId '{DefaultCharacterId}' " +
                "kann nicht aufgeloest werden.",
                context
            );

            isValid = false;
        }

        return isValid;
    }

    private static string NormalizeCharacterId(
        string value
    )
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
