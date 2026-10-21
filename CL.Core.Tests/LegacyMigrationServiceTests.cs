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
        public void ImportFromFolder_CopiesAllFilesAndSubdirectories()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "cl_test_data_" + Guid.NewGuid());
            string subDir = Path.Combine(tempDir, "sub");
            Directory.CreateDirectory(subDir);

            try
            {
                File.WriteAllText(Path.Combine(tempDir, "custom1.txt"), "hello");
                File.WriteAllText(Path.Combine(subDir, "custom2.txt"), "world");

                var result = LegacyMigrationService.ImportFromFolder(tempDir);
                Assert.True(result.Success);
                Assert.True(result.FilesImported >= 2);

                string targetData = PlatformPaths.DataFolder();
                Assert.True(File.Exists(Path.Combine(targetData, "custom1.txt")));
                Assert.True(File.Exists(Path.Combine(targetData, "sub", "custom2.txt")));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);

                string targetData = PlatformPaths.DataFolder();
                string testFile1 = Path.Combine(targetData, "custom1.txt");
                string testFile2 = Path.Combine(targetData, "sub", "custom2.txt");
                if (File.Exists(testFile1)) File.Delete(testFile1);
                if (File.Exists(testFile2)) File.Delete(testFile2);
                string testSub = Path.Combine(targetData, "sub");
                if (Directory.Exists(testSub)) Directory.Delete(testSub, true);
            }
        }
    }
}
