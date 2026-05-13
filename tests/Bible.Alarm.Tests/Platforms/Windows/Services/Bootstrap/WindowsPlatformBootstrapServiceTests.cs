#nullable enable

using Bible.Alarm.Platforms.Windows.Services.Bootstrap;

namespace Bible.Alarm.Tests;

[Trait("Platform", "Windows")]
public sealed class WindowsPlatformBootstrapServiceTests
{
    [Fact]
    public async Task InitializeAsync_completes_without_throwing()
    {
        var sut = new WindowsPlatformBootstrapService();

        await sut.InitializeAsync();
    }
}
