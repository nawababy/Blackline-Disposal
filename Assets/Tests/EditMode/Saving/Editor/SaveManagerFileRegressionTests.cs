using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class SaveManagerFileRegressionTests
{
    private const string TestRootFolderName = "BlacklineDisposalTests";
    private const string RealSaveDirectory =
        @"C:\Users\Pascal Dlutko\AppData\LocalLow\DefaultCompany\My project\Saves";

    private GameObject saveManagerGameObject;
    private SaveManager saveManager;
    private string testSaveDirectory;

    [SetUp]
    public void SetUp()
    {
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
            if (saveManagerGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    saveManagerGameObject
                );
            }
        }
        finally
        {
            saveManager = null;
            saveManagerGameObject = null;

            SaveManager.ClearSaveDirectoryPathOverrideForTests();

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
