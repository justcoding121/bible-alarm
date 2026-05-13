#nullable enable

using Bible.Alarm.Services.UI;
using Bible.Alarm.Tests.Support;

namespace Bible.Alarm.Tests;

public sealed class MessageHandlingServiceTests
{
    [Fact]
    public void Ctor_accepts_dependencies()
    {
        var sut = new MessageHandlingService(
            TestLogging.CreateLogger(),
            null!,
            null!,
            null!);

        Assert.NotNull(sut);
    }
}
