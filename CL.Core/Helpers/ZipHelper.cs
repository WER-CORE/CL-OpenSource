using System.IO;
using System.IO.Compression;
using System.Linq;

namespace CL.Core.Helpers
{
    public static class ZipHelper
    {
        public static void ExtractMap(string zipPath, string extractToFolder)
        {
            try
            {
                using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                {
                    var rootFolders = archive.Entries
                        .Where(e => !string.IsNullOrEmpty(e.Name))
                        .Select(e => e.FullName.Split('/')[0])
                        .Distinct()
                        .ToList();

                    bool hasSingleRootFolder = rootFolders.Count == 1;

                    if (hasSingleRootFolder)
                    {
                        archive.ExtractToDirectory(extractToFolder, true);
                    }
                    else
                    {
                        string folderName = Path.GetFileNameWithoutExtension(zipPath);
                        string newMapFolder = Path.Combine(extractToFolder, folderName);

                        Directory.CreateDirectory(newMapFolder);
                        archive.ExtractToDirectory(newMapFolder, true);
                    }
                }

                File.Delete(zipPath);
            }
            catch 
            {
            }
        }
    }
}
