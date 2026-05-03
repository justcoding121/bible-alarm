#nullable enable

using CommunityToolkit.Maui.Services;

namespace CommunityToolkit.Maui.MediaElement.Tests;

public sealed class AppPackageServiceTests_windows
{
    [Fact]
    public void FullAppPackageFilePath_returns_non_whitespace_rooted_like_path_under_test_host()
    {
        var path = AppPackageService.FullAppPackageFilePath;

        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.True(Path.IsPathFullyQualified(path), path);
    }

    [Fact]
    public void IsPackagedApp_is_stable_across_reads()
    {
        var first = AppPackageService.IsPackagedApp;
        var second = AppPackageService.IsPackagedApp;

        Assert.Equal(first, second);
    }
}
