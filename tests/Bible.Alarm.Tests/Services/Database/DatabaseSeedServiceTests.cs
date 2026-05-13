#nullable enable

using Bible.Alarm.Services.Database;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class DatabaseSeedServiceTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new DatabaseSeedService(
            TestLogging.CreateLogger(),
            null!,
            null!,
            null!);

        Assert.NotNull(sut);
    }
}
