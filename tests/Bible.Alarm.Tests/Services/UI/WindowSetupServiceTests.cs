#nullable enable

using Bible.Alarm.Services.UI;

namespace Bible.Alarm.Tests;

public sealed class WindowSetupServiceTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new WindowSetupService(
            null!,
            null!,
            null!);

        Assert.NotNull(sut);
    }
}
