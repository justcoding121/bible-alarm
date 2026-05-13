#nullable enable

using Bible.Alarm.Platforms.Windows.Services.Storage;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsStorageServiceTests
{
    [Fact]
    public void StorageRoot_and_CacheRoot_are_absolute_paths()
    {
        var sut = new WindowsStorageService();

        Assert.True(Path.IsPathRooted(sut.StorageRoot));
        Assert.True(Path.IsPathRooted(sut.CacheRoot));
    }

    [Fact]
    public void MainAssembly_points_at_production_assembly()
    {
        var sut = new WindowsStorageService();

        Assert.Same(typeof(WindowsStorageService).Assembly, sut.MainAssembly);
    }
}
