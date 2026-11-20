using System;

namespace CL.Core.Helpers
{
    public static class StoragePathHelper
    {
        public static string? TryGetLocalPathFromUri(Uri? uri)
        {
            if (uri == null || !uri.IsAbsoluteUri)
                return null;

            if (!uri.IsFile)
                return null;

            try
            {
                string localPath = uri.LocalPath;
                return string.IsNullOrWhiteSpace(localPath) ? null : localPath;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}
