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
