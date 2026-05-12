#nullable enable

using Bible.Alarm.Services.Network;

namespace Bible.Alarm.Tests;

public sealed class NetworkStatusServiceTests
{
    [Fact]
    public async Task IsInternetAvailable_returns_resolved_boolean()
    {
        var sut = new NetworkStatusService();
        var task = sut.IsInternetAvailable();
        Assert.True(task.IsCompletedSuccessfully);
        _ = await task;
    }
}
