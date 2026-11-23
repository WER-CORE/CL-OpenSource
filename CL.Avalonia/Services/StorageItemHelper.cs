using Avalonia.Platform.Storage;
using CL.Core.Helpers;

namespace CL_CLegendary_Launcher_.Avalonia.Services;

public static class StorageItemHelper
{
    public static string? TryGetSafeLocalPath(IStorageItem? item)
    {
        if (item == null)
            return null;

        try
        {
            string? localPath = item.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath))
                return localPath;
        }
        catch
        {
        }

        return StoragePathHelper.TryGetLocalPathFromUri(item.Path);
    }
}
