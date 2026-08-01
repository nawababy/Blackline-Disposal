using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class SaveManagerFileRegressionTests
{
    private const string TestRootFolderName = "BlacklineDisposalTests";
    private const string CurrentSlotPreferenceKey = "CurrentSlot";
    private const string RealSaveDirectory =
        @"C:\Users\Pascal Dlutko\AppData\LocalLow\DefaultCompany\My project\Saves";

    private GameObject saveManagerGameObject;
    private GameObject gameManagerGameObject;
    private SaveManager saveManager;
    private string testSaveDirectory;
    private bool hadCurrentSlotPreference;
    private int originalCurrentSlotPreference;

    [SetUp]
    public void SetUp()
    {
        hadCurrentSlotPreference =
            PlayerPrefs.HasKey(CurrentSlotPreferenceKey);

        originalCurrentSlotPreference =
            PlayerPrefs.GetInt(CurrentSlotPreferenceKey, -1);

        testSaveDirectory = Path.Combine(
            Path.GetTempPath(),
            TestRootFolderName,
            Guid.NewGuid().ToString("N")
        );

        Directory.CreateDirectory(testSaveDirectory);

        SaveManager.SetSaveDirectoryPathOverrideForTests(
            testSaveDirectory
        );

        if (SaveManager.Instance != null)
        {
            Assert.Fail(
                "SaveManager.Instance already exists before test setup. " +
                "The test will not destroy an existing singleton."
            );
        }

        saveManagerGameObject =
            new GameObject("SaveManagerFileRegressionTests_SaveManager");

        saveManager =
            saveManagerGameObject.AddComponent<SaveManager>();
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (gameManagerGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    gameManagerGameObject
                );
            }

            if (saveManagerGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    saveManagerGameObject
                );
            }
        }
        finally
        {
            gameManagerGameObject = null;
            saveManager = null;
            saveManagerGameObject = null;

            SaveManager.ClearSaveDirectoryPathOverrideForTests();

            RestoreCurrentSlotPreference();

            DeleteTemporaryTestDirectory();
        }
    }

    [Test]
    public void SaveDirectoryPath_UsesEditorTestOverride()
    {
        string normalSaveDirectory =
            Path.Combine(
                Application.persistentDataPath,
                "Saves"
            );

        Assert.That(
            saveManager.SaveDirectoryPath,
            Is.EqualTo(testSaveDirectory)
        );

        Assert.That(
            saveManager.SaveDirectoryPath,
            Is.Not.EqualTo(normalSaveDirectory)
        );

        Assert.That(
            Directory.EnumerateFileSystemEntries(testSaveDirectory),
            Is.Empty
        );
    }

    [Test]
    public void GetSaveSlotStatus_EmptyTemporaryDirectory_ReturnsEmpty()
    {
        Assert.That(
            Directory.EnumerateFileSystemEntries(testSaveDirectory),
            Is.Empty
        );

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Empty)
        );

        Assert.That(status.CanLoad, Is.False);
        Assert.That(status.CanDelete, Is.False);
    }

    [Test]
    public void CreateNewSave_WritesValidMainFileOnlyInsideTemporaryDirectory()
    {
        bool wasCreated =
            saveManager.CreateNewSave(
                0,
                "Test Save",
                false
            );

        Assert.That(wasCreated, Is.True);

        string expectedSaveFilePath =
            Path.Combine(
                testSaveDirectory,
                "save_slot_0.json"
            );

        Assert.That(
            File.Exists(expectedSaveFilePath),
            Is.True
        );

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Valid)
        );

        Assert.That(status.CanLoad, Is.True);
        Assert.That(status.CanDelete, Is.True);
        Assert.That(status.DisplayName, Is.EqualTo("Test Save"));

        Assert.That(
            File.Exists(expectedSaveFilePath + ".write.tmp"),
            Is.False
        );

        AssertAllGeneratedFilesStayInsideTestDirectory();
    }

    [Test]
    public void GetSaveSlotStatus_CorruptedMainOnly_ReturnsCorrupted()
    {
        WriteInvalidSaveCandidate(
            GetMainSavePath(0)
        );

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Corrupted)
        );

        Assert.That(status.CanLoad, Is.False);
        Assert.That(status.CanDelete, Is.True);
    }

    [Test]
    public void GetSaveSlotStatus_ValidBackupWithoutMain_ReturnsRecoverable()
    {
        string mainSavePath =
            CreateValidMainSave(0, "Backup Recovery Save");

        string backupSavePath =
            GetBackupSavePath(0);

        CopyInsideTestDirectory(
            mainSavePath,
            backupSavePath
        );

        DeleteInsideTestDirectory(mainSavePath);

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Recoverable)
        );

        Assert.That(status.CanLoad, Is.True);
        Assert.That(status.CanDelete, Is.True);
        Assert.That(
            NormalizeDirectoryPath(status.SourcePath),
            Is.EqualTo(NormalizeDirectoryPath(backupSavePath))
        );
    }

    [Test]
    public void GetSaveSlotStatus_ValidBackupWithCorruptedMain_ReturnsRecoverable()
    {
        string mainSavePath =
            CreateValidMainSave(0, "Backup With Corrupted Main");

        string backupSavePath =
            GetBackupSavePath(0);

        CopyInsideTestDirectory(
            mainSavePath,
            backupSavePath
        );

        WriteInvalidSaveCandidate(mainSavePath);

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Recoverable)
        );

        Assert.That(status.CanLoad, Is.True);
        Assert.That(status.CanDelete, Is.True);
        Assert.That(
            NormalizeDirectoryPath(status.SourcePath),
            Is.EqualTo(NormalizeDirectoryPath(backupSavePath))
        );
    }

    [Test]
    public void GetSaveSlotStatus_ValidTemporaryWithoutMain_ReturnsRecoverable()
    {
        string mainSavePath =
            CreateValidMainSave(0, "Temporary Recovery Save");

        string temporarySavePath =
            GetTemporarySavePath(0);

        CopyInsideTestDirectory(
            mainSavePath,
            temporarySavePath
        );

        DeleteInsideTestDirectory(mainSavePath);

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Recoverable)
        );

        Assert.That(status.CanLoad, Is.True);
        Assert.That(status.CanDelete, Is.True);
        Assert.That(
            NormalizeDirectoryPath(status.SourcePath),
            Is.EqualTo(NormalizeDirectoryPath(temporarySavePath))
        );
    }

    [Test]
    public void GetSaveSlotStatus_ValidWriteTemporaryWithoutMain_ReturnsRecoverable()
    {
        string mainSavePath =
            CreateValidMainSave(0, "Write Temporary Recovery Save");

        string writeTemporarySavePath =
            GetWriteTemporarySavePath(0);

        CopyInsideTestDirectory(
            mainSavePath,
            writeTemporarySavePath
        );

        DeleteInsideTestDirectory(mainSavePath);

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Recoverable)
        );

        Assert.That(status.CanLoad, Is.True);
        Assert.That(status.CanDelete, Is.True);
        Assert.That(
            NormalizeDirectoryPath(status.SourcePath),
            Is.EqualTo(NormalizeDirectoryPath(writeTemporarySavePath))
        );
    }

    [Test]
    public void DeleteSave_RemovesAllSlotCandidateFiles()
    {
        string mainSavePath =
            CreateValidMainSave(0, "Delete Candidates Save");

        string backupSavePath =
            GetBackupSavePath(0);

        string temporarySavePath =
            GetTemporarySavePath(0);

        string writeTemporarySavePath =
            GetWriteTemporarySavePath(0);

        CopyInsideTestDirectory(
            mainSavePath,
            backupSavePath
        );

        CopyInsideTestDirectory(
            mainSavePath,
            temporarySavePath
        );

        CopyInsideTestDirectory(
            mainSavePath,
            writeTemporarySavePath
        );

        Assert.That(File.Exists(mainSavePath), Is.True);
        Assert.That(File.Exists(backupSavePath), Is.True);
        Assert.That(File.Exists(temporarySavePath), Is.True);
        Assert.That(File.Exists(writeTemporarySavePath), Is.True);

        bool wasDeleted =
            saveManager.DeleteSave(0);

        Assert.That(wasDeleted, Is.True);
        Assert.That(File.Exists(mainSavePath), Is.False);
        Assert.That(File.Exists(backupSavePath), Is.False);
        Assert.That(File.Exists(temporarySavePath), Is.False);
        Assert.That(File.Exists(writeTemporarySavePath), Is.False);

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Empty)
        );

        Assert.That(status.CanLoad, Is.False);
        Assert.That(status.CanDelete, Is.False);
    }

    [Test]
    public void GetSaveSlotStatus_AllCandidatesInvalid_ReturnsCorrupted()
    {
        WriteInvalidSaveCandidate(
            GetMainSavePath(0)
        );

        WriteInvalidSaveCandidate(
            GetBackupSavePath(0)
        );

        WriteInvalidSaveCandidate(
            GetTemporarySavePath(0)
        );

        WriteInvalidSaveCandidate(
            GetWriteTemporarySavePath(0)
        );

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Corrupted)
        );

        Assert.That(
            status.State,
            Is.Not.EqualTo(SaveSlotState.Recoverable)
        );

        Assert.That(status.CanLoad, Is.False);
        Assert.That(status.CanDelete, Is.True);
    }

    [Test]
    public void GetSaveSlotStatus_CompleteVersion4Save_ReturnsValid()
    {
        WriteSaveDataCandidate(
            GetMainSavePath(0),
            SaveGameData.CreateNew(0, "Complete Version 4 Save")
        );

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Valid)
        );
    }

    [Test]
    public void GetSaveSlotStatus_HeaderOnlyVersion4Save_ReturnsCorrupted()
    {
        WriteJsonCandidate(
            GetMainSavePath(0),
            "{\"saveVersion\":4,\"slotIndex\":0}"
        );

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Corrupted)
        );
    }

    [Test]
    public void GetSaveSlotStatus_Version3WithoutVersion4Field_RemainsValid()
    {
        string version3Json =
            CreateVersion4JsonWithOmittedField(
                "sharedWorld.facilitiesSnapshotInitialized"
            ).Replace(
                "\"saveVersion\":4",
                "\"saveVersion\":3"
            );

        WriteJsonCandidate(
            GetMainSavePath(0),
            version3Json
        );

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Valid)
        );
    }

    [TestCase("saveName")]
    [TestCase("createdUtc")]
    [TestCase("lastSavedUtc")]
    [TestCase("player")]
    [TestCase("sharedWorld")]
    [TestCase("player.personalCash")]
    [TestCase("player.position")]
    [TestCase("player.position.x")]
    [TestCase("player.position.y")]
    [TestCase("player.position.z")]
    [TestCase("player.rotation")]
    [TestCase("player.rotation.x")]
    [TestCase("player.rotation.y")]
    [TestCase("player.rotation.z")]
    [TestCase("player.selectedHotbarSlot")]
    [TestCase("player.hotbarSlots")]
    [TestCase("player.characterId")]
    [TestCase("player.appearance")]
    [TestCase("sharedWorld.sharedBankBalance")]
    [TestCase("sharedWorld.worldTrashSnapshotInitialized")]
    [TestCase("sharedWorld.worldTrashObjects")]
    [TestCase("sharedWorld.facilitiesSnapshotInitialized")]
    [TestCase("sharedWorld.facilities")]
    public void GetSaveSlotStatus_Version4MissingRequiredField_ReturnsCorrupted(
        string omittedField
    )
    {
        WriteJsonCandidate(
            GetMainSavePath(0),
            CreateVersion4JsonWithOmittedField(omittedField)
        );

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Corrupted),
            omittedField
        );
    }

    [TestCase(0)]
    [TestCase(5)]
    public void GetSaveSlotStatus_UnsupportedVersion_ReturnsCorrupted(
        int saveVersion
    )
    {
        SaveGameData saveData =
            SaveGameData.CreateNew(0, "Invalid Version Save");

        saveData.saveVersion = saveVersion;

        WriteSaveDataCandidate(
            GetMainSavePath(0),
            saveData
        );

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Corrupted)
        );
    }

    [Test]
    public void GetSaveSlotStatus_MismatchedSlotIndex_ReturnsCorrupted()
    {
        WriteSaveDataCandidate(
            GetMainSavePath(0),
            SaveGameData.CreateNew(1, "Wrong Slot Save")
        );

        SaveSlotStatus status =
            saveManager.GetSaveSlotStatus(0);

        Assert.That(
            status.State,
            Is.EqualTo(SaveSlotState.Corrupted)
        );
    }

    [Test]
    public void StartNewGame_MissingSaveManager_DoesNotContinueFlow()
    {
        DestroySaveManagerForTest();

        GameManager gameManager =
            CreateGameManagerForTest();

        int initialSlot = gameManager.CurrentSlot;
        int initialSceneHandle =
            SceneManager.GetActiveScene().handle;

        LogAssert.Expect(
            LogType.Error,
            "Neues Spiel fuer Slot 1 kann nicht gestartet werden, " +
            "weil kein SaveManager verfuegbar ist."
        );

        gameManager.StartNewGame(0);

        Assert.That(gameManager.CurrentSlot, Is.EqualTo(initialSlot));
        Assert.That(
            SceneManager.GetActiveScene().handle,
            Is.EqualTo(initialSceneHandle)
        );
    }

    [Test]
    public void ContinueGame_MissingSaveManager_DoesNotContinueFlow()
    {
        DestroySaveManagerForTest();

        GameManager gameManager =
            CreateGameManagerForTest();

        int initialSlot = gameManager.CurrentSlot;
        int initialSceneHandle =
            SceneManager.GetActiveScene().handle;

        LogAssert.Expect(
            LogType.Error,
            "Spielstand 1 kann nicht fortgesetzt werden, " +
            "weil kein SaveManager verfuegbar ist."
        );

        gameManager.ContinueGame(0);

        Assert.That(gameManager.CurrentSlot, Is.EqualTo(initialSlot));
        Assert.That(
            SceneManager.GetActiveScene().handle,
            Is.EqualTo(initialSceneHandle)
        );
    }

    [Test]
    public void MigrateLegacySave_CreateNewSaveFailure_ReturnsFalse()
    {
        CreateValidMainSave(0, "Existing Save");

        GameManager gameManager =
            CreateGameManagerForTest();

        MethodInfo migrateLegacySave =
            typeof(GameManager).GetMethod(
                "MigrateLegacySave",
                BindingFlags.Instance |
                BindingFlags.NonPublic
            );

        Assert.That(migrateLegacySave, Is.Not.Null);

        LogAssert.Expect(
            LogType.Error,
            "Legacy-Spielstand fuer Slot 1 kann nicht migriert " +
            "werden, weil kein SaveManager verfuegbar ist."
        );

        object result =
            migrateLegacySave.Invoke(
                gameManager,
                new object[] { 0 }
            );

        Assert.That(result, Is.EqualTo(false));
    }

    private static string CreateVersion4JsonWithOmittedField(
        string omittedField
    )
    {
        List<string> positionFields =
            new List<string>();

        AddJsonField(
            positionFields,
            "player.position.x",
            "x",
            "0",
            omittedField
        );

        AddJsonField(
            positionFields,
            "player.position.y",
            "y",
            "0",
            omittedField
        );

        AddJsonField(
            positionFields,
            "player.position.z",
            "z",
            "0",
            omittedField
        );

        List<string> rotationFields =
            new List<string>();

        AddJsonField(
            rotationFields,
            "player.rotation.x",
            "x",
            "0",
            omittedField
        );

        AddJsonField(
            rotationFields,
            "player.rotation.y",
            "y",
            "0",
            omittedField
        );

        AddJsonField(
            rotationFields,
            "player.rotation.z",
            "z",
            "0",
            omittedField
        );

        List<string> playerFields =
            new List<string>();

        AddJsonField(
            playerFields,
            "player.personalCash",
            "personalCash",
            "0",
            omittedField
        );

        AddJsonField(
            playerFields,
            "player.position",
            "position",
            BuildJsonObject(positionFields),
            omittedField
        );

        AddJsonField(
            playerFields,
            "player.rotation",
            "rotation",
            BuildJsonObject(rotationFields),
            omittedField
        );

        AddJsonField(
            playerFields,
            "player.selectedHotbarSlot",
            "selectedHotbarSlot",
            "0",
            omittedField
        );

        AddJsonField(
            playerFields,
            "player.hotbarSlots",
            "hotbarSlots",
            "[]",
            omittedField
        );

        AddJsonField(
            playerFields,
            "player.characterId",
            "characterId",
            "\"\"",
            omittedField
        );

        AddJsonField(
            playerFields,
            "player.appearance",
            "appearance",
            "{}",
            omittedField
        );

        List<string> sharedWorldFields =
            new List<string>();

        AddJsonField(
            sharedWorldFields,
            "sharedWorld.sharedBankBalance",
            "sharedBankBalance",
            "500",
            omittedField
        );

        AddJsonField(
            sharedWorldFields,
            "sharedWorld.worldTrashSnapshotInitialized",
            "worldTrashSnapshotInitialized",
            "false",
            omittedField
        );

        AddJsonField(
            sharedWorldFields,
            "sharedWorld.worldTrashObjects",
            "worldTrashObjects",
            "[]",
            omittedField
        );

        AddJsonField(
            sharedWorldFields,
            "sharedWorld.facilitiesSnapshotInitialized",
            "facilitiesSnapshotInitialized",
            "false",
            omittedField
        );

        AddJsonField(
            sharedWorldFields,
            "sharedWorld.facilities",
            "facilities",
            "[]",
            omittedField
        );

        List<string> rootFields =
            new List<string>
            {
                "\"saveVersion\":4",
                "\"slotIndex\":0"
            };

        AddJsonField(
            rootFields,
            "saveName",
            "saveName",
            "\"Required Field Save\"",
            omittedField
        );

        AddJsonField(
            rootFields,
            "createdUtc",
            "createdUtc",
            "\"2026-08-01T00:00:00.0000000Z\"",
            omittedField
        );

        AddJsonField(
            rootFields,
            "lastSavedUtc",
            "lastSavedUtc",
            "\"2026-08-01T00:00:00.0000000Z\"",
            omittedField
        );

        AddJsonField(
            rootFields,
            "player",
            "player",
            BuildJsonObject(playerFields),
            omittedField
        );

        AddJsonField(
            rootFields,
            "sharedWorld",
            "sharedWorld",
            BuildJsonObject(sharedWorldFields),
            omittedField
        );

        return BuildJsonObject(rootFields);
    }

    private static void AddJsonField(
        List<string> fields,
        string fieldPath,
        string fieldName,
        string rawValue,
        string omittedField
    )
    {
        if (string.Equals(
                fieldPath,
                omittedField,
                StringComparison.Ordinal))
        {
            return;
        }

        fields.Add(
            "\"" + fieldName + "\":" + rawValue
        );
    }

    private static string BuildJsonObject(
        List<string> fields
    )
    {
        return "{" +
               string.Join(",", fields) +
               "}";
    }

    private void WriteSaveDataCandidate(
        string path,
        SaveGameData saveData
    )
    {
        WriteJsonCandidate(
            path,
            JsonUtility.ToJson(saveData, true)
        );
    }

    private void WriteJsonCandidate(
        string path,
        string json
    )
    {
        AssertPathInsideTestDirectory(path);

        File.WriteAllText(
            path,
            json
        );
    }

    private void DestroySaveManagerForTest()
    {
        if (saveManagerGameObject != null)
        {
            UnityEngine.Object.DestroyImmediate(
                saveManagerGameObject
            );
        }

        saveManager = null;
        saveManagerGameObject = null;

        Assert.That(SaveManager.Instance, Is.Null);
    }

    private GameManager CreateGameManagerForTest()
    {
        Assert.That(
            GameManager.Instance,
            Is.Null,
            "GameManager.Instance already exists before test setup."
        );

        gameManagerGameObject =
            new GameObject(
                "SaveManagerFileRegressionTests_GameManager"
            );

        return gameManagerGameObject.AddComponent<GameManager>();
    }

    private void RestoreCurrentSlotPreference()
    {
        if (hadCurrentSlotPreference)
        {
            PlayerPrefs.SetInt(
                CurrentSlotPreferenceKey,
                originalCurrentSlotPreference
            );
        }
        else
        {
            PlayerPrefs.DeleteKey(
                CurrentSlotPreferenceKey
            );
        }

        PlayerPrefs.Save();
    }

    private string CreateValidMainSave(
        int slotIndex,
        string displayName
    )
    {
        bool wasCreated =
            saveManager.CreateNewSave(
                slotIndex,
                displayName,
                false
            );

        Assert.That(wasCreated, Is.True);

        string mainSavePath =
            GetMainSavePath(slotIndex);

        Assert.That(
            File.Exists(mainSavePath),
            Is.True
        );

        return mainSavePath;
    }

    private string GetMainSavePath(
        int slotIndex
    )
    {
        return Path.Combine(
            testSaveDirectory,
            "save_slot_" + slotIndex + ".json"
        );
    }

    private string GetBackupSavePath(
        int slotIndex
    )
    {
        return GetMainSavePath(slotIndex) +
               ".bak";
    }

    private string GetTemporarySavePath(
        int slotIndex
    )
    {
        return GetMainSavePath(slotIndex) +
               ".tmp";
    }

    private string GetWriteTemporarySavePath(
        int slotIndex
    )
    {
        return GetMainSavePath(slotIndex) +
               ".write.tmp";
    }

    private void CopyInsideTestDirectory(
        string sourcePath,
        string targetPath
    )
    {
        AssertPathInsideTestDirectory(sourcePath);
        AssertPathInsideTestDirectory(targetPath);

        File.Copy(
            sourcePath,
            targetPath,
            true
        );
    }

    private void WriteInvalidSaveCandidate(
        string path
    )
    {
        AssertPathInsideTestDirectory(path);

        File.WriteAllText(
            path,
            "{ \"saveVersion\": 4, \"slotIndex\": 0,"
        );
    }

    private void DeleteInsideTestDirectory(
        string path
    )
    {
        AssertPathInsideTestDirectory(path);

        if (File.Exists(path))
            File.Delete(path);
    }

    private void AssertAllGeneratedFilesStayInsideTestDirectory()
    {
        string normalizedTestDirectory =
            NormalizeDirectoryPath(testSaveDirectory);

        foreach (string path in Directory.EnumerateFileSystemEntries(
                     testSaveDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            string normalizedPath =
                Path.GetFullPath(path);

            Assert.That(
                normalizedPath.StartsWith(
                    normalizedTestDirectory,
                    StringComparison.OrdinalIgnoreCase
                ),
                Is.True,
                "A generated save test file was outside the temporary test directory."
            );
        }
    }

    private void AssertPathInsideTestDirectory(
        string path
    )
    {
        Assert.That(
            string.IsNullOrWhiteSpace(path),
            Is.False,
            "Path must not be empty."
        );

        string normalizedTestDirectory =
            NormalizeDirectoryPath(testSaveDirectory);

        string normalizedPath =
            Path.GetFullPath(path);

        string testDirectoryPrefix =
            normalizedTestDirectory +
            Path.DirectorySeparatorChar;

        Assert.That(
            normalizedPath.StartsWith(
                testDirectoryPrefix,
                StringComparison.OrdinalIgnoreCase
            ),
            Is.True,
            "Path must stay inside the isolated SaveManager test directory."
        );
    }

    private void DeleteTemporaryTestDirectory()
    {
        if (string.IsNullOrWhiteSpace(testSaveDirectory))
            return;

        if (!Directory.Exists(testSaveDirectory))
            return;

        Assert.That(
            IsSafeTemporaryTestDirectory(testSaveDirectory),
            Is.True,
            "Refusing to delete a path that is not the isolated test directory."
        );

        Directory.Delete(
            testSaveDirectory,
            true
        );
    }

    private static bool IsSafeTemporaryTestDirectory(
        string path
    )
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        string fullPath =
            NormalizeDirectoryPath(path);

        string tempPath =
            NormalizeDirectoryPath(Path.GetTempPath());

        string persistentDataPath =
            NormalizeDirectoryPath(Application.persistentDataPath);

        string realSaveDirectory =
            NormalizeDirectoryPath(RealSaveDirectory);

        return fullPath.StartsWith(
                   tempPath,
                   StringComparison.OrdinalIgnoreCase
               ) &&
               fullPath.IndexOf(
                   TestRootFolderName,
                   StringComparison.OrdinalIgnoreCase
               ) >= 0 &&
               !PathsMatch(fullPath, persistentDataPath) &&
               !PathsMatch(fullPath, realSaveDirectory);
    }

    private static bool PathsMatch(
        string firstPath,
        string secondPath
    )
    {
        return string.Equals(
            firstPath,
            secondPath,
            StringComparison.OrdinalIgnoreCase
        );
    }

    private static string NormalizeDirectoryPath(
        string path
    )
    {
        string fullPath =
            Path.GetFullPath(path);

        return fullPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar
        );
    }
}
