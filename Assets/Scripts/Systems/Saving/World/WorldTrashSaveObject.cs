using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Trash))]
public sealed class WorldTrashSaveObject : MonoBehaviour
{
    [Header("World Save Identification")]
    [Tooltip(
        "Eindeutige ID dieses einzelnen Müllobjekts in der Welt. " +
        "Jeder platzierte Müllsack braucht eine andere ID."
    )]
    [SerializeField]
    private string worldObjectId = string.Empty;

    [Header("Saved State")]
    [Tooltip(
        "Speichert Position und Rotation dieses Müllobjekts."
    )]
    [SerializeField]
    private bool saveTransform = true;

    private Trash cachedTrash;

    public string WorldObjectId => worldObjectId;

    public string TrashTypeId
    {
        get
        {
            CacheReferences();

            return cachedTrash != null
                ? cachedTrash.TrashId
                : string.Empty;
        }
    }

    public string SceneName =>
        gameObject.scene.IsValid()
            ? gameObject.scene.name
            : string.Empty;

    public bool HasValidWorldObjectId =>
        !string.IsNullOrWhiteSpace(worldObjectId);

    private void Reset()
    {
        CacheReferences();

        if (string.IsNullOrWhiteSpace(worldObjectId))
            GenerateNewWorldObjectId();
    }

    private void Awake()
    {
        CacheReferences();
        ValidateWorldObjectId();
        CheckForDuplicateWorldObjectId();
    }

    private void OnValidate()
    {
        CacheReferences();

        if (worldObjectId == null)
            worldObjectId = string.Empty;

        worldObjectId = worldObjectId.Trim();
    }

    private void CacheReferences()
    {
        if (cachedTrash == null)
            cachedTrash = GetComponent<Trash>();
    }

    [ContextMenu("Generate New World Object ID")]
    public void GenerateNewWorldObjectId()
    {
        worldObjectId =
            Guid.NewGuid().ToString("N");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);

        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager
                .MarkSceneDirty(gameObject.scene);
        }
#endif
    }

    private void ValidateWorldObjectId()
    {
        if (HasValidWorldObjectId)
            return;

        Debug.LogError(
            $"Das Müllobjekt '{name}' besitzt keine World Object ID. " +
            "Öffne das Komponenten-Menü und wähle " +
            "'Generate New World Object ID'.",
            gameObject
        );
    }

    private void CheckForDuplicateWorldObjectId()
    {
        if (!HasValidWorldObjectId)
            return;

        WorldTrashSaveObject[] allWorldTrash =
            FindObjectsByType<WorldTrashSaveObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        foreach (WorldTrashSaveObject other in allWorldTrash)
        {
            if (other == null || other == this)
                continue;

            if (!string.Equals(
                    other.worldObjectId,
                    worldObjectId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            Debug.LogError(
                $"Doppelte World Object ID gefunden:\n" +
                $"{worldObjectId}\n\n" +
                $"Objekt 1: {name}\n" +
                $"Objekt 2: {other.name}\n\n" +
                "Erzeuge bei einem der beiden Objekte eine neue ID.",
                gameObject
            );

            return;
        }
    }

    public WorldTrashSaveData CreateSaveData(bool exists)
    {
        CacheReferences();

        WorldTrashSaveData saveData =
            new WorldTrashSaveData
            {
                worldObjectId = worldObjectId,
                trashTypeId = TrashTypeId,
                sceneName = SceneName,
                exists = exists,
                position = new SerializableVector3(),
                rotation = new SerializableVector3()
            };

        if (saveTransform)
        {
            saveData.position.Set(transform.position);
            saveData.rotation.Set(transform.eulerAngles);
        }

        return saveData;
    }

    public void ApplySaveData(
        WorldTrashSaveData saveData
    )
    {
        if (saveData == null)
            return;

        if (!saveData.exists)
        {
            RemoveFromWorld();
            return;
        }

        if (!saveTransform)
            return;

        Vector3 savedPosition =
            saveData.position != null
                ? saveData.position.ToVector3()
                : transform.position;

        Vector3 savedRotation =
            saveData.rotation != null
                ? saveData.rotation.ToVector3()
                : transform.eulerAngles;

        Rigidbody trashRigidbody =
            GetComponent<Rigidbody>();

        if (trashRigidbody == null)
        {
            transform.SetPositionAndRotation(
                savedPosition,
                Quaternion.Euler(savedRotation)
            );

            return;
        }

        bool wasKinematic =
            trashRigidbody.isKinematic;

        bool wasUsingGravity =
            trashRigidbody.useGravity;

        trashRigidbody.isKinematic = true;
        trashRigidbody.useGravity = false;

        trashRigidbody.linearVelocity =
            Vector3.zero;

        trashRigidbody.angularVelocity =
            Vector3.zero;

        transform.SetPositionAndRotation(
            savedPosition,
            Quaternion.Euler(savedRotation)
        );

        trashRigidbody.isKinematic =
            wasKinematic;

        trashRigidbody.useGravity =
            wasUsingGravity;
    }

    private void RemoveFromWorld()
    {
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}