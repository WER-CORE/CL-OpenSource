using System;
using System.IO;
using CL.Core.Services;
using Xunit;

namespace CL.Core.Tests
{
    public class LegacyMigrationServiceTests
    {
        [Fact]
        public void ImportFromFolder_NonExistentFolder_ReturnsFailure()
        {
            string nonExistent = Path.Combine(Path.GetTempPath(), "cl_test_non_existent_" + Guid.NewGuid());
            var result = LegacyMigrationService.ImportFromFolder(nonExistent);

            Assert.False(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        }

        [Fact]
        public void ImportFromFolder_EmptyFolder_ReturnsFailure()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "cl_test_empty_" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);

            try
            {
                var result = LegacyMigrationService.ImportFromFolder(tempDir);
                Assert.False(result.Success);
                Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ImportFromFolder_CurrentDataFolder_ReturnsFailure()
        {
            string dataFolder = PlatformPaths.DataFolder();
            if (!Directory.Exists(dataFolder))
                Directory.CreateDirectory(dataFolder);

            var result = LegacyMigrationService.ImportFromFolder(dataFolder);
            Assert.False(result.Success);
            Assert.Contains("поточною папкою Data", result.ErrorMessage);
        }

        [Fact]
        public void Deserialization_LegacyUserSaves_Test()
        {
            string path = @"C:\Users\Vlad\Desktop\CL_Launcher_v1.0.0.5\Data\UserSaves.json";
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                var settings = Newtonsoft.Json.JsonConvert.DeserializeObject<CL.Core.Models.AppSettings>(json);
                Assert.NotNull(settings);
                Assert.Equal(1280, settings.width);
                Assert.Equal(720, settings.height);
                Assert.Equal("Forest", settings.Them);
                Assert.Equal(1.5, settings.UIScale);
            }
        }

        [Fact]
        public void ImportFromFolder_CopiesAllFilesAndSubdirectories()
        {
            string runId = Guid.NewGuid().ToString("N");
            string fileName1 = $"custom1_{runId}.txt";
            string fileName2 = $"custom2_{runId}.txt";

            string tempDir = Path.Combine(Path.GetTempPath(), "cl_test_data_" + runId);
            string subDir = Path.Combine(tempDir, "sub_" + runId);
            Directory.CreateDirectory(subDir);

            try
            {
                File.WriteAllText(Path.Combine(tempDir, fileName1), "hello");
                File.WriteAllText(Path.Combine(subDir, fileName2), "world");

                var result = LegacyMigrationService.ImportFromFolder(tempDir);
                Assert.True(result.Success);
                Assert.True(result.FilesImported >= 2);

                string targetData = PlatformPaths.DataFolder();
                Assert.True(File.Exists(Path.Combine(targetData, fileName1)));
                Assert.True(File.Exists(Path.Combine(targetData, "sub_" + runId, fileName2)));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);

                string targetData = PlatformPaths.DataFolder();
                string testFile1 = Path.Combine(targetData, fileName1);
                string testFile2 = Path.Combine(targetData, "sub_" + runId, fileName2);
                if (File.Exists(testFile1)) File.Delete(testFile1);
                if (File.Exists(testFile2)) File.Delete(testFile2);
                string testSub = Path.Combine(targetData, "sub_" + runId);
                if (Directory.Exists(testSub)) Directory.Delete(testSub, true);
            }
        }
    
        [Fact]
        public void ImportFromFolder_UserSaves_ImportedAndPreserved()
        {
            string runId = Guid.NewGuid().ToString("N");
            string sourceDir = Path.Combine(Path.GetTempPath(), "cl_test_legacy_source_" + runId);
            string targetDir = Path.Combine(Path.GetTempPath(), "cl_test_legacy_target_" + runId);
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);

            string? originalFolderPath = SettingsManager.FolderPath;
            var originalDefault = SettingsManager.Default;

