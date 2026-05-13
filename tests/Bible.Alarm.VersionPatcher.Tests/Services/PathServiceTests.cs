#nullable enable

using Bible.Alarm.VersionPatcher.Services.Infrastructure;

namespace Bible.Alarm.VersionPatcher.Tests;

public sealed class PathServiceTests
{
    [Fact]
    public void GetCsprojPath_filename_is_Bible_Alarm_csproj()
    {
        var sut = new PathService();

        var path = sut.GetCsprojPath();

        Assert.Equal("Bible.Alarm.csproj", Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Android_iOS_Windows_manifest_paths_use_expected_leaf_files()
    {
        var sut = new PathService();

        Assert.EndsWith(
            Path.Combine("Platforms", "Android", "AndroidManifest.xml"),
            sut.GetAndroidManifestPath(),
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(
            Path.Combine("Platforms", "iOS", "Info.plist"),
            sut.GetIosInfoPlistPath(),
            StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(
            Path.Combine("Platforms", "Windows", "Package.appxmanifest"),
            sut.GetWindowsManifestPath(),
            StringComparison.OrdinalIgnoreCase);
    }
}
