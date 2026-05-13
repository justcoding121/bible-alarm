#nullable enable

using Bible.Alarm.Services.Bootstrap;

namespace Bible.Alarm.Tests;

public sealed class DatabaseBootstrapServiceTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new DatabaseBootstrapService(
            null!,
            null!,
            null!);

        Assert.NotNull(sut);
    }
}
