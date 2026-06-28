using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class SaveGameData
{
    // ==================================================
    // SAVE INFORMATION
    // ==================================================

    public int saveVersion = 2;

    public int slotIndex = -1;

    public string saveName = "New Save";

    public string createdUtc = string.Empty;

    public string lastSavedUtc = string.Empty;

    // ==================================================
    // SAVE SECTIONS
    // ==================================================

    public PlayerSaveData player =
        new PlayerSaveData();

    public SharedWorldSaveData sharedWorld =
        new SharedWorldSaveData();

    // ==================================================
    // CREATION
    // ==================================================

    public static SaveGameData CreateNew(
        int newSlotIndex,
        string newSaveName
    )
    {
        string currentUtcTime =
            DateTime.UtcNow.ToString("O");

        return new SaveGameData
        {
            saveVersion = 2,

            slotIndex =
                newSlotIndex,

            saveName =
                string.IsNullOrWhiteSpace(newSaveName)
                    ? $"Save {newSlotIndex + 1}"
                    : newSaveName.Trim(),

            createdUtc =
                currentUtcTime,

            lastSavedUtc =
                currentUtcTime,

            player =
                new PlayerSaveData(),

            sharedWorld =
                new SharedWorldSaveData()
        };
    }

    public void UpdateLastSavedTime()
    {
        lastSavedUtc =
            DateTime.UtcNow.ToString("O");
    }
}

// ======================================================
// PLAYER SAVE DATA
// ======================================================

[Serializable]
public sealed class PlayerSaveData
{
    // ==================================================
    // MONEY
    // ==================================================

    public int personalCash;

    // ==================================================
    // POSITION
    // ==================================================

    public SerializableVector3 position =
        new SerializableVector3();

    public SerializableVector3 rotation =
        new SerializableVector3();

    // ==================================================
    // HOTBAR
    // ==================================================

    public int selectedHotbarSlot;

    public List<HotbarSlotSaveData> hotbarSlots =
        new List<HotbarSlotSaveData>();

    // ==================================================
    // CHARACTER
    // ==================================================

    public string characterId =
        string.Empty;
}

// ======================================================
// HOTBAR SAVE DATA
// ======================================================

[Serializable]
public sealed class HotbarSlotSaveData
{
    public int slotIndex;

    public string itemId =
        string.Empty;

    public int amount = 1;
}

// ======================================================
// SHARED WORLD SAVE DATA
// ======================================================

[Serializable]
public sealed class SharedWorldSaveData
{
    // ==================================================
    // SHARED BANK
    // ==================================================

    /*
     * Startguthaben eines komplett neuen Spielstands.
     *
     * Nach dem ersten Speichern wird anschlieﬂend immer
     * der tats‰chlich vorhandene Bankbetrag gespeichert.
     */
    public int sharedBankBalance = 500;

    // ==================================================
    // WORLD TRASH
    // ==================================================

    public bool worldTrashSnapshotInitialized;

    public List<WorldTrashSaveData> worldTrashObjects =
        new List<WorldTrashSaveData>();

    // ==================================================
    // FACILITIES
    // ==================================================

    public List<FacilitySaveData> facilities =
        new List<FacilitySaveData>();
}

// ======================================================
// FACILITY SAVE DATA
// ======================================================

[Serializable]
public sealed class FacilitySaveData
{
    public string facilityId =
        string.Empty;

    public int levelIndex;

    public bool isProcessing;

    public float processingTimeRemaining;

    public float processingBaseValue;
}

// ======================================================
// SERIALIZABLE VECTOR3
// ======================================================

[Serializable]
public sealed class SerializableVector3
{
    public float x;
    public float y;
    public float z;

    public SerializableVector3()
    {
    }

    public SerializableVector3(
        float newX,
        float newY,
        float newZ
    )
    {
        x = newX;
        y = newY;
        z = newZ;
    }

    public SerializableVector3(
        Vector3 vector
    )
    {
        Set(vector);
    }

    public void Set(
        Vector3 vector
    )
    {
        x = vector.x;
        y = vector.y;
        z = vector.z;
    }

    public Vector3 ToVector3()
    {
        return new Vector3(
            x,
            y,
            z
        );
    }
}