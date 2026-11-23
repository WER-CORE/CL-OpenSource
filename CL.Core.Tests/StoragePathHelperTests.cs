using System;
using CL.Core.Helpers;
using Xunit;

namespace CL.Core.Tests;

public class StoragePathHelperTests
{
    [Fact]
    public void TryGetLocalPathFromUri_NullUri_ReturnsNull_ForCancelledDialog()
    {
        string? result = StoragePathHelper.TryGetLocalPathFromUri(null);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("relative/path/folder")]
    [InlineData("folder")]
    [InlineData("")]
    public void TryGetLocalPathFromUri_RelativeUri_ReturnsNull_WithoutThrowing(string rawUri)
    {
        var relativeUri = new Uri(rawUri, UriKind.Relative);

        string? result = StoragePathHelper.TryGetLocalPathFromUri(relativeUri);

        Assert.Null(result);
    }

    [Theory]
    [InlineData("content://com.android.externalstorage.documents/tree/primary%3ADownload")]
    [InlineData("https://example.com/modpack.mrpack")]
    [InlineData("mtp://phone/Internal%20storage")]
    public void TryGetLocalPathFromUri_NonFileAbsoluteUri_ReturnsNull_AndDoesNotReturnOriginalString(string rawUri)
    {
        var uri = new Uri(rawUri, UriKind.Absolute);

        string? result = StoragePathHelper.TryGetLocalPathFromUri(uri);

        Assert.Null(result);
    }

    [Fact]
    public void TryGetLocalPathFromUri_AbsoluteFileUri_ReturnsLocalPath()
    {
        string tempPath = System.IO.Path.GetTempPath();
        var fileUri = new Uri(tempPath, UriKind.Absolute);

        string? result = StoragePathHelper.TryGetLocalPathFromUri(fileUri);

        Assert.NotNull(result);
        Assert.Equal(fileUri.LocalPath, result);
    }
}