            try
            {
                SettingsManager.FolderPath = targetDir;
                SettingsManager.Default = new CL.Core.Models.AppSettings();

                string legacyJson = @"{
  ""width"": 1280,
  ""height"": 720,
  ""FullScreen"": true,
  ""OP"": 2699,
  ""Them"": ""Forest"",
  ""PathLacunher"": ""G:\\.ClMinecraft"",
  ""EncryptKey"": """",
  ""TutorialComplete"": true,
  ""StatsGameVanila"": 2.5,
  ""StatsGameMod"": 63.2,
  ""UIScale"": 1.5
}";
                File.WriteAllText(Path.Combine(sourceDir, "UserSaves.json"), legacyJson);

                var result = LegacyMigrationService.ImportFromFolder(sourceDir, targetDir);
                Assert.True(result.Success);
                Assert.True(result.SettingsImported);

                Assert.Equal(1280, SettingsManager.Default.width);
                Assert.Equal(720, SettingsManager.Default.height);
                Assert.True(SettingsManager.Default.FullScreen);
                Assert.Equal(2699, SettingsManager.Default.OP);
                Assert.Equal("Forest", SettingsManager.Default.Them);
                Assert.Equal("G:\\.ClMinecraft", SettingsManager.Default.PathLacunher);
                Assert.Equal(1.5, SettingsManager.Default.UIScale);
                Assert.False(string.IsNullOrWhiteSpace(SettingsManager.Default.EncryptKey));

                string targetSettingsFile = Path.Combine(targetDir, "UserSaves.json");
                Assert.True(File.Exists(targetSettingsFile));
                string savedOnDisk = File.ReadAllText(targetSettingsFile);
                var reloadedFromDisk = Newtonsoft.Json.JsonConvert.DeserializeObject<CL.Core.Models.AppSettings>(savedOnDisk);
                Assert.NotNull(reloadedFromDisk);
                Assert.Equal(1280, reloadedFromDisk.width);
                Assert.Equal(720, reloadedFromDisk.height);
                Assert.Equal("Forest", reloadedFromDisk.Them);
                Assert.Equal(1.5, reloadedFromDisk.UIScale);
                Assert.False(string.IsNullOrWhiteSpace(reloadedFromDisk.EncryptKey));
            }
            finally
            {
                SettingsManager.FolderPath = originalFolderPath;
                SettingsManager.Default = originalDefault;

                if (Directory.Exists(sourceDir))
                    Directory.Delete(sourceDir, true);

                if (Directory.Exists(targetDir))
                    Directory.Delete(targetDir, true);
            }
        }
    
        [Fact]
        public void ImportFromFolder_ActualDesktopFolder_ImportsCorrectly()
        {
            string desktopData = @"C:\Users\Vlad\Desktop\CL_Launcher_v1.0.0.5\Data";
            if (!Directory.Exists(desktopData)) return;

            string tempTarget = Path.Combine(Path.GetTempPath(), "cl_test_desktop_import_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempTarget);

            string? originalFolderPath = SettingsManager.FolderPath;
            var originalDefault = SettingsManager.Default;

            try
            {
                SettingsManager.FolderPath = tempTarget;
                SettingsManager.Default = new CL.Core.Models.AppSettings();

                var result = LegacyMigrationService.ImportFromFolder(desktopData, tempTarget);
                Assert.True(result.Success);
                Assert.True(result.SettingsImported);

                Assert.Equal(1280, SettingsManager.Default.width);
                Assert.Equal(720, SettingsManager.Default.height);
                Assert.Equal("Forest", SettingsManager.Default.Them);
                Assert.Equal(1.5, SettingsManager.Default.UIScale);
                Assert.Equal("G:\\.ClMinecraft", SettingsManager.Default.PathLacunher);
                Assert.True(SettingsManager.Default.FullScreen);
            }
            finally
            {
                SettingsManager.FolderPath = originalFolderPath;
                SettingsManager.Default = originalDefault;

                if (Directory.Exists(tempTarget))
                    Directory.Delete(tempTarget, true);
            }
        }
    }
}