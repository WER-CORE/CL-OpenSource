using System.IO;
using System.Threading.Tasks;

namespace CL.Core.Helpers
{
    public static class DirectoryHelper
    {
        public static void CopyDirectoryParallel(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            Directory.CreateDirectory(destinationDir);

            Parallel.ForEach(dir.GetFiles(), (file) =>
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            });

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectoryParallel(subDir.FullName, newDestinationDir);
            }
        }
    }
}
